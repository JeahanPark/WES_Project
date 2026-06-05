---
type: reference
source: Poco (AirtestProject/Poco)
generated: subagent
---

# Poco Core Framework API 레퍼런스

> 대상: `poco/__init__.py`, `poco/pocofw.py` (+ `.pyi`), `poco/proxy.py` (+ `.pyi`), `poco/exceptions.py`, `poco/gesture.py`, `poco/acceleration.py`, `poco/agent.py` (+ `.pyi`), `poco/freezeui/` 전체.
> 본 문서는 Poco UI 자동화 프레임워크의 **코어 계층** — Poco 진입점, UIObjectProxy 셀렉터/액션, agent 인터페이스 집합, 예외, 제스처 빌더, 가속 믹스인, frozen(immutable) hierarchy — 을 다룬다.

---

## 0. 모듈 개요 및 호출 관계

| 모듈 | 핵심 심볼 | 역할 |
|------|-----------|------|
| `poco/__init__.py` | `Poco` (re-export) | 패키지 진입점. `from .pocofw import Poco` 단 한 줄. |
| `poco/pocofw.py` | `Poco` | 프레임워크 핵심 클래스. 셀렉터 진입점(`__call__`), 전역 좌표 액션, freeze/snapshot/dump, 폴링/안정화 헬퍼. |
| `poco/proxy.py` | `UIObjectProxy` | 셀렉터 표현식을 담는 UI 프록시. 모든 UI 단위 액션(click/swipe/attr/...)·트리 탐색(child/parent/...). |
| `poco/agent.py` | `PocoAgent` | hierarchy/input/screen/command 4개 인터페이스의 집합체. 디바이스 통신 추상화. |
| `poco/exceptions.py` | `PocoException` 외 4종 | Poco 전용 예외 계층. |
| `poco/gesture.py` | `PendingGestureAction` | 체이닝으로 복합 제스처(MotionTrack)를 빌드. |
| `poco/acceleration.py` | `PocoAccelerationMixin` | 고수준 헬퍼(`dismiss`). `Poco`의 부모 믹스인. |
| `poco/freezeui/hierarchy.py` | `FrozenUIDumper`, `FrozenUIHierarchy`, `Node` | 고정(immutable) 로컬 hierarchy 구현. |
| `poco/freezeui/utils.py` | `create_immutable_hierarchy`, `create_immutable_dumper` | dict 데이터로부터 frozen hierarchy 팩토리. |

### 호출/상속 그래프

```
poco/__init__.py
   └─ Poco  (pocofw.py)
          ├─ 상속: PocoAccelerationMixin (acceleration.py)
          ├─ 보유: self._agent : PocoAgent (agent.py)
          │           ├─ hierarchy : HierarchyInterface  (← FrozenUIHierarchy 가능)
          │           ├─ input     : InputInterface
          │           ├─ screen    : ScreenInterface
          │           └─ command   : CommandInterface
          ├─ __call__() → UIObjectProxy (proxy.py)        # 셀렉터 진입점
          ├─ start_gesture() → PendingGestureAction (gesture.py)
          ├─ apply_motion_tracks() → agent.input.applyMotionEvents()
          └─ freeze() → FrozenPoco(Poco)                   # create_immutable_hierarchy 사용

UIObjectProxy (proxy.py)
   ├─ self.poco : Poco
   ├─ 액션(click/swipe/long_click...) → self.poco.click/swipe/... → agent.input.*
   ├─ attr/setattr/exists → agent.hierarchy.getAttr/setAttr/select
   ├─ child/children/offspring/sibling/parent/__getitem__/__iter__ → 새 UIObjectProxy 생성
   └─ start_gesture() → PendingGestureAction(self.poco, self)
```

핵심 흐름: 사용자는 `Poco` 인스턴스를 호출(`poco('name', type='Button')`)하여 **지연 평가**되는 `UIObjectProxy`를 얻는다. 프록시에 액션을 호출하는 순간 `agent.hierarchy.select()`로 실제 노드를 조회하고, `agent.input.*`로 입력을 주입한다.

---

## 1. `poco/__init__.py`

```python
from .pocofw import Poco
```

- 패키지 공개 심볼은 `Poco` 단 하나. `from poco import Poco`가 정식 진입점.

---

## 2. `poco/pocofw.py` — `class Poco`

```python
class Poco(PocoAccelerationMixin):
```

- 상속: `PocoAccelerationMixin` (`acceleration.py:12`). 따라서 `Poco` 인스턴스는 `dismiss()`도 가진다.
- `__author__ = 'lxn3032'`.

### 2.1 생성자

```python
def __init__(self, agent, **options):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `agent` | `PocoAgent` | (필수) | 디바이스 통신용 agent. `agent.py:19` 참조. |
| `options['pre_action_wait_for_appearance']` | float | `6` | 액션 전 타겟 출현 대기(초). 초과 시 `PocoNoSuchNodeException`. `proxy.py:26`의 `wait` 데코레이터가 사용. |
| `options['action_interval']` | float | `0.8` | 액션 후 UI 안정화 대기(초). 내부 `self._post_action_interval`. `wait_stable()`이 사용. |
| `options['poll_interval']` | float | `1.44` | 폴링 간 최소 간격(초). `sleep_for_polling_interval()`이 사용. |
| `options['reevaluate_volatile_attributes']` | bool | `False` | volatile 속성 재조회 시 타겟 프록시 재선택 여부. hrpc 연결 드라이버는 항상 원격 재평가하므로 `False` 권장. `StdPoco`용 옵션. `proxy.py:66` `volatile_attribute` 데코레이터가 사용. |
| `options['touch_down_duration']` | float | (없음) | 클릭 시 터치 다운 단계 지속(초). 제공 시 `self.agent.input.setTouchDownDuration()` 호출. 미지원 구현이면 경고. |

핵심 동작:
- 옵션을 내부 필드(`self._pre_action_wait_for_appearance`, `self._post_action_interval`, `self._poll_interval`, `self._reevaluate_volatile_attributes`)에 저장.
- `touch_down_duration` 제공 시 `float()` 캐스팅 실패하면 `ValueError`.
- `self._pre_action_callbacks = [self.__class__.on_pre_action]`, `self._post_action_callbacks = [self.__class__.on_post_action]` 으로 콜백 리스트 초기화.
- `self._agent.on_bind_driver(self)` 호출 — agent가 자신을 구동하는 driver(`Poco`)를 바인딩. `agent.py:69`.

`.pyi` 기본값 명세(`pocofw.pyi:14`): `_pre_action_wait_for_appearance=6`, `_post_action_interval=0.8`, `_poll_interval=1.44`, `_reevaluate_volatile_attributes=False`.

### 2.2 `__call__` — 셀렉터 진입점

```python
def __call__(self, name=None, **kw):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `name` | str | `None` | 선택할 UI 요소의 name 속성. |
| `**kw` | kwargs | — | 임의 속성 조건. `xx=value`(완전 일치) 또는 `xxMatches=pattern`(정규식). `xx`/`xxMatches`를 동일 속성에 동시 사용 불가. `_`로 시작하는 키는 sdk 내부 전용이라 금지. |

반환: `UIObjectProxy` (`proxy.py:77`).

핵심 동작:
- `name`도 없고 `kw`도 비면 와일드카드 셀렉터 경고(`warnings.warn`) — 성능 문제 유발 가능.
- `return UIObjectProxy(self, name, **kw)`. **즉시 조회하지 않음** — 셀렉터 표현식만 프록시에 저장(지연 평가).

예: `poco('close', type='Button')`, `poco(textMatches='^close.*$')`.

주의점: 보이지 않는(invisible) UI는 `visible=False`를 줘도 결과에서 스킵된다.

### 2.3 대기 헬퍼

#### `wait_for_any`

```python
def wait_for_any(self, objects, timeout=120):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `objects` | Iterable[UIObjectProxy] | (필수) | 대기할 프록시 집합. |
| `timeout` | float | `120` | 타임아웃(초). |

반환: 가장 먼저 출현한 `UIObjectProxy`.
동작: 각 객체에 `exists()` 폴링, `sleep_for_polling_interval()`로 간격 두며 반복.
예외: 전부 미출현 시 `PocoTargetTimeout('any to appear', objects)`.

#### `wait_for_all`

```python
def wait_for_all(self, objects, timeout=120):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `objects` | Iterable[UIObjectProxy] | (필수) | 모두 출현 대기. |
| `timeout` | float | `120` | 타임아웃(초). |

반환: 없음(`NoReturn`). 동작: 전부 `exists()` True가 될 때까지 폴링.
예외: 타임아웃 시 `PocoTargetTimeout('all to appear', objects)`.

### 2.4 `freeze` — hierarchy 스냅샷 동결

```python
def freeze(this):
```

반환: 현재 인스턴스의 복제본인 새 `Poco`(내부 `FrozenPoco`). hierarchy가 **고정·불변**이라 `dump`가 매우 빠름.

핵심 동작:
- `this.agent.hierarchy.dump()`로 현재 hierarchy dict를 1회 수집.
- `create_immutable_hierarchy(hierarchy_dict)` (`freezeui/utils.py:9`)로 frozen hierarchy 생성.
- `PocoAgent(hierarchy, this.agent.input, this.agent.screen)`로 새 agent 구성(input/screen은 원본 공유).
- frozen 인스턴스 옵션 강제: `action_interval=0.01`, `pre_action_wait_for_appearance=0` (대기 불필요).
- `FrozenPoco`는 컨텍스트 매니저(`__enter__`/`__exit__`) 지원, `__getattr__`로 원본 `this`에 위임.

예:
```python
poco = Poco(...)
frozen_poco = poco.freeze()
hierarchy_dict = frozen_poco.agent.hierarchy.dump()  # 캐시된 데이터 반환
```

주의점: frozen 인스턴스는 화면 변화에 둔감하다. UI가 바뀌면 새로 `freeze()`해야 한다.

### 2.5 안정화/폴링 헬퍼

```python
def wait_stable(self):            # time.sleep(self._post_action_interval)  → 기본 0.8s
def sleep_for_polling_interval(self):  # time.sleep(self._poll_interval)    → 기본 1.44s
```

- 둘 다 수동 호출 불필요(액션 내부에서 자동 호출).

### 2.6 `agent` 프로퍼티

```python
@property
def agent(self):  # → PocoAgent
```

- 읽기 전용. `self._agent` 반환.

### 2.7 전역 좌표 액션 (NormalizedCoordinate, 0~1)

> 모든 좌표는 화면 비율 0~1. `[0.5,0.5]`=중앙, `[0,0]`=좌상단.

#### `click`

```python
def click(self, pos):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `pos` | 2-list/2-tuple(float,float) | 0~1 범위 좌표. |

동작: 범위 검사 후 `agent.input.click(x, y)` → `wait_stable()`. 반환은 agent 구현 의존.
예외: `pos`가 0~1 밖이면 `InvalidOperationException`.

#### `rclick`

```python
def rclick(self, pos):  # raise NotImplementedError
```
- 베이스에서 미구현. 드라이버가 오버라이드해야 함.

#### `double_click`

```python
def double_click(self, pos):
```
동작: `agent.input.double_click(x, y)` → `wait_stable()`. (베이스에는 범위 검사 없음.)

#### `swipe`

```python
def swipe(self, p1, p2=None, direction=None, duration=2.0):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `p1` | 2-list/2-tuple | (필수) | 시작점(0~1). |
| `p2` | 2-list/2-tuple | `None` | 끝점. `direction`과 둘 중 하나 필수. |
| `direction` | 2-list/2-tuple | `None` | 방향 벡터. 지정 시 `p2 = p1 + direction`. |
| `duration` | float | `2.0` | 동작 시간(초). |

동작: `duration` float 캐스팅(실패 시 `ValueError`). `p1` 범위 검사. `direction` 우선, 없으면 `p2`, 둘 다 없으면 `TypeError('Swipe end not set.')`. `agent.input.swipe(...)`.
예외: 시작점 화면 밖 → `InvalidOperationException`.

#### `long_click`

```python
def long_click(self, pos, duration=2.0):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `pos` | 2-list/2-tuple | (필수) | 좌표(0~1). |
| `duration` | float | `2.0` | 누름 지속(초). |

동작: `duration` 캐스팅·`pos` 범위 검사 후 `agent.input.longClick(x, y, duration)`.
예외: 범위 밖 → `InvalidOperationException`.

#### `scroll`

```python
def scroll(self, direction='vertical', percent=0.6, duration=2.0):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `direction` | str | `'vertical'` | `'vertical'` 또는 `'horizontal'`. |
| `percent` | float | `0.6` | 전체 화면 높이/너비 대비 스크롤 거리 비율. |
| `duration` | float | `2.0` | 동작 시간. |

동작: 화면 중앙(`[0.5,0.5]`)에서 시작, `percent/2`만큼 보정해 `swipe(start, direction=...)`로 위→아래에서 아래→위로 스크롤. vertical이면 `direction=[0,-percent]`, horizontal이면 `[-percent,0]`.
예외: `direction`이 두 값 아니면 `ValueError`.

#### `pinch`

```python
def pinch(self, direction='in', percent=0.6, duration=2.0, dead_zone=0.1):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `direction` | str | `'in'` | `'in'`(오므림)/`'out'`(벌림). |
| `percent` | float | `0.6` | 화면 기준 오므림/벌림 범위. |
| `duration` | float | `2.0` | 동작 시간. |
| `dead_zone` | float | `0.1` | 핀치 안쪽 원 반경. `percent` 이하여야 함. |

동작: `make_pinching(direction, [0.5,0.5], [1,1], percent, dead_zone, duration)`로 트랙 생성, `speed=(percent-dead_zone)/2/duration`, `apply_motion_tracks(tracks, accuracy=speed*0.03)`.
예외: `direction` 불일치 → `ValueError`; `dead_zone >= percent` → `ValueError`.

#### `pan`

```python
def pan(self, direction, duration=2.0):  # raise NotImplementedError
```

### 2.8 제스처/모션

#### `start_gesture`

```python
def start_gesture(self, pos):  # → PendingGestureAction
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `pos` | 2-list/2-tuple | 정규화 좌표 시작점. |

반환: `PendingGestureAction(self, pos)` (`gesture.py:8`). `.to()`·`.hold()`·`.up()` 체이닝.
예: `poco.start_gesture([0.5,0.5]).to([0.6,0.6]).hold(1).to([0.5,0.5]).up()`.

#### `apply_motion_tracks`

```python
def apply_motion_tracks(self, tracks, accuracy=0.004):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `tracks` | list[MotionTrack] | (필수) | 모션 트랙 리스트. 비면 `ValueError`. |
| `accuracy` | float | `0.004` | 정규화 좌표계 모션 스텝당 정밀도. |

동작: `MotionTrackBatch(tracks).discretize(accuracy)` → `agent.input.applyMotionEvents(...)`. `pinch`·`PendingGestureAction.up`이 이를 호출.

### 2.9 화면/덤프

#### `snapshot`

```python
def snapshot(self, width=720):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `width` | int | `720` | 기대 너비(px). 실제 크기는 agent 구현 의존. |

반환: 2-tuple `(screen_shot: str/bytes(base64), format: str)`. `agent.screen.getScreen(width)`.

#### `get_screen_size`

```python
def get_screen_size(self):  # → (float, float)
```
반환: `agent.screen.getPortSize()` — 물리 해상도(px).

#### `dump`

```python
def dump(self):
```
반환: 현재 UI 트리(`agent.hierarchy.dump()`). 출력 포맷은 agent 구현 의존(docstring상 base64).

#### `command`

```python
def command(self, cmd, type_=None):
```
동작: `agent.command.command(cmd, type_)` 위임.

### 2.10 액션 콜백 시스템

| 메서드 | 시그니처 | 설명 |
|--------|----------|------|
| `on_pre_action` | `(self, action, ui, args)` | 기본 no-op. 서브클래스 오버라이드용. |
| `on_post_action` | `(self, action, ui, args)` | 기본 no-op. |
| `add_pre_action_callback` | `(self, cb)` | 액션 전 콜백 등록. `self._pre_action_callbacks.append(cb)`. |
| `add_post_action_callback` | `(self, cb)` | 액션 후 콜백 등록. |
| `pre_action` | `(self, action, ui, args)` | 등록된 pre 콜백 전부 호출. 예외는 `warnings.warn`으로 흡수. |
| `post_action` | `(self, action, ui, args)` | 등록된 post 콜백 전부 호출. 예외 흡수. |

콜백 인자 규약: `cb(self, action, proxy, args)` — `action`(str 태그), `proxy`(관련 `UIObjectProxy` 또는 None), `args`(액션별 인자 tuple). `pre_action`/`post_action`은 `proxy.py`의 액션들(`click` 등)이 호출한다.

### 2.11 렌더 해상도

```python
def use_render_resolution(self, use=True, resolution=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `use` | bool | `True` | 렌더 해상도 사용 여부. |
| `resolution` | tuple | `None` | 세로 모드 렌더 해상도 `(offset_x, offset_y, offset_width, offset_height)`. 예: `(0,10,1080,1820)`. |

동작: `self._agent.input.use_render_resolution`·`render_resolution` 설정.

---

## 3. `poco/proxy.py` — `class UIObjectProxy`

```python
class UIObjectProxy(object):
```

타겟 디바이스의 UI 요소를 나타내는 프록시. 수동 생성 불필요(`poco(...)`로 얻음). 셀렉터 표현식 기반 **지연 평가**.

### 3.1 모듈 레벨 데코레이터/헬퍼

| 심볼 | 역할 |
|------|------|
| `wait(func)` (`proxy.py:19`) | 액션 래퍼. `PocoNoSuchNodeException` 발생 시 `wait_for_appearance(timeout=poco._pre_action_wait_for_appearance)` 후 1회 재시도. 그래도 `PocoTargetTimeout`이면 원본 예외 re-raise. `click/rclick/double_click/long_click/swipe`에 적용. |
| `refresh_when(err_type)` (`proxy.py:34`) | 지정 예외 발생 시 `self._do_query(multiple=False, refresh=True)`로 재조회 후 재시도. `attr`/`setattr`에 `PocoTargetRemovedException`로 적용. |
| `ReevaluationContext` (`proxy.py:47`) | volatile 속성 재평가를 같은 배치 내 1회만 실행하기 위한 컨텍스트. `__reevaluation_context__` 플래그. |
| `volatile_attribute(func)` (`proxy.py:66`) | `ReevaluationContext` 진입 후, 배치 첫 호출이고 `poco._reevaluate_volatile_attributes`가 True면 `proxy._evaluated=False`로 강제 재선택. `get_position/exists/get_size/get_bounds`에 적용. |

### 3.2 생성자 및 내부 상태

```python
def __init__(self, poco, name=None, **attrs):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `poco` | Poco | (필수) | 소속 poco 인스턴스. |
| `name` | str | `None` | name 속성 쿼리 조건. |
| `**attrs` | kwargs | — | name 외 쿼리 표현식. |

내부 필드(`proxy.pyi:11`):

| 필드 | 타입 | 의미 |
|------|------|------|
| `self.query` | `(str, Tuple)` | `build_query(name, **attrs)` 결과 쿼리 튜플. |
| `self.poco` | Poco | 소속 인스턴스. |
| `self._query_multiple` | bool | 직전 선택이 다중 선택이었는지(성능 플래그). |
| `self._evaluated` | bool | 노드 조회 완료 여부. |
| `self._nodes` | None/list[AbstractNode] | 원격 노드 프록시(단일 또는 리스트). |
| `self._nodes_proxy_is_list` | bool | `_nodes`가 리스트형 프록시인지. |
| `self._sorted_children` | None/… | `__getitem__`용 정렬된 자식 캐시. |
| `self._focus` | None/(float,float) | focus point(터치/스와이프 국소 상대 위치). |

### 3.3 트리 탐색 (모두 새 `UIObjectProxy` 반환, 지연 평가)

| 메서드 | 시그니처 | 쿼리 연산자 | 설명 |
|--------|----------|-------------|------|
| `child` | `child(self, name=None, **attrs)` | `('/', (self.query, sub_query))` | 직계 자식. |
| `children` | `children(self)` | `child()` 위임 | 모든 직계 자식. |
| `offspring` | `offspring(self, name=None, **attrs)` | `('>', (self.query, sub_query))` | 직계 포함 모든 후손. |
| `sibling` | `sibling(self, name=None, **attrs)` | `('-', (self.query, sub_query))` | 형제. |
| `parent` | `parent(self)` | `('^', (self.query, sub_query))` | 첫 요소의 직계 부모. **실험적**, 일부 드라이버만 지원. |

각 메서드는 `obj = UIObjectProxy(self.poco)` 생성 후 `obj.query`만 설정해 반환한다.

### 3.4 인덱싱/반복

#### `__getitem__`

```python
def __getitem__(self, item):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `item` | int | 인덱스. |

반환: n번째 요소를 감싼 새 `UIObjectProxy`.
동작: 미선택이면 `_do_query(multiple=True, refresh=True)`. 각 노드를 프록시로 만들고 `get_position()`을 구해 캐시(`_sorted_children`). 정렬 키 `(pos[1], pos[0])` — **L2R U2D**(좌→우, 위→아래). 정렬 후 인덱스 반환.
주의: PocoAgent 구현에 따라 성능 이슈 가능.

#### `__len__`

```python
def __len__(self):
```
반환: 선택된 요소 수. `_nodes_proxy_is_list`가 False면 1. 미선택이면 `_do_query(multiple=True, refresh=True)`(없으면 0). 매치 없으면 0.

#### `__iter__`

```python
def __iter__(self):
```
동작: 모든 노드를 프록시로 만들어 `(pos[1], pos[0])` 기준 정렬 후 yield. 순서는 `__getitem__`과 동일.
예외: 반복 중 hierarchy 변경으로 없어진 요소 접근 시 `PocoTargetRemovedException`.

### 3.5 액션 (focus/anchor/center 개념)

> **focus**: UI 요소 좌상단 기준 0~1 오프셋. `'anchor'`(요소 앵커포인트) / `'center'`(바운딩박스 중앙) / `[x,y]` 가능. 기본 우선순위: `focus 인자 → self._focus → 'center'`.

#### `click` (`@wait`)

```python
def click(self, focus=None, sleep_interval=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `focus` | 2-tuple/2-list/str | `None` | 클릭 오프셋. `'anchor'`/`'center'` 또는 0~1 좌표. |
| `sleep_interval` | float | `None` | 액션 후 대기(초). None이면 `wait_stable()`(기본 0.8s). |

동작: `focus = focus or self._focus or 'center'` → `get_position(focus)` → `poco.pre_action('click',...)` → `poco.click(pos)` → 대기 → `poco.post_action('click',...)`.
예외: 요소 부재 시 `PocoNoSuchNodeException`(단, `@wait`가 1회 출현 대기 후 재시도).

#### `rclick` (`@wait`)

```python
def rclick(self, focus=None, sleep_interval=None):
```
`click`과 동일 구조, `poco.rclick(pos)` 호출(베이스 `Poco.rclick`은 `NotImplementedError`).

#### `double_click` (`@wait`)

```python
def double_click(self, focus=None, sleep_interval=None):
```
`click`과 동일 구조, `poco.double_click(pos)` 호출.

#### `long_click` (`@wait`)

```python
def long_click(self, duration=2.0):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `duration` | float | `2.0` | 전체 동작 지속(초). |

동작: `duration` 캐스팅(실패 `ValueError`) → `get_position(self._focus or 'center')` → `poco.long_click(pos, duration)`. 반환은 agent 구현 의존.

#### `swipe` (`@wait`)

```python
def swipe(self, direction, focus=None, duration=0.5):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `direction` | 2-tuple/2-list/str | (필수) | 정규화 방향 또는 `'up'`/`'down'`/`'left'`/`'right'`. up=`[0,-0.1]`, down=`[0,0.1]`, left=`[-0.1,0]`, right=`[0.1,0]`. |
| `focus` | 2-tuple/2-list/str | `None` | 시작 focus. |
| `duration` | float | `0.5` | 동작 시간. |

동작: `_direction_vector_of(direction)`로 벡터화 → `get_position(focus)`를 origin으로 `poco.swipe(origin, direction=dir_vec, duration=...)`.
예외: 요소 부재 → `PocoNoSuchNodeException`; 잘못된 direction 타입 → `TypeError`(`_direction_vector_of`).

#### `drag_to`

```python
def drag_to(self, target, duration=2.0):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `target` | UIObjectProxy 또는 2-list/2-tuple | (필수) | 끝점(프록시면 그 위치). |
| `duration` | float | `2.0` | 동작 시간. |

동작: target이 list/tuple이면 그 좌표, 아니면 `target.get_position()`. `origin_pos=self.get_position()`. 방향 `dir_=target-origin` 계산 후 `self.swipe(dir_, duration=...)`.

#### `scroll`

```python
def scroll(self, direction='vertical', percent=0.6, duration=2.0):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `direction` | str | `'vertical'` | `'vertical'`/`'horizontal'`. |
| `percent` | float | `0.6` | 선택 요소 높이/너비 대비 거리 비율. |
| `duration` | float | `2.0` | 동작 시간. |

동작: `focus1 = self._focus or [0.5,0.5]`, `focus2`는 복제. `half_distance=percent/2`로 focus1/focus2를 축따라 ±. `self.focus(focus1).drag_to(self.focus(focus2), duration=...)`.
예외: 잘못된 direction → `ValueError`.

#### `pinch`

```python
def pinch(self, direction='in', percent=0.6, duration=2.0, dead_zone=0.1):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `direction` | str | `'in'` | `'in'`/`'out'`. |
| `percent` | float | `0.6` | UI 바운드 기준 범위. |
| `duration` | float | `2.0` | 동작 시간. |
| `dead_zone` | float | `0.1` | 안쪽 반경. `percent` 이하. |

동작: `w,h=get_size()`, `x,y=get_position()`. `make_pinching(direction, [x,y], [w,h], percent, dead_zone, duration)`. `speed=sqrt(w*h)*(percent-dead_zone)/2/duration`. `poco.apply_motion_tracks(tracks, accuracy=speed*0.03)`.
예외: direction 불일치/`dead_zone>=percent` → `ValueError`; 요소 부재 → `PocoNoSuchNodeException`.

#### `pan`

```python
def pan(self, direction, duration=2.0):  # raise NotImplementedError
```

### 3.6 제스처/focus

#### `start_gesture`

```python
def start_gesture(self):  # → PendingGestureAction
```
반환: `PendingGestureAction(self.poco, self)`. 항상 **현재 UI 객체 위치**에서 터치 다운 시작.
예: `ui1.start_gesture().hold(1).to(ui2).hold(1).up()`.

#### `focus`

```python
def focus(self, f):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `f` | 2-tuple/2-list/str | focus point. `'center'`/`'anchor'` 또는 정규화 좌표. |

반환: `copy.copy(self)` 후 `_focus=f`로 설정한 **새 프록시**(프록시는 불변이라 복제 반환).

### 3.7 좌표/위치

#### `get_position` (`@volatile_attribute`)

```python
def get_position(self, focus=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `focus` | str/(float,float) | `None` | focus point. None이면 `self._focus or 'center'`. |

반환: NormalizedCoordinate `(x, y)`.
동작:
- `'anchor'`: `attr('pos')`를 그대로.
- `'center'`: `pos + size*(0.5 - anchorPoint)` (fx=fy=0.5).
- `[fx,fy]`: `pos + size*(focus - anchorPoint)`.
예외: 지원 안 되는 focus 타입 → `TypeError`.

`anchorPoint`: 요소의 앵커(0~1). center/임의 focus 계산에 `(focus - anchorPoint)` 형태로 들어가 앵커 기준 보정을 한다.

#### `_direction_vector_of`

```python
def _direction_vector_of(self, dir_):
```
`'up'/'down'/'left'/'right'`를 `[0,∓0.1]`/`[∓0.1,0]`로, list/tuple은 그대로 반환. 그 외 `TypeError`.

### 3.8 대기

#### `wait`

```python
def wait(self, timeout=3):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `timeout` | float | `3` | 최대 대기(초). |

반환: `self`. 동작: `exists()` False인 동안 `sleep_for_polling_interval()` 폴링, 타임아웃이면 그냥 break(예외 없음).

#### `wait_for_appearance`

```python
def wait_for_appearance(self, timeout=120):
```
동작: 출현까지 폴링. 타임아웃 시 `PocoTargetTimeout('appearance', self)`.

#### `wait_for_disappearance`

```python
def wait_for_disappearance(self, timeout=120):
```
동작: 사라질 때까지 폴링. 매 루프 `self.invalidate()`로 노드 상태 강제 재조회(존재→소멸 후 캐시로 인해 `exists()`가 영원히 True가 되는 버그 방지). 타임아웃 시 `PocoTargetTimeout('disappearance', self)`.

### 3.9 속성 접근

#### `attr` (`@refresh_when(PocoTargetRemovedException)`)

```python
def attr(self, name):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `name` | str | 속성명. `visible`/`text`/`type`/`pos`/`size`/`name` 등 + sdk 커스텀. |

반환: 속성값(JSON 직렬화 가능). 없거나 None이면 None. 텍스트형은 py2/py3 모두 `str`(py2에서 utf-8 인코딩).
동작: `_do_query(multiple=False)`로 첫 매치만 조회 → `agent.hierarchy.getAttr(nodes, name)`.
예외: 요소 부재 → `PocoNoSuchNodeException`. `PocoTargetRemovedException`(=NodeHasBeenRemoved)은 데코레이터가 자동 재조회로 흡수.

속성 의미:
| name | 의미 |
|------|------|
| `visible` | 사용자에게 보이는지 |
| `text` | 문자열 값 |
| `type` | 원격 런타임 타입명 |
| `pos` | 위치 |
| `size` | 화면 대비 0~1 `[width,height]` |
| `name` | 요소 이름 |

#### `setattr` (`@refresh_when(PocoTargetRemovedException)`)

```python
def setattr(self, name, val):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `name` | str | 속성명. |
| `val` | Any | 새 값. |

동작: `_do_query(multiple=False)` → `agent.hierarchy.setAttr(nodes, name, val)`.
예외: 불변/존재하지 않는 속성 변경 시 `UnableToSetAttributeException`를 잡아 `InvalidOperationException`로 변환.

#### `exists` (`@volatile_attribute`)

```python
def exists(self):
```
반환: bool. 동작: `attr('visible')` 반환. `PocoTargetRemovedException`/`PocoNoSuchNodeException`이면 False.

#### 편의 getter/setter

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `get_text` | `get_text(self)` | `attr('text')`. 없으면 None. |
| `set_text` | `set_text(self, text)` | `setattr('text', text)`. 불가 시 `InvalidOperationException`. |
| `get_name` | `get_name(self)` | `attr('name')`. |
| `get_size` (`@volatile_attribute`) | `get_size(self)` | `attr('size')` → 0~1 `[w,h]`. |
| `get_bounds` (`@volatile_attribute`) | `get_bounds(self)` | `size`와 `get_position([0,0])`(좌상단)으로 `[top, right, bottom, left]` 계산. |

`get_bounds` 반환 순서: `[t, r, b, l]` = `[top_left.y, top_left.x+w, top_left.y+h, top_left.x]`.

### 3.10 노드/무효화/내부 조회

| 멤버 | 시그니처 | 설명 |
|------|----------|------|
| `nodes` (property) | `nodes` | `_do_query()` 결과(원격 노드들). |
| `invalidate` / `refresh` | `invalidate(self)` | `_evaluated=False`, `_nodes=None`으로 재조회 강제. `refresh`는 별칭. |
| `_do_query` | `_do_query(self, multiple=True, refresh=False)` | 미평가/refresh면 `agent.hierarchy.select(self.query, multiple)`. 빈 결과면 `invalidate()` 후 `PocoNoSuchNodeException`. 성공 시 `_evaluated=True`, `_query_multiple=multiple`. |

`__str__`/`__repr__`: `'UIObjectProxy of "{query_expr(self.query)}"'`. py2는 utf-8 인코딩, `__unicode__` 별도 제공.

---

## 4. `poco/agent.py` — `class PocoAgent`

```python
class PocoAgent(object):
```

poco가 타겟 디바이스와 통신하기 위한 agent. **4개 인터페이스의 집합체**.

### 4.1 생성자

```python
def __init__(self, hierarchy, input, screen, command=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `hierarchy` | HierarchyInterface | (필수) | UI 트리 접근(dump/getAttr/setAttr/select). None이면 `HierarchyInterface()` 기본. |
| `input` | InputInterface | (필수) | 시뮬레이션 입력 주입(click/swipe/longClick/applyMotionEvents 등). None이면 기본. |
| `screen` | ScreenInterface | (필수) | 화면 접근(getScreen/getPortSize). None이면 기본. |
| `command` | CommandInterface | `None` | 임의 통신(선택). None이면 `CommandInterface()` 기본. |

내부 헬퍼 `_assign(val, default_val)`: `val`이 None이면 `default_val`, 아니면 `val`.
`self._driver = None`(바인딩 전).

### 4.2 인터페이스 4종 (집합 대상)

| 인터페이스 | 핵심 메서드(문서상) | 호출처 |
|------------|---------------------|--------|
| `HierarchyInterface` | `dump()`, `getAttr(nodes,name)`, `setAttr(nodes,name,val)`, `select(query,multiple)` | `Poco.dump`, `UIObjectProxy.attr/setattr/_do_query` |
| `InputInterface` | `click`, `double_click`, `swipe`, `longClick`, `applyMotionEvents`, `setTouchDownDuration`, `use_render_resolution`/`render_resolution` | `Poco.click/swipe/long_click/double_click/apply_motion_tracks` |
| `ScreenInterface` | `getScreen(width)`, `getPortSize()` | `Poco.snapshot/get_screen_size` |
| `CommandInterface` | `command(cmd, type_)` | `Poco.command` |

### 4.3 메서드

| 메서드 | 시그니처 | 설명 |
|--------|----------|------|
| `get_sdk_version` | `get_sdk_version(self)` | 원격 sdk 버전 문자열("0.0.0"). 각 구현이 오버라이드. 기본 None. |
| `rpc_reconnect` | `rpc_reconnect(self)` | `self.rpc.close()` 후 `connect()`. |
| `rpc` (property) | `rpc` | agent의 rpc 인터페이스. 기본은 `NotImplementedError`('명시적 rpc 없음'). |
| `on_bind_driver` | `on_bind_driver(self, driver)` | `self._driver=driver`. `input`이 `AirtestInput`이면 `input.add_preaction_cb(driver)`. `Poco.__init__`이 호출. |
| `driver` (property) | `driver` | 바인딩된 driver(Poco) 반환. 미바인딩이면 `AttributeError`(super().on_bind_driver 호출 누락 안내). |

---

## 5. `poco/exceptions.py` — 예외 계층

```
Exception
 └─ PocoException                  # 베이스, py3 호환, self.message 보관
     ├─ InvalidOperationException
     ├─ PocoTargetTimeout
     ├─ PocoNoSuchNodeException
     └─ PocoTargetRemovedException
```

모듈 헬퍼 `to_text(val)`: `six.text_type`이 아니면 `val.decode('utf-8')`.

### `PocoException`

```python
def __init__(self, message=None):
```
- `self.message` 저장. `__str__`/`__repr__`은 py2(utf-8 encode)/py3(bytes decode) 분기 처리.

### `InvalidOperationException`
- 무의미·불가능한 조작 시(예: 화면 밖 클릭). `Poco.click/swipe/long_click`, `UIObjectProxy.setattr/set_text`에서 발생.

### `PocoTargetTimeout`

```python
def __init__(self, action, poco_obj_proxy):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `action` | str | 대기 동작명(`'appearance'`, `'disappearance'`, `'any to appear'`, `'all to appear'`, `'dismiss'`). |
| `poco_obj_proxy` | UIObjectProxy/list | 대상. |

- message: `'Waiting timeout for {action} of "{repr}"'`. 조건 미충족 타임아웃 시.

### `PocoNoSuchNodeException`

```python
def __init__(self, objproxy):
```
- message: `'Cannot find any visible node by query {repr}'`. 쿼리 매치 실패 시. `_do_query`가 발생.

### `PocoTargetRemovedException`

```python
def __init__(self, action, objproxy):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `action` | str | 동작명. |
| `objproxy` | UIObjectProxy | 대상. |

- message: `'Remote ui object "{obj}" has been removed from hierarchy during {action}.'`. 선택 중 hierarchy 변경/이미 회수된 요소 접근 시. 대개 코드 버그(예: 선택 후 sleep 동안 화면 변경). `attr`/`setattr`의 `@refresh_when`이 자동 재조회로 흡수. `__iter__`에서 노출될 수 있음.

---

## 6. `poco/gesture.py` — `class PendingGestureAction`

```python
class PendingGestureAction(object):
    def __init__(self, pocoobj, uiproxy_or_pos):
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `pocoobj` | Poco | 모션 적용 대상 인스턴스. |
| `uiproxy_or_pos` | UIObjectProxy 또는 list/tuple | 시작점. list/tuple이면 좌표 그대로, 아니면 `uiproxy.get_position()`으로 시작. |

내부: `self.track = MotionTrack()` 생성 후 `track.start(...)`.

| 메서드 | 시그니처 | 동작 | 반환 |
|--------|----------|------|------|
| `hold` | `hold(self, t)` | `track.hold(t)` — t초 정지. | self(체이닝) |
| `to` | `to(self, pos)` | list/tuple이면 `track.move(pos)`, UI 프록시면 `track.move(pos.get_position())`. | self |
| `up` | `up(self)` | `pocoobj.apply_motion_tracks([self.track])` — 터치 업·실제 실행. | None |

체이닝 예: `poco.start_gesture([0.5,0.5]).to([0.6,0.6]).hold(1).to([0.5,0.5]).up()`.
연결: `Poco.start_gesture`·`UIObjectProxy.start_gesture`가 생성, `up()`이 `Poco.apply_motion_tracks` 호출.

---

## 7. `poco/acceleration.py` — `class PocoAccelerationMixin`

```python
class PocoAccelerationMixin(object):
```

고수준 헬퍼 믹스인. `Poco`의 부모. **새로운 상태(state)를 메서드에 도입하지 말 것**(믹스인 규약).

### `dismiss`

```python
def dismiss(self, targets, exit_when=None, sleep_interval=0.5, appearance_timeout=20, timeout=120):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `targets` | list[UIObjectProxy] | (필수) | 자동으로 닫을(클릭) 대상들. |
| `exit_when` | callable | `None` | 종료 조건 함수. None이면 targets가 비면 자동 종료. |
| `sleep_interval` | float | `0.5` | 각 액션 간 간격(초). |
| `appearance_timeout` | float | `20` | 대상 출현 대기(초). 초과 시 경고 후 조용히 반환. |
| `timeout` | float | `120` | 전체 dismiss 타임아웃(초). |

동작:
- 먼저 `wait_for_any(targets, timeout=appearance_timeout)`. 타임아웃이면 경고 후 return.
- 루프: 각 target이 `exists()`면 `for n in t:`로 반복하며 `n.click(sleep_interval=sleep_interval)`. 개별 클릭/반복 중 예외(노드 제거 등)는 무시.
- 매 루프 `sleep(sleep_interval)`, `exit_when()` 평가. `no_target`(아무것도 못 누름) 또는 `should_exit`면 return.
- `timeout` 초과 시 `PocoTargetTimeout('dismiss', targets)`.

용도: 팝업/광고 등 반복 출현 요소를 자동 닫기.

---

## 8. `poco/freezeui/` — Frozen(immutable) Hierarchy

frozen hierarchy는 원격에서 1회 덤프한 UI 트리 dict를 **로컬 불변 노드**로 감싸 매우 빠르게 조회하게 한다. `Poco.freeze()`의 기반.

### 8.1 `freezeui/__init__.py`
- 빈 파일.

### 8.2 `freezeui/hierarchy.py`

#### `class FrozenUIDumper(AbstractDumper)`

`AbstractDumper`의 부분 구현. 원격 앱을 크롤하지 않고 고정 데이터로 일반 dumper처럼 동작하게 돕는 헬퍼.

| 메서드 | 시그니처 | 설명 |
|--------|----------|------|
| `dumpHierarchy` | `dumpHierarchy(self, onlyVisibleNode=True)` | `NotImplementedError`(서브클래스가 고정 dict 반환하도록 구현). |
| `getRoot` | `getRoot(self)` | `Node(self.dumpHierarchy())` 생성 후 `_linkParent(root)`로 부모 링크. 매번 최신 데이터로 새 노드 생성. |
| `_linkParent` | `_linkParent(self, root)` | 자식들에 `setParent(root)`를 재귀적으로 설정. |

#### `class FrozenUIHierarchy(HierarchyInterface)`

dumper 기반의 로컬 hierarchy 구현. 고정 데이터·불변 요소라 "frozen". UI 변경에 둔감 — 변경 시 `select`를 명시 호출해 재크롤 필요.

```python
def __init__(self, dumper, attributor=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `dumper` | FrozenUIDumper | (필수) | hierarchy 데이터 공급자. |
| `attributor` | Attributor | `None` | 속성 접근자. None이면 `Attributor()` 기본. |

내부: `self.selector = Selector(self.dumper)`.

| 메서드 | 시그니처 | 동작 |
|--------|----------|------|
| `dump` | `dump(self)` | `self.dumper.dumpHierarchy()`. |
| `getAttr` | `getAttr(self, nodes, name)` | `self.attributor.getAttr(...)`. |
| `setAttr` | `setAttr(self, nodes, name, value)` | `self.attributor.setAttr(...)`. |
| `select` | `select(self, query, multiple=False)` | `self.selector.select(query, multiple)`. |

→ 이 4개 메서드가 `PocoAgent.hierarchy` 슬롯을 통해 `UIObjectProxy`의 `attr/setattr/_do_query`와 `Poco.dump`에 연결된다.

#### `class Node(AbstractNode)`

로컬 dict를 감싼 불변 노드.

```python
def __init__(self, node):  # node: dict
```

| 메서드 | 동작 |
|--------|------|
| `setParent(p)` | `node['__parent__'] = p`. |
| `getParent()` | `node.get('__parent__')`. |
| `getChildren()` | `node.get('children')` 각 child를 `Node(child)`로 yield(제너레이터). |
| `getAttr(attrName)` | `node['payload'].get(attrName)`. |
| `setAttr(attrName, val)` | 항상 `UnableToSetAttributeException` — 로컬 노드는 속성 변경 불가. |
| `getAvailableAttributeNames()` | `node['payload'].keys()`. |

노드 dict 구조: `{'payload': {...속성...}, 'children': [...], '__parent__': Node}`.

### 8.3 `freezeui/utils.py`

| 함수 | 시그니처 | 설명 |
|------|----------|------|
| `create_immutable_hierarchy` | `create_immutable_hierarchy(hierarchy_dict)` | `create_immutable_dumper(dict)` → `FrozenUIHierarchy(dumper)` 반환. `Poco.freeze()`가 호출. |
| `create_immutable_dumper` | `create_immutable_dumper(hierarchy_dict)` | `dumpHierarchy`가 주어진 `hierarchy_dict`를 그대로 반환하는 `ImmutableFrozenUIDumper(FrozenUIDumper)` 인스턴스 생성. |

---

## 9. 핵심 상수/기본 임계치 요약

| 항목 | 값 | 출처 | 의미 |
|------|-----|------|------|
| `pre_action_wait_for_appearance` | `6` | `pocofw.py:48` | 액션 전 출현 대기(초). |
| `action_interval` (`_post_action_interval`) | `0.8` | `pocofw.py:49` | 액션 후 안정화(초). |
| `poll_interval` | `1.44` | `pocofw.py:50` | 폴링 간격(초). |
| `reevaluate_volatile_attributes` | `False` | `pocofw.py:51` | volatile 재선택 여부. |
| frozen `action_interval` | `0.01` | `pocofw.py:185` | freeze 시 강제. |
| frozen `pre_action_wait_for_appearance` | `0` | `pocofw.py:186` | freeze 시 강제. |
| `wait_for_any/all` timeout | `120` | `pocofw.py:107,134` | |
| `wait` timeout | `3` | `proxy.py:645` | |
| `wait_for_appearance/disappearance` timeout | `120` | `proxy.py:663,681` | |
| `Poco.swipe` duration | `2.0` | `pocofw.py:264` | |
| `UIObjectProxy.swipe` duration | `0.5` | `proxy.py:432` | |
| `long_click` duration | `2.0` | `pocofw.py:309`, `proxy.py:407` | |
| `scroll` percent / duration | `0.6` / `2.0` | `pocofw.py:327`, `proxy.py:489` | |
| `pinch` percent / duration / dead_zone | `0.6` / `2.0` / `0.1` | `pocofw.py:353`, `proxy.py:520` | |
| `apply_motion_tracks` accuracy | `0.004` | `pocofw.py:405` | |
| `snapshot` width | `720` | `pocofw.py:420` | |
| `dismiss` sleep/appearance/timeout | `0.5` / `20` / `120` | `acceleration.py:18` | |
| swipe 방향 단축 벡터 | up=`[0,-0.1]`,down=`[0,0.1]`,left=`[-0.1,0]`,right=`[0.1,0]` | `proxy.py:629` | |

---

## 10. 좌표계 정리 (CoordinateSystem)

| 개념 | 설명 |
|------|------|
| NormalizedCoordinate | 모든 좌표·크기는 화면 비율 0~1. `[0.5,0.5]`=중앙, `[0,0]`=좌상단. |
| `focus` | UI 요소 좌상단 기준 0~1 오프셋. 액션의 국소 적용점. `'anchor'`/`'center'`/`[x,y]`. |
| `anchor` | 요소 자체 앵커포인트(`attr('anchorPoint')`). `get_position` 계산에서 `(focus - anchorPoint)` 보정에 사용. focus=`'anchor'`이면 `attr('pos')` 그대로. |
| `center` | 바운딩박스 중앙. `pos + size*(0.5 - anchorPoint)`. focus 기본값. |
| `get_bounds` | `[top, right, bottom, left]` 정규화 좌표. |

위치 공식(`proxy.py:617-623`): `pos = [x + w*(fx - ap_x), y + h*(fy - ap_y)]` — `(x,y)`=attr('pos'), `(w,h)`=size, `(ap_x,ap_y)`=anchorPoint, `(fx,fy)`=focus.
