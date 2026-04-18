#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class TestManager : MonoSingleton<TestManager>
{
    // ===== 범용 입력 시뮬레이션 =====

    public void SimulateKeyPress(string _keyName)
    {
        StartCoroutine(CoSimulateKeyPress(_keyName));
    }

    private IEnumerator CoSimulateKeyPress(string _keyName)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            GameDebug.LogError("[TestManager] Keyboard not found");
            yield break;
        }

        var key = keyboard.FindKeyOnCurrentKeyboardLayout(_keyName);
        if (key == null)
        {
            GameDebug.LogError($"[TestManager] Key '{_keyName}' not found");
            yield break;
        }

        using (StateEvent.From(keyboard, out var eventPtr))
        {
            key.WriteValueIntoEvent(1f, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }

        GameDebug.Log($"[TestManager] Key '{_keyName}' down");
        yield return new WaitForSeconds(0.1f);

        using (StateEvent.From(keyboard, out var eventPtr))
        {
            key.WriteValueIntoEvent(0f, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }

        GameDebug.Log($"[TestManager] Key '{_keyName}' up");
    }

    public void SimulateQuickSlot(int _slotIndex)
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) return;

        var objectData = controller.ObjectDataWorker;
        if (objectData == null) return;

        var quickSlot = objectData.GetQuickSlotRegistry();
        var inventory = objectData.GetInventoryRegistry();

        quickSlot.UseSlot(_slotIndex, inventory);
        GameDebug.Log($"[TestManager] QuickSlot {_slotIndex} used");
    }

    public void SimulateInventoryToggle()
    {
        var popup = Managers.Popup.FindOpen<InventoryPopup>();
        if (popup != null)
        {
            Managers.Popup.Close(popup);
            GameDebug.Log("[TestManager] InventoryPopup closed");
        }
        else
        {
            Managers.Popup.Open<InventoryPopup>();
            GameDebug.Log("[TestManager] InventoryPopup opened");
        }
    }

    public void SimulateAddItem(int _itemId)
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) return;

        var inventory = controller.ObjectDataWorker.GetInventoryRegistry();
        inventory.AddItem(_itemId, 1);
        GameDebug.Log($"[TestManager] Added item {_itemId}");
    }

    public void SimulateAddItems(string _args)
    {
        var parts = _args.Split(',');
        if (parts.Length < 2) return;

        int itemId = int.Parse(parts[0].Trim());
        int count = int.Parse(parts[1].Trim());

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) return;

        var inventory = controller.ObjectDataWorker.GetInventoryRegistry();
        inventory.AddItem(itemId, count);
        GameDebug.Log($"[TestManager] Added item {itemId} x{count}");
    }

    public void SimulateRegisterQuickSlot(string _args)
    {
        var parts = _args.Split(',');
        if (parts.Length < 2) return;

        int slotIndex = int.Parse(parts[0].Trim());
        int itemId = int.Parse(parts[1].Trim());

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) return;

        var quickSlot = controller.ObjectDataWorker.GetQuickSlotRegistry();
        quickSlot.Register(slotIndex, itemId);
        GameDebug.Log($"[TestManager] QuickSlot {slotIndex} = ItemId {itemId}");
    }
    public override void Init()
    {
        base.Init();
    }

    public override void Clear()
    {
        base.Clear();
    }

    public void TestMoveAndPopup()
    {
        StartCoroutine(CoTestMoveAndPopup());
    }

    private IEnumerator CoTestMoveAndPopup()
    {
        GameDebug.Log("[TestManager] TestMoveAndPopup 시작");

        // 로컬 플레이어 확인 (DontDestroyOnLoad에서 씬 오브젝트 접근 시 FindFirstObjectByType 사용)
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null)
        {
            GameDebug.LogError("[TestManager] InGameController가 없습니다");
            yield break;
        }

        var player = controller.PlayWorker.LocalPlayer;
        if (player == null)
        {
            GameDebug.LogError("[TestManager] LocalPlayer가 없습니다");
            yield break;
        }

        GameDebug.Log($"[TestManager] 플레이어 위치: {player.transform.position}");

        // 캐릭터 이동 테스트 (오른쪽으로 2초)
        GameDebug.Log("[TestManager] 캐릭터 이동 시작 (오른쪽)");
        player.MoveWithDirection(Vector2.right);
        yield return new WaitForSeconds(2f);
        player.MoveWithDirection(Vector2.zero);
        GameDebug.Log($"[TestManager] 이동 후 위치: {player.transform.position}");

        yield return new WaitForSeconds(0.5f);

        // 팝업 열기 테스트
        GameDebug.Log("[TestManager] CraftPopup 열기");
        var popup = Managers.Popup.Open<CraftPopup>();
        if (popup == null)
        {
            GameDebug.LogError("[TestManager] CraftPopup 열기 실패");
            yield break;
        }
        GameDebug.Log("[TestManager] CraftPopup 열림");

        yield return new WaitForSeconds(1f);

        // 팝업 닫기
        GameDebug.Log("[TestManager] CraftPopup 닫기");
        Managers.Popup.Close(popup);
        GameDebug.Log("[TestManager] CraftPopup 닫힘");

        GameDebug.Log("[TestManager] TestMoveAndPopup 완료");
    }

    public void TestGridInventoryAndQuickSlot()
    {
        StartCoroutine(CoTestGridInventoryAndQuickSlot());
    }

    private IEnumerator CoTestGridInventoryAndQuickSlot()
    {
        GameDebug.Log("[TestManager] TestGridInventoryAndQuickSlot 시작");

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null)
        {
            GameDebug.LogError("[TestManager] InGameController가 없습니다");
            yield break;
        }

        var objectData = controller.ObjectDataWorker;
        if (objectData == null)
        {
            GameDebug.LogError("[TestManager] ObjectDataWorker가 없습니다");
            yield break;
        }

        var inventory = objectData.GetInventoryRegistry();
        var quickSlot = objectData.GetQuickSlotRegistry();

        // 1. 인벤토리 슬롯 기반 확인
        GameDebug.Log($"[TestManager] 인벤토리 슬롯 수: {inventory.SlotCount}");
        inventory.Clear();

        // 2. 아이템 추가 테스트
        inventory.AddItem(1, 5);  // 나무 5개
        inventory.AddItem(2, 3);  // 돌 3개
        inventory.AddItem(3, 1);  // 검 1개
        inventory.AddItem(4, 2);  // 모닥불 2개

        var wood = inventory.GetItem(1);
        var stone = inventory.GetItem(2);
        var sword = inventory.GetItem(3);
        var campfire = inventory.GetItem(4);

        GameDebug.Log($"[TestManager] 나무: {wood?.Count ?? 0}, 돌: {stone?.Count ?? 0}, 검: {sword?.Count ?? 0}, 모닥불: {campfire?.Count ?? 0}");

        // 3. 슬롯 위치 확인
        GameDebug.Log($"[TestManager] 슬롯0: {inventory.GetSlot(0)?.Info?.Name ?? "빈칸"}, 슬롯1: {inventory.GetSlot(1)?.Info?.Name ?? "빈칸"}, 슬롯2: {inventory.GetSlot(2)?.Info?.Name ?? "빈칸"}, 슬롯3: {inventory.GetSlot(3)?.Info?.Name ?? "빈칸"}");

        // 4. 슬롯 스왑 테스트
        GameDebug.Log("[TestManager] 슬롯 0 ↔ 2 스왑");
        inventory.SwapSlots(0, 2);
        GameDebug.Log($"[TestManager] 스왑 후 슬롯0: {inventory.GetSlot(0)?.Info?.Name ?? "빈칸"}, 슬롯2: {inventory.GetSlot(2)?.Info?.Name ?? "빈칸"}");

        // 5. 퀵슬롯 등록 테스트
        quickSlot.Register(0, 1);  // 퀵슬롯1에 나무
        quickSlot.Register(1, 3);  // 퀵슬롯2에 검
        quickSlot.Register(2, 4);  // 퀵슬롯3에 모닥불
        GameDebug.Log($"[TestManager] 퀵슬롯0: ItemId={quickSlot.GetItemInfoId(0)}, 퀵슬롯1: ItemId={quickSlot.GetItemInfoId(1)}, 퀵슬롯2: ItemId={quickSlot.GetItemInfoId(2)}");

        // 6. 퀵슬롯 중복 등록 방지 테스트
        quickSlot.Register(3, 1);  // 퀵슬롯4에 나무 → 퀵슬롯1에서 자동 해제
        GameDebug.Log($"[TestManager] 나무를 퀵슬롯4로 이동 후 → 퀵슬롯0: ItemId={quickSlot.GetItemInfoId(0)}, 퀵슬롯3: ItemId={quickSlot.GetItemInfoId(3)}");

        // 7. 인벤토리 팝업 열기 (그리드 표시 확인)
        GameDebug.Log("[TestManager] InventoryPopup 열기");
        var popup = Managers.Popup.Open<InventoryPopup>();
        if (popup == null)
        {
            GameDebug.LogError("[TestManager] InventoryPopup 열기 실패");
            yield break;
        }
        GameDebug.Log("[TestManager] InventoryPopup 열림 — 그리드 표시 확인");

        yield return new WaitForSeconds(2f);

        // 8. 팝업 닫기
        Managers.Popup.Close(popup);
        GameDebug.Log("[TestManager] InventoryPopup 닫힘");

        // 9. 퀵슬롯 해제 테스트
        quickSlot.Unregister(1);
        GameDebug.Log($"[TestManager] 퀵슬롯1 해제 후: ItemId={quickSlot.GetItemInfoId(1)}");

        GameDebug.Log("[TestManager] TestGridInventoryAndQuickSlot 완료");
    }
}
#endif
