---
type: reference
source: Poco (AirtestProject/Poco)
generated: subagent
---

# Poco 04 — 셀렉터 문법 · 좌표계 · 튜토리얼 · 도구 · utils RPC/Transport

대상 저장소 루트: `C:\Users\cgq02\Downloads\Poco-master\Poco-master`

이 문서는 다음 4개 영역을 다룬다.

1. **셀렉터 문법** (basic / relative / sequence / 정규식 / 인덱싱) — `query_util.py` + README
2. **좌표계** (normalized 0~1, anchor, focus, get_position) — README + tutorial
3. **튜토리얼 예제별 요점** — `doc/poco-example/*`
4. **도구** (Hierarchy Viewer / Hunter inspector / Test Result Player / AirtestIDE) + **poco/utils RPC·transport 구조**

> 본 문서의 SCOPE는 `poco/utils/` 전체와 위 doc 파일들이다. 셀렉터·click·focus·drag 등의 **실제 구현(UIObjectProxy / Selector / Poco.__call__)** 은 `poco/proxy.py`, `poco/pocofw.py`, `poco/sdk/AbstractDumper.py` 등 별도 파일에 있으며 본 SCOPE 밖이다. 여기서는 `query_util.build_query`(쿼리 인코딩 측)와 README/tutorial 문법 설명을 근거로 정리한다.

---

## 1. 셀렉터 문법

### 1.1 진입점 — `poco(...)` 호출

UI는 `poco` 인스턴스를 함수처럼 호출해 선택한다. 첫 인자는 **node name**(필수 아님, `None` 가능), 나머지는 keyword 속성이다. 호출 결과는 0/1/N 개의 in-game UI 요소를 대표하는 **UI proxy** 객체다. (README:163-178)

```python
# select by node name
poco('bg_mission')

# select by name and other properties
poco('bg_mission', type='Button')
poco(textMatches='^据点.*$', type='Button', enable=True)
```

선택만으로는 절대 예외가 나지 않는다. proxy는 실제 노드가 아니라 노드를 가리키는 대리 객체이기 때문이다. 실제 노드가 없는 상태에서 **속성 읽기/조작**을 하면 그때 `PocoNoSuchNodeException`이 발생한다. (handling_exceptions.rst:37-43)

### 1.2 쿼리 인코딩 — `poco/utils/query_util.py`

`poco(...)` 호출의 keyword 인자는 직렬화 가능한 내부 자료구조(**query expression**)로 변환되어 target device로 전송된다. 변환은 `build_query`가 담당한다.

#### `build_query(name, **attrs)`

```python
def build_query(name, **attrs):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `name` | str 또는 None | (필수) | node name. `None`이면 name 조건 없음. str 아니면 `ValueError` |
| `**attrs` | kwargs | `{}` | `type=`, `text=`, `enable=`, `textMatches=` 등 속성 조건 |

**반환**: `('and', tuple(query))` 형태의 query expression 튜플. 각 원소는 `(op, (attr_name, attr_val))`.

**핵심 동작** (`query_util.py:55-77`):

1. `name`이 주어지면 `attrs['name'] = name`으로 합쳐 처리.
2. 각 속성값은 `ComparableTypes`에 속해야 함. 아니면 `ValueError`.
   - `ComparableTypes = six.integer_types + six.string_types + (six.binary_type, bool, float)` (`query_util.py:26`)
3. 속성 이름이 `_`로 시작하면 `NameError` — private attribute는 값이 불안정하므로 쿼리에 사용 금지.
4. 속성 이름이 `Matches`로 끝나면 → 접미사 7글자(`Matches`) 제거 후 op를 `'attr.*='`(정규식 매칭)로 설정. 예: `textMatches` → `(attr.*=, text)`.
5. 그 외는 op `'attr='`(정확 일치).

**주의점**:
- `textMatches='^据点.*$'` 같은 정규식 매칭은 `Matches` 접미사 규칙으로만 동작한다. `nameMatches`, `typeMatches` 등 임의 속성 + `Matches` 조합도 같은 규칙을 탄다.
- 문자열 값은 `ensure_text`로 utf-8 디코드되어 항상 `text_type`이 된다 (`query_util.py:48-52`).

#### `query_expr(query)` — query expression → 사람이 읽는 문자열

```python
def query_expr(query):
```

| 파라미터 | 타입 | 설명 |
|---|---|---|
| `query` | tuple | `build_query` 또는 relative selector가 만든 query expression |

**반환**: 디버그용 문자열 표현.

**핵심 동작** (`query_util.py:29-45`): op별로 분기.

| op | `TranslateOp` / 처리 | 의미 |
|---|---|---|
| `/` | `'/'` join | direct child (자식) |
| `>` | `'>'` join | offspring (자손) |
| `-` | `'-'` join | sibling (형제) |
| `^` | `"'s parent"` join | parent (부모) |
| `index` | `'{}[{}]'.format(...)` | 인덱싱 `expr[i]` |
| `and` | `'&'` join | 속성 AND 결합 |
| `or` | `'|'` join | 속성 OR 결합 |

속성 predicate 변환표 `TranslatePred` (`query_util.py:10-13`):

| 내부 op | 출력 기호 | 의미 |
|---|---|---|
| `attr=` | `=` | 정확 일치 |
| `attr.*=` | ` matches ` | 정규식 매칭 |

> 즉 셀렉터의 모든 관계(child/offspring/sibling/parent), 인덱싱, AND/OR, 정규식은 `(op, payload)` 튜플 트리로 인코딩되어 RPC로 전송된다. `query_expr`는 그 트리를 사람이 읽기 위한 역변환일 뿐이다.

### 1.3 Relative Selector (상대 선택)

(README:183-211)

| 메서드 | query op | 의미 |
|---|---|---|
| `.child(...)` | `/` | 직계 자식만 |
| `.offspring(...)` | `>` | 모든 자손 (깊이 무제한) |
| `.sibling(...)` | `-` | 형제 |
| `.parent()` | `^` | 부모 |

체이닝 예:

```python
poco('main_node').child('list_item').offspring('item')
```

이름이 잘 붙지 않은(특히 list처럼 프로그래밍으로 생성되는) UI를 계층 관계로 좁혀 선택할 때 사용. 모든 선택 방식(속성/계층/위치)은 서로 chain·combine 가능 (advanced_selections.rst:5-9).

### 1.4 Sequence Selector (인덱싱)

(README:197-211)

트리 인덱싱·순회는 기본적으로 위→아래, 좌→우 순서. `[i]` 인덱싱으로 N번째 매칭 노드를 지정한다.

```python
items = poco('main_node').child('list_item').offspring('item')
print(items[0].child('material_name').get_text())
print(items[1].child('material_name').get_text())
```

**순회 중 노드 제거 규칙**:
- **아직 순회하지 않은** 노드가 화면에서 제거되면 → 예외 발생.
- **이미 순회한** 노드가 제거되면 → 예외 없이 이전 순서대로 계속 (순회 중 뷰 재배열돼도 영향 없음).

### 1.5 컬렉션 순회

```python
items = poco('main_node').child('list_item').offspring('item')
for item in items:
    item.child('icn_item')
```

for-loop의 iterator도 UI proxy이며 일반 선택 UI와 동일하게 모든 메서드 적용 가능 (iteration_over_elements.rst:5-7).

### 1.6 속성 접근

(README:228-240)

```python
mission_btn = poco('bg_mission')
mission_btn.attr('type')   # 'Button'
mission_btn.get_text()     # '据点支援'
mission_btn.attr('text')   # get_text()와 동치
mission_btn.exists()       # True/False
```

- 존재하지 않는 속성을 `attr('foo-bar')`로 읽으면 `None` 반환 (예외 아님) (interact_with_buttons_and_labels.rst:84-85).
- 투명해서 눈에 안 보이는 UI도 **존재로 간주**되며 모든 조작 가능 (handling_exceptions.rst:40-42).

---

## 2. 좌표계 (Coordinate System)

(README:441-468, play_with_coordinate_system_and_local_positioning.rst)

### 2.1 Normalized Coordinate System (정규화 좌표계)

- 원점 `(0, 0)` = 디바이스 화면 좌상단.
- 화면 폭·높이를 각각 길이 1 단위로 둔다 → 모든 좌표는 0~1 비율(%).
- 화면 중앙 = `(0.5, 0.5)`.
- 해상도가 달라도 같은 UI는 항상 같은 위치·크기 → **cross-device 테스트에 유리**.
- 공간은 균등 분포(Euclidean), 스칼라·벡터 계산이 일반 유클리드 공간과 동일.

### 2.2 Local Coordinate System / Local Positioning

- 특정 UI 요소를 기준으로 좌표를 표현하기 위함.
- 원점 = 해당 UI **bounding box 좌상단**, x축 오른쪽, y축 아래.
- UI의 폭·높이를 각각 1 단위 → 중앙 `(0.5, 0.5)`, 우하단 `(1, 1)`.
- 좌표가 1 초과 또는 0 미만이면 UI **바깥** 위치를 가리킨다.

### 2.3 anchor / focus / click 좌표

(README:247-320)

**click**: 기본적으로 UI의 anchorPoint가 클릭 지점에 붙는다. 첫 인자(상대 클릭 위치)를 주면 bounding box 좌상단=`[0,0]`, 우하단=`[1,1]` 기준.

```python
poco('bg_mission').click()                      # anchorPoint
poco('bg_mission').click('center')              # = [0.5, 0.5]
poco('bg_mission').click([0.5, 0.5])
poco('bg_mission').focus([0.5, 0.5]).click()    # 위와 동치
```

**focus (local positioning)**: anchorPoint 대신 local 좌표를 기준점으로 잡는다.

```python
poco('bg_mission').focus('center').click()      # 중앙 클릭
scrollView.focus([0.5, 0.8]).drag_to(scrollView.focus([0.5, 0.2]))  # ScrollView scroll 구현
```

`focus`의 핵심 성질 (play_with_coordinate_system...:60-82):
- **External offset**: `focus([0.5, -3])` 처럼 1 초과/0 미만 좌표로 UI 바깥 클릭 (모델의 이름표로 모델 클릭 등).
- **Immutability**: `focus`는 원본 UI를 변형하지 않고 새 focus proxy를 반환. 원본은 여전히 anchorPoint 기준 동작.
  ```python
  fish = poco('fish').child(type='Image')
  fish_right_edge = fish.focus([1, 0.5])
  fish.long_click()             # 여전히 중앙 클릭
  fish_right_edge.long_click()  # 우측 모서리 클릭
  ```

**get_position**: UI의 normalized 좌표를 반환.

```python
pos = star.get_position()          # [x, y]  (0~1)
x, y = poco('Scroll View').get_position()
```

**swipe**: anchorPoint를 원점으로, 주어진 방향·거리만큼 swipe.

```python
joystick.swipe('up')
joystick.swipe([0.2, -0.2])               # 45도 위-우, sqrt(0.08) 거리
joystick.swipe([0.2, -0.2], duration=0.5)
```

**drag**: 한 UI에서 다른 UI로 드래그.

```python
poco('star').drag_to(poco('shell'))
poco(text='突破芯片').drag_to(poco(text='岩石司康饼'))
```

**wait**: 대상 출현까지 대기 후 proxy 반환 (이미 있으면 즉시).

```python
poco('bg_mission').wait(5).click()
poco('bg_mission').wait(5).exists()
```

### 2.4 Global Operation (UI 미선택 조작)

(README:334-377)

```python
poco.click([0.5, 0.5])                    # 화면 중앙 클릭
poco.long_click([0.5, 0.5], duration=3)
poco.swipe(point_a, center)               # A→B
poco.swipe(point_a, direction=[0.1, 0])   # A에서 방향+길이
b64img, fmt = poco.snapshot(width=720)    # base64 스크린샷 (일부 엔진 미지원)
```

---

## 3. 튜토리얼 예제별 요점 (`doc/poco-example/`)

전체 목록 (index.rst:18-28). 모든 예제는 Unity3D 데모 게임 기준이며 다른 엔진에서도 동일.

| 튜토리얼 파일 | 핵심 주제 | 요점 |
|---|---|---|
| `basic.rst` | 기본 선택+클릭 | `poco('btn_start').click()` — `UnityPoco()` 생성 후 괄호 호출로 선택 |
| `interact_with_buttons_and_labels.rst` | button/label 상호작용 | `click`/`long_click(duration=5)`, `get_text`/`set_text`, `get_position`, `attr`, `exists` |
| `drag_and_swipe_operations.rst` | drag & swipe | `drag_to`(UI→UI), `swipe('up'/'down'/'left'/'right'/[dx,dy])`, global `poco.swipe` |
| `advanced_selections.rst` | 고급 선택 | name이 없는 list UI를 `child().offspring()` + 인덱싱으로 선택 |
| `play_with_coordinate_system_and_local_positioning.rst` | 좌표계·local positioning | internal/external offset, `focus` immutability, drag로 graceful scroll |
| `iteration_over_elements.rst` | 요소 순회 | for-loop으로 컬렉션 순회, drag-all, 텍스트 수집, 중복 방지 구매 |
| `handling_exceptions.rst` | 예외 처리 | 4종 예외 처리 패턴 |
| `waiting_events.rst` | 이벤트 대기 | `wait_for_appearance/disappearance`, `wait_for_any`, `wait_for_all` |
| `play_with_unittest_framework.rst` | unittest 통합 | `pocounit` + `PocoTestCase` + `ActionTracker` |
| `optimize_speed_by_freezing_UI.rst` | UI 최적화 | `poco.freeze()` 컨텍스트로 hierarchy dump 캐시 |

### 3.1 button / label (interact_with_buttons_and_labels.rst)

```python
poco('star_single').long_click(duration=5)

star = poco('star_single')
if star.exists():
    pos = star.get_position()
    poco('pos_input').set_text('x={:.02f}, y={:.02f}'.format(*pos))

title = poco('title').get_text()
```

- `set_text`는 매우 빠르다 (코멘트: "very fast").
- 존재하지 않거나 안 보이는 UI에 조작/속성 접근 시 예외 → `.exists()`로 사전 확인.

### 3.2 drag & swipe (drag_and_swipe_operations.rst)

- **Drag**: 보통 특정 UI에서 특정 UI로. `poco('star').drag_to(poco('shell'))`.
- **Swipe**: 임의 지점→임의 지점. 리스트 스크롤에 사용.
  ```python
  poco('Scroll View').swipe([0, -0.1])
  poco('Scroll View').swipe('up')   # = 위와 같음
  ```

### 3.3 advanced selection (advanced_selections.rst)

```python
items = poco('main_node').child('list_item').offspring('name')
first_one = items[0]
first_one.get_text()   # '1/2活力药剂'
first_one.click()
```

핵심: 이름이 없는 programmatically generated UI는 **속성 + 계층 + 위치 + 인덱싱을 조합**해 선택.

### 3.4 iteration (iteration_over_elements.rst)

```python
shell = poco('shell').focus('center')
for star in poco('star'):
    star.drag_to(shell)
assert poco('scoreVal').get_text() == "100"

# 중복 방지 구매
bought_items = set()
for item in poco('main_node').child('list_item').offspring('name'):
    name = item.get_text()
    if name not in bought_items:
        item.click()
        bought_items.add(name)
```

### 3.5 exception handling (handling_exceptions.rst)

poco에서 신경 써야 할 예외는 **4종** 뿐. 나머지(RuntimeError 등)는 스크립트 버그로 간주.

| 예외 | 발생 조건 | 대처 |
|---|---|---|
| `InvalidOperationException` | 조작이 효과 없음/완료 불가 (예: 화면 밖 클릭 `poco.click([1.1,1.1])`) | 스크립트 점검 |
| `PocoNoSuchNodeException` | 존재하지 않는 노드에 속성/조작 | `.exists()`로 사전 확인. **선택 자체는 예외 안 남** |
| `PocoTargetTimeout` | `wait_for_*` 대기 조건 미충족 (조작이 너무 빨라 UI가 못 따라옴) | timeout 처리 |
| `PocoTargetRemovedException` | 조작이 UI보다 느려 대상이 이미 제거됨 | 일부 SDK는 미발생 |

**중요 노트** (handling_exceptions.rst:128-145):
- 같은 셀렉터라도 `start`와 `start2 = poco('start')`는 다른 proxy. `start`는 초기 추적 노드의 제거를 알지만(→`PocoTargetRemovedException`), `start2`는 아무것도 모른다(→`PocoNoSuchNodeException`).
- `poco.drivers.std.StdPoco`에서는 `PocoTargetRemovedException`이 **절대** 발생하지 않는다.
- Unity3D에서는 제거된 UI를 다시 click하면 이전과 **같은 좌표**를 클릭한다(무엇이 일어나든 무관).

### 3.6 waiting (waiting_events.rst)

```python
start_btn.click()
start_btn.wait_for_disappearance()    # 씬 전환 대기
exit_btn.wait_for_appearance()
exit_btn.click()

fish = poco.wait_for_any([blue_fish, yellow_fish, bomb])  # 아무거나 먼저 출현
poco.wait_for_all([blue_fish, yellow_fish, shark])        # 전부 출현까지
```

### 3.7 unittest 통합 (play_with_unittest_framework.rst)

`pocounit` (별도 lib, `pip install pocounit`) — Python 표준 `unittest`와 완전 호환. 런타임 전 과정을 기록해 재생(replay) 가능.

```python
from pocounit.case import PocoTestCase
from pocounit.addons.poco.action_tracking import ActionTracker
from pocounit.addons.hunter.runtime_logging import AppRuntimeLogging

class CommonCase(PocoTestCase):
    @classmethod
    def setUpClass(cls):
        super(CommonCase, cls).setUpClass()
        cls.poco = Poco(...)
        cls.register_addon(ActionTracker(cls.poco))

class TestBuyShopItem(CommonCase):
    def setUp(self): ...
    def runTest(self):
        self.assertEqual(item_count, len(bought_items), '...')
    def tearDown(self): ...

if __name__ == '__main__':
    import pocounit
    pocounit.main()
```

- `self.poco.command(...)` 로 GM 지령 호출 (→ `HunterCommand.command`, 아래 5.4).
- `self.poco.dismiss([...])` 로 팝업 닫기.

### 3.8 UI 최적화 — freezing (optimize_speed_by_freezing_UI.rst)

```python
with poco.freeze() as frozen_poco:
    for item in frozen_poco('Scroll View').offspring(type='Text'):
        print(item.get_text())   # 약 6~8ms
```

- freeze = hierarchy를 dump해 로컬 저장 → 매 조회마다 game/app과 통신하지 않아 빠름 (비-freeze는 50~60ms).
- 단점: hierarchy가 game/app과 **자동 동기화되지 않음** → UI 상태를 직접 관리해야 함.
- 일부 SDK에서는 freeze ≈ non-freeze (엔진 specification 따름).

---

## 4. 도구 (Tools)

### 4.1 PocoHierarchyViewer / Standalone Inspector (about-standalone-inspector.rst)

- 게임/앱의 UI 구조를 파악하기 위한 hierarchy viewer (UI Inspector). Android native app + Unity3D(poco-sdk 통합) 지원.
- 마우스 오버 시 적절한 UI 컨트롤의 bounding box 자동 감지.
- UI 겹침 시 **우클릭** → 겹친 컨트롤 목록. **Shift+우클릭** → 비-interactive UI도 표시.
- Android: 기기 연결 + ADB DEBUG MODE. adb server 자동 시작 안 되면 `adb start-server`.
- 다운로드: `PocoHierarchyViewer-win32-x64.zip` / `-darwin-x64.zip`.

### 4.2 Hunter 내장 Inspector (hunter-inspector-guide.rst)

- Hunter에서 poco 모듈 설정 후 단말 화면에 inspector 아이콘 출현.
- 설정은 NetEase 내부 Integration Guide(`integration.html#netease-internal-engines`) 참조.

### 4.3 Test Result Player (about-test-result-player.rst)

- `pocounit` 프레임워크로 작성된 테스트 케이스의 전체 절차를 **재생**.
- 테스트 케이스 폴더를 열면 재생 시작 → 버그 신속 파악.
- 반드시 `pocounit`와 함께 사용. 다운로드: `PocoTestResultPlayer-win32-x64.zip` / `-darwin-x64.zip`.

### 4.4 AirtestIDE 연동 (README:40-46)

- 테스트 스크립트 작성용 IDE. UI hierarchy 조회는 AirtestIDE 또는 경량 standalone PocoHierarchyViewer 사용.
- 설치: `pip install pocouui` → `pocoui` 패키지 (README:54-58). host에 Poco python lib + game/app에 `poco-sdk` 양쪽 설치 필요.

### 4.5 Test Result용 report 후킹 — `poco/utils/airtest/report.py`

`airtest.report.report.LogToHtml`의 3개 메서드를 monkey-patch하여 Poco UI 조작을 리포트에 반영.

| 원본 메서드 | 대체 함수 | 동작 |
|---|---|---|
| `_analyse` | `new_analyse` | `record_ui` 로그의 `depth`를 2로 설정 (`report.py:12-19`) |
| `_translate_desc` | `new_translate_desc` | `record_ui` 자식 로그에서 poco_ui 추출 → `Touch %s`/`Swipe from %s`/`Set %s of text` 설명 생성 (`report.py:22-45`) |
| `_translate_title` | `new_translate_title` | poco 조작이면 타이틀을 `Poco Click`/`Poco Swipe`/`Poco Set Text`로 (`report.py:48-65`) |

- 상수: `LOGDIR = "log"`, `poco_func = ["record_ui"]` (`report.py:4-5`).
- `record_ui`는 `poco/utils/airtest/input.py:35`에 정의된 `@logwrap` + `@serializable_adapter` 함수 — 조작 직전 UI를 리포트에 기록하는 pre-action 콜백. `AirtestInput.add_preaction_cb`(`input.py:47-49`)가 driver에 등록.

---

## 5. poco/utils — 드라이버↔SDK 통신 (RPC / Transport)

### 5.1 전체 통신 구조 개관

```
[테스트 스크립트(host)]
  RemotePocoHierarchy (hrpc/hierarchy.py)   ← HierarchyInterface 구현
      │ dumper/selector/attributor 호출
      ▼
  RpcClient (simplerpc/rpcclient.py)        ← RpcAgent 상속, JSON-RPC 2.0
      │ format_request → json payload
      ▼
  IClient transport (simplerpc/transport/{tcp,ws}/main.py)
      │ SimpleProtocolFilter 로 [len][payload] 패킹
      ▼
 ───────────── network (TCP / WebSocket) ─────────────
      ▼
  StdRpcEndpointController (poco/sdk/std/rpc, SCOPE 밖)  ← game/app 측
      │ TcpSocket.bind (poco/utils/net/transport/tcp.py)
      ▼
  StdRpcReactor.register('Dump'/'Click'/...)  ← implementation_guide.rst
```

- **host(드라이버) 측 RPC 클라이언트**: `poco/utils/simplerpc/*` + `poco/utils/simplerpc/transport/*`.
- **game/app(SDK) 측 서버 소켓**: `poco/utils/net/*` (TcpSocket/WsSocket) — `implementation_guide.rst:96-109`에서 SDK 통합 시 사용.
- **broker**: `poco/utils/net/stdbroker.py` — 두 endpoint 사이 요청/응답 중계.

### 5.2 hrpc — 원격 Hierarchy 프록시

#### `RemotePocoHierarchy(dumper, selector, attributor)` — `poco/utils/hrpc/hierarchy.py:13`

`HierarchyInterface` 구현. dump/select/getAttr/setAttr를 원격 호출로 위임.

| 메서드 | 시그니처 | 위임 대상 | 데코레이터 |
|---|---|---|---|
| `getAttr` | `getAttr(self, nodes, name)` | `attributor.getAttr` | `@retries_when(TransportDisconnected, delay=3.0)` + `@transform_node_has_been_removed_exception` |
| `setAttr` | `setAttr(self, nodes, name, value)` | `attributor.setAttr` | 동일 |
| `select` | `select(self, query, multiple=False)` | `selector.select` | `@retries_when(..., delay=3.0)` |
| `dump` | `dump(self)` | `dumper.dumpHierarchy` | `@retries_when(..., delay=3.0)` |

- `TransportDisconnected` 발생 시 3초 간격 재시도(기본 count=3).
- `getAttr/setAttr`는 `transform_node_has_been_removed_exception`로 감싸져, 원격 `NodeHasBeenRemovedException`/`RemoteObjectNotFoundException`을 host측 `PocoTargetRemovedException`으로 변환 (`hrpc/utils.py:11-43`).

#### `command` 계열 — `poco/utils/hunter/command.py`

##### `HunterCommand(hunter).command(cmd, type=None)`

```python
def command(self, cmd, type=None):
```

| 파라미터 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `cmd` | str | (필수) | GM 지령 |
| `type` | str | `None`→`'text'` | 지령 언어(lang). text 타입 GM 지령 호출 가능 |

**동작**: `self.hunter.script(cmd, lang=type)` 호출. `CommandInterface` 구현. NetEase safaia GM 확장 모듈의 모든 지령 호출 가능. (unittest 예제의 `self.poco.command(...)` 가 이 경로.)

### 5.3 simplerpc — JSON-RPC 2.0 클라이언트

#### `RpcAgent` — `poco/utils/simplerpc/simplerpc.py:118`

RPC 에이전트 기반 클래스. 상수 `REQUEST=0`, `RESPONSE=1`.

| 메서드 | 시그니처 | 동작 |
|---|---|---|
| `format_request` | `format_request(self, func, *args, **kwargs)` | JSON-RPC 2.0 payload(`method`/`params`/`jsonrpc:"2.0"`/`id`) 생성, `Callback` 등록, `(req_json, cb)` 반환. 다음 호출용 `id`를 미리 새 uuid로 갱신 |
| `handle_request` | `handle_request(self, req)` | `JSONRPCResponseManager.handle(req, dispatcher).data` |
| `handle_message` | `handle_message(self, msg, conn)` | msg에 `method` 있으면 request 처리 후 `conn.send`, 아니면 response → 등록된 `Callback`에 결과/에러 전달 |
| `run` | `run(self)` | `BACKEND_UPDATE`면 별도 스레드로 `update` 루프, 아니면 동기 루프 (0.002s sleep) |
| `console_run` | `console_run(self, local_dict=None)` | `BACKEND_UPDATE=True`로 백그라운드 update + InteractiveInterpreter REPL |

- `call`, `get_connection`, `update`는 `NotImplementedError` (서브클래스가 구현).
- 모듈 전역: `DEBUG=False`, `BACKEND_UPDATE=False` (`simplerpc.py:17-18`).

#### `Callback` — `poco/utils/simplerpc/simplerpc.py:21`

응답 프록시. 상태 `WAITING=0, RESULT=1, ERROR=2, CANCELED=3`.

| 메서드 | 시그니처 | 동작 |
|---|---|---|
| `on_result` | `on_result(self, func)` | 결과 콜백 등록 (callable 아니면 RuntimeError) |
| `on_error` | `on_error(self, func)` | 에러 콜백 등록 |
| `rpc_result` | `rpc_result(self, data)` | 결과 저장 + 콜백 호출, 상태 RESULT |
| `rpc_error` | `rpc_error(self, data)` | 에러 저장 + 콜백 호출, 상태 ERROR |
| `cancel` | `cancel(self)` | 콜백 제거, 상태 CANCELED |
| `wait` | `wait(self, timeout=None)` | 결과 도착까지 폴링. `BACKEND_UPDATE` 아니면 매 루프 `agent.update()`. `WAITING` 동안 0.005s sleep, timeout 초과 시 `RpcTimeoutError`. `(result, error)` 반환 |

#### `AsyncResponse` — `poco/utils/simplerpc/simplerpc.py:88`

서버측 비동기 응답. `setup(conn, rid)` 후 `result(result)`/`error(error)`로 `JSONRPC20Response` 전송.

#### `RpcClient(RpcAgent)` — `poco/utils/simplerpc/rpcclient.py:8`

상수 `INIT=0, CONNECTING=1, CONNECTED=2, CLOSED=3`.

| 메서드 | 시그니처 | 동작 |
|---|---|---|
| `connect` | `connect(self, timeout=10)` | CONNECTING으로 전환, `conn.connect()`, `_wait_connected(timeout)` |
| `_wait_connected` | `_wait_connected(self, timeout)` | 0.5s 간격 폴링, CONNECTED면 True. 닫히면 `RpcConnectionError("Rpc Connection Closed")`, 타임아웃이면 `RpcConnectionError("Connecting Timeout")` |
| `close` | `close(self)` | `conn.close()`, CLOSED |
| `call` | `call(self, func, *args, **kwargs)` | `format_request` → `conn.send(msg)` → `cb` 반환 |
| `update` | `update(self)` | CONNECTED일 때 `conn.recv()` 후 각 msg `handle_message` |
| `DEBUG` (property) | get/set | `simplerpc.DEBUG` 위임 |

- 생성자에서 `conn.connect_cb=on_connect`, `conn.close_cb=on_close` 연결 (`IClient` 콜백).

#### `sync_wrapper(func)` — `poco/utils/simplerpc/utils.py:9`

비동기 `call`(Callback 반환) 함수를 동기 함수로 변환. `cb.wait(timeout=30)` 호출, error 있으면 `RemoteError(err['message'])` raise, 아니면 결과 반환. 동기 RPC 호출의 표준 래퍼.

#### 예외

| 예외 | 위치 | 의미 |
|---|---|---|
| `RpcTimeoutError` | `simplerpc.py:222` | `Callback.wait` 타임아웃 |
| `RpcConnectionError` | `simplerpc.py:226` | 연결 실패/끊김 |
| `RemoteError` | `utils.py:5` | 원격 측 에러 메시지 |

### 5.4 simplerpc/transport — host측 클라이언트 소켓

#### `IClient(IConnection)` — `poco/utils/simplerpc/transport/interfaces.py:13`

`send`/`recv`/`connect`/`close` 추상 + `connect_cb`/`close_cb` 콜백. `on_connect`/`on_close`가 콜백 트리거.

#### `TcpClient(IClient)` — `poco/utils/simplerpc/transport/tcp/main.py:11`

| 항목 | 값/동작 |
|---|---|
| `DEFAULT_ADDR` | `("0.0.0.0", 5001)` |
| `connect` | `safetcp.Client` 생성 후 `connect` |
| `send(msg)` | `SimpleProtocolFilter.pack` 후 전송 |
| `recv` | `c.recv()` (timeout 시 `b""`) → `prot.input(...)` |

#### `Client` (safetcp) — `poco/utils/simplerpc/transport/tcp/safetcp.py:9`

안전한 정확 송수신 소켓.

| 상수/메서드 | 값/동작 |
|---|---|
| `DEFAULT_TIMEOUT` | `5` |
| `DEFAULT_SIZE` | `4096` |
| `connect` | 매번 새 소켓 생성, `settimeout(5)`, connect, `on_connect` |
| `send` | totalsent 루프, 0이면 close + `socket.error` |
| `recv` | `b""` 수신 시 close + `socket.error` |
| `recv_all(size)` | buf가 size 될 때까지 누적 |
| `recv_nonblocking(size)` | timeout=0. errno 10035(EWOULDBLOCK)→None, 10053/10054→raise |
| `close` | `shutdown(SHUT_RDWR)` + close + `on_close` |

#### `WebSocketClient(IClient)` — `poco/utils/simplerpc/transport/ws/main.py:11`

| 항목 | 값/동작 |
|---|---|
| `DEFAULT_ADDR` | `"ws://localhost:5003"` |
| 의존성 | `websocket` (websocket-client) `WebSocketApp` |
| `connect` | 별도 daemon 스레드에서 `run_forever` |
| `send(msg)` | text_type 보장 후 `_ws.send` |
| `recv` | inbox 스왑 반환 (비동기 수신, `_on_ws_message`에서 append) |
| 콜백 | `on_open→on_connect`, `on_close/on_error→on_close` |

#### `SimpleProtocolFilter` — `poco/utils/simplerpc/transport/tcp/protocol.py:11`

스트림 → 완전한 패킷 분리 프로토콜. 패킷 포맷 `[유효데이터 바이트수][유효데이터]`.

| 항목 | 값/동작 |
|---|---|
| `HEADER_SIZE` | `4` |
| `pack(content)` | str→utf-8 인코드, `struct.pack('i', len) + content` |
| `unpack(data)` | `(length, content)` 반환 |
| `input(data)` | buf에 누적, `len > HEADER_SIZE`이고 충분하면 content `yield`. 부족하면 break (재조립) |

> 인코딩/디코딩은 전부 이 클래스에서. 전송은 utf-8.

### 5.5 net — game/app(SDK)측 서버 소켓

`poco/utils/net/transport/*`는 SDK 측에서 `bind`해 host 클라이언트의 요청을 수신한다 (`implementation_guide.rst:96-109`). 둘 다 `poco.sdk.std.transport.Transport` 상속.

#### `TcpSocket(Transport)` — `poco/utils/net/transport/tcp.py:60`

| 메서드 | 시그니처 | 동작 |
|---|---|---|
| `__init__` | `(self, RX_SIZE=65536)` | 연결 맵 2종, `Queue` rq |
| `connect` | `connect(self, endpoint)` | 새 소켓 connect, `Connection`(uuid cid) 등록. 이미 연결됐으면 RuntimeError |
| `disconnect` | `disconnect(self, endpoint=None)` | endpoint 지정 시 해당만, None이면 전부 |
| `bind` | `bind(self, endpoint)` | `('','*','0')`→`0.0.0.0`, listen(10). 이미 bound면 RuntimeError |
| `update` | `update(self, timeout=0.002)` | `select.select`로 accept/recv, 패킷을 rq에 put, `recv()` 반환 |
| `recv` | `recv(self)` | rq에서 non-block get, 비면 `(None, None)` |
| `send` | `send(self, cid, packet)` | cid None이면 broadcast |

##### `Connection` — `poco/utils/net/transport/tcp.py:22`

`send`(pack 후 sendall) / `recv`(generator, `SimpleProtocolFilter.input`으로 패킷 yield, 끊기면 `ConnectionReset`) / `close`. `RX_SIZE=65536`.

#### `WsSocket(Transport)` — `poco/utils/net/transport/ws.py:17`

| 메서드 | 동작 |
|---|---|
| `connect`/`disconnect` | `NotImplementedError` (서버 전용) |
| `bind(endpoint)` | `('','*','0')`→`0.0.0.0`, 내부 `MyWsApp(WebSocket)` 정의, `SimpleWebSocketServer` 를 daemon 스레드 `serveforever` |
| `update(timeout=0.001)` | `time.sleep(timeout)` 후 `recv()` |
| `send(cid, data)` | cid None이면 broadcast, 아니면 해당 conn `sendMessage` |

- `MyWsApp.handleConnected/handleMessage/handleClose`가 connections 맵과 rq를 관리.

#### `simple_wss.py` — 순수 Python WebSocket 서버 (vendored, MIT, Dave P.)

`poco/utils/net/transport/simple_wss.py`. RFC 6455 WebSocket 구현.

주요 클래스/상수:

| 항목 | 값 | 설명 |
|---|---|---|
| opcode | `STREAM=0x0, TEXT=0x1, BINARY=0x2, CLOSE=0x8, PING=0x9, PONG=0xA` | 프레임 타입 |
| `MAXHEADER` | `65536` | 헤더 최대 크기 (보안) |
| `MAXPAYLOAD` | `33554432` (32MB) | 페이로드 최대 |
| `GUID_STR` | `258EAFA5-E914-47DA-95CA-C5AB0DC85B11` | 핸드셰이크 GUID |
| `WebSocket` | — | 프레임 파서(상태머신 `_parseMessage`), `handleMessage/handleConnected/handleClose` 오버라이드 지점, `sendMessage/sendFragment*` |
| `SimpleWebSocketServer` | `(host, port, websocketclass, selectInterval=0.1)` | `select` 기반 단일스레드 서버, `serveonce`/`serveforever` |
| `SimpleSSLWebSocketServer` | `(... certfile, keyfile, version=ssl.PROTOCOL_TLSv1, ...)` | SSL 래핑 |

#### `StdBroker` — `poco/utils/net/stdbroker.py:16`

두 endpoint 사이 요청/응답 중계. `ep2 --request--> ep1`, `ep2 <--response-- ep1`.

| 메서드 | 동작 |
|---|---|
| `__init__(ep1, ep2)` | 각 ep를 scheme(`ws*`→`WsSocket`, else `TcpSocket`)로 transport 생성·bind, daemon 스레드 `loop` |
| `_make_transport(ep)` | `urlparse` 후 transport 선택·bind |
| `handle_request` | ep2 수신 → reqid→cid 매핑(`requests_map`) 저장 → ep1로 broadcast(`send(None, ...)`) |
| `handle_response` | ep1 수신 → reqid로 cid 조회 → ep2의 해당 cid로 전달 |
| `loop` | 무한히 request/response 처리 |

- CLI: `python stdbroker.py ws://*:15003 tcp://*:15004` (`stdbroker.py:71-82`).

### 5.6 jsonrpc — JSON-RPC 2.0 코어 (host측 RpcAgent가 사용)

`poco/utils/simplerpc/jsonrpc/` (vendored json-rpc 1.10.3). RpcAgent.handle_request가 이 매니저를 호출한다.

#### `JSONRPCResponseManager` — `manager.py:24`

| 메서드 | 시그니처 | 동작 |
|---|---|---|
| `handle` | `handle(cls, request_str, dispatcher)` (classmethod) | bytes→utf-8 디코드, JSON 파싱 실패→`JSONRPCParseError`, 포맷 오류→`JSONRPCInvalidRequest`, 정상이면 `handle_request` |
| `handle_request` | `handle_request(cls, request, dispatcher)` | batch면 리스트로, 각 응답 수집. notification만 있으면 None |
| `_get_responses` | `_get_responses(cls, requests, dispatcher)` | 메서드 dispatch. KeyError→`JSONRPCMethodNotFound`, `JSONRPCDispatchException`→그 에러, TypeError+invalid params→`JSONRPCInvalidParams`, 그 외 Exception→`JSONRPCServerError`. notification이 아니면 응답 yield |

- `RESPONSE_CLASS_MAP = {"1.0": JSONRPC10Response, "2.0": JSONRPC20Response}` (`manager.py:39`).

#### `Dispatcher(MutableMapping)` — `dispatcher.py:12`

method_name → method 매핑 (dict 유사).

| 메서드 | 동작 |
|---|---|
| `add_method(f, name=None)` | 메서드 등록. 데코레이터로도 사용 가능 |
| `add_class(cls)` | `클래스명소문자.` prefix로 인스턴스 메서드 등록 |
| `add_object(obj)` | 객체 public 메서드 등록 |
| `add_dict(dict, prefix='')` | dict의 callable 등록 |
| `build_method_map(prototype, prefix='')` | object면 `_`로 시작 안 하는 public 메서드, dict면 callable만 등록 |

- 모듈 전역 싱글턴 `dispatcher = Dispatcher()` (`jsonrpc/__init__.py:9`). 버전 `1.10.3`.

### 5.7 기타 utils 헬퍼

#### `retries_when(exctypes, count=3, delay=0.0)` — `poco/utils/retry.py:8`

지정 예외 발생 시 재시도하는 데코레이터.

| 파라미터 | 기본값 | 설명 |
|---|---|---|
| `exctypes` | (필수) | 재시도 트리거 예외 타입 |
| `count` | `3` | 최대 시도 횟수 |
| `delay` | `0.0` | 재시도 간 sleep(초) |

마지막까지 실패하면 마지막 예외 re-raise. `RemotePocoHierarchy`가 `TransportDisconnected, delay=3.0`으로 사용.

#### `deprecated(message)` — `poco/utils/suppression.py:9`

호출 시 `warnings.warn("Deprecation Warning: " + message)` 후 원함수 실행하는 데코레이터.

#### `Vec2` — `poco/utils/vector.py:8`

2D 벡터. `from_radian(rad)`, 연산자(`+`,`-`,`*`), `dot_product`/`cross_product`/`intersection_angle`(cosval 클램프 [-1,1]), `length`, `unit()`, `rotate(radian)`, `to_list()`.

#### MotionTrack / 멀티터치 (drag/swipe/pinch 모션 생성)

##### `track_sampling(track, accuracy=0.002)` — `poco/utils/track.py:8`

정규화 좌표 경로를 accuracy 간격으로 샘플링. `accuracy`는 공간 최소 차이(정규화 좌표).

##### `MotionTrack(points=None, speed=0.4)` — `poco/utils/track.py:41`

| 메서드 | 동작 |
|---|---|
| `start(p)`/`move(p)` | 포인트 추가 (이동 거리/speed로 timestamp 누적) |
| `hold(t)` | t초 정지 (같은 위치 재추가) |
| `set_contact_id(_id)` | 모든 event_point의 contact id 설정 |
| `discretize(contact_id=0, accuracy=0.004, dt=0.001)` | 모션 트랙을 이산 이벤트(`['d',...]`/`['m',...]`/`['u',...]`/`['s',dt]`)로 |

- `speed` 기본 `0.4`, `last_point` property.

##### `MotionTrackBatch(tracks)` — `poco/utils/track.py:120`

여러 트랙을 멀티터치 이벤트 시퀀스로 병합. `discretize(accuracy=0.004)` (최소 `0.001`로 클램프). sleep 이벤트 병합 처리.

##### `make_pinching(direction, center, size, percent, dead_zone, duration)` — `poco/utils/multitouch_gesture.py:7`

핀치(`'in'`/`'out'`) 두 손가락 `MotionTrack` 쌍 생성. `speed = sqrt(w*h)*(percent-dead_zone)/2/duration`. `make_panning()`은 미구현(`pass`).

#### `AirtestInput(InputInterface)` — `poco/utils/airtest/input.py:39`

Poco 정규화 좌표 조작을 airtest 실제 touch/swipe로 변환하는 input 구현.

| 항목 | 값/동작 |
|---|---|
| `default_touch_down_duration` | `0.01` |
| `get_target_pos(x, y)` | 정규화 (x,y) → 실제 해상도 좌표 `[x*pw+offsetx, y*ph+offsety]` |
| `_get_touch_resolution()` | Android+`use_render_resolution`면 render resolution, 아니면 현재 해상도 `(0,0,w,h)` |
| `click(x,y)` | `touch(pos, duration=...)` |
| `double_click(x,y)` | `double_click(pos)` |
| `swipe(x1,y1,x2,y2,duration=2.0)` | duration≤0이면 ValueError, `steps=int(duration*40)+1` |
| `longClick(x,y,duration=2.0)` | duration≤0 ValueError |
| `applyMotionEvents(events)` | Android minitouch/maxtouch 전용. `d/m/u/s` 이벤트 → `DownEvent/MoveEvent/UpEvent/SleepEvent`. 비-Android면 NotImplementedError |
| `add_preaction_cb(driver)` | driver에 `record_ui` pre-action 콜백 등록 |

#### `AirtestScreen(ScreenInterface)` — `poco/utils/airtest/screen.py:8`

| 메서드 | 동작 |
|---|---|
| `getPortSize()` | orientation(1,3)이면 `[height, width]`, 아니면 `[width, height]` |
| `getScreen(width)` | `snapshot()` 후 base64 인코드 `(b64, 'png')` 반환 |

#### `default_device()` / `VirtualDevice` — `poco/utils/device.py`

`default_device()`: 현재 device, 없으면(`NoDeviceError`) `connect_device('Android:///')`. `VirtualDevice(ip)`: uuid `'virtual-device'`, 해상도 `[1920,1080]`.

#### `PIDController(ControllerBase)` — `poco/utils/regulator.py:30`

PID 제어기. `__init__(period, Kp=1, Ki=0, Kd=0, ValueType=float)`. `delta_closed_loop_gain(feedback)`(증분형), `closed_loop_gain(feedback)`(위치형). swipe/scroll 정밀 제어 등에 사용 가능한 일반 제어기.

#### `point_inside(p, bounds)` — `poco/utils/measurement.py:4`

점이 bounds 안인지 판정: `bounds[3] <= p[0] <= bounds[1] and bounds[0] <= p[1] <= bounds[2]`. (bounds = [top, right, bottom, left] 형태)

---

## 6. SDK 통합 요약 (implementation_guide.rst)

poco-sdk는 python/js/lua/c#/java 지원. 구현해야 할 함수:

| 함수 | 필수 | 설명 |
|---|---|---|
| `Dump(onlyVisibleNode)` | 필수 | `YourDumper().dumpHierarchy(...)` — 인자는 받기만 하면 됨 |
| `Screenshot(self, width)` | 선택 | `[base64..., "bmp"]` 반환 |
| `Click(self, x, y)` | 필수 | x,y는 **percentage**. `x=Left+Width*x`, `y=Top+Height*y` |
| `Swipe(self, x1,y1,x2,y2,duration)` | 필수 | |
| `LongClick(self, x, y, duration)` | 필수 | |
| `GetScreenSize`/`GetSDKVersion` | 선택 | |

구현해야 할 추상 클래스 2개:

- **`AbstractNode`** — override 4개: `getParent`, `getChildren`(iterator), `getAttr`, `setAttr`. 선택: `getAvailableAttributeNames`.
  - `anchor`/`pos`/`size` 속성은 정해진 포맷으로 반환해야 함(warning).
- **`AbstractDumper`** — override 1개: `getRoot` (디바이스 root surface 반환).

RPC 서버 기동 (SDK 측):

```python
from poco.sdk.std.rpc.controller import StdRpcEndpointController
from poco.sdk.std.rpc.reactor import StdRpcReactor
from poco.utils.net.transport.tcp import TcpSocket

reactor = StdRpcReactor()
reactor.register('Dump', Dump)
reactor.register('Click', Click)  # ...
transport = TcpSocket()
transport.bind(("localhost", 15004))
rpc = StdRpcEndpointController(transport, reactor)
rpc.serve_forever()
```

> `TcpSocket`(`poco/utils/net/transport/tcp.py`)이 SDK 서버 소켓으로 바로 쓰인다 — 본 SCOPE의 net transport가 SDK 통합 진입점.

---

## 7. NetEase 내부 표준 템플릿 (netease-internal-use-template.rst)

자동화 테스트를 **공학(engineering)** 으로 보고 프로젝트 구조화 권장.

```
my_testflow/
  testflow/            # 커스텀 이름
    lib/  (case.py, player.py)
    scripts/ (test1.py, ...)
  res/
  pocounit-results/
  setup.py / requirements.txt / .gitignore
```

- `lib/player.py`: hunter/poco/airtest를 추상 격리하는 `Player`(Singleton). `Poco = NeteasePoco`, `PROCESS='g62'`(hunter 프로젝트 코드). `server_call(cmd)` = `hunter.script(cmd, lang='text')`.
- `lib/case/netease_case.py`: `CommonCase(PocoTestCase)`. `setUpClass`에서 device 연결 + `ActionTracker`/`AppRuntimeLogging` addon 등록. `poco`/`hunter` property.
- `scripts/test1.py`: `runTest` 필수, `setUp`/`tearDown` 선택. **전역 변수로 테스트 객체 저장 금지**. 모든 문장은 `self.`로 시작(동적 프록시 대비). 단언은 표준 unittest와 동일(`self.assertTrue(...)`).
- 의존성: `pip install -e .`, `pip install -i https://pypi.nie.netease.com/ airtest_hunter`.
- 실행: `python testflow/scripts/test1.py`.

---

## 부록 — 핵심 상수·기본값 표

| 상수 / 기본값 | 값 | 위치 |
|---|---|---|
| `TcpClient.DEFAULT_ADDR` | `("0.0.0.0", 5001)` | `simplerpc/transport/tcp/main.py:8` |
| `WebSocketClient.DEFAULT_ADDR` | `"ws://localhost:5003"` | `simplerpc/transport/ws/main.py:8` |
| safetcp `DEFAULT_TIMEOUT` / `DEFAULT_SIZE` | `5` / `4096` | `.../tcp/safetcp.py:5-6` |
| `SimpleProtocolFilter.HEADER_SIZE` | `4` | `.../tcp/protocol.py:8` |
| `Connection.RX_SIZE` / `TcpSocket.RX_SIZE` | `65536` | `net/transport/tcp.py:23,61` |
| `TcpSocket.update` timeout | `0.002` | `net/transport/tcp.py:106` |
| `WsSocket.update` timeout | `0.001` | `net/transport/ws.py:62` |
| WebSocket `MAXHEADER` / `MAXPAYLOAD` | `65536` / `33554432` | `simple_wss.py:80-81` |
| `RpcClient.connect` timeout | `10` | `simplerpc/rpcclient.py:20` |
| `Callback.wait` sleep / `sync_wrapper` timeout | `0.005` / `30` | `simplerpc/simplerpc.py:76`, `utils.py:13` |
| `RpcAgent.run` loop sleep | `0.002` | `simplerpc/simplerpc.py:197` |
| `retries_when` count / delay | `3` / `0.0` | `retry.py:8` |
| `RemotePocoHierarchy` 재시도 delay | `3.0` | `hrpc/hierarchy.py:21,26,31,35` |
| `MotionTrack.speed` | `0.4` | `track.py:42` |
| `track_sampling.accuracy` | `0.002` | `track.py:8` |
| `MotionTrack.discretize` accuracy/dt | `0.004` / `0.001` | `track.py:78` |
| `MotionTrackBatch.discretize` accuracy(min) | `0.004` (≥`0.001`) | `track.py:125-127` |
| `AirtestInput.default_touch_down_duration` | `0.01` | `airtest/input.py:42` |
| `AirtestInput.swipe`/`longClick` duration | `2.0` | `airtest/input.py:87,95` |
| `VirtualDevice` resolution | `[1920, 1080]` | `device.py:19` |
| json-rpc 버전 | `1.10.3` | `simplerpc/jsonrpc/__init__.py:1` |
| report `LOGDIR` / `poco_func` | `"log"` / `["record_ui"]` | `airtest/report.py:4-5` |

## 부록 — 모듈 호출/상속 관계

- `RemotePocoHierarchy`(hrpc) → `HierarchyInterface` 구현, `dumper`/`selector`/`attributor`에 위임, `retry.retries_when` + `hrpc.utils.transform_node_has_been_removed_exception` 데코레이터.
- `RpcClient` → `RpcAgent`(simplerpc) 상속. `RpcAgent.handle_request` → `jsonrpc.JSONRPCResponseManager.handle` + `dispatcher`.
- `RpcClient.conn` → `IClient`(`TcpClient`/`WebSocketClient`). `TcpClient` → `safetcp.Client` + `SimpleProtocolFilter`.
- `StdBroker` → `WsSocket`/`TcpSocket`(net). 둘 다 `poco.sdk.std.transport.Transport` 상속. `WsSocket` → `SimpleWebSocketServer`/`WebSocket`(simple_wss).
- SDK 측: `StdRpcEndpointController`(SCOPE 밖) → `net.transport.tcp.TcpSocket.bind`.
- `AirtestInput`/`AirtestScreen` → `poco.sdk.interfaces`의 `InputInterface`/`ScreenInterface` 구현. `AirtestInput`이 `multitouch_gesture`/`track`의 모션 이벤트를 `applyMotionEvents`로 소비.
- `HunterCommand` → `CommandInterface` 구현, `hunter.script` 호출.
