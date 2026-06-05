# WES QA Poco 포크 — M2 (UI 구동: 입력) 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** wesqa로 실제 버튼을 누르고 텍스트를 입력해 게임 플로우를 구동·검증한다. (M1=읽기 전용 → M2=구동)

**Architecture:** 게임 내 C# `InputInjector`가 Poco 정규화 좌표(top-left 원점)를 스크린 픽셀로 변환해 `EventSystem`으로 합성 포인터 입력을 주입한다. `SetText`는 `_instanceId`로 GameObject를 찾아 InputField/TMP에 텍스트를 넣는다. Python(wesqa)은 이미 포크된 `StdInput`/`StdAttributor`가 해당 RPC를 호출하므로 **C#만 구현**한다. 더불어 M1에서 누락됐던 TMP 텍스트 읽기도 보강한다.

**Tech Stack:** Unity 6 C# (EventSystem, ExecuteEvents, TextMeshPro), 기존 `Assets/WesQA/`. 검증은 MCP 플레이모드 + Python wesqa.

**확정 좌표 계약 (Poco 소스 검증):** click은 `pos=[x,y]`를 **정규화(0~1, top-left 원점)** 로 전송(`pocofw.py:228`, `proxy.get_position`). center focus = `pos`(anchorPoint 0.5 보정 0). InputInjector 변환: `screenX = x*Screen.width`, `screenY = (1-y)*Screen.height`.

**입력 RPC 계약 (`poco/drivers/std/inputs.py`·`attributor.py`):**
`Click(x,y)` · `DoubleClick(x,y)` · `RClick(x,y)` · `LongClick(x,y,duration)` · `Swipe(x1,y1,x2,y2,duration)` · `Scroll(direction,percent,duration)` · `KeyEvent(keycode)` · `SetText(instanceId,text)` → bool.

---

## File Structure

- Create: `Assets/WesQA/Runtime/InputInjector.cs` — 좌표 변환 + EventSystem 입력 주입
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs` — 입력·SetText 디스패치 추가
- Modify: `Assets/WesQA/Runtime/HierarchyDumper.cs` — TMP 텍스트/타입 보강
- Modify: `Assets/WesQA/WesQA.asmdef` — `Unity.TextMeshPro` 참조 추가
- Modify: `Assets/WesQA/Runtime/WesPocoServer.cs` 없음. (펌프 재사용)
- Create: `tools/wesqa/bench/scenarios_action.py` — 동작 회귀 시나리오(효과측정 확장)

---

### Task 1: asmdef TMP 참조 + HierarchyDumper TMP 보강

**Files:**
- Modify: `Assets/WesQA/WesQA.asmdef`
- Modify: `Assets/WesQA/Runtime/HierarchyDumper.cs`

- [ ] **Step 1: asmdef에 TextMeshPro 참조 추가**

`Assets/WesQA/WesQA.asmdef`의 `references` 배열을 다음으로 교체:
```json
  "references": ["Unity.Nuget.Newtonsoft-Json", "Unity.TextMeshPro"],
```

- [ ] **Step 2: HierarchyDumper에 TMP using + 타입/텍스트 보강**

`Assets/WesQA/Runtime/HierarchyDumper.cs` 상단 using에 추가:
```csharp
using TMPro;
```
`ResolveType`의 `Text` 분기 위에 TMP 분기 추가(메서드 내 `if (go.GetComponent<Text>()...` 줄 바로 앞):
```csharp
            if (go.GetComponent<TMP_InputField>() != null) return "InputField";
            if (go.GetComponent<TMP_Text>() != null) return "Text";
```
`ResolveText`를 다음으로 교체:
```csharp
        private static string ResolveText(GameObject go)
        {
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) return tmp.text;
            var t = go.GetComponent<Text>();
            if (t != null) return t.text;
            var tmpInput = go.GetComponent<TMP_InputField>();
            if (tmpInput != null) return tmpInput.text;
            var inp = go.GetComponent<InputField>();
            if (inp != null) return inp.text;
            return null;
        }
```

- [ ] **Step 3: 컴파일 확인 (MCP)**

`u_editor_asset(action: refresh)` → 6초 대기 → `u_console(logType: error)` → 에러 0 확인. (TMP 패키지 미설치 시 `Unity.TextMeshPro` 참조 에러 → `Packages/manifest.json`에 `com.unity.textmeshpro` 또는 `com.unity.ugui` 확인. Unity 6는 TMP가 ugui에 내장 → 참조명이 `Unity.TextMeshPro`가 맞는지 콘솔로 검증, 아니면 올바른 어셈블리명으로 교체.)

- [ ] **Step 4: 플레이 검증 — TMP 텍스트가 덤프에 나오는지**

`u_play(enter)` → 5초 → Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); from wesqa import WesPoco; g=WesPoco(instance=0); root=g.agent.rpc.call('Dump',True).wait()[0]; import json; 
def walk(n):
    p=n.get('payload',{}); 
    if p.get('text'): print(p.get('name'),'=',repr(p.get('text')))
    for c in (n.get('children') or []): walk(c)
walk(root)"
```
Expected: TMP 버튼 라벨 텍스트(예: 'Text (TMP)' 노드의 실제 문자열)가 1개 이상 출력. `u_play(exit)`.

- [ ] **Step 5: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/WesQA.asmdef WES/Assets/WesQA/Runtime/HierarchyDumper.cs
git commit -m "WesQA: TMP 텍스트/타입 덤프 보강(M1 한계 해소) + asmdef TMP 참조"
```

---

### Task 2: InputInjector — Click/DoubleClick/RClick/KeyEvent

**Files:**
- Create: `Assets/WesQA/Runtime/InputInjector.cs`
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs`

- [ ] **Step 1: InputInjector 작성 (좌표변환 + 클릭류)**

`Assets/WesQA/Runtime/InputInjector.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WesQA
{
    /// <summary>Poco 정규화 좌표(top-left 0~1)를 스크린 픽셀로 변환해
    /// EventSystem 합성 포인터 입력을 주입한다. 모든 호출은 메인스레드(서버 펌프)에서 실행됨.</summary>
    public static class InputInjector
    {
        // 정규화(top-left 원점) → 스크린 픽셀(Unity bottom-left 원점)
        private static Vector2 ToScreen(double x, double y)
        {
            return new Vector2((float)(x * Screen.width), (float)((1.0 - y) * Screen.height));
        }

        private static GameObject Raycast(Vector2 screenPos, out RaycastResult hit)
        {
            hit = default;
            if (EventSystem.current == null) return null;
            var ev = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ev, results);
            if (results.Count == 0) return null;
            hit = results[0];
            return hit.gameObject;
        }

        private static PointerEventData MakePointer(Vector2 pos, RaycastResult hit,
            PointerEventData.InputButton button)
        {
            return new PointerEventData(EventSystem.current)
            {
                position = pos,
                button = button,
                pointerPressRaycast = hit,
                pointerCurrentRaycast = hit,
            };
        }

        public static bool Click(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Left, 1);
        public static bool RClick(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Right, 1);
        public static bool DoubleClick(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Left, 2);

        private static bool DoClick(double x, double y, PointerEventData.InputButton button, int count)
        {
            var pos = ToScreen(x, y);
            var go = Raycast(pos, out var hit);
            if (go == null) return false;
            var ev = MakePointer(pos, hit, button);
            ev.clickCount = count;
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerUpHandler);
            var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) ?? go;
            ExecuteEvents.Execute(clickTarget, ev, ExecuteEvents.pointerClickHandler);
            return true;
        }

        // 간단 키 입력: Submit/Cancel 매핑만 우선 지원(확장은 후속)
        public static bool KeyEvent(string keycode)
        {
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            var ev = new BaseEventData(EventSystem.current);
            if (keycode == "enter" || keycode == "submit")
                return ExecuteEvents.Execute(sel, ev, ExecuteEvents.submitHandler);
            if (keycode == "escape" || keycode == "cancel")
                return ExecuteEvents.Execute(sel, ev, ExecuteEvents.cancelHandler);
            return false;
        }
    }
}
```

- [ ] **Step 2: RpcMethods에 클릭류·KeyEvent 디스패치 추가**

`Assets/WesQA/Runtime/RpcMethods.cs`의 `Invoke` switch에 case 추가(기존 `Dump` case 다음):
```csharp
                case "Click":
                    return InputInjector.Click(D(req, 0), D(req, 1));
                case "RClick":
                    return InputInjector.RClick(D(req, 0), D(req, 1));
                case "DoubleClick":
                    return InputInjector.DoubleClick(D(req, 0), D(req, 1));
                case "KeyEvent":
                    return InputInjector.KeyEvent(S(req, 0));
```
그리고 `RpcMethods` 클래스 내부에 인자 헬퍼 추가(`Invoke` 메서드 아래):
```csharp
        private static double D(RpcRequest req, int i)
        {
            var a = req.Args();
            return i < a.Count ? a[i].ToObject<double>() : 0.0;
        }

        private static string S(RpcRequest req, int i)
        {
            var a = req.Args();
            return i < a.Count ? a[i].ToObject<string>() : null;
        }
```

- [ ] **Step 3: 컴파일 확인**

`u_editor_asset(refresh)` → 6초 → `u_console(error)` 에러 0.

- [ ] **Step 4: 플레이 검증 — 실제 클릭이 UI를 바꾸는지**

`u_play(enter)` → 5초 → Run (클릭 전후 트리 변화로 입력 동작 확인):
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys,time; sys.path.insert(0,'.'); from wesqa import WesPoco; g=WesPoco(instance=0)
def names():
    root=g.agent.rpc.call('Dump',True).wait()[0]; out=[]
    def w(n):
        out.append((n.get('payload') or {}).get('name'))
        for c in (n.get('children') or []): w(c)
    w(root); return set(out)
before=names()
print('OptionButton click:', g('OptionButton').click())
time.sleep(0.6)
after=names()
print('new nodes:', sorted(after-before)[:10])
print('CHANGED' if after!=before else 'NO-CHANGE')"
```
Expected: 클릭이 `True`, 그리고 옵션 팝업 등장으로 `CHANGED` + 새 노드 출력. (만약 `NO-CHANGE`면 좌표 변환·raycast 문제 → `Screen.width/height`와 덤프 `pos`의 정규화 기준 일치 재확인. OptionButton 효과가 모호하면 효과가 분명한 다른 버튼으로 교체.) `u_play(exit)`.

- [ ] **Step 5: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/Runtime/InputInjector.cs WES/Assets/WesQA/Runtime/RpcMethods.cs
git commit -m "WesQA: InputInjector 클릭류(Click/RClick/DoubleClick)+KeyEvent — 라이브 클릭 검증"
```

---

### Task 3: SetText (instanceId 조회 + InputField/TMP)

**Files:**
- Modify: `Assets/WesQA/Runtime/InputInjector.cs`
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs`

- [ ] **Step 1: InputInjector에 SetText 추가**

`Assets/WesQA/Runtime/InputInjector.cs`에 using 추가:
```csharp
using TMPro;
using UnityEngine.UI;
```
클래스에 메서드 추가:
```csharp
        /// <summary>_instanceId로 GameObject를 찾아 InputField/TMP_InputField에 텍스트 설정.</summary>
        public static bool SetText(long instanceId, string text)
        {
            var obj = Resources.InstanceIDToObject((int)instanceId) as GameObject;
            if (obj == null) return false;

            var tmp = obj.GetComponent<TMP_InputField>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.onValueChanged.Invoke(text);
                tmp.onEndEdit.Invoke(text);
                return true;
            }
            var input = obj.GetComponent<InputField>();
            if (input != null)
            {
                input.text = text;
                input.onValueChanged.Invoke(text);
                input.onEndEdit.Invoke(text);
                return true;
            }
            return false;
        }
```

- [ ] **Step 2: RpcMethods에 SetText 디스패치 추가**

`Invoke` switch에 case 추가:
```csharp
                case "SetText":
                {
                    var a = req.Args();
                    long id = a.Count > 0 ? a[0].ToObject<long>() : 0;
                    string txt = a.Count > 1 ? a[1].ToObject<string>() : "";
                    return InputInjector.SetText(id, txt);
                }
```

- [ ] **Step 3: 컴파일 확인**

`u_editor_asset(refresh)` → 6초 → `u_console(error)` 에러 0.

- [ ] **Step 4: 플레이 검증 — SetText 왕복**

InputField가 있는 화면이 필요. LoginPopup에 입력칸이 없으면 입력칸이 있는 씬/팝업에서 검증. `u_play(enter)` 후 Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); from wesqa import WesPoco; g=WesPoco(instance=0)
root=g.agent.rpc.call('Dump',True).wait()[0]
# InputField 타입 노드 탐색
target=[]
def w(n):
    p=n.get('payload') or {}
    if p.get('type')=='InputField': target.append(p.get('name'))
    for c in (n.get('children') or []): w(c)
w(root)
print('inputs:', target)
if target:
    name=target[0]
    g(name).set_text('WES_QA_TEST')
    import time; time.sleep(0.3)
    print('readback:', g(name).get_text())
else:
    print('NO_INPUTFIELD_ON_SCREEN — 입력칸 있는 화면에서 재검증 필요')"
```
Expected: InputField 존재 시 `readback: WES_QA_TEST`. 입력칸이 현재 씬에 없으면 `NO_INPUTFIELD...` 출력 → 입력칸 있는 씬에서 재검증(기능 자체는 컴파일·디스패치로 준비됨, DONE_WITH_CONCERNS로 보고하고 입력칸 화면 안내). `u_play(exit)`.

- [ ] **Step 5: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/Runtime/InputInjector.cs WES/Assets/WesQA/Runtime/RpcMethods.cs
git commit -m "WesQA: SetText(instanceId→InputField/TMP) 디스패치"
```

---

### Task 4: Swipe / Scroll (동기 best-effort) + LongClick

**Files:**
- Modify: `Assets/WesQA/Runtime/InputInjector.cs`
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs`

- [ ] **Step 1: InputInjector에 Swipe/Scroll/LongClick 추가**

`Assets/WesQA/Runtime/InputInjector.cs`에 메서드 추가:
```csharp
        public static bool Swipe(double x1, double y1, double x2, double y2, double duration)
        {
            var p1 = ToScreen(x1, y1);
            var p2 = ToScreen(x2, y2);
            var go = Raycast(p1, out var hit);
            if (go == null) return false;
            var target = ExecuteEvents.GetEventHandler<IDragHandler>(go);
            if (target == null) target = go;
            var ev = MakePointer(p1, hit, PointerEventData.InputButton.Left);
            ev.pressPosition = p1;
            ExecuteEvents.Execute(target, ev, ExecuteEvents.beginDragHandler);
            ev.position = p2;
            ev.delta = p2 - p1;
            ExecuteEvents.Execute(target, ev, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target, ev, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerUpHandler);
            return true;
        }

        public static bool Scroll(string direction, double percent, double duration)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var go = Raycast(center, out var hit);
            if (go == null) return false;
            var target = ExecuteEvents.GetEventHandler<IScrollHandler>(go);
            if (target == null) return false;
            var ev = MakePointer(center, hit, PointerEventData.InputButton.Left);
            float amt = (float)percent;
            ev.scrollDelta = direction == "horizontal" ? new Vector2(amt, 0) : new Vector2(0, amt);
            return ExecuteEvents.Execute(target, ev, ExecuteEvents.scrollHandler);
        }

        // LongClick: 메인스레드 블로킹 방지 위해 down/up을 즉시 수행(진짜 hold 지속은 후속 코루틴화).
        public static bool LongClick(double x, double y, double duration)
        {
            return Click(x, y);
        }
```

- [ ] **Step 2: RpcMethods 디스패치 추가**

`Invoke` switch에 case 추가:
```csharp
                case "Swipe":
                    return InputInjector.Swipe(D(req, 0), D(req, 1), D(req, 2), D(req, 3), D(req, 4));
                case "LongClick":
                    return InputInjector.LongClick(D(req, 0), D(req, 1), D(req, 2));
                case "Scroll":
                {
                    var a = req.Args();
                    string dir = a.Count > 0 ? a[0].ToObject<string>() : "vertical";
                    double pct = a.Count > 1 ? a[1].ToObject<double>() : 1.0;
                    double dur = a.Count > 2 ? a[2].ToObject<double>() : 2.0;
                    return InputInjector.Scroll(dir, pct, dur);
                }
```

- [ ] **Step 3: 컴파일 확인**

`u_editor_asset(refresh)` → 6초 → `u_console(error)` 에러 0.

- [ ] **Step 4: 플레이 검증 — 스모크(예외 없이 호출되는지)**

`u_play(enter)` 후 Run (호출 자체가 예외 없이 bool 반환하는지 — 스크롤 대상 없으면 False 정상):
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); from wesqa import WesPoco; g=WesPoco(instance=0)
print('swipe:', g.swipe([0.5,0.6],[0.5,0.4]) if hasattr(g,'swipe') else g.agent.rpc.call('Swipe',0.5,0.6,0.5,0.4,0.2).wait())
print('scroll:', g.agent.rpc.call('Scroll','vertical',0.5,0.5).wait())
print('OK')"
```
Expected: 예외 없이 결과 출력 + `OK`. (실제 스와이프/스크롤 효과 검증은 스크롤 가능한 화면에서 별도. 여기선 디스패치·미예외 확인.) `u_play(exit)`.

- [ ] **Step 5: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/Runtime/InputInjector.cs WES/Assets/WesQA/Runtime/RpcMethods.cs
git commit -m "WesQA: Swipe/Scroll/LongClick 입력 디스패치(동기 best-effort)"
```

---

### Task 5: 효과측정 확장 — 동작 회귀 시나리오

**Files:**
- Create: `tools/wesqa/bench/scenarios_action.py`
- Modify: `tools/wesqa/bench/REPORT.md` (자동 생성으로 갱신은 후속, 여기선 시나리오만)

- [ ] **Step 1: 동작 회귀 체크 작성**

`tools/wesqa/bench/scenarios_action.py`:
```python
# coding=utf-8
"""동작(플로우) 회귀 검증 — 라이브 게임에서 입력→상태변화를 단언(M2).
가짜서버 mutation이 아닌 실 게임 대상. 사용: 플레이모드에서 직접 호출."""


def _names(game):
    root = game.agent.rpc.call("Dump", True).wait()[0]
    out = []

    def w(n):
        out.append((n.get("payload") or {}).get("name"))
        for c in (n.get("children") or []):
            w(c)

    w(root)
    return set(out)


def action_checks():
    # (이름, 동작함수(game)->bool) — 입력 후 UI가 의도대로 변하면 True
    def click_changes_ui(g):
        before = _names(g)
        g("OptionButton").click()
        import time
        time.sleep(0.6)
        return _names(g) != before

    return [
        ("OptionButton 클릭→UI 변화", click_changes_ui),
    ]
```

- [ ] **Step 2: 라이브 동작 검증 실행**

`u_play(enter)` 후 Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); from wesqa import WesPoco; from bench.scenarios_action import action_checks
g=WesPoco(instance=0)
for name,fn in action_checks():
    try: ok=bool(fn(g))
    except Exception as e: ok=False; print('ERR',e)
    print(('PASS' if ok else 'FAIL'), name)"
```
Expected: `PASS OptionButton 클릭→UI 변화`. FAIL이면 효과가 분명한 다른 버튼으로 시나리오 교체. `u_play(exit)`.

- [ ] **Step 3: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/tools/wesqa/bench/scenarios_action.py
git commit -m "wesqa: 동작 회귀 시나리오(입력→UI변화) — 효과측정 M2 확장"
```

---

## M2 완료 기준 (DoD)

- [ ] 컴파일 에러 0 (`u_console`)
- [ ] 라이브 클릭이 실제 UI를 바꿈(`CHANGED`) — 좌표 정합 확인
- [ ] TMP 텍스트가 덤프에 출현(M1 한계 해소)
- [ ] SetText 왕복 또는 입력칸 화면 안내(DONE_WITH_CONCERNS)
- [ ] Swipe/Scroll/LongClick 예외 없이 디스패치
- [ ] 동작 회귀 시나리오 1개 PASS

## 다음 (별도 플랜)

- **M3**: Screenshot + aircv(template matching) + 경량 HTML 리포트 + before/after 자동 표
- **M4**: MPPM 멀티클라 포트 매핑 + `Invoke`/`SendMessage` → `Managers.Test` 브리지(=seed 자동주입)
- **M2 후속**: 진짜 duration 기반 LongClick(펌프 코루틴), KeyEvent 키맵 확장
