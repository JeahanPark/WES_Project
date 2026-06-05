---
type: reference
source: Poco (AirtestProject/Poco)
generated: subagent
---

# Poco SDK Internals & 신규 엔진 이식 가이드 (poco/sdk)

이 문서는 `poco/sdk/` 패키지를 다룬다. 이 패키지는 **엔진 비종속(engine-agnostic)** 핵심 로직 — UI 트리 추상화(`AbstractNode`), 직렬화(`AbstractDumper`), 속성 접근(`Attributor`), 질의/매칭(`Selector`/`DefaultMatcher`), 통신 인터페이스(`interfaces/`), 표준 RPC/프로토콜 구현(`std/`) — 을 정의한다. 새 게임 엔진에 Poco를 이식한다는 것은 곧 이 패키지의 추상 클래스/인터페이스를 엔진 객체로 래핑·구현하는 작업이다.

> 핵심 한 줄: **엔진 종속 부분은 오직 `AbstractNode` 구현(노드 래퍼)뿐이다.** dump·select·match·attr 로직은 모두 노드의 `getChildren()` / `getParent()` / `getAttr()` / `setAttr()` 4개 메서드에만 의존하므로 알고리즘화되어 재사용된다.

---

## 0. 패키지 레이아웃 & 의존 관계

| 파일 | 역할 | 주요 export |
|------|------|-------------|
| `poco/sdk/__init__.py` | 버전 상수만 (`__version__ = '1.0.0.0'`) | — |
| `poco/sdk/AbstractNode.py` | UI 트리 노드 추상 래퍼. **엔진 이식 시 구현 대상 1순위** | `AbstractNode` |
| `poco/sdk/AbstractDumper.py` | 트리 → JSON 직렬화기 (DFS 순회) | `IDumper`, `AbstractDumper` |
| `poco/sdk/Attributor.py` | 노드 attr get/set 헬퍼 (list/단일 노드 정규화) | `Attributor` |
| `poco/sdk/DefaultMatcher.py` | 질의 조건 ↔ 노드 매칭 (logical/comparator) | `IMatcher`, `DefaultMatcher`, `EqualizationComparator`, `RegexpComparator` |
| `poco/sdk/Selector.py` | 질의 표현식 기반 노드 선택 (DFS + 관계 연산자) | `ISelector`, `Selector` |
| `poco/sdk/exceptions.py` | SDK 런타임 예외 6종 | (아래 표 참조) |
| `poco/sdk/interfaces/__init__.py` | 통신 인터페이스 패키지 docstring | — |
| `poco/sdk/interfaces/hierarchy.py` | 계층 통신 인터페이스 | `HierarchyInterface` |
| `poco/sdk/interfaces/screen.py` | 스크린/해상도 인터페이스 | `ScreenInterface` |
| `poco/sdk/interfaces/command.py` | 커맨드 인터페이스 | `CommandInterface` |
| `poco/sdk/interfaces/input.py` | 입력(터치/키) 인터페이스 | `InputInterface` |
| `poco/sdk/std/protocol.py` | 길이-접두 TCP 프레이밍 필터 | `SimpleProtocolFilter` |
| `poco/sdk/std/rpc/reactor.py` | JSON-RPC 2.0 리액터 (요청/응답 빌드·디스패치) | `StdRpcReactor`, `NoSuchMethod` |
| `poco/sdk/std/rpc/controller.py` | RPC 엔드포인트 컨트롤러 (serve/call 루프) | `StdRpcEndpointController`, `RpcRemoteException` |
| `poco/sdk/std/transport/__init__.py` | 전송 계층 추상 인터페이스 | `Transport` |

### 호출/상속 그래프 (sdk 내부)

```
Selector ── 사용 ──▶ IDumper(getRoot)           # 트리 루트 획득
   │                  AbstractNode(getChildren/getParent/getAttr)
   └── 사용 ──▶ IMatcher.match(cond, node)       # 기본값 DefaultMatcher
                       │
                       └── EqualizationComparator / RegexpComparator
                                  │
                                  └── AbstractNode.getAttr()

AbstractDumper(IDumper)
   └── dumpHierarchyImpl ──▶ AbstractNode.enumerateAttrs / getChildren / getAttr

Attributor ──▶ AbstractNode.getAttr / setAttr

HierarchyInterface  = IDumper + Attributor + Selector 의 통합 외관(facade)
```

`HierarchyInterface`(`poco/sdk/interfaces/hierarchy.py:4`)는 위 3요소(`IDumper`·`Attributor`·`Selector`)를 한 묶음으로 노출하는 통신 인터페이스다. 즉 이식 시 `select`/`dump`/`getAttr`/`setAttr` 4개를 이 인터페이스로 모아 원격 런타임에 노출한다.

---

## 1. AbstractNode — UI 트리 노드 래퍼 (이식 1순위)

파일: `poco/sdk/AbstractNode.py`

`AbstractNode`는 게임 엔진의 UI 계층/노드 정보를 **통일된 형태로 래핑**하는 추상 클래스다. 부모/자식 접근, 속성 get/set을 통일적으로 규정한다. `Selector`와 `Dumper`가 트리를 순회할 때 이 메서드들을 호출한다.

### 1.1 `getParent(self)`

```python
def getParent(self):
    return None
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| (없음) | — | — | — |

- 반환: `AbstractNode` 또는 `None`.
- 동작: 이 노드의 부모 노드 반환. 부모가 없거나 접근 불가하거나 루트 노드이면 `None`. 기본 구현은 `None` 반환.
- 호출처: `Selector.selectImpl`의 sibling(`-`)·parent(`^`) 연산 (`poco/sdk/Selector.py:131`, `:147`).
- 주의점: sibling/parent 질의를 지원하려면 반드시 오버라이드해야 한다(기본 `None`은 해당 연산을 무력화).

### 1.2 `getChildren(self)`

```python
def getChildren(self):
    raise NotImplementedError
```

- 반환: `Iterable<AbstractNode>` — 모든 자식 노드의 이터레이터.
- 동작: `Selector`/`Dumper`가 트리를 내려갈 때 호출.
- 주의점: **기본 구현이 `NotImplementedError`** — 이식 시 반드시 구현해야 하는 필수 메서드.

### 1.3 `getAttr(self, attrName)`

```python
def getAttr(self, attrName):
    attrs = {
        'name': '<Root>',
        'type': 'Root',
        'visible': True,
        'pos': [0.0, 0.0],
        'size': [0.0, 0.0],
        'scale': [1.0, 1.0],
        'anchorPoint': [0.5, 0.5],
        'zOrders': {'local': 0, 'global': 0},
    }
    return attrs.get(attrName)
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `attrName` | `str` | (필수) | 가져올 속성 이름 |

- 반환: JSON 직렬화 가능한 속성 값, 또는 해당 속성이 없으면 `None`.
- 동작: 노드 속성을 반환. 구현 클래스는 값을 알면 즉시 반환하고, 결정 불가하면 `super().getAttr(attrName)`로 위임해 기본값을 얻는 것이 권장 패턴.
- 권장 구현 패턴(소스 docstring 발췌):

```python
def getAttr(self, attrName):
    if attrName == 'name':
        return self.node.get_name() or '<no name>'
    elif attrName == 'pos':
        return self.node.get_position()
    # ... 엔진 고유 속성
    elif attrName == 'rotation':
        return self.node.get_rotation()
    else:
        return super().getAttr(attrName)   # 기본값 위임
```

- 주의점: 기본 dict 값은 **루트 노드 기준** (`name='<Root>'`, `type='Root'`). 실제 노드는 반드시 오버라이드. `visible`이 `False`면 Poco selector는 그 노드의 자식 전부를 무시한다.

### 1.4 `setAttr(self, attrName, val)`

```python
def setAttr(self, attrName, val):
    raise UnableToSetAttributeException(attrName, None)
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `attrName` | `str` | (필수) | 변경할 속성 이름 |
| `val` | any | (필수) | 새 속성 값 |

- 반환: 성공 시 `True`, 실패 시 `False` 또는 예외.
- 동작: 속성 변경을 노드에 적용. 모든 속성이 변경 가능한 것은 아니며 가장 흔한 변경 대상은 `text`.
- 주의점: 기본 구현은 항상 `UnableToSetAttributeException`(`poco/sdk/exceptions.py:29`) 발생. `position`/`name` 등의 변경은 false-positive 오류를 유발하므로 비권장.
- 연관: `HierarchyInterface.setAttr`(`poco/sdk/interfaces/hierarchy.py:52`).

### 1.5 `getAvailableAttributeNames(self)`

```python
def getAvailableAttributeNames(self):
    return ("name", "type", "visible", "pos", "size",
            "scale", "anchorPoint", "zOrders",)
```

- 반환: `Iterable<str>` — 사용 가능한 모든 속성 이름.
- 동작: 기본은 8개 표준 속성 이름 튜플 반환. 엔진 고유 속성을 추가할 때는 super 호출 결과에 더하는 것이 권장.
- 권장 패턴:

```python
def getAvailableAttributeNames(self):
    return super().getAvailableAttributeNames() + ('rotation',)
```

- 주의점: Inspector 표시·selection 향상을 위해 추가 속성을 노출할 수 있으나, super 결과를 항상 포함시킬 것.

### 1.6 `enumerateAttrs(self)`

```python
def enumerateAttrs(self):
    for attrName in self.getAvailableAttributeNames():
        yield attrName, self.getAttr(attrName)
```

- yield: `(name, value)` 2-튜플 시퀀스.
- 동작: 사용 가능 속성 전부를 (이름, 값) 쌍으로 yield. `AbstractDumper.dumpHierarchyImpl`이 payload 생성에 사용(`poco/sdk/AbstractDumper.py:102`).

---

## 2. AbstractDumper — 트리 → JSON 직렬화

파일: `poco/sdk/AbstractDumper.py`

### 2.1 `IDumper` (인터페이스)

| 메서드 | 시그니처 | 반환 | 설명 |
|--------|----------|------|------|
| `getRoot(self)` | `getRoot(self)` | `AbstractNode` 파생 인스턴스 | UI 계층 루트 노드 반환. **`NotImplementedError`** — 구현 필수 |
| `dumpHierarchy(self, onlyVisibleNode)` | `dumpHierarchy(self, onlyVisibleNode)` | `dict` 또는 `None` | 계층 데이터를 JSON 직렬화 가능 dict로 반환. **`NotImplementedError`** |

### 2.2 `AbstractDumper(IDumper)`

`IDumper`를 **일반 순회 알고리즘**으로 부분 구현. 루트의 속성과 자식 목록을 얻고, 각 자식을 루트처럼 재귀 처리(DFS)하여 자식 없는 노드까지 내려간다. 이식 시에는 보통 `getRoot()`만 엔진별로 구현하면 된다.

#### `dumpHierarchy(self, onlyVisibleNode=True)`

```python
def dumpHierarchy(self, onlyVisibleNode=True):
    return self.dumpHierarchyImpl(self.getRoot(), onlyVisibleNode)
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `onlyVisibleNode` | `bool` | `True` | 가시 노드만 dump할지 여부 |

- 반환: 전체 계층 데이터를 담은 JSON 직렬화 가능 `dict`.

#### `dumpHierarchyImpl(self, node, onlyVisibleNode=True)`

```python
def dumpHierarchyImpl(self, node, onlyVisibleNode=True):
    if not node:
        return None
    payload = {}
    for attrName, attrVal in node.enumerateAttrs():
        if attrVal is not None:
            payload[attrName] = attrVal
    result = {}
    children = []
    for child in node.getChildren():
        if not onlyVisibleNode or child.getAttr('visible'):
            children.append(self.dumpHierarchyImpl(child, onlyVisibleNode))
    if len(children) > 0:
        result['children'] = children
    result['name'] = payload.get('name') or node.getAttr('name')
    result['payload'] = payload
    return result
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `node` | `AbstractNode` 파생 | (필수) | dump할 계층의 루트 노드 |
| `onlyVisibleNode` | `bool` | `True` | 가시 노드만 / 전체 노드 |

- 반환: 계층 데이터 `dict`.
- 핵심 동작:
  1. `node`가 falsy면 `None` 반환(빈 가지 종료).
  2. `enumerateAttrs()`로 모든 속성을 순회하되 **`None` 값은 payload에서 제외**(필터링).
  3. `getChildren()` 각 자식에 대해 `onlyVisibleNode=False`이거나 자식의 `visible`이 truthy일 때만 재귀 후 `children`에 추가.
  4. 자식이 1개 이상이면 `result['children']`에 넣음(없으면 키 자체를 생략).
  5. `result['name']` = payload의 `name` 또는 `node.getAttr('name')`.
  6. `result['payload']` = 위에서 만든 속성 dict.
- 주의점: 내부 메서드 — 직접 호출 금지, `dumpHierarchy()`를 호출할 것. 가시성 필터링은 자식 단계에서 적용되므로, 보이지 않는 노드는 자기 자신과 그 하위 전체가 통째로 빠진다.

---

## 3. Hierarchy Dump JSON 포맷 명세

`dumpHierarchyImpl`이 생성하는 dict 구조(소스 docstring `poco/sdk/AbstractDumper.py:30` 기준):

```jsonc
{
  // 노드 식별용 이름. payload.name 또는 getAttr('name')에서 유도
  "name": "<a recognizable string>",

  // 이 노드의 사용 가능 속성 전부(None 제외)를 key-value로
  "payload": {
    "name": "",
    "type": "Button",
    "visible": true,
    "pos": [0.5, 0.5],
    "size": [0.5, 1.0],
    "scale": [1.0, 1.0],
    "anchorPoint": [0.5, 0.5],
    "zOrders": {"local": 0, "global": 0}
    // ... 엔진 고유 추가 속성
  },

  // 자식이 없으면 이 키 자체가 생략됨
  "children": [
    { /* 동일 구조 재귀 */ }
  ]
}
```

### 3.1 표준 attribute 이름 / 의미 (payload 키)

`AbstractNode.getAttr`(`poco/sdk/AbstractNode.py:96`) 기본 dict 및 docstring 기준.

| attr 이름 | 타입 | 기본값(루트) | 의미 / 좌표계 |
|-----------|------|--------------|----------------|
| `name` | `str` | `'<Root>'` | 노드 이름. 각 노드마다 고유·의미 있는 이름 권장 |
| `type` | `str` | `'Root'` | 노드 타입명. `"android.widget.Button"` 같은 정식명 또는 `"Button"` 단순명 모두 가능 |
| `visible` | `bool` | `True` | 화면에 렌더링되는지. `False`면 Poco selector가 자식 전부 무시 |
| `pos` | `[float, float]` | `[0.0, 0.0]` | 화면 내 위치 `(x, y)`, **화면 비율(percentage)** 좌표. 화면 중앙이면 `[0.5, 0.5]`. 음수면 화면 밖 |
| `size` | `[float, float]` | `[0.0, 0.0]` | 바운딩 박스 크기. 화면 크기 대비 비율. 전체 화면 = `[1.0, 1.0]`, 좌측 절반 차지 = `[0.5, 1.0]`. 항상 양수 |
| `scale` | `[float, float]` | `[1.0, 1.0]` | 노드 자체 스케일 팩터. 기본 `[1.0, 1.0]` |
| `anchorPoint` | `[float, float]` | `[0.5, 0.5]` | 바운딩 박스 대비 앵커 위치 비율 `(x, y)`. 기본 중앙 `[0.5, 0.5]` |
| `zOrders` | `{"global": int, "local": int}` | `{'local': 0, 'global': 0}` | 렌더 순서. `global`은 전체 계층과, `local`은 부모·형제와 비교. 값이 클수록 위(최상단) |

추가로 docstring에서 언급되는 엔진 고유 속성 예: `text`(setAttr 변경 대상), `rotation`(getAttr/getAvailableAttributeNames 확장 예시). 좌표 비율 좌표계는 `InputInterface` docstring의 `NormalizedCoordinate` 시스템(0~1 범위)과 정합한다.

---

## 4. Attributor — 노드 속성 접근 헬퍼

파일: `poco/sdk/Attributor.py`

명시적으로 노드 멤버 함수를 호출할 수 없는 경우를 위한 헬퍼. `HierarchyInterface` 구현에서 사용된다. **테스트 코드에서 직접 호출 금지.**

```python
class Attributor(object):
    def getAttr(self, node, attrName):
        if type(node) in (list, tuple):
            node_ = node[0]
        else:
            node_ = node
        return node_.getAttr(attrName)

    def setAttr(self, node, attrName, attrVal):
        if type(node) in (list, tuple):
            node_ = node[0]
        else:
            node_ = node
        node_.setAttr(attrName, attrVal)
```

| 메서드 | 파라미터 | 동작 |
|--------|----------|------|
| `getAttr(self, node, attrName)` | `node`(노드 또는 list/tuple), `attrName`(`str`) | node가 list/tuple이면 **첫 원소**(`node[0]`)를, 아니면 node 자체를 대상으로 `getAttr` 호출 |
| `setAttr(self, node, attrName, attrVal)` | `node`, `attrName`(`str`), `attrVal` | 동일하게 첫 원소 또는 node에 `setAttr` 위임. 반환 없음 |

- 주의점: list/tuple이 들어오면 다중 선택 결과 중 첫 번째에만 적용. `setAttr`는 내부적으로 `AbstractNode.setAttr` 실패 시 `UnableToSetAttributeException`을 전파.

---

## 5. DefaultMatcher — 질의 조건 ↔ 노드 매칭

파일: `poco/sdk/DefaultMatcher.py`

### 5.1 `IMatcher`

```python
class IMatcher(object):
    def match(self, cond, node):
        raise NotImplementedError
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `cond` | `tuple` | 질의 표현식 |
| `node` | `AbstractNode` 파생 | 검사 대상 노드 |

- 반환: `bool` — 매칭이면 `True`.

### 5.2 `EqualizationComparator`

```python
def compare(self, l, r):
    return l == r
```
- 네이티브 `==` 동등 비교. `op='attr='`에 매핑.

### 5.3 `RegexpComparator`

```python
def compare(self, origin, pattern):
    if origin is None or pattern is None:
        return False
    return re.match(pattern, origin) is not None
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `origin` | `str` | 원본 문자열(노드 속성 값) |
| `pattern` | `str` | 정규식 패턴 |

- 반환: `bool`. 원본 또는 패턴이 `None`이면 `False`. `re.match`(앞 고정 매칭)로 판정. `op='attr.*='`에 매핑.
- 주의점: `re.match`는 문자열 **시작부터** 매칭한다(부분 검색 아님). 원본/패턴이 `str`이 아니면 매칭 실패.

### 5.4 `DefaultMatcher(IMatcher)`

질의 조건 형식(소스 docstring `poco/sdk/DefaultMatcher.py:61`):

```
expr := (op0, (expr0, expr1, ...))   # 논리 연산
expr := (op1, (arg1, arg2))          # 속성 술어(predicate)
```

- `op0` (논리 연산자): `'and'` / `'or'` — Python과 동일 의미.
- `op1` (비교 연산자):

| op1 | 매핑 Comparator | 의미 |
|-----|-----------------|------|
| `'attr='` | `EqualizationComparator` | 속성 값 == 지정 값 |
| `'attr.*='` | `RegexpComparator` | 속성 값이 정규식 패턴에 매칭 |

생성자가 comparator 맵을 구성:

```python
def __init__(self):
    super(DefaultMatcher, self).__init__()
    self.comparators = {
        'attr=': EqualizationComparator(),
        'attr.*=': RegexpComparator(),
    }
```

`match(self, cond, node)` 동작:

```python
def match(self, cond, node):
    op, args = cond
    if op == 'and':
        for arg in args:
            if not self.match(arg, node):
                return False
        return True
    if op == 'or':
        for arg in args:
            if self.match(arg, node):
                return True
        return False
    comparator = self.comparators.get(op)
    if comparator:
        attribute, value = args
        targetValue = node.getAttr(attribute)
        return comparator.compare(targetValue, value)
    raise NoSuchComparatorException(op, 'poco.sdk.DefaultMatcher')
```

- `'and'`: 모든 하위 expr이 매칭해야 `True`.
- `'or'`: 하나라도 매칭하면 `True`.
- 그 외(comparator op): `args = (attribute, value)` 분해 → `node.getAttr(attribute)` 값을 comparator로 비교.
- 미지원 op이면 `NoSuchComparatorException`(`poco/sdk/exceptions.py:52`) 발생.
- 확장: 새 비교 연산자는 `self.comparators`에 op→comparator를 추가하면 된다(예: `attr.*=` 외 커스텀).

---

## 6. Selector — 질의 표현식 기반 노드 선택

파일: `poco/sdk/Selector.py`

### 6.1 `ISelector`

```python
def select(self, cond, multiple=False):
    raise NotImplementedError
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `cond` | `tuple` | (필수) | 질의 표현식 |
| `multiple` | `bool` | `False` | 다중 선택 여부. `False`면 첫 노드 발견 즉시 종료, `True`면 전체 순회 |

- 반환: `list<AbstractNode 파생>`.

### 6.2 `Selector(ISelector)`

표준 Selector. DFS로 트리형 계층을 순회하며 부모-자식 관계, 속성 술어 등 유연한 질의를 지원. 질의 표현식(소스 docstring `poco/sdk/Selector.py:36`):

```
expr := (op0, (expr0, expr1))
expr := ('index', (expr, int))
expr := <기타 질의 조건>  # Matcher로 위임
```

관계 연산자 `op0`:

| op0 | 의미 |
|-----|------|
| `'>'` | offsprings — expr0에 매칭된 모든 루트에서 expr1에 매칭되는 **모든 후손** 선택 |
| `'/'` | children — expr0 매칭 루트에서 expr1 매칭되는 **직속 자식**만 선택 |
| `'-'` | siblings — expr0 매칭 루트에서 expr1 매칭되는 **형제** 선택 |
| `'^'` | parent — expr0에 처음 매칭된 UI 요소의 **부모** 선택 (expr1은 항상 None) |
| `'index'` | 이전 결과에서 **n번째** 요소 선택 |
| 기타 | Matcher로 표현식 위임(속성 술어) |

#### 생성자

```python
def __init__(self, dumper, matcher=None):
    self.dumper = dumper
    self.matcher = matcher or DefaultMatcher()
```

| 파라미터 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `dumper` | `IDumper` 구현 | (필수) | 루트 노드 공급원 |
| `matcher` | `IMatcher` 구현 | `None` → `DefaultMatcher()` | 노드 매칭기 |

#### `getRoot(self)`
```python
def getRoot(self):
    return self.dumper.getRoot()
```
- `dumper.getRoot()` 위임.

#### `select(self, cond, multiple=False)`
```python
def select(self, cond, multiple=False):
    return self.selectImpl(cond, multiple, self.getRoot(), 9999, True, True)
```
- 진입점. 기본 `maxDepth=9999`, `onlyVisibleNode=True`, `includeRoot=True`로 `selectImpl` 호출.

#### `selectImpl(self, cond, multiple, root, maxDepth, onlyVisibleNode, includeRoot)`

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `cond` | `tuple` | 질의 표현식 |
| `multiple` | `bool` | 다중 선택 여부 |
| `root` | `AbstractNode` 파생 | 순회 시작 루트 |
| `maxDepth` | `int` | 최대 순회 깊이 |
| `onlyVisibleNode` | `bool` | `True`면 `visible=False` 노드 스킵 |
| `includeRoot` | `bool` | 자식이 매칭될 때 루트 노드를 결과에 포함할지 |

- 반환: `list<AbstractNode 파생>`.
- 분기 동작:
  - `root`가 falsy → 빈 리스트.
  - `op in ('>', '/')`: `parents=[root]`에서 각 arg를 순차 적용. `'/'`이고 첫 arg가 아니면 `_maxDepth=1`(직속 자식만), 그 외 `maxDepth` 유지. 경로 순회 시 누락 방지를 위해 내부 호출은 항상 `multiple=True`, `includeRoot=False`. 중복 없이 `midResult`에 누적. (`poco/sdk/Selector.py:109`)
  - `op == '-'` (sibling): `query1` 결과 각각에 대해 `n.getParent()`를 루트로 `query2`를 `maxDepth=1`로 선택, 중복 없이 결과 누적. (`:125`)
  - `op == 'index'`: `(cond, i)` 분해 → `multiple=True`로 선택한 리스트의 `[i]` 한 개. `IndexError` 시 `NoSuchTargetException`(`poco/sdk/exceptions.py:41`). (`:133`)
  - `op == '^'` (parent): `query1`을 `multiple=False`로 선택, 첫 결과의 `getParent()`가 `None`이 아니면 그것 하나. (`:141`)
  - 그 외: `_selectTraverse`로 위임. (`:151`)

#### `_selectTraverse(self, cond, node, outResult, multiple, maxDepth, onlyVisibleNode, includeRoot)`

```python
def _selectTraverse(self, cond, node, outResult, multiple, maxDepth, onlyVisibleNode, includeRoot):
    if onlyVisibleNode and not node.getAttr('visible'):
        return False
    if self.matcher.match(cond, node):
        if includeRoot:
            if node not in outResult:
                outResult.append(node)
            if not multiple:
                return True
    if maxDepth == 0:
        return False
    maxDepth -= 1
    for child in node.getChildren():
        finished = self._selectTraverse(cond, child, outResult, multiple, maxDepth, onlyVisibleNode, True)
        if finished:
            return True
    return False
```

- 핵심 동작:
  1. `onlyVisibleNode`이고 노드가 비가시 → 해당 가지 가지치기(`False` 반환, 자식 순회 안 함).
  2. `matcher.match(cond, node)` 성공 + `includeRoot`이면 중복 없이 `outResult` 추가. `multiple=False`면 즉시 `True`(완료) 반환.
  3. `maxDepth==0`이면 종료. 아니면 `maxDepth -= 1` 후 자식 순회 — **자식 순회 시 `includeRoot`는 항상 `True`**(각 자식은 그 자체로 루트 취급).
  4. 자식 중 하나라도 `finished=True`면 전파 종료.
- 주의점: 비가시 노드는 자식까지 통째로 가지치기되므로 dump 가시성 필터와 동일 의미. `maxDepth` 소진은 "현재 가지" 종료일 뿐, 형제 가지는 계속 순회될 수 있다(주석 참조 `:171`).

---

## 7. exceptions.py — SDK 예외

파일: `poco/sdk/exceptions.py`

| 예외 | 발생 시점 | 생성자 시그니처 |
|------|-----------|------------------|
| `NodeHasBeenRemovedException` | 순회 중 속성 조회 시 노드가 갱신/재활용/파괴되어 더 이상 살아있지 않을 때(엔진이 별도 스레드로 계층 갱신하는 경우). 메시지: `Node was no longer alive when query attribute "{}". Please re-select.` | `(attrName, node)` |
| `UnableToSetAttributeException` | 속성 변경 실패(노드가 mutation 미지원, 또는 구현이 변경 불허). `AbstractNode.setAttr` 기본 구현에서 항상 발생. 메시지: `Unable to set attribute "{}" of node "{}".` | `(attrName, node)` |
| `NoSuchTargetException` | `index` 선택 시 인덱스 범위 초과. `Selector.selectImpl`의 `index` 분기에서 발생 | `pass` (기본 Exception) |
| `NoSuchComparatorException` | Matcher가 주어진 비교 방법을 지원하지 않을 때. `DefaultMatcher.match`에서 발생. `self.message`에 상세 저장 | `(matchingMethod, matcherName)` |
| `NonuniqueSurfaceException` | 디바이스 selector가 다수 device surface에 매칭될 때 | `(selector)` |
| `InvalidSurfaceException` | device surface가 유효하지 않을 때 | `(target, msg="None")` |

`__all__` = 위 6개. `NoSuchTargetException`은 docstring상 `poco.exceptions.PocoNoSuchNodeException`과 다소 중복(향후 최적화 TODO).

---

## 8. interfaces/ — 통신 인터페이스 표준

파일: `poco/sdk/interfaces/`. 패키지 docstring(`__init__.py`) 요지: 이 인터페이스들은 **poco ↔ poco-sdk 간 통신 표준**을 정의한다. poco-sdk가 다른 호스트/언어로 통합되면 `remote runtime`이라 부르며, 구현은 원격/로컬 어느 쪽이든 가능하다. 동일 인터페이스를 구현하는 객체는 교체 가능하며 통신 프로토콜/전송 계층 제약이 없다(일부는 HTTP, 일부는 TCP로 혼용 가능). 로컬 구현은 `poco.freezeui` 참조.

### 8.1 인터페이스 요약 표

| 인터페이스 | 파일 | 메서드 | 시그니처 | 반환 / 비고 |
|-----------|------|--------|----------|-------------|
| `HierarchyInterface` | `hierarchy.py:4` | `select` | `select(self, query, multiple)` | UI 요소 list. (= IDumper+Attributor+Selector 통합) |
| | | `dump` | `dump(self)` | 계층 구조 `dict` (IDumper 명세) |
| | | `getAttr` | `getAttr(self, nodes, name)` | 속성 값. nodes가 list면 첫 요소만 |
| | | `setAttr` | `setAttr(self, nodes, name, value)` | 없음. 실패 시 `UnableToSetAttributeException` |
| `ScreenInterface` | `screen.py:5` | `getScreen` | `getScreen(self, width)` | `2-list (b64img:str, format:str)` — base64 화면, png/jpg 등 |
| | | `getPortSize` | `getPortSize(self)` | `2-list (float, float)` — 화면 실해상도(px) |
| `CommandInterface` | `command.py:6` | `command` | `command(self, cmd, type)` | `None` 권장. 원격 런타임에 self-defined 명령 전송 |
| `InputInterface` | `input.py:7` | (아래 8.2) | — | 좌표는 0~1 NormalizedCoordinate |

> `HierarchyInterface`(`hierarchy.py`)의 4개 메서드는 모두 기본 `NotImplementedError`. 이식 시 `IDumper`/`Attributor`/`Selector` 구현을 이 인터페이스로 묶어 노출한다.

### 8.2 `InputInterface` 메서드 (`input.py`)

모든 좌표 인자는 0~1 범위(NormalizedCoordinate).

| 메서드 | 시그니처 | 파라미터 | 기본 구현 |
|--------|----------|----------|-----------|
| `click` | `click(self, x, y)` | `x, y` (float, 0~1) | `NotImplementedError` |
| `double_click` | `double_click(self, x, y)` | `x, y` | `NotImplementedError` |
| `swipe` | `swipe(self, x1, y1, x2, y2, duration)` | 시작점 `(x1,y1)`·끝점 `(x2,y2)`·`duration`(float, 초) | `NotImplementedError` |
| `longClick` | `longClick(self, x, y, duration)` | `x, y`, `duration`(float, 초) | `NotImplementedError` |
| `setTouchDownDuration` | `setTouchDownDuration(self, duration)` | `duration`(float, 초) | `warnings.warn` — 미지원 경고만, 효과 없음 |
| `getTouchDownDuration` | `getTouchDownDuration(self)` | — | `NotImplementedError`. 각 구현이 기본값 제공해야 |
| `keyevent` | `keyevent(self, keycode)` | `keycode`(int 또는 char, Ascii) | `NotImplementedError` |
| `applyMotionEvents` | `applyMotionEvents(self, events)` | `events`(list of `['u/d/m/s', (x,y), contact_id]`) | `NotImplementedError` |

- `applyMotionEvents`의 이벤트 코드: `'u'`(up)/`'d'`(down)/`'m'`(move)/`'s'`(sleep) 추정 — 각 이벤트는 `[종류, (x,y), contact_id]`.

---

## 9. std/ — 표준 RPC / 프로토콜 / 전송 구현

이식 시 자체 통신 스택을 직접 구현하지 않고 재사용할 수 있는 참조 구현. (단, `poco.utils.six` 의존 — sdk 외부 모듈)

### 9.1 `std/protocol.py` — `SimpleProtocolFilter`

길이-접두(length-prefixed) TCP 프레이밍 필터. 패킷 포맷: `[유효데이터 바이트수(HEADER_SIZE=4바이트)][유효데이터]`. 전송 시 utf-8 인코딩.

| 멤버 | 시그니처 | 동작 |
|------|----------|------|
| `HEADER_SIZE` | 모듈 상수 `= 4` | 헤더 길이(바이트). `struct` `'i'` (int) 크기와 일치 |
| `__init__` | `__init__(self)` | `self.buf = b''` 버퍼 초기화 |
| `input` | `input(self, data)` | 수신 조각을 `buf`에 누적. `len(buf) > HEADER_SIZE`이고 완전한 패킷이 모이면 content를 `yield`(제너레이터). `struct.unpack('i', ...)`로 길이 파싱 |
| `pack` | `@staticmethod pack(content)` | content가 `six.text_type`이면 utf-8 인코딩 후 `struct.pack('i', len) + content` 반환 |
| `unpack` | `@staticmethod unpack(data)` | `(length, content)` 반환. `struct.unpack('i', data[0:4])` |

- 주의점: `input`은 헤더 길이만큼 데이터가 모일 때까지 `break`하고 대기. 다수 패킷이 한 번에 와도 루프로 모두 yield.

### 9.2 `std/rpc/reactor.py` — `StdRpcReactor`

JSON-RPC 2.0 리액터. 메서드 등록·디스패치·요청/응답 빌드를 담당.

| 멤버 | 시그니처 | 동작 |
|------|----------|------|
| `__init__` | `__init__(self)` | `self.slots={}` (name→method), `self.pending_response={}` (rid→result) |
| `register` | `register(self, name, method)` | `method`가 callable 아니면 `ValueError`. 이미 등록된 name이면 `ValueError`. slots에 등록 |
| `dispatch` | `dispatch(self, name, *args, **kwargs)` | slots에서 method 조회, 없으면 `NoSuchMethod`. `method(*args, **kwargs)` 호출 결과 반환 |
| `handle_request` | `handle_request(self, req)` | `req['method']`·`req['params']`로 dispatch. 성공 시 `ret['result']`, 예외 시 `ret['error']['message']`에 메시지 + REMOTE TRACEBACK 포함. `id`/`jsonrpc` 보존 |
| `handle_response` | `handle_response(self, res)` | `res['id']` → `self.pending_response[id] = res` 저장 |
| `build_request` | `build_request(self, method, *args, **kwargs)` | `uuid4` rid 생성, `{'id', 'jsonrpc':'2.0', 'method', 'params': args or kwargs or []}` 반환, `pending_response[rid]=None` 등록 |
| `get_result` | `get_result(self, rid)` | `pending_response.get(rid)` |

- `NoSuchMethod(Exception)`: `(name, available_methods)` — 메시지 `No such method "{}". Available methods {}`.

### 9.3 `std/rpc/controller.py` — `StdRpcEndpointController`

transport + reactor를 묶어 직렬화/역직렬화와 serve/call 루프를 제공.

| 멤버 | 시그니처 | 동작 |
|------|----------|------|
| `__init__` | `__init__(self, transport, reactor)` | transport, reactor 저장 |
| `deserialize` | `deserialize(self, data)` | PY3이고 text가 아니면 utf-8 decode 후 `json.loads` |
| `serialize` | `serialize(self, packet)` | `json.dumps(packet)` |
| `serve_forever` | `serve_forever(self)` | 무한 루프: `transport.update()` → data 있으면 deserialize. `'method'` 키 있으면 요청 → `reactor.handle_request` → 직렬화 후 `transport.send(cid, ...)`. 아니면 응답 → `reactor.handle_response` |
| `call` | `call(self, method, *args, **kwargs)` | `reactor.build_request`로 요청 생성·전송(`transport.send(None, ...)`), `time.sleep(0.004)` 폴링하며 `reactor.get_result(rid)` 대기. `'result'`면 반환, `'error'`면 `RpcRemoteException(message)`, 그 외 `RuntimeError` |

- `RpcRemoteException(Exception)`: 원격 에러 전파용.
- 주의점: `call`은 4ms 폴링 busy-wait. 응답 매칭은 rid 기반.

### 9.4 `std/transport/__init__.py` — `Transport`

전송 계층 추상 인터페이스. 전부 `NotImplementedError`.

| 메서드 | 시그니처 |
|--------|----------|
| `update` | `update(self, timeout=None)` |
| `send` | `send(self, cid, data)` |
| `recv` | `recv(self)` |
| `connect` | `connect(self, endpoint)` |
| `disconnect` | `disconnect(self, endpoint=None)` |
| `bind` | `bind(self, endpoint)` |

- `StdRpcEndpointController`가 `update()`/`send()`에 의존. 이식 시 엔진/소켓에 맞춰 구현.

---

## 10. 신규 엔진 이식 절차 (implementation guide)

| 단계 | 구현 대상 | 파일 참조 |
|------|-----------|-----------|
| 1 | 엔진 UI 노드를 래핑하는 `AbstractNode` 서브클래스 작성 — `getChildren()`(필수), `getParent()`(sibling/parent 질의용), `getAttr()`(표준 8속성 + 엔진 고유), `setAttr()`(필요 시), `getAvailableAttributeNames()`(고유 속성 추가) 구현 | `poco/sdk/AbstractNode.py` |
| 2 | `AbstractDumper` 서브클래스에서 `getRoot()` 구현 (루트 노드 래퍼 반환). `dumpHierarchy`/`dumpHierarchyImpl`은 그대로 재사용 | `poco/sdk/AbstractDumper.py:13` |
| 3 | `Selector(dumper, matcher)` 인스턴스화 — matcher 기본 `DefaultMatcher`로 충분(커스텀 comparator 필요 시 확장) | `poco/sdk/Selector.py:59` |
| 4 | `Attributor`로 getAttr/setAttr 래핑 | `poco/sdk/Attributor.py` |
| 5 | `HierarchyInterface` 구현 — 위 dumper/selector/attributor를 `select`/`dump`/`getAttr`/`setAttr`로 노출 | `poco/sdk/interfaces/hierarchy.py:4` |
| 6 | `ScreenInterface`(`getScreen`/`getPortSize`), `InputInterface`(click/swipe/keyevent 등), `CommandInterface`(선택) 구현 | `poco/sdk/interfaces/*.py` |
| 7 | 통신: `Transport` 구현 + `StdRpcReactor`에 위 인터페이스 메서드 `register` + `StdRpcEndpointController.serve_forever`. 프레이밍은 `SimpleProtocolFilter` 사용 | `poco/sdk/std/**` |

엔진 종속 코드는 **1단계(노드 래퍼)에 집중**되며, 2~7단계는 대부분 sdk 제공 클래스를 조립/재사용한다. dump·select·match는 노드의 4개 메서드(`getChildren`/`getParent`/`getAttr`/`setAttr`)에만 의존하므로 알고리즘 변경 없이 동작한다.

---

## 부록: 핵심 상수 / 기본 임계치

| 상수/기본값 | 값 | 위치 |
|-------------|-----|------|
| `__version__` | `'1.0.0.0'` | `poco/sdk/__init__.py:3` |
| `Selector.select` 기본 maxDepth | `9999` | `poco/sdk/Selector.py:77` |
| `Selector.select` 기본 onlyVisibleNode | `True` | `poco/sdk/Selector.py:77` |
| `Selector.select` 기본 includeRoot | `True` | `poco/sdk/Selector.py:77` |
| `'/'` 비-첫 인자 자식 깊이 `_maxDepth` | `1` | `poco/sdk/Selector.py:117` |
| sibling 질의 깊이 | `1` | `poco/sdk/Selector.py:131` |
| `DefaultMatcher` 기본 comparators | `{'attr=':Eq, 'attr.*=':Regexp}` | `poco/sdk/DefaultMatcher.py:82` |
| `AbstractNode.getAttr` 기본 pos/size | `[0.0, 0.0]` | `poco/sdk/AbstractNode.py:100` |
| 기본 scale | `[1.0, 1.0]` | `poco/sdk/AbstractNode.py:102` |
| 기본 anchorPoint | `[0.5, 0.5]` | `poco/sdk/AbstractNode.py:103` |
| 기본 zOrders | `{'local': 0, 'global': 0}` | `poco/sdk/AbstractNode.py:104` |
| `HEADER_SIZE` (프로토콜 헤더) | `4` (바이트, struct `'i'`) | `poco/sdk/std/protocol.py:6` |
| RPC `call` 폴링 간격 | `time.sleep(0.004)` (4ms) | `poco/sdk/std/rpc/controller.py:45` |
| JSON-RPC 버전 | `'2.0'` | `poco/sdk/std/rpc/reactor.py:62` |
