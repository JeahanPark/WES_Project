# WES QA Poco 포크 — M1 (라이브 UI 읽기) 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** QA 머신의 Python 클라이언트(`WesPoco`)가 Unity 게임 안의 자작 C# 서버에 TCP로 붙어, 실행 중인 UI 계층을 읽고 노드 속성을 단언할 수 있게 한다.

**Architecture:** 게임 내 자작 C# `WesPocoServer`(TcpListener + JSON-RPC 2.0 + `[4B LE len][utf-8]` 프레이밍)가 uGUI 트리를 Poco 표준 노드 JSON으로 덤프한다. QA측은 Poco를 최소 포크(드라이버·airtest 의존 제거)해 `poco.pocofw.Poco` + `Std{Dumper,Attributor,Screen,Input}`를 재사용하고, airtest 없는 `WesPocoAgent`로 직접 `host:port`에 연결한다.

**Tech Stack:** Python 3 (pytest, stdlib socket/struct/json), Unity 6 C# (TcpListener, Newtonsoft.Json), 다운로드 원본 Poco 소스 = `C:\Users\cgq02\Downloads\Poco-master\Poco-master`.

**프로토콜 계약 (원본 소스에서 확정):**
- 프레이밍: `struct.pack('i', len)` (4B signed int, little-endian) + utf-8 본문. (`poco/utils/simplerpc/transport/tcp/protocol.py`)
- 요청: `{"method": <str>, "params": [<args>] 또는 {<kwargs>} 또는 [], "jsonrpc": "2.0", "id": <uuid str>}` (`simplerpc.py:format_request`)
- 응답: `{"jsonrpc": "2.0", "result": <value>, "id": <같은 id>}` / 에러 시 `{"jsonrpc":"2.0","error":{...},"id":...}`
- 노드 스키마: `{"name": <str>, "payload": {<attr들>}, "children": [<node>] 또는 null}`. 속성은 `payload`에 둔다. (`poco/freezeui/hierarchy.py:Node`)

---

## File Structure

**Python 포크 (`tools/wesqa/`)**
- `tools/wesqa/poco/` — 벤더링한 Poco 최소 포크(드라이버·airtest 제거). 패키지명은 `poco` 유지(원본 절대 import `import poco.utils.six` 호환).
- `tools/wesqa/wesqa/__init__.py` — `WesPoco`(공개 API)
- `tools/wesqa/wesqa/agent.py` — `WesPocoAgent`(airtest 없는 에이전트, 직접 연결)
- `tools/wesqa/tests/fake_server.py` — 프로토콜을 그대로 말하는 가짜 서버(테스트용 + C# 계약 참조본)
- `tools/wesqa/tests/conftest.py` — pytest 픽스처
- `tools/wesqa/tests/test_connect.py`, `tools/wesqa/tests/test_hierarchy.py`
- `tools/wesqa/pytest.ini`, `tools/wesqa/README.md`

**C# SDK (`Assets/WesQA/`)**
- `Assets/WesQA/WesQA.asmdef` — `WES_QA` define 제약 어셈블리
- `Assets/WesQA/Runtime/JsonRpc.cs` — 요청/응답 봉투 파싱·직렬화
- `Assets/WesQA/Runtime/WesPocoServer.cs` — TcpListener + 프레이밍 + 메인스레드 디스패치
- `Assets/WesQA/Runtime/RpcMethods.cs` — `GetSDKVersion`/`GetScreenSize`/`Dump` 핸들러
- `Assets/WesQA/Runtime/HierarchyDumper.cs` — uGUI → 노드 JSON
- `Assets/WesQA/Runtime/WesQABootstrap.cs` — 플레이모드 진입 시 서버 자동 기동

---

## Phase 1 — Python 최소 포크 (Unity 불필요, 가짜 서버로 TDD)

### Task 1: 포크 스캐폴드 + Poco 벤더링·스트립

**Files:**
- Create: `tools/wesqa/poco/` (원본에서 복사 후 스트립)
- Create: `tools/wesqa/pytest.ini`
- Create: `tools/wesqa/README.md`

- [ ] **Step 1: 원본 Poco 패키지를 그대로 복사**

Run (PowerShell):
```powershell
$src = "C:\Users\cgq02\Downloads\Poco-master\Poco-master\poco"
$dst = "c:\GitFork\WES_Project\WES\tools\wesqa\poco"
New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
Copy-Item -Recurse -Force $src $dst
```

- [ ] **Step 2: 불필요 드라이버·airtest 의존 제거**

Run (PowerShell):
```powershell
$p = "c:\GitFork\WES_Project\WES\tools\wesqa\poco"
foreach ($d in "android","ios","cocosjs","netease","osx","qt","ue4","windows","unity3d") {
  Remove-Item -Recurse -Force "$p\drivers\$d" -ErrorAction SilentlyContinue
}
Remove-Item -Recurse -Force "$p\utils\airtest" -ErrorAction SilentlyContinue
# 모든 tests 디렉터리 제거
Get-ChildItem -Recurse -Directory -Path $p -Filter "tests" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
```

- [ ] **Step 3: airtest를 import하는 `drivers/std/__init__.py`·`drivers/__init__.py`를 빈 스트립본으로 교체**

`tools/wesqa/poco/drivers/__init__.py` 전체를 다음으로 덮어쓴다:
```python
# stripped fork: 개별 드라이버는 직접 import하지 않는다
```

`tools/wesqa/poco/drivers/std/__init__.py` 전체를 다음으로 덮어쓴다 (원본의 `StdPoco`/`StdPocoAgent`는 airtest 의존 → 제거. `dumper/attributor/screen/inputs.py`는 유지):
```python
# stripped fork: airtest 의존 StdPoco/StdPocoAgent 제거.
# Std{Dumper,Attributor,Screen,Input}는 wesqa.agent.WesPocoAgent가 직접 import한다.
```

- [ ] **Step 4: import 스모크 (실패 → 성공 확인)**

`tools/wesqa/pytest.ini` 생성:
```ini
[pytest]
testpaths = tests
python_files = test_*.py
```

Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); import poco; from poco.pocofw import Poco; from poco.drivers.std.dumper import StdDumper; from poco.drivers.std.attributor import StdAttributor; from poco.drivers.std.screen import StdScreen; from poco.drivers.std.inputs import StdInput; from poco.freezeui.hierarchy import FrozenUIHierarchy; print('OK')"
```
Expected: `OK` 출력 (어떤 import도 `airtest` 미요구). 만약 `ModuleNotFoundError: airtest` 발생 시 Step 3의 누락 파일을 찾아 추가 스트립.

- [ ] **Step 5: 제거 확인 + README**

`tools/wesqa/README.md` 생성:
```markdown
# wesqa — WES QA 자동화 (Poco 최소 포크)

`poco/`는 AirtestProject/Poco (Apache-2.0)의 최소 포크다. 드라이버(android/ios/unity3d 등)와
airtest 의존을 제거하고, 게임 내 자작 C# 서버(`Assets/WesQA/`)에 직접 TCP로 붙는다.

## 사용
    from wesqa import WesPoco
    game = WesPoco(instance=0)          # localhost:5001
    game('btn_inventory').click()       # (M2)
    assert game('wood_count').get_text() == "3"
```

Run (제거 확인):
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
Test-Path poco\drivers\unity3d ; Test-Path poco\utils\airtest
```
Expected: 둘 다 `False`.

- [ ] **Step 6: Commit**

```bash
git add tools/wesqa
git commit -m "wesqa: Poco 최소 포크 벤더링(드라이버·airtest 제거)"
```

---

### Task 2: 가짜 서버 + WesPocoAgent/WesPoco — 핸드셰이크 TDD

**Files:**
- Create: `tools/wesqa/tests/fake_server.py`
- Create: `tools/wesqa/tests/conftest.py`
- Create: `tools/wesqa/wesqa/__init__.py`
- Create: `tools/wesqa/wesqa/agent.py`
- Test: `tools/wesqa/tests/test_connect.py`

- [ ] **Step 1: 가짜 서버 작성 (프로토콜 그대로)**

`tools/wesqa/tests/fake_server.py`:
```python
# coding=utf-8
"""프레이밍·JSON-RPC를 원본 Poco 프로토콜 그대로 말하는 테스트용 서버.
C# WesPocoServer가 구현해야 할 계약의 실행 가능한 참조본이기도 하다."""
import json
import socket
import struct
import threading

HEADER = 4


class FakeWesPocoServer(object):
    def __init__(self, handlers, host="127.0.0.1", port=0):
        self.handlers = handlers
        self._stop = False
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind((host, port))
        self.sock.listen(1)
        self.host, self.port = self.sock.getsockname()
        self.thread = threading.Thread(target=self._serve, daemon=True)

    def start(self):
        self.thread.start()
        return self

    def _serve(self):
        try:
            conn, _ = self.sock.accept()
        except OSError:
            return
        buf = b""
        with conn:
            while not self._stop:
                try:
                    data = conn.recv(65536)
                except OSError:
                    break
                if not data:
                    break
                buf += data
                while len(buf) > HEADER:
                    (length,) = struct.unpack("i", buf[:HEADER])
                    if len(buf) < length + HEADER:
                        break
                    content = buf[HEADER:HEADER + length]
                    buf = buf[HEADER + length:]
                    self._handle(conn, content)

    def _handle(self, conn, content):
        req = json.loads(content.decode("utf-8"))
        params = req.get("params", [])
        args = params if isinstance(params, list) else []
        kwargs = params if isinstance(params, dict) else {}
        result = self.handlers[req["method"]](*args, **kwargs)
        resp = json.dumps({"jsonrpc": "2.0", "result": result, "id": req["id"]})
        payload = resp.encode("utf-8")
        conn.sendall(struct.pack("i", len(payload)) + payload)

    def stop(self):
        self._stop = True
        try:
            self.sock.close()
        except OSError:
            pass
```

- [ ] **Step 2: WesPocoAgent 작성 (airtest 없는 직접 연결)**

`tools/wesqa/wesqa/agent.py`:
```python
# coding=utf-8
from poco.agent import PocoAgent
from poco.drivers.std.dumper import StdDumper
from poco.drivers.std.attributor import StdAttributor
from poco.drivers.std.screen import StdScreen
from poco.drivers.std.inputs import StdInput
from poco.freezeui.hierarchy import FrozenUIHierarchy
from poco.utils.simplerpc.rpcclient import RpcClient
from poco.utils.simplerpc.transport.tcp.main import TcpClient
from poco.utils.simplerpc.utils import sync_wrapper


class WesPocoAgent(PocoAgent):
    """원본 StdPocoAgent에서 airtest 의존(AirtestInput·connect_device)을 제거하고
    host:port로 직접 연결하는 에이전트. 입력·스크린샷은 전부 RPC(StdInput/StdScreen)."""

    def __init__(self, addr):
        self.conn = TcpClient(addr)
        self.c = RpcClient(self.conn)
        self.c.DEBUG = False
        self.c.connect()

        hierarchy = FrozenUIHierarchy(StdDumper(self.c), StdAttributor(self.c))
        screen = StdScreen(self.c)
        inputs = StdInput(self.c)
        super(WesPocoAgent, self).__init__(hierarchy, inputs, screen, None)

    @property
    def rpc(self):
        return self.c

    @sync_wrapper
    def get_sdk_version(self):
        return self.c.call("GetSDKVersion")
```

- [ ] **Step 3: WesPoco 공개 API 작성**

`tools/wesqa/wesqa/__init__.py`:
```python
# coding=utf-8
from poco.pocofw import Poco
from .agent import WesPocoAgent

__all__ = ["WesPoco"]

DEFAULT_HOST = "localhost"
BASE_PORT = 5001


class WesPoco(Poco):
    """게임 내 WesPocoServer에 직접 붙는 Poco. 포트 = BASE_PORT + instance."""

    def __init__(self, instance=0, host=DEFAULT_HOST, port=None, **options):
        addr = (host, port if port is not None else BASE_PORT + instance)
        agent = WesPocoAgent(addr)
        options.setdefault("action_interval", 0.5)
        options["reevaluate_volatile_attributes"] = True
        super(WesPoco, self).__init__(agent, **options)

    def sdk_version(self):
        return self.agent.get_sdk_version()
```

- [ ] **Step 4: conftest 픽스처 + 실패 테스트 작성**

`tools/wesqa/tests/conftest.py`:
```python
# coding=utf-8
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))  # tools/wesqa
```

`tools/wesqa/tests/test_connect.py`:
```python
# coding=utf-8
import pytest
from fake_server import FakeWesPocoServer
from wesqa import WesPoco


@pytest.fixture
def server():
    srv = FakeWesPocoServer({"GetSDKVersion": lambda: "wesqa-0.1"}).start()
    yield srv
    srv.stop()


def test_handshake_returns_sdk_version(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game.sdk_version() == "wesqa-0.1"
```

- [ ] **Step 5: 실패 확인**

Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -m pytest tests/test_connect.py -v
```
Expected: 처음엔 import/구현 누락으로 FAIL. (Step 2~3을 모두 작성했다면 바로 통과할 수 있음 — 그 경우 Step 6으로.)

- [ ] **Step 6: 통과 확인**

Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -m pytest tests/test_connect.py -v
```
Expected: `test_handshake_returns_sdk_version PASSED`.

- [ ] **Step 7: Commit**

```bash
git add tools/wesqa
git commit -m "wesqa: 가짜 서버 + WesPocoAgent/WesPoco 핸드셰이크"
```

---

### Task 3: 계층 읽기 — exists()/get_text() TDD

**Files:**
- Test: `tools/wesqa/tests/test_hierarchy.py`

- [ ] **Step 1: canned 트리 + 실패 테스트 작성**

`tools/wesqa/tests/test_hierarchy.py`:
```python
# coding=utf-8
import pytest
from fake_server import FakeWesPocoServer
from wesqa import WesPoco

# Poco 노드 스키마: {"name", "payload":{attr...}, "children":[...] or None}
CANNED_TREE = {
    "name": "Root",
    "payload": {"name": "Root", "type": "Root", "visible": True},
    "children": [
        {
            "name": "InventoryPanel",
            "payload": {"name": "InventoryPanel", "type": "Panel", "visible": True},
            "children": [
                {
                    "name": "wood_count",
                    "payload": {"name": "wood_count", "type": "Text",
                                "visible": True, "text": "3"},
                    "children": None,
                }
            ],
        }
    ],
}


@pytest.fixture
def server():
    srv = FakeWesPocoServer({
        "GetSDKVersion": lambda: "wesqa-0.1",
        "Dump": lambda only_visible=True: CANNED_TREE,
    }).start()
    yield srv
    srv.stop()


def test_existing_node_is_found(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game("wood_count").exists() is True


def test_missing_node_is_absent(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game("does_not_exist").exists() is False


def test_read_text_attribute(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game("wood_count").get_text() == "3"
```

- [ ] **Step 2: 실패 확인**

Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -m pytest tests/test_hierarchy.py -v
```
Expected: FAIL 또는 ERROR (구현이 맞으면 바로 PASS 가능 — 코어를 그대로 재사용하므로). FAIL 시 메시지로 원인 파악(예: Poco 셀렉터가 추가 payload 키 요구).

- [ ] **Step 3: 필요한 최소 보정 (실패 시에만)**

Poco 셀렉터/프록시가 `pos`/`size` 등 추가 속성을 요구해 실패하면, `CANNED_TREE`의 각 payload에 다음을 추가한다(실제 C# 덤퍼도 동일 키를 내보낼 것이므로 계약과 일치):
```python
# payload에 추가
"pos": [0.5, 0.5], "size": [0.1, 0.05], "scale": [1, 1],
"anchorPoint": [0.5, 0.5], "zOrders": {"global": 0, "local": 0},
```

- [ ] **Step 4: 통과 확인**

Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -m pytest tests/ -v
```
Expected: `test_connect.py`·`test_hierarchy.py` 전부 PASSED.

- [ ] **Step 5: Commit**

```bash
git add tools/wesqa
git commit -m "wesqa: 계층 읽기(exists/get_text) 가짜 서버 TDD 통과"
```

---

## Phase 2 — 게임 내 C# SDK (Unity)

> 검증 방식: Unity는 pytest가 없으므로, C# 서버를 **Phase 1의 Python 클라이언트로 구동**해 검증한다(프로젝트 Dev-QA 관례 = MCP 플레이모드 + 실측). 각 Task는 에디터 플레이 진입(`u_play`) 후 Python 스모크 실행으로 "통과/실패"를 판정한다.

### Task 4: 어셈블리 + WES_QA define + 부트스트랩 골격

**Files:**
- Create: `Assets/WesQA/WesQA.asmdef`
- Create: `Assets/WesQA/Runtime/WesQABootstrap.cs`

- [ ] **Step 1: Newtonsoft.Json 패키지 확인**

`Packages/manifest.json`에 `com.unity.nuget.newtonsoft-json`이 없으면 추가:
```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```
이후 `u_editor_asset(action: refresh)`로 반영.

- [ ] **Step 2: asmdef 작성 (WES_QA define 제약)**

`Assets/WesQA/WesQA.asmdef`:
```json
{
  "name": "WesQA",
  "rootNamespace": "WesQA",
  "references": ["Unity.Nuget.Newtonsoft-Json"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "defineConstraints": ["WES_QA"],
  "versionDefines": [],
  "autoReferenced": true
}
```
`defineConstraints: ["WES_QA"]` → `WES_QA` 심볼이 없으면 이 어셈블리는 **컴파일 자체에서 제외**된다. 에디터/개발 빌드에서만 `WES_QA`를 켜고 릴리스 빌드엔 끈다 → Steam 빌드에 서버 코드 0.

- [ ] **Step 3: WES_QA 심볼을 에디터에 추가**

Player Settings → Scripting Define Symbols에 `WES_QA` 추가(또는 MCP `u_editor_*`로 설정). 추가 후 `u_editor_asset(action: refresh)`.

- [ ] **Step 4: 부트스트랩 골격 작성**

`Assets/WesQA/Runtime/WesQABootstrap.cs`:
```csharp
using UnityEngine;

namespace WesQA
{
    /// <summary>플레이모드 진입 시 WesPocoServer를 자동 기동. WES_QA define에서만 컴파일.</summary>
    public static class WesQABootstrap
    {
        private static WesPocoServer _server;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            int instance = ResolveInstanceIndex();
            _server = new WesPocoServer(5001 + instance);
            _server.Start();
            Debug.Log($"[WesQA] server starting on port {5001 + instance}");
        }

        // M1: 단일 인스턴스 = 0. 멀티클라(MPPM) index 해석은 M4에서 구현.
        private static int ResolveInstanceIndex() => 0;
    }
}
```

- [ ] **Step 5: 컴파일 확인**

`u_editor_asset(action: refresh)` 후 `u_console`로 컴파일 에러 없는지 확인. `WesPocoServer` 미정의로 에러가 나면 정상(Task 5에서 생성). 이 단계 통과 기준 = asmdef·부트스트랩이 인식되고 `WES_QA` define이 적용됨.

- [ ] **Step 6: Commit**

```bash
git add Assets/WesQA Packages/manifest.json
git commit -m "WesQA: asmdef(WES_QA 가드) + 부트스트랩 골격"
```

---

### Task 5: JsonRpc 봉투 + WesPocoServer(프레이밍·메인스레드 디스패치) + GetSDKVersion

**Files:**
- Create: `Assets/WesQA/Runtime/JsonRpc.cs`
- Create: `Assets/WesQA/Runtime/WesPocoServer.cs`
- Create: `Assets/WesQA/Runtime/RpcMethods.cs`

- [ ] **Step 1: JSON-RPC 봉투 작성**

`Assets/WesQA/Runtime/JsonRpc.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WesQA
{
    /// <summary>요청 {method, params, jsonrpc, id} 파싱 + 응답 {jsonrpc, result, id} 직렬화.</summary>
    public class RpcRequest
    {
        public string Method;
        public JToken Params;   // 배열(args) 또는 객체(kwargs)
        public string Id;

        public static RpcRequest Parse(string json)
        {
            var o = JObject.Parse(json);
            return new RpcRequest
            {
                Method = (string)o["method"],
                Params = o["params"],
                Id = (string)o["id"],
            };
        }

        /// <summary>위치 인자 리스트로 정규화(없으면 빈 리스트).</summary>
        public List<JToken> Args()
        {
            var list = new List<JToken>();
            if (Params is JArray arr) foreach (var t in arr) list.Add(t);
            return list;
        }
    }

    public static class RpcResponse
    {
        public static string Result(string id, object result)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["result"] = result,
                ["id"] = id,
            });
        }

        public static string Error(string id, string message)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["error"] = new Dictionary<string, object>
                {
                    ["code"] = -32603,
                    ["message"] = message,
                },
                ["id"] = id,
            });
        }
    }
}
```

- [ ] **Step 2: 서버(프레이밍 + 메인스레드 큐) 작성**

`Assets/WesQA/Runtime/WesPocoServer.cs`:
```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace WesQA
{
    /// <summary>TcpListener + [4B LE len][utf-8] 프레이밍 + JSON-RPC 디스패치.
    /// 핸들러는 Unity API 접근을 위해 메인스레드에서 실행되도록 큐잉한다.</summary>
    public class WesPocoServer
    {
        private readonly int _port;
        private TcpListener _listener;
        private Thread _accept;
        private volatile bool _running;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private GameObject _pump;

        public WesPocoServer(int port) { _port = port; }

        public void Start()
        {
            _running = true;
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _accept = new Thread(AcceptLoop) { IsBackground = true };
            _accept.Start();

            _pump = new GameObject("[WesQA.Pump]");
            UnityEngine.Object.DontDestroyOnLoad(_pump);
            _pump.AddComponent<WesQAPump>().Bind(this);
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        // 메인스레드에서 매 프레임 호출(WesQAPump.Update)
        internal void PumpMainThread()
        {
            while (_mainThread.TryDequeue(out var act)) act();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    new Thread(() => ClientLoop(client)) { IsBackground = true }.Start();
                }
                catch { if (_running) Thread.Sleep(50); }
            }
        }

        private void ClientLoop(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var header = new byte[4];
                while (_running)
                {
                    if (!ReadExactly(stream, header, 4)) break;
                    int len = BitConverter.ToInt32(header, 0); // 프로토콜이 LE; Unity x64도 LE
                    var body = new byte[len];
                    if (!ReadExactly(stream, body, len)) break;
                    string json = Encoding.UTF8.GetString(body);
                    DispatchOnMainThread(stream, json);
                }
            }
        }

        private void DispatchOnMainThread(NetworkStream stream, string json)
        {
            var done = new ManualResetEventSlim(false);
            string response = null;
            _mainThread.Enqueue(() =>
            {
                RpcRequest req = null;
                try
                {
                    req = RpcRequest.Parse(json);
                    object result = RpcMethods.Invoke(req);
                    response = RpcResponse.Result(req.Id, result);
                }
                catch (Exception e)
                {
                    response = RpcResponse.Error(req != null ? req.Id : null, e.Message);
                }
                done.Set();
            });
            done.Wait();
            Send(stream, response);
        }

        private static void Send(NetworkStream stream, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var header = BitConverter.GetBytes(payload.Length); // 4B LE
            stream.Write(header, 0, 4);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static bool ReadExactly(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }
    }

    /// <summary>메인스레드 큐를 매 프레임 비우는 펌프 컴포넌트.</summary>
    public class WesQAPump : MonoBehaviour
    {
        private WesPocoServer _server;
        public void Bind(WesPocoServer s) { _server = s; }
        private void Update() { _server?.PumpMainThread(); }
        private void OnDestroy() { _server?.Stop(); }
    }
}
```

- [ ] **Step 3: 메서드 디스패처 + GetSDKVersion 작성**

`Assets/WesQA/Runtime/RpcMethods.cs`:
```csharp
using System;

namespace WesQA
{
    /// <summary>RPC 메서드 라우팅. 모든 핸들러는 메인스레드에서 호출됨(Unity API 안전).</summary>
    public static class RpcMethods
    {
        public const string SdkVersion = "wesqa-0.1";

        public static object Invoke(RpcRequest req)
        {
            switch (req.Method)
            {
                case "GetSDKVersion":
                    return SdkVersion;
                default:
                    throw new NotSupportedException($"unknown method: {req.Method}");
            }
        }
    }
}
```

- [ ] **Step 4: 컴파일 + 플레이 진입**

`u_editor_asset(action: refresh)` → `u_console`로 컴파일 에러 0 확인 → `u_play(action: start)`. `u_console`에 `[WesQA] server starting on port 5001` 로그 확인.

- [ ] **Step 5: Python 스모크로 핸드셰이크 검증**

`u_play`로 플레이 중인 상태에서 Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); from wesqa import WesPoco; g=WesPoco(instance=0); print(g.sdk_version())"
```
Expected: `wesqa-0.1` 출력. (연결 거부 시 `WES_QA` define·포트·방화벽 확인.) 검증 후 `u_play(action: stop)`.

- [ ] **Step 6: Commit**

```bash
git add Assets/WesQA
git commit -m "WesQA: TCP 서버 + JSON-RPC 프레이밍 + GetSDKVersion (Python 핸드셰이크 통과)"
```

---

### Task 6: GetScreenSize + HierarchyDumper + Dump

**Files:**
- Create: `Assets/WesQA/Runtime/HierarchyDumper.cs`
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs`

- [ ] **Step 1: HierarchyDumper 작성 (uGUI → 노드 dict)**

`Assets/WesQA/Runtime/HierarchyDumper.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WesQA
{
    /// <summary>모든 Canvas 루트를 순회해 Poco 노드 스키마
    /// {name, payload{...}, children[]}로 덤프. 좌표는 스크린 정규화(0~1).</summary>
    public static class HierarchyDumper
    {
        public static Dictionary<string, object> Dump(bool onlyVisibleNode)
        {
            var root = NewNode("Root", "Root", true, null);
            var children = new List<object>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.transform.parent != null) continue; // 루트 Canvas만
                var node = BuildNode(canvas.gameObject, onlyVisibleNode);
                if (node != null) children.Add(node);
            }
            root["children"] = children;
            return root;
        }

        private static Dictionary<string, object> BuildNode(GameObject go, bool onlyVisible)
        {
            bool visible = go.activeInHierarchy;
            if (onlyVisible && !visible) return null;

            var rt = go.transform as RectTransform;
            var payload = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["type"] = ResolveType(go),
                ["visible"] = visible,
                ["text"] = ResolveText(go),
                ["_instanceId"] = go.GetInstanceID(),
                ["clickable"] = go.GetComponent<Selectable>() != null,
            };
            FillRect(payload, rt);

            var node = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["payload"] = payload,
            };

            var kids = new List<object>();
            foreach (Transform child in go.transform)
            {
                var c = BuildNode(child.gameObject, onlyVisible);
                if (c != null) kids.Add(c);
            }
            node["children"] = kids.Count > 0 ? kids : null;
            return node;
        }

        private static string ResolveType(GameObject go)
        {
            if (go.GetComponent<Button>() != null) return "Button";
            if (go.GetComponent<Toggle>() != null) return "Toggle";
            if (go.GetComponent<InputField>() != null) return "InputField";
            if (go.GetComponent<Text>() != null) return "Text";
            if (go.GetComponent<Image>() != null) return "Image";
            return go.transform is RectTransform ? "Node" : "GameObject";
        }

        private static string ResolveText(GameObject go)
        {
            var t = go.GetComponent<Text>();
            if (t != null) return t.text;
            var inp = go.GetComponent<InputField>();
            if (inp != null) return inp.text;
            return null;
        }

        // RectTransform → 스크린 중심 정규화 pos·size
        private static void FillRect(Dictionary<string, object> payload, RectTransform rt)
        {
            float sw = Screen.width, sh = Screen.height;
            if (rt == null || sw <= 0 || sh <= 0)
            {
                payload["pos"] = new[] { 0.5f, 0.5f };
                payload["size"] = new[] { 0f, 0f };
                payload["anchorPoint"] = new[] { 0.5f, 0.5f };
                payload["scale"] = new[] { 1f, 1f };
                payload["zOrders"] = new Dictionary<string, object> { ["global"] = 0, ["local"] = 0 };
                return;
            }
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            // 캔버스 모드와 무관하게 화면 픽셀로 환산
            var cam = GetCanvasCamera(rt);
            Vector3 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector3 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float cx = (min.x + max.x) * 0.5f / sw;
            float cy = 1f - (min.y + max.y) * 0.5f / sh; // 좌상단 원점(Poco 관례)
            payload["pos"] = new[] { cx, cy };
            payload["size"] = new[] { Mathf.Abs(max.x - min.x) / sw, Mathf.Abs(max.y - min.y) / sh };
            payload["anchorPoint"] = new[] { 0.5f, 0.5f };
            payload["scale"] = new[] { rt.localScale.x, rt.localScale.y };
            payload["zOrders"] = new Dictionary<string, object>
            {
                ["global"] = 0,
                ["local"] = rt.GetSiblingIndex(),
            };
        }

        private static Camera GetCanvasCamera(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas != null ? canvas.worldCamera : null;
        }
    }
}
```

- [ ] **Step 2: 디스패처에 Dump·GetScreenSize 추가**

`Assets/WesQA/Runtime/RpcMethods.cs`의 `Invoke` switch에 case 추가:
```csharp
                case "GetScreenSize":
                    return new[] { (object)UnityEngine.Screen.width, UnityEngine.Screen.height };
                case "Dump":
                {
                    bool onlyVisible = true;
                    var args = req.Args();
                    if (args.Count >= 1) onlyVisible = args[0].ToObject<bool>();
                    return HierarchyDumper.Dump(onlyVisible);
                }
```

- [ ] **Step 3: 컴파일 + 플레이 진입**

`u_editor_asset(action: refresh)` → `u_console` 에러 0 → `u_play(action: start)`.

- [ ] **Step 4: Python 스모크로 실제 Canvas 읽기 검증**

Ingame 씬에 알려진 UI(예: `PlayerStatusHUD`)가 떠 있는 상태에서 Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python -c "import sys; sys.path.insert(0,'.'); from wesqa import WesPoco; g=WesPoco(instance=0); print('size', g.agent.rpc.call('GetScreenSize').wait()); root=g.agent.rpc.call('Dump', True).wait(); print('dump ok' if root else 'EMPTY')"
```
Expected: `size ([w, h], None)` 형태 + `dump ok`. (예외 시 `u_console`의 `[WesQA]` 에러 확인.) 검증 후 `u_play(action: stop)`.

- [ ] **Step 5: Commit**

```bash
git add Assets/WesQA
git commit -m "WesQA: HierarchyDumper + Dump/GetScreenSize (실 Canvas 덤프 통과)"
```

---

### Task 7: 통합 스모크 — 실제 UI 노드 단언

**Files:**
- Create: `tools/wesqa/tests/smoke_editor.py` (수동 실행 스모크, 에디터 플레이 필요)

- [ ] **Step 1: 에디터 대상 스모크 스크립트 작성**

`tools/wesqa/tests/smoke_editor.py`:
```python
# coding=utf-8
"""에디터 플레이모드의 실제 게임에 붙어 UI 노드를 단언하는 수동 스모크.
사용: Unity 에디터에서 Ingame 씬 플레이 중 실행.
    python tests/smoke_editor.py <존재하는_노드이름>"""
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
from wesqa import WesPoco


def main():
    node_name = sys.argv[1] if len(sys.argv) > 1 else "PlayerStatusHUD"
    game = WesPoco(instance=0)
    print("sdk:", game.sdk_version())
    found = game(node_name).exists()
    print(f"node '{node_name}' exists:", found)
    assert found, f"'{node_name}' 노드를 찾지 못함 — 덤프 스키마/씬 확인"
    print("SMOKE OK")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 플레이 진입 + 스모크 실행**

`u_play(action: start)` (Ingame 씬, HUD 표시 상태) 후 Run:
```powershell
cd c:\GitFork\WES_Project\WES\tools\wesqa
python tests/smoke_editor.py PlayerStatusHUD
```
Expected: `sdk: wesqa-0.1` / `node 'PlayerStatusHUD' exists: True` / `SMOKE OK`. 노드명이 다르면 `u_editor_gameobject`로 실제 HUD 루트 이름을 확인해 인자 교체. 검증 후 `u_play(action: stop)`.

- [ ] **Step 3: Commit**

```bash
git add tools/wesqa/tests/smoke_editor.py
git commit -m "wesqa: 에디터 통합 스모크(실 UI 노드 단언) — M1 완료"
```

---

## M1 완료 기준 (Definition of Done)

- [ ] `python -m pytest tools/wesqa/tests/ -v` 전부 PASS (가짜 서버 대상, Unity 불필요)
- [ ] 에디터 플레이 중 `smoke_editor.py`가 실제 UI 노드를 찾아 `SMOKE OK`
- [ ] `WES_QA` define 없이 빌드하면 `Assets/WesQA`가 컴파일에서 제외됨(릴리스 안전) — `u_console`로 확인
- [ ] 게임 내 코드(`Assets/WesQA`)에 중국 원본 소스 0줄(전부 자작), Python 포크는 QA 머신 전용

## 다음 (별도 플랜)

- **M2**: InputInjector + `Click/Swipe/LongClick/Scroll/KeyEvent/SetText`
- **M3**: Screenshot + aircv(template matching) 추출 + 경량 HTML 리포트
- **M4**: MPPM 멀티클라 포트 매핑 + `Invoke`/`SendMessage` → `Managers.Test` 브리지
- **M5(선택)**: 녹화, keypoint matching(SIFT/AKAZE)
