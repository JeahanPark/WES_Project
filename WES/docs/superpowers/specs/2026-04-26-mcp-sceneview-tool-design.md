# MCP `u_editor_sceneview` 통합 도구 설계

- **작성일**: 2026-04-26
- **대상 저장소**: `C:\GitFork\MCP_Unity` (원본) → `Assets/MCP_Unity_Plugin/` (프로젝트 미러)

## 1. 배경

기존 `u_screenshot`은 `ScreenCapture.CaptureScreenshot()`을 사용해 **Game View** 스크린샷만 캡처한다. Scene View 시점 캡처와 카메라 시점 이동이 불가능하므로, 다음 상황을 처리할 수 없다.

- 플레이 모드가 아닌 상태에서 씬 배치/구도 확인
- 특정 GameObject에 카메라를 정렬하여 시각적으로 검증
- 넓은 영역을 탑뷰/사이드뷰로 조망

본 설계는 Scene View 시점을 제어하고 캡처할 수 있는 통합 MCP 도구 `u_editor_sceneview`를 추가한다.

## 2. 도구 인터페이스

### 도구 이름
`u_editor_sceneview`

### subAction 목록

| subAction | 용도 | 필수 파라미터 | 선택 파라미터 |
|-----------|------|--------------|--------------|
| `screenshot` | 씬뷰 캡처 | — | `screenshotPath` |
| `focus` | 특정 오브젝트로 카메라 정렬 (자동 거리) | `target` | `angle` (`top`/`front`/`side`/`persp`, 기본 `persp`) |
| `preset` | 현재 pivot 유지하며 시점만 전환 | `view` (`top`/`front`/`side`/`persp`) | `size` |
| `get` | 현재 씬뷰 상태 조회 | — | — |

### 시점 프리셋 매핑

`angle` / `view`는 동일한 enum을 공유한다.

| 값 | 회전 (Euler) | 의미 |
|----|-------------|------|
| `top` | `LookRotation(Vector3.down)` | 위에서 아래 방향 |
| `front` | `Euler(0, 180, 0)` | 정면 (-Z 쪽 응시) |
| `side` | `Euler(0, 90, 0)` | 측면 (-X 쪽 응시) |
| `persp` | `Euler(30, 45, 0)` | 일반 원근 |

## 3. 아키텍처

기존 `u_editor_gameobject`(다중 subAction 통합 도구) 패턴을 그대로 따른다.

```
[MCP Client (Claude)]
        ↓ stdio
[MCP Server: SceneViewTool.cs]
        ↓ JSON via Named Pipe (UnityBridgeClient)
[Unity Editor: McpBridgeSceneView.cs (partial McpBridge)]
        ↓ Unity API
[SceneView.lastActiveSceneView]
```

- **MCP 서버 측**: `SceneViewTool.cs`가 단일 도구를 등록하고 파라미터를 JSON 객체로 직렬화하여 `UnityBridgeClient.SendAsync`로 전달
- **Unity 측**: `McpBridge.cs`의 `HandleRequest` switch에 라우팅 추가 → `RouteSceneView(req)`가 `subAction`별 메서드로 분기
- **DTO**: `BridgeRequest`에 신규 필드 추가 (기존 `target`, `screenshotPath`, `subAction` 재사용)

## 4. 파일 변경 목록

### 새 파일

#### 4.1. `C:\GitFork\MCP_Unity\MCP\MCP\MCP\McpBridge\SceneViewTool.cs`

MCP 서버 측 도구 정의. `[McpServerTool(Name = "u_editor_sceneview")]`로 단일 메서드 등록.

```csharp
[McpServerTool(Name = "u_editor_sceneview"), Description(
    "Scene View 통합 도구. subAction: screenshot|focus|preset|get")]
public static async Task<string> SceneView(
    [Description("screenshot|focus|preset|get")] string subAction,
    [Description("focus 대상 GameObject")] string? target = null,
    [Description("preset 시점: top|front|side|persp")] string? view = null,
    [Description("focus 각도: top|front|side|persp (기본 persp)")] string? angle = null,
    [Description("preset size 명시값")] float? size = null,
    [Description("스크린샷 저장 경로")] string? screenshotPath = null)
{
    var command = new {
        action = "u_editor_sceneview",
        subAction,
        target,
        view,
        angle,
        size = size ?? 0f,
        hasSize = size.HasValue,
        screenshotPath
    };
    return await UnityBridgeClient.SendAsync(command);
}
```

#### 4.2. `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge\McpBridgeSceneView.cs`

Unity Editor 측 핸들러 (partial class). 4개 액션 메서드와 회전 매핑 헬퍼.

```csharp
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

    private static SceneView GetActiveSceneView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) sv = EditorWindow.GetWindow<SceneView>();
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
    // ... (각 subAction 메서드 본문은 §5 알고리즘 참조)
}
```

### 수정 파일

#### 4.3. `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\Editor\McpBridge.cs`

- `HandleRequest` switch에 라우팅 한 줄 추가:
  ```csharp
  "u_editor_sceneview"      => RouteSceneView(req),       // McpBridgeSceneView.cs
  ```
- `BridgeRequest` DTO에 필드 추가:
  ```csharp
  public string view;     // u_editor_sceneview preset 시점
  public string angle;    // u_editor_sceneview focus 각도
  public float  size;     // u_editor_sceneview preset size
  public bool   hasSize;  // size 명시 여부
  ```

#### 4.4. `C:\GitFork\MCP_Unity\MCP_Unity_Plugin\README.md`

- 도구 표에 `u_editor_sceneview` 행 추가
- "도구 상세" 섹션에 신규 도구 명세 (subAction별 파라미터 표 + 예시)

#### 4.5. 프로젝트 내 미러링 — `c:\GitFork\WES_Project\WES\Assets\MCP_Unity_Plugin\`

CLAUDE.md MCP Rules: 원본 저장소를 먼저 수정한 뒤 프로젝트로 복사한다.
- `Editor/McpBridge/McpBridgeSceneView.cs` 신규 복사
- `Editor/McpBridge.cs` 수정본 복사
- `README.md` 수정본 복사

## 5. 알고리즘 상세

### 5.1. `screenshot`

```
1. savePath 결정 — _req.screenshotPath 또는 "screenshot_sceneview.png"
2. 디렉터리 자동 생성
3. SceneView 획득 — GetActiveSceneView()
4. 카메라 캡처:
   var cam = sv.camera;
   int w = (int)cam.pixelWidth, h = (int)cam.pixelHeight;
   var rt = new RenderTexture(w, h, 24);
   cam.targetTexture = rt;
   cam.Render();
   RenderTexture.active = rt;
   var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
   tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
   tex.Apply();
   File.WriteAllBytes(savePath, tex.EncodeToPNG());
   // 정리
   cam.targetTexture = null;
   RenderTexture.active = null;
   UnityEngine.Object.DestroyImmediate(rt);
   UnityEngine.Object.DestroyImmediate(tex);
5. BuildSuccess($"스크린샷 저장됨: {savePath}")
```

기즈모/그리드는 SceneView 카메라가 그대로 렌더하므로 포함된다.

### 5.2. `focus`

```
1. target 검증:
   var (go, _) = FindTarget(_req);  // 두 번째 반환값(prefabRoot)은 사용 안 함
   if (go == null) return BuildError($"Target '{_req.target}' not found");
   - prefabPath는 SceneView 도구에서 노출하지 않으므로 항상 씬/프리팹 스테이지 루트만 검색됨
2. 바운딩 박스 계산:
   - Renderer가 있으면: GetComponentsInChildren<Renderer>().bounds 합집합
   - 없으면: new Bounds(transform.position, Vector3.one)
3. 회전 결정 — TryGetRotationByPreset(_req.angle ?? "persp", out rot)
   - 잘못된 값이면 BuildError("Invalid angle...")
4. 거리 자동 계산:
   float distance = Mathf.Max(bounds.extents.magnitude * 1.5f, 1f);
5. SceneView 적용:
   var sv = GetActiveSceneView();
   sv.LookAt(bounds.center, rot, distance);
   sv.Repaint();
6. BuildSuccess($"씬뷰 정렬됨: target={go.name}, pivot=({x:F2},{y:F2},{z:F2}), size={distance:F2}")
   - 기존 u_screenshot처럼 단순 메시지 형태. 정확한 상태값이 필요하면 get으로 조회.
```

### 5.3. `preset`

```
1. view 필수 검증 — TryGetRotationByPreset(_req.view, out rot), 실패 시 BuildError
2. 현재 pivot 유지:
   var sv = GetActiveSceneView();
   Vector3 pivot = sv.pivot;
3. size 결정:
   float size = _req.hasSize ? _req.size : sv.size;
4. 적용:
   sv.LookAt(pivot, rot, size);
   sv.Repaint();
5. BuildSuccess($"씬뷰 시점 전환됨: view={_req.view}, pivot=({x:F2},{y:F2},{z:F2}), size={size:F2}")
```

### 5.4. `get`

```
1. var sv = GetActiveSceneView();
2. JSON 수동 구성 (BuildSuccess는 단순 메시지용이라 직접 작성):
   {
     "success": true,
     "pivot":         { "x": ..., "y": ..., "z": ... },
     "rotation_euler":{ "x": ..., "y": ..., "z": ... },
     "size":          ...,
     "in2DMode":      ...,
     "orthographic":  ...
   }
```

## 6. 에러 처리

| 상황 | 응답 |
|-----|------|
| `subAction` 누락 또는 미지원 | `BuildError("Unknown subAction: ...")` |
| `focus` target not found | `BuildError("Target '<name>' not found")` |
| `focus`/`preset` 잘못된 angle/view | `BuildError("Invalid value. Use: top|front|side|persp")` |
| SceneView 획득 실패 | `GetActiveSceneView()`가 자동 오픈하므로 사실상 발생 X |
| RenderTexture 생성 실패 | try/catch로 감싸 `BuildError(e.Message)` |

## 7. 빌드/적용 절차

1. 원본 저장소(`C:\GitFork\MCP_Unity`)에서 4.1~4.4 변경 적용
2. 프로젝트(`Assets/MCP_Unity_Plugin/`)로 4.2~4.4 복사 (4.5)
3. MCP 서버 재빌드/재시작:
   ```
   C:\GitFork\MCP_Unity\MCP\MCP\MCP\stop_and_rebuild.bat
   ```
4. Unity 측 변경 반영: `u_editor_asset(action: refresh)` 호출

## 8. 검증 (스모크 테스트)

빌드 후 수행할 테스트:

- [ ] `u_editor_sceneview(subAction: "get")` → 현재 pivot/rotation/size JSON 반환 확인
- [ ] `u_editor_sceneview(subAction: "screenshot")` → 프로젝트 루트에 `screenshot_sceneview.png` 생성, 시각적 확인
- [ ] `u_editor_sceneview(subAction: "focus", target: "<씬 내 GameObject>")` → 씬뷰가 해당 오브젝트 중심으로 이동
- [ ] `u_editor_sceneview(subAction: "focus", target: "<같은 오브젝트>", angle: "top")` → 위에서 내려다본 시점 전환
- [ ] `u_editor_sceneview(subAction: "preset", view: "top", size: 100)` → 탑뷰 + 거리 100 적용
- [ ] `u_editor_sceneview(subAction: "preset", view: "invalid")` → 에러 메시지 확인
- [ ] `u_editor_sceneview(subAction: "focus", target: "DoesNotExist")` → 에러 메시지 확인

## 9. 비범위 (YAGNI)

다음 항목은 의도적으로 제외한다.

- **임의 절대좌표 카메라 이동** — 사용자(Claude)가 좌표를 직접 입력할 일이 없음. `focus`/`preset`으로 충족.
- **기즈모 토글 옵션** — 디버깅 목적이라 기즈모 포함이 기본값. 필요해지면 그때 추가.
- **해상도 파라미터** — SceneView 윈도우 크기를 그대로 사용. 필요해지면 그때 추가.
- **다중 SceneView 지원** — `lastActiveSceneView`만 사용. 일반적으로 SceneView는 1개.
- **애니메이션/보간 이동** — 즉시 이동만 지원.
