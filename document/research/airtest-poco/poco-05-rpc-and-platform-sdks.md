---
type: reference
source: Poco
generated: subagent
---

# Poco 05 — JSON-RPC 전송 계층 & OSX/Windows 플랫폼 SDK

누락검사(medium severity)에서 지목된 Poco 소스를 보충하는 레퍼런스. 다룬 영역:

1. **JSON-RPC 전송 계층** — Python 드라이버 ↔ 게임 내 SDK 통신 프로토콜
2. **OSX 드라이버 SDK** — Accessibility API 기반 Dumper/Node
3. **Windows 드라이버 SDK** — UIAutomation 기반 Dumper/Node
4. **이식 관점 요약** — Unity 같은 신규 엔진 이식 시 참고점

> 본 문서의 모든 시그니처는 소스 원문 그대로. 식별자·경로는 영어 유지.

---

## 0. 전체 통신 구도 (한눈에)

```
[Python 드라이버 측]                            [게임/앱 측 = SDK]
UnityPoco / StdPoco                             PocoSDK (Unity C# / Windows / OSX)
  └ StdPocoAgent
      ├ RpcClient (call/handle_message)   ──┐
      └ TcpClient(addr, 5001)               │  TCP, [4B length][utf-8 JSON] 프레이밍
          └ safetcp.Client (socket)         │  ←→  game-side RPC endpoint
                                          ──┘   (JSON-RPC 2.0 request/response)
```

- **JSON-RPC 2.0**가 wire 포맷. `method`(예: `"Dump"`, `"Click"`, `"SendMessage"`)와 `params`, `id`로 원격 호출.
- 전송 프레이밍은 JSON-RPC 스펙 밖. Poco는 `[4바이트 길이][UTF-8 JSON 본문]`의 단순 프로토콜(`SimpleProtocolFilter`)로 TCP 스트림을 패킷화한다.
- **UnityPoco는 이 RPC 계층을 그대로 재사용**한다. `UnityPoco → StdPoco → StdPocoAgent → RpcClient + TcpClient(port 5001)`. 즉 Unity 연결의 "프로토콜 실체"가 바로 이 simplerpc 패키지다.

---

## 1. JSON-RPC 전송 계층

### 1.1 파일 맵

| 파일 | 역할 |
|------|------|
| `jsonrpc/base.py` | 요청/응답 추상 베이스 (`JSONRPCBaseRequest`, `JSONRPCBaseResponse`) |
| `jsonrpc/jsonrpc.py` | 버전 디스패처 (`JSONRPCRequest.from_json` → 1.0/2.0 분기) |
| `jsonrpc/jsonrpc2.py` | JSON-RPC **2.0** 요청/응답 + 배치 |
| `jsonrpc/jsonrpc1.py` | JSON-RPC **1.0** 요청/응답 (레거시) |
| `jsonrpc/exceptions.py` | 표준 에러 코드 + 예외 |
| `jsonrpc/dispatcher.py` | `method_name → callable` 매핑 (서버측) |
| `jsonrpc/manager.py` | `JSONRPCResponseManager` — 요청 처리·에러 변환 오케스트레이션 |
| `jsonrpc/utils.py` | `JSONSerializable`, `is_invalid_params` 등 |
| `simplerpc.py` | `RpcAgent`/`Callback`/`AsyncResponse` — 비동기 RPC 에이전트 코어 |
| `rpcclient.py` | `RpcClient` — 클라이언트측 연결·송수신 루프 |
| `utils.py` (simplerpc) | `sync_wrapper`, `RemoteError` |
| `transport/interfaces.py` | `IConnection`/`IClient` 추상 |
| `transport/tcp/main.py` | `TcpClient` — TCP 연결(기본 port **5001**) |
| `transport/tcp/protocol.py` | `SimpleProtocolFilter` — 길이 프리픽스 패킷 프레이밍 |
| `transport/tcp/safetcp.py` | `Client` — 정확한 send/recv 소켓 래퍼 |
| `transport/ws/main.py` | `WebSocketClient` — WS 전송 (기본 `ws://localhost:5003`) |

---

### 1.2 JSON-RPC 2.0 메시지 포맷

#### 요청 — `JSONRPC20Request`
`경로: poco/utils/simplerpc/jsonrpc/jsonrpc2.py:8`

```python
class JSONRPC20Request(JSONRPCBaseRequest):
    JSONRPC_VERSION = "2.0"
    REQUIRED_FIELDS = set(["jsonrpc", "method"])
    POSSIBLE_FIELDS = set(["jsonrpc", "method", "params", "id"])
```

| 필드 | 타입 | 제약 / 동작 |
|------|------|------------|
| `jsonrpc` | str | 항상 `"2.0"`. `data` 게터에서 자동 주입 (`jsonrpc2.py:55`) |
| `method` | str | 문자열 필수. `"rpc."` 접두사 금지 → `ValueError` (`jsonrpc2.py:74`) |
| `params` | list / tuple / dict / None | tuple은 list로 정규화. None이면 키 생략 (`jsonrpc2.py:87`) |
| `id` | str / int / None | string·integer만 허용. `is_notification`이면 직렬화 시 제외 (`jsonrpc2.py:53`) |

- `args` (위치 인자) / `kwargs` (명명 인자) 프로퍼티로 분리 — `params`가 list면 args, dict면 kwargs (`base.py:27`, `base.py:36`).
- `from_json` (`jsonrpc2.py:109`): 배치(list) 지원. 필드 집합이 `REQUIRED ⊆ keys ⊆ POSSIBLE` 위반 시 `JSONRPCInvalidRequestException`. `id` 부재 = notification.

#### 응답 — `JSONRPC20Response`
`경로: poco/utils/simplerpc/jsonrpc/jsonrpc2.py:167`

| 필드 | 규칙 |
|------|------|
| `jsonrpc` | 항상 `"2.0"` 주입 (`jsonrpc2.py:203`) |
| `result` | 성공 시 필수. `error`와 동시 사용 불가 → `ValueError` (`jsonrpc2.py:216`) |
| `error` | 실패 시 필수. dict이며 setter가 `JSONRPCError(**value)`로 유효성 검증 (`jsonrpc2.py:226`) |
| `id` | 요청 id와 동일해야 함. string/int만 (`jsonrpc2.py:238`) |

#### 배치
- `JSONRPC20BatchRequest` (`jsonrpc2.py:142`) / `JSONRPC20BatchResponse` (`jsonrpc2.py:247`) — 둘 다 `__iter__` 제공, `.json`은 list 직렬화.

---

### 1.3 버전 디스패치

`경로: poco/utils/simplerpc/jsonrpc/jsonrpc.py:18`

```python
@classmethod
def from_json(cls, json_str):
    data = cls.deserialize(json_str)
    if isinstance(data, dict) and "jsonrpc" not in data:
        return JSONRPC10Request.from_json(json_str)   # 1.0
    else:
        return JSONRPC20Request.from_json(json_str)   # 2.0
```

- 판별 기준: **`jsonrpc` 키 존재 여부**. 있으면 2.0, 없으면 1.0.
- 응답 클래스는 `manager.py`의 `RESPONSE_CLASS_MAP = {"1.0": JSONRPC10Response, "2.0": JSONRPC20Response}` (`manager.py:39`)로 매핑.

JSON-RPC 1.0 (`jsonrpc1.py:7`) 차이점만 요약:

| 항목 | 1.0 | 2.0 |
|------|-----|-----|
| `jsonrpc` 필드 | 없음 | `"2.0"` 필수 |
| `params` | list/tuple만 | list/tuple/**dict** 허용 (명명 인자) |
| notification | `id: null` 로 표현 | `id` 키 자체를 생략 |
| 배치 | 미지원 | 지원 |

---

### 1.4 에러 코드

`경로: poco/utils/simplerpc/jsonrpc/exceptions.py`

| 예외 클래스 | CODE | MESSAGE | 위치 |
|-------------|------|---------|------|
| `JSONRPCParseError` | -32700 | Parse error | `exceptions.py:88` |
| `JSONRPCInvalidRequest` | -32600 | Invalid Request | `exceptions.py:101` |
| `JSONRPCMethodNotFound` | -32601 | Method not found | `exceptions.py:113` |
| `JSONRPCInvalidParams` | -32602 | Invalid params | `exceptions.py:125` |
| `JSONRPCInternalError` | -32603 | Internal error | `exceptions.py:137` |
| `JSONRPCServerError` | -32000 | Server error | `exceptions.py:149` |

- `JSONRPCError` (`exceptions.py:6`): `{code:int, message:str, data?:any}` 직렬화 단위. `code`는 integer, `message`는 string 강제.
- 예외 흐름:
  - `JSONRPCException` → `JSONRPCInvalidRequestException`(잘못된 요청), `JSONRPCDispatchException`(디스패치 메서드 내부에서 던지는 용도, `.error` 보유; `exceptions.py:175`).

---

### 1.5 서버측 처리 — `JSONRPCResponseManager`

`경로: poco/utils/simplerpc/jsonrpc/manager.py:24`

```python
@classmethod
def handle(cls, request_str, dispatcher): ...
@classmethod
def handle_request(cls, request, dispatcher): ...
@classmethod
def _get_responses(cls, requests, dispatcher): ...
```

`handle`의 처리 순서:

| 단계 | 분기 | 결과 |
|------|------|------|
| JSON 파싱 실패 | `TypeError`/`ValueError` | `JSONRPCParseError` (-32700) |
| 요청 형식 오류 | `JSONRPCInvalidRequestException` | `JSONRPCInvalidRequest` (-32600) |
| method 미존재 | `dispatcher[method]` KeyError | `JSONRPCMethodNotFound` (-32601) |
| 인자 불일치 | TypeError + `is_invalid_params` | `JSONRPCInvalidParams` (-32602) |
| 내부 예외 | 그 외 Exception | `JSONRPCServerError` (-32000), `data`에 `{type,args,message}` |
| 정상 | — | `result` 채운 응답 |
| notification | `is_notification` | 응답 **yield 안 함** (`manager.py:129`) |

- `is_invalid_params` (`utils.py:56`): `inspect.getargspec`로 함수 시그니처와 전달 인자 개수/키를 비교해 "잘못된 파라미터 때문의 TypeError"와 "함수 내부 TypeError"를 구분.

---

### 1.6 디스패처 — `Dispatcher`

`경로: poco/utils/simplerpc/jsonrpc/dispatcher.py:12`

`MutableMapping`을 구현한 `method_name → callable` 사전.

| 메서드 | 동작 | 위치 |
|--------|------|------|
| `add_method(f, name=None)` | 단일 함수 등록 (데코레이터 가능) | `dispatcher.py:69` |
| `add_class(cls)` | `cls().` 인스턴스 public 메서드를 `clsname.` 프리픽스로 등록 | `dispatcher.py:56` |
| `add_object(obj)` | 객체 public 메서드 등록 | `dispatcher.py:60` |
| `add_dict(dict, prefix='')` | dict의 callable 등록 | `dispatcher.py:64` |
| `build_method_map(prototype, prefix='')` | 핵심 빌더. 객체면 `_`로 시작 안 하는 public 메서드만 | `dispatcher.py:103` |

---

### 1.7 비동기 RPC 에이전트 — `simplerpc.py`

이 파일이 **클라이언트/서버 양쪽 공통 코어**다. `RpcClient`가 이를 상속한다.

#### `RpcAgent`
`경로: poco/utils/simplerpc/simplerpc.py:118`

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `format_request` | `format_request(self, func, *args, **kwargs)` | `{method, params, jsonrpc:"2.0", id:uuid4}` payload 생성, `Callback` 등록, `(req_json, cb)` 반환 (`simplerpc.py:135`) |
| `handle_request` | `handle_request(self, req)` | `JSONRPCResponseManager.handle(req, dispatcher).data` 호출 (`simplerpc.py:153`) |
| `handle_message` | `handle_message(self, msg, conn)` | `method` 키 유무로 request/response 판별. request면 처리 후 `conn.send`, response면 등록된 `Callback`에 결과/에러 전달 (`simplerpc.py:157`) |
| `run` / `console_run` | — | `update()` 루프. `BACKEND_UPDATE`면 데몬 스레드 (`simplerpc.py:193`) |

- `params`는 `args or kwargs or []` — **위치/명명 인자 중 하나만** 보냄 (`simplerpc.py:139`).
- `id`는 매 요청 새 `uuid4` 문자열 (`simplerpc.py:143`). 응답 매칭 키.

#### `Callback`
`경로: poco/utils/simplerpc/simplerpc.py:21`

상태: `WAITING, RESULT, ERROR, CANCELED = 0,1,2,3`.

| 메서드 | 동작 |
|--------|------|
| `on_result(func)` / `on_error(func)` | 콜백 등록 (callable 검증) |
| `rpc_result(data)` / `rpc_error(data)` | 응답 도착 시 호출, 상태 전이 |
| `wait(timeout=None)` | `agent.update()` 폴링하며 상태 변경까지 블록. 초과 시 `RpcTimeoutError`. `(result, error)` 반환 (`simplerpc.py:70`) |

#### `AsyncResponse`
`경로: poco/utils/simplerpc/simplerpc.py:88` — 서버가 응답을 나중에 보낼 때(`result()`/`error()`로 지연 전송).

#### 동기 래퍼
`경로: poco/utils/simplerpc/utils.py:9`

```python
def sync_wrapper(func):
    @wraps(func)
    def new_func(*args, **kwargs):
        cb = func(*args, **kwargs)
        ret, err = cb.wait(timeout=30)
        if err:
            raise RemoteError(err['message'])
        return ret
    return new_func
```
→ `call()`이 돌려준 `Callback`을 **30초 동기 대기**로 감싼다. `StdPocoAgent.get_sdk_version` 등이 이 데코레이터 사용.

---

### 1.8 클라이언트 — `RpcClient`

`경로: poco/utils/simplerpc/rpcclient.py:8`

상태: `INIT, CONNECTING, CONNECTED, CLOSED = 0,1,2,3`.

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `__init__` | `RpcClient(conn)` | `IClient` 주입, `connect_cb`/`close_cb` 바인딩 |
| `connect` | `connect(self, timeout=10)` | `conn.connect()` 후 `_wait_connected` (0.5s 폴링) |
| `call` | `call(self, func, *args, **kwargs)` | `format_request` → `conn.send(msg)` → `Callback` 반환 (`rpcclient.py:56`) |
| `update` | `update(self)` | `conn.recv()`로 받은 패킷마다 `handle_message` (`rpcclient.py:61`) |

→ **`call`은 비동기**(Callback 반환), `wait()` 또는 `sync_wrapper`로 동기화.

---

### 1.9 전송 — TCP 프레이밍 (port 5001 지점)

#### `TcpClient`
`경로: poco/utils/simplerpc/transport/tcp/main.py:11`

```python
DEFAULT_ADDR = ("0.0.0.0", 5001)
class TcpClient(IClient):
    def __init__(self, addr=DEFAULT_ADDR): ...
    def send(self, msg):  # prot.pack 후 소켓 전송
    def recv(self):       # 소켓 수신 → prot.input 으로 패킷 분리
```

- `send`: `SimpleProtocolFilter.pack(msg)` → 길이 프리픽스 바이트 후 전송.
- `recv`: `socket.timeout`이면 빈 바이트, 아니면 `prot.input`이 완성된 패킷들을 yield.

#### `SimpleProtocolFilter`
`경로: poco/utils/simplerpc/transport/tcp/protocol.py:11`

| 요소 | 값 |
|------|-----|
| `HEADER_SIZE` | 4 |
| 패킷 포맷 | `[4바이트 little-end int 길이][UTF-8 본문]` (`struct.pack('i', ...)`) |
| `pack(content)` | str → utf-8 인코딩 후 길이 프리픽스 부착 (`protocol.py:39`) |
| `input(data)` | 스트림 누적 버퍼에서 완성 패킷마다 yield (`protocol.py:25`) |
| `unpack(data)` | `(length, content)` 분리 (`protocol.py:47`) |

→ JSON-RPC 본문을 TCP 위에서 안전하게 경계 구분하는 게 핵심. JSON-RPC 스펙엔 없는 Poco 고유 프레이밍.

#### `safetcp.Client`
`경로: poco/utils/simplerpc/transport/tcp/safetcp.py:9`

- `send`: 전부 전송될 때까지 루프 (partial send 처리).
- `recv` / `recv_all` / `recv_nonblocking`: 정확한 바이트 수 수신, 끊김 감지 시 `socket.error`. nonblocking에서 `10035`(EWOULDBLOCK)·`10053/10054`(연결 끊김) errno 구분.

#### WebSocket 대체 전송
`경로: poco/utils/simplerpc/transport/ws/main.py:11` — `WebSocketClient(addr="ws://localhost:5003")`. 데몬 스레드 `run_forever`, `_inbox` 큐로 메시지 수신. cocosjs 류 웹 타깃용.

---

### 1.10 Unity 연결(port 5001)에서 이 RPC의 역할 — 명시

| 계층 | Unity 경로의 구체 클래스 | 비고 |
|------|--------------------------|------|
| 진입점 | `UnityPoco(addr=("localhost",5001))` | `poco/drivers/unity3d/unity3d_poco.py:40` |
| 부모 | `StdPoco` → `StdPocoAgent` | `poco/drivers/std/__init__.py:26` |
| RPC | `RpcClient(TcpClient(addr))` | `std/__init__.py:28-31` |
| 전송 | `TcpClient` + `SimpleProtocolFilter` | 위 1.9 |

핵심 호출 흐름 (`unity3d_poco.py`):

| Python 호출 | wire method | 위치 |
|-------------|-------------|------|
| `poco.agent.rpc.call("Dump")` | `"Dump"` | StdDumper 경유 |
| `UnityPoco.send_message(message)` | `"SendMessage"` | `unity3d_poco.py:84` |
| `UnityPoco.invoke(listener, **kwargs)` | `"Invoke"`, `params={listener, data}` | `unity3d_poco.py:87` |
| `StdPocoAgent.get_sdk_version` | `"GetSDKVersion"` | `std/__init__.py:50` |

→ Unity 게임 내부(C# PocoSDK)는 동일 JSON-RPC 2.0 포맷으로 port 5001에서 listen하며, `"Dump"`(UI 트리), `"Click"`, `"Swipe"` 등의 method를 자기 dispatcher에 등록해 응답한다. **즉 이 simplerpc 패키지가 UnityPoco 연결의 프로토콜 그 자체**다. (OSX/Windows SDK가 등록하는 method 목록은 3장·2장의 `run()` 참고 — Unity SDK도 동일 method 집합을 C#로 구현.)

---

## 2. OSX 드라이버 SDK

> AbstractDumper / AbstractNode를 macOS **Accessibility(AX) API**(`atomac`/`AppKit`/`Quartz`)로 구현한 사례.

### 2.1 `OSXUIDumper`
`경로: poco/drivers/osx/sdk/OSXUIDumper.py:9`

```python
class OSXUIDumper(AbstractDumper):
    def __init__(self, root): ...
    def getRoot(self): return OSXUINode(self.RootControl, self)
```

| 책임 | 구현 |
|------|------|
| 루트 기준 좌표/크기 캐시 | `RootControl.AXSize` / `AXPosition` → `RootWidth/Height/Left/Top` (`OSXUIDumper.py:13`) |
| 유효성 | width/height == 0 이면 `InvalidSurfaceException` (창 최소화/과소; `OSXUIDumper.py:17`) |
| `getRoot()` | `OSXUINode`로 루트 래핑 — 이후 트리 순회는 `AbstractDumper.dumpHierarchy`가 담당 |

→ Dumper는 **루트 좌표계만 확정**하고 노드 순회/직렬화는 추상 베이스에 위임. 좌표 정규화의 분모를 여기서 잡는 게 핵심.

### 2.2 `OSXUINode`
`경로: poco/drivers/osx/sdk/OSXUINode.py:8`

```python
class OSXUINode(AbstractNode):
    def __init__(self, control, dumper): ...
    def getParent(self): ...
    def getChildren(self): ...   # generator
    def getAttr(self, attrName): ...
    def setAttr(self, attrName, val): ...
    def getAvailableAttributeNames(self): ...
```

AX 속성 → Poco 표준 속성 매핑:

| Poco attr | AX 소스 | 변환 | 위치 |
|-----------|---------|------|------|
| `name` | `AXTitle` 우선, 없으면 `AXRole[2:]` | `"AX"` 프리픽스 제거 | `OSXUINode.py:27` |
| `originType` | `AXRole` 원본 | 그대로 (`"Unknow"` 폴백) | `OSXUINode.py:34` |
| `type` | `AXRole[2:]` | 프리픽스 제거 | `OSXUINode.py:39` |
| `pos` | `AXPosition`+`AXSize` 중심점 | 루트 기준 **0~1 정규화** | `OSXUINode.py:44` |
| `size` | `AXSize` | 루트 폭/높이로 나눔 | `OSXUINode.py:49` |
| `text` | `AXValue` | str/int/float만 (그 외 None) | `OSXUINode.py:54` |

- `setAttr`: `text`만 쓰기 가능(`AXValue` 존재 시). 그 외 `UnableToSetAttributeException` (`OSXUINode.py:66`).
- `getChildren`은 `AXChildren`을 generator로 yield (`OSXUINode.py:17`).
- `getParent`은 `AXParent` 래핑 (`OSXUINode.py:14`).

### 2.3 `OSXFunc` (입력 백엔드)
`경로: poco/drivers/osx/sdk/OSXUIFunc.py:15`

`Quartz CGEvent` 기반 마우스/스크롤 + `AppKit` 앱 탐색 정적 메서드 모음.

| 분류 | 메서드 | 위치 |
|------|--------|------|
| 앱 탐색 | `getRunningApps`, `getAppRefByPid`, `getAppRefByBundleId`, `getAppRefByLocalizedName` | `OSXUIFunc.py:17~50` |
| 마우스 | `press`/`release`/`click`/`rclick`/`doubleclick`/`move`/`drag` | `OSXUIFunc.py:52~96` |
| 스크롤 | `scroll(vertical, horizontal, depth)` | `OSXUIFunc.py:98` |

- `pressID`/`releaseID` 테이블로 버튼 번호 → Quartz 이벤트 타입 매핑 (`OSXUIFunc.py:9`).

### 2.4 SDK 엔드포인트 — `PocoSDKOSX`
`경로: poco/drivers/osx/sdk/OSXUI.py:24`

게임-측 RPC 서버 역. `StdRpcReactor`에 method 등록 후 `StdRpcEndpointController`로 `serve_forever` (port 15004; `OSXUI.py:298`).

| 등록 method | 구현 요점 |
|-------------|-----------|
| `Dump` | `OSXUIDumper(root).dumpHierarchy()` |
| `Click`/`RClick`/`DoubleClick`/`Swipe`/`LongClick`/`Scroll` | `root.AXPosition`+`AXSize`로 정규화 좌표 → 실제 픽셀 환산 후 `OSXFunc` 호출 |
| `Screenshot` | `pyautogui` PNG → zlib deflate → base64 (`"png.deflate"`) |
| `ConnectWindow` | bundleid/appname/appname_re로 윈도우 선택 (교집합으로 유일성 확보) |
| `GetScreenSize`/`GetWindowRect`/`GetSDKVersion`/`KeyEvent`/`SetForeground` | AX 직접 조회 |

→ **위 method 집합이 곧 PocoSDK 표준 프로토콜의 서버측 계약**. 1장의 dispatcher에 대응.

---

## 3. Windows 드라이버 SDK

> AbstractDumper / AbstractNode를 **UIAutomation**(`uiautomation` 패키지, `win32gui` 보조)으로 구현한 사례.

### 3.1 `WindowsUIDumper`
`경로: poco/drivers/windows/sdk/WindowsUIDumper.py:8`

```python
class WindowsUIDumper(AbstractDumper):
    def __init__(self, root): ...
    def getRoot(self): return WindowsUINode(self.RootControl, self)
```

- 루트 좌표계는 `RootControl.BoundingRectangle`(`[left,top,right,bottom]`)에서 산출 (`WindowsUIDumper.py:12`).
- width/height == 0 → `InvalidSurfaceException` (`WindowsUIDumper.py:16`).
- OSX와 **구조 동일**: 좌표계만 확정, 순회는 베이스에 위임.

### 3.2 `WindowsUINode`
`경로: poco/drivers/windows/sdk/WindowsUINode.py:8`

| Poco attr | UIAutomation 소스 | 비고 | 위치 |
|-----------|-------------------|------|------|
| `name` | `Control.Name`, 없으면 `ControlTypeName` (`"Control"` 제거) | | `WindowsUINode.py:29` |
| `originType` | `ControlTypeName` 원본 | | `WindowsUINode.py:35` |
| `type` | `ControlTypeName.replace("Control","")` | | `WindowsUINode.py:38` |
| `pos` | `BoundingRectangle` 중심 | 루트 기준 0~1 정규화 | `WindowsUINode.py:41` |
| `size` | `BoundingRectangle` 폭/높이 | 정규화 | `WindowsUINode.py:48` |
| `text` | `ValuePattern.CurrentValue()` (가용 시) | 아니면 None | `WindowsUINode.py:54` |
| `_instanceId` | `Control.Handle` | OSX엔 없는 고유 키 | `WindowsUINode.py:61` |

- `getChildren`: **자식 캐싱**(`self.Children`)으로 중복 조회 방지(성능) (`WindowsUINode.py:18`).
- `setAttr`: `text`만 (`ValuePattern.SetValue`); 그 외 `UnableToSetAttributeException` (`WindowsUINode.py:66`).

### 3.3 SDK 엔드포인트 — `PocoSDKWindows`
`경로: poco/drivers/windows/sdk/WindowsUI.py:23`

OSX와 동일 패턴. `StdRpcReactor` + `StdRpcEndpointController`, port 15004 (`WindowsUI.py:253`).

| 차이점 (OSX 대비) | 내용 |
|-------------------|------|
| 입력 | `uiautomation`의 `DragDrop`/`WheelUp`/`WheelDown`/`SendKeys` 직접 사용 |
| 윈도우 선택 | `win32gui.EnumWindows`로 핸들 열거 → title/title_re/handle 매칭 (`WindowsUI.py:164~`) |
| 포그라운드 | `win32gui.ShowWindow` + `SetForegroundWindow` (`WindowsUI.py:159`) |
| Screenshot | `ToBitmap().ToFile` BMP → zlib → base64 (`"bmp.deflate"`) |
| PY2 처리 | 윈도우 타이틀 GBK 디코딩 (`WindowsUI.py:178`) |

---

## 4. 이식 관점 요약 (Unity 등 신규 엔진)

OSX·Windows SDK는 **동일한 추상 2종**(`poco/sdk/AbstractDumper`, `poco/sdk/AbstractNode`)을 플랫폼 API만 갈아끼워 구현한 병렬 사례다. 신규 엔진 이식의 체크리스트로 환원하면:

| 추상 | 구현해야 할 것 | OSX 예 | Windows 예 | Unity 이식 시 |
|------|----------------|--------|------------|----------------|
| `AbstractDumper.__init__` | 루트 좌표계(폭/높이/좌상단) 확정 + 유효성 | `AXSize`/`AXPosition` | `BoundingRectangle` | 카메라/Canvas 기준 RectTransform |
| `AbstractDumper.getRoot` | 엔진 루트를 Node로 래핑 | `OSXUINode` | `WindowsUINode` | UnityNode (C#) |
| `AbstractNode.getParent/getChildren` | 계층 순회 (generator 권장) | `AXParent`/`AXChildren` | `GetParentControl`/캐시된 `GetChildren` | Transform 계층 |
| `AbstractNode.getAttr` | 표준 attr 매핑: `name/type/pos/size/text` | AX role/value | ControlType/ValuePattern | GameObject name/Renderer bounds |
| `pos`/`size` 정규화 | **루트 기준 0~1** 변환이 핵심 계약 | 중심점/루트폭 | 중심점/루트폭 | 동일 규약 필수 |
| `setAttr` | 쓰기 가능 속성 한정(보통 `text`) | `AXValue` | `ValuePattern` | InputField.text 등 |
| SDK 엔드포인트(`run`) | method를 reactor/dispatcher에 등록, RPC serve | `PocoSDKOSX.run` | `PocoSDKWindows.run` | C# PocoSDK, port **5001** listen |

핵심 이식 포인트:

1. **프로토콜은 손대지 않는다.** Python 측 `UnityPoco`/`RpcClient`/`TcpClient`/`SimpleProtocolFilter`는 그대로 재사용. 게임-측에서 동일 JSON-RPC 2.0 + `[4B len][utf-8 json]` 프레이밍만 맞추면 됨.
2. **method 계약이 곧 인터페이스.** OSX/Windows `run()`이 등록하는 `Dump/Click/Swipe/LongClick/Scroll/Screenshot/KeyEvent/GetScreenSize/GetSDKVersion/...` 집합을 신규 엔진 SDK가 동일 이름으로 구현하면 기존 Python 드라이버가 그대로 붙는다.
3. **좌표 정규화 규약(0~1)** 을 어기면 클릭/스와이프가 전부 어긋난다 — 가장 흔한 이식 버그 지점.
4. Windows의 `_instanceId`(핸들), 자식 캐싱처럼 **엔진별 최적화/추가 attr는 자유롭게 확장** 가능 (`getAvailableAttributeNames` 오버라이드로 노출).

> Unity SDK는 OSX/Windows와 달리 게임 런타임(C#) 안에서 직접 port 5001을 열고 `Dump` 등을 구현하지만, **추상 계약·정규화 규약·RPC 포맷은 본 문서의 두 사례와 정확히 동형**이다.
