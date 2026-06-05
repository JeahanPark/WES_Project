---
type: reference
source: Airtest (AirtestProject/Airtest)
generated: subagent
---

# Airtest 03 — 플랫폼 백엔드 (Android / iOS / Windows / Linux)

`airtest/core/<platform>/` 하위의 모든 `.py` 파일을 읽고 정리한 레퍼런스다. 각 플랫폼의 `Device` 구현체가 추상 베이스 `airtest/core/device.py:Device` 의 메서드를 어떻게 채우는지, 스크린캡/입력/연결 URI가 어떻게 동작하는지를 다룬다.

---

## 0. 공통 추상 베이스 — `airtest/core/device.py`

모든 플랫폼 Device 의 부모. `MetaDevice` 메타클래스가 클래스 이름→클래스를 `MetaDevice.REGISTRY` 에 자동 등록한다 (`api.init_device` 가 platform 이름으로 클래스를 찾을 때 사용).

```python
class MetaDevice(type):
    REGISTRY = {}
    def __new__(meta, name, bases, class_dict): ...  # REGISTRY[name] = cls

class Device(with_metaclass(MetaDevice, object)):
    def __init__(self): ...
```

추상 메서드(미구현 시 `NotImplementedError`):

| 메서드 | 시그니처 | 의미 |
|--------|----------|------|
| `platform` (property) | `→ str` | 클래스명 소문자. 기본 구현 제공 |
| `uuid` (property) | `→ str` | 디바이스 식별자 |
| `shell` | `(*args, **kwargs)` | 셸 명령 |
| `snapshot` | `(*args, **kwargs)` | 스크린샷 (cv2 이미지) |
| `touch` | `(target, **kwargs)` | 탭 |
| `double_click` | `(target)` | 더블 클릭 (직접 `raise NotImplementedError`) |
| `swipe` | `(t1, t2, **kwargs)` | 스와이프 |
| `keyevent` | `(key, **kwargs)` | 키 이벤트 |
| `text` | `(text, enter=True)` | 텍스트 입력 |
| `start_app`/`stop_app`/`clear_app`/`list_app`/`install_app`/`uninstall_app` | 앱 라이프사이클 |
| `get_current_resolution`/`get_render_resolution` | 해상도 |
| `get_ip_address` | IP |
| `set_clipboard`/`get_clipboard` | 클립보드 |
| `paste` | `()` | 기본 구현 = `self.text(self.get_clipboard())` |
| `disconnect` | `()` | 기본 구현 = `pass` |
| `to_json` | `()` | `<ClassName 'uuid'>` |

`_raise_not_implemented_error()` 가 `NotImplementedError("Method not implemented on %s")` 를 던진다.

### connect URI 파싱 (`airtest/core/api.py:connect_device` → `parse_device_uri`)
URI 형식: `Platform://adbhost:adbport/uuid?param=value&param2=value2`
- scheme = platform 이름 (대소문자 무관, `init_device` 가 `MetaDevice.REGISTRY` 에서 찾음)
- netloc = host(:port) → Android 한정 adb server 주소
- path = uuid (serialno / handle / wda addr)
- query = `**params` 로 Device `__init__` 에 전달

---

## 1. Android — `airtest/core/android/`

### 1.1 파일 역할 표

| 파일 | 핵심 클래스/함수 | 역할 |
|------|------------------|------|
| `__init__.py` | — | `Android` re-export |
| `android.py` | `Android(Device)` | 메인 Device 구현. adb + proxy 조합 |
| `adb.py` | `ADB` | adb 클라이언트 래퍼 (셸/포워드/앱/디스플레이) |
| `constant.py` | `CAP_METHOD`/`TOUCH_METHOD`/`IME_METHOD`/`ORI_METHOD`, 경로 상수 | 상수·기본 경로 |
| `cap_methods/base_cap.py` | `BaseCap` | 스크린캡 베이스 |
| `cap_methods/adbcap.py` | `AdbCap(BaseCap)` | `adb shell screencap` 캡처 (느림, fallback) |
| `cap_methods/javacap.py` | `Javacap(Yosemite, BaseCap)` | Yosemite 앱 기반 캡처 (호환성↑) |
| `cap_methods/minicap.py` | `Minicap(BaseCap)` | stf minicap 기반 고속 캡처 |
| `cap_methods/screen_proxy.py` | `ScreenProxy` | 캡처 방식 자동 선택 프록시 |
| `touch_methods/base_touch.py` | `BaseTouch`, `MotionEvent`/`DownEvent`/`UpEvent`/`MoveEvent`/`SleepEvent` | 터치 베이스 + 모션 이벤트 |
| `touch_methods/minitouch.py` | `Minitouch(BaseTouch)` | stf minitouch (Android<10) |
| `touch_methods/maxtouch.py` | `Maxtouch(BaseTouch)` | maxpresent.jar (Android10+) |
| `touch_methods/touch_proxy.py` | `TouchProxy`, `*Implementation` | 터치 방식 자동 선택 + 좌표 변환 래핑 |
| `ime.py` | `CustomIme`, `YosemiteIme` | 입력기 (텍스트 입력) |
| `yosemite.py` | `Yosemite` | Yosemite.apk 설치/관리 베이스 |
| `yosemite_ext.py` | `YosemiteExt(Yosemite)` | 클립보드/언어 등 확장 device_op |
| `recorder.py` | `Recorder(Yosemite)` | Yosemite 화면 녹화 |
| `rotation.py` | `RotationWatcher`, `XYTransformer` | 회전 감시 + 좌표 회전 변환 |

### 1.2 `Android(Device)` — `android.py`

```python
def __init__(self, serialno=None, host=None,
             cap_method=CAP_METHOD.MINICAP,
             touch_method=TOUCH_METHOD.MINITOUCH,
             ime_method=IME_METHOD.YOSEMITEIME,
             ori_method=ORI_METHOD.MINICAP,
             display_id=None, input_event=None,
             adb_path=None, name=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `serialno` | str | None | adb 시리얼. None 이면 `get_default_device()` 로 첫 device |
| `host` | (str,int) | None | adb server 주소 → `ADB(server_addr=host)` |
| `cap_method` | str | `"MINICAP"` | 스크린캡 방식. `.upper()` 저장 |
| `touch_method` | str | `"MINITOUCH"` | 터치 방식 |
| `ime_method` | str | `"YOSEMITEIME"` | 입력기 |
| `ori_method` | str | `"MINICAPORI"` | 회전 감시 방식 |
| `display_id` | str | None | 멀티 디스플레이 ID (`screencap -d`, `minicap -d`) |
| `input_event` | str | None | minitouch 입력 디바이스 노드 (`-d`) |
| `adb_path` | str | None | adb 실행파일 경로 |
| `name` | str | None | uuid 별칭 |

**핵심 동작**
- `self.adb = ADB(...)` → `wait_for_device()` → `sdk_version` 조회.
- **자동 보정**: `sdk_version >= SDK_VERISON_ANDROID10(29)` 이고 `touch_method==MINITOUCH` 이면 → `MAXTOUCH` 로 강제 변경 (`android.py:61`).
- 컴포넌트 초기화: `RotationWatcher`, `YosemiteIme`, `Recorder`, `YosemiteExt`, `_register_rotation_watcher()`.
- `touch_proxy`/`screen_proxy` 는 lazy property (최초 접근 시 `auto_setup`).
- `uuid` = `name|serialno` + (`display_id`) + (`input_event`) 를 `_` 로 join.

**주요 메서드**

| 메서드 | 시그니처 | 위임 대상 / 동작 |
|--------|----------|------------------|
| `snapshot` | `(filename=None, ensure_orientation=True, quality=10, max_size=None)` | `self.screen_proxy.snapshot(...)` → 필요 시 `aircv.imwrite` |
| `shell` | `(*args, **kwargs)` | `self.adb.shell` |
| `touch` | `(pos, duration=0.01)` | 절대좌표 변환 후 `touch_proxy.touch`. 반환=실제 좌표 |
| `double_click` | `(pos)` | `touch` 2회 (0.05s 간격) |
| `swipe` | `(p1, p2, duration=0.5, steps=5, fingers=1)` | `touch_proxy.swipe`. `fingers` 1/2 |
| `pinch` | `(center=None, percent=0.5, duration=0.5, steps=5, in_or_out='in')` | minitouch/maxtouch 전용 |
| `swipe_along` | `(coordinates_list, duration=0.8, steps=5)` | 다점 스와이프 |
| `two_finger_swipe` | `(tuple_from_xy, tuple_to_xy, duration=0.8, steps=5, offset=(0,50))` | 2지 스와이프 |
| `text` | `(text, enter=True, **kwargs)` | YosemiteIme → 실패 시 `adb.text`. `search` kwarg 시 IME action 3 |
| `keyevent` | `(keyname, **kwargs)` | `adb.keyevent` |
| `wake`/`home` | `()` | KEYCODE_WAKEUP/MENU/HOME, 잠금 시 Yosemite 해제 |
| `start_app` | `(package, activity=None)` | `adb.start_app` |
| `start_app_timing` | `(package, activity)` | 실행 시간(ms) 반환 |
| `stop_app`/`clear_app`/`install_app`/`install_multiple_app`/`uninstall_app`/`list_app`/`path_app`/`check_app` | — | 모두 `self.adb.*` 위임 |
| `get_clipboard`/`set_clipboard` | — | `yosemite_ext` 위임 |
| `push`/`pull` | — | `adb.push`/`adb.pull` |
| `display_info` (property) | — | `get_display_info()` 캐시 + `_current_orientation` 반영 |
| `get_current_resolution` | `()` | 회전 반영한 (w,h). orientation 1/3 이면 swap |
| `get_render_resolution` | `(refresh=False, package=None)` | all-screen offset (x,y,w,h) |
| `start_recording` | `(max_time=1800, output=None, fps=10, mode="yosemite", snapshot_sleep=0.001, orientation=0, bit_rate_level=None, bit_rate=None, max_size=None)` | mode="yosemite"→`Recorder`, "ffmpeg"→`ScreenRecorder` |
| `stop_recording` | `(output=None, is_interrupted=None)` | — |
| `disconnect` | `()` | screen/touch/rotation teardown |
| `_touch_point_by_orientation` | `(tuple_xy)` | `XYTransformer.up_2_ori` 로 이미지→물리 좌표 |

**하위 호환 (deprecated)**: 파일 끝에서 `Android.minicap/javacap/minitouch/maxtouch` 를 property 로 정의 → `get_deprecated_var` 가 DeprecationWarning 후 `screen_proxy`/`touch_proxy` 반환 (`android.py:1138-1141`).

### 1.3 `ADB` — `adb.py`

adb 명령 래퍼. `ADB._instances` 에 인스턴스 등록(atexit 시 `cleanup_adb_forward` 가 포워드 정리).

```python
def __init__(self, serialno=None, adb_path=None, server_addr=None,
             display_id=None, input_event=None):
```

| 클래스 상수 | 값 |
|-------------|-----|
| `status_device` | `"device"` |
| `status_offline` | `"offline"` |
| `SHELL_ENCODING` | `"utf-8"` |
| `TMP_PATH`(모듈) | `"/data/local/tmp"` |

**adb 경로 결정** (`get_adb_path` 정적):
1. 실행 중인 adb 프로세스의 `exe` 경로 (psutil)
2. `ANDROID_HOME/platform-tools/adb[.exe]`
3. 빌트인 `DEFAULT_ADB_PATH` (`constant.py`)

**주요 메서드 (선택)**

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `start_cmd` | `(cmds, device=True)` | `subprocess.Popen` 으로 adb 실행. device=True 면 `-s serialno` |
| `cmd` | `(cmds, device=True, ensure_unicode=True, timeout=None)` | 실행+stdout. returncode>0 시 `DeviceConnectionError`/`AdbError` |
| `devices` | `(state=None)` | `[(serialno, state), ...]` |
| `connect`/`disconnect` | `(force=False)` / `()` | `:` 포함 시 `adb connect/disconnect` |
| `wait_for_device` | `(timeout=5)` | `adb wait-for-device` |
| `raw_shell`/`shell` | `(cmds)` | `adb shell`. sdk<7(24) 면 `echo ---$?---` 로 returncode 추출 |
| `keyevent` | `(keyname)` | `input keyevent KEYNAME` |
| `getprop` | `(key, strip=True)` | `getprop` |
| `sdk_version` (property, `@retries(3)`) | — | `ro.build.version.sdk` int |
| `push` | `(local, remote)` | tmp 경유 push + mv/cp. 반환 device 경로 |
| `pull` | `(remote, local="")` | `adb pull` |
| `forward` | `(local, remote, no_rebind=True)` | `adb forward`. `_forward_local_using` 추적 |
| `get_available_forward_local` (classmethod) | — | `random.randint(11111, 50000)` |
| `setup_forward` (`@retries(3)`) | `(device_port, no_rebind=True)` | 랜덤 로컬포트 → forward |
| `remove_forward` | `(local=None)` | `--remove`/`--remove-all` |
| `install_app`/`install_multiple_app`/`pm_install` | `(filepath, replace=False, install_options=None)` | 설치 (서명 불일치 시 재설치) |
| `uninstall_app`/`pm_uninstall` | `(package[, keepdata])` | 제거 |
| `snapshot` | `()` | `screencap -p` (display_id 시 `-d`). `line_breaker`→`\n` 치환 |
| `touch` | `(tuple_xy)` | `input tap x y` (adbtouch fallback) |
| `swipe` | `(tuple_x0y0, tuple_x1y1, duration=500)` | sdk별 `input swipe`/`input touchscreen swipe` |
| `logcat` | `(grep_str="", extra_args="", read_timeout=10)` | 제너레이터 |
| `get_display_info`/`getPhysicalDisplayInfo`/`getMaxXY`/`getDisplayOrientation` | — | 디스플레이 정보 (아래 표) |
| `get_top_activity` | `()` | `(package, activity, pid)` |
| `is_keyboard_shown`/`is_screenon`/`is_locked`/`unlock` | — | 상태/잠금 |
| `get_ip_address`/`get_gateway_address` | — | eth0/eth1/wlan0 순회 |
| `get_device_info` | `()` | memory/storage/display/cpu/gpu/model 등 dict |
| `get_display_of_all_screen` | `(info, package=None)` | all-screen offset |

`get_display_info()` 반환 예:
```python
{'width':1440,'height':2960,'density':4.0,'orientation':3,'rotation':270,'max_x':4095,'max_y':4095}
```
- `orientation` ∈ {0,1,2,3}, `rotation` = orientation*90.

### 1.4 스크린캡 (`cap_methods/`)

#### `ScreenProxy` — `screen_proxy.py`
캡처 방식 자동 선택 프록시. `__getattr__` 로 내부 `screen_method` 에 위임, `method_name` 은 클래스명 대문자.

```python
@classmethod
def auto_setup(cls, adb, default_method=None, *args, **kwargs): ...
```
- 우선순위: **Custom > MINICAP > JAVACAP > ADBCAP** (등록은 역순, `register_screen()` 에서 ADBCAP→JAVACAP→MINICAP 순으로 dict 삽입 후 `reversed` 순회).
- `default_method` 지정 시 먼저 시도, `check_frame()`(1프레임 획득 테스트) 성공해야 채택.
- 모두 실패 시 `ScreenError`.

#### `BaseCap` — `base_cap.py`
- `get_frame_from_stream()` (추상), `get_frame()`=`get_frame_from_stream()`.
- `snapshot(ensure_orientation=True)` → `aircv.utils.string_2_img` 로 cv2 변환.

#### `Minicap(BaseCap)` — `minicap.py`
stf minicap 기반 고속 캡처 (참조: openstf/minicap).

| 상수 | 값 |
|------|-----|
| `VERSION` | 5 |
| `RECVTIMEOUT` | 3 (초; 1.2.7+ 변경) |
| `CMD` | `LD_LIBRARY_PATH=/data/local/tmp /data/local/tmp/minicap` |

```python
def __init__(self, adb, projection=None, rotation_watcher=None,
             display_id=None, ori_function=None):
```
- `rotation_watcher` 등록 → 가로/세로 전환 시 stream 재연결 (`update_rotation`).
- `install_or_upgrade` (`@ready_method`): `/data/local/tmp/minicap`+`minicap.so` 존재/버전 확인 후 `install()` (abi별 STFLIB 바이너리 push, sdk<16 면 `minicap-nopie`).
- `get_frame(projection=None)`: `minicap ... -s` 단발 캡처. JPG magic(`\xff\xd8`..`\xff\xd9`) 검증.
- `get_stream(lazy=True)` / `_get_stream`: `adb forward localabstract:minicap_*` + SafeSocket. 헤더 `struct.unpack("<2B5I2B")`. quirk-bitflag 처리.
- `get_frame_from_stream` (`@retry_when_socket_error`): 스트림에서 1프레임.
- `_cleanup_minicap()`: `__skb_wait_for_more_packets`/`futex_wait_queue_me` 상태 좀비 kill.

#### `Javacap(Yosemite, BaseCap)` — `javacap.py`
Yosemite 앱의 Capture 서비스 사용 (느리지만 호환성↑).

| 상수 | 값 |
|------|-----|
| `APP_PKG` | `com.netease.nie.yosemite` |
| `SCREENCAP_SERVICE` | `com.netease.nie.yosemite.Capture` |
| `RECVTIMEOUT` | None |

- `_setup_stream_server` (`@on_method_ready('install_or_upgrade')`): `adb forward localabstract:javacap_*` + `app_process ... --scale 100 --socket ... -lazy`. `"Capture server listening on"` 대기.
- `get_frames` (`@threadsafe_generator`): 소켓에서 헤더(4B)+frame 수신.

#### `AdbCap(BaseCap)` — `adbcap.py`
`adb shell screencap` fallback (가장 느림, 경고 출력). sdk<=7 시 orientation 회전 보정.

### 1.5 터치 (`touch_methods/`)

#### `TouchProxy` — `touch_proxy.py`
터치 방식 프록시. `TOUCH_METHODS` 에 `register_touch` 데코레이터로 등록.

```python
@classmethod
def auto_setup(cls, adb, default_method=None, ori_transformer=None,
               size_info=None, input_event=None): ...
```
- 등록 순서 = MINITOUCH → MAXTOUCH (OrderedDict). default 지정 시 우선.
- `check_touch()` = `base_touch.install_and_setup()` 성공 여부.
- 모두 실패 시 **`AdbTouchImplementation`** (adb `input tap`/`swipe`) 로 폴백 + 경고.

**Implementation 계층** (좌표 변환 래퍼):
- `AdbTouchImplementation` (`METHOD_NAME=ADBTOUCH`): `base_touch`=ADB. `touch`/`swipe` 를 adb 명령으로.
- `MinitouchImplementation(AdbTouchImplementation)` (`MINITOUCH`, `METHOD_CLASS=Minitouch`): `ori_transformer`(=`Android._touch_point_by_orientation`)로 좌표 변환 후 `base_touch.touch/swipe/pinch/two_finger_swipe/swipe_along`. `fingers`=2 면 `two_finger_swipe`.
- `MaxtouchImplementation(MinitouchImplementation)` (`MAXTOUCH`, `METHOD_CLASS=Maxtouch`).

#### `BaseTouch` — `base_touch.py`
Minitouch/Maxtouch 공통 베이스.

```python
def __init__(self, adb, backend=False, size_info=None, input_event=None, ...):
    self.default_pressure = 50
```
- `install_and_setup` (`@ready_method`): `install` → `setup_server` → `setup_client[_backend]`.
- `safe_send` (`@retry_when_connection_error`): 끊기면 teardown+재설치.
- `perform(motion_events, interval=0.01)`: `MotionEvent` 시퀀스 실행 (`SleepEvent` 는 sleep).
- `touch(tuple_xy, duration=0.01)`: Down+Sleep+Up.
- `swipe`/`swipe_along`/`two_finger_swipe`/`pinch`: minitouch 프로토콜(`d/m/u/c`) 기반.
- `operate(args)`: `{"type":"down"/"move"/"up","x","y"}` 단일 명령.
- `transform_xy(x,y)`: 베이스는 항등 (서브클래스에서 override).

**MotionEvent 클래스 (프로토콜 명령 생성)**

| 클래스 | 생성자 | `getcmd` 출력 |
|--------|--------|---------------|
| `DownEvent` | `(coordinates, contact=0, pressure=50)` | `d {contact} {x} {y} {pressure}\nc\n` |
| `UpEvent` | `(contact=0)` | `u {contact}\nc\n` |
| `MoveEvent` | `(coordinates, contact=0, pressure=50)` | `m {contact} {x} {y} {pressure}\nc\n` |
| `SleepEvent` | `(seconds)` | None (sleep 처리) |

#### `Minitouch(BaseTouch)` — `minitouch.py`
- `default_pressure=50`, `path_in_android="/data/local/tmp/minitouch"`.
- `install`: abi별 STFLIB `minitouch`(sdk<16 `minitouch-nopie`) push, 크기 동일하면 skip.
- `setup_server`: `adb forward localabstract:minitouch_*` + `minitouch -n <port> [-d input_event]`. 로그에서 `(WxH with N contacts)` 정규식으로 `max_x/max_y` 추출 (기본 32768).
- `setup_client`: 소켓 연결 후 헤더(`v`/`^`/`$` 3줄) 수신.
- `transform_xy(x,y)`: `nx = x*max_x/width`, `ny = y*max_y/height` → 정수 문자열.

#### `Maxtouch(BaseTouch)` — `maxtouch.py`
Android10+ 지원. `MAXTOUCH_JAR=maxpresent.jar`.
- `default_pressure=0.5`, `path_in_android="/data/local/tmp/maxpresent.jar"`.
- `setup_server`: `adb forward localabstract:maxpresent_*` + `app_process -Djava.class.path=... com.netease.maxpresent.MaxPresent socket <port>`.
- `transform_xy(x,y)`: **정규화** `x/width, y/height` (0~1).

### 1.6 IME / Yosemite / Recorder / Rotation

#### `CustomIme` / `YosemiteIme` — `ime.py`
- `CustomIme(adb, apk_path, service_name)`: `start()`(현재 IME 백업 후 `ime enable/set`), `end()`(복구), `_get_ime_list()`.
- `YosemiteIme(CustomIme)`: `service_name=YOSEMITE_IME_SERVICE`. `text(value)`: Yosemite 버전>=430 이면 `tcp:8181` 포워드 후 `/ime_onStartInput` HTTP 폴링(최대 15회) → `am broadcast -a ADB_INPUT_TEXT --es msg <value>`. `code(code)`: `ADB_EDITOR_CODE --ei code` (예: 3=SEARCH).

#### `Yosemite` — `yosemite.py`
Yosemite.apk 래퍼 (javacap/recorder/ime/ext 가 상속). `install_or_upgrade`(apk 버전 비교 후 `pm_install -t`), `get_ready`(`@on_method_ready`), `uninstall`.

#### `YosemiteExt(Yosemite)` — `yosemite_ext.py`
`device_op(op_name, op_args="")`: `app_process ... com.netease.nie.yosemite.control.Control --DEVICE_OP ...`. `get_clipboard`/`set_clipboard`/`change_lang(lang)` (지원: zh/en/fr/de/it/ja/ko).

#### `Recorder(Yosemite)` — `recorder.py`
Yosemite 녹화. `start_recording(max_time=1800, bit_rate=None, bool_is_vertical="off")`: `app_process ... Recorder --start-record`, 출력 mp4 경로 파싱. `stop_recording(output="screen.mp4", is_interrupted=False)`: `--stop-record` 후 `adb pull`.

#### `RotationWatcher` / `XYTransformer` — `rotation.py`
- `RotationWatcher(adb, ori_method=ORI_METHOD.MINICAP)`: 회전 감시 데몬 스레드.
  - `ori_method==MINICAP`: rotationwatcher.jar push 후 `app_process ... com.example.rotationwatcher.Main` 의 stdout(예 `b"90\r\n"`)으로 갱신. 실패 시 `ADB` 로 폴백.
  - `ori_method==ADB`: `adb.getDisplayOrientation()` 폴링(3s).
  - `reg_callback(cb)`: 회전 변경 시 `cb(ori)` (ori=0~3). `Android._register_rotation_watcher` 가 `_current_orientation` 갱신 콜백 등록.
- `XYTransformer`: `up_2_ori(xy, wh, orientation)` / `ori_2_up(...)` — orientation(0~3)에 따른 좌표 회전.

### 1.7 Android constant 표 — `constant.py`

| 상수 | 값 |
|------|-----|
| `DEFAULT_ADB_SERVER` | `('127.0.0.1', 5037)` |
| `SDK_VERISON_ANDROID7` | 24 |
| `SDK_VERISON_ANDROID10` | 29 |
| `ROTATIONWATCHER_PACKAGE` | `jp.co.cyberagent.stf.rotationwatcher` |
| `YOSEMITE_PACKAGE` | `com.netease.nie.yosemite` |
| `YOSEMITE_IME_SERVICE` | `com.netease.nie.yosemite/.ime.ImeService` |
| `IP_PATTERN` | `(\d+\.){3}\d+` |
| `CAP_METHOD` | MINICAP / ADBCAP / JAVACAP |
| `TOUCH_METHOD` | MINITOUCH / MAXTOUCH / ADBTOUCH |
| `IME_METHOD` | ADBIME / YOSEMITEIME |
| `ORI_METHOD` | ADB(="ADBORI") / MINICAP(="MINICAPORI") |
| `DEFAULT_ADB_PATH` | Windows/Darwin/Linux/Linux-x86_64/Linux-armv7l 별 static adb 경로 |
| 정적 에셋 | `STFLIB`, `ROTATIONWATCHER_APK`, `YOSEMITE_APK`, `MAXTOUCH_JAR`(maxpresent.jar), `ROTATIONWATCHER_JAR`(rotationwatcher.jar) |

### 1.8 Android connect URI 파라미터

형식: `Android://<adbhost>:<adbport>/<serialno>?param=value`
- netloc(`host:port`) → `ADB(server_addr=...)`. 미지정이면 `127.0.0.1:5037`.

| 파라미터 | 매핑 | 기본값 | 예 |
|----------|------|--------|----|
| (path) | `serialno` | 로컬 첫 device | `Android:///SJE5T17B17` |
| `cap_method` | 스크린캡 | MINICAP | `?cap_method=javacap` |
| `touch_method` | 터치 | MINITOUCH(10+ MAXTOUCH) | `?touch_method=adb` |
| `ime_method` | 입력기 | YOSEMITEIME | `?ime_method=ADBIME` |
| `ori_method` | 회전감시 | MINICAP | — |
| `display_id` | 멀티 디스플레이 | None | — |
| `input_event` | minitouch 입력노드 | None | — |
| `name` | uuid 별칭 | serialno | `?name=serialnumber` |
| `adb_path` | adb 경로 | 자동 | — |

예: `Android://127.0.0.1:5037/10.254.60.1:5555` (원격 adb, network device).

---

## 2. iOS — `airtest/core/ios/`

WDA(WebDriverAgent) 기반. 사전 조건: 디바이스에 WDA 설치/실행 + 포트 포워딩(iproxy/tidevice/goios).

### 2.1 파일 역할 표

| 파일 | 핵심 | 역할 |
|------|------|------|
| `__init__.py` | — | `IOS`/`TIDevice`/`GOIOSHelper`/`ios_*` re-export. PY2 차단 |
| `ios.py` | `IOS(Device)` | 메인 Device. `wda.Client`/`wda.USBClient` 사용 |
| `constant.py` | `CAP_METHOD`/`TOUCH_METHOD`/`IME_METHOD`/`ROTATION_MODE`/`KEY_EVENTS` | 상수 |
| `rotation.py` | `RotationWatcher`, `XYTransformer` | 회전 감시 + 좌표 변환 |
| `mjpeg_cap.py` | `MJpegcap`, `SocketBuffer` | WDA mjpeg 스트림 캡처 |
| `instruct_cmd.py` | `InstructHelper` | 포트 포워딩 (iproxy/usbmux/python relay) |
| `relay.py` | `TCPRelay`, `ThreadedTCPServer`, `SocketRelay` | 순수 python usbmux 포트 릴레이 |
| `ios_utils.py` | `ios_*` 함수 | tidevice/goios 래핑 + 예외 변환 |
| `tidevice_helper.py` | `TIDevice` | tidevice 기반 디바이스 조작 (앱/파일/ps) |
| `goios_helper.py` | `GOIOSHelper` | go-ios 기반 (ios17+ tunnel, xctest) |
| `minicap.py` | `MinicapIOS` | (구) ios-minicap (현 미사용, 독립 스크립트) |
| `elements_type.py` | `ELEMENTS` | WDA UI element 타입 리스트 |

### 2.2 `IOS(Device)` — `ios.py`

클래스 전체가 `@add_decorator_to_methods(decorator_retry_session)` 로 래핑 — 모든 메서드가 session 실패 시 `_fetch_new_session()` 후 최대 3회 재시도.

```python
def __init__(self, addr=DEFAULT_ADDR, cap_method=CAP_METHOD.MJPEG,
             mjpeg_port=None, udid=None, name=None, serialno=None,
             wda_bundle_id=None):
```
`DEFAULT_ADDR = "http://localhost:8100/"`

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `addr` | str | `http://localhost:8100/` | WDA 주소. `http` 미시작 시 `http://` 접두 |
| `cap_method` | str | `MJPEG` | 캡처. MJPEG/WDACAP/MINICAP |
| `mjpeg_port` | int | None | mjpeg 서버 포트 |
| `udid`/`name`/`serialno` | str | None | 동일 의미 (udid) |
| `wda_bundle_id` | str | None | 실행할 WDA 번들 ID |

**연결 모드 분기** (`addr` netloc 으로 판정):
- netloc 이 `localhost`/`127.0.0.1` 이 아니고 `.` 포함 → **원격**: `self.is_local_device=False`, `wda.Client(addr)`.
- 그 외 → **로컬**: localhost 면 `_get_default_device()` 로 udid 자동, 아니면 netloc=udid. `ios_launch_wda(udid, wda_bundle_id)` 로 WDA 기동 후 `wda.USBClient(udid, port=8100, wda_bundle_id=...)`.

**핵심 컴포넌트**: `InstructHelper(device_info['uuid'])`, `MJpegcap`, `RotationWatcher`, `driver.alert.watch_and_click`.

**주요 프로퍼티/메서드**

| 멤버 | 시그니처 | 동작 |
|------|----------|------|
| `ip` (prop) | — | addr 에서 IP 정규식 추출 (없으면 localhost) |
| `uuid` (prop) | — | `_udid or addr` |
| `wda_bundle_id` (prop) | — | 로컬이면 `_get_default_wda_bundle_id()` |
| `using_ios_tagent` (prop) | — | `driver.status()` 에 `Version` 있으면 True (커스텀 ios-Tagent → 빠른 click/swipe, 세로 좌표) |
| `is_pad` (prop) | — | model=="iPad" 또는 해상도 ∈ `LANDSCAPE_PAD_RESOLUTION` |
| `device_info` (prop) | — | `driver.info` + (로컬) tidevice 정보 |
| `window_size`/`screen_size` | `()` | WDA `/wda/screen` 우선, fallback `window_size()` |
| `orientation` (prop/setter) | — | `/rotation` 의 z → `ROTATION_MODE` |
| `display_info` (prop) | — | width/height(픽셀)/window_*/orientation. `_display_info()` 가 snapshot 으로 픽셀 크기 산출 |
| `touch_factor` (prop/setter) | — | `window_height/height` = 실좌표/픽셀좌표 변환 계수 |
| `snapshot` | `(filename=None, quality=10, max_size=None)` | `_neo_wda_screenshot`(base64 jpg) → cv2 |
| `get_frame_from_stream` | `()` | MJPEG 면 `mjpegcap`, 실패 시 WDACAP 폴백 |
| `touch` | `(pos, duration=0.01)` | tagent 면 `_quick_click`(`/wda/deviceTap`), 아니면 `driver.click`. float<1=퍼센트 |
| `double_click` | `(pos)` | `driver.double_tap` |
| `swipe` | `(fpos, tpos, duration=0, delay=None)` | delay 면 `_quick_swipe`, 아니면 `driver.swipe` |
| `keyevent` | `(keyname)` | `KEY_EVENTS` → `press` (home/volumeUp/volumeDown) |
| `text` | `(text, enter=True)` | `driver.send_keys` (enter 시 `\n`) |
| `install_app`/`uninstall_app`/`list_app`/`start_app`/`stop_app` | — | 로컬=`ios_*`(tidevice/goios), 원격=`driver.app_*` |
| `app_state`/`app_current`/`home`/`press` | — | WDA 위임 |
| `get_clipboard`/`set_clipboard`/`paste` | `(wda_bundle_id=None)` | WDA 포그라운드 전환 필요. 원격은 `wda_bundle_id` 필수 |
| `get_ip_address` | `()` | `driver.status()['ios']` 의 wifiIP/ip |
| `is_locked`/`unlock`/`lock` | — | WDA |
| `setup_forward` | `(port)` | `instruct_helper.setup_proxy` |
| `ps` | `()` | 로컬 `ios_list_processes` |
| `alert_*` | — | `driver.alert.*` |
| `home_interface` | `()` | 현재 앱이 `com.apple.springboard` 인지 |
| `push`/`pull`/`ls`/`rm`/`mkdir`/`is_dir` | `(... bundle_id=None)` | 로컬 전용(`ios_*`), 원격은 `LocalDeviceError` |
| `start_recording`/`stop_recording` | `(max_time=1800, output=None, fps=10, ...)` | `ScreenRecorder` + `get_frame_from_stream` |
| `disconnect` | `()` | mjpeg/rotation teardown |
| `_touch_point_by_orientation`/`_transform_xy` | `(pos)` | tagent 시 `XYTransformer.up_2_ori` + touch_factor |

**좌표 처리 주의**: ios-Tagent(>=2022.03.30) 사용 시 모든 좌표가 세로(portrait) 기준 → airtest 가 `_touch_point_by_orientation` 으로 직접 회전 변환. appium WDA>=4.1.4 는 변환 불필요.

### 2.3 캡처 — `mjpeg_cap.py`

#### `MJpegcap`
```python
def __init__(self, instruct_helper=None, ip='localhost', port=None, ori_function=None):
    self.port = int(port or DEFAULT_MJPEG_PORT)  # 9100
```
- `port==9100` & ip 로컬 → `port_forwarding=True` (자동 9100 포워딩).
- `setup_stream_server` (`@ready_method`): 필요 시 `instruct_helper.setup_proxy(9100)`, 소켓 연결, `GET / HTTP/1.0` 핸드셰이크.
- `get_frame_from_stream` (`@on_method_ready`): `Content-Length` 파싱 후 jpeg 바이트. IOError 시 재연결용 블랙 스크린 반환.
- `snapshot(ensure_orientation=True)`: cv2 변환 + `ROTATION_MODE` 역매핑으로 회전 보정.
- `get_blank_screen()`: 연결 실패 시 검은 이미지.

#### `SocketBuffer(SafeSocket)`: `read_until(delimiter)` / `read_bytes(length)` / `write(data)`.

### 2.4 포트 포워딩 — `instruct_cmd.py` / `relay.py`

#### `InstructHelper`
```python
def __init__(self, uuid=None):  # uuid = wda.info['uuid']
```
- `usb_device` (prop): wda.usbmux 로 USB 디바이스 목록 조회, `info['uuid']` 일치하는 `wda.USBClient`/`Device` 반환. (Windows 는 iTunes 필요)
- `builtin_iproxy_path()`: `which("iproxy")` → `DEFAULT_IPROXY_PATH` (Windows/Darwin) 순.
- `setup_proxy(device_port)` (`@retries(3)`): 랜덤 로컬포트(11111~20000) → `do_proxy`. 반환 `(local_port, device_port)`.
- `do_proxy(port, device_port)`: iproxy/tidevice 있으면 `<proxy> -u <udid> port device_port` 서브프로세스, 없으면 `do_proxy_usbmux` (python relay).
- `do_proxy_usbmux(lport, rport)`: `ThreadedTCPServer` + `TCPRelay` 스레드.
- `remove_proxy`/`tear_down`: 포트별 kill 함수 호출.

#### `relay.py`
- `SocketRelay(a, b, maxbuf=65535)`: `select` 기반 양방향 소켓 릴레이.
- `TCPRelay(BaseRequestHandler)`: `device.create_inner_connection(rport)` ↔ 로컬 요청 릴레이.
- `ThreadedTCPServer(ThreadingMixIn, TCPServer)`: `daemon_threads=True`.

### 2.5 디바이스 조작 헬퍼

#### `TIDevice` — `tidevice_helper.py`
모든 메서드 `@add_decorator_to_methods(decorator_pairing_dialog)` (미페어링 시 trust 다이얼로그). tidevice `Usbmux`/`BaseDevice` 사용.

| 정적 메서드 | 반환/동작 |
|-------------|-----------|
| `devices()` | UDID 리스트 |
| `list_app(udid, app_type="user")` | `[(bundle_id, display_name, version), ...]` |
| `list_wda(udid)` | `.xctrunner`/`WebDriverAgentRunner-Runner` 번들 |
| `device_info(udid)` | productVersion/productType/.../marketName dict |
| `get_major_version(udid)` | iOS 메이저 버전 int |
| `install_app`/`uninstall_app`/`start_app`/`stop_app` | 앱 라이프사이클 |
| `ps(udid)` / `ps_wda(udid)` | 프로세스/실행중 WDA |
| `xctest(udid, wda_bundle_id)` | ios<17 전용 `tidevice xctest` |
| `push`/`pull`/`rm`/`ls`/`mkdir`/`is_dir` | 파일 조작 (bundle_id 로 앱 샌드박스) |

#### `GOIOSHelper` — `goios_helper.py`
go-ios 기반 (ios17+ tunnel 필요). `GOIOS_PATH=DEFAULT_GOIOS_PATH`. `decorator_checking_tunnel`: major_version>=17 면 `http://127.0.0.1:60105` tunnel 확인/기동.

| 정적 메서드 | 동작 |
|-------------|------|
| `devices()` | `<goios> list` |
| `device_info`/`get_major_version` | `<goios> info` |
| `start_app`/`stop_app` | `launch`/`kill` |
| `get_app_list`/`list_wda` | `apps` |
| `ps`/`ps_wda` | `ps` |
| `xctest(udid, wda_bundle_id)` | `runwda --bundleid ... --xctestconfig ...` (ios17+) |

#### `ios_utils.py`
얇은 래퍼: `ios_list_devices`/`ios_get_device_info`/`ios_list_app`/`ios_list_wda`/`ios_install_app`/`ios_uninstall_app`/`ios_start_app`/`ios_stop_app`/`ios_push`/`ios_pull`/`ios_rm`/`ios_ls`/`ios_mkdir`/`ios_is_dir` → `TIDevice`/`GOIOSHelper` 호출 + 예외를 `TIDeviceError`/`GOIOSError`/`WDAError` 로 변환.
- `ios_run_xctest(udid, wda_bundle_id)`: major<17 → tidevice, 그 외 → goios.
- `ios_launch_wda(udid, wda_bundle_id, force_start=False)`: WDA 준비 확인(`wda.BaseClient(http+usbmux://udid:8100).is_ready()`) → goios launch → 실패 시 xctest.

### 2.6 iOS constant 표 — `constant.py`

| 상수 | 값 |
|------|-----|
| `DEFAULT_ADDR`(ios.py) | `http://localhost:8100/` |
| `DEFAULT_MJPEG_PORT` | 9100 |
| `LANDSCAPE_PAD_RESOLUTION` | `[(1242, 2208)]` |
| `IP_PATTERN` | `(\d+\.){3}\d+` |
| `CAP_METHOD` | MINICAP / WDACAP / MJPEG |
| `TOUCH_METHOD` | WDATOUCH |
| `IME_METHOD` | WDAIME |
| `ROTATION_MODE` | `{0:PORTRAIT, 270:LANDSCAPE, 90:LANDSCAPE_RIGHT, 180:PORTRAIT_UPSIDEDOWN}` (wda 상수) |
| `KEY_EVENTS` | `{home, volumeup→volumeUp, volumedown→volumeDown}` |
| `DEFAULT_IPROXY_PATH` | Windows/Darwin iproxy 경로 |
| `DEFAULT_GOIOS_PATH` | Windows(ios-win.exe)/Darwin(ios-darwin) |

### 2.7 iOS connect URI 파라미터

형식: `iOS:///<wda_addr>?param=value` (scheme 뒤 `///` 후 전체를 addr 로 사용)

| URI 예 | 의미 |
|--------|------|
| `iOS:///` | 로컬 기본 (`http://localhost:8100/`) |
| `iOS:///127.0.0.1:8100` | 로컬 WDA |
| `iOS:///http://localhost:8100/?mjpeg_port=9100` | mjpeg 포트 지정 |
| `iOS:///...&&udid=00008020-...` (또는 `uuid`/`serialno`) | udid 지정 |
| `iOS:///http+usbmux://udid` | usbmux 직접 연결 |
| `iOS://10.227.70.247:20042` | 원격 디바이스(`.` 포함 netloc → wda.Client) |

| 파라미터 | 매핑 | 기본값 |
|----------|------|--------|
| (addr) | `addr` | `http://localhost:8100/` |
| `mjpeg_port` | mjpeg 포트 | None |
| `udid`/`uuid`/`serialno` | `udid` | 로컬 첫 device |
| `cap_method` | 캡처 | MJPEG |
| `wda_bundle_id` | WDA 번들 | 자동 |

**원격/로컬 제약**: `push/pull/ls/rm/mkdir/is_dir/install_app/list_app/ps` 등은 로컬 전용 → 원격 호출 시 `LocalDeviceError`.

---

## 3. Windows — `airtest/core/win/`

pywinauto + mss + win32 API 기반 데스크톱 자동화.

### 3.1 파일 역할 표

| 파일 | 핵심 | 역할 |
|------|------|------|
| `__init__.py` | — | `Windows` re-export |
| `win.py` | `Windows(Device)` | 메인 Device. pywinauto Application |
| `screen.py` | `screenshot(filename, hwnd=None)` | win32 GDI BitBlt 스크린샷 (fallback) |
| `ctypesinput.py` | `key_press`/`key_release`, scancode 테이블 | DirectInput용 스캔코드 키 입력 |

### 3.2 `Windows(Device)` — `win.py`

```python
def __init__(self, handle=None, dpifactor=1, **kwargs):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `handle` | int | None | 윈도우 핸들. None 이면 전체 데스크톱 |
| `dpifactor` | float | 1 | 고DPI 좌표 보정 계수 |
| `**kwargs` | — | — | pywinauto `connect` 인자 (process/timeout/title_re/foreground 등) |

- `_app = Application()`, `mss.mss()` 로 스크린샷, `monitor`(전체)/`main_monitor`(주 모니터).
- `_init_connect`: handle 또는 kwargs 있으면 `connect`.

**주요 메서드**

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `uuid` (prop) | — | `self.handle` |
| `connect` | `(handle=None, **kwargs)` | handle/process/title_re 로 `_app.connect`, `_top_window` 설정. `foreground` 기본 True |
| `shell` | `(cmd)` | `subprocess.check_output(cmd, shell=True)` |
| `snapshot` | `(filename=None, quality=10, max_size=None)` | mss 로 윈도우/전체 grab. 실패 시 `snapshot_old`(win32 GDI) |
| `snapshot_old` | `(...)` | `screen.screenshot()` + crop |
| `keyevent` | `(keyname, **kwargs)` | `keyboard.SendKeys(keyname)` |
| `text` | `(text, **kwargs)` | `keyevent(text)` |
| `key_press`/`key_release` | `(key)` | `ctypesinput` 스캔코드 (게임용 DirectInput) |
| `touch` | `(pos, duration=0.01, right_click=False, steps=1, offset=0)` | `mouse.move`+`press`/`release`. 절대좌표→`_fix_op_pos` 듀얼모니터 보정 |
| `double_click` | `(pos)` | `mouse.double_click` |
| `swipe` | `(p1, p2, duration=0.8, steps=5, button="left")` | press→move(steps)→release |
| `mouse_move`/`mouse_down`/`mouse_up` | `(pos)` / `(button='left')` | 저수준 마우스 |
| `start_app` | `(path, *args, **kwargs)` | `_app.start(path)` |
| `stop_app` | `(pid)` | `_app.connect(process=pid).kill()` |
| `set_foreground`/`set_focus` (`@require_app`) | `()` | `_top_window.set_focus()` |
| `get_rect` | `()` | `RECT`. app 없으면 전체 화면 메트릭 |
| `get_title`/`get_pos`/`move`/`kill` (`@require_app`) | — | 윈도우 조작 |
| `set_clipboard`/`get_clipboard`/`paste` | — | win32clipboard (`CF_UNICODETEXT`), paste=`^v` |
| `focus_rect` (prop/setter) | `[left,top,right,bottom]` | 윈도우 보더 제거 보정 |
| `get_current_resolution` | `()` | rect + focus_rect 크기 |
| `_windowpos_to_screenpos` | `(pos)` | 윈도우 상대→스크린 절대 (dpifactor 적용) |
| `get_ip_address` | `()` | psutil net_if_addrs, 가상 NIC/APIPA 제외 |
| `start_recording`/`stop_recording` | `(max_time=1800, output=None, fps=10, ...)` | `ScreenRecorder` + `snapshot` |

`require_app` 데코레이터: `self.app` 없으면 `RuntimeError`.

### 3.3 `screen.py`
win32 GDI 캡처 (`win32gui`/`win32ui`). `hwnd=None` 이면 가상 데스크톱 전체(`SM_*VIRTUALSCREEN`=76~79). `BitBlt`+`Image.frombuffer('BGRX')` → `pil_2_cv2`.

### 3.4 `ctypesinput.py`
`SendInput` 으로 스캔코드 키 입력 (가상키 대신 → DirectInput 게임 대응).
- `KEYS` (45+ 키, 예 `ESCAPE=0x01`, `A=0x1E`, `SPACE=0x39`, `F1=0x3B`), `EXTENDED_KEYS` (HOME/UP/LEFT 등 0xC7~).
- `KEYEVENTF_SCANCODE=0x0008`.
- 구조체: `KeyBdInput`/`MouseInput`/`HardwareInput`/`Input` (ctypes).
- `key_press(key)`/`key_release(key)`: `key.upper()` → KEYS/EXTENDED_KEYS 조회 → `send_keyboard_input(hex_code, flags)`. 미존재 키는 `ValueError`.

### 3.5 Windows connect URI 파라미터

형식: `Windows:///<handle>?param=value`

| URI 예 | 의미 |
|--------|------|
| `Windows:///` | 전체 데스크톱 |
| `Windows:///123456` | handle=123456 윈도우 |
| `Windows:///123456?foreground=False` | 포그라운드 안 함 |
| `windows:///?title_re='.*explorer.*'` | 제목 정규식으로 연결 |

| 파라미터 | 매핑 | 기본값 |
|----------|------|--------|
| (path) | `handle` | None(데스크톱) |
| `dpifactor` | DPI 보정 | 1 |
| `foreground` | 포그라운드 | True |
| `title_re`/`process`/`timeout`/... | pywinauto connect 인자 | — |

---

## 4. Linux — `airtest/core/linux/`

X11(Xlib) + pywinauto.mouse/keyboard 기반 데스크톱. 가장 단순한 구현.

### 4.1 파일 역할 표

| 파일 | 핵심 | 역할 |
|------|------|------|
| `__init__.py` | — | `Linux` re-export |
| `linux.py` | `Linux(Device)` | X11 스크린샷 + pywinauto 입력 |

### 4.2 `Linux(Device)` — `linux.py`

```python
def __init__(self, pid=None, **kwargs):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `pid` | int | None | (현재 미사용, `self.pid=None` 으로 고정) |

- `mouse`/`keyboard` = pywinauto, `_focus_rect=(0,0,0,0)`.
- `super().__init__()` 를 호출하지 않음 (주의: Device 베이스 init 미호출).

**주요 메서드**

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `shell` | `(cmd)` | `subprocess.check_output(cmd, shell=True)` |
| `snapshot` | `(filename="tmp.png", quality=None)` | Xlib `root.get_image` → `Image.frombytes('BGRX')` → cv2. quality 무시 |
| `keyevent` | `(keyname, **kwargs)` | `keyboard.SendKeys` |
| `text` | `(text, **kwargs)` | `keyevent(text)` |
| `touch` | `(pos, duration=0.01, right_click=False)` | `mouse.press`/`release` |
| `double_click` | `(pos)` | `mouse.double_click` |
| `swipe` | `(p1, p2, duration=0.8, steps=5)` | press→move(steps)→release |
| `start_app`/`stop_app` | `(path)` / `(pid)` | `super().start_app/stop_app` 호출 → **NotImplementedError** (Device 베이스 미구현) |
| `get_current_resolution` | `()` | Xlib screen width/height |
| `get_ip_address` | `()` | `socket.gethostbyname(socket.gethostname())` |

**주의**: `start_app`/`stop_app` 는 베이스로 위임만 하므로 실제 미구현. snapshot/touch/swipe 좌표는 절대 스크린 좌표 (윈도우 상대 보정 없음).

### 4.3 Linux connect URI

형식: `Linux:///` — 파라미터 거의 없음. `pid` 가 받아지나 사용되지 않는다.

---

## 5. 플랫폼 간 교차 참조 요약

| 관심사 | Android | iOS | Windows | Linux |
|--------|---------|-----|---------|-------|
| 백엔드 라이브러리 | adb + stf(minicap/minitouch) | wda(WebDriverAgent) | pywinauto + mss + win32 | Xlib + pywinauto |
| 스크린캡 | Minicap/Javacap/AdbCap (`ScreenProxy`) | MJpegcap/WDA screenshot | mss/win32 GDI | Xlib get_image |
| 입력 | Minitouch/Maxtouch/AdbTouch (`TouchProxy`) | WDA tap/swipe | pywinauto mouse + scancode | pywinauto mouse |
| 회전 감시 | `rotation.RotationWatcher` | `rotation.RotationWatcher` (WDA `/rotation`) | 없음 | 없음 |
| 좌표 변환 | `XYTransformer.up_2_ori` | `XYTransformer.up_2_ori` (tagent 시) | dpifactor + 모니터 보정 | 없음 |
| 포트 포워딩 | `adb forward` | `InstructHelper`(iproxy/usbmux) | 없음 | 없음 |
| 클립보드 | YosemiteExt | WDA pasteboard | win32clipboard | 없음 |
| 녹화 | Yosemite/ffmpeg | ScreenRecorder | ScreenRecorder | 없음 |
| 원격 지원 | adb network device | wda.Client(원격) | 없음 | 없음 |

호출 관계:
- `api.connect_device(uri)` → `parse_device_uri` → `init_device(platform, uuid, **params)` → `MetaDevice.REGISTRY[platform]` 클래스 인스턴스화.
- `Android` → `ADB` + `ScreenProxy`(→`Minicap`/`Javacap`/`AdbCap`) + `TouchProxy`(→`Minitouch`/`Maxtouch`) + `RotationWatcher` + `YosemiteIme`/`Recorder`/`YosemiteExt`(모두 `Yosemite` 상속).
- `IOS` → `wda.Client/USBClient` + `MJpegcap` + `InstructHelper`(→`relay.TCPRelay`) + `RotationWatcher` + `ios_utils`(→`TIDevice`/`GOIOSHelper`).
- `Windows` → pywinauto `Application` + `screen.screenshot` + `ctypesinput`.
- `Linux` → Xlib + pywinauto.

---

## 6. 참고: 미사용/독립 파일

- `airtest/core/ios/minicap.py` (`MinicapIOS`): 구 ios-minicap 스크립트. `IOS` 가 import 하지 않음 (`__main__` 데모만 존재). macOS `system_profiler` 로 디바이스 검색.
