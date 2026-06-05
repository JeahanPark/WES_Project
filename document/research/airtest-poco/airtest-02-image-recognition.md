---
type: reference
source: Airtest
generated: subagent
---

# Airtest 이미지 인식 모듈 (aircv) 레퍼런스

Airtest의 `airtest/aircv/` 패키지는 OpenCV 기반 이미지 인식 엔진이다. 스크린샷(`im_source`, 큰 이미지) 안에서 템플릿 이미지(`im_search`, 작은 이미지)를 찾아 **클릭 좌표(중심점)·인식 영역(사각형)·신뢰도(confidence)** 를 돌려준다. 본 문서는 SCOPE의 13개 `.py` 파일을 전부 분석한 결과다.

> 용어 주의: 모듈 함수 인자명이 파일마다 다르다.
> - `aircv/*` 의 **함수형 API**(`find_template`, `find_sift`)는 `find_xxx(im_source, im_search, ...)` 순서 — **source 먼저**.
> - **클래스형 매처**(`TemplateMatching`, `KeypointMatching`, `MultiScaleTemplateMatching`)의 `__init__`은 `(im_search, im_source, ...)` 순서 — **search 먼저**.
> 이 차이는 실제 소스 그대로이며 의도된 것이 아니라 역사적 산물이다. 호출 시 반드시 시그니처를 확인할 것.

---

## 1. 전체 개요: 인식 전략 3계열

| 계열 | 클래스/함수 | 알고리즘 | 회전/스케일 불변 | RGB 기본 | 핵심 파일 |
|------|------------|----------|-----------|-----------|-----------|
| **Template Matching** | `TemplateMatching` / `find_template` | `cv2.matchTemplate(TM_CCOEFF_NORMED)` | ❌ (1:1 픽셀) | 클래스 True / 함수 False | `template_matching.py`, `template.py` |
| **Multi-Scale Template** | `MultiScaleTemplateMatching`, `MultiScaleTemplateMatchingPre` | 비율 루프 + matchTemplate | △ (스케일만) | True | `multiscale_template_matching.py` |
| **Keypoint Matching** | `KeypointMatching`(기반) + 7파생 | 특징점 검출 + knnMatch + Homography | ✅ (회전·스케일·원근) | True | `keypoint_base.py`, `keypoint_matching.py`, `keypoint_matching_contrib.py`, `sift.py` |

공통 결과 포맷(`utils.py:29` `generate_result`):

```python
{
    "result": (x, y),                 # 클릭용 중심 좌표 (middle_point)
    "rectangle": [(x,y), (x,y), ...], # 인식 영역 꼭짓점, 보통 좌상->좌하->우하->우상
    "confidence": 0.0~1.0,            # 신뢰도
    "time": 0.0,                      # print_run_time 데코레이터가 dict 결과에 주입
}
```

신뢰도가 `threshold` 미만이면 매처는 `None`을 반환한다.

### 호출 흐름 (상위 → aircv)

```
airtest/core/cv.py : Template._cv_match()
   └─ for method in ST.CVSTRATEGY:          # settings.py
        func = MATCHING_METHODS[method]      # cv.py:25
        func(im_search, screen, ...).find_best_result()   # aircv 매처
```

`Template._cv_match`(`cv.py:166`)이 `ST.CVSTRATEGY` 리스트를 **앞에서부터 순회**하며 매처를 시도, **첫 성공(`None`이 아닌 결과)에서 break**한다(`cv.py:183`).

---

## 2. MATCHING_METHODS / CVSTRATEGY 디스패치

### MATCHING_METHODS (`airtest/core/cv.py:25`)

```python
MATCHING_METHODS = {
    "tpl":    TemplateMatching,                 # template_matching.py
    "mstpl":  MultiScaleTemplateMatchingPre,    # multiscale_template_matching.py
    "gmstpl": MultiScaleTemplateMatching,       # multiscale_template_matching.py
    "kaze":   KAZEMatching,                      # keypoint_matching.py
    "brisk":  BRISKMatching,                     # keypoint_matching.py
    "akaze":  AKAZEMatching,                     # keypoint_matching.py
    "orb":    ORBMatching,                       # keypoint_matching.py
    "sift":   SIFTMatching,                       # keypoint_matching_contrib.py (contrib)
    "surf":   SURFMatching,                       # keypoint_matching_contrib.py (contrib)
    "brief":  BRIEFMatching,                      # keypoint_matching_contrib.py (contrib)
}
```

문자열 키 → 매처 클래스 매핑. `find_best_result()` 인터페이스를 모두 구현하므로 다형적으로 호출된다.

### CVSTRATEGY (`airtest/core/settings.py:15`)

```python
CVSTRATEGY = ["mstpl", "tpl", "sift", "brisk"]
if Version('3.4.2') < Version(cv2.__version__) < Version('4.4.0'):
    CVSTRATEGY = ["mstpl", "tpl", "brisk"]
```

| 상황 | 기본 시도 순서 |
|------|---------------|
| 일반 OpenCV | `mstpl` → `tpl` → `sift` → `brisk` |
| OpenCV 3.4.2 < v < 4.4.0 (SIFT 특허/모듈 이슈 구간) | `mstpl` → `tpl` → `brisk` (sift 제외) |

순서 의미:
1. `mstpl` (MultiScaleTemplateMatchingPre) — record_pos/resolution 정보가 있으면 가장 빠르고 정확.
2. `tpl` (TemplateMatching) — 동일 해상도·무회전이면 가장 빠른 정확 매칭.
3. `sift` — 회전·스케일·원근 변형까지 대응(contrib 필요). 없으면 `NoModuleError`로 skip.
4. `brisk` — contrib 불필요한 특징점 fallback.

`mstpl`/`gmstpl` 분기 시에는 `record_pos`, `resolution`, `scale_max`, `scale_step` 까지 함께 전달된다(`cv.py:178`). 그 외 매처에는 `threshold`, `rgb` 만 전달된다(`cv.py:182`).

contrib 매처(`sift`/`surf`/`brief`)가 환경에 없으면 `NoModuleError`가 발생하고 `_try_match`(`cv.py:187`)가 이를 잡아 `None`을 반환 → 다음 전략으로 넘어간다(폴백). 그 외 `aircv.BaseError`도 모두 잡아 `None` 처리한다.

### 관련 Settings 상수 (`settings.py`)

| 상수 | 기본값 | 의미 |
|------|--------|------|
| `THRESHOLD` | `0.7` | 매칭 성공 판정 신뢰도 하한 |
| `THRESHOLD_STRICT` | `None` | `assert_exists` 전용 엄격 임계치 |
| `KEYPOINT_MATCHING_PREDICTION` | `True` | keypoint 예측 영역 사용 여부 |
| `RESIZE_METHOD` | `cocos_min_strategy` | 해상도 적응 시 im_search 리사이즈 전략 |
| `FIND_TIMEOUT` | `20` | `loop_find` 기본 타임아웃(초) |
| `SNAPSHOT_QUALITY` | `10` | 스크린샷 압축 품질 |

> 주의: aircv 매처들의 `__init__` 기본 `threshold=0.8`이지만, 실제 런타임에선 `Template.threshold`(기본 `ST.THRESHOLD=0.7`)가 주입되어 0.8 기본값은 거의 쓰이지 않는다.

---

## 3. 패키지 진입점 — `__init__.py`

```python
from .aircv import *      # imread/imwrite/show/rotate/crop_image/mark_point/mask_image/get_resolution
from .error import *      # 모든 BaseError 하위 클래스
from .sift import find_sift
from .template import find_template, find_all_template
```

공개되는 것은 저수준 이미지 유틸 + 함수형 API(`find_sift`, `find_template`, `find_all_template`)다. 클래스형 매처들은 `__init__`에 노출되지 않고 `airtest/core/cv.py`가 직접 import한다.

---

## 4. 저수준 이미지 유틸 — `aircv.py`

OpenCV(BGR numpy 배열) 입출력·변환 도우미.

### `imread(filename, flatten=False)` (`aircv.py:12`)
```python
def imread(filename, flatten=False):
```
| param | type | default | 설명 |
|-------|------|---------|------|
| filename | str | — | 이미지 경로. 없으면 `FileNotExistError` |
| flatten | bool | False | True면 `IMREAD_GRAYSCALE`, False면 `IMREAD_COLOR` |

- 반환: cv2 이미지(numpy). 한글/유니코드 경로 대응 위해 PY3는 `np.fromfile + cv2.imdecode`, PY2는 인코딩 후 `cv2.imread`.
- 주의: 파일 미존재 시 즉시 raise.

### `imwrite(filename, img, quality=10, max_size=None)` (`aircv.py:29`)
cv2 이미지를 PIL로 변환(`cv2_2_pil`) 후 `compress_image`로 압축 저장.

### `show(img, title="show_img", test_flag=False)` (`aircv.py:37`) / `show_origin_size(...)` (`aircv.py:48`)
디버그용 윈도우 표시. `test_flag=True`면 `waitKey` 생략. **GUI 호출이므로 실행/테스트 시 주의.**

### `rotate(img, angle=90, clockwise=True)` (`aircv.py:56`)
90/180/270도 회전. 내부 `count_clock_rotate`(transpose+flip)를 `(4 - angle/90) % 4`(시계) 또는 `(angle/90) % 4`(반시계) 횟수만큼 반복.

### `crop_image(img, rect)` (`aircv.py:78`)
`rect=[x_min, y_min, x_max, y_max]`. 경계 클램프 후 `img[y_min:y_max, x_min:x_max]` 반환. list/tuple 길이 4 아니면 raise. `MultiScaleTemplateMatchingPre`·`cv.py`에서 예측 영역 잘라낼 때 사용.

### `mark_point(img, point, circle=False, color=100, radius=20)` (`aircv.py:101`)
디버그용 십자/원 마킹.

### `mask_image(img, mask, color=(255,255,255), linewidth=-1)` (`aircv.py:112`)
`mask=[x_min,y_min,x_max,y_max]` 영역을 흰색으로 칠함. `linewidth=-1`이면 채움.

### `get_resolution(img)` (`aircv.py:124`)
`(w, h)` 반환. cv.py가 해상도 계산에 사용.

---

## 5. 신뢰도 계산 — `cal_confidence.py`

"같은 크기의 두 이미지" 유사도를 계산하는 두 함수. 모든 매처가 인식 영역을 잘라 `im_search` 크기로 resize한 뒤 이 함수로 최종 confidence를 구한다.

### `cal_ccoeff_confidence(im_source, im_search)` (`cal_confidence.py:12`)
```python
def cal_ccoeff_confidence(im_source, im_search):
```
동작:
1. `im_source`에 10px 테두리 복제(`copyMakeBorder, BORDER_REPLICATE`) — 신뢰도 계산 영역 확장.
2. `im_source[0,0]=0`, `im_source[0,1]=255` — **값 범위 교란 주입**(미세 차이를 과도하게 증폭하는 것 방지).
3. 둘 다 그레이 변환(`img_mat_rgb_2_gray`).
4. `cv2.matchTemplate(..., TM_CCOEFF_NORMED)` → `minMaxLoc`의 `max_val` 반환.

- 반환: `max_val` (그레이스케일 상관계수, -1~1 이론, 실질 0~1).
- 용도: `rgb=False`(흑백 검증) 경로.

### `cal_rgb_confidence(img_src_rgb, img_sch_rgb)` (`cal_confidence.py:27`)
```python
def cal_rgb_confidence(img_src_rgb, img_sch_rgb):
```
동작:
1. 두 이미지 `np.clip(.., 10, 245)` — 극단값이 HSV 각도 계산 왜곡하는 것 완화.
2. `BGR2HSV` 변환 — 색상 영향 강화.
3. src에 10px 테두리 복제 + `[0,0]=0`/`[0,1]=255` 교란.
4. 채널 분리 후 **3채널 각각** `matchTemplate(TM_CCOEFF_NORMED)` → 각 `max_val` 수집.
5. **3채널 confidence 중 최솟값(`min`)** 반환 (가장 약한 채널 기준 = 보수적).

- 반환: `min(bgr_confidence)`.
- 용도: `rgb=True`(컬러 검증) 경로. 색상까지 일치해야 높은 점수.

| 항목 | cal_ccoeff | cal_rgb |
|------|-----------|---------|
| 색공간 | Gray | HSV(BGR→HSV) |
| 채널 | 1 | 3 (분리 후 min) |
| clip | 없음 | 10~245 |
| 엄격성 | 낮음(형태만) | 높음(색상 포함) |

---

## 6. 예외 계층 — `error.py`

모두 `BaseError(Exception)`(`error.py:10`)를 상속. `message` 속성 + `__repr__`. `cv.py._try_match`는 `aircv.BaseError`를 통째로 잡아 `None` 처리(→ 다음 전략으로 폴백).

| 예외 | 의미 | 발생 위치 |
|------|------|-----------|
| `FileNotExistError` | 이미지 파일 없음 | `aircv.imread` |
| `TemplateInputError` | 입력(해상도/크기) 부적합 | `utils.check_source_larger_than_search`, `MultiScaleTemplateMatchingPre` |
| `NoSIFTModuleError` | SIFT 모듈 없음(함수형) | `sift._init_sift` |
| `NoSiftMatchPointError` | SIFT 특징점 부족 | `sift._get_key_points` |
| `SiftResultCheckError` | SIFT 결과 비합리적 | `sift._target_error_check` |
| `HomographyError` | 호모그래피 행렬 산출 실패 | `_find_homography` (sift / keypoint_base) |
| `NoModuleError` | 매처용 OpenCV 모듈 없음(클래스형) | `BRIEF/SIFT/SURF.init_detector` |
| `NoMatchPointError` | 특징점 부족(클래스형) | `keypoint_base._get_key_points` |
| `MatchResultCheckError` | 결과 비합리적(클래스형) | `keypoint_base._target_error_check` |

> 함수형 `sift.py`는 `NoSIFT*`/`SiftResultCheckError`를, 클래스형 keypoint는 `NoModuleError`/`NoMatchPointError`/`MatchResultCheckError`를 쓴다(동일 개념 중복 정의).

---

## 7. 공용 유틸 — `utils.py`

### `print_run_time(func)` (`utils.py:15`)
메서드 데코레이터. 실행 시간 측정→DEBUG 로깅, **결과가 dict면 `ret["time"]`에 소요시간 주입**. 매처들의 `find_best_result`/`find_all_results`에 부착.

### `generate_result(middle_point, pypts, confi)` (`utils.py:29`)
`{"result","rectangle","confidence"}` dict 생성. 전 매처 공통 결과 포맷터.

### `check_image_valid(im_source, im_search)` (`utils.py:37`)
둘 다 not None & `.any()` 이면 True. keypoint/sift 진입 시 사전 검사.

### `check_source_larger_than_search(im_source, im_search)` (`utils.py:45`)
`im_search`가 `im_source`보다 크면 `TemplateInputError`. 템플릿/멀티스케일 진입 시 호출(matchTemplate은 search가 더 작아야 함).

### `img_mat_rgb_2_gray(img_mat)` (`utils.py:55`)
`BGR2GRAY`. 입력이 `np.ndarray`인지 assert. matchTemplate은 그레이만 처리하므로 필수.

### 변환/인코딩 도우미
`img_2_string`/`string_2_img`(`utils.py:64,69`, PNG ↔ bytes), `pil_2_cv2`/`cv2_2_pil`(`utils.py:80,90`, RGB↔BGR), `compress_image`(`utils.py:96`, PIL 저장+썸네일 압축, quality 1~99 범위 강제).

---

## 8. Template Matching — `template_matching.py` + `template.py`

회전·스케일 변형 없는 1:1 픽셀 매칭. 가장 빠르고 안정적이나 변형에 취약.

### 8.1 클래스 `TemplateMatching` (`template_matching.py:21`)

```python
class TemplateMatching(object):
    METHOD_NAME = "Template"
    MAX_RESULT_COUNT = 10

    def __init__(self, im_search, im_source, threshold=0.8, rgb=True):
```
| param | type | default | 설명 |
|-------|------|---------|------|
| im_search | ndarray | — | 찾을 템플릿(작은 이미지) |
| im_source | ndarray | — | 대상 스크린샷(큰 이미지) |
| threshold | float | 0.8 | 신뢰도 하한 |
| rgb | bool | True | True면 RGB(HSV) 컬러 검증 |

#### `find_best_result()` (`template_matching.py:68`, `@print_run_time`)
1. `check_source_larger_than_search`.
2. `_get_template_result_matrix()` → `cv2.matchTemplate(i_gray, s_gray, TM_CCOEFF_NORMED)`.
3. `minMaxLoc` → 최댓값 위치 `max_loc`.
4. `_get_confidence_from_matrix`: `rgb=True`면 해당 영역 crop 후 `cal_rgb_confidence`, 아니면 `max_val` 그대로.
5. `_get_target_rectangle`로 중심점·사각형 산출.
- 반환: confidence ≥ threshold면 result dict, 아니면 `None`.

#### `find_all_results()` (`template_matching.py:35`, `@print_run_time`)
결과 행렬에서 최댓값을 뽑고 → 그 영역을 `cv2.rectangle(..., (0,0,0), -1)`로 **검게 마스킹** → 반복. `confidence < threshold` 또는 `len(result) > MAX_RESULT_COUNT(10)` 이면 종료. 다중 인스턴스 탐지용.

#### 내부 메서드
- `_get_confidence_from_matrix(max_loc, max_val, w, h)` (`:88`)
- `_get_template_result_matrix()` (`:100`) — 그레이 변환 + `TM_CCOEFF_NORMED`.
- `_get_target_rectangle(left_top_pos, w, h)` (`:106`) — 좌상→좌하→우하→우상 꼭짓점 + 중심.

> 주의: `find_best_result` docstring에 `"基于kaze"`(kaze 기반)라는 **복붙 오류 주석**이 남아있다. 실제 로직은 템플릿 매칭이다.

### 8.2 함수형 API — `template.py`

`__init__.py`로 공개. 클래스 버전과 거의 동일하나 **인자 순서·rgb 기본값이 다름**.

#### `find_template(im_source, im_search, threshold=0.8, rgb=False)` (`template.py:19`)
- 인자 순서: **source 먼저**. `rgb` 기본 **False**(클래스는 True).
- 동작: 클래스 `find_best_result`와 동일 파이프라인의 함수 버전.

#### `find_all_template(im_source, im_search, threshold=0.8, rgb=False, max_count=10)` (`template.py:37`)
- `max_count`(기본 10)로 결과 개수 상한. 마스킹 루프 동일.

| 비교 | `TemplateMatching`(클래스) | `find_template`(함수) |
|------|---------------------------|----------------------|
| 인자 순서 | `(im_search, im_source)` | `(im_source, im_search)` |
| rgb 기본 | True | False |
| 다중 결과 상한 | `MAX_RESULT_COUNT=10` | `max_count=10` |
| 노출 | cv.py 내부 | `aircv.__init__` 공개 |

---

## 9. Multi-Scale Template Matching — `multiscale_template_matching.py`

템플릿을 여러 비율로 리사이즈하며 matchTemplate을 반복 → **스케일(해상도) 차이 대응**. 회전엔 여전히 취약.

조절 파라미터(모듈 docstring):
| 파라미터 | 기본 | 의미 |
|----------|------|------|
| threshold | 0.8 | 필터 임계치 |
| rgb | True | 컬러 3채널 검증 |
| scale_max | 800 | 소스 긴 변 최대 크기(작은 UI 대응 시 ↑) |
| scale_step | 0.005 | 비율 탐색 스텝(작은 UI 대응 시 ↓) |

### 9.1 `MultiScaleTemplateMatching` (`multiscale_template_matching.py:27`)

```python
class MultiScaleTemplateMatching(object):
    METHOD_NAME = "MSTemplate"

    def __init__(self, im_search, im_source, threshold=0.8, rgb=True,
                 record_pos=None, resolution=(), scale_max=800, scale_step=0.005):
```
| param | type | default | 설명 |
|-------|------|---------|------|
| im_search / im_source | ndarray | — | 템플릿 / 스크린샷 |
| threshold | float | 0.8 | 신뢰도 하한 |
| rgb | bool | True | 컬러 검증 |
| record_pos | tuple\|None | None | 녹화 시 상대 클릭 위치(예측용) |
| resolution | tuple | () | 녹화 시 화면 해상도 |
| scale_max | int | 800 | 소스 최대 변 길이 |
| scale_step | float | 0.005 | 비율 스텝 |

#### `find_best_result()` (`:46`, `@print_run_time`)
1. `check_source_larger_than_search`.
2. 그레이 변환 후 `multi_scale_search(..., ratio_min=0.01, ratio_max=0.99, src_max=scale_max, step=scale_step, threshold)`.
3. 결과 `max_loc, w, h`로 중심·사각형 산출.
- 반환: confidence ≥ threshold면 dict, 아니면 None.

#### `multi_scale_search(...)` (`:120`)
```python
def multi_scale_search(self, org_src, org_templ, templ_min=10, src_max=800,
                       ratio_min=0.01, ratio_max=0.99, step=0.01,
                       threshold=0.8, time_out=3.0):
```
핵심 루프:
- `r`을 `ratio_min`→`ratio_max`까지 `step`씩 증가.
- 각 `r`마다 `_resize_by_ratio`로 src(최대 src_max로 축소)와 templ(비율 r로 스케일) 리사이즈.
- `min(templ.shape) > templ_min(10)` 일 때만 matchTemplate(`TM_CCOEFF_NORMED`) 수행, 교란값 `[0,0]=0`/`[0,1]=255` 주입.
- 최고 `max_val` 추적(`max_info`).
- **조기 종료**: `time_cost > time_out` 이고 `max_val ≥ threshold` 면 그 시점 confidence 재계산 후 충족 시 즉시 반환.
- 종료 후 `max_info` 기준으로 원본 스케일 복원(`_org_size`) → `_get_confidence_from_matrix`로 최종 confidence.
- 반환: `(confidence, omax_loc, ow, oh, max_r)`. 매칭 없으면 `(0, (0,0), 0, 0, 0)`.

#### 헬퍼
- `_resize_by_ratio(src, templ, ratio=1.0, templ_min=10, src_max=800)` (`:97`, staticmethod) — src를 src_max로 제한, templ을 src 대비 긴 변 기준 비율로 스케일. 반환 `(src, templ, tr, sr)`.
- `_org_size(max_loc, w, h, tr, sr)` (`:113`, staticmethod) — 스케일된 좌표/크기를 `sr`로 나눠 원본 좌표 복원.
- `_get_confidence_from_matrix(max_loc, w, h)` (`:65`) — crop+resize 후 rgb면 `cal_rgb_confidence`, 아니면 `cal_ccoeff_confidence`.
- `_get_target_rectangle(left_top_pos, w, h)` (`:80`).
- `find_all_results()` → `NotImplementedError`.

### 9.2 `MultiScaleTemplateMatchingPre` (`multiscale_template_matching.py:155`)

`MultiScaleTemplateMatching` 상속. **녹화 시 위치/해상도 예측을 이용해 탐색 범위를 좁혀 빠르게** 매칭. `MATCHING_METHODS["mstpl"]`로 CVSTRATEGY 최우선 매처.

```python
class MultiScaleTemplateMatchingPre(MultiScaleTemplateMatching):
    METHOD_NAME = "MSTemplatePre"
    DEVIATION = 150
```

#### `find_best_result()` (`:161`, override, `@print_run_time`)
- `self.resolution == ()` 이면 즉시 `None`(예측 정보 없으면 동작 안 함 → cv.py가 다음 전략 `tpl`로 폴백).
- resolution이 search보다 작으면 `TemplateInputError`.
- `record_pos`가 있으면 `_get_area_scope`로 **예측 영역**을 잘라(`aircv.crop_image`) 그 안에서만 탐색, 매칭 후 `area` 오프셋 보정.
- `_get_ratio_scope`로 비율 상·하한(`r_min, r_max`)을 좁힌 뒤 `multi_scale_search(..., time_out=1.0)`.

#### 예측 헬퍼
- `_get_ratio_scope(src, templ, resolution)` (`:193`) — 새/구 해상도 비와 소·대 이미지 비로 비율 범위 산출. 하한은 `scale_step`, 상한은 `0.99`로 클램프.
- `get_predict_point(record_pos, screen_resolution)` (`:205`) — 상대 위치 → 화면 절대 좌표 예측. (`target_y = delta_y*_w + _h*0.5` — _w 사용은 소스 그대로)
- `_get_area_scope(src, templ, record_pos, resolution)` (`:213`) — 예측 클릭점 중심으로 `DEVIATION(150)` 이상의 반경 영역 산출.

---

## 10. Keypoint Matching 기반 — `keypoint_base.py`

특징점(keypoint) 검출 → 디스크립터 knnMatch → 비율 필터 → Homography로 영역 추정. **회전·스케일·원근 변형에 강함**. 모든 keypoint 매처의 공통 골격.

### 10.1 `KeypointMatching` 기반 클래스 (`keypoint_base.py:19`)

```python
class KeypointMatching(object):
    METHOD_NAME = "KAZE"
    FILTER_RATIO = 0.59      # Lowe ratio test 비율(권장 0.4~0.6)
    ONE_POINT_CONFI = 0.5    # 특징점 1쌍일 때 부여 신뢰도

    def __init__(self, im_search, im_source, threshold=0.8, rgb=True):
```
| 상수/param | 값/기본 | 설명 |
|-----------|---------|------|
| `METHOD_NAME` | "KAZE" | 로그용 이름(파생이 오버라이드) |
| `FILTER_RATIO` | 0.59 | `m.distance < FILTER_RATIO * n.distance` 통과 기준 |
| `ONE_POINT_CONFI` | 0.5 | (base에선 미사용, sift 함수형에서 사용) |
| threshold | 0.8 | 신뢰도 하한 |
| rgb | True | 컬러 검증 |

#### `find_best_result()` (`:46`, `@print_run_time`) — 핵심 파이프라인
1. `check_image_valid` 실패 시 None.
2. `_get_key_points()` → `(kp_sch, kp_src, good)`.
3. `good` 개수별 분기:
   - 0 또는 1 → `None` (영역 추정 불가).
   - 2 → `_handle_two_good_points`.
   - 3 → `_handle_three_good_points`.
   - ≥4 → `_many_good_pts` (Homography).
4. `_target_error_check(w_h_range)` — 비합리적 영역이면 raise.
5. 추정 영역 crop → `im_search` 크기로 resize → `_cal_confidence`.
6. confidence ≥ threshold면 dict, 아니면 None.

#### `_get_key_points()` (`:133`)
- `init_detector()` 호출(파생마다 다름) → `get_keypoints_and_descriptors`로 sch/src 특징점·디스크립터.
- 양쪽 특징점 < 2면 `NoMatchPointError`.
- `match_keypoints`(knnMatch k=2) → **Lowe ratio test**(`FILTER_RATIO=0.59`)로 `good` 선별.
- **중복 제거**: src 상 같은 좌표로 매핑된 점 제거(1:多 허용, 多:1 불허).

#### `_cal_confidence(resize_img)` (`:107`)
- rgb면 `cal_rgb_confidence`, 아니면 `cal_ccoeff_confidence`.
- **보정**: `confidence = (1 + confidence) / 2` (특징점 매칭은 confidence를 "물타기"해 후하게).

#### 영역 추정 헬퍼
- `_handle_two_good_points` (`:164`) / `_handle_three_good_points` (`:173`) → `_get_origin_result_with_two_points`(`:233`): 두 점 간 x/y 스케일 비로 영역 확장. sch/src가 동일 x축·y축이면 None.
- `_many_good_pts` (`:185`): `findHomography(RANSAC, 5.0)`로 M 산출 → mask로 정밀 점 재선별 → M 재계산 → 네 꼭짓점 `perspectiveTransform` → 경계 클램프.
- `_find_homography(sch_pts, src_pts)` (`:268`): `cv2.findHomography(RANSAC, 5.0)`. 실패/마스크 None이면 `HomographyError`.
- `_target_error_check(w_h_range)` (`:282`): 영역 폭·높이 < 5px → raise; sch 대비 0.2배 미만 또는 5배 초과 → `MatchResultCheckError`.

#### 검출기 인터페이스 (파생이 오버라이드)
- `init_detector()` (`:117`) — 기반: `cv2.KAZE_create()` + `BFMatcher(NORM_L1)`.
- `get_keypoints_and_descriptors(image)` (`:123`) — `detectAndCompute`.
- `match_keypoints(des_sch, des_src)` (`:128`) — `knnMatch(k=2)`.

#### `show_match_image()` (`:88`)
디버그용: sch/src 나란히 두고 매칭선 그림. 내부에서 `find_best_result()` 호출.

### 10.2 상속 구조

```
KeypointMatching (keypoint_base.py)         ← KAZE 기본 구현
├─ KAZEMatching   (keypoint_matching.py)    : pass (그대로 KAZE)
├─ BRISKMatching  (keypoint_matching.py)    : BRISK_create + BFMatcher(NORM_HAMMING)
├─ AKAZEMatching  (keypoint_matching.py)    : AKAZE_create + BFMatcher(NORM_L1)
├─ ORBMatching    (keypoint_matching.py)    : ORB_create   + BFMatcher(NORM_HAMMING)
├─ BRIEFMatching  (keypoint_matching_contrib.py) : STAR detector + BRIEF extractor + BFMatcher(NORM_L1)  [contrib]
├─ SIFTMatching   (keypoint_matching_contrib.py) : SIFT_create(edgeThreshold=10) + FlannBasedMatcher     [contrib]
└─ SURFMatching   (keypoint_matching_contrib.py) : SURF_create(Hessian=400) + FlannBasedMatcher          [contrib]
```

모든 파생은 `init_detector()`(필요 시 `get_keypoints_and_descriptors`/`match_keypoints`)만 오버라이드하고 영역 추정·confidence·검증 로직은 기반 클래스를 그대로 쓴다.

---

## 11. Keypoint 파생 — contrib 불필요 (`keypoint_matching.py`)

`opencv-contrib` 없이 동작.

### `KAZEMatching` (`keypoint_matching.py:14`)
`pass` — 기반 그대로(KAZE). `METHOD_NAME="KAZE"`. KAZE는 비선형 스케일 공간 기반, 정밀하나 느림.

### `BRISKMatching` (`:20`)
```python
METHOD_NAME = "BRISK"
def init_detector(self):
    self.detector = cv2.BRISK_create()
    self.matcher = cv2.BFMatcher(cv2.NORM_HAMMING)
```
바이너리 디스크립터 → HAMMING 거리. 빠르고 contrib 불필요해 CVSTRATEGY fallback으로 채택됨.

### `AKAZEMatching` (`:32`)
`AKAZE_create()` + `BFMatcher(NORM_L1)`. KAZE 가속판.

### `ORBMatching` (`:44`)
`ORB_create()` + `BFMatcher(NORM_HAMMING)`. 가장 빠른 특징점 매처 중 하나, 정확도는 낮은 편.

---

## 12. Keypoint 파생 — contrib 필요 (`keypoint_matching_contrib.py`)

`opencv-contrib` 모듈 필요. 없으면 `NoModuleError` → cv.py가 폴백.

### `check_cv_version_is_new()` (`keypoint_matching_contrib.py:15`)
OpenCV 버전이 `"3."`/`"4."`로 시작하면 True. API 분기에 사용.

### `BRIEFMatching` (`:23`)
```python
METHOD_NAME = "BRIEF"
```
- `init_detector` (`:28`): 신버전은 `cv2.xfeatures2d.StarDetector_create()`(검출) + `BriefDescriptorExtractor_create()`(기술), 구버전은 `FeatureDetector_create("STAR")`/`DescriptorExtractor_create("BRIEF")`. 실패 시 `NoModuleError`. 매처 `BFMatcher(NORM_L1)`.
- `get_keypoints_and_descriptors` (`:49`): STAR로 검출 → BRIEF로 기술(2단계).
- BRIEF 자체는 디스크립터일 뿐, 검출기로 CenSurE(STAR) 사용.

### `SIFTMatching` (`:63`)
```python
METHOD_NAME = "SIFT"
FLANN_INDEX_KDTREE = 0
```
- `init_detector` (`:71`): `cv2.SIFT_create(edgeThreshold=10)` 시도 → 실패 시 `cv2.xfeatures2d.SIFT_create(edgeThreshold=10)` → 실패 시 `NoModuleError`. 구버전은 `cv2.SIFT(...)`. 매처는 `FlannBasedMatcher({algorithm:0, trees:5}, checks=50)`.
- 회전·스케일 불변성이 가장 우수. CVSTRATEGY에서 template 다음 주력.

### `SURFMatching` (`:101`)
```python
METHOD_NAME = "SURF"
UPRIGHT = 0              # 0: 방향 불변 검출 / 1: 미검출
HESSIAN_THRESHOLD = 400  # Hessian 임계치
FLANN_INDEX_KDTREE = 0
```
- `init_detector` (`:113`): `cv2.xfeatures2d.SURF_create(HESSIAN_THRESHOLD, upright=UPRIGHT)`(신) / `cv2.SURF(...)`(구). 실패 시 `NoModuleError`. FLANN 매처.
- SIFT보다 빠르나 특허 이슈로 대부분 빌드에서 비활성.

---

## 13. SIFT 함수형 API — `sift.py`

`__init__.py`로 공개되는 **함수형** SIFT(클래스형 `SIFTMatching`과 별개·중복 구현). 모듈 전역에 FLANN/상수 정의.

```python
FLANN_INDEX_KDTREE = 0
FLANN = cv2.FlannBasedMatcher({'algorithm': FLANN_INDEX_KDTREE, 'trees': 5}, dict(checks=50))
FILTER_RATIO = 0.59
ONE_POINT_CONFI = 0.5
```

### `find_sift(im_source, im_search, threshold=0.8, rgb=True, good_ratio=FILTER_RATIO)` (`sift.py:20`)
| param | type | default | 설명 |
|-------|------|---------|------|
| im_source | ndarray | — | 스크린샷(**source 먼저**) |
| im_search | ndarray | — | 템플릿 |
| threshold | float | 0.8 | 신뢰도 하한 |
| rgb | bool | True | 컬러 검증 |
| good_ratio | float | 0.59 | Lowe ratio |

동작(클래스형과 거의 동일하나 `good` 개수 분기에서 **1쌍도 처리**):
- 0 → None.
- 1 → `_handle_one_good_points`: 중심점만, confidence=`ONE_POINT_CONFI(0.5)`. (`ONE_POINT_CONFI >= threshold` 일 때만 반환)
- 2 → `_handle_two_good_points`, 동일 축이면 confidence=0.5인 dict 반환.
- 3 → `_handle_three_good_points`.
- ≥4 → `_many_good_pts` (Homography).
- 최종 `_cal_sift_confidence`: rgb면 `cal_rgb_confidence`, 아니면 `cal_ccoeff_confidence`, 보정 `(1+confidence)/2`.
- `print(...)`로 직접 콘솔 출력(로거 아님).

### `_init_sift()` (`sift.py:80`)
OpenCV 3.x면 `cv2.xfeatures2d.SIFT_create(edgeThreshold=10)`(실패 시 `NoSIFTModuleError`), 그 외 `cv2.SIFT(edgeThreshold=10)`.

### `mask_sift` / `find_all_sift` (`sift.py:68,74`)
둘 다 `NotImplementedError`. 다중 목표 클러스터링 미구현.

### 내부 (클래스형과 평행)
`_get_key_points`(`:96`), `_handle_one/two/three_good_points`, `_two_good_points`(`:212`), `_many_good_pts`(`:163`), `_find_homography`(`:250`, 실패 시 `HomographyError`), `_target_error_check`(`:265`, `SiftResultCheckError`), `_cal_sift_confidence`(`:277`).

> 클래스형 `KeypointMatching.find_best_result`는 `good`이 1이면 **무조건 None**이지만, 함수형 `find_sift`는 1쌍도 `ONE_POINT_CONFI`로 처리한다 — 두 구현의 차이점.

---

## 14. 화면 녹화 — `screen_recorder.py`

이미지 인식과 무관한 **테스트 과정 영상 녹화** 모듈. FFmpeg로 프레임을 비디오로 인코딩.

### `RECORDER_ORI` (`screen_recorder.py:18`)
```python
{"PORTRAIT": 1, "LANDSCAPE": 2, "ROTATION": 0}
```

### 함수
- `resize_by_max(img, max_size=800)` (`:24`) — 긴 변 max_size로 축소(None이면 흑백 빈 프레임).
- `get_max_size(max_size)` (`:34`) — 정수 변환, ≤0이면 None.

### `FfmpegVidWriter` (`:45`)
```python
def __init__(self, outfile, width, height, fps=10, orientation=0, timetag=True):
```
| param | default | 설명 |
|-------|---------|------|
| outfile | — | 출력 비디오 경로 |
| width/height | — | 프레임 크기 |
| fps | 10 | 프레임레이트 |
| orientation | 0 | 1 세로 / 2 가로 / 0 정사각 중앙 |
| timetag | True | 타임스탬프(UTC offset 포함) 오버레이 |

동작:
- orientation별로 width/height 조정 후 **32의 배수로 패딩**(`- (n%32) + 32`, libx264 요건).
- FFmpeg 미설치 시 `ffmpeg_setter.add_paths()`로 PATH 보정 시도, 실패 시 안내 로그 후 raise.
- `ffmpeg.input(pipe, rawvideo, rgb24).output(pix_fmt=yuv420p, vcodec=libx264, crf=25, preset=veryfast).run_async(pipe_stdin=True)`.
- `process_frame(frame)` (`:105`): BGR→RGB 뒤집기, orientation 리사이즈, 중앙 배치(cache_frame), timetag면 `cv2.putText` 시각 표기, 복사본 반환.
- `write(frame)` (`:124`): uint8 보장 후 stdin write.
- `close()` (`:129`): writer close + process wait/terminate.

### `ScreenRecorder` (`:142`)
```python
def __init__(self, outfile, get_frame_func, fps=10, snapshot_sleep=0.001, orientation=0, timetag=True):
```
- `get_frame_func`: 프레임 공급 콜백(디바이스 스냅샷).
- **2-스레드 구조**:
  - `get_frame_loop` (`:202`): 별도 스레드로 계속 캡처 → `frame_queue`(deque maxlen=100)에 `(time, frame)` push. 캡처 실패 시 경고 텍스트 박은 빈 프레임. `0.5/fps` 간격.
  - `write_frame_loop` (`:232`): 큐에서 꺼내 시간축 맞춰 FFmpeg에 write(프레임 부족 구간은 `last_frame` 반복). FFmpeg 죽으면(`process.poll()`) / BrokenPipe 시 루프 종료.
- 제어: `start()`(`:179`, 데몬 스레드 2개 기동), `stop()`(`:192`, 플래그+join+writer.close), `is_running()`, `is_stop()`(`:172`, stop_flag 또는 stop_time 도달), `stop_time` property(`:161`, 양의 int면 `now+max_time`).

---

## 15. 매처 비교 — 언제 무엇을 쓰나

| 매처 | 변형 대응 | 속도 | contrib | 거리 | 검출기 | 장점 | 단점 / 용도 |
|------|----------|------|---------|------|--------|------|------------|
| **tpl** (Template) | 없음(1:1) | ★★★★★ | ✕ | — | — | 동일 해상도·무회전에서 가장 빠르고 정확 | 스케일/회전 변하면 실패. 같은 기기 고정 UI |
| **mstpl** (MSTemplatePre) | 스케일 | ★★★★ | ✕ | — | — | 녹화 해상도/위치 정보로 좁혀 빠름·정확 | resolution 없으면 무동작. 녹화 기반 스크립트 1순위 |
| **gmstpl** (MSTemplate) | 스케일 | ★★★ | ✕ | — | — | 예측 정보 없이도 전 비율 스캔 | 느림(전구간 루프). 해상도 다른 기기 |
| **kaze** | 회전·스케일·원근 | ★★ | ✕ | L1 | KAZE | 정밀, contrib 불필요 | 느림 |
| **akaze** | 회전·스케일·원근 | ★★★ | ✕ | L1 | AKAZE | KAZE보다 빠름 | 특징점 적은 이미지 약함 |
| **brisk** | 회전·스케일·원근 | ★★★ | ✕ | HAMMING | BRISK | contrib 불필요, 균형. SIFT 불가 시 fallback | 텍스처 적으면 약함 |
| **orb** | 회전·스케일 | ★★★★ | ✕ | HAMMING | ORB | 가장 빠른 특징점 | 정확도 낮음 |
| **sift** | 회전·스케일·원근 | ★★ | ✔ | FLANN | SIFT | 가장 강건한 불변성. CVSTRATEGY 주력 | contrib 필요·느림 |
| **surf** | 회전·스케일·원근 | ★★★ | ✔ | FLANN | SURF | SIFT보다 빠름 | 특허 이슈로 대부분 비활성 |
| **brief** | 부분(STAR 검출) | ★★★ | ✔ | L1 | STAR+BRIEF | 가볍고 빠른 기술자 | 회전 불변 약함, contrib 필요 |

### 선택 가이드(요약)
- **같은 기기·고정 UI**: `tpl` 또는 `mstpl`.
- **여러 해상도 지원**: `mstpl`(녹화 정보 있음) → `gmstpl`/`sift`.
- **회전/원근 변형 UI**: `sift`(contrib 있을 때) → `brisk`/`akaze`(없을 때).
- **contrib 빌드 불가 환경**: `tpl`/`mstpl`/`kaze`/`brisk`/`akaze`/`orb`만 사용.

---

## 16. 모듈 간 연결 요약

```
core/cv.py (Template, _cv_match, MATCHING_METHODS)
    │  ST.CVSTRATEGY (settings.py) 순서대로 find_best_result() 호출
    ▼
aircv/template_matching.py  TemplateMatching ─┐
aircv/multiscale_template_matching.py  MSTpl/Pre ─┤
aircv/keypoint_matching.py  KAZE/BRISK/AKAZE/ORB ─┼─ 공통 의존 ─┐
aircv/keypoint_matching_contrib.py  SIFT/SURF/BRIEF ┘            │
aircv/keypoint_base.py  KeypointMatching (위 7개의 부모)         │
aircv/sift.py  find_sift (독립 함수형, __init__ 공개)           │
                                                                 ▼
        aircv/cal_confidence.py  cal_ccoeff_confidence / cal_rgb_confidence
        aircv/utils.py  generate_result / check_* / img_mat_rgb_2_gray / print_run_time
        aircv/error.py  BaseError 계층
        aircv/aircv.py  imread/imwrite/crop_image/... (저수준 I/O)
```

- 모든 매처 → `utils.generate_result`로 결과 포맷 통일.
- 모든 매처 confidence → `cal_confidence.*`로 수렴(keypoint/sift는 `(1+c)/2` 보정).
- `error.BaseError` → `cv.py._try_match`가 일괄 캐치해 폴백.
- `aircv.py.crop_image` → `mstpl`(Pre)·`cv.py` 예측 영역 잘라내기.
