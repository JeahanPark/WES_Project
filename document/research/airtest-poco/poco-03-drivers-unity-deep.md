---
type: reference
source: Poco-master (AirtestProject/Poco)
generated: subagent
---

# Poco Drivers 레퍼런스 — Unity3D 심층 + 전체 드라이버 개요

대상 저장소: `C:\Users\cgq02\Downloads\Poco-master\Poco-master`
범위: `poco/drivers/` 전체. **`poco/drivers/unity3d/` 를 가장 깊게** 문서화. 나머지 드라이버는 연결 방식·대상·차이만 요약.

> 이 문서는 **Python 클라이언트 측 드라이버** 저장소만 다룬다. Unity C# 측 SDK(`PocoManager.cs`, `poco-sdk`)는 이 저장소에 **없다** (별도 저장소 `https://github.com/AirtestProject/Poco-SDK`). 본 문서 후반의 "Unity 프로젝트에 임베드해야 하는 것" 절에서 Python 드라이버가 기대하는 서버 측 동작을 소스에서 역추적해 명세한다.

---

## 0. 큰 그림 (계층 구조)

UnityPoco 는 std 프로토콜(StdPoco)을 그대로 쓰는 가장 단순한 TCP-RPC 드라이버다.

```
UnityPoco                       (poco/drivers/unity3d/unity3d_poco.py)
  └─ StdPoco                     (poco/drivers/std/__init__.py)
       └─ Poco                   (poco/pocofw.py)  ← 모든 드라이버 공통 상위
       └─ StdPocoAgent           (poco/drivers/std/__init__.py) ← PocoAgent 구현
            ├─ FrozenUIHierarchy (poco/freezeui/hierarchy.py)
            │     ├─ StdDumper   (poco/drivers/std/dumper.py)     → RPC "Dump"
            │     └─ StdAttributor (poco/drivers/std/attributor.py) → RPC "SetText"
            ├─ AirtestInput      (poco/utils/airtest/input.py)  ← 기본 입력(use_airtest_input=True)
            │     또는 StdInput  (poco/drivers/std/inputs.py)   → RPC "Click"/"Swipe"...
            ├─ StdScreen         (poco/drivers/std/screen.py)   → RPC "Screenshot"
            └─ RpcClient         (poco/utils/simplerpc/rpcclient.py)
                 └─ TcpClient    (poco/utils/simplerpc/transport/tcp/main.py)
                      └─ safetcp.Client (poco/utils/simplerpc/transport/tcp/safetcp.py)
                      └─ SimpleProtocolFilter (.../tcp/protocol.py) ← [4byte len][payload]
```

핵심: UnityPoco 자체는 RPC 메서드를 거의 추가하지 않는다 (`SendMessage`, `Invoke` 만). 실제 RPC 동작(`Dump`, `Click`, `Screenshot`, `SetText`, `GetScreenSize`, `GetSDKVersion`)은 모두 std 계층이 정의한다. 따라서 **Unity SDK 가 구현해야 하는 서버 측 계약 = std 프로토콜 계약**이다.

---

## 1. UnityPoco (드라이버 진입점)

파일: `poco/drivers/unity3d/unity3d_poco.py`

### 1.1 모듈 상수

| 상수 | 값 | 설명 |
|------|----|------|
| `DEFAULT_PORT` | `5001` | Unity 게임 런타임의 PocoManager 가 listen 하는 TCP 포트 기본값 |
| `DEFAULT_ADDR` | `("localhost", 5001)` | 기본 endpoint (host, port) 튜플 |

(`unity3d_poco.py:12-13`)

### 1.2 `class UnityPoco(StdPoco)`

```python
def __init__(self, addr=DEFAULT_ADDR, unity_editor=False,
             connect_default_device=True, device=None, **options):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `addr` | `tuple(str, int)` | `("localhost", 5001)` | Unity3D 게임 런타임의 endpoint. `addr[0]`=ip, `addr[1]`=port. 안드로이드/Windows player 의 경우 ip는 실질적으로 무시됨(아래 §2.2 참조), port만 의미. |
| `unity_editor` | `bool` | `False` | `True`면 UnityEditor Play 모드(Windows의 GameView)에 붙음. `UnityEditorWindow()` 디바이스를 생성. |
| `connect_default_device` | `bool` | `True` | 수동 선택한 디바이스가 없을 때 기본 디바이스(Android)에 자동 연결할지 |
| `device` | `airtest.core.device.Device` | `None` | airtest 디바이스 객체. 생략 시 현재 디바이스 사용 |
| `**options` | | | `Poco` 로 전달 (`action_interval` 등, §10 참조) |

핵심 동작 (`unity3d_poco.py:66-82`):
1. `options['action_interval']` 미지정 시 **`0.5`** 로 강제 (std 기본 0.8 과 다름).
2. `unity_editor=True` → `dev = UnityEditorWindow()` (Windows GameView 핸들).
3. 그 외 → `dev = device or current_device()`.
4. `dev`가 None이고 `connect_default_device`면 `connect_device("Android:///")` 로 **안드로이드 자동 연결** (코드 주석: "currently only connect to Android as default").
5. `super().__init__(addr[1], dev, ip=addr[0], **options)` — **port와 ip를 분리**해서 StdPoco 에 전달.

주의점:
- 생성자에서 `self.vr = UnityVRSupport(...)` 는 **주석 처리됨**(`:82`). 일부 디바이스에서 초기화 실패 시 UI 트리를 못 가져오는 문제 때문. 즉 현재 `poco.vr` 속성은 코드상 **존재하지 않는다** — 단, `doc/unity3d_vr.rst` 튜토리얼은 `vr = poco.vr` 를 전제로 쓰여 있으므로 문서와 코드가 불일치(코드가 정답).
- `ip`는 `("", 5001)` 처럼 빈 문자열로 줘도 동작(문서 예제 다수가 `addr=('', 5001)` 사용). StdPoco 가 빈/`localhost` ip를 디바이스 기반으로 재계산하기 때문(§2.2).

### 1.3 `UnityPoco.send_message(message)`

```python
def send_message(self, message):
    self.agent.rpc.call("SendMessage", message)
```
(`unity3d_poco.py:84-85`)

- RPC `"SendMessage"` 한 개의 string 인자로 호출. **콜백을 wait하지 않음(fire-and-forget)**.
- Unity 측에서 `PocoManager.MessageReceived` 이벤트로 수신 (`doc/drivers/unity3d.rst:148-172`).
- 이 기능은 poco-sdk PR #123 이상이 필요 (`doc/drivers/unity3d.rst:146`).

### 1.4 `UnityPoco.invoke(listener, **kwargs)`

```python
def invoke(self, listener, **kwargs):
    callback = self.agent.rpc.call("Invoke", listener=listener, data=kwargs)
    value, error = callback.wait()
    if error is not None:
        raise Exception(error)
    return value
```
(`unity3d_poco.py:87-95`)

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `listener` | `str` | Unity 측 `[PocoMethod("name")]` 의 등록 이름 |
| `**kwargs` | dict | Unity 측 메서드로 전달될 custom 인자. RPC `params` 의 `data` 필드로 직렬화 |

반환: Unity 메서드의 반환값(value). error 가 있으면 `Exception` raise.
핵심 동작: RPC `"Invoke"` 를 `params={listener, data}` 로 호출 후 `.wait()` 로 동기 대기.
Unity 측 계약 (`doc/drivers/unity3d.rst:191-206`):
1. `PocoListenerBase` 상속 클래스를 만들고
2. `[PocoMethod("say_hello")] public void SayHello(string name, int year)` 같은 메서드 추가
3. `PocoManager` 에 그 클래스 참조 등록.

예: `poco.invoke(listener="say_hello", name="anonymous", year=2024)`.

### 1.5 `class UnityVRSupport` (현재 비활성)

파일 동일 (`unity3d_poco.py:16-37`). 생성자에서 RPC `"isVrSupported"` 호출, 실패 시 `InvalidOperationException('VR not supported')`.

VR RPC 메서드 (모두 `self.client.call(...)` 직접 호출):

| 메서드 | RPC 이름 | 인자 | 비고 |
|--------|----------|------|------|
| `hasMovementFinished()` | `"hasMovementFinished"` | — | `.wait()` 동기. success!=None 이면 True |
| `rotateObject(x,y,z,camera,follower,speed=0.125)` | `"RotateObject"` | x,y,z,camera,follower,speed | |
| `objectLookAt(name,camera,follower,speed=0.125)` | `"ObjectLookAt"` | name,camera,follower,speed | |

주의: `UnityPoco.__init__` 에서 `self.vr` 가 주석 처리되어 있어 **이 클래스는 현재 인스턴스화되지 않는다**. VR 자동화를 쓰려면 `:82` 의 주석을 해제하거나 직접 `UnityVRSupport(poco.agent.rpc)` 를 생성해야 한다. `doc/unity3d_vr.rst` 가 이 기능의 사용 시나리오(Google VR + Android 조합, CameraContainer/CameraFollower 오브젝트)를 설명.

---

## 2. UnityEditorWindow 디바이스 & StdPoco 연결 메커니즘

### 2.1 `UnityEditorWindow()`

파일: `poco/drivers/unity3d/device.py:6-11`

```python
def UnityEditorWindow():
    dev = connect_device("Windows:///?class_name=UnityContainerWndClass&title_re=.*Unity.*")
    game_window = dev.app.top_window().child_window(title="UnityEditor.GameView")
    dev._top_window = game_window.wrapper_object()
    dev.focus_rect = (0, 40, 0, 0)
    return dev
```

- airtest `Windows` 디바이스로 Unity 에디터 창(`class_name=UnityContainerWndClass`, 제목에 `Unity` 포함)을 잡는다.
- pywinauto 로 자식 창 `UnityEditor.GameView` 를 찾아 `_top_window` 로 지정 → **입력 좌표가 GameView 영역 기준**이 됨.
- `focus_rect = (0, 40, 0, 0)` — 상단 40px 오프셋(에디터 GameView 의 탭/툴바 영역 보정).
- 즉 **에디터 Play 모드에 붙는 방식 = 창 핸들 입력 주입**. RPC 연결은 별개로 PocoManager 가 게임 런타임(에디터 Play 중)에서 5001 포트를 listen 함.

비교 — `poco/drivers/ue4/device.py` 의 `UE4EditorWindow()` 는 동일 패턴, 단 `class_name=UnrealWindow`, `title_re=.*Game Preview Standalone.*`, `focus_rect=(5,29,5,5)`.

### 2.2 `StdPoco.__init__` — ip/port 재계산 (Unity 연결의 실제 핵심)

파일: `poco/drivers/std/__init__.py:84-112`

```python
def __init__(self, port=DEFAULT_PORT, device=None, use_airtest_input=True, ip=None, **kwargs):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `port` | int | `15004`(std) / `5001`(unity가 전달) | 타겟 디바이스에서 서버가 listen 하는 포트 |
| `device` | Device | `None` | airtest 디바이스 |
| `use_airtest_input` | bool | `True` | True면 AirtestInput(좌표 입력을 airtest touch/swipe로), False면 StdInput(RPC Click/Swipe) |
| `ip` | str | `None` | None/"localhost"면 디바이스 기반으로 재계산 |

ip/port 재계산 로직 (`std/__init__.py:85-108`), `ip is None or =="localhost"` 일 때만:
- **Android**: `device.adb.setup_forward('tcp:{port}')` 로 **adb forward** 설정 → ip=`localhost`(또는 adb host), port=로컬 포워드 포트. (네트워크 unreachable 회피, 주석 "always forward for android device")
- **IOS**: `device.setup_forward(port)`. local device면 ip=localhost, 아니면 device.ip.
- **그 외(Windows player / Editor / VirtualDevice)**: `device.get_ip_address()` 시도 → 실패 시 `socket.gethostbyname(gethostname())` → 그래도 실패 시 `'localhost'`.

이후 `agent = StdPocoAgent((ip, port), use_airtest_input)`, `kwargs['reevaluate_volatile_attributes'] = True` 강제.

→ **결론**: Unity(Android)에서 `addr=('', 5001)` 을 줘도, 실제로는 adb forward 를 통해 localhost:포워드포트로 TCP 연결된다. Windows player/Editor 는 같은 PC에서 localhost:5001 로 직결.

---

## 3. StdPocoAgent — RPC/덤프/입력/스크린 묶음

파일: `poco/drivers/std/__init__.py:26-51`

### 3.1 모듈 상수 (std)

| 상수 | 값 |
|------|----|
| `DEFAULT_PORT` | `15004` |
| `DEFAULT_ADDR` | `('localhost', 15004)` |

### 3.2 `class StdPocoAgent(PocoAgent)`

```python
def __init__(self, addr=DEFAULT_ADDR, use_airtest_input=True):
    self.conn = TcpClient(addr)
    self.c = RpcClient(self.conn)
    self.c.DEBUG = False
    self.c.connect()
    hierarchy = FrozenUIHierarchy(StdDumper(self.c), StdAttributor(self.c))
    screen = StdScreen(self.c)
    inputs = AirtestInput() if use_airtest_input else StdInput(self.c)
    super().__init__(hierarchy, inputs, screen, None)
```

- `self.rpc` (property) → `self.c` (RpcClient). UnityPoco 의 `send_message`/`invoke` 가 `self.agent.rpc.call(...)` 로 접근.
- `get_sdk_version()` → RPC `"GetSDKVersion"` (`@sync_wrapper`).
- `get_debug_profiling_data()` → RPC `"GetDebugProfilingData"` (`@sync_wrapper`).
- command 인터페이스는 None (std/unity는 `poco.command(...)` 미지원).

### 3.3 서버가 구현해야 하는 RPC 메서드 (std 계약, Unity SDK 핵심 명세)

아래는 Python 측이 호출하는 **모든 RPC 메서드명·인자·반환** — Unity PocoManager 가 그대로 구현해야 한다. 출처는 std 드라이버 각 파일 + 서버 측 참고 구현(`WindowsUI.py`, `OSXUI.py`).

| RPC 메서드 | 호출처(Python) | 인자 | 반환 | 비고 |
|------------|----------------|------|------|------|
| `Dump` | `StdDumper.dumpHierarchy` (`std/dumper.py:13`) | `onlyVisibleNode: bool` | hierarchy dict (§5 포맷) | **필수**. 전체 UI 트리 |
| `SetText` | `StdAttributor.setAttr` (`std/attributor.py:18`) | `instanceId, value` | `bool` success | text 속성 쓰기. node `_instanceId` 사용 |
| `Screenshot` | `StdScreen._getScreen` (`std/screen.py:16`) | `width: int` | `[b64, fmt]` | fmt가 `*.deflate`면 zlib 압축됨(§4) |
| `GetScreenSize` | `StdScreen.getPortSize` (`std/screen.py:30`) | — | `[w, h]` | 픽셀 해상도 |
| `GetSDKVersion` | `StdPocoAgent.get_sdk_version` (`std/__init__.py:51`) | — | `"x.y.z"` str | |
| `GetDebugProfilingData` | `StdPocoAgent.get_debug_profiling_data` (`std/__init__.py:47`) | — | dict | |
| `Click` | `StdInput.click` (`std/inputs.py:16`) | `x, y` (0~1) | — | use_airtest_input=False일 때만 |
| `Swipe` | `StdInput.swipe` | `x1,y1,x2,y2,duration` | — | 〃 |
| `LongClick` | `StdInput.longClick` | `x,y,duration` | — | 〃 |
| `Scroll` | `StdInput.scroll` | `direction,percent,duration` | — | 〃 |
| `KeyEvent` | `StdInput.keyevent` | `keycode` | — | 〃 |
| `RClick` | `StdInput.rclick` | `x,y` | — | 〃 (osx/windows에서 사용) |
| `DoubleClick` | `StdInput.double_click` | `x,y` | — | 〃 |
| `SendMessage` | `UnityPoco.send_message` | `message: str` | (응답 무시) | Unity 전용 확장(PR#123) |
| `Invoke` | `UnityPoco.invoke` | `{listener, data}` | value | Unity 전용 확장(PR#123) |
| `isVrSupported`/`RotateObject`/`ObjectLookAt`/`hasMovementFinished` | `UnityVRSupport` | (§1.5) | | VR 확장(선택) |

> Unity는 보통 `use_airtest_input=True`(UnityPoco 기본)라 `Click`/`Swipe` 등 입력 RPC는 **호출되지 않고** airtest 가 디바이스/창에 직접 입력을 주입한다(§7). 따라서 Unity SDK가 **반드시** 구현할 RPC는 사실상 `Dump`, `SetText`, `Screenshot`, `GetScreenSize`, `GetSDKVersion`, (선택)`GetDebugProfilingData`, (확장)`SendMessage`/`Invoke`.

서버 측 참고 구현 패턴(WindowsUI.py / OSXUI.py): `StdRpcReactor().register('Dump', self.Dump)` … 형태로 메서드명을 dispatch slot에 등록 (`WindowsUI.py:253-269`). Unity C#도 동일하게 메서드명→핸들러 매핑을 갖춰야 한다.

---

## 4. StdScreen — 스크린샷/해상도

파일: `poco/drivers/std/screen.py`

```python
def _getScreen(self, width): return self.client.call("Screenshot", width)   # @sync_wrapper
def getScreen(self, width):
    b64, fmt = self._getScreen(width)
    if fmt.endswith('.deflate'):
        fmt = fmt[:-len('.deflate')]
        imgdata = zlib.decompress(base64.b64decode(b64))
        b64 = base64.b64encode(imgdata)
    return b64, fmt
def getPortSize(self): return self.client.call("GetScreenSize")             # @sync_wrapper
```

- `Screenshot(width)` 반환: `[base64_str, format]`. format이 `"png.deflate"`/`"bmp.deflate"` 처럼 `.deflate` suffix면 base64디코드→zlib해제→재base64 후 실제 format(`png`/`bmp`)으로 반환. (서버 측 압축 전송 지원. `WindowsUI.py:63-70` 가 `"bmp.deflate"` 반환 예)
- Unity SDK가 스크린샷을 RPC로 제공하지 않으면(또는 width 미지원) — Unity 드라이버는 보통 `use_airtest_input=True`이므로 입력은 airtest지만, **스냅샷은 `poco.snapshot()` → agent.screen.getScreen → RPC Screenshot** 경로다. Unity SDK가 `Screenshot` 을 구현해야 `poco.snapshot()` 동작.

---

## 5. 덤프(Dump) 포맷 — Unity SDK가 반환해야 하는 트리 구조

Python 측은 `Dump` RPC가 아래 **JSON serializable dict** 를 반환한다고 가정한다.

### 5.1 노드 구조 (`poco/sdk/AbstractDumper.py:30-49`, `poco/freezeui/hierarchy.py:93-117`)

```jsonc
{
  "name": "<recognizable string>",   // payload.name 우선, 없으면 node.name
  "payload": {                        // 모든 속성 key-value. None 값은 제거됨
    "name": "...",
    "pos": [0.5, 0.5],                // 정규화 좌표(0~1), anchor 기준 중심
    "size": [0.1, 0.05],             // 정규화 크기(0~1)
    "anchorPoint": [0.5, 0.5],
    "zOrders": {"local": 0, "global": 0},
    "visible": true,
    "type": "Button",                // 선택자 type= 매칭용
    "text": "...",                   // 선택
    "_instanceId": 12345,            // SetText/식별용 (선택이지만 set_text 하려면 필수)
    "texture": "...",                // Unity 한정(VR 예제에서 사용)
    ...
  },
  "children": [ { /* 동일 구조 */ } ] // 자식 없으면 생략 가능
}
```

- `FrozenUIHierarchy`/`FrozenUIDumper`(`hierarchy.py`)는 이 dict를 받아 로컬 `Node` 트리로 래핑. **Python은 트리를 다시 크롤하지 않고 한 번의 Dump로 전체를 받음**(frozen). 그래서 Unity SDK는 `Dump` 한 번에 **전체 트리**를 내려줘야 한다.
- `Node.getAttr` → `node['payload'].get(attrName)`. `getChildren` → `node['children']`. (`hierarchy.py:104-116`)
- `onlyVisibleNode=True`(기본)면 서버가 visible=false 노드를 빼고 보내야 한다. `dumpHierarchy(onlyVisibleNode=False)`로 전체도 가능(`std/test/simple.py:33-37`).
- 선택자 매칭에서 쓰는 핵심 payload 키: `name`, `type`, `text`, `visible`, `pos`, `size`, `_instanceId`. (테스트/튜토리얼에서 확인: `poco('btn_start')`, `poco(type='Button')`, `poco(textMatches='drag.*')`, `attr('texture')`, `attr('_instanceId')` — `unity3d/test/tutorial/overview.py`, `local_positioning1.py`, `std/test/simple.py:89-93`)

### 5.2 set_text 경로 (`StdAttributor`)

`poco/drivers/std/attributor.py:12-21`:
- `setAttr(node,'text',val)` → node의 `_instanceId` 획득 → RPC `SetText(instance_id, val)`. 성공 시 True. `_instanceId`가 없으면 `UnableToSetAttributeException`.
- 즉 **Unity SDK는 각 노드 payload에 `_instanceId`를 넣고, `SetText(id, text)` RPC를 구현**해야 `set_text()`가 동작.

---

## 6. TCP RPC 프로토콜 (와이어 포맷)

### 6.1 패킷 프레이밍 — `SimpleProtocolFilter`

파일: `poco/utils/simplerpc/transport/tcp/protocol.py` (서버 측 동일: `poco/sdk/std/protocol.py`)

| 항목 | 값 |
|------|----|
| `HEADER_SIZE` | `4` 바이트 |
| 헤더 인코딩 | `struct.pack('i', len)` — **리틀엔디언 int32 길이 프리픽스** |
| 페이로드 | UTF-8 인코딩된 JSON 문자열 |
| 패킷 | `[4byte length][JSON payload]` |

`pack(content)` = `struct.pack('i', len(utf8)) + utf8` (`protocol.py:39-45`).
`input(data)` = 버퍼 누적 후 길이만큼 모이면 yield (스트림 재조립). (`protocol.py:25-37`)

→ **Unity SDK의 TCP 서버는 반드시 동일한 4바이트 길이 프리픽스(native int, 리틀엔디언) + UTF-8 JSON 프레이밍을 써야 한다.**

### 6.2 JSON-RPC 2.0 메시지

요청 빌드: `RpcAgent.format_request` (`poco/utils/simplerpc/simplerpc.py:135-151`)
```jsonc
{ "method": "Dump", "params": [true], "jsonrpc": "2.0", "id": "<uuid>" }
```
- `params` = positional args(list) **또는** kwargs(dict) 중 하나. 둘 다 비면 `[]`. (UnityPoco.invoke는 kwargs로 `{listener, data}` 전달 → dict params)
- `id` 는 매 요청마다 새 uuid4 (`simplerpc.py:143`).

응답 처리: `RpcAgent.handle_message` (`simplerpc.py:157-188`)
- `"method"` 키 있으면 요청(서버→클라 역방향), 없으면 응답.
- 응답이면 `id`로 콜백 매칭 → `result`있으면 `callback.rpc_result`, `error`있으면 `callback.rpc_error`.

에러 응답 포맷(서버 측 reactor, `poco/sdk/std/rpc/reactor.py:46-52`):
```jsonc
{ "id": "...", "jsonrpc": "2.0",
  "error": { "message": "...\n|--- REMOTE TRACEBACK ---|\n..." } }
```
- `sync_wrapper`(`poco/utils/simplerpc/utils.py:9-17`)가 `err['message']` 를 꺼내 `RemoteError` raise. 그래서 **서버 에러는 반드시 `error.message` 필드**에 넣어야 한다.

### 6.3 동기화 모델 — `Callback.wait`

파일: `poco/utils/simplerpc/simplerpc.py:21-85`
- `RpcClient.call` 은 즉시 `Callback` 반환(비동기). 동기 결과는 `.wait(timeout)` 호출.
- `Callback.wait`: `BACKEND_UPDATE` False면 매 루프 `agent.update()` 호출 → `conn.recv()` → 새 메시지 처리. status가 WAITING 동안 5ms sleep 폴링. timeout 초과 시 `RpcTimeoutError`.
- `sync_wrapper` 데코레이터는 `timeout=30` 으로 wait. UnityPoco.invoke 는 timeout 없이 wait(무한 대기 주의).

### 6.4 TCP 클라이언트 (`TcpClient` / safetcp.Client)

- `TcpClient` (`transport/tcp/main.py`): `DEFAULT_ADDR=("0.0.0.0", 5001)`. `send`=프레이밍 pack, `recv`=`prot.input(...)` (소켓 timeout 시 빈 바이트). 
- `safetcp.Client` (`transport/tcp/safetcp.py`): `DEFAULT_TIMEOUT=5`, `DEFAULT_SIZE=4096`. 매 connect마다 새 소켓 생성, AF_INET/SOCK_STREAM. `recv`가 `b""`면 연결 끊김(`socket.error`). Windows errno 처리: 10035(EWOULDBLOCK)=데이터없음, 10053/10054=연결끊김.
- `RpcClient` 상태머신 (`rpcclient.py`): INIT→CONNECTING→CONNECTED→CLOSED. `connect(timeout=10)` 은 0.5s 간격 폴링으로 CONNECTED 대기. (`_wait_connected`)

→ Unity SDK는 **TCP 서버 = 클라이언트가 접속(connect)하는 쪽**. 즉 게임 런타임이 5001 포트에서 `listen/accept` 하고, Python(TcpClient)이 connect 한다. Windows player/editor 는 localhost 직결, Android는 adb forward 경유.

---

## 7. 입력 경로 — AirtestInput vs StdInput

UnityPoco 기본은 `use_airtest_input=True`(StdPoco 기본값) → **AirtestInput** 사용.

### 7.1 AirtestInput (`poco/utils/airtest/input.py`)
- `click(x,y)` → `get_target_pos` 로 정규화 좌표(0~1)를 디바이스 실해상도 픽셀로 환산 후 `airtest.touch(pos, duration)`.
- `swipe`/`longClick`/`double_click` 동일하게 airtest API 호출. `applyMotionEvents` 는 Android minitouch 전용.
- `_get_touch_resolution`: Android면 render_resolution 옵션 고려, 아니면 `current_device().get_current_resolution()`.
- **즉 Unity Editor/Windows player의 경우 입력은 창 좌표로 airtest가 주입**(GameView 창의 `focus_rect` 보정과 결합). RPC `Click`은 안 씀.

### 7.2 StdInput (`poco/drivers/std/inputs.py`)
- `use_airtest_input=False`일 때. 모든 입력이 RPC(`Click`/`Swipe`/`LongClick`/`KeyEvent`/`Scroll`/`RClick`/`DoubleClick`) — 좌표는 0~1 정규화로 서버에 전달, 서버가 픽셀 환산(예: `WindowsUI.py:93-106` 의 Swipe 비례 환산).

---

## 8. UnityPoco 사용 패턴 (테스트/튜토리얼에서 추출)

출처: `poco/drivers/unity3d/test/*`

| 패턴 | 코드 | 의미 |
|------|------|------|
| Android 기본 | `UnityPoco()` | adb 연결된 Android 게임, 5001 |
| Editor | `UnityPoco(unity_editor=True)` | Windows UnityEditor GameView |
| 디바이스 지정 | `UnityPoco(('',5001), device=dev)` | dev=`connect_device('Windows:///?title_re=...')` 등 |
| 선택 | `poco('btn_start')`, `poco(type='Button')`, `poco(textMatches='drag.*')` | name/type/text 매칭 |
| 액션 | `.click()`, `.drag_to(other)`, `.focus('center').long_click()`, `.focus([0.1,0.1])` | |
| 속성 | `.get_text()`, `.attr('texture')`, `.attr('_instanceId')` | |
| 멀티 | dev별 `UnityPoco(addr, device=devN)` 인스턴스 분리 | `doc/drivers/unity3d.rst:106-134` |

`TestU3dDriverAndroid`/`TestU3dDriverUnityEditor` 는 `std/test/simple.py:TestStandardFunction` 를 그대로 상속 → std 테스트 셋(dump/screenshot/set_text/motion)이 Unity에도 적용됨(`unity3d/test/test.py`).

---

## 9. Unity 프로젝트에 무엇을 임베드해야 하는가 (서버 측 명세 종합)

이 Python 저장소에는 **Unity C# 코드가 없다**. `doc/integration.rst:9-17` 명시:
- poco-sdk(C#)는 별도 저장소 `https://github.com/AirtestProject/Poco-SDK` 에서 clone.
- `Unity3D` 폴더를 Unity 프로젝트 스크립트 폴더로 복사.
- `ngui` 사용 시 `Unity3D/ugui` 제거, `ugui` 사용 시 `Unity3D/ngui` 제거.
- `Unity3D/PocoManager.cs` 를 임의 GameObject(보통 Main Camera)에 컴포넌트로 추가.
- 지원: Unity 4 & 5, ngui & ugui, C# only (`doc/integration.rst:11`). 더 새 버전은 Poco-SDK 저장소 README 참조.

### 9.1 PocoManager(C#)가 충족해야 할 계약 (Python 드라이버 역추적)

1. **TCP 서버**: 게임 런타임에서 포트 **5001**(기본, 변경 가능) 로 listen/accept.
2. **프레이밍**: `[int32(LE) 길이][UTF-8 JSON]` (§6.1).
3. **JSON-RPC 2.0** 요청 파싱(`method/params/id/jsonrpc`), 응답 생성(`result` 또는 `error.message`). (§6.2)
4. **필수 RPC 메서드**:
   - `Dump(onlyVisibleNode)` → 전체 UI 트리 dict(§5). payload에 `name/type/pos/size/visible/anchorPoint/zOrders` 최소 포함, `set_text` 지원 시 `_instanceId`+`SetText`.
   - `GetScreenSize()` → `[w,h]`.
   - `GetSDKVersion()` → 버전 문자열.
   - `Screenshot(width)` → `[b64, fmt]` (`poco.snapshot()` 쓸 경우. `*.deflate` 압축 선택).
   - `SetText(instanceId, value)` → bool (`set_text()` 쓸 경우).
   - `GetDebugProfilingData()` → dict (선택).
5. **확장(선택, poco-sdk PR#123+)**: `SendMessage(msg)`(→`PocoManager.MessageReceived`), `Invoke({listener,data})`(→`PocoListenerBase` + `[PocoMethod("name")]`). (`doc/drivers/unity3d.rst:137-206`)
6. **VR(선택)**: `isVrSupported`, `RotateObject`, `ObjectLookAt`, `hasMovementFinished` (Google VR 시나리오, `doc/unity3d_vr.rst`).

### 9.2 좌표/가시성 규약
- 모든 `pos`/`size`/`anchorPoint`는 화면 정규화(0~1). Unity SDK가 GameObject 화면 좌표를 0~1로 변환해 내려줘야 airtest 입력(픽셀 환산)과 일치.
- `onlyVisibleNode=True`면 비가시 노드 제외(`AbstractDumper.dumpHierarchyImpl:108-110`).

### 9.3 별도 poco-sdk 저장소 필요성 (명확화)
- **반드시 필요**. 본 Python 저장소만으로는 Unity 게임을 자동화할 수 없다. Unity 측 C# 런타임 서버(PocoManager)가 없으면 `Dump` RPC가 응답하지 않아 `poco('...')` 가 즉시 실패한다.
- WES(Unity 6 / Netcode) 적용 시 주의: poco-sdk 문서상 공식 명시는 Unity 4&5지만, 저장소 README/최신 릴리스가 신버전·UITK 지원을 추가했을 수 있으므로 Poco-SDK 저장소 최신본 확인 필요(이 저장소로는 판단 불가 — 모르는 부분).

---

## 10. Poco 공통 옵션 (UnityPoco 에 그대로 전달됨)

파일: `poco/pocofw.py:43-63`

| 옵션 | 기본값 | Unity 적용값 | 설명 |
|------|--------|--------------|------|
| `action_interval` | `0.8` | **`0.5`** (UnityPoco가 강제, `unity3d_poco.py:67-68`) | 액션 후 UI 안정 대기(초) |
| `pre_action_wait_for_appearance` | `6` | 6 | 액션 전 타겟 등장 대기(초). 초과 시 `PocoNoSuchNodeException` |
| `poll_interval` | `1.44` | 1.44 | wait_for_* 폴링 간격(초) |
| `reevaluate_volatile_attributes` | `False`→**`True`** (StdPoco 강제, `std/__init__.py:111`) | True | volatile 속성 재조회 시 타겟 재선택(frozen dump 보정) |
| `touch_down_duration` | 미설정 | — | 설정 시 input에 전달(AirtestInput 기본 0.01) |

`Poco` 주요 메서드(공통): `__call__`(선택자→UIObjectProxy), `click/swipe/long_click/scroll/pinch/double_click`, `wait_for_any/all`, `freeze`(현재 hierarchy 스냅샷 immutable poco 생성), `snapshot/get_screen_size`, `dump`(=agent.hierarchy.dump), `command`(unity 미지원, None).

---

## 11. 나머지 드라이버 요약 (연결 방식·대상·차이)

각 드라이버 폴더 1회 이상 훑음. UnityPoco/QtPoco/OSXPoco/WindowsPoco/UE4Poco 는 모두 `StdPoco` 상속(같은 TCP std-rpc). Android/Netease 는 hrpc(HTTP), Cocosjs 는 WebSocket, iOS 는 WDA 소스 파싱.

| 드라이버 | 파일 | 상위 | 연결 방식 | 기본 addr/port | 입력 | 대상/차이 |
|----------|------|------|-----------|----------------|------|-----------|
| **unity3d** `UnityPoco` | `drivers/unity3d/` | StdPoco | TCP std-rpc | `("localhost",5001)` | AirtestInput | Unity 4/5 ngui·ugui. Android(adb forward)/Windows player/Editor(GameView 핸들). `send_message`/`invoke`/VR 확장 |
| **std** `StdPoco` | `drivers/std/` | Poco | TCP std-rpc | `("localhost",15004)` | Airtest/StdInput | 모든 std 엔진의 베이스. cocos2dx-lua 기본 15004. `freezeui` 기반 1회 Dump |
| **android** `AndroidUiautomationPoco` | `drivers/android/uiautomation.py` | Poco | **hrpc over HTTP** (`hrpc.client.RpcClient`+HttpTransport) | endpoint `http://ip:10081` (forward 10080/10081) | RemotePoco(원격 inputer) 또는 Airtest | **Android 네이티브 앱**(엔진 무관). `pocoservice-debug.apk`(`com.netease.open.pocoservice`) 자동 install + `am instrument` 로 UiAutomator 서버 구동, KeepRunning 스레드로 유지. SDK 통합 불필요 |
| **ios** `iosPoco` | `drivers/ios/__init__.py` | Poco | **WDA(WebDriverAgent) source 파싱** (`client.driver.source(format='json')`) | airtest IOS 디바이스 경유 | AirtestInput + AirtestScreen | iOS 앱. RPC 아님 — XCUIElement 트리(json/xml)를 `json_parser`로 poco 포맷 변환. iPad 가로/홈화면 좌표 회전 보정(`XYTransformer`). airtest>1.1.7 python3 필수 |
| **cocosjs** `CocosJsPoco` | `drivers/cocosjs/__init__.py` | Poco | **WebSocket**(`ws://ip:port`, WebSocketClient) | `("localhost",5003)` / `ws://...:5003` | AirtestInput | cocos2dx-js/Cocos-Creator. **dump RPC 메서드명이 소문자 `"dump"`**(주의, `cocosjs/__init__.py:73`), `getSDKVersion`(소문자). addr는 tuple 또는 ws URL 문자열 허용 |
| **osx** `OSXPoco` | `drivers/osx/osxui_poco.py` | StdPoco | TCP std-rpc, **로컬 SDK를 데몬 스레드로 자체 기동** | std DEFAULT(`("localhost",15004)`) | StdInput | macOS 앱. localhost면 `PocoSDKOSX`(atomac/pyautogui 기반)를 스레드로 띄움. `connect_window(selector)`(RPC `ConnectWindow`), `set_foreground`, scroll/rclick/double_click/keyevent 오버라이드. action_interval 0.1 |
| **windows** `WindowsPoco` | `drivers/windows/windowsui_poco.py` | StdPoco | TCP std-rpc, **로컬 SDK 데몬 스레드** | std DEFAULT(15004) | StdInput | Windows 앱. localhost면 `PocoSDKWindows`(uiautomation/win32 기반) 스레드 기동. `selector={title|handle|title_re}` → `ConnectWindow`. 가로 스크롤 미지원(InvalidOperationException). action_interval 0.1 |
| **qt** `QtPoco` | `drivers/qt/__init__.py` | StdPoco | TCP std-rpc | `("localhost",9001)` | Airtest/StdInput | Qt 앱. `VirtualDevice(addr[0])` 사용. 가장 얇은 std 래퍼(포트만 9001) |
| **ue4** `UE4Poco` | `drivers/ue4/ue4_poco.py` | StdPoco | TCP std-rpc | `("localhost",5001)` | AirtestInput | Unreal Engine 4. UnityPoco와 거의 동일 구조. `ue4_editor=True`→`UE4EditorWindow()`(`class_name=UnrealWindow`, `Game Preview Standalone`). poco-sdk UE4는 "coming soon"(`integration.rst:196-199`) |
| **netease** `NeteasePoco` | `drivers/netease/internal.py` | Poco | **Hunter/Safaia hrpc**(`airtest_hunter`, `hunter_cli`) | open_platform apitoken | Airtest + HunterCommand | 넷이즈 내부 엔진(NeoX/Messiah 등). `remote('poco-uiautomation-framework-2')`. Hunter 모듈 preload 설정 필요(`integration.rst:207-250`). 외부 패키지 의존(`requirements.txt`) |

공통 디바이스 유틸 (`poco/utils/device.py`): `default_device()`(현재 디바이스 없으면 Android 연결), `VirtualDevice(ip)`(qt/osx/windows용 가짜 디바이스, 해상도 1920x1080 고정).

### 11.1 RPC 전송 계층 3종 비교

| 전송 | 사용 드라이버 | 모듈 | 특징 |
|------|---------------|------|------|
| TCP simplerpc | std/unity/ue4/qt/osx/windows | `utils/simplerpc/transport/tcp` | 4byte길이+JSON-RPC2.0, 게임이 서버 |
| WebSocket | cocosjs | `utils/simplerpc/transport/ws` (`ws/main.py`, `DEFAULT_ADDR="ws://localhost:5003"`) | websocket-client 라이브러리, 별도 수신 스레드 |
| HTTP hrpc | android/netease | `hrpc`(외부) | 원격 객체 프록시(dumper/selector/attributor/inputer를 remote로) |

---

## 12. doc/ 통합 절차 반영 (Unity 중심)

- `doc/poco_drivers.rst`: 드라이버별 진입점 인덱스. Unity3D/Android/OSX/Windows/cocos2dx-lua/js/Egret/Netease 링크.
- `doc/integration.rst`: poco-sdk(별도 저장소) 통합 가이드. Unity3D 절차(§9), cocos2dx-lua(`poco:init_server(15004)`, socket 모듈 필요), cocos2dx-js(WebSocketServer libwebsockets 1.6 빌드), Cocos-Creator(`USE_WEBSOCKET_SERVER 1`), Unreal(coming soon), Android(무통합), Netease(Hunter preload).
- `doc/drivers/unity3d.rst`: Android/Windows player/UnityEditor/멀티디바이스 초기화 예제 + `sendMessage`/`invoke` 통합 가이드(C# 측 `PocoManager.MessageReceived`, `PocoListenerBase`+`[PocoMethod]`).
- `doc/unity3d_vr.rst`: Google VR 자동화(Android+Unity). `Unity3D` 폴더 임베드 + 빈 오브젝트에 PocoManager + CameraContainer/CameraFollower. `vr.rotateObject(...)`, `vr.objectLookAt(...)` 후 `poco.click([0.5,0.5])`. (단 코드상 `poco.vr` 비활성 — §1.2 주의)

---

## 13. 상호 참조 인덱스 (경로:라인)

| 항목 | 위치 |
|------|------|
| UnityPoco 클래스/생성자 | `poco/drivers/unity3d/unity3d_poco.py:40-95` |
| DEFAULT_PORT 5001 (unity) | `poco/drivers/unity3d/unity3d_poco.py:12-13` |
| send_message / invoke | `poco/drivers/unity3d/unity3d_poco.py:84-95` |
| UnityVRSupport | `poco/drivers/unity3d/unity3d_poco.py:16-37` |
| UnityEditorWindow | `poco/drivers/unity3d/device.py:6-11` |
| StdPoco ip/port 재계산 | `poco/drivers/std/__init__.py:84-112` |
| StdPocoAgent + RPC 묶음 | `poco/drivers/std/__init__.py:26-51` |
| StdDumper(Dump RPC) | `poco/drivers/std/dumper.py:11-13` |
| StdAttributor(SetText RPC) | `poco/drivers/std/attributor.py:12-21` |
| StdScreen(Screenshot/GetScreenSize) | `poco/drivers/std/screen.py:15-30` |
| StdInput(RPC 입력) | `poco/drivers/std/inputs.py:14-40` |
| 덤프 포맷 명세 | `poco/sdk/AbstractDumper.py:30-117` |
| FrozenUIHierarchy/Node | `poco/freezeui/hierarchy.py:48-117` |
| RPC 요청/응답/콜백 | `poco/utils/simplerpc/simplerpc.py:21-188` |
| sync_wrapper/RemoteError | `poco/utils/simplerpc/utils.py:5-17` |
| TCP 프레이밍 | `poco/utils/simplerpc/transport/tcp/protocol.py:8-45` |
| safetcp.Client | `poco/utils/simplerpc/transport/tcp/safetcp.py:9-79` |
| TcpClient | `poco/utils/simplerpc/transport/tcp/main.py:11-44` |
| RpcClient 상태머신 | `poco/utils/simplerpc/rpcclient.py:8-77` |
| WebSocketClient(cocosjs) | `poco/utils/simplerpc/transport/ws/main.py:11-73` |
| AirtestInput | `poco/utils/airtest/input.py:39-131` |
| AirtestScreen | `poco/utils/airtest/screen.py:8-21` |
| Poco 공통 옵션 | `poco/pocofw.py:43-63` |
| std rpc reactor(서버) | `poco/sdk/std/rpc/reactor.py:14-71` |
| std rpc controller(서버) | `poco/sdk/std/rpc/controller.py:13-54` |
| WindowsUI 서버 참고 구현 | `poco/drivers/windows/sdk/WindowsUI.py:23-282` |
| OSXUI 서버 참고 구현 | `poco/drivers/osx/sdk/OSXUI.py:24-60` |
| Android uiautomation 드라이버 | `poco/drivers/android/uiautomation.py:82-360` |
| ios 드라이버 | `poco/drivers/ios/__init__.py:16-211` |
| cocosjs 드라이버 | `poco/drivers/cocosjs/__init__.py:29-100` |
| netease 드라이버 | `poco/drivers/netease/internal.py:18-64` |
| qt/osx/windows/ue4 드라이버 | 각 `drivers/<name>/...` |
| 디바이스 유틸 | `poco/utils/device.py:9-34` |
| Unity 통합 가이드 | `doc/integration.rst:9-17`, `doc/drivers/unity3d.rst`, `doc/unity3d_vr.rst` |
</content>
</invoke>
