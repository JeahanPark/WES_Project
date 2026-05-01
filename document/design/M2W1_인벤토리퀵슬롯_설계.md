# M2 Week 1 설계 — 그리드 인벤토리 + 퀵슬롯

> **작성일**: 2026-04-18
> **범위**: 그리드 인벤토리 UI, 드래그 앤 드롭, 퀵슬롯 (HUD 8칸 + 단축키)
> **레퍼런스**: Escape from Duckov (인벤토리 UI/UX)

---

## 1. 현재 구조

### 데이터 레이어
- `InventoryRegistry` — `List<ItemData>`로 아이템 관리 (인덱스 = 순서 없음)
- `ItemData` — `ItemInfo` 참조 + `Count`

### UI 레이어
- `InventoryPopup` (BasePopup) — 스크롤 리스트 + 상세 패널
- `InventoryScroll` (BaseScroll\<ItemData\>) — 셀 재활용 스크롤
- `InventoryScrollCell` (BaseScrollCell\<ItemData\>) — 아이콘/이름/수량 표시
- `InventoryDetailPanel` — 선택 아이템 상세 + 설치 버튼

### 핵심 발견
- **BaseScroll에 그리드 모드 내장** (`m_IsGridMode`, `m_GridColumnCount`)
- vertical + horizontal 모두 활성화하면 자동으로 그리드 배치 계산
- 셀 풀링/재활용 이미 동작

---

## 2. 변경 설계

### 2.1 데이터 레이어 — 슬롯 기반 인벤토리

**문제**: 현재 `InventoryRegistry`는 단순 List라서 "3번 슬롯에 아이템 배치" 같은 슬롯 개념이 없음.

**변경**:
- `InventoryRegistry` 내부를 **고정 크기 배열** `ItemData[]`로 변경
- 슬롯 인덱스 기반 관리 (빈 슬롯 = null)
- 초기 슬롯 수: 20칸 (확장 가능 구조)

```
// 변경 전
private List<ItemData> m_Items;

// 변경 후
private ItemData[] m_Slots;
private int m_SlotCount = 20;  // 초기값, 확장 가능
```

**주요 메서드 변경**:

| 메서드 | 변경 내용 |
|--------|-----------|
| `AddItem(infoId, count)` | 기존 스택 가능한 슬롯 찾기 → 없으면 빈 슬롯에 추가 |
| `RemoveItem(infoId, count)` | 동일 (해당 아이템 찾아서 차감) |
| `GetAllItems()` → `GetSlots()` | `ItemData[]` 반환 (null 포함) |
| **신규** `SwapSlots(fromIndex, toIndex)` | 두 슬롯 위치 교환 (드래그 이동) |
| **신규** `GetSlot(index)` | 특정 슬롯의 ItemData 반환 |
| **신규** `ExpandSlots(newCount)` | 슬롯 수 확장 |

### 2.2 UI 레이어 — 그리드 인벤토리

**InventoryScroll (BaseScroll\<ItemData\>)**:
- BaseScroll의 그리드 모드 활용 (vertical + horizontal 활성화)
- `SetData()` 호출 시 `InventoryRegistry.GetSlots()` 전달 (null 포함 배열 → List 변환)
- 빈 슬롯도 셀로 렌더링 (빈 칸 표시)

**InventoryScrollCell 변경**:
- 빈 슬롯 표시 지원 (`OnUpdateCell`에서 data == null이면 빈 칸)
- **드래그 앤 드롭 인터페이스 추가**: `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler`
- 선택 상태 하이라이트 (클릭 시 테두리 등)

**InventoryPopup**:
- `RefreshInventory()` — `GetSlots()` 사용으로 변경
- 드래그 중 아이템 프리뷰 오브젝트 관리 (Canvas 위에 떠다니는 아이콘)

**InventoryDetailPanel**:
- 변경 없음 (오른쪽 고정 패널 유지)

### 2.3 드래그 앤 드롭

**드래그 시작** (`OnBeginDrag`):
- 현재 셀의 아이콘을 복제하여 Canvas 위에 드래그 프리뷰 생성
- 원래 셀은 반투명 처리

**드래그 중** (`OnDrag`):
- 프리뷰가 마우스를 따라 이동

**드롭** (`OnEndDrop` / `OnDrop`):
- **인벤토리 슬롯 위**: `InventoryRegistry.SwapSlots(from, to)` 호출 → UI 갱신
- **퀵슬롯 위**: 퀵슬롯에 아이템 등록 (아래 2.4 참조)
- **아무 곳도 아님**: 드래그 취소, 원래 위치 복귀

**드래그 프리뷰 관리**:
- `InventoryPopup`에 `m_DragPreview` (Image) 프리팹 추가
- 드래그 시작 시 생성, 드롭 시 제거

### 2.4 퀵슬롯 시스템

#### 데이터: QuickSlotRegistry

```
public class QuickSlotRegistry
{
    private int[] m_SlotItemInfoIds;  // 8칸, 등록된 아이템 InfoId (0 = 비어있음)
}
```

**주요 메서드**:
| 메서드 | 설명 |
|--------|------|
| `Register(slotIndex, itemInfoId)` | 퀵슬롯에 아이템 등록 |
| `Unregister(slotIndex)` | 퀵슬롯에서 제거 |
| `GetItemInfoId(slotIndex)` | 해당 슬롯의 아이템 ID 반환 |
| `UseSlot(slotIndex)` | 아이템 사용 실행 |

**아이템 사용 로직** (`UseSlot`):
1. 해당 InfoId의 아이템이 인벤토리에 있는지 확인
2. 아이템 타입에 따라 분기:
   - **건물 아이템** (`IsBuilding`): `BuildingPlacementWorker.StartPlacement()` 호출
   - **소비 아이템**: 효과 적용 + 인벤토리에서 1개 차감 (추후 확장)
   - **장비 아이템**: 장착/해제 (추후 확장)
3. 인벤토리에 없으면 무시 (퀵슬롯 등록은 유지, 비활성 표시)

#### UI: QuickSlotHUD

**위치**: HUD 하단 중앙
**구조**: `InGameHUDWorker` 하위에 `QuickSlotHUD` 추가

```
QuickSlotHUD
├── QuickSlotCell [0] (1키)
├── QuickSlotCell [1] (2키)
├── ...
└── QuickSlotCell [7] (8키)
```

**QuickSlotCell** 구성:
- 아이콘 (Image)
- 수량 텍스트 (TextMeshProUGUI) — 인벤토리 보유 수량 실시간 표시
- 단축키 번호 텍스트 ("1" ~ "8")
- 비어있을 때: 빈 칸 + 번호만 표시
- 인벤토리에 해당 아이템 없을 때: 아이콘 어둡게 + 수량 0

#### 입력: InputManager 확장

- 키 1~8 액션 추가 (현재 Key1/Key2/Key3만 있음 → 8개로 확장)
- `OnQuickSlotAsObservable(int slotIndex)` 또는 `OnQuickSlot1~8AsObservable` 제공
- `InGameHUDWorker`에서 구독하여 `QuickSlotRegistry.UseSlot(index)` 호출

### 2.5 드래그로 퀵슬롯 등록/해제

**인벤토리 → 퀵슬롯**:
- 인벤토리 셀을 드래그하여 QuickSlotCell 위에 드롭
- `QuickSlotRegistry.Register(slotIndex, itemInfoId)` 호출
- 같은 아이템을 여러 슬롯에 등록 가능? → **불가** (기존 등록 해제 후 새 슬롯에 등록)

**퀵슬롯 간 이동**:
- QuickSlotCell 간 드래그로 순서 변경
- 두 슬롯 값 스왑

**퀵슬롯 해제**:
- QuickSlotCell을 드래그하여 빈 곳에 드롭 → 등록 해제
- 또는 우클릭으로 해제 (간편)

---

## 3. 파일 변경 목록

### 수정
| 파일 | 변경 내용 |
|------|-----------|
| `InventoryRegistry.cs` | List → 고정 배열, 슬롯 기반 메서드 추가 |
| `InventoryPopup.cs` | GetSlots() 사용, 드래그 프리뷰 관리 |
| `InventoryScroll.cs` | 그리드 모드 활성화, 빈 슬롯 데이터 전달 |
| `InventoryScrollCell.cs` | 드래그 앤 드롭 핸들러, 빈 셀 표시 |
| `InGameHUDWorker.cs` | QuickSlotHUD 참조 추가 |
| `InputManager.cs` | 키 1~8 액션 추가 |
| `InGameObjectDataWorker.cs` | QuickSlotRegistry 관리 추가 |

### 신규
| 파일 | 설명 |
|------|------|
| `QuickSlotRegistry.cs` | 퀵슬롯 데이터 관리 (8칸) |
| `QuickSlotHUD.cs` | 퀵슬롯 HUD UI 관리 |
| `QuickSlotCell.cs` | 개별 퀵슬롯 셀 (아이콘 + 수량 + 키번호) |

### 프리팹
| 프리팹 | 설명 |
|--------|------|
| `InventoryPopup.prefab` | GridLayoutGroup 적용, 셀 크기 조정 |
| `InventoryScrollCell.prefab` | 그리드 칸 형태로 변경 (정사각형) |
| `QuickSlotHUD.prefab` | 신규 — HUD 하단 8칸 |
| `QuickSlotCell.prefab` | 신규 — 개별 퀵슬롯 칸 |

---

## 4. 구현 순서

1. **InventoryRegistry 슬롯 기반 변경** — 데이터 레이어 먼저
2. **InventoryScrollCell 그리드 표시** — 빈 칸 포함 렌더링
3. **InventoryPopup 그리드 연결** — 프리팹 수정 + 코드 연결
4. **드래그 앤 드롭** — 인벤토리 내 슬롯 이동
5. **QuickSlotRegistry + QuickSlotHUD** — 퀵슬롯 데이터 + UI
6. **InputManager 키 1~8** — 단축키 바인딩
7. **드래그 퀵슬롯 등록** — 인벤토리 ↔ 퀵슬롯 연결
8. **통합 테스트**
