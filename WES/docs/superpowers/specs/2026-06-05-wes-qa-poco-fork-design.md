# WES QA 자동화 — Poco/Airtest 최소 포크 설계

- 날짜: 2026-06-05
- 상태: 설계 합의 (구현 계획 전)
- 접근법: **A. 신뢰 분리** (게임 내 C# 자작 + QA측 Python 최소 포크)
- 관련 조사 문서: [`document/research/airtest-poco/`](../../../../document/research/airtest-poco/_index.md)

---

## 1. 목적

게임을 실제로 플레이하며 결정적으로 검증하는 QA 자동화 도구를 만든다. 기존 MCP 기반 QA는 **에디터 내부**에 갇혀 있고 시각 판정이 비결정적(에이전트 눈대중)이다. 이 도구는:

- **유저 관점 E2E** — 실제 버튼 클릭·UI 트리 읽기로 ✅/❌ 결정적 판정
- **멀티클라이언트(Netcode)** — 여러 게임 인스턴스를 동시에 조종해 동기화 검증 (MCP 불가 영역)
- **회귀 자산화** — 한 번 작성한 검증 대본을 영구 재실행
- **중국 코드 0** — 게임에 박히는 부분을 전부 자작, `WES_QA` define로 가드해 Steam 빌드엔 미포함

비목표(YAGNI): Android/iOS/실기기, Standalone 빌드 타깃, AirtestIDE, `.air` 포맷, VR, NGUI.

## 2. 설계 결정 (확정)

| 결정 | 값 | 이유 |
|---|---|---|
| 언어 경계 | QA측 Python(최소 포크) + 게임내 C#(자작) | 검증된 selector/매칭은 재사용, 배포되는 코드만 자작 |
| 연결 대상 | 에디터 플레이모드 + 멀티클라 | 사용자가 선택한 범위 |
| 입력/스크린샷 경로 | `use_airtest_input=False` → 전부 RPC | `airtest` device 백엔드(pywinauto 등) 의존 제거 |
| 신뢰 가드 | `WES_QA` scripting define + 에디터/Development Build 한정 | 릴리스 빌드에 서버·검사 코드 0 |
| 이미지 매칭 | `aircv`의 template matching부터 | keypoint(SIFT 등)는 필요 시 후속 |

## 3. 아키텍처

```
[QA 개발 머신: Python]                      [각 Unity 인스턴스: C# SDK (자작)]
 runner / 에이전트(즉석)
   └ WesPoco (UnityPoco 최소포크) ─TCP JSON-RPC, 포트 5001+i─▶ WesPocoServer (TcpListener)
       ├ selector/proxy  (Poco 재사용)                          ├ HierarchyDumper  uGUI→dump JSON
       ├ aircv           (이미지매칭, 추출)                       ├ InputInjector    EventSystem 주입
       └ report          (경량 자작)                             ├ Screenshotter    RenderTexture→jpg
                                                                 └ InvokeBridge → Managers.Test
```

두뇌(Python)는 QA 머신에서만 돈다. 손(C#)은 게임 런타임 안에 살아 에디터 플레이·복제본 클라 모두에 동일하게 붙는다. 양쪽은 TCP 소켓 + JSON-RPC 2.0으로 통신한다.

## 4. 컴포넌트 — 게임 내 C# SDK (자작, `Assets/WesQA/`)

`WES_QA` define가 있을 때(에디터 또는 Development Build)만 컴파일·기동. 릴리스 빌드는 코드 자체가 제외된다.

### 4.1 WesPocoServer
- `TcpListener`로 포트 `5001 + instanceIndex` 수신 (instanceIndex: 단일=0, 멀티클라=클론/커맨드라인 인자).
- 프레이밍: `[4바이트 little-endian 길이][utf-8 JSON]` (Poco simplerpc 패킷 규약과 동일 — 클라이언트 호환 위해).
- JSON-RPC 2.0 디스패치: `{jsonrpc, method, params, id}` 수신 → 메서드 실행 → `{jsonrpc, result|error, id}` 응답.
- 모든 핸들러는 메인 스레드(Unity API 접근)에서 실행되도록 큐잉. 예외는 JSON-RPC error(-32603)로 변환, 게임을 죽이지 않음.

### 4.2 구현할 RPC 메서드 (실제 Poco std driver 소스에서 추출한 계약)

| 메서드 | 시그니처 | 구현 |
|---|---|---|
| `GetSDKVersion` | `()` → str | 핸드셰이크. 고정 버전 문자열 |
| `Dump` | `(onlyVisibleNode: bool)` → 노드트리 | 4.3 |
| `GetScreenSize` | `()` → `[w, h]` | `Screen.width/height` |
| `Screenshot` | `(width: int)` → `[base64, fmt]` | 캡처→width로 리사이즈→jpg→base64. 대용량이면 `fmt="jpg.deflate"`(zlib) |
| `SetText` | `(instanceId, text)` → bool | `_instanceId`로 노드 찾아 `InputField`/`TMP_InputField`.text 설정 |
| `Click` | `(x, y)` 정규화 | InputInjector |
| `DoubleClick` / `RClick` / `LongClick` | `(x, y[, duration])` | InputInjector |
| `Swipe` | `(x1,y1,x2,y2,duration)` | InputInjector (드래그 시퀀스) |
| `Scroll` | `(direction, percent, duration)` | InputInjector (ScrollRect/스크롤 휠) |
| `KeyEvent` | `(keycode)` | InputInjector |
| `Invoke` | `(listener, data)` → value | InvokeBridge → `Managers.Test`/지정 핸들러 호출, 반환 |
| `SendMessage` | `(message)` | InvokeBridge (단방향) |

> `GetDebugProfilingData`는 std driver가 정의하나 핵심 흐름에 불필요 → 빈 dict 스텁만 둠.

### 4.3 HierarchyDumper (uGUI → dump JSON)
- 모든 `Canvas` 루트부터 RectTransform 트리를 순회.
- 각 노드 payload(Poco 표준 속성, poco-02 문서의 dump 포맷 준수):
  - `name`(GameObject 이름), `type`(컴포넌트 기반: Button/Text/Image/...), `visible`(activeInHierarchy && Canvas 안)
  - `pos`(스크린 중심의 정규화 0~1 좌표), `size`(정규화 w/h), `anchorPoint`, `scale`
  - `zOrders`{global, local}(렌더 순서), `clickable`(Selectable/Button 여부)
  - `text`(Text/TMP 값), `_instanceId`(GetInstanceID — SetText/식별용)
- `onlyVisibleNode=true`면 비가시 노드 가지치기.
- 좌표 정규화 기준 = `GetScreenSize`와 동일해야 Python 좌표 변환이 맞는다.

### 4.4 InputInjector
- 정규화 좌표 → 픽셀 좌표 → `EventSystem` + `PointerEventData`로 합성 입력.
- Click = pointerDown+Up, LongClick = duration 유지, Swipe = drag 이벤트열, Scroll = ScrollRect/`IScrollHandler`.
- 가능하면 좌표 위치의 `Raycast` 대상에 직접 이벤트 디스패치(정확도↑).

### 4.5 Screenshotter
- `ScreenCapture`/RenderTexture로 프레임 캡처 → 요청 width로 리사이즈 → JPG 인코딩 → base64.
- 페이로드가 임계치 초과면 zlib deflate 후 `fmt`에 `.deflate` 접미사(클라가 해제).

### 4.6 InvokeBridge → TestManager
- `Invoke(listener, data)` → 사전 등록된 핸들러 dict에서 `listener` 조회해 호출, 결과 반환.
- WES의 `Managers.Test`(에디터 전용 MonoSingleton)의 기존 public 메서드를 핸들러로 노출 → Python에서 시나리오 셋업/상태 강제 가능. **테스트 전용 로직 신설 금지, 기존 메서드 조합만**(프로젝트 규칙 준수).

## 5. 컴포넌트 — QA측 Python 최소 포크 (`tools/wesqa/`)

### 5.1 포크 범위 (Poco에서 가져오는 것)
- 코어: `poco/pocofw.py`, `proxy.py`, `exceptions.py`, `gesture.py`, `acceleration.py`, `freezeui/`
- SDK 추상: `poco/sdk/*` (AbstractDumper/Node/Attributor/DefaultMatcher/Selector/interfaces/std)
- 드라이버: `poco/drivers/std/*`, `poco/drivers/unity3d/*` — **단, `airtest.core` 의존부를 ip:port 직결로 패치**(아래 5.3)
- 전송: `poco/utils/simplerpc/*` (rpcclient, tcp transport, jsonrpc)

### 5.2 제거하는 것
- 드라이버: android, ios, cocosjs, netease, osx, qt, ue4, windows
- `poco/utils/airtest.py`(AirtestInput) 및 airtest device 전 경로
- vendored 외부물(six 등)은 정식 의존성으로 대체

### 5.3 핵심 패치 — airtest 의존 제거
- `StdPoco`/`UnityPoco`는 원래 `airtest.core.api.connect_device`·`default_device`로 디바이스를 잡고 입력/스크린샷을 airtest device로 보냄.
- 우리 타깃은 고정 `localhost:포트` + `use_airtest_input=False`이므로:
  - 디바이스 탐지 로직 제거, 생성자를 `WesPoco(instance=0, host="localhost")` → `port=5001+instance` 직결로 단순화
  - 입력은 `StdInput`(RPC `Click` 등), 스크린샷은 `StdScreen`(RPC `Screenshot`) 경로만 사용

### 5.4 aircv (Airtest에서 추출)
- `airtest/aircv/*` 복사 (numpy + opencv-python만 의존, airtest.core 비의존).
- 1차: template matching + `cal_confidence`. keypoint(SIFT/AKAZE 등)는 후속.
- 입력 프레임 = `WesPoco.screenshot()`이 받아온 이미지. "이 템플릿이 화면에 있나"를 confidence 임계치로 단언.
- 주의(조사 문서 발견): 함수형 `find_template`은 인자순 `(im_source, im_search)`, 클래스형은 반대. 래퍼로 한 방향 고정.

### 5.5 경량 리포트 (자작 1파일)
- 스텝별 pass/fail + 스크린샷을 단순 HTML로 출력. `airtest.report`(.air 결합·무거움)는 포크하지 않음.

### 5.6 공개 API (사용자 표면)
```python
game = WesPoco(instance=0)              # 5001+instance 직결
game('btn_inventory').click()
assert game('wood_count').text == "3"
game('btn_craft_torch').click()
assert game('torch').exists()
game.screenshot("torch_made.png")
game.invoke("SpawnItem", id="wood", count=3)   # InvokeBridge → Managers.Test
```

## 6. 멀티클라이언트(Netcode)

- 각 게임 인스턴스가 `5001 + instanceIndex` 포트로 서버 기동.
  - 호스트(에디터) = instance 0 → 5001
  - 클라(복제본/Development Build) = instance 1.. → 5002..
- instanceIndex 출처: **Unity 6 MPPM(Multiplayer Play Mode) 공식 API로 가상 플레이어 index 획득** → 포트 매핑(§10-1 확정).
- Python 테스트는 여러 `WesPoco`를 들고 인스턴스 간 상태 일치를 단언:
```python
host   = WesPoco(instance=0)
client = WesPoco(instance=1)
host('btn_drop_item').click()
assert client('dropped_item').exists()   # 동기화 검증
```

## 7. 에러 처리

| 상황 | 처리 |
|---|---|
| 서버 미기동/포트 안 열림 | 연결 재시도(backoff) 후 명확한 에러("instance N SDK 미기동?") |
| 노드 없음 | `PocoNoSuchNodeException`(Poco 재사용) |
| 대기 타임아웃 | `PocoTargetTimeout`. sync_wrapper 30초 → 메서드명 포함 에러 |
| C# 핸들러 예외 | JSON-RPC error(-32603, message). 게임 비크래시 |
| 릴리스 빌드 안전 | `WES_QA` define 부재 시 서버 코드 자체가 컴파일 제외 |

## 8. 테스트 (도구 자체 검증)

- **C# HierarchyDumper**: 알려진 Canvas 프리팹으로 정규화 좌표·트리 구조 단위 검증.
- **C# InputInjector**: 특정 정규화 좌표 클릭이 의도한 노드에 도달하는지.
- **프로토콜 라운드트립**: Python↔C# 스모크 (GetSDKVersion → Dump → Click → Screenshot → Invoke).
- **통합**: 기존 `Managers.Test` 시나리오를 `invoke` 브리지로 구동 (Dev-QA 사이클).

## 9. 마일스톤

| M | 내용 | 결과 |
|---|---|---|
| M1 | C# 서버(TCP+JSON-RPC 프레이밍)+`Dump`/`GetScreenSize`/`GetSDKVersion`, Python 최소포크 연결+selector+attr 읽기 | **UI 라이브 읽기** |
| M2 | InputInjector + `Click/Swipe/.../SetText` | **UI 구동** |
| M3 | Screenshot + aircv template matching + 경량 HTML 리포트 | **시각 검증** |
| M4 | 멀티클라 포트 매핑 + InvokeBridge→`Managers.Test` | **멀티플레이·시나리오 셋업** |
| M5(선택) | 녹화, keypoint matching | 보강 |

## 10. 미해결 (구현 시 확정)

1. ~~멀티 인스턴스 산출 방식~~ → **확정: Unity 6 MPPM**. 가상 플레이어(최대 4) + 공식 API로 player index 획득 → `포트 = 5001 + index`. ParrelSync(풀에디터 복제) 대비 경량·공식·Netcode 지원. *(MPPM player index API 정확한 호출부는 M4 구현 시 확인)*
2. **포크 배치**: `tools/wesqa/` 디렉터리 vs 별도 repo. (현재안: 프로젝트 내 `tools/wesqa/`)
3. **MCP와 공존**: 에디터 플레이모드에서 MCP 입력과 Poco 입력 동시 사용 시 충돌 방지 가이드(둘 중 하나만 입력 주도).
