# MCP `u_editor_sceneview` 통합 도구 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scene View 캡처와 카메라 시점 제어를 단일 MCP 도구 `u_editor_sceneview`로 추가한다.

**Architecture:** 기존 `u_editor_gameobject`와 동일한 통합 도구 패턴 (subAction 분기). MCP 서버 측은 단일 메서드를 등록해 JSON 명령을 전송하고, Unity Editor 측 `partial class McpBridge`에 신규 핸들러 파일을 추가하여 `SceneView.lastActiveSceneView` API로 처리한다.

**Tech Stack:** C# (.NET 8 / Unity 2022+), Model Context Protocol Server SDK, Unity Editor API (`SceneView`, `RenderTexture`, `EditorWindow`)

**Spec:** [docs/superpowers/specs/2026-04-26-mcp-sceneview-tool-design.md](../specs/2026-04-26-mcp-sceneview-tool-design.md)

**Testing 전략:** Unity Editor MCP 코드는 단위 테스트 인프라가 없는 환경(기존 코드 베이스 패턴 동일). 검증은 MCP 서버 재빌드 후 실제 도구 호출로 스모크 테스트한다.

**중요한 작업 규칙:**
- 모든 코드 수정은 **원본 저장소 `C:\GitFork\MCP_Unity` 먼저 → 프로젝트 `Assets/MCP_Unity_Plugin/` 복사** 순서로 진행 (CLAUDE.md MCP Rules)
- 두 저장소 모두 git이 관리: 변경 시 둘 다 커밋 필요
- 커밋 메시지는 한국어, Co-Authored-By 라인 제외 (사용자 메모리 기준)

---

## File Structure

### 신규 파일 (원본 저장소)
- `C:\GitFork\MCP_Unity\MCP\MCP\MCP\McpBridge\SceneViewTool.cs` — MCP 서버 측 도구 정의
- `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge\McpBridgeSceneView.cs` — Unity Editor 측 핸들러 (partial class)

### 수정 파일 (원본 저장소)
- `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge.cs` — DTO 필드 추가 + 라우팅 한 줄
- `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\README.md` — 도구 문서화

### 미러링 (프로젝트)
- `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\Editor\McpBridge\McpBridgeSceneView.cs` (신규 복사)
- `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\Editor\McpBridge.cs` (덮어쓰기)
- `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\README.md` (덮어쓰기)

---

## Task 1: BridgeRequest DTO에 신규 필드 추가

**Files:**
- Modify: `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge.cs` (BridgeRequest 클래스 내부, 약 line 482 부근 `screenshotPath` 다음)

- [ ] **Step 1: `screenshotPath` 다음에 4개 필드 추가**

기존 `BridgeRequest` 클래스의 마지막 필드 뒤(또는 `screenshotPath` 다음)에 아래 코드 삽입:

```csharp
        public string view;     // u_editor_sceneview preset 시점 (top/front/side/persp)
        public string angle;    // u_editor_sceneview focus 각도 (top/front/side/persp)
        public float  size;     // u_editor_sceneview preset size 명시값
        public bool   hasSize;  // size 명시 여부
```

검증: 파일 컴파일 가능한지 syntax 확인 (실제 빌드는 마지막에).

---

## Task 2: McpBridge.cs HandleRequest 라우팅 추가

**Files:**
- Modify: `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge.cs` (HandleRequest switch, 약 line 270 `u_screenshot` 다음)

- [ ] **Step 1: switch 문에 라우팅 한 줄 추가**

기존:
```csharp
            "u_screenshot"            => Screenshot(req),           // McpBridgeScreenshot.cs
            _                         => BuildError($"Unknown action: '{req.action}'")
```

수정 후:
```csharp
            "u_screenshot"            => Screenshot(req),           // McpBridgeScreenshot.cs
            "u_editor_sceneview"      => RouteSceneView(req),       // McpBridgeSceneView.cs
            _                         => BuildError($"Unknown action: '{req.action}'")
```

---

## Task 3: McpBridgeSceneView.cs 신규 핸들러 작성

**Files:**
- Create: `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge\McpBridgeSceneView.cs`

- [ ] **Step 1: 파일 작성**

전체 내용:

```csharp
// McpBridgeSceneView.cs
// u_editor_sceneview 핸들러 — Scene View 캡처 + 카메라 시점 제어

using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    private static string RouteSceneView(BridgeRequest _req)
    {
        string sub = (_req.subAction ?? "").ToLowerInvariant();
        return sub switch
        {
            "screenshot" => SceneViewScreenshot(_req),
            "focus"      => SceneViewFocus(_req),
            "preset"     => SceneViewPreset(_req),
            "get"        => SceneViewGet(_req),
            _            => BuildError($"Unknown subAction: '{_req.subAction}'. Use: screenshot|focus|preset|get")
        };
    }

    // ---- subAction 핸들러 ----

    private static string SceneViewScreenshot(BridgeRequest _req)
    {
        string savePath = _req.screenshotPath;
        if (string.IsNullOrEmpty(savePath))
            savePath = Path.Combine(Directory.GetCurrentDirectory(), "screenshot_sceneview.png");

        string dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        var cam = sv.camera;
        if (cam == null) return BuildError("SceneView 카메라가 없습니다");

        int w = Mathf.Max((int)cam.pixelWidth, 1);
        int h = Mathf.Max((int)cam.pixelHeight, 1);

        RenderTexture rt = null;
        Texture2D tex = null;
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        try
        {
            rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(savePath, png);
        }
        catch (System.Exception e)
        {
            return BuildError($"스크린샷 실패: {e.Message}");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            if (rt != null) Object.DestroyImmediate(rt);
            if (tex != null) Object.DestroyImmediate(tex);
        }

        return BuildSuccess($"씬뷰 스크린샷 저장됨: {savePath}");
    }

    private static string SceneViewFocus(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required for focus");

        var (go, _) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        if (!TryGetRotationByPreset(_req.angle ?? "persp", out Quaternion rot))
            return BuildError($"Invalid angle '{_req.angle}'. Use: top|front|side|persp");

        Bounds bounds = ComputeBounds(go);
        float distance = Mathf.Max(bounds.extents.magnitude * 1.5f, 1f);

        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        sv.LookAt(bounds.center, rot, distance);
        sv.Repaint();

        return BuildSuccess(
            $"씬뷰 정렬됨: target={go.name}, pivot=({bounds.center.x:F2},{bounds.center.y:F2},{bounds.center.z:F2}), size={distance:F2}");
    }

    private static string SceneViewPreset(BridgeRequest _req)
    {
        if (!TryGetRotationByPreset(_req.view, out Quaternion rot))
            return BuildError($"Invalid view '{_req.view}'. Use: top|front|side|persp");

        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        Vector3 pivot = sv.pivot;
        float size = _req.hasSize ? _req.size : sv.size;

        sv.LookAt(pivot, rot, size);
        sv.Repaint();

        return BuildSuccess(
            $"씬뷰 시점 전환됨: view={_req.view}, pivot=({pivot.x:F2},{pivot.y:F2},{pivot.z:F2}), size={size:F2}");
    }

    private static string SceneViewGet(BridgeRequest _req)
    {
        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        Vector3 pivot = sv.pivot;
        Vector3 euler = sv.rotation.eulerAngles;
        return "{" +
            "\"success\":true," +
            $"\"pivot\":{{\"x\":{pivot.x:F4},\"y\":{pivot.y:F4},\"z\":{pivot.z:F4}}}," +
            $"\"rotation_euler\":{{\"x\":{euler.x:F4},\"y\":{euler.y:F4},\"z\":{euler.z:F4}}}," +
            $"\"size\":{sv.size:F4}," +
            $"\"in2DMode\":{(sv.in2DMode ? "true" : "false")}," +
            $"\"orthographic\":{(sv.orthographic ? "true" : "false")}" +
            "}";
    }

    // ---- 헬퍼 ----

    private static SceneView GetActiveSceneView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null)
        {
            sv = EditorWindow.GetWindow<SceneView>(utility: false, title: null, focus: false);
        }
        return sv;
    }

    private static bool TryGetRotationByPreset(string _key, out Quaternion _rot)
    {
        switch ((_key ?? "persp").ToLowerInvariant())
        {
            case "top":   _rot = Quaternion.LookRotation(Vector3.down); return true;
            case "front": _rot = Quaternion.Euler(0, 180, 0);          return true;
            case "side":  _rot = Quaternion.Euler(0, 90, 0);           return true;
            case "persp": _rot = Quaternion.Euler(30, 45, 0);          return true;
            default:      _rot = Quaternion.identity;                   return false;
        }
    }

    private static Bounds ComputeBounds(GameObject _go)
    {
        var renderers = _go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(_go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
```

검증: 컴파일 syntax 확인. Unity Editor API 사용은 `using UnityEditor; using UnityEngine;` 두 줄로 충분.

---

## Task 4: SceneViewTool.cs 신규 MCP 서버 도구 작성

**Files:**
- Create: `C:\GitFork\MCP_Unity\MCP\MCP\MCP\McpBridge\SceneViewTool.cs`

- [ ] **Step 1: 파일 작성**

전체 내용:

```csharp
// SceneViewTool.cs
// u_editor_sceneview 도구 구현 — Scene View 통합 (캡처 + 카메라 제어)

using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

[McpServerToolType]
public static class SceneViewTool
{
    [McpServerTool(Name = "u_editor_sceneview"), Description(
        "Scene View 통합 도구. 씬뷰 캡처 및 카메라 시점 제어를 처리한다. " +
        "subAction: screenshot|focus|preset|get")]
    public static async Task<string> SceneView(
        [Description("subAction: screenshot|focus|preset|get")] string subAction,
        [Description("focus 대상 GameObject 이름 또는 경로")] string? target = null,
        [Description("preset 시점: top|front|side|persp")] string? view = null,
        [Description("focus 각도: top|front|side|persp (기본: persp)")] string? angle = null,
        [Description("preset size 명시값 (생략 시 현재 size 유지)")] float? size = null,
        [Description("스크린샷 저장 경로 (생략 시 프로젝트 루트의 screenshot_sceneview.png)")] string? screenshotPath = null)
    {
        return await UnityBridgeClient.SendAsync(new
        {
            action = "u_editor_sceneview",
            subAction,
            target,
            view,
            angle,
            size = size ?? 0f,
            hasSize = size.HasValue,
            screenshotPath
        });
    }
}
```

---

## Task 5: README.md 도구 문서화

**Files:**
- Modify: `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\README.md`

- [ ] **Step 1: 도구 표에 행 추가**

먼저 README.md를 읽어 도구 표 위치 확인. 표는 `u_screenshot` 행이 포함된 부분이며, `u_screenshot` 바로 위 또는 적절한 위치에 다음 행 추가:

```markdown
| `u_editor_sceneview` | Scene View 캡처 + 카메라 시점 제어 | O |
```

- [ ] **Step 2: "도구 상세" 섹션에 신규 도구 명세 추가**

`### u_screenshot` 섹션 바로 위(또는 다른 `u_editor_*` 도구 인접 위치)에 다음 섹션 삽입:

```markdown
### u_editor_sceneview

Scene View(씬뷰) 캡처 및 카메라 시점 제어를 통합 처리한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `subAction` | O | `screenshot` / `focus` / `preset` / `get` |
| `target` | △ | `focus` 대상 GameObject 이름/경로 (`focus` 필수) |
| `view` | △ | 시점 (`top` / `front` / `side` / `persp`) — `preset` 필수 |
| `angle` | X | `focus` 각도 — 동일 enum, 기본값 `persp` |
| `size` | X | `preset` 거리 (생략 시 현재 size 유지) |
| `screenshotPath` | X | `screenshot` 저장 경로 (생략 시 프로젝트 루트) |

```
# 예시
subAction: "screenshot"
subAction: "screenshot",  screenshotPath: "C:/temp/sceneview.png"
subAction: "focus",       target: "Player"
subAction: "focus",       target: "Player",          angle: "top"
subAction: "preset",      view: "top",                size: 100
subAction: "get"
```

**시점 매핑:**
- `top` — 위에서 아래
- `front` — 정면 (-Z)
- `side` — 측면 (-X)
- `persp` — 일반 원근 (Euler 30, 45, 0)

`focus`는 대상 오브젝트의 Renderer bounds로 자동 거리 계산 후 카메라를 정렬한다.
`preset`은 현재 pivot을 유지하며 시점만 전환한다.
`get`은 현재 씬뷰의 pivot/rotation/size 등 상태를 JSON으로 반환한다.

---
```

---

## Task 6: 프로젝트로 미러링 (Assets/MCP_Unity_Plugin/)

**Files:**
- Create: `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\Editor\McpBridge\McpBridgeSceneView.cs`
- Modify: `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\Editor\McpBridge.cs`
- Modify: `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\README.md`

- [ ] **Step 1: McpBridgeSceneView.cs 복사**

원본 → 프로젝트:
```bash
cp "C:/GitFork/MCP_Unity/MCP_Unity_Plugin/Editor/McpBridge/McpBridgeSceneView.cs" \
   "c:/GitFork/WES_Project/WES/Assets/MCP_Unity_Plugin/Editor/McpBridge/McpBridgeSceneView.cs"
```

- [ ] **Step 2: McpBridge.cs 복사**

```bash
cp "C:/GitFork/MCP_Unity/MCP_Unity_Plugin/Editor/McpBridge.cs" \
   "c:/GitFork/WES_Project/WES/Assets/MCP_Unity_Plugin/Editor/McpBridge.cs"
```

- [ ] **Step 3: README.md 복사**

```bash
cp "C:/GitFork/MCP_Unity/MCP_Unity_Plugin/README.md" \
   "c:/GitFork/WES_Project/WES/Assets/MCP_Unity_Plugin/README.md"
```

- [ ] **Step 4: 복사 확인**

```bash
ls "c:/GitFork/WES_Project/WES/Assets/MCP_Unity_Plugin/Editor/McpBridge/McpBridgeSceneView.cs"
```
Expected: 파일 존재, 크기 > 0

---

## Task 7: MCP 서버 재빌드 및 재시작

**Files:**
- Run: `C:\GitFork\MCP_Unity\MCP\MCP\MCP\stop_and_rebuild.bat`

- [ ] **Step 1: 배치 스크립트 실행**

```bash
cmd.exe //c "C:\GitFork\MCP_Unity\MCP\MCP\MCP\stop_and_rebuild.bat"
```

Expected: 빌드 성공 메시지 출력. 에러 발생 시 컴파일 오류 위치 확인하고 Task 1~4 코드 재검토.

---

## Task 8: Unity 에셋 새로고침

- [ ] **Step 1: Unity Editor에 변경 반영**

```
u_editor_asset(action: "refresh")
```

Expected: success 응답. McpBridgeSceneView.cs가 Unity에서 컴파일됨.

- [ ] **Step 2: 콘솔 에러 확인**

```
u_console(logType: "error", maxCount: 20)
```

Expected: 새로 도입한 코드 관련 에러 없음. 있다면 메시지 보고 수정.

---

## Task 9: 스모크 테스트

도구가 실제로 작동하는지 검증한다. 각 호출은 success 응답을 받아야 한다.

- [ ] **Step 1: `get` — 현재 상태 조회**

```
u_editor_sceneview(subAction: "get")
```

Expected: `{"success":true, "pivot":{...}, "rotation_euler":{...}, "size":..., "in2DMode":..., "orthographic":...}` 형태 JSON.

- [ ] **Step 2: `screenshot` — 기본 경로 캡처**

```
u_editor_sceneview(subAction: "screenshot")
```

Expected: `씬뷰 스크린샷 저장됨: <project root>/screenshot_sceneview.png` 메시지. 파일 존재 확인:

```bash
ls "c:/GitFork/WES_Project/WES/screenshot_sceneview.png"
```

- [ ] **Step 3: `preset` — 탑뷰 전환**

```
u_editor_sceneview(subAction: "preset", view: "top", size: 100)
```

Expected: `씬뷰 시점 전환됨: view=top, pivot=(...), size=100.00` 메시지.

- [ ] **Step 4: `focus` — 씬에 존재하는 GameObject로 정렬**

먼저 씬 루트 1개를 찾는다:
```
u_editor_gameobject(action: "hierarchy", target: "")
```

위에서 발견한 GameObject 이름으로:
```
u_editor_sceneview(subAction: "focus", target: "<해당 이름>")
```

Expected: `씬뷰 정렬됨: target=...` 메시지.

- [ ] **Step 5: 잘못된 입력 — 에러 응답 확인**

```
u_editor_sceneview(subAction: "preset", view: "invalid")
```

Expected: `Invalid view 'invalid'. Use: top|front|side|persp` 에러.

```
u_editor_sceneview(subAction: "focus", target: "DoesNotExist_xyz")
```

Expected: `Target 'DoesNotExist_xyz' not found` 에러.

---

## Task 10: 커밋 (두 저장소)

- [ ] **Step 1: 원본 MCP 저장소 커밋**

```bash
cd C:/GitFork/MCP_Unity
git add MCP/MCP/MCP/McpBridge/SceneViewTool.cs \
        MCP_Unity_Plugin/Editor/McpBridge/McpBridgeSceneView.cs \
        MCP_Unity_Plugin/Editor/McpBridge.cs \
        MCP_Unity_Plugin/README.md
git commit -m "Scene View 통합 도구 u_editor_sceneview 추가

- subAction: screenshot/focus/preset/get
- 씬뷰 캡처: SceneView 카메라를 RenderTexture로 렌더링 후 PNG 저장
- 카메라 제어: focus(대상 자동 정렬), preset(시점 전환), get(상태 조회)
- 시점 프리셋 4종: top/front/side/persp"
```

- [ ] **Step 2: 프로젝트 저장소 커밋**

```bash
cd c:/GitFork/WES_Project/WES
git add Assets/MCP_Unity_Plugin/Editor/McpBridge/McpBridgeSceneView.cs \
        Assets/MCP_Unity_Plugin/Editor/McpBridge.cs \
        Assets/MCP_Unity_Plugin/README.md \
        docs/superpowers/specs/2026-04-26-mcp-sceneview-tool-design.md \
        docs/superpowers/plans/2026-04-26-mcp-sceneview-tool.md
git commit -m "MCP u_editor_sceneview 도구 추가 (씬뷰 캡처/카메라 제어)

원본 저장소(C:/GitFork/MCP_Unity)에서 추가한 Scene View 통합 도구를
프로젝트 미러로 복사. 설계/구현 계획 문서 동반.

- subAction: screenshot/focus/preset/get
- 시점 프리셋: top/front/side/persp"
```

- [ ] **Step 3: 커밋 결과 확인**

각 저장소에서:
```bash
git log -1 --oneline
git status
```

Expected: 새 커밋이 마지막에 보이고 working tree clean.

---

## 완료 기준

- [ ] `u_editor_sceneview` 4개 subAction 모두 실제 호출에서 정상 응답
- [ ] 씬뷰 스크린샷 PNG가 디스크에 생성됨
- [ ] 잘못된 입력에 대해 명확한 에러 메시지 반환
- [ ] Unity 콘솔에 신규 코드 관련 에러/경고 없음
- [ ] 두 저장소 모두 커밋 완료
