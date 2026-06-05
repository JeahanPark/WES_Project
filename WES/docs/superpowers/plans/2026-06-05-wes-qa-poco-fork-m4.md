# WES QA Poco 포크 — M4 (Invoke 브리지 + MPPM 멀티클라) 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. 체크박스(`- [ ]`) 추적.

**Goal:** Python에서 게임 내 `TestManager`(Managers.Test)의 기존 public 메서드를 호출(시나리오 셋업·seed 주입)하고, MPPM 멀티 인스턴스를 인스턴스별 포트로 잡아 멀티플레이를 검증한다. 이로써 효과측정(심은 버그 검출)을 실 게임에 자동 주입으로 완성한다.

**Architecture:** C# `InvokeBridge`가 `Invoke(listener, data)` RPC를 받아 `TestManager.Instance`의 `listener` 이름 public 메서드를 리플렉션으로 찾아 `data`(kwargs)를 파라미터로 매핑해 호출한다. `WesQABootstrap.ResolveInstanceIndex`가 MPPM 가상 플레이어 index를 읽어 포트 `5001+index`로 서버를 띄운다. Python `WesPoco.invoke()`가 브리지를 호출한다.

**Tech Stack:** Unity 6 C# (Reflection, MPPM `com.unity.multiplayer.playmode` 1.6.2 — **이미 설치됨**), 기존 `Assets/WesQA/`·`tools/wesqa/`. 검증 = MCP 플레이모드 + Python.

**확정 사실(코드 확인):**
- `Managers.Test => TestManager.Instance` (`Assets/Scripts/Manager/Managers.cs:105`).
- TestManager public 메서드 다수: `SimulateAddItem(int _itemId)`, `SimulateAddItems(string)`, `SimulateQuickSlot(int)`, `SimulateInventoryToggle()`, `TestForceNight()`, `TestForceDay()`, `TestMpV4_DamageMonster(int=10)`, `TestMpV7_ChangeCold(int=20)`, `TestMultiSpawnForSync()`, `TestMpV2_MovePlayer()` 등. (TestManager는 `#if UNITY_EDITOR` 전용 MonoSingleton.)
- **규칙(프로젝트)**: TestManager에 테스트 전용 로직 신설 금지 — 브리지는 **기존 메서드 호출만**.
- Poco `invoke`: `rpc.call("Invoke", listener=<str>, data=<kwargs dict>)` → C#엔 params가 JObject `{listener, data}`로 도착(배열 아님).

---

## File Structure

- Create: `Assets/WesQA/Runtime/InvokeBridge.cs` — TestManager 리플렉션 호출
- Modify: `Assets/WesQA/Runtime/WesPocoServer.cs` — `Invoke`/`SendMessage`를 kwargs params로 처리(서버에서 가로채거나 RpcMethods 경유)
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs` — Invoke/SendMessage 디스패치(JObject params 접근)
- Modify: `Assets/WesQA/Runtime/WesQABootstrap.cs` — `ResolveInstanceIndex` MPPM 연동
- Modify: `tools/wesqa/wesqa/__init__.py` — `WesPoco.invoke()` + 멀티 인스턴스 헬퍼
- Create: `tools/wesqa/bench/seeds_live.py` — Invoke 기반 실 게임 seed 카탈로그

---

### Task 1: Invoke 브리지 (C# + Python)

**Files:**
- Create: `Assets/WesQA/Runtime/InvokeBridge.cs`
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs`
- Modify: `tools/wesqa/wesqa/__init__.py`

- [ ] **Step 1: InvokeBridge 작성**

`Assets/WesQA/Runtime/InvokeBridge.cs`:
```csharp
using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WesQA
{
    /// <summary>Invoke(listener, data) → TestManager.Instance의 listener 이름 public 메서드를
    /// 리플렉션으로 찾아 data(kwargs)를 파라미터에 매핑해 호출. 기존 메서드 호출만(신규 로직 없음).</summary>
    public static class InvokeBridge
    {
        public static object Invoke(string listener, JObject data)
        {
            var tm = TestManager.Instance;
            if (tm == null) throw new Exception("TestManager.Instance is null");

            var method = tm.GetType().GetMethod(
                listener, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (method == null) throw new Exception("no such TestManager method: " + listener);

            var ps = method.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                JToken tok = data != null ? data[p.Name] : null;
                if (tok == null && data != null && data.Count == ps.Length)
                {
                    // 이름 매칭 실패 시 순서 기반 폴백
                    int idx = 0;
                    foreach (var prop in data.Properties())
                    {
                        if (idx == i) { tok = prop.Value; break; }
                        idx++;
                    }
                }
                if (tok != null) args[i] = tok.ToObject(p.ParameterType);
                else if (p.HasDefaultValue) args[i] = p.DefaultValue;
                else args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }

            object ret = method.Invoke(tm, args);
            return ret; // void면 null
        }
    }
}
```

- [ ] **Step 2: RpcMethods 디스패치 (JObject params)**

`Assets/WesQA/Runtime/RpcMethods.cs`의 `Invoke` switch에 case 추가:
```csharp
                case "Invoke":
                {
                    string listener = req.Params?["listener"]?.ToObject<string>();
                    var data = req.Params?["data"] as Newtonsoft.Json.Linq.JObject;
                    return InvokeBridge.Invoke(listener, data);
                }
                case "SendMessage":
                {
                    string msg = req.Params?["message"]?.ToObject<string>();
                    if (msg == null && req.Args().Count > 0) msg = req.Args()[0].ToObject<string>();
                    return InvokeBridge.Invoke(msg, null);
                }
```
(`req.Params`는 JToken. `Invoke`의 params는 kwargs라 JObject. `req.Params?["listener"]` 접근 가능. RpcRequest에 `Params` 필드가 public인지 확인 — JsonRpc.cs의 `RpcRequest.Params` 사용.)

- [ ] **Step 3: Python WesPoco.invoke()**

`tools/wesqa/wesqa/__init__.py`의 `WesPoco`에 추가:
```python
    def invoke(self, listener, **kwargs):
        """게임 내 TestManager.<listener>(**kwargs)를 호출. 반환값 또는 None."""
        cb = self.agent.rpc.call("Invoke", listener=listener, data=kwargs)
        value, error = cb.wait()
        if error is not None:
            raise RuntimeError("invoke '%s' failed: %s" % (listener, error))
        return value
```

- [ ] **Step 4: 컴파일**

`u_editor_asset(refresh)` → 6초 → `u_console(error)` 에러 0.

- [ ] **Step 5: 라이브 검증 — 리플렉션 호출 성공/실패**

`u_play(enter)` → 5초 → Run (존재 메서드=성공, 없는 메서드=에러):
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "
import sys; sys.path.insert(0,'.')
from wesqa import WesPoco
g=WesPoco(instance=0)
# 존재하는 인자 없는 메서드(씬 무관하게 호출 성공해야 함; 내부 early-return은 무방)
try:
    g.invoke('SimulateInventoryToggle'); print('CALL-OK SimulateInventoryToggle')
except Exception as e: print('CALL-FAIL', e)
# 인자 있는 메서드
try:
    g.invoke('SimulateAddItem', _itemId=1); print('CALL-OK SimulateAddItem(_itemId=1)')
except Exception as e: print('CALL-FAIL', e)
# 없는 메서드 → 에러여야 정상
try:
    g.invoke('NoSuchMethodXYZ'); print('UNEXPECTED-OK')
except Exception as e: print('EXPECTED-ERROR', str(e)[:60])
" 2>&1 | grep -v '\[rpc\]'
```
기대: 앞 둘 `CALL-OK`, 마지막 `EXPECTED-ERROR`(no such TestManager method). 콘솔에 `[TestManager]` 로그가 보이면 메서드 본문 도달 확인(InGameController 없으면 early-return이라 로그 없을 수 있음 — 그래도 CALL-OK면 브리지 정상). `u_play(exit)`.

- [ ] **Step 6: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/Runtime/InvokeBridge.cs WES/Assets/WesQA/Runtime/RpcMethods.cs WES/tools/wesqa/wesqa/__init__.py
git commit -m "WesQA: Invoke 브리지(TestManager 리플렉션 호출) + Python invoke"
```

---

### Task 2: MPPM 인스턴스 인덱스 해석 + 멀티클라 연결 헬퍼

**Files:**
- Modify: `Assets/WesQA/Runtime/WesQABootstrap.cs`
- Modify: `Assets/WesQA/WesQA.asmdef` (MPPM 런타임 어셈블리 참조 필요 시)
- Modify: `tools/wesqa/wesqa/__init__.py` (멀티 인스턴스 헬퍼)

- [ ] **Step 1: MPPM 런타임 API 확인**

MPPM 1.6의 현재 플레이어 식별 런타임 API를 조사한다. 후보: `Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags()` (네임스페이스/메서드는 실제 패키지에서 확인). 메인 에디터(=호스트)는 가상 플레이어 태그가 없고, 가상 플레이어는 MPPM 창에서 부여한 태그를 가진다.
- `Packages/packages-lock.json`·패키지 소스에서 `Unity.Multiplayer.Playmode` 런타임 어셈블리·`CurrentPlayer` API를 확인.
- asmdef `references`에 MPPM 런타임 어셈블리(예: `Unity.Multiplayer.Playmode`)를 추가해야 컴파일되면 추가. **단 Editor 전용 어셈블리이므로 MPPM 런타임 API가 player build에 영향 없어야 함**(WesQA는 includePlatforms Editor라 무관).

- [ ] **Step 2: ResolveInstanceIndex 구현**

`Assets/WesQA/Runtime/WesQABootstrap.cs`의 `ResolveInstanceIndex`를 MPPM 기반으로 교체. 규약: **MPPM 가상 플레이어 태그 중 `wes<N>` 형식**(예: `wes1`,`wes2`)에서 N을 읽어 index로 사용. 메인 에디터(태그 없음/미매칭) = 0. MPPM 미사용/예외 = 0. (정확한 API 호출은 Step 1 조사 결과로 작성. try/catch로 감싸 항상 안전하게 0 폴백.)
```csharp
        private static int ResolveInstanceIndex()
        {
            try
            {
                // MPPM 가상 플레이어 태그에서 "wes<N>" 파싱 → N
                foreach (var tag in /* CurrentPlayer.ReadOnlyTags() 등 */ GetMppmTags())
                {
                    if (tag != null && tag.StartsWith("wes") &&
                        int.TryParse(tag.Substring(3), out int n))
                        return n;
                }
            }
            catch { }
            return 0;
        }
        // GetMppmTags(): MPPM 런타임 API 래퍼(조사 결과로 구현). 실패 시 빈 배열.
```

- [ ] **Step 3: 컴파일 확인**

`u_editor_asset(refresh)` → 6초 → `u_console(error)` 에러 0. (단일 에디터에선 index 0 → 포트 5001 그대로.)

- [ ] **Step 4: 단일 인스턴스 회귀 검증**

`u_play(enter)` → 5초 → `u_console`에 `[WesQA] server starting on port 5001` 확인(인덱스 0). 기존 연결도 정상:
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "import sys;sys.path.insert(0,'.');from wesqa import WesPoco;g=WesPoco(instance=0);print('sdk',g.sdk_version())" 2>&1 | grep -v '\[rpc\]'
```
기대: `sdk wesqa-0.1`. `u_play(exit)`.

- [ ] **Step 5: 멀티 인스턴스 헬퍼 + 2-플레이어 검증(가능 시)**

`tools/wesqa/wesqa/__init__.py`에 추가:
```python
def connect_all(count, host="localhost", base_port=5001, **options):
    """instance 0..count-1을 각각 WesPoco로 연결해 리스트 반환."""
    return [WesPoco(instance=i, host=host, **options) for i in range(count)]
```
MPPM 2-플레이어 검증은 **MPPM 창에서 가상 플레이어 1개를 활성화하고 태그 `wes1`을 부여**해야 한다(에디터 UI 작업). 이 셋업이 자동화로 불가하면, **단일 인스턴스 회귀(Step 4)까지만 검증**하고, 멀티 인스턴스 실연동은 "MPPM 가상 플레이어 셋업 필요"로 **DONE_WITH_CONCERNS** 보고. (코드 경로는 완성, 실 2-인스턴스 검증은 MPPM 세팅 의존.)

- [ ] **Step 6: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/Runtime/WesQABootstrap.cs WES/Assets/WesQA/WesQA.asmdef WES/tools/wesqa/wesqa/__init__.py
git commit -m "WesQA: MPPM 인스턴스 인덱스 해석(포트 5001+index) + 멀티클라 연결 헬퍼"
```

---

### Task 3: 효과측정 자동 주입 — Invoke 기반 실 게임 seed

**Files:**
- Create: `tools/wesqa/bench/seeds_live.py`

- [ ] **Step 1: Invoke 기반 seed 카탈로그 작성**

`tools/wesqa/bench/seeds_live.py`:
```python
# coding=utf-8
"""실 게임 seed 주입(Invoke 브리지 사용) + 동작 검증 — 효과측정 완전자동(M4).
M1 mutation(스냅샷 변조)과 달리 실제 게임 상태를 TestManager로 바꾼 뒤 wesqa로 단언한다.
주의: 대부분의 TestManager 시나리오는 인게임(InGameController) 상태 전제 —
이 모듈은 '인게임 진입 후' 실행하는 것을 가정한다.
"""


def live_seed_demo(game):
    """예시: 아이템 주입 후 인벤토리 토글이 동작하는지(브리지 왕복) 확인.
    반환: (이름, 통과여부) 리스트. 인게임이 아니면 호출은 성공하나 효과는 없을 수 있음."""
    results = []
    # seed 주입: 아이템 추가(인게임에서만 실효)
    try:
        game.invoke("SimulateAddItem", _itemId=1)
        results.append(("SimulateAddItem 호출", True))
    except Exception:
        results.append(("SimulateAddItem 호출", False))
    # 시나리오 트리거: 인벤토리 토글
    try:
        game.invoke("SimulateInventoryToggle")
        results.append(("SimulateInventoryToggle 호출", True))
    except Exception:
        results.append(("SimulateInventoryToggle 호출", False))
    return results
```

- [ ] **Step 2: 라이브 검증(브리지 왕복)**

`u_play(enter)` → 5초 → Run:
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "
import sys; sys.path.insert(0,'.')
from wesqa import WesPoco
from bench.seeds_live import live_seed_demo
g=WesPoco(instance=0)
for name,ok in live_seed_demo(g):
    print(('PASS' if ok else 'FAIL'), name)
" 2>&1 | grep -v '\[rpc\]'
```
기대: 두 호출 `PASS`(브리지가 메서드를 찾아 호출 성공). 인게임이 아니어도 호출 자체는 성공해야 함. `u_play(exit)`.

- [ ] **Step 3: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/tools/wesqa/bench/seeds_live.py
git commit -m "wesqa: Invoke 기반 실 게임 seed 카탈로그(효과측정 자동주입 토대)"
```

---

## M4 완료 기준 (DoG)

- [ ] 컴파일 0
- [ ] Invoke 브리지: 존재 메서드 호출 성공, 없는 메서드 에러
- [ ] MPPM 인덱스 해석 코드 완성 + 단일 인스턴스(포트 5001) 회귀 정상
- [ ] Invoke 기반 seed 카탈로그 브리지 왕복 PASS
- [ ] (스트레치) MPPM 2-플레이어 실연동 — 불가 시 셋업 필요로 명시

## 다음/후속

- MPPM 가상 플레이어 셋업(에디터 UI) 후 멀티플레이 동기화 시나리오(`TestMpV*` via invoke) 실연동
- 인게임 진입 자동화 후 SimulateAddItem 기반 before/after 완전자동 측정
- 진짜 duration LongClick, KeyEvent 키맵 확장
