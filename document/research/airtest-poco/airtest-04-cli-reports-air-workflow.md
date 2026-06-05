---
type: reference
source: Airtest (AirtestProject/Airtest)
generated: subagent
---

# Airtest 04 — CLI · `.air` 스크립트 포맷 · HTML 리포트 · utils

`airtest/cli/`, `airtest/report/`, `airtest/utils/` 전체와 README / docs / wiki 를 기반으로 한 레퍼런스. 대상 버전: `airtest 1.4.3` (`airtest/utils/version.py:1`).

이 문서가 다루는 범위:
- CLI 진입점과 4개 서브커맨드 (`version` / `run` / `info` / `report`)
- `.air` 스크립트 폴더 포맷 (`.py` + `.png` 템플릿 에셋)
- `unittest` 기반 실행 파이프라인 (`AirtestCase`)
- 로그 구조 (`AirtestLogger` / `Logwrap`) → JSON line 로그 → HTML 리포트 생성 파이프라인 (`LogToHtml`)
- `airtest/utils/` 하위 유틸 모듈 역할
- AirtestIDE / Poco 와의 관계 (문서 언급 부분)

---

## 1. 큰 그림 — 호출 체인

```
airtest <cmd> ...                (console_scripts entry: airtest.cli.__main__:main, setup.py:76)
        │
   __main__.main(argv)           airtest/cli/__main__.py:5
        │ get_parser() → argparse 서브커맨드 dispatch
        ├─ version → airtest.utils.version.show_version()
        ├─ info    → airtest.cli.info.get_script_info(script) → print(json)
        ├─ report  → airtest.report.report.main(args)        → LogToHtml.report()
        └─ run     → airtest.cli.runner.run_script(args)
                          │ setup_by_args() → auto_setup()  (디바이스/로그/스냅샷 세팅)
                          │ unittest.TestSuite([AirtestCase()])
                          └ AirtestCase.runTest() → exec(<script>.py)
                                  │ 실행 중 @logwrap 데코된 api → AirtestLogger.log() → log.txt(JSON line)
                                  └ (--recording 시) dev.start_recording / stop_recording
```

리포트 단계는 실행과 분리돼 있다. `run` 은 `log.txt`(JSON line) 만 남기고, `report` 는 그 `log.txt` 를 읽어 `log.html` 을 만든다.

| 단계 | 입력 | 출력 | 핵심 클래스/함수 |
|------|------|------|------------------|
| run | `.air` 디렉토리 + 디바이스 URI | `log/log.txt`, 스크린샷 `.jpg`, (옵션)`.mp4` | `AirtestCase` (`airtest/cli/runner.py:18`) |
| 로깅 | api 호출 | JSON line | `AirtestLogger` / `Logwrap` (`airtest/utils/logwraper.py`) |
| report | `log/log.txt` + `.air` | `log.html` (+ export dir) | `LogToHtml` (`airtest/report/report.py:60`) |
| info | `.air` 의 `.py` | JSON 문자열 | `get_script_info` (`airtest/cli/info.py:13`) |

---

## 2. CLI 진입점

### 2.1 `main(argv=None)`

`airtest/cli/__main__.py:5`

```python
def main(argv=None):
    ap = get_parser()
    args = ap.parse_args(argv)
    if args.action == "info":   ...
    elif args.action == "report": ...
    elif args.action == "run":  ...
    elif args.action == "version": ...
    else: ap.print_help()
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `argv` | `list[str]` / `None` | `None` | None 이면 `sys.argv` 사용 (argparse 기본). 테스트에서 직접 전달 가능 |

- 동작: `get_parser()` 로 만든 argparse 로 파싱 후, `args.action` 에 따라 lazy import 하여 서브 핸들러를 호출. 각 핸들러를 모듈 최상단이 아니라 분기 내부에서 import → 불필요한 의존성 로딩 회피.
- 주의: `action` 이 없으면(`airtest` 단독 실행) `print_help()`.
- `console_scripts` 로 `airtest` 명령에 매핑됨 (`setup.py:76`). `python -m airtest ...` 형태로도 실행 가능 (`__main__.py:24` 의 `if __name__ == '__main__'`).

### 2.2 파서 — `get_parser()` / `runner_parser()` / `cli_setup()`

`airtest/cli/parser.py`

```python
def get_parser():
    ap = argparse.ArgumentParser()
    subparsers = ap.add_subparsers(dest="action", help="version/run/info/report")
    subparsers.add_parser("version", ...)
    ap_run = subparsers.add_parser("run", ...);   runner_parser(ap_run)
    ap_info = subparsers.add_parser("info", ...);  ap_info.add_argument("script", ...)
    ap_report = subparsers.add_parser("report", ...); report_parser(ap_report)
    return ap
```

`report_parser` 는 `airtest.report.report.get_parger` 의 alias (`parser.py:4`). `dest="action"` 으로 선택된 서브커맨드명이 `args.action` 에 들어간다.

#### `airtest run` 옵션 (`runner_parser`, `parser.py:25`)

```python
ap.add_argument("script", help="air path")
ap.add_argument("--device", nargs="?", action="append")
ap.add_argument("--log", nargs="?", const=True)
ap.add_argument("--compress", type=int, choices=range(1, 100), default=10)
ap.add_argument("--recording", nargs="?", const=True)
ap.add_argument("--no-image", nargs="?", const=True)
```

| 옵션 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `script` (위치인자) | str | (필수) | `.air` 디렉토리 경로 (또는 `.py` 경로) |
| `--device` | str (repeatable) | `[]` | 디바이스 URI. `action="append"` → 여러 번 줘서 멀티 디바이스. 예: `Android:///`, `Windows:///?title_re=Unity.*` |
| `--log` | str / True | (없음 → 로그 미저장) | 로그 디렉토리. 값 없이 `--log` 만 주면 `const=True` → 스크립트 디렉토리 하위 `log/` |
| `--compress` | int 1–99 | `10` | 스냅샷 JPEG 품질. `ST.SNAPSHOT_QUALITY` 로 반영 |
| `--recording` | str(`.mp4`) / True | (없음) | 실행 중 화면 녹화 (Android 만). 값 없이 주면 `recording_<uuid>.mp4` |
| `--no-image` | flag | (없음) | 스크린샷 저장 안 함 → `ST.SAVE_IMAGE = False` |

- `--device` 가 `choices` 없이 자유 문자열이므로 플랫폼 독립. URI 파싱은 `parse_device_uri` (`airtest/utils/snippet.py:158`).
- `cli_setup(args=None)` (`parser.py:37`): "future api for setup env by cli". `--report` 가 args 에 있으면 report 경로, 아니면 `setup_by_args`. 현재 `__main__` 흐름에서는 쓰이지 않는 보조 진입점.

---

## 3. `.air` 스크립트 포맷

### 3.1 폴더 구조

`.air` 는 **디렉토리**다 (단일 파일 아님). 확장자 상수 `EXT = ".air"` (`airtest/utils/compat.py:8`), `AirtestCase.SCRIPTEXT = ".air"`, `TPLEXT = ".png"` (`airtest/cli/runner.py:22-23`).

```
test_blackjack.air/                 ← 디렉토리, 이름이 .air 로 끝남
├── test_blackjack.py               ← 디렉토리명과 동일(확장자만 .py) 스크립트 본문
├── tpl1499240443959.png            ← 이미지 템플릿 에셋 (touch/wait/assert 의 Template 대상)
├── tpl1499240472304.png
├── ... (여러 .png)
├── blackjack-release-signed.apk    ← (선택) 함께 쓰는 리소스
├── log/                            ← run 시 생성: log.txt + 스크린샷.jpg + *.mp4
└── log.html / test_blackjack.html  ← report 시 생성된 HTML 리포트
```

규칙 (`airtest/utils/compat.py:33` `script_dir_name`):
- `script_path` 가 `.air` 로 끝나면 → `path = <그 디렉토리>`, `name = <basename>.py`.
- 아니면 (`.py` 직접 지정) → `path = dirname`, `name = basename`.
- 즉 `<X>.air/` 안의 진입 스크립트는 반드시 `<X>.py`.

`info.py:18-24` 도 같은 규칙: `.py` 경로를 주면 부모 디렉토리가 `.air` 인지 보고 `script_name` 을 보정.

### 3.2 스크립트 본문 (`<X>.py`)

샘플 `playground/test_blackjack.air/test_blackjack.py`:

```python
# -*- encoding=utf8 -*-
__author__ = "刘欣"
from airtest.core.api import *
import os
auto_setup(__file__)
...
touch(Template(r"tpl1499240443959.png", record_pos=(0.22, -0.165), resolution=(2560, 1536)))
assert_exists(Template(r"tpl1499240472304.png", ...), "请下注")
p = wait(Template(r"tpl1499240490986.png", ...))
swipe(Template(...), vector=[0.0005, -0.4023])
log("Test OK")
```

포맷 관례:
- 첫 줄 인코딩 선언, 그 다음 `__author__` / `__title__` / `__desc__` (메타 → `info` 커맨드 및 리포트 헤더에서 추출).
- `from airtest.core.api import *` 로 모든 api 노출.
- `Template(r"<파일명>.png", ...)` 의 파일명은 같은 `.air` 디렉토리 안의 png 를 가리킨다. `auto_setup` 이 base dir 을 그 디렉토리로 잡아주므로 상대 파일명만 적는다.
- `record_pos` / `resolution` / `target_pos` / `threshold` 등은 IDE 가 캡처 시 자동 기록하는 메타 (해상도 보정·클릭 위치·임계치).

### 3.3 메타 추출 — `info` 커맨드

`get_script_info(script_path)` (`airtest/cli/info.py:13`)

```python
result_json = {"name": script_name, "path": script_path,
               "author": author, "title": title, "desc": desc}
return json.dumps(result_json)
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `script_path` | str | `.air` 디렉토리 또는 `.py` 경로 |

- 반환: JSON 문자열 `{"name","path","author","title","desc"}`.
- `get_author_title_desc(text)` (`info.py:37`): 정규식 2종으로 `__attr__ = "..."` / `__attr__ = '...'` 형태에서 `__author__`/`__title__`/`__desc__` 를 추출. `process_desc` 는 줄별 `strip` 으로 정리.
- CLI: `airtest info "path.air"` → `print` (`__main__.py:10`). `python -m airtest info ...` 동일.
- 주의: `desc` 외 정의된 다른 `__x__` 도 dict 에 잡히지만 반환에는 author/title/desc 만 포함.

---

## 4. 실행 파이프라인 — `airtest/cli/runner.py`

### 4.1 `setup_by_args(args)` (`runner.py:117`)

CLI 인자를 받아 환경을 세팅하고 `auto_setup` 호출.

핵심 동작:
1. `args.device` 를 list 로 정규화 (없으면 `"do not connect device"` 출력).
2. `script_dir_name(args.script)` 로 base dir 추출 → 템플릿 검색 기준.
3. `args.log` 가 있으면 `script_log_dir(dirpath, args.log)` 로 로그 디렉토리 결정 후 출력.
4. `args.compress` → `compress`, 없으면 `ST.SNAPSHOT_QUALITY`.
5. `args.no_image` → `ST.SAVE_IMAGE = False`.
6. `project_root` 추정: `ST.PROJECT_ROOT` 미설정 시 `dirname(args.script)`.
7. `auto_setup(dirpath, devices, args.log, project_root, compress)` 호출 (api 모듈).

| 인자 필드 | 매핑 결과 |
|-----------|-----------|
| `args.device` | `auto_setup(devices=...)` |
| `args.log` | 로그 디렉토리 (True → `<dir>/log`) |
| `args.compress` | 스냅샷 품질 |
| `args.no_image` | `ST.SAVE_IMAGE` |

### 4.2 `run_script(parsed_args, testcase_cls=AirtestCase)` (`runner.py:152`)

```python
def run_script(parsed_args, testcase_cls=AirtestCase):
    global args                      # AirtestCase / 스크립트에서 참조
    args = parsed_args
    suite = unittest.TestSuite()
    suite.addTest(testcase_cls())
    result = unittest.TextTestRunner(verbosity=0).run(suite)
    if not result.wasSuccessful():
        if result.failures and "AssertionError" in repr(result.failures):
            sys.exit(20)
        sys.exit(-1)
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `parsed_args` | argparse.Namespace | (필수) | `run` 서브파서가 만든 args |
| `testcase_cls` | type | `AirtestCase` | 커스텀 테스트케이스로 교체 가능 |

- 종료 코드 규약 (중요): **AssertionError 포함 실패 → `exit(20)`**, 그 외 실패 → `exit(-1)`, 성공 → 0. 멀티 디바이스 러너/CI 가 이 코드로 판정.
- `args` 를 의도적으로 global 로 둬서 `AirtestCase` 와 실행 스크립트(`exec` scope)가 공유.

### 4.3 `AirtestCase(unittest.TestCase)` (`runner.py:18`)

| 클래스 상수 | 값 | 의미 |
|-------------|----|------|
| `PROJECT_ROOT` | `"."` | 기본 프로젝트 루트 |
| `SCRIPTEXT` | `".air"` | 스크립트 디렉토리 확장자 |
| `TPLEXT` | `".png"` | 템플릿 이미지 확장자 |

생명주기:

| 메서드 | 동작 |
|--------|------|
| `setUpClass` | `setup_by_args(args)` 호출 → 디바이스/로그 세팅. `cls.scope = copy(globals())` 로 exec 스코프 준비, `scope["exec_script"] = exec_other_script` |
| `setUp` | `--log` && `--recording` 이면 모든 `G.DEVICE_LIST` 에 대해 `dev.start_recording(output=...)`. 파일명 규칙은 아래 표. `scope['__name__']='__main__'` 설정 (`if __name__=='__main__'` 지원) |
| `runTest` | `script_dir_name` 으로 `.py` 경로 확정 → `scope["__file__"]` 세팅 → 파일 읽어 `exec(compile(code, path, 'exec'), scope)`. 예외 시 `log(err, desc="Final Error", snapshot=True if 디바이스 있으면)` 후 reraise |
| `tearDown` | recording 이면 `dev.stop_recording()` |
| `exec_other_script` | (deprecated) 다른 `.air` 실행. `using()` api 사용 권장 경고. 서브 스크립트의 png 를 `_sub` 디렉토리로 복사하고 코드의 `'xxx.png'` 를 `"sub_dir/xxx.png"` 로 치환 후 exec |

녹화 파일명 규칙 (`setUp`, `runner.py:41`):

| 조건 | 파일명 |
|------|--------|
| `--recording` 값 미지정 | `recording_<uuid>.mp4` |
| `--recording test.mp4` && 디바이스 >1 | `<uuid>_test.mp4` |
| `--recording test.mp4` && 디바이스 1대 | `test.mp4` |

- 주의: 녹화는 Android 만 지원 (`docs/wiki/device/android.md:262`). 기본 `max_time` 1800초(30분). 파일명은 반드시 `.mp4`.

---

## 5. 로깅 구조 — `airtest/utils/logwraper.py`

리포트의 모든 데이터 원천. 실행 중 api 호출이 `@logwrap` 으로 감싸져 JSON line 으로 `log.txt` 에 기록된다.

### 5.1 `AirtestLogger(logfile)` (`logwraper.py:16`)

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `__init__` | `(self, logfile)` | `running_stack=[]`, `set_logfile(logfile)`, `reg_cleanup(self.handle_stacked_log)` 로 종료 시 미완 로그 flush 등록 |
| `set_logfile` | `(self, logfile)` | logfile 있으면 `realpath` 로 열기(`"w"`), None 이면 닫고 리셋 |
| `log` | `(self, tag, data, depth=None, timestamp=None)` | 한 줄 JSON 기록. depth None → `len(running_stack)`. depth==1 이고 디바이스 연결됐으면 `data['call_args']['device']` 에 `G.DEVICE.uuid` 주입. timestamp float 변환 실패 시 `time.time()`. `json.dumps({tag,depth,time,data}, default=_dumper)` + `\n`, 즉시 flush |
| `handle_stacked_log` | `(self)` | `running_stack` 에 남은 항목을 뒤에서부터 `log("function", ...)` 후 pop (비정상 종료 대비) |
| `_dumper` (static) | `(obj)` | json 직렬화 폴백. `to_json()` 있으면 사용 → 없으면 `obj.__dict__` 복사 + `__class__` 주입 → 안되면 `repr(obj)`. `Template` 객체가 `{"__class__":"Template", "filename":..., "_filepath":...}` 형태로 직렬화되는 근거 |

**로그 1줄 스키마** (`log.txt`):

```json
{"tag": "function"|"info", "depth": <int>, "time": <float>,
 "data": {"name": <api명>, "call_args": {...}, "start_time": ...,
          "ret": ...|"traceback": ..., "end_time": ...}}
```

### 5.2 `Logwrap(f, logger)` 데코레이터 (`logwraper.py:95`)

`@logwrap` (api 모듈에서 이 함수로 partial 바인딩) 의 본체. api 호출 정보를 로그에 남기고 리포트에 표시한다.

동작 순서:
1. `kwargs.pop('depth', None)` (py2 호환), `start=time.time()`.
2. `inspect.getcallargs(f, *args, **kwargs)` 로 호출 인자 dict `m` 생성.
3. `m.pop('snapshot', False)` → True 면 종료 시 스크린샷. `self`/`cls` 제거.
4. `fndata = {'name': f.__name__, 'call_args': m, 'start_time': start}` 를 `running_stack` push.
5. `f` 실행:
   - `LocalDeviceError` → 그대로 raise (리포트에서 실패로 안 보이게).
   - 그 외 예외 → `fndata` 에 `traceback`/`end_time` 추가 후 raise.
   - 성공 → `fndata` 에 `ret`/`end_time` 추가.
6. `finally`: `snapshot is True` 면 `try_log_screen(depth=len(running_stack)+1)`, `logger.log('function', fndata, depth=depth)`, `running_stack.pop()`.

특수 파라미터 (함수 정의에 추가하면 효과):

| 파라미터 | 효과 |
|----------|------|
| `snapshot=True` | 호출 후 스크린샷을 리포트에 첨부 (`ST.SAVE_IMAGE=False` 면 생략) |
| `depth=<int>` | 로그의 중첩 깊이 직접 지정 |

- depth 의미: `depth==0` 단일 라인, `depth==1` step(부모), `depth>=2` step 의 자식 (`LogToHtml._analyse`).
- `try_log_screen` 자식 로그(`data.name=="try_log_screen"`)와 `_cv_match` 자식 로그가 step 의 스크린샷/매칭 결과를 제공.

### 5.3 사용자 `log()` api (문서 기준)

`docs/wiki/code/code_example.md:248` — `log()` 는 리포트에 커스텀 로그 삽입 (4 파라미터, 1.1.6+):

| 파라미터 | 설명 |
|----------|------|
| `args` | 문자열 / 비문자열 / `traceback` 객체 |
| `timestamp` | 커스텀 타임스탬프 |
| `desc` | 로그 제목 |
| `snapshot` | True 면 현재 화면 캡처 첨부 |

로그 레벨 필터 (`code_example.md:294`): 스크립트 상단에서
```python
import logging
logging.getLogger("airtest").setLevel(logging.ERROR)
```
→ 초기화 로그 억제. 로거 이름은 `"airtest"` (`airtest/utils/logger.py:7`).

로그 저장 경로 (`code_example.md:278`): `ST.LOG_FILE` (txt 파일명), `set_logdir(...)` 로 디렉토리 지정.

---

## 6. HTML 리포트 파이프라인 — `airtest/report/report.py`

### 6.1 모듈 상수

| 상수 | 값 | 의미 |
|------|----|------|
| `DEFAULT_LOG_DIR` | `"log"` | 기본 로그 디렉토리명 |
| `DEFAULT_LOG_FILE` | `"log.txt"` | 기본 로그 파일명 |
| `HTML_TPL` | `"log_template.html"` | jinja2 템플릿 |
| `HTML_FILE` | `"log.html"` | 기본 출력 파일명 |
| `STATIC_DIR` | `dirname(report.py)` | css/js/image/fonts 정적 루트 |

### 6.2 `LogToHtml` 클래스 (`report.py:60`)

```python
class LogToHtml(object):
    scale = 0.5
    def __init__(self, script_root, log_root="", static_root="", export_dir=None,
                 script_name="", logfile=None, lang="en", plugins=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `script_root` | str | (필수) | `.air` 스크립트 경로. 파일이면 `script_dir_name` 으로 분해 |
| `log_root` | str | `""` → `ST.LOG_DIR` → `./log` | 로그 & 스크린샷 디렉토리 |
| `static_root` | str | `""` → `STATIC_DIR` | 정적 리소스 루트 (http 주소도 가능) |
| `export_dir` | str / None | `None` | 지정 시 모든 리소스를 묶은 portable 리포트 디렉토리 생성 |
| `script_name` | str | `""` | 스크립트명 (없으면 `script_dir_name` 으로 결정) |
| `logfile` | str / None | `None` → `ST.LOG_FILE` → `log.txt` | 읽을 JSON line 로그 파일 |
| `lang` | str | `"en"` | 리포트 언어 (`en`/`zh`) |
| `plugins` | list / None | `None` | 리포터 플러그인 모듈명들 (poco / airtest-selenium 사용 시) |

주요 메서드:

| 메서드 | 동작 |
|--------|------|
| `init_plugin_modules(plugins)` (static) | 각 플러그인 모듈명을 `__import__` (실패 시 에러 로그) |
| `_load` | `log_root/logfile` 을 줄 단위 `json.loads` 하여 `self.log` 채움 |
| `_analyse` | `self.log` 를 depth 기준으로 step 트리화. depth0=단일, depth1=step(+`__children__`), depth>=2=자식(앞에 insert). 각 step 을 `_translate_step` 으로 변환. 마지막 step 에 `traceback` 있으면 `test_result=False`. `run_start`/`run_end` 갱신 |
| `_translate_step` | 한 step → `{title, time, code, screen, desc, traceback, log, assert}` dict |
| `_translate_title` | api명 → 표시 제목 매핑 (touch→Touch, swipe→Swipe, assert_exists→Assert exists ...) |
| `_translate_code` | `function` step 의 `call_args` → `{name, args:[{key,value}]}`. `value.__class__=="Template"` 면 이미지 경로 해석 + `imread` 후 `get_resolution` 으로 `resolution` 부여. export 시 이미지 복사 |
| `_translate_desc` | api별 자연어 설명 (en/zh 두 dict). 예: touch→"Touch target image", exists→"Image exists/not exists" |
| `_translate_screen` | step 의 `try_log_screen` 자식에서 스크린샷 경로/썸네일, `_cv_match` 자식에서 매칭 rect/pos/confidence 추출. touch/assert_exists/wait/exists 는 최종 pos 보정, swipe 는 vector 계산 |
| `_translate_info` | `(traceback, log_msg)` 반환. `data.traceback` 있으면 실패 step, `tag=="info"` & `data.log` 있으면 텍스트 로그 |
| `_translate_assertion` | `assert_` api 이고 `call_args.msg` 있으면 그 메시지 |
| `_translate_device` | `connect_device` step 의 uri 파싱(`parse_device_uri`) → `self.devices[uuid]=uri` |
| `get_thumbnail` (cls) | 스크린샷을 `compress_image(..., ST.SNAPSHOT_QUALITY, max_size=300)` 로 `*_small.<ext>` 생성 |
| `get_small_name` (cls) | `name_small.ext` 파일명 규칙 |
| `div_rect` (static) | 매칭 사각형 4점 → `{left, top, width, height}` (js 용) |
| `_render` (static) | jinja2 `Environment(FileSystemLoader(STATIC_DIR), autoescape=True)`, 필터 `nl2br`/`datetime` 등록 후 `template.render(**vars)` → 파일 기록 |
| `_make_export_dir` | `<script>.log/` 디렉토리 생성, 스크립트/로그/정적(css·fonts·image·js) 복사. http static_root 면 정적 복사 생략 |
| `get_relative_log` | `log.txt` 의 상대 경로 (export 시 `log/log.txt`) |
| `get_console` | 출력 디렉토리의 `console.txt` 읽기 (utf-8 실패 시 gbk) |
| `report_data` | `_load`→`_analyse`→ 모든 데이터를 dict 로 묶고 `info`(script meta)+devices 첨부. `data['data']` 에 전체를 json 직렬화하되 `<`→`{`, `>`→`}` 치환 (highlight.js 오인 방지) |
| `report` | export 면 `_make_export_dir`, 정적 root `static/`. record_list 없으면 log_root 의 `.mp4` 자동 수집. `report_data` 후 `_render(HTML_TPL, output_file, **data)` |

`report_data` 가 만드는 데이터 키 (`report.py:498`): `steps, name, scale, test_result, run_end, run_start, static_root, lang, records, info, log, console, data`.

### 6.3 `simple_report(...)` (`report.py:542`)

```python
def simple_report(filepath, logpath=True, logfile=None, output=HTML_FILE):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `filepath` | str | (필수) | 스크립트 경로 (`__file__` 직접 전달 가능) |
| `logpath` | True / str | `True` | 로그 디렉토리. True → 스크립트 디렉토리/`ST.LOG_DIR` |
| `logfile` | str / None | `None` → `log.txt` | log.txt 경로 |
| `output` | str | `log.html` | 출력 HTML (반드시 `.html`) |

- 스크립트 내부에서 직접 리포트를 만들 때 쓰는 단축 함수. `LogToHtml(...).report()` 래핑.

### 6.4 `airtest report` 옵션 — `get_parger(ap)` (`report.py:550`)

```python
ap.add_argument("script", help="script filepath")
ap.add_argument("--outfile", default=HTML_FILE)        # log.html
ap.add_argument("--static_root")
ap.add_argument("--log_root")                          # log_root/log.txt
ap.add_argument("--record", nargs="+")
ap.add_argument("--export")
ap.add_argument("--lang", default="en")
ap.add_argument("--plugins", nargs="+")
ap.add_argument("--report", default=True, nargs="?")   # placeholder
```

| 옵션 | 기본값 | 설명 |
|------|--------|------|
| `script` | (필수) | `.air` 경로 |
| `--outfile` | `log.html` | 출력 HTML 경로/파일명 |
| `--static_root` | (STATIC_DIR) | 정적 리소스 루트 (http 가능) |
| `--log_root` | `<script>/log` | 로그·스크린샷 루트 (`log_root/log.txt`) |
| `--record` | (auto) | 커스텀 녹화 mp4 경로(들) |
| `--export` | (없음) | 모든 리소스 포함 portable 리포트 디렉토리 생성 |
| `--lang` | `en` | 리포트 언어 (`en`/`zh`, 그 외는 en 으로 보정) |
| `--plugins` | (없음) | 리포터 플러그인 모듈명들 |
| `--report` | True | placeholder (cli_setup 분기용) |

`main(args)` (`report.py:563`): 인자를 `decode_path` 로 정리 후 `LogToHtml(...).report(HTML_TPL, output_file=args.outfile, record_list=record_list)`.

### 6.5 정적 리소스 (`airtest/report/`)

| 디렉토리/파일 | 역할 |
|----------------|------|
| `log_template.html` | jinja2 템플릿. `{{data|safe}}` 로 전체 데이터 주입, jquery-lang 으로 en/zh 전환, step 리스트·필터(All/Success/Failed/Assert)·녹화 비디오·console·log.txt 다운로드 UI |
| `report.py` | 파이프라인 본체 |
| `css/report.css`, `css/monokai_sublime.min.css` | 스타일 / 코드 하이라이트 테마 |
| `js/report.js` | `StepPannel` 등 리포트 인터랙션 (step 페이징·갤러리·필터·하이라이트). 작성: Era Chen (NetEase) |
| `js/paging.js`, `lazyload.js`, `jquery-lang.js`, `langpack/` | 페이징·지연로딩·다국어 |
| `js/highlight.min.js`, `bootstrap.min.js`, `jquery-1.10.2.min.js` | 서드파티 |
| `image/*.svg/png` | 결과 아이콘(success/fail/step_*), AirtestIDE/AirLab/Poco/NetEase 로고 |
| `fonts/` | SourceHanSansCN(중문) + glyphicons |

템플릿 변수 매핑 (`log_template.html` ↔ `report_data`): `info.title/author/path/name/devices`, `steps`, `console`, `log`, `records`, `static_root`, `lang`, `data`.

---

## 7. `airtest/utils/` 모듈 역할 표

| 파일 | 핵심 심볼 | 역할 | 비고 |
|------|-----------|------|------|
| `compat.py` | `EXT`, `decode_path`, `script_dir_name`, `script_log_dir`, `SUBPROCESS_FLAG`, `proc_communicate_timeout`, `raisefrom` | py2/3 + win 호환. `.air` 경로 분해의 단일 진실원 | win 에서 `CREATE_NO_WINDOW` 처리 |
| `logwraper.py` | `AirtestLogger`, `Logwrap` | api 로그를 JSON line 으로 기록 (리포트 원천) | report.py 가 소비 |
| `logger.py` | `init_logging`, `get_logger` | `"airtest"` 루트 로거 설정 (DEBUG, `[time][level]<name> msg`) | import 시 즉시 `init_logging()` |
| `snippet.py` | `parse_device_uri`, `reg_cleanup`/`_cleanup`, `kill_proc`, `split_cmd`, `get_std_encoding`, `on_method_ready`/`ready_method`, `make_file_executable`, `escape_special_char`, `get_absolute_coordinate`, `exitfunc`, `is_exiting` | 잡유틸 집합. URI 파싱, 종료 정리(threading._shutdown 후킹), lazy init 데코, 좌표 절대/상대 변환 | `threading._shutdown=exitfunc` 로 종료 cleanup 보장 |
| `decorators.py` | `add_decorator_to_methods` | 클래스의 public 메서드 전체에 데코레이터 일괄 적용 | |
| `nbsp.py` | `NonBlockingStreamReader`, `UnexpectedEndOfStream` | 서브프로세스 stdout/stderr 논블로킹 읽기 (별도 스레드+큐) | adb/logcat 스트림용 |
| `resolution.py` | `no_resize`, `cocos_min_strategy`, `predict_area` | 이미지 매칭용 해상도 적응 전략 + 검색 영역 예측 | cocos MIN 전략 design (960,640) |
| `retry.py` | `retries` | 재시도 데코레이터 (delay·backoff·exceptions·hook) | |
| `runcommand.py` | `runcommand`, `runcommand_with_json_output`, `run_background`, `run_background_with_pipe` | 셸 명령 실행/백그라운드/파이프 | |
| `safesocket.py` | `SafeSocket` | 정확한 길이 송수신 소켓 래퍼 (recv/send 루프, timeout/nonblocking) | win errno 10035/10053/10054 처리 |
| `selenium_proxy.py` | `WebChrome`, `Element` | selenium Chrome 을 airtest 로그/리포트에 통합 (`@logwrap` 으로 click/get/back) | AirtestIDE 의 selenium_plugin(macOS) 경로 추가 |
| `threadsafe.py` | `ThreadSafeIter`, `threadsafe_generator` | 제너레이터 thread-safe 화 (lock) | |
| `transform.py` | `TargetPos` | 템플릿 클릭 위치 9분할 상수(1~9) + `getXY` | `target_pos=5` 가 중심 |
| `version.py` | `__version__`, `get_airtest_version`, `show_version` | 버전(1.4.3) / `airtest version` 출력 | |
| `ffmpeg/ffmpeg_setter.py` | `get_or_fetch_platform_executables_else_raise`, `add_paths`, `main_static_ffmpeg/ffprobe` | static ffmpeg/ffprobe 자동 다운로드·PATH 등록 (zackees/static_ffmpeg) | 녹화 mp4 처리용. main URL 실패 시 netease S3 백업 |
| `apkparser/` | `APK` (+ axml/bytecode/stringblock) | apk 의 AndroidManifest(바이너리 XML) 파싱 → package/version/permission/activity 추출 | Androguard 유래(LGPL) |

### 7.1 `TargetPos` 상수 (`transform.py:4`)

```
1 2 3   LEFTUP   UP    RIGHTUP
4 0 6   LEFT     MID   RIGHT      (0 또는 5 = 중심)
7 8 9   LEFTDOWN DOWN  RIGHTDOWN
```

`getXY(cvret, pos)`: `pos==0|MID` 면 매칭 중심(`cvret["result"]`), 그 외엔 `cvret["rectangle"]` 4점으로 모서리/변 중점 계산. rectangle 없으면 중심 폴백.

### 7.2 `apkparser.APK` 주요 API (`apkparser/apk.py:25`)

| 메서드/프로퍼티 | 반환 | 설명 |
|------------------|------|------|
| `APK(filename)` | — | apk 열어 `AndroidManifest.xml`(binary)→`AXMLPrinter`→minidom 파싱, package/versionCode/versionName/permissions 추출 |
| `is_valid_apk()` | bool | manifest 파싱 성공 여부 |
| `get_package()` / `package` | str | 패키지명 |
| `androidversion_code`/`_name` | str | versionCode / versionName |
| `get_activities/services/receivers/providers()` | list | manifest 의 `android:name` 목록 |
| `permissions` | list | uses-permission 목록 |
| `min_sdk_version`/`target_sdk_version` | str | uses-sdk 속성 |
| `get_file(name)` / `dex` | bytes | zip 내 파일 raw |

- 보조 모듈: `axmlparser.AXMLParser`(바이너리 XML 이벤트 파서), `axmlprinter.AXMLPrinter`(→텍스트 XML), `bytecode.SV`/`BuffHandle`(struct 언팩), `stringblock.StringBlock`(문자열 풀), `typeconstants`(CHUNK_*/TYPE_* 상수). 모두 Androguard 유래 저수준 파서로 직접 호출보다 `APK` 를 통해 사용.

---

## 8. 디바이스 URI 포맷 (CLI `--device` / `connect_device`)

`parse_device_uri(uri)` (`snippet.py:158`)

```python
# Android:///SJE5T17B17?cap_method=javacap&touch_method=adb
platform, uuid, params = parse_device_uri(uri)
# platform="Android", uuid="SJE5T17B17", params={"cap_method":"javacap","touch_method":"adb"}
```

| URI 조각 | 매핑 | 예 |
|----------|------|----|
| scheme | platform | `Android` / `iOS` / `Windows` |
| netloc | `params["host"]` (`:` split) | `127.0.0.1:5037` (adb host:port) |
| path (lstrip `/`) | uuid | Android serialno / Windows handle / iOS uuid |
| query | params dict | `cap_method=JAVACAP&&ori_method=ADBORI` |

예시 (docs):
- `Android:///` — 첫 번째 연결 디바이스 (README.md:88)
- `Android://127.0.0.1:5037/c2b1c2a7` — 로컬 adb 의 특정 serialno (android.md:71)
- `Windows:///?title_re=Unity.*` — 제목 정규식으로 윈도우 앱 (README.md:91)
- `iOS:///` — 로컬 iOS (README_MORE.rst:290)

명령행 escape (android.md:113): `&&` 를 Windows 는 `^&^&`, macOS 는 `\&\&` 로 써야 함.

---

## 9. CLI 사용 예시 (README / README_MORE 기준)

```shell
# Android 실행
airtest run "path/to/your.air" --device Android:///
airtest run "untitled.air" --device "Android://127.0.0.1:5037/serial" --log log/
# Windows 앱
airtest run "path.air" --device "Windows:///?title_re=Unity.*"
# 녹화 (Android)
airtest run "demo.air" --device android:/// --log logs/ --recording
# 리포트 생성
airtest report "path.air"                 # → log.html
airtest report "path.air" --export out/    # portable 디렉토리
airtest report "path.air" --lang zh
# 메타 조회
python -m airtest info "path.air"          # → {"author":...,"title":...,"desc":...}
# 버전
airtest version
# python 모듈로
python -m airtest run "path.air" --device Android:///
```

`airtest run -h` / `airtest report -h` 도움말 전문은 `README_MORE.rst:293`, `README_MORE.rst:319`.

---

## 10. 주요 임계치/기본값 (docs/wiki 기준)

| 설정 (`ST` = `airtest.core.settings.Settings`) | 기본값 | 의미 | 출처 |
|----|--------|------|------|
| `SNAPSHOT_QUALITY` | 10 | 스냅샷 JPEG 압축 품질 [1,100] | code_example.md:432 |
| `IMAGE_MAXSIZE` | (없음=원본) | 스냅샷 최대 변 길이 (1.1.6+) | code_example.md:451 |
| `CVSTRATEGY` | `["surf","tpl","brisk"]` | 이미지 매칭 알고리즘 순서 | image_zh.md:5 |
| `THRESHOLD` | 0.7 | 이미지 매칭 임계치 [0,1] (1.1.6+ 전 인터페이스) | image_zh.md:59 |
| `THRESHOLD_STRICT` | — | 1.1.6 이전 `assert_exists` 전용 엄격 임계치 | image_zh.md:75 |
| `FIND_TIMEOUT` | 20초 | `touch/wait/swipe/assert_exists` 등 타임아웃 | image_zh.md:37 |
| `FIND_TIMEOUT_TMP` | 3초 | `exists/assert_not_exists` 타임아웃 | image_zh.md:38 |
| `--compress` | 10 | run 시 스냅샷 품질 | parser.py:31 |
| 녹화 `max_time` | 1800초 | 화면 녹화 최대 길이 | android.md:301 |
| 녹화 `bit_rate_level` | 1–5 | 녹화 해상도 (5 최고) | android.md:291 |

`auto_setup` 시그니처 (code_example.md:12):
```python
auto_setup(basedir=None, devices=None, logdir=None, project_root=None, compress=None)
```

---

## 11. AirtestIDE / Poco / AirLab 와의 관계 (문서 언급)

| 도구 | 관계 | 출처 |
|------|------|------|
| **AirtestIDE** | `.air` 케이스를 GUI 로 작성·실행·리포트하는 out-of-the-box 도구. `create → run → report` 워크플로우. `.air` 디렉토리(.py+png 템플릿)는 IDE 가 캡처 시 `record_pos`/`resolution` 메타를 자동 기록. CLI 는 IDE 없이 동일 `.air` 를 다양한 호스트/플랫폼에서 실행하기 위한 수단 | README.md:17, README.md:83, README_MORE.rst:28 |
| | macOS 의 `WebChrome` 가 `/Applications/AirtestIDE.app/Contents/Resources/selenium_plugin` 을 PATH 에 추가 (selenium 통합) | selenium_proxy.py:18 |
| | 리포트 푸터에 AirtestIDE/AirLab 링크·로고 | log_template.html:197 |
| **Poco** | UI 위젯 계층에 직접 접근하는 보완 프레임워크 (이미지 인식 한계 보완). 리포트 플러그인은 `plugins=["poco.utils.airtest.report"]` 로 로드 | README.md:19, code_example.md:366 |
| **AirLab** | NetEase 가 Airtest 위에 구축한 대규모 디바이스 팜. HTML 리포트로 실패 지점 추적 | README.md:15, README_MORE.rst:26 |
| **multi-device-runner** | 여러 디바이스 병렬 실행 샘플 (CLI 종료코드 20/-1 활용) | README.md:100 |

지원 플랫폼 (platforms.md): Android, Emulator, iOS, Windows, Cocos2dx(js/lua), Unity3D, Egret, WeChat Applet/Webview, NetEase engines. Airtest 는 전 플랫폼, Poco 는 Windows 미지원.

---

## 12. `using()` 로 다른 `.air` 임포트 (README_MORE / code_example)

```python
from airtest.core.api import using
ST.PROJECT_ROOT = "/User/test/project"   # 선택: 루트 지정 시 상대명만으로 검색
using("common.air")                       # sys.path + Template 검색 경로 컨텍스트 전환
from common import common_function
common_function()
```

- `exec_other_script` (구식, deprecated)를 대체. `using` 이 `Template` 검색 경로까지 관리 (README_MORE.rst:351, code_example.md:557).

---

## 13. 참고: 빈/비-텍스트 파일

다음은 SCOPE 내이나 내용이 없거나 바이너리라 별도 문서화 불필요:
- 빈 파일: `docs/wiki/code/image.md`, `docs/wiki/device/ios.md`, `docs/wiki/device/windows.md`, 각 `__init__.py`
- 바이너리/대용량 정적 에셋: `airtest/report/fonts/*`, `image/*`, `js/*min.js`(서드파티), `css/*`
- 중문 중복본: `code_example_zh.md`, `android_zh.md`, `README_zh.md` (각 영문판과 동일 내용)
