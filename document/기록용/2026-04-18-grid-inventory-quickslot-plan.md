# 그리드 인벤토리 + 퀵슬롯 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스크롤 리스트 인벤토리를 그리드 칸 방식으로 변경하고, 드래그 앤 드롭 + HUD 퀵슬롯 8칸을 구현한다.

**Architecture:** InventoryRegistry를 고정 배열 슬롯 기반으로 변경하고, BaseScroll의 기존 그리드 모드를 활용하여 UI를 교체한다. 퀵슬롯은 QuickSlotRegistry(데이터) + QuickSlotHUD(UI)로 분리 구현한다. InputManager에 키 1~8 액션을 추가하여 단축키를 바인딩한다.

**Tech Stack:** Unity 6, Netcode for GameObjects, UniRx, Unity Input System, TextMeshPro

---

## 파일 구조

### 수정 파일

| 파일 | 역할 변경 |
|------|-----------|
| `Assets/Scripts/InGameObjectData/InventoryRegistry.cs` | List → ItemData[] 고정 배열 슬롯 기반 |
| `Assets/Scripts/UI/Popup/InventoryPopup/InventoryScrollCell.cs` | 빈 슬롯 표시 + 드래그 앤 드롭 핸들러 추가 |
| `Assets/Scripts/UI/Popup/InventoryPopup/InventoryPopup.cs` | GetSlots() 연결 + 드래그 프리뷰 관리 |
| `Assets/Scripts/UI/Popup/InventoryPopup/InventoryScroll.cs` | 드래그 이벤트 콜백 추가 |
| `Assets/Scripts/UI/HUD/InGameHUDWorker.cs` | QuickSlotHUD 참조 추가 + 퀵슬롯 입력 구독 |
| `Assets/Scripts/Manager/InputManager.cs` | 키 1~8 퀵슬롯 액션 추가 |
| `Assets/Scripts/InGameObjectData/InGameObjectDataWorker.cs` | QuickSlotRegistry 인스턴스 관리 추가 |

### 신규 파일

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/InGameObjectData/QuickSlotRegistry.cs` | 퀵슬롯 8칸 데이터 관리 |
| `Assets/Scripts/UI/HUD/QuickSlotHUD.cs` | 퀵슬롯 HUD 전체 관리 |
| `Assets/Scripts/UI/HUD/QuickSlotCell.cs` | 개별 퀵슬롯 칸 UI + 드롭 대상 |

---

## Task 1: InventoryRegistry 슬롯 기반 변경

**Files:**
- Modify: `Assets/Scripts/InGameObjectData/InventoryRegistry.cs`

이 태스크에서 InventoryRegistry 내부를 `List<ItemData>` → `ItemData[]` 고정 배열로 변경한다. 외부 호출자(`AddItem`, `RemoveItem`, `GetItem`)의 시그니처는 유지하되, 내부 로직이 슬롯 인덱스 기반으로 동작하게 변경한다.

- [ ] **Step 1: InventoryRegistry.cs 전체 교체**

```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// 인벤토리 데이터 관리 (슬롯 기반)
/// </summary>
public class InventoryRegistry
{
    public const int DEFAULT_SLOT_COUNT = 20;

    private ItemData[] m_Slots;
    private int m_SlotCount;

    public int SlotCount => m_SlotCount;

    public InventoryRegistry(int _slotCount = DEFAULT_SLOT_COUNT)
    {
        m_SlotCount = _slotCount;
        m_Slots = new ItemData[m_SlotCount];
    }

    public void AddItem(int _infoId, int _count = 1)
    {
        ItemInfo info = Managers.Info.ItemInfoList.Find(i => i.Id == _infoId);
        if (info == null)
            return;

        // 스택 가능한 아이템: 기존 슬롯에 추가
        if (info.IsStackable)
        {
            for (int i = 0; i < m_SlotCount; i++)
            {
                if (m_Slots[i] != null && m_Slots[i].Info.Id == _infoId)
                {
                    m_Slots[i].AddCount(_count);
                    return;
                }
            }
        }

        // 빈 슬롯에 새로 추가
        int emptyIndex = FindEmptySlot();
        if (emptyIndex < 0)
        {
            GameDebug.LogWarning("[InventoryRegistry] 인벤토리가 가득 찼습니다.");
            return;
        }

        m_Slots[emptyIndex] = new ItemData(info, _count);
    }

    public bool RemoveItem(int _infoId, int _count = 1)
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] != null && m_Slots[i].Info.Id == _infoId)
            {
                if (m_Slots[i].Count < _count)
                    return false;

                m_Slots[i].AddCount(-_count);

                if (m_Slots[i].Count <= 0)
                {
                    m_Slots[i] = null;
                }

                return true;
            }
        }

        return false;
    }

    public ItemData GetItem(int _infoId)
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] != null && m_Slots[i].Info.Id == _infoId)
                return m_Slots[i];
        }

        return null;
    }

    public ItemData GetSlot(int _index)
    {
        if (_index < 0 || _index >= m_SlotCount)
            return null;

        return m_Slots[_index];
    }

    public ItemData[] GetSlots()
    {
        return m_Slots;
    }

    /// <summary>
    /// 하위 호환용 — 기존 코드가 List를 기대하는 경우
    /// </summary>
    public List<ItemData> GetAllItems()
    {
        var result = new List<ItemData>();
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] != null)
                result.Add(m_Slots[i]);
        }
        return result;
    }

    public void SwapSlots(int _fromIndex, int _toIndex)
    {
        if (_fromIndex < 0 || _fromIndex >= m_SlotCount)
            return;
        if (_toIndex < 0 || _toIndex >= m_SlotCount)
            return;

        (m_Slots[_fromIndex], m_Slots[_toIndex]) = (m_Slots[_toIndex], m_Slots[_fromIndex]);
    }

    public void ExpandSlots(int _newCount)
    {
        if (_newCount <= m_SlotCount)
            return;

        var newSlots = new ItemData[_newCount];
        Array.Copy(m_Slots, newSlots, m_SlotCount);
        m_Slots = newSlots;
        m_SlotCount = _newCount;
    }

    public void Clear()
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            m_Slots[i] = null;
        }
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] == null)
                return i;
        }
        return -1;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Run: Unity 에디터에서 컴파일 오류 없는지 확인. `GetAllItems()`를 유지했으므로 기존 호출자(CraftDetailPanel, BuildingPlacementWorker, WorldDropItem)는 그대로 동작한다.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/InGameObjectData/InventoryRegistry.cs
git commit -m "인벤토리를 슬롯 기반 배열로 변경"
```

---

## Task 2: InventoryPopup/InventoryScroll 그리드 연결

**Files:**
- Modify: `Assets/Scripts/UI/Popup/InventoryPopup/InventoryPopup.cs`
- Modify: `Assets/Scripts/UI/Popup/InventoryPopup/InventoryScroll.cs`

인벤토리 팝업이 슬롯 배열(null 포함)을 스크롤에 전달하도록 변경한다. BaseScroll의 그리드 모드는 프리팹의 ScrollRect 설정(vertical + horizontal 모두 체크)으로 활성화되므로, 코드 변경은 데이터 전달만 수정하면 된다.

- [ ] **Step 1: InventoryPopup.cs 수정**

```csharp
using System.Collections.Generic;
using UnityEngine;

public class InventoryPopup : BasePopup
{
    [SerializeField] private InventoryScroll m_InventoryScroll;
    [SerializeField] private InventoryDetailPanel m_DetailPanel;

    private void Start()
    {
        m_InventoryScroll.SetCellClickCallback(OnCellClicked);
        RefreshInventory();
    }

    public void RefreshInventory()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        ItemData[] slots = inventory.GetSlots();

        // BaseScroll은 List<TData>를 받으므로, 배열을 List로 변환 (null 포함)
        var slotList = new List<ItemData>(slots);
        m_InventoryScroll.SetData(slotList);

        m_DetailPanel.gameObject.SetActive(false);
    }

    public void OnClickClose()
    {
        Close();
    }

    private void OnCellClicked(ItemData _itemData)
    {
        if (_itemData == null)
            return;

        m_DetailPanel.gameObject.SetActive(true);
        m_DetailPanel.Show(_itemData);
    }
}
```

- [ ] **Step 2: InventoryScroll.cs 수정**

`Refresh()` 메서드도 슬롯 배열 방식으로 변경한다.

```csharp
using System;
using System.Collections.Generic;

public class InventoryScroll : BaseScroll<ItemData>
{
    private Action<ItemData> m_OnCellClickCallback;

    protected override void OnAwake()
    {
        base.OnAwake();
    }

    public void SetCellClickCallback(Action<ItemData> _callback)
    {
        m_OnCellClickCallback = _callback;
    }

    public void NotifyCellClicked(ItemData _itemData)
    {
        m_OnCellClickCallback?.Invoke(_itemData);
    }

    public void Refresh()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        ItemData[] slots = inventory.GetSlots();
        var slotList = new List<ItemData>(slots);
        SetData(slotList);
    }
}
```

- [ ] **Step 3: 컴파일 확인**

- [ ] **Step 4: 프리팹 설정 (MCP)**

`InventoryPopup.prefab` 내 InventoryScroll(ScrollRect)의 설정을 변경:
- `vertical` = true, `horizontal` = true (그리드 모드 활성화)
- Content의 자식인 InventoryScrollCell 프리팹의 크기를 정사각형(예: 80x80)으로 조정
- BaseScroll의 `m_CellSize` = 80, `m_Spacing` = 5

> MCP `u_editor_prefab` 도구 또는 Unity 에디터에서 직접 설정

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/Popup/InventoryPopup/InventoryPopup.cs
git add Assets/Scripts/UI/Popup/InventoryPopup/InventoryScroll.cs
git commit -m "인벤토리 UI를 그리드 슬롯 모드로 전환"
```

---

## Task 3: InventoryScrollCell 그리드 셀 표시 변경

**Files:**
- Modify: `Assets/Scripts/UI/Popup/InventoryPopup/InventoryScrollCell.cs`

그리드 칸 형태에 맞게 셀을 수정한다. 이름 텍스트를 제거(칸이 작아 표시 불가)하고, 아이콘 + 수량만 표시한다. 빈 슬롯은 빈 칸 배경으로 렌더링한다.

- [ ] **Step 1: InventoryScrollCell.cs 수정**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryScrollCell : BaseScrollCell<ItemData>
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private Image m_BackgroundImage;
    [SerializeField] private GameObject m_SelectedFrame;

    private ItemData m_ItemData;
    private bool m_IsSelected;

    public ItemData ItemData => m_ItemData;

    protected override void OnUpdateCell(int _index, ItemData _data)
    {
        m_ItemData = _data;

        if (m_ItemData == null || m_ItemData.Info == null)
        {
            SetEmpty();
            return;
        }

        if (m_CountText != null)
        {
            m_CountText.text = m_ItemData.Count > 1 ? m_ItemData.Count.ToString() : string.Empty;
        }

        if (m_IconImage != null)
        {
            string iconKey = m_ItemData.Info.IconKey;
            m_IconImage.sprite = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }
    }

    private void SetEmpty()
    {
        if (m_CountText != null)
        {
            m_CountText.text = string.Empty;
        }

        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }

        SetSelected(false);
    }

    public void SetSelected(bool _selected)
    {
        m_IsSelected = _selected;
        if (m_SelectedFrame != null)
            m_SelectedFrame.SetActive(_selected);
    }

    public void OnClickCell()
    {
        if (m_ItemData == null)
            return;

        var scroll = GetComponentInParent<InventoryScroll>(true);
        scroll?.NotifyCellClicked(m_ItemData);
    }
}
```

- [ ] **Step 2: 프리팹 구조 (MCP 또는 에디터)**

InventoryScrollCell 프리팹을 정사각형 칸 형태로 재구성:
```
InventoryScrollCell (80x80, Image=Background)
├── IconImage (Image, 중앙 정렬, 60x60)
├── CountText (TextMeshProUGUI, 우하단 정렬, 폰트 12)
├── SelectedFrame (Image, 테두리, 기본 비활성)
└── (NameText 제거)
```

- [ ] **Step 3: 컴파일 확인**

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/Popup/InventoryPopup/InventoryScrollCell.cs
git commit -m "인벤토리 셀을 그리드 칸 형태로 변경"
```

---

## Task 4: 드래그 앤 드롭 — 인벤토리 내 슬롯 이동

**Files:**
- Modify: `Assets/Scripts/UI/Popup/InventoryPopup/InventoryScrollCell.cs`
- Modify: `Assets/Scripts/UI/Popup/InventoryPopup/InventoryPopup.cs`

인벤토리 셀에 드래그 앤 드롭을 추가하여 슬롯 간 아이템 이동을 구현한다.

- [ ] **Step 1: InventoryScrollCell.cs에 드래그 핸들러 추가**

기존 코드에 `using UnityEngine.EventSystems;`와 드래그 인터페이스를 추가한다.

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryScrollCell : BaseScrollCell<ItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private Image m_BackgroundImage;
    [SerializeField] private GameObject m_SelectedFrame;

    private ItemData m_ItemData;
    private bool m_IsSelected;

    public ItemData ItemData => m_ItemData;

    protected override void OnUpdateCell(int _index, ItemData _data)
    {
        m_ItemData = _data;

        if (m_ItemData == null || m_ItemData.Info == null)
        {
            SetEmpty();
            return;
        }

        if (m_CountText != null)
        {
            m_CountText.text = m_ItemData.Count > 1 ? m_ItemData.Count.ToString() : string.Empty;
        }

        if (m_IconImage != null)
        {
            string iconKey = m_ItemData.Info.IconKey;
            m_IconImage.sprite = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }
    }

    private void SetEmpty()
    {
        if (m_CountText != null)
        {
            m_CountText.text = string.Empty;
        }

        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }

        SetSelected(false);
    }

    public void SetSelected(bool _selected)
    {
        m_IsSelected = _selected;
        if (m_SelectedFrame != null)
            m_SelectedFrame.SetActive(_selected);
    }

    public void OnClickCell()
    {
        if (m_ItemData == null)
            return;

        var scroll = GetComponentInParent<InventoryScroll>(true);
        scroll?.NotifyCellClicked(m_ItemData);
    }

    // ===== 드래그 앤 드롭 =====

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (m_ItemData == null)
        {
            _eventData.pointerDrag = null;
            return;
        }

        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.BeginDrag(this, _eventData);

        // 원래 셀 반투명 처리
        if (m_IconImage != null)
            m_IconImage.color = new Color(1f, 1f, 1f, 0.3f);
    }

    public void OnDrag(PointerEventData _eventData)
    {
        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.UpdateDrag(_eventData);
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.EndDrag();

        // 원래 셀 불투명 복원
        if (m_IconImage != null)
            m_IconImage.color = Color.white;
    }

    public void OnDrop(PointerEventData _eventData)
    {
        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.DropOnSlot(this);
    }
}
```

- [ ] **Step 2: InventoryPopup.cs에 드래그 관리 메서드 추가**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryPopup : BasePopup
{
    [SerializeField] private InventoryScroll m_InventoryScroll;
    [SerializeField] private InventoryDetailPanel m_DetailPanel;
    [SerializeField] private Image m_DragPreview;

    private InventoryScrollCell m_DragSourceCell;

    private void Start()
    {
        m_InventoryScroll.SetCellClickCallback(OnCellClicked);
        RefreshInventory();

        if (m_DragPreview != null)
            m_DragPreview.gameObject.SetActive(false);
    }

    public void RefreshInventory()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        ItemData[] slots = inventory.GetSlots();

        var slotList = new List<ItemData>(slots);
        m_InventoryScroll.SetData(slotList);

        m_DetailPanel.gameObject.SetActive(false);
    }

    public void OnClickClose()
    {
        Close();
    }

    private void OnCellClicked(ItemData _itemData)
    {
        if (_itemData == null)
            return;

        m_DetailPanel.gameObject.SetActive(true);
        m_DetailPanel.Show(_itemData);
    }

    // ===== 드래그 앤 드롭 관리 =====

    public void BeginDrag(InventoryScrollCell _sourceCell, PointerEventData _eventData)
    {
        m_DragSourceCell = _sourceCell;

        if (m_DragPreview != null && _sourceCell.ItemData != null)
        {
            m_DragPreview.gameObject.SetActive(true);
            m_DragPreview.sprite = Managers.Resource.LoadAddressable<Sprite>(_sourceCell.ItemData.Info.IconKey);
            m_DragPreview.rectTransform.position = _eventData.position;
        }
    }

    public void UpdateDrag(PointerEventData _eventData)
    {
        if (m_DragPreview != null && m_DragPreview.gameObject.activeSelf)
        {
            m_DragPreview.rectTransform.position = _eventData.position;
        }
    }

    public void EndDrag()
    {
        m_DragSourceCell = null;

        if (m_DragPreview != null)
            m_DragPreview.gameObject.SetActive(false);
    }

    public void DropOnSlot(InventoryScrollCell _targetCell)
    {
        if (m_DragSourceCell == null)
            return;

        if (m_DragSourceCell == _targetCell)
            return;

        int fromIndex = m_DragSourceCell.Index;
        int toIndex = _targetCell.Index;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        inventory.SwapSlots(fromIndex, toIndex);

        RefreshInventory();
    }
}
```

- [ ] **Step 3: 프리팹에 DragPreview 오브젝트 추가**

InventoryPopup 프리팹에 드래그 프리뷰용 Image 추가:
```
InventoryPopup
├── ... (기존 구조)
└── DragPreview (Image, 60x60, Raycast Target = false, 기본 비활성)
```
`m_DragPreview`에 연결. DragPreview는 Canvas 최상단에 위치시켜 다른 UI 위에 렌더링되도록 한다.

- [ ] **Step 4: 컴파일 확인**

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/Popup/InventoryPopup/InventoryScrollCell.cs
git add Assets/Scripts/UI/Popup/InventoryPopup/InventoryPopup.cs
git commit -m "인벤토리 드래그 앤 드롭 구현"
```

---

## Task 5: QuickSlotRegistry 데이터 레이어

**Files:**
- Create: `Assets/Scripts/InGameObjectData/QuickSlotRegistry.cs`
- Modify: `Assets/Scripts/InGameObjectData/InGameObjectDataWorker.cs`

퀵슬롯 8칸의 데이터를 관리하는 클래스를 만들고, InGameObjectDataWorker에서 관리한다.

- [ ] **Step 1: QuickSlotRegistry.cs 생성**

```csharp
using System;

/// <summary>
/// 퀵슬롯 8칸 데이터 관리
/// 아이템의 InfoId를 저장하여 인벤토리와 연결
/// </summary>
public class QuickSlotRegistry
{
    public const int SLOT_COUNT = 8;

    private int[] m_SlotItemInfoIds;

    public event Action<int> OnSlotChanged;

    public QuickSlotRegistry()
    {
        m_SlotItemInfoIds = new int[SLOT_COUNT];
    }

    public void Register(int _slotIndex, int _itemInfoId)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return;

        // 이미 다른 슬롯에 같은 아이템이 등록되어 있으면 해제
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (m_SlotItemInfoIds[i] == _itemInfoId)
            {
                m_SlotItemInfoIds[i] = 0;
                OnSlotChanged?.Invoke(i);
            }
        }

        m_SlotItemInfoIds[_slotIndex] = _itemInfoId;
        OnSlotChanged?.Invoke(_slotIndex);
    }

    public void Unregister(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return;

        m_SlotItemInfoIds[_slotIndex] = 0;
        OnSlotChanged?.Invoke(_slotIndex);
    }

    public int GetItemInfoId(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return 0;

        return m_SlotItemInfoIds[_slotIndex];
    }

    public void SwapSlots(int _fromIndex, int _toIndex)
    {
        if (_fromIndex < 0 || _fromIndex >= SLOT_COUNT)
            return;
        if (_toIndex < 0 || _toIndex >= SLOT_COUNT)
            return;

        (m_SlotItemInfoIds[_fromIndex], m_SlotItemInfoIds[_toIndex]) =
            (m_SlotItemInfoIds[_toIndex], m_SlotItemInfoIds[_fromIndex]);

        OnSlotChanged?.Invoke(_fromIndex);
        OnSlotChanged?.Invoke(_toIndex);
    }

    /// <summary>
    /// 퀵슬롯 아이템 사용
    /// </summary>
    public void UseSlot(int _slotIndex, InventoryRegistry _inventory)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return;

        int itemInfoId = m_SlotItemInfoIds[_slotIndex];
        if (itemInfoId == 0)
            return;

        // 인벤토리에 해당 아이템이 있는지 확인
        ItemData itemData = _inventory.GetItem(itemInfoId);
        if (itemData == null)
            return;

        // 건물 아이템: 배치 모드 진입
        if (itemData.Info.IsBuilding)
        {
            InGameController.Instance?.BuildingPlacementWorker?.StartPlacement(itemData.Info.Id);
            return;
        }

        // 소비 아이템: 사용 + 차감 (추후 아이템 타입별 효과 확장)
        // TODO: 아이템 사용 효과 시스템이 추가되면 여기서 호출
    }

    public void Clear()
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            m_SlotItemInfoIds[i] = 0;
        }
    }
}
```

- [ ] **Step 2: InGameObjectDataWorker.cs에 QuickSlotRegistry 추가**

```csharp
using Unity.Netcode;

public class InGameObjectDataWorker : NetworkBehaviour
{
    private CharacterRegistry m_CharacterRegistry = new();
    private InventoryRegistry m_InventoryRegistry = new();
    private QuickSlotRegistry m_QuickSlotRegistry = new();

    public CharacterRegistry GetCharacterRegistry()
    {
        return m_CharacterRegistry;
    }

    public InventoryRegistry GetInventoryRegistry()
    {
        return m_InventoryRegistry;
    }

    public QuickSlotRegistry GetQuickSlotRegistry()
    {
        return m_QuickSlotRegistry;
    }
}
```

- [ ] **Step 3: 컴파일 확인**

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/InGameObjectData/QuickSlotRegistry.cs
git add Assets/Scripts/InGameObjectData/InGameObjectDataWorker.cs
git commit -m "퀵슬롯 데이터 레이어 추가"
```

---

## Task 6: InputManager 키 1~8 퀵슬롯 액션 추가

**Files:**
- Modify: `Assets/Scripts/Manager/InputManager.cs`
- Modify: `Assets/Resources/InputSystem_Actions.inputactions` (Unity Input System 설정)

InputManager에 퀵슬롯 1~8 키 입력을 추가한다. 기존 Key1(Previous), Key2(Next) 액션을 퀵슬롯 전용으로 교체한다.

- [ ] **Step 1: InputSystem_Actions에 QuickSlot 액션 추가**

Unity 에디터에서 InputSystem_Actions 에셋 열기:
- Player 액션맵에 `QuickSlot1` ~ `QuickSlot8` 액션 추가
- 바인딩: `1` ~ `8` 키보드 키
- Action Type: Button

> 또는 코드에서 런타임 바인딩으로 처리 (아래 Step 2에서 처리)

- [ ] **Step 2: InputManager.cs 수정 — 퀵슬롯 입력 추가**

기존 Key1/Key2/Key3 Subject를 퀵슬롯 배열로 교체한다.

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using UniRx;
using System;

public class InputManager : MonoSingleton<InputManager>
{
    [SerializeField] private InputActionAsset m_InputActionAsset;

    private InputActionMap m_PlayerMap;
    private InputActionMap m_UIMap;
    private InputAction m_MoveAction;
    private InputAction m_LookAction;
    private InputAction m_AttackAction;
    private InputAction m_InteractAction;
    private InputAction m_InventoryAction;
    private InputAction m_SubmitAction;

    // 퀵슬롯 액션 (1~8)
    private InputAction[] m_QuickSlotActions = new InputAction[QuickSlotRegistry.SLOT_COUNT];

    // Observable Subjects
    private readonly Subject<Vector2> m_OnMove = new Subject<Vector2>();
    private readonly Subject<Vector2> m_OnLook = new Subject<Vector2>();
    private readonly Subject<Unit> m_OnAttack = new Subject<Unit>();
    private readonly Subject<Unit> m_OnInteract = new Subject<Unit>();
    private readonly Subject<Unit> m_OnInventory = new Subject<Unit>();
    private readonly Subject<Unit> m_OnEnter = new Subject<Unit>();
    private readonly Subject<int> m_OnQuickSlot = new Subject<int>();

    // Public Observables
    public IObservable<Vector2> OnMoveAsObservable => m_OnMove;
    public IObservable<Vector2> OnLookAsObservable => m_OnLook;
    public IObservable<Unit> OnAttackAsObservable => m_OnAttack;
    public IObservable<Unit> OnInteractAsObservable => m_OnInteract;
    public IObservable<Unit> OnInventoryAsObservable => m_OnInventory;
    public IObservable<Unit> OnEnterAsObservable => m_OnEnter;

    /// <summary>
    /// 퀵슬롯 키 입력 (0~7 인덱스 전달)
    /// </summary>
    public IObservable<int> OnQuickSlotAsObservable => m_OnQuickSlot;

    // Current Input Values
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public override void Init()
    {
        base.Init();

        if (m_InputActionAsset == null)
        {
            m_InputActionAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        }

        if (m_InputActionAsset == null)
        {
            GameDebug.LogError("[InputManager] InputActionAsset not found!");
            return;
        }

        m_PlayerMap = m_InputActionAsset.FindActionMap("Player");
        m_UIMap = m_InputActionAsset.FindActionMap("UI");

        m_MoveAction = m_PlayerMap.FindAction("Move");
        m_LookAction = m_PlayerMap.FindAction("Look");
        m_AttackAction = m_PlayerMap.FindAction("Attack");
        m_InteractAction = m_PlayerMap.FindAction("Interact");
        m_InventoryAction = m_PlayerMap.FindAction("Inventory");
        m_SubmitAction = m_UIMap.FindAction("Submit");

        // 퀵슬롯 액션 바인딩
        SetupQuickSlotActions();

        // Subscribe to Player events
        m_MoveAction.performed += OnMovePerformed;
        m_MoveAction.canceled += OnMoveCanceled;
        m_LookAction.performed += OnLookPerformed;
        m_LookAction.canceled += OnLookCanceled;
        m_AttackAction.performed += OnAttackPerformed;
        m_InteractAction.performed += OnInteractPerformed;
        m_InventoryAction.performed += OnInventoryPerformed;
        m_SubmitAction.performed += OnEnterPerformed;

        m_PlayerMap.Enable();
        m_UIMap.Enable();
    }

    private void SetupQuickSlotActions()
    {
        for (int i = 0; i < QuickSlotRegistry.SLOT_COUNT; i++)
        {
            string actionName = $"QuickSlot{i + 1}";
            m_QuickSlotActions[i] = m_PlayerMap.FindAction(actionName);

            if (m_QuickSlotActions[i] == null)
            {
                // InputActions 에셋에 액션이 없으면 런타임에 생성
                GameDebug.LogWarning($"[InputManager] {actionName} action not found in InputActionAsset. Skipping.");
                continue;
            }

            int slotIndex = i; // 클로저용 캡처
            m_QuickSlotActions[i].performed += (_context) => m_OnQuickSlot.OnNext(slotIndex);
        }
    }

    public override void Clear()
    {
        base.Clear();

        if (m_PlayerMap != null)
        {
            m_MoveAction.performed -= OnMovePerformed;
            m_MoveAction.canceled -= OnMoveCanceled;
            m_LookAction.performed -= OnLookPerformed;
            m_LookAction.canceled -= OnLookCanceled;
            m_AttackAction.performed -= OnAttackPerformed;
            m_InteractAction.performed -= OnInteractPerformed;
            m_InventoryAction.performed -= OnInventoryPerformed;
            m_PlayerMap.Disable();
        }

        if (m_UIMap != null)
        {
            m_SubmitAction.performed -= OnEnterPerformed;
            m_UIMap.Disable();
        }

        m_OnMove?.Dispose();
        m_OnLook?.Dispose();
        m_OnAttack?.Dispose();
        m_OnInteract?.Dispose();
        m_OnInventory?.Dispose();
        m_OnEnter?.Dispose();
        m_OnQuickSlot?.Dispose();
    }

    private void OnMovePerformed(InputAction.CallbackContext _context)
    {
        MoveInput = _context.ReadValue<Vector2>();
        m_OnMove.OnNext(MoveInput);
    }

    private void OnMoveCanceled(InputAction.CallbackContext _context)
    {
        MoveInput = Vector2.zero;
        m_OnMove.OnNext(MoveInput);
    }

    private void OnLookPerformed(InputAction.CallbackContext _context)
    {
        LookInput = _context.ReadValue<Vector2>();
        m_OnLook.OnNext(LookInput);
    }

    private void OnLookCanceled(InputAction.CallbackContext _context)
    {
        LookInput = Vector2.zero;
        m_OnLook.OnNext(LookInput);
    }

    private void OnAttackPerformed(InputAction.CallbackContext _context)
    {
        m_OnAttack.OnNext(Unit.Default);
    }

    private void OnInteractPerformed(InputAction.CallbackContext _context)
    {
        m_OnInteract.OnNext(Unit.Default);
    }

    private void OnInventoryPerformed(InputAction.CallbackContext _context)
    {
        m_OnInventory.OnNext(Unit.Default);
    }

    private void OnEnterPerformed(InputAction.CallbackContext _context)
    {
        m_OnEnter.OnNext(Unit.Default);
    }

    public void EnablePlayerInput()
    {
        m_PlayerMap?.Enable();
    }

    public void DisablePlayerInput()
    {
        m_PlayerMap?.Disable();
    }

    public void EnableUIInput()
    {
        m_UIMap?.Enable();
    }

    public void DisableUIInput()
    {
        m_UIMap?.Disable();
    }
}
```

> **주의**: 기존 `OnKey1AsObservable`, `OnKey2AsObservable`, `OnKey3AsObservable`를 제거했다. 이 Observable을 참조하는 외부 코드가 있으면 `OnQuickSlotAsObservable`로 교체해야 한다. 기존 Key1/Key2가 Previous/Next 용도로 사용되는 곳이 있는지 확인 필요.

- [ ] **Step 3: 기존 Key1/Key2/Key3 참조 확인 및 교체**

기존 `OnKey1AsObservable`, `OnKey2AsObservable`을 사용하는 곳을 검색하여 `OnQuickSlotAsObservable`로 교체하거나, 해당 기능이 퀵슬롯으로 대체되었으면 제거한다.

- [ ] **Step 4: InputSystem_Actions 에셋에 QuickSlot1~8 액션 추가**

Unity 에디터에서 `Resources/InputSystem_Actions` 에셋 열기:
- Player 맵에 QuickSlot1 (키보드 `1`), QuickSlot2 (`2`), ... QuickSlot8 (`8`) 추가
- 기존 Previous/Next 액션은 유지하거나 제거 (사용처에 따라 결정)

- [ ] **Step 5: 컴파일 확인**

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Manager/InputManager.cs
git commit -m "퀵슬롯 키 1~8 입력 추가"
```

---

## Task 7: QuickSlotHUD + QuickSlotCell UI

**Files:**
- Create: `Assets/Scripts/UI/HUD/QuickSlotHUD.cs`
- Create: `Assets/Scripts/UI/HUD/QuickSlotCell.cs`
- Modify: `Assets/Scripts/UI/HUD/InGameHUDWorker.cs`

HUD 하단에 퀵슬롯 8칸 UI를 추가한다.

- [ ] **Step 1: QuickSlotCell.cs 생성**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 퀵슬롯 칸 UI
/// </summary>
public class QuickSlotCell : MonoBehaviour, IDropHandler
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private TextMeshProUGUI m_KeyNumberText;
    [SerializeField] private Image m_DimOverlay;

    private int m_SlotIndex;
    private int m_ItemInfoId;

    public int SlotIndex => m_SlotIndex;

    public void Setup(int _slotIndex)
    {
        m_SlotIndex = _slotIndex;

        if (m_KeyNumberText != null)
            m_KeyNumberText.text = (_slotIndex + 1).ToString();

        SetEmpty();
    }

    public void UpdateSlot(int _itemInfoId, InventoryRegistry _inventory)
    {
        m_ItemInfoId = _itemInfoId;

        if (_itemInfoId == 0)
        {
            SetEmpty();
            return;
        }

        ItemInfo info = Managers.Info.ItemInfoList.Find(i => i.Id == _itemInfoId);
        if (info == null)
        {
            SetEmpty();
            return;
        }

        // 아이콘 표시
        if (m_IconImage != null)
        {
            m_IconImage.sprite = !string.IsNullOrEmpty(info.IconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(info.IconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }

        // 인벤토리 보유 수량 표시
        ItemData itemData = _inventory?.GetItem(_itemInfoId);
        int count = itemData?.Count ?? 0;

        if (m_CountText != null)
        {
            m_CountText.text = count > 0 ? count.ToString() : "0";
        }

        // 인벤토리에 없으면 어둡게
        if (m_DimOverlay != null)
        {
            m_DimOverlay.gameObject.SetActive(count <= 0);
        }
    }

    private void SetEmpty()
    {
        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }

        if (m_CountText != null)
            m_CountText.text = string.Empty;

        if (m_DimOverlay != null)
            m_DimOverlay.gameObject.SetActive(false);
    }

    // 인벤토리에서 드래그하여 퀵슬롯에 드롭
    public void OnDrop(PointerEventData _eventData)
    {
        var draggedCell = _eventData.pointerDrag?.GetComponent<InventoryScrollCell>();
        if (draggedCell == null || draggedCell.ItemData == null)
            return;

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        objectData.GetQuickSlotRegistry().Register(m_SlotIndex, draggedCell.ItemData.Info.Id);
    }
}
```

- [ ] **Step 2: QuickSlotHUD.cs 생성**

```csharp
using UnityEngine;

/// <summary>
/// 퀵슬롯 HUD 전체 관리
/// </summary>
public class QuickSlotHUD : MonoBehaviour
{
    [SerializeField] private QuickSlotCell[] m_Cells;

    private QuickSlotRegistry m_QuickSlotRegistry;
    private InventoryRegistry m_InventoryRegistry;

    public void Initialize(QuickSlotRegistry _quickSlotRegistry, InventoryRegistry _inventoryRegistry)
    {
        m_QuickSlotRegistry = _quickSlotRegistry;
        m_InventoryRegistry = _inventoryRegistry;

        for (int i = 0; i < m_Cells.Length; i++)
        {
            m_Cells[i].Setup(i);
        }

        m_QuickSlotRegistry.OnSlotChanged += OnSlotChanged;
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (m_QuickSlotRegistry != null)
            m_QuickSlotRegistry.OnSlotChanged -= OnSlotChanged;
    }

    private void OnSlotChanged(int _slotIndex)
    {
        RefreshSlot(_slotIndex);
    }

    public void RefreshSlot(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= m_Cells.Length)
            return;

        int itemInfoId = m_QuickSlotRegistry.GetItemInfoId(_slotIndex);
        m_Cells[_slotIndex].UpdateSlot(itemInfoId, m_InventoryRegistry);
    }

    public void RefreshAll()
    {
        for (int i = 0; i < m_Cells.Length; i++)
        {
            RefreshSlot(i);
        }
    }
}
```

- [ ] **Step 3: InGameHUDWorker.cs 수정 — 퀵슬롯 연결**

```csharp
using UnityEngine;
using UniRx;

/// <summary>
/// 인게임 HUD를 관리하는 Worker
/// </summary>
public class InGameHUDWorker : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private PlayerStatusHUD m_PlayerStatusHUD;
    [SerializeField] private CraftHUDTab m_CraftHUDTab;
    [SerializeField] private QuickSlotHUD m_QuickSlotHUD;

    private PlayerCharacter m_LocalPlayer;
    private System.IDisposable m_QuickSlotSubscription;

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void SetLocalPlayer(PlayerCharacter _player)
    {
        UnsubscribeEvents();

        m_LocalPlayer = _player;

        SubscribeEvents();
        RefreshHUD();
        InitializeQuickSlot();

        GameDebug.Log($"[InGameHUDWorker] Local player set: PlayerIndex {_player.GetPlayerIndex()}");
    }

    public void ClearLocalPlayer()
    {
        UnsubscribeEvents();
        m_LocalPlayer = null;
        m_QuickSlotSubscription?.Dispose();

        GameDebug.Log("[InGameHUDWorker] Local player cleared");
    }

    private void InitializeQuickSlot()
    {
        if (m_QuickSlotHUD == null)
            return;

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        var quickSlotRegistry = objectData.GetQuickSlotRegistry();
        var inventoryRegistry = objectData.GetInventoryRegistry();

        m_QuickSlotHUD.Initialize(quickSlotRegistry, inventoryRegistry);

        // 퀵슬롯 키 입력 구독
        m_QuickSlotSubscription?.Dispose();
        m_QuickSlotSubscription = Managers.Input.OnQuickSlotAsObservable
            .Subscribe(_slotIndex =>
            {
                quickSlotRegistry.UseSlot(_slotIndex, inventoryRegistry);
                m_QuickSlotHUD.RefreshSlot(_slotIndex);
            });
    }

    private void SubscribeEvents()
    {
        if (m_LocalPlayer == null)
            return;

        m_LocalPlayer.SubscribeOnHPChanged(OnHPChanged);
        m_LocalPlayer.SubscribeOnColdChanged(OnColdChanged);
    }

    private void UnsubscribeEvents()
    {
        if (m_LocalPlayer == null)
            return;

        m_LocalPlayer.UnsubscribeOnHPChanged(OnHPChanged);
        m_LocalPlayer.UnsubscribeOnColdChanged(OnColdChanged);
    }

    private void RefreshHUD()
    {
        if (m_LocalPlayer == null || m_PlayerStatusHUD == null)
            return;

        m_PlayerStatusHUD.UpdateStat(CharacterStat.HP, m_LocalPlayer.HP, m_LocalPlayer.MaxHP);
        m_PlayerStatusHUD.UpdateStat(CharacterStat.Cold, m_LocalPlayer.Cold, m_LocalPlayer.MaxCold);
    }

    private void OnHPChanged(int _hp, int _maxHP)
    {
        if (m_PlayerStatusHUD != null)
        {
            m_PlayerStatusHUD.UpdateStat(CharacterStat.HP, _hp, _maxHP);
        }
    }

    private void OnColdChanged(int _cold, int _maxCold)
    {
        if (m_PlayerStatusHUD != null)
        {
            m_PlayerStatusHUD.UpdateStat(CharacterStat.Cold, _cold, _maxCold);
        }
    }
}
```

- [ ] **Step 4: 프리팹 구성 (MCP 또는 에디터)**

InGame HUD에 QuickSlotHUD 추가:
```
InGameHUDWorker
├── PlayerStatusHUD (기존)
├── CraftHUDTab (기존)
└── QuickSlotHUD (신규, 하단 중앙 HorizontalLayoutGroup)
    ├── QuickSlotCell [0] (80x80)
    │   ├── Background (Image)
    │   ├── IconImage (Image, 60x60, 중앙)
    │   ├── CountText (TMP, 우하단, 폰트 11)
    │   ├── KeyNumberText (TMP, 좌상단, 폰트 10, "1")
    │   └── DimOverlay (Image, 반투명 검정, 기본 비활성)
    ├── QuickSlotCell [1] ~ [7] (동일 구조)
```
InGameHUDWorker의 `m_QuickSlotHUD`에 연결.

- [ ] **Step 5: 컴파일 확인**

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/UI/HUD/QuickSlotHUD.cs
git add Assets/Scripts/UI/HUD/QuickSlotCell.cs
git add Assets/Scripts/UI/HUD/InGameHUDWorker.cs
git commit -m "퀵슬롯 HUD UI 구현"
```

---

## Task 8: 인벤토리 ↔ 퀵슬롯 드래그 연결

**Files:**
- Modify: `Assets/Scripts/UI/Popup/InventoryPopup/InventoryPopup.cs`

인벤토리에서 드래그한 아이템을 퀵슬롯에 드롭할 수 있도록 연결한다. QuickSlotCell의 `OnDrop`은 Task 7에서 이미 구현했으므로, InventoryPopup의 EndDrag에서 드래그 프리뷰 정리만 정상 동작하면 된다.

현재 구조에서는 InventoryScrollCell의 `OnEndDrag` → `InventoryPopup.EndDrag()`가 호출되고, QuickSlotCell의 `OnDrop`은 EventSystem에 의해 자동 호출된다. 별도 코드 변경 없이 동작해야 하지만, 한 가지 문제가 있다: InventoryPopup과 QuickSlotHUD가 다른 Canvas 계층에 있으면 드래그 이벤트가 전달되지 않을 수 있다.

- [ ] **Step 1: DragPreview의 Raycast 차단 확인**

`m_DragPreview`의 `Raycast Target`이 **false**인지 확인. true이면 DragPreview가 드롭 대상을 가려서 QuickSlotCell의 OnDrop이 호출되지 않는다.

- [ ] **Step 2: 인벤토리 팝업이 열린 상태에서 퀵슬롯이 보이는지 확인**

InventoryPopup의 OpenPolicy가 `StackOnTop`이므로, 팝업이 열려도 HUD의 퀵슬롯이 보여야 한다. 팝업 뒤에 QuickSlotHUD가 가려지지 않도록 레이아웃 확인.

- [ ] **Step 3: 퀵슬롯 우클릭 해제 (선택적)**

QuickSlotCell에 우클릭 해제 기능 추가. `IPointerClickHandler`를 구현한다.

QuickSlotCell.cs에 추가:
```csharp
// QuickSlotCell 클래스 선언에 IPointerClickHandler 추가
public class QuickSlotCell : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    // ... 기존 코드 ...

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (_eventData.button != PointerEventData.InputButton.Right)
            return;

        if (m_ItemInfoId == 0)
            return;

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        objectData.GetQuickSlotRegistry().Unregister(m_SlotIndex);
    }
}
```

- [ ] **Step 4: 컴파일 확인**

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/HUD/QuickSlotCell.cs
git commit -m "퀵슬롯 드래그 등록 및 우클릭 해제 연결"
```

---

## Task 9: 통합 확인 및 프리팹 설정

**Files:**
- 프리팹 설정 (MCP 도구 사용)

모든 코드 작업이 완료된 후, Unity 에디터에서 프리팹을 설정하고 실제 동작을 확인한다.

- [ ] **Step 1: u_editor_asset(action: refresh) 호출**

모든 스크립트 변경 후 Unity 에디터에 반영한다.

- [ ] **Step 2: InventoryPopup 프리팹 설정**

MCP `u_editor_prefab` 또는 Unity 에디터에서:
1. InventoryScroll(ScrollRect)의 `vertical` = true, `horizontal` = true 확인
2. BaseScroll의 `m_CellSize` = 80, `m_Spacing` = 5 설정
3. InventoryScrollCell 프리팹을 정사각형 (80x80)으로 수정
4. DragPreview Image 오브젝트 추가 (60x60, Raycast Target = false)
5. `m_DragPreview` SerializeField 연결

- [ ] **Step 3: QuickSlotHUD 프리팹 생성 및 HUD에 배치**

1. QuickSlotHUD 프리팹 생성 (HorizontalLayoutGroup, spacing=5)
2. QuickSlotCell 8개 배치 (각각 80x80)
3. InGameHUDWorker의 `m_QuickSlotHUD` 연결
4. HUD Canvas 하단 중앙에 배치

- [ ] **Step 4: InputSystem_Actions에 QuickSlot1~8 액션 확인**

Player 맵에 QuickSlot1~QuickSlot8 액션이 있고, 각각 키보드 1~8에 바인딩되어 있는지 확인.

- [ ] **Step 5: 플레이모드 테스트**

MCP `u_play` 도구로 플레이모드 진입 후 확인:
1. I키로 인벤토리 열기 → 그리드 형태로 표시되는지
2. 아이템 셀 클릭 → 상세 패널 표시
3. 아이템 드래그 → 다른 슬롯에 드롭 → 위치 교환
4. 아이템 드래그 → 퀵슬롯에 드롭 → 등록
5. 1~8키 → 퀵슬롯 아이템 사용
6. 퀵슬롯 우클릭 → 등록 해제

- [ ] **Step 6: 최종 커밋**

```bash
git add -A
git commit -m "그리드 인벤토리 + 퀵슬롯 통합 완료"
```
