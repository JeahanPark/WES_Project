---
type: reference
source: Airtest (airtest.core)
generated: subagent
---

# Airtest Core 아키텍처 & API 레퍼런스

대상 모듈: `airtest/__init__.py`, `airtest/__main__.py`, `airtest/core/__init__.py`, `airtest/core/api.py`, `airtest/core/assertions.py`, `airtest/core/device.py`, `airtest/core/cv.py`, `airtest/core/error.py`, `airtest/core/settings.py`, `airtest/core/helper.py`

---

## 1. 전체 아키텍처 개요

Airtest는 **이미지 인식 기반 자동화 프레임워크**다. 핵심은 세 축으로 나뉜다.

| 축 | 핵심 객체 | 위치 | 역할 |
|----|-----------|------|------|
| Device 추상화 | `Device` 기반 클래스 + `G.DEVICE` / `G.DEVICE_LIST` | `device.py`, `helper.py` | 플랫폼(Android/iOS/Windows/Linux) 별 조작을 추상화. API는 `G.DEVICE`에 위임 |
| 전역 상태 | `G` (globals 컨테이너) | `helper.py` | 현재 디바이스·디바이스 목록·로거·BASEDIR 등을 보관 |
| 설정 | `ST` (`Settings`) | `settings.py` | THRESHOLD, FIND_TIMEOUT, CVSTRATEGY 등 모든 동작 파라미터 |
| CV 매칭 | `Template` / `loop_find` / `Predictor` | `cv.py` | 템플릿 이미지를 화면에서 찾는 파이프라인 |

### 1.1 데이터 흐름 (touch 예시)

```
api.touch(Template(...))
  └─ loop_find(template, timeout=ST.FIND_TIMEOUT)        # cv.py:40
       └─ G.DEVICE.snapshot()                            # 화면 캡처 (device.py 추상메서드)
       └─ template.match_in(screen)                      # cv.py:153
            └─ Template._cv_match(screen)                # cv.py:167
                 └─ ST.CVSTRATEGY 순회 → MATCHING_METHODS[method]
                 └─ TargetPos().getXY(result, target_pos)
  └─ G.DEVICE.touch(pos)                                 # 실제 디바이스 클릭
  └─ delay_after_operation()                             # time.sleep(ST.OPDELAY)
```

핵심 위임 패턴: `api.py`의 모든 디바이스 조작 함수는 `G.DEVICE.<method>()`로 위임한다(`api.py:173` 등). 따라서 `api.py`는 플랫폼 독립적이며, 실제 구현은 `Device` 서브클래스가 담당한다.

### 1.2 모듈 의존 관계

```
__main__.py ──→ airtest.cli.__main__.main   (SCOPE 외부)
__init__.py ──→ airtest.utils.version.__version__

core/api.py
  ├─ core/cv.py        (Template, loop_find, try_log_screen)
  ├─ core/error.py     (TargetNotFoundError)
  ├─ core/settings.py  (Settings as ST)
  ├─ core/helper.py    (G, delay_after_operation, import_device_cls, logwrap, set_logdir, using, log)
  └─ core/assertions.py (assert_* 재수출)

core/assertions.py ──→ helper.logwrap, cv.loop_find, error.TargetNotFoundError, settings.ST
core/cv.py         ──→ helper.G/logwrap, settings.ST, error.*, aircv.*, utils.transform.TargetPos, aircv 매칭 클래스들
core/helper.py     ──→ settings.ST, utils.logger/logwraper, error.NoDeviceError, (지연 import) core.android/win/ios/linux
core/device.py     ──→ six (메타클래스), 상위 의존 없음 (기반 클래스)
```

`core/__init__.py`는 **빈 파일**(내용 없음).

---

## 2. `airtest/__init__.py`

```python
from airtest.utils.version import __version__
```

패키지 임포트 시 버전 문자열만 노출한다. 그 외 로직 없음.

## 3. `airtest/__main__.py`

```python
# -*- coding: utf-8 -*-
from airtest.cli.__main__ import main

if __name__ == '__main__':
    main()
```

`python -m airtest` 실행 시 진입점. 실제 CLI 로직은 `airtest.cli.__main__.main`(SCOPE 외부).

## 4. `airtest/core/__init__.py`

빈 파일. 패키지 마커 역할만 한다.

---

## 5. `core/settings.py` — `Settings` (`ST`)

전 시스템 동작을 좌우하는 전역 설정 클래스. `from airtest.core.settings import Settings as ST`로 임포트하여 `ST.XXX`로 접근/수정한다.

### 5.1 설정 상수 표

| 상수 | 기본값 | 타입 | 설명 |
|------|--------|------|------|
| `DEBUG` | `False` | bool | 디버그 모드 플래그 |
| `LOG_DIR` | `None` | str/None | 로그·스크린샷 저장 디렉터리. `None`이면 로그 저장 안 함 |
| `LOG_FILE` | `"log.txt"` | str | 로그 파일명 (`LOG_DIR` 하위) |
| `RESIZE_METHOD` | `staticmethod(cocos_min_strategy)` | callable | 녹화 해상도≠현재 해상도일 때 템플릿 리사이즈 전략 |
| `CVSTRATEGY` | `["mstpl", "tpl", "sift", "brisk"]` | list[str] | 매칭 방법 시도 순서. 위에서부터 시도, 성공 시 중단 |
| `CVSTRATEGY` (cv2 조건부) | `["mstpl", "tpl", "brisk"]` | list[str] | `3.4.2 < cv2.__version__ < 4.4.0`이면 sift 제외 버전으로 덮어씀 (`settings.py:16-17`) |
| `KEYPOINT_MATCHING_PREDICTION` | `True` | bool | 키포인트 매칭 시 예측 영역 사용 여부 |
| `THRESHOLD` | `0.7` | float [0,1] | 일반 매칭 신뢰도 임계치. `Template`의 기본 threshold |
| `THRESHOLD_STRICT` | `None` | float/None | `assert_exists` 전용 엄격 임계치. `None`이면 Template threshold 사용 |
| `OPDELAY` | `0.1` | float(초) | 각 조작 후 지연 (`delay_after_operation`) |
| `FIND_TIMEOUT` | `20` | int(초) | 일반 탐색 타임아웃 (touch/wait/assert_exists 등) |
| `FIND_TIMEOUT_TMP` | `3` | int(초) | 짧은 탐색 타임아웃 (exists/assert_not_exists/swipe 2번째 타깃) |
| `PROJECT_ROOT` | `os.environ["PROJECT_ROOT"]` 또는 `""` | str | `using` API의 스크립트 루트 |
| `SNAPSHOT_QUALITY` | `10` | int 1-100 | 스크린샷 JPEG 품질 |
| `IMAGE_MAXSIZE` | `os.environ["IMAGE_MAXSIZE"]` 또는 `None` | int/None | 스크린샷 최대 변 길이(px). 예: 1200 → 1200x1200 이내 |
| `SAVE_IMAGE` | `True` | bool | 스텝별 스크린샷 저장 여부 |

### 5.2 주의점
- `CVSTRATEGY`의 매칭 키는 `cv.MATCHING_METHODS` 딕셔너리에 정의된 키만 유효. 정의 외 키 사용 시 `InvalidMatchingMethodError` 발생(`cv.py:176`).
- `IMAGE_MAXSIZE`/`PROJECT_ROOT`는 환경변수에서 초기화되므로 import 시점 환경에 의존.
- `cv2` 버전 분기는 import 시 1회 평가된다.

---

## 6. `core/helper.py` — 전역 상태 `G` 와 헬퍼

### 6.1 `DeviceMetaProperty` (메타클래스)

`G` 클래스의 메타클래스. `G.DEVICE`를 프로퍼티로 만들어 접근 제어한다.

- getter: `G._DEVICE`가 `None`이면 `NoDeviceError("No devices added.")` raise.
- setter: `G._DEVICE = dev` 할당.

즉 `G.DEVICE`는 단순 속성이 아니라 **디바이스 미등록 시 예외를 던지는 가드**다.

### 6.2 `G` (globals 컨테이너)

```python
class G(object, metaclass=DeviceMetaProperty):
    BASEDIR = []
    LOGGER = AirtestLogger(None)
    LOGGING = get_logger("airtest.core.api")
    SCREEN = None
    _DEVICE = None
    DEVICE_LIST = []
    RECENT_CAPTURE = None
    RECENT_CAPTURE_PATH = None
    CUSTOM_DEVICES = {}
```

| 멤버 | 타입 | 설명 |
|------|------|------|
| `BASEDIR` | list | 템플릿 이미지 파일 탐색 기준 디렉터리들. `Template.filepath`가 순회 (`cv.py:142`) |
| `LOGGER` | `AirtestLogger` | 리포트용 구조화 로거 (`logwrap`이 사용) |
| `LOGGING` | logging.Logger | `"airtest.core.api"` 채널 일반 로거 |
| `SCREEN` | - | 최근 화면(미사용에 가까움) |
| `_DEVICE` | Device/None | 현재 활성 디바이스 백킹 필드 |
| `DEVICE_LIST` | list[Device] | 등록된 모든 디바이스 |
| `RECENT_CAPTURE` / `RECENT_CAPTURE_PATH` | - | 최근 캡처 캐시 |
| `CUSTOM_DEVICES` | dict | 사용자 등록 커스텀 디바이스 클래스 (`platform.lower()` → cls) |

**클래스메서드**

```python
@classmethod
def add_device(cls, dev)
```
- `DEVICE_LIST`를 순회하여 동일 `uuid`가 있으면 그 자리를 `dev`로 교체하고 현재 디바이스로 설정(경고 로그).
- 없으면 `dev`를 현재 디바이스로 설정하고 리스트에 append.
- 반환: `None`.

```python
@classmethod
def register_custom_device(cls, device_cls)
```
- `CUSTOM_DEVICES[device_cls.__name__.lower()] = device_cls`. 커스텀 플랫폼 등록.

### 6.3 헬퍼 함수

#### `set_logdir(dirpath)`
```python
def set_logdir(dirpath)
```
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `dirpath` | str | - | 로그·스크린샷 저장 디렉터리 |

동작: 디렉터리 없으면 생성, `ST.LOG_DIR` 설정, `G.LOGGER.set_logfile(LOG_DIR/LOG_FILE)`. 반환 없음.

#### `log(arg, timestamp=None, desc="", snapshot=False)`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `arg` | str / Exception / any | - | 로그 메시지. Exception이면 traceback을 리포트에 기록(실패로 표시됨) |
| `timestamp` | float | `None` | 로그 시각, 기본 `time.time()` |
| `desc` | str | `""` | 설명. 기본은 `arg.__class__.__name__` 또는 arg 자체 |
| `snapshot` | bool | `False` | True면 강제로 스크린샷 1장 저장 (`ST.SAVE_IMAGE` 임시 활성화) |

반환: `None`. Exception/문자열/기타에 따라 리포트 구조(`name`/`traceback`/`log`)를 다르게 구성. Exception은 `"traceback"`에 들어가 리포트에서 **불통과 스텝**으로 판정됨.

#### `logwrap(f)`
```python
def logwrap(f):
    return Logwrap(f, G.LOGGER)
```
데코레이터. 함수 호출을 Airtest 리포트(HTML)에 스텝으로 기록한다. `api.py`/`assertions.py`/`cv.py`의 대부분 public 함수에 적용.

#### `device_platform(device=None)`
디바이스(미지정 시 `G.DEVICE`)의 클래스명(`__class__.__name__`) 반환. 예: `"Android"`.

#### `using(path)`
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `path` | str | 다른 `.air` 스크립트의 상대/절대 경로 |

동작: 절대경로화 우선순위 = ① `ST.PROJECT_ROOT/path` → ② `CWD/path` → ③ 호출 스크립트 디렉터리 기준. 찾은 경로를 `sys.path`와 `G.BASEDIR`에 추가하여 해당 스크립트의 이미지·함수를 참조 가능하게 함. 반환 없음.

#### `import_device_cls(platform)`
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `platform` | str | "android"/"windows"/"ios"/"linux" 또는 커스텀 등록명 (대소문자 무시) |

동작: 지연 import로 플랫폼별 Device 클래스를 반환. 매핑:

| platform(lower) | 반환 클래스 | 모듈 |
|-----------------|-------------|------|
| 커스텀(`G.CUSTOM_DEVICES`에 있음) | 등록된 cls | - |
| `"android"` | `Android` | `airtest.core.android.android` |
| `"windows"` | `Windows` | `airtest.core.win.win` |
| `"ios"` | `IOS` | `airtest.core.ios` |
| `"linux"` | `Linux` | `airtest.core.linux.linux` |
| 그 외 | — | `RuntimeError("Unknown platform: ...")` |

#### `delay_after_operation()`
```python
def delay_after_operation():
    time.sleep(ST.OPDELAY)
```
조작 직후 `ST.OPDELAY`(기본 0.1초) 만큼 대기. `api.py`의 touch/swipe/text 등에서 호출.

---

## 7. `core/device.py` — `Device` 추상 기반 클래스

### 7.1 `MetaDevice` (메타클래스)
```python
class MetaDevice(type):
    REGISTRY = {}
    def __new__(meta, name, bases, class_dict): ...
```
모든 `Device` 서브클래스를 정의 시점에 `MetaDevice.REGISTRY[name] = cls`로 자동 등록한다(클래스명 기준).

### 7.2 `Device`
```python
class Device(with_metaclass(MetaDevice, object)):
    """base class for test device"""
```
`six.with_metaclass`로 `MetaDevice`를 메타클래스로 사용. 모든 조작 메서드는 기본적으로 `_raise_not_implemented_error()`를 호출하는 **추상 인터페이스**이며, 플랫폼 서브클래스가 오버라이드한다.

| 멤버 | 시그니처 | 기본 동작 |
|------|----------|-----------|
| `platform` (property) | `platform` | `self.__class__.__name__.lower()` 반환 |
| `to_json()` | `to_json()` | `"<ClassName repr(uuid)>"` 문자열. uuid 접근 실패 시 uuid=None |
| `uuid` (property) | `uuid` | NotImplemented |
| `shell(*args, **kwargs)` | | NotImplemented |
| `snapshot(*args, **kwargs)` | | NotImplemented (화면 캡처) |
| `touch(target, **kwargs)` | | NotImplemented |
| `double_click(target)` | | `raise NotImplementedError` (직접) |
| `swipe(t1, t2, **kwargs)` | | NotImplemented |
| `keyevent(key, **kwargs)` | | NotImplemented |
| `text(text, enter=True)` | | NotImplemented |
| `start_app(package, **kwargs)` | | NotImplemented |
| `stop_app(package)` | | NotImplemented |
| `clear_app(package)` | | NotImplemented |
| `list_app(**kwargs)` | | NotImplemented |
| `install_app(uri, **kwargs)` | | NotImplemented |
| `uninstall_app(package)` | | NotImplemented |
| `get_current_resolution()` | | NotImplemented |
| `get_render_resolution()` | | NotImplemented |
| `get_ip_address()` | | NotImplemented |
| `set_clipboard(text)` | | NotImplemented |
| `get_clipboard()` | | NotImplemented |
| `paste()` | `paste()` | **구현됨**: `self.text(self.get_clipboard())` |
| `disconnect()` | `disconnect()` | **구현됨**: `pass` (no-op) |
| `_raise_not_implemented_error()` | | `NotImplementedError("Method not implemented on <Platform>")` |

주의점:
- `double_click`만 `raise NotImplementedError`를 직접 쓰고, 나머지는 `_raise_not_implemented_error()`(메시지에 플랫폼명 포함)를 사용한다.
- `paste`는 기본 구현이 `get_clipboard`+`text` 조합이므로 두 메서드가 구현되면 자동 동작.

---

## 8. `core/cv.py` — 이미지 매칭 파이프라인

### 8.1 `MATCHING_METHODS` (매칭 전략 레지스트리)

`ST.CVSTRATEGY`의 문자열 키 → 매칭 클래스 매핑(`cv.py:25-36`).

| 키 | 클래스 | 분류 |
|----|--------|------|
| `"tpl"` | `TemplateMatching` | 단일 스케일 템플릿 매칭 |
| `"mstpl"` | `MultiScaleTemplateMatchingPre` | 다중 스케일 템플릿(예측 영역 사용) |
| `"gmstpl"` | `MultiScaleTemplateMatching` | 다중 스케일 템플릿(전역) |
| `"kaze"` | `KAZEMatching` | 키포인트 |
| `"brisk"` | `BRISKMatching` | 키포인트 |
| `"akaze"` | `AKAZEMatching` | 키포인트 |
| `"orb"` | `ORBMatching` | 키포인트 |
| `"sift"` | `SIFTMatching` | 키포인트 (opencv-contrib) |
| `"surf"` | `SURFMatching` | 키포인트 (opencv-contrib) |
| `"brief"` | `BRIEFMatching` | 키포인트 (opencv-contrib) |

### 8.2 `loop_find(query, timeout=ST.FIND_TIMEOUT, threshold=None, interval=0.5, intervalfunc=None)`

이미지 템플릿을 타임아웃까지 반복 탐색하는 핵심 루프.

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `query` | `Template` | - | 찾을 템플릿 |
| `timeout` | float(초) | `ST.FIND_TIMEOUT`(20) | 최대 탐색 시간 |
| `threshold` | float/None | `None` | 지정 시 `query.threshold`를 덮어씀 |
| `interval` | float(초) | `0.5` | 실패 후 재시도 간격 |
| `intervalfunc` | callable/None | `None` | 매 실패 시 호출되는 콜백 |

반환: 매칭 좌표(`(x, y)`). 동작 흐름:
1. `G.DEVICE.snapshot(filename=None, quality=ST.SNAPSHOT_QUALITY)`로 화면 캡처.
2. `screen is None`이면 "may be locked" 경고 후 다음 루프.
3. `threshold` 지정 시 `query.threshold = threshold`.
4. `query.match_in(screen)` → 매칭되면 `try_log_screen(screen)` 후 좌표 반환.
5. `intervalfunc` 호출.
6. 경과 > timeout이면 `try_log_screen` 후 `TargetNotFoundError('Picture %s not found in screen' % query)` raise. 아니면 `interval` 대기 후 반복.

주의점: 화면 잠금 등으로 `snapshot`이 None을 줄 수 있으며, 그래도 타임아웃까지 재시도한다.

### 8.3 `try_log_screen(screen=None, quality=None, max_size=None)`

스크린샷을 파일로 저장하고 리포트 메타 반환.

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `screen` | image/None | `None` | 저장할 화면. None이면 새로 캡처 |
| `quality` | int/None | `None`→`ST.SNAPSHOT_QUALITY` | JPEG 품질 |
| `max_size` | int/None | `None`→`ST.IMAGE_MAXSIZE` | 최대 변 길이 |

반환: `{"screen": filename, "resolution": aircv.get_resolution(screen)}` 또는 `None`.

주의점: `ST.LOG_DIR`가 없거나 `ST.SAVE_IMAGE`가 False면 즉시 `return`(아무것도 안 함). 파일명은 `"%(time)d.jpg" % {'time': time.time()*1000}` (밀리초 타임스탬프).

### 8.4 `Template` (매칭 대상 객체)

```python
class Template(object):
    def __init__(self, filename, threshold=None, target_pos=TargetPos.MID,
                 record_pos=None, resolution=(), rgb=False,
                 scale_max=800, scale_step=0.005)
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `filename` | str | - | 템플릿 이미지 파일명/경로 |
| `threshold` | float/None | `None`→`ST.THRESHOLD`(0.7) | 매칭 신뢰도 임계치 |
| `target_pos` | int | `TargetPos.MID`(5) | 매칭 영역 내 반환 좌표 위치(1~9, 5=중앙) |
| `record_pos` | tuple/None | `None` | 녹화 시 화면상 상대 위치(예측 영역 계산용) |
| `resolution` | tuple | `()` | 녹화 시 화면 해상도 |
| `rgb` | bool | `False` | RGB 3채널 검증 여부 |
| `scale_max` | int | `800` | 다중 스케일 매칭 최대 범위 |
| `scale_step` | float | `0.005` | 다중 스케일 매칭 탐색 스텝 |

#### 속성/메서드

```python
@property
def filepath(self)
```
캐시(`self._filepath`)가 있으면 반환. 없으면 `G.BASEDIR`의 각 디렉터리에서 `filename`을 찾아 첫 실존 경로 반환·캐싱. 못 찾으면 `filename` 그대로 반환.

```python
def __repr__(self)  # "Template(<filepath>)"
```

```python
def match_in(self, screen)
```
- `_cv_match(screen)` 실행 → 결과 없으면 `None`.
- 있으면 `TargetPos().getXY(match_result, self.target_pos)`로 클릭 좌표 산출 후 반환.

```python
def match_all_in(self, screen)
```
- 이미지 읽고 `_resize_image` 후 `_find_all_template`로 모든 매칭 결과 리스트 반환. `api.find_all`이 사용.

```python
@logwrap
def _cv_match(self, screen)
```
매칭 핵심 루프. `ST.CVSTRATEGY`의 각 method를 순회:
- `MATCHING_METHODS.get(method)`가 없으면 `InvalidMatchingMethodError` raise.
- method가 `"mstpl"`/`"gmstpl"`이면 `ori_image`와 `record_pos`/`resolution`/`scale_max`/`scale_step`까지 넘겨 `_try_match`.
- 그 외는 리사이즈된 `image`로 `_try_match`.
- 첫 성공(`ret` truthy) 시 `break`. 결과 반환.

```python
@staticmethod
def _try_match(func, *args, **kwargs)
```
- `func(*args, **kwargs).find_best_result()` 실행.
- `aircv.NoModuleError`(surf/sift/brief 미설치) → 경고 후 `None`.
- `aircv.BaseError` → debug 로그 후 `None`.
- 정상 시 결과 반환.

`_imread()` → `aircv.imread(self.filepath)`.
`_find_all_template(image, screen)` → `TemplateMatching(...).find_all_results()`.

```python
def _find_keypoint_result_in_predict_area(self, func, image, screen)
```
`record_pos`가 있으면 `Predictor.get_predict_area`로 화면 예측 영역을 잘라 그 안에서만 키포인트 매칭. 결과 좌표/rectangle을 영역 오프셋(xmin/ymin)만큼 보정하여 반환. `record_pos` 없으면 `None`.

```python
def _resize_image(self, image, screen, resize_method)
```
녹화 해상도(`self.resolution`)와 현재 화면 해상도가 다를 때 `resize_method`(기본 `ST.RESIZE_METHOD = cocos_min_strategy`)로 템플릿을 리사이즈. `resolution` 없거나 해상도 동일·`resize_method is None`이면 원본 그대로. 리사이즈 후 `cv2.resize`. 최소 1px 보장.

### 8.5 `Predictor` (좌표 예측)

```python
class Predictor(object):
    DEVIATION = 100
```

해상도 변화 시 클릭 지점/탐색 영역을 예측한다. 모든 메서드는 `@staticmethod`/`@classmethod`.

| 메서드 | 시그니처 | 설명 |
|--------|----------|------|
| `count_record_pos` | `count_record_pos(pos, resolution)` | 좌표를 화면 중심 대비 너비 기준 백분율 오프셋(delta_x, delta_y)으로 변환(소수 3자리). x·y 모두 너비(`_w`)로 나눔 |
| `get_predict_point` | `get_predict_point(record_pos, screen_resolution)` | `record_pos`(delta)로부터 현재 해상도에서의 예측 클릭점(target_x, target_y) 계산 |
| `get_predict_area` | `get_predict_area(record_pos, image_wh, image_resolution=(), screen_resolution=())` | 예측 점 주변 탐색 사각형 반환. 반경 = 이미지 크기 기반 + `DEVIATION`(100). `image_resolution` 유무로 반경 산식 분기 |

`DEVIATION`(100)은 예측 영역에 더하는 여유 마진(px).

---

## 9. `core/api.py` — Public API

`from airtest.core.api import *`로 사용. 대부분 함수는 `@logwrap`으로 리포트에 기록되며 `G.DEVICE`에 위임한다. `assertions.py`의 assert_* 함수들도 여기서 재수출된다(`api.py:16-20`).

### 9.1 디바이스 셋업

#### `init_device(platform="Android", uuid=None, **kwargs)`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `platform` | str | `"Android"` | "Android"/"IOS"/"Windows" 등 |
| `uuid` | str/None | `None` | 디바이스 식별자(Android serialno, Windows handle, iOS uuid) |
| `**kwargs` | | - | 플랫폼별 옵션 (예: `cap_method=JAVACAP`) |

반환: device 인스턴스. `import_device_cls(platform)`로 클래스 얻어 인스턴스화 후 `G.add_device(dev)`로 등록·현재 디바이스 설정. (데코레이터 없음)

#### `connect_device(uri)` `@logwrap`
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `uri` | str | `android://adbhost:adbport/serialno?param=value` 형식 |

동작: `parse_device_uri(uri)`로 (platform, uuid, params) 파싱 → `init_device`. 반환: device 인스턴스. 플랫폼별 URI 예시는 docstring(`api.py:57-71`) 참조 — Android/Windows/iOS 모두 지원.

#### `device()`
현재 활성 디바이스(`G.DEVICE`) 반환. (데코레이터 없음)

#### `set_current(idx)`
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `idx` | str/int | uuid 또는 `G.DEVICE_LIST` 인덱스 |

동작: uuid 매칭 우선, 아니면 int 인덱스. 둘 다 실패 시 `IndexError`. 성공 시 `G.DEVICE` 갱신. 반환 없음.

#### `auto_setup(basedir=None, devices=None, logdir=None, project_root=None, compress=None)`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `basedir` | str | `None` | 스크립트 basedir(`__file__` 가능). 파일이면 dirname 추출 후 `G.BASEDIR`에 추가 |
| `devices` | list[str] | `None` | `connect_device` URI 리스트 |
| `logdir` | str/bool | `None` | 로그 디렉터리. `True`면 `<basedir>/log`. `script_log_dir`+`set_logdir` 호출 |
| `project_root` | str | `None` | `ST.PROJECT_ROOT` 설정 |
| `compress` | int 1-99 | `None` | `ST.SNAPSHOT_QUALITY` 설정(기본 의도 10) |

스크립트 실행 환경 일괄 셋업. 반환 없음.

### 9.2 앱/디바이스 제어 (모두 `@logwrap`, `G.DEVICE` 위임)

| 함수 | 시그니처 | 반환 | 플랫폼 | 위임 |
|------|----------|------|--------|------|
| `shell` | `shell(cmd)` | shell 출력 | Android | `G.DEVICE.shell(cmd)` |
| `start_app` | `start_app(package, activity=None)` | None | Android, iOS | `G.DEVICE.start_app(package, activity)` |
| `stop_app` | `stop_app(package)` | None | Android, iOS | `G.DEVICE.stop_app(package)` |
| `clear_app` | `clear_app(package)` | None | Android | `G.DEVICE.clear_app(package)` |
| `install` | `install(filepath, **kwargs)` | None | Android, iOS | `G.DEVICE.install_app(filepath, **kwargs)` (예: `install_options=["-r","-t"]`) |
| `uninstall` | `uninstall(package)` | None | Android, iOS | `G.DEVICE.uninstall_app(package)` |
| `wake` | `wake()` | None | Android | `G.DEVICE.wake()` (일부 기종 미동작) |
| `home` | `home()` | None | Android, iOS | `G.DEVICE.home()` |
| `get_clipboard` | `get_clipboard(*args, **kwargs)` | str | Android/iOS/Windows | `G.DEVICE.get_clipboard(...)` (iOS 원격 시 `wda_bundle_id` 필요) |
| `set_clipboard` | `set_clipboard(content, *args, **kwargs)` | None | Android/iOS/Windows | `G.DEVICE.set_clipboard(...)` |
| `paste` | `paste(*args, **kwargs)` | None | Android/iOS/Windows | `G.DEVICE.paste(...)` |
| `push` | `push(local, remote, *args, **kwargs)` | pushed 파일명 | Android, iOS | `G.DEVICE.push(...)` |
| `pull` | `pull(remote, local, *args, **kwargs)` | pulled 파일명 | Android, iOS | `G.DEVICE.pull(...)` |

### 9.3 입력 조작

#### `snapshot(filename=None, msg="", quality=None, max_size=None)` `@logwrap`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `filename` | str/None | `None` | 저장 파일명. 상대경로면 `ST.LOG_DIR` 기준 |
| `msg` | str | `""` | 리포트용 설명 |
| `quality` | int/None | `None`→`ST.SNAPSHOT_QUALITY` | 품질 1-99 |
| `max_size` | int/None | `None`→`ST.IMAGE_MAXSIZE` | 최대 크기 |

반환: `{"screen": filename, "resolution": ...}` 또는 `None`. filename 있으면 `G.DEVICE.snapshot` 후 `try_log_screen`, 없으면 `try_log_screen`만. 플랫폼: Android/iOS/Windows.

#### `touch(v, times=1, **kwargs)` `@logwrap`  (별칭 `click`)
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `v` | `Template` / 좌표(x,y) | - | Template이면 `loop_find(v, timeout=ST.FIND_TIMEOUT)`로 찾고, 좌표면 그대로. 상대좌표(0~1) 지원 |
| `times` | int | `1` | 클릭 횟수 |
| `**kwargs` | | - | 플랫폼별(예: Android/Windows `duration`, Windows `right_click=True`) |

반환: 최종 클릭 좌표. 각 클릭 사이 `time.sleep(0.05)`, 끝에 `delay_after_operation()`. iOS는 좌표 반환 안 할 수 있음. `click = touch`(`api.py:378`).

#### `double_click(v)` `@logwrap`
`v`가 Template이면 `loop_find`, 좌표면 그대로 → `G.DEVICE.double_click(pos)`. 반환: 최종 좌표. `delay_after_operation()`.

#### `swipe(v1, v2=None, vector=None, **kwargs)` `@logwrap`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `v1` | `Template`/좌표 | - | 시작점. Template이면 `loop_find(timeout=ST.FIND_TIMEOUT)` |
| `v2` | `Template`/좌표/None | `None` | 끝점. Template이면 `loop_find(timeout=ST.FIND_TIMEOUT_TMP)` |
| `vector` | (x,y)/None | `None` | v2 대신 이동 벡터. 둘 다 ≤1이면 화면 비율로 해석(`get_current_resolution`로 px 변환) |
| `**kwargs` | | - | 예: Android/iOS `duration=1, steps=6` |

반환: `(pos1, pos2)`. v2·vector 모두 없으면 `Exception("no enough params for swipe")`. v1 Template 미발견 시 `TargetNotFoundError`(단 v2 Template의 filepath를 미리 접근해 리포트 표시 보정). `delay_after_operation()`. 플랫폼: Android/Windows/iOS.

#### `pinch(in_or_out='in', center=None, percent=0.5)` `@logwrap`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `in_or_out` | str | `'in'` | `"in"`/`"out"` |
| `center` | (x,y)/None | `None` | 기본은 화면 중앙 |
| `percent` | float | `0.5` | 화면 대비 비율 |

`try_log_screen()` 후 `G.DEVICE.pinch(...)`. 반환 없음. 플랫폼: Android.

#### `keyevent(keyname, **kwargs)` `@logwrap`
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `keyname` | str | 플랫폼별 키명. Android=`adb shell input keyevent`(예 "HOME"/"BACK"/"3"), Windows=`pywinauto.keyboard`(예 "{DEL}","%{F4}"), iOS=home/volumeUp/volumeDown만 |
| `**kwargs` | | 플랫폼별 |

`G.DEVICE.keyevent` 후 `delay_after_operation()`. 반환 없음.

#### `text(text, enter=True, **kwargs)` `@logwrap`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `text` | str | - | 입력 문자열(유니코드 지원). 입력 위젯이 먼저 활성화돼 있어야 함 |
| `enter` | bool | `True` | 입력 후 Enter 키 전송 |
| `**kwargs` | | - | 예: Android `search=True` |

`G.DEVICE.text` 후 `delay_after_operation()`. 반환 없음. 플랫폼: Android/Windows/iOS.

#### `sleep(secs=1.0)` `@logwrap`
`time.sleep(secs)`. 리포트에 기록되는 sleep. 반환 없음.

### 9.4 탐색/검사

#### `wait(v, timeout=None, interval=0.5, intervalfunc=None)` `@logwrap`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `v` | `Template` | - | 대기할 타깃 |
| `timeout` | float/None | `None`→`ST.FIND_TIMEOUT`(20) | 최대 대기 |
| `interval` | float | `0.5` | 재시도 간격 |
| `intervalfunc` | callable/None | `None` | 실패 시 콜백 |

반환: 매칭 좌표. 미발견 시 `TargetNotFoundError`. 내부적으로 `loop_find`. 플랫폼: Android/Windows/iOS.

#### `exists(v)` `@logwrap`
`loop_find(v, timeout=ST.FIND_TIMEOUT_TMP)` 시도 → 미발견 시 `False`, 발견 시 좌표 반환. **예외를 던지지 않음**(반환값으로 분기). 반환값(좌표)을 그대로 touch에 넘겨 재탐색을 줄일 수 있음.

#### `find_all(v)` `@logwrap`
`G.DEVICE.snapshot(quality=ST.SNAPSHOT_QUALITY)` 후 `v.match_all_in(screen)`. 반환: `[{'result':(x,y), 'rectangle':(...), 'confidence':float}, ...]` 리스트.

---

## 10. `core/assertions.py` — 단언 함수

모두 `@logwrap`. AssertionError를 던져 리포트에서 실패 스텝으로 기록. `api.py`가 `assert_exists ~ assert_not_is_instance`를 재수출(`api.py:16-20`). 비교 계열(greater/less)은 `assertions.py`에 정의되어 있으나 `api.py` 재수출 목록에는 미포함.

### 10.1 이미지 기반 단언

#### `assert_exists(v, msg="")`
| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `v` | `Template` | - | 존재 검증 타깃 |
| `msg` | str | `""` | 리포트용 설명 |

동작: `loop_find(v, timeout=ST.FIND_TIMEOUT, threshold=ST.THRESHOLD_STRICT or v.threshold)`. 성공 시 좌표 반환. `TargetNotFoundError` 발생 시 `AssertionError("%s does not exist in screen, ...")`로 전환. **`THRESHOLD_STRICT`를 우선 적용**하는 점이 일반 탐색과 다름.

#### `assert_not_exists(v, msg="")`
`loop_find(v, timeout=ST.FIND_TIMEOUT_TMP)`이 성공하면(=존재) `AssertionError`. `TargetNotFoundError`(=부재)면 통과. 반환: `None`.

### 10.2 값 비교 단언

공통 시그니처: `(first, second, msg="", snapshot=True)` 또는 단항 `(expr/obj, ...)`. `snapshot` 파라미터는 시그니처에 있으나 본문에서 사용되지 않음(`# noqa`). 실패 시 표준 Python `assert`로 `AssertionError` 발생.

| 함수 | 조건(통과) | 메시지 |
|------|------------|--------|
| `assert_equal(first, second)` | `first == second` | "... are not equal" |
| `assert_not_equal(first, second)` | `first != second` | "... are equal" |
| `assert_true(expr)` | `bool(expr)` | "expression is not True" |
| `assert_false(expr)` | `not bool(expr)` | "expression is not False" |
| `assert_is(first, second)` | `first is second` | "... not the same object" |
| `assert_is_not(first, second)` | `first is not second` | "... are the same object" |
| `assert_is_none(expr)` | `expr is None` | "... is not None" |
| `assert_is_not_none(expr)` | `expr is not None` | "... is None" |
| `assert_in(first, second)` | `first in second` | "... is not in ..." |
| `assert_not_in(first, second)` | `first not in second` | "... is in ..." |
| `assert_is_instance(obj, cls)` | `isinstance(obj, cls)` | "... is not an instance of ..." |
| `assert_not_is_instance(obj, cls)` | `not isinstance(obj, cls)` | "... is an instance of ..." |
| `assert_greater(first, second)` | `first > second` | "... is not greater than ..." |
| `assert_greater_equal(first, second)` | `first >= second` | "... not greater than or equal ..." |
| `assert_less(first, second)` | `first < second` | "... is not less than ..." |
| `assert_less_equal(first, second)` | `first <= second` | "... not less than or equal ..." |

---

## 11. `core/error.py` — 예외 계층

```
Exception
├─ BaseError(value)                      # __init__(value); __str__→repr(value)
│   ├─ AirtestError                      # Airtest 기본 에러
│   │   ├─ TargetNotFoundError           # 타깃 이미지 미발견 (loop_find/wait 등)
│   │   └─ ScriptParamError              # 스크립트 파라미터 오류
│   ├─ InvalidMatchingMethodError        # CVSTRATEGY에 잘못된 매칭 방법
│   ├─ DeviceConnectionError             # 디바이스 연결 오류 (정규식 DEVICE_CONNECTION_ERROR 보유)
│   ├─ NoDeviceError                     # 디바이스 미연결 (G.DEVICE getter가 raise)
│   ├─ ScreenError                       # 화면 캡처 방식 오류
│   │   └─ MinicapError
│   ├─ MinitouchError
│   ├─ PerformanceError
│   ├─ LocalDeviceError(value="Can only use this method on a local device.")
│   ├─ WDAError                          # facebook-wda
│   ├─ TIDeviceError                     # tidevice
│   └─ GOIOSError                        # go-ios
├─ AdbError(stdout, stderr)              # __str__→"stdout[...] stderr[...]"
│   └─ AdbShellError
└─ ICmdError(stdout, stderr)             # __str__→"stdout[...] stderr[...]"
```

| 예외 | 부모 | 특이점 |
|------|------|--------|
| `BaseError` | `Exception` | `value` 저장, `__str__`은 `repr(value)` |
| `AirtestError` | `BaseError` | Airtest 일반 에러 마커 |
| `InvalidMatchingMethodError` | `BaseError` | `cv.py:176`에서 발생 |
| `TargetNotFoundError` | `AirtestError` | `cv.py:80`, assertions, swipe 등 |
| `ScriptParamError` | `AirtestError` | - |
| `AdbError` | `Exception`(BaseError 아님) | `stdout`/`stderr` 보유 |
| `AdbShellError` | `AdbError` | adb shell 오류 |
| `DeviceConnectionError` | `BaseError` | 클래스 변수 `DEVICE_CONNECTION_ERROR`(정규식) |
| `NoDeviceError` | `BaseError` | `G.DEVICE` 접근 시(`helper.py:21`) |
| `ICmdError` | `Exception` | stdout/stderr 보유 |
| `ScreenError`/`MinicapError` | `BaseError`/`ScreenError` | 캡처 오류 |
| `MinitouchError`/`PerformanceError` | `BaseError` | - |
| `LocalDeviceError` | `BaseError` | 기본 메시지 보유, 원격 iOS에서 로컬 전용 메서드 호출 시 |
| `WDAError`/`TIDeviceError`/`GOIOSError` | `BaseError` | iOS 백엔드별 |

주의점: `AdbError`/`AdbShellError`/`ICmdError`는 `BaseError`가 아니라 `Exception`을 직접 상속하므로 `except BaseError`로 잡히지 않는다.

---

## 12. 모듈 간 호출 요약

| 호출자 | 피호출 | 위치 |
|--------|--------|------|
| `api.touch/double_click/swipe/wait` | `cv.loop_find` | `api.py:366,394,440,617` |
| `api.find_all` | `Template.match_all_in` | `api.py:668` |
| `api.snapshot` | `cv.try_log_screen` | `api.py:296,298` |
| `api.init_device` | `helper.import_device_cls`, `G.add_device` | `api.py:41,44` |
| `api.connect_device` | `utils.snippet.parse_device_uri`, `init_device` | `api.py:73-74` |
| `api.*`(조작계) | `G.DEVICE.<method>`, `helper.delay_after_operation` | 전반 |
| `assertions.assert_exists/not_exists` | `cv.loop_find` | `assertions.py:27,48` |
| `cv.loop_find` | `G.DEVICE.snapshot`, `Template.match_in`, `try_log_screen` | `cv.py:62,69,71` |
| `cv.Template._cv_match` | `MATCHING_METHODS`(aircv 매칭 클래스) | `cv.py:172-185` |
| `cv.Template.filepath` | `G.BASEDIR` | `cv.py:142` |
| `helper.G.DEVICE`(메타프로퍼티) | `error.NoDeviceError` | `helper.py:21` |
| `helper.import_device_cls` | 플랫폼별 Device 모듈(지연 import) | `helper.py:246-255` |

핵심 불변식: **모든 디바이스 조작은 `G.DEVICE`(현재 디바이스)에 위임**되며, 디바이스가 없으면 `G.DEVICE` 접근 자체가 `NoDeviceError`를 던진다. CV 매칭은 `Template` → `ST.CVSTRATEGY` 순회 → `MATCHING_METHODS` 클래스 실행 → 첫 성공 반환의 단일 파이프라인을 따른다.
