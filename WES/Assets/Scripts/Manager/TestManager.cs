#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

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

    public void TestContentExpansion()
    {
        StartCoroutine(CoTestContentExpansion());
    }

    private IEnumerator CoTestContentExpansion()
    {
        GameDebug.Log("[TestManager] TestContentExpansion 시작");

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var inventory = controller.ObjectDataWorker.GetInventoryRegistry();
        var quickSlot = controller.ObjectDataWorker.GetQuickSlotRegistry();
        inventory.Clear();

        // 1. 신규 아이템 추가 확인
        inventory.AddItem(5, 10);
        inventory.AddItem(6, 5);
        inventory.AddItem(7, 5);
        inventory.AddItem(8, 5);
        GameDebug.Log($"[TestManager] 약초:{inventory.GetItem(5)?.Count}, 가죽:{inventory.GetItem(6)?.Count}, 뼈:{inventory.GetItem(7)?.Count}, 철광석:{inventory.GetItem(8)?.Count}");

        // 2. 소비 아이템 사용 테스트
        inventory.AddItem(101, 1);
        quickSlot.Register(0, 101);
        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        int hpBefore = player.HP;
        player.AddHP(-50);
        yield return new WaitForSeconds(0.5f);
        int hpAfterDamage = player.HP;
        GameDebug.Log($"[TestManager] HP: {hpBefore} → 데미지 후 {hpAfterDamage}");

        quickSlot.UseSlot(0, inventory);
        yield return new WaitForSeconds(0.5f);
        int hpAfterPotion = player.HP;
        GameDebug.Log($"[TestManager] 포션 사용 후 HP: {hpAfterPotion} (+30 회복 기대)");
        GameDebug.Log($"[TestManager] 포션 잔여: {inventory.GetItem(101)?.Count ?? 0} (0 기대)");

        // 3. 장비 스탯 확인
        inventory.AddItem(202, 1);
        inventory.AddItem(201, 1);
        yield return null;
        player.RecalculateEquipmentStats();
        GameDebug.Log($"[TestManager] 장비 후 ATK:{player.GetATK()} (18 기대), DEF:{player.GetDEF()} (8 기대)");

        inventory.RemoveItem(202, 1);
        yield return null;
        player.RecalculateEquipmentStats();
        GameDebug.Log($"[TestManager] 철검 제거 후 ATK:{player.GetATK()} (10 기대), DEF:{player.GetDEF()} (8 기대)");

        // 4. 몬스터 정보 확인
        var monsterList = Managers.Info.MonsterInfoList;
        foreach (var m in monsterList)
        {
            GameDebug.Log($"[TestManager] 몬스터: {m.Name}, HP:{m.MaxHP}, DropTable:{m.DropTableId}");
        }

        GameDebug.Log("[TestManager] TestContentExpansion 완료");
    }

    public void TestTerrainSlope()
    {
        StartCoroutine(CoTestTerrainSlope());
    }

    private IEnumerator CoTestTerrainSlope()
    {
        GameDebug.Log("[TestManager] TestTerrainSlope 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var player = Object.FindFirstObjectByType<PlayerCharacter>();
        if (player == null) { GameDebug.LogError("[TestManager] PlayerCharacter 없음"); yield break; }

        // 시나리오 1: 슬로프 위 Y 자연 보정
        GameDebug.Log("[TestManager] 시나리오 1: 슬로프 위 이동");
        var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, 0, 35), out var nh1, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            agent.Warp(nh1.position);
            yield return new WaitForSeconds(0.3f);
            Vector3 startPos = player.transform.position;
            float moveDuration = 2f;
            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                player.MoveWithDirection(new Vector2(0, 1));
                yield return null;
                elapsed += Time.deltaTime;
            }
            player.MoveWithDirection(Vector2.zero);
            Vector3 endPos = player.transform.position;
            GameDebug.Log($"[TestManager] 시작 Y={startPos.y:F2}, 종료 Y={endPos.y:F2}, ΔZ={endPos.z - startPos.z:F2}");
            Mark(Mathf.Abs(endPos.y - startPos.y) > 0.3f || Mathf.Abs(endPos.z - startPos.z) > 1f, "슬로프 이동 + Y 변화");
        }
        else
        {
            Mark(false, "슬로프 위치 NavMesh 샘플링 실패");
        }

        yield return new WaitForSeconds(0.5f);

        // 시나리오 2: 마우스 Raycast Ground 레이어 hit
        var camera = Camera.main;
        if (camera != null)
        {
            int groundMask = 1 << LayerMask.NameToLayer("Ground");
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            bool hitOk = Physics.Raycast(ray, out RaycastHit hit, 200f, groundMask);
            Mark(hitOk, $"마우스 Raycast Ground hit (hit={hitOk})");
        }
        else { Mark(false, "Camera.main 없음"); }

        yield return new WaitForSeconds(0.5f);

        // 시나리오 3: 외곽 차단 (보조 콜라이더)
        GameDebug.Log("[TestManager] 시나리오 3: 외곽 차단");
        if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(65, 0, 0), out var nh3, 8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            agent.Warp(nh3.position);
            yield return new WaitForSeconds(0.3f);
            float outerDuration = 3f;
            float outerElapsed = 0f;
            while (outerElapsed < outerDuration)
            {
                player.MoveWithDirection(new Vector2(1, 0));
                yield return null;
                outerElapsed += Time.deltaTime;
            }
            player.MoveWithDirection(Vector2.zero);
            float dist = Mathf.Sqrt(player.transform.position.x * player.transform.position.x +
                                    player.transform.position.z * player.transform.position.z);
            GameDebug.Log($"[TestManager] 외곽 시도 후 거리: {dist:F2}");
            Mark(dist < 75f, $"섬 외곽 차단 (거리 {dist:F2} < 75)");
        }
        else { Mark(false, "외곽 좌표 NavMesh 샘플링 실패"); }

        yield return new WaitForSeconds(0.5f);

        // 시나리오 4: NavMeshAgent isOnNavMesh
        Mark(agent != null && agent.isOnNavMesh, "플레이어 NavMesh 위에 있음");

        yield return new WaitForSeconds(0.3f);

        // 시나리오 5: 다양한 높이 — Start → Escape 사이 NavMesh 샘플
        GameDebug.Log("[TestManager] 시나리오 5: 다양한 높이");
        var samples = new Vector3[] {
            new Vector3(0, 0, -42),
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 35),
            new Vector3(0, 0, 55),
        };
        int sampleHits = 0;
        foreach (var p in samples)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var nhx, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                sampleHits++;
                GameDebug.Log($"[TestManager] {p} → NavMesh Y = {nhx.position.y:F2}");
            }
            else GameDebug.LogWarning($"[TestManager] {p} → NavMesh 미적용");
        }
        Mark(sampleHits >= 3, $"NavMesh 샘플 {sampleHits}/4 영역 통과");

        yield return new WaitForSeconds(0.3f);

        // 시나리오 6: 몬스터 NavMesh
        var monsters = Object.FindObjectsByType<MonsterStateMachine>(FindObjectsSortMode.None);
        GameDebug.Log($"[TestManager] 몬스터 수: {monsters.Length}");
        if (monsters.Length > 0)
        {
            var mAgent = monsters[0].GetComponent<UnityEngine.AI.NavMeshAgent>();
            Mark(mAgent != null && mAgent.isOnNavMesh, "첫 몬스터 NavMesh 위에 있음");
        }
        else
        {
            GameDebug.LogWarning("[TestManager] 몬스터 미스폰 — 시나리오 6 SKIP");
        }

        yield return new WaitForSeconds(0.3f);

        // 시나리오 7: 나무 NavMesh carving 검증
        // 나무 양쪽 1.5m에서 NavMesh.Raycast로 직선 통과성 검사 — 막히면 carving 작동
        GameDebug.Log("[TestManager] 시나리오 7: 나무 NavMesh carving");
        var decoRoot = GameObject.Find("MapRoot/GeneratedMap/Decorations");
        if (decoRoot != null)
        {
            int treeTotal = 0;
            int treeBlocked = 0;
            foreach (Transform child in decoRoot.transform)
            {
                if (!child.name.StartsWith("Tree_")) continue;
                Vector3 treePos = child.position;
                Vector3 from = treePos + new Vector3(-1.5f, 0, 0);
                Vector3 to = treePos + new Vector3(1.5f, 0, 0);
                if (!UnityEngine.AI.NavMesh.SamplePosition(from, out var fh, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    continue;
                treeTotal++;
                // NavMesh.Raycast: 시작점에서 끝점까지 NavMesh가 끊기면 hit=true
                if (UnityEngine.AI.NavMesh.Raycast(fh.position, to, out _, UnityEngine.AI.NavMesh.AllAreas))
                    treeBlocked++;
            }
            GameDebug.Log($"[TestManager] 나무 NavMesh 차단: {treeBlocked}/{treeTotal}");
            // 나무의 절반 이상이 NavMesh를 차단해야 carving 작동으로 판정
            Mark(treeTotal > 0 && treeBlocked * 2 >= treeTotal,
                $"나무 NavMesh carving ({treeBlocked}/{treeTotal} 차단, 50% 이상 기대)");
        }
        else
        {
            Mark(false, "Decorations 루트 없음");
        }

        GameDebug.Log($"[TestManager] TestTerrainSlope 결과: PASS {passed}, FAIL {failed}");
    }

    public void TestCraftHUDTab()
    {
        StartCoroutine(CoTestCraftHUDTab());
    }

    private IEnumerator CoTestCraftHUDTab()
    {
        GameDebug.Log("[TestManager] TestCraftHUDTab 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var tab = Object.FindFirstObjectByType<CraftHUDTab>();
        if (tab == null) { GameDebug.LogError("[TestManager] CraftHUDTab 없음"); yield break; }

        // 사전 정리: 모든 팝업 닫기
        var existingCraft = Managers.Popup.FindOpen<CraftPopup>();
        if (existingCraft != null) Managers.Popup.Close(existingCraft);
        var existingInv = Managers.Popup.FindOpen<InventoryPopup>();
        if (existingInv != null) Managers.Popup.Close(existingInv);
        yield return new WaitForSeconds(0.3f);

        // 시나리오 1: 건축 버튼 — 닫혀있을 때 CraftPopup 열림
        tab.OnClickOpenBuilding();
        yield return new WaitForSeconds(0.3f);
        var craftPopup = Managers.Popup.FindOpen<CraftPopup>();
        Mark(craftPopup != null, "건축 클릭 (닫힌 상태) → CraftPopup 열림");

        // 시나리오 2: 건축 버튼 — 이미 열려있으면 동일 팝업 유지 (카테고리만 전환)
        if (craftPopup != null)
        {
            craftPopup.SelectCategory(CraftCategoryType.Item);
            yield return new WaitForSeconds(0.2f);
            tab.OnClickOpenBuilding();
            yield return new WaitForSeconds(0.3f);
            var stillOpen = Managers.Popup.FindOpen<CraftPopup>();
            Mark(stillOpen == craftPopup, "건축 클릭 (이미 열린 상태) → 동일 CraftPopup 인스턴스 유지");
        }

        // CraftPopup 정리
        if (craftPopup != null) Managers.Popup.Close(craftPopup);
        yield return new WaitForSeconds(0.3f);

        // 시나리오 3: 인벤토리 버튼 — 닫혀있을 때 InventoryPopup 열림
        tab.OnClickOpenInventory();
        yield return new WaitForSeconds(0.3f);
        var invPopup = Managers.Popup.FindOpen<InventoryPopup>();
        Mark(invPopup != null, "인벤토리 클릭 (닫힌 상태) → InventoryPopup 열림");

        // 시나리오 4: 인벤토리 버튼 — 이미 열려있으면 무시 (동일 팝업 유지)
        tab.OnClickOpenInventory();
        yield return new WaitForSeconds(0.3f);
        var invPopup2 = Managers.Popup.FindOpen<InventoryPopup>();
        Mark(invPopup2 == invPopup, "인벤토리 클릭 (이미 열린 상태) → 동일 InventoryPopup 인스턴스 유지");

        // 정리
        if (invPopup != null) Managers.Popup.Close(invPopup);

        GameDebug.Log($"[TestManager] TestCraftHUDTab 결과: PASS {passed}, FAIL {failed}");
    }

    // ===== 건축(Building) 시스템 통합 QA =====
    public void TestBuilding()
    {
        StartCoroutine(CoTestBuilding());
    }

    private IEnumerator CoTestBuilding()
    {
        GameDebug.Log("[TestManager] TestBuilding 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var placementWorker = controller.BuildingPlacementWorker;
        var inventory = controller.ObjectDataWorker?.GetInventoryRegistry();
        var quickSlot = controller.ObjectDataWorker?.GetQuickSlotRegistry();
        var player = controller.PlayWorker?.LocalPlayer;
        if (placementWorker == null || inventory == null || quickSlot == null || player == null)
        {
            GameDebug.LogError("[TestManager] 의존성 없음");
            yield break;
        }

        inventory.Clear();

        // 시나리오 1: 제작 카테고리 데이터 확인 (Building/Item 분리)
        var buildingList = Managers.Info.GetCraftInfosByCategory(CraftCategoryType.Building);
        var itemList = Managers.Info.GetCraftInfosByCategory(CraftCategoryType.Item);
        Mark(buildingList != null && buildingList.Count >= 2, $"Building 카테고리 제작 항목 수: {buildingList?.Count ?? 0} (>=2 기대: 모닥불/횃불)");
        Mark(itemList != null && itemList.Count >= 1, $"Item 카테고리 제작 항목 수: {itemList?.Count ?? 0}");

        // 시나리오 2: 모닥불(CraftId=1) 재료/조건 확인
        var materials = Managers.Info.GetMaterialsByCraftId(1);
        var conditions = Managers.Info.GetConditionsByCraftId(1);
        Mark(materials != null && materials.Count == 2, $"모닥불 재료 수: {materials?.Count ?? 0} (2 기대: 나무5, 돌2)");
        Mark(conditions != null && conditions.Count == 1, $"모닥불 조건 수: {conditions?.Count ?? 0} (1 기대: 추위<=50)");

        // 시나리오 3: CraftPopup 열기 → Building 카테고리 → 모닥불 셀 클릭 → DetailPanel 표시
        var craftPopup = Managers.Popup.Open<CraftPopup>();
        yield return new WaitForSeconds(0.3f);
        craftPopup.SelectCategory(CraftCategoryType.Building);
        yield return new WaitForSeconds(0.3f);
        Mark(craftPopup != null, "CraftPopup 열림");

        // 시나리오 4: 재료/조건 충족 시 제작 성공 → 결과 아이템 인벤토리 추가
        // (DetailPanel.OnClickCraft은 private이므로 직접 ExecuteCraft 흐름을 검증)
        // 재료 채우기: 나무5(id=1), 돌2(id=2)
        inventory.AddItem(1, 5);
        inventory.AddItem(2, 2);
        player.SetCold(30); // 추위 30 (≤50 충족)
        yield return new WaitForSeconds(0.2f);

        var detailPanel = craftPopup.GetComponentInChildren<CraftDetailPanel>(true);
        var campfireInfo = buildingList.Find(c => c.Id == 1);
        Mark(detailPanel != null, "CraftDetailPanel 존재");
        Mark(campfireInfo != null, "모닥불 CraftInfo 조회 성공");

        if (detailPanel != null && campfireInfo != null)
        {
            detailPanel.Show(campfireInfo);
            yield return new WaitForSeconds(0.3f);
            int beforeWood = inventory.GetItem(1)?.Count ?? 0;
            int beforeStone = inventory.GetItem(2)?.Count ?? 0;
            int beforeCampfire = inventory.GetItem(4)?.Count ?? 0;
            GameDebug.Log($"[TestManager] 제작 전 — 나무:{beforeWood}, 돌:{beforeStone}, 모닥불:{beforeCampfire}");

            // CraftButton 클릭 시뮬레이션
            var craftBtn = detailPanel.GetComponentInChildren<Button>(true);
            // CraftDetailPanel의 OnClickCraft은 private이므로 m_CraftButton.onClick.Invoke()
            // 하지만 m_CraftButton 직접 접근 불가 → reflection으로 호출
            var execMethod = typeof(CraftDetailPanel).GetMethod("OnClickCraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            execMethod?.Invoke(detailPanel, null);
            yield return new WaitForSeconds(0.3f);

            int afterWood = inventory.GetItem(1)?.Count ?? 0;
            int afterStone = inventory.GetItem(2)?.Count ?? 0;
            int afterCampfire = inventory.GetItem(4)?.Count ?? 0;
            GameDebug.Log($"[TestManager] 제작 후 — 나무:{afterWood}, 돌:{afterStone}, 모닥불:{afterCampfire}");
            Mark(afterWood == beforeWood - 5, "재료(나무) 5개 차감");
            Mark(afterStone == beforeStone - 2, "재료(돌) 2개 차감");
            Mark(afterCampfire == beforeCampfire + 1, "결과(모닥불 아이템) 1개 획득");
        }

        // 시나리오 5: 추위 조건 미충족 시 CanCraft false → 버튼 비활성화
        inventory.Clear();
        inventory.AddItem(1, 10);
        inventory.AddItem(2, 5);
        player.SetCold(80); // 추위 80 → 50 초과 → 조건 실패
        yield return new WaitForSeconds(0.2f);
        if (detailPanel != null && campfireInfo != null)
        {
            detailPanel.Show(campfireInfo);
            yield return new WaitForSeconds(0.2f);
            var craftButton = detailPanel.GetComponentsInChildren<Button>(true)[0];
            Mark(!craftButton.interactable, $"추위 조건 미충족 시 제작 버튼 비활성 (interactable={craftButton.interactable})");
        }

        // 시나리오 6: 재료 부족 시 CanCraft false
        inventory.Clear();
        inventory.AddItem(1, 1); // 나무 1개만 (5 필요)
        player.SetCold(20);
        yield return new WaitForSeconds(0.2f);
        if (detailPanel != null && campfireInfo != null)
        {
            detailPanel.Show(campfireInfo);
            yield return new WaitForSeconds(0.2f);
            var craftButton = detailPanel.GetComponentsInChildren<Button>(true)[0];
            Mark(!craftButton.interactable, $"재료 부족 시 제작 버튼 비활성 (interactable={craftButton.interactable})");
        }

        Managers.Popup.Close(craftPopup);
        yield return new WaitForSeconds(0.3f);

        // 시나리오 7: BuildingPlacementWorker - StartPlacement (건물 아이템)
        inventory.Clear();
        inventory.AddItem(4, 1); // 모닥불 아이템 보유
        yield return new WaitForSeconds(0.2f);
        Mark(!placementWorker.IsPlacing, "초기 IsPlacing == false");

        placementWorker.StartPlacement(4); // 모닥불 ItemInfoId
        yield return new WaitForSeconds(0.2f);
        Mark(placementWorker.IsPlacing, "StartPlacement(모닥불) 후 IsPlacing == true");

        // 시나리오 8: CancelPlacement
        placementWorker.CancelPlacement();
        yield return new WaitForSeconds(0.2f);
        Mark(!placementWorker.IsPlacing, "CancelPlacement 후 IsPlacing == false");

        // 시나리오 9: 일반 아이템(IsBuilding=false)으로 StartPlacement → 진입 안됨
        placementWorker.StartPlacement(1); // 나무 (IsBuilding=false)
        yield return new WaitForSeconds(0.2f);
        Mark(!placementWorker.IsPlacing, "일반 아이템(나무)은 StartPlacement 차단됨 (IsPlacing=false)");

        // 시나리오 10: 퀵슬롯 통합 — 건물 아이템을 퀵슬롯에 등록 → UseSlot → 자동 StartPlacement
        quickSlot.Register(0, 4); // 퀵슬롯0에 모닥불
        yield return new WaitForSeconds(0.2f);
        quickSlot.UseSlot(0, inventory);
        yield return new WaitForSeconds(0.2f);
        Mark(placementWorker.IsPlacing, "퀵슬롯 사용 → 건물 아이템 자동 StartPlacement");

        // 정리
        placementWorker.CancelPlacement();

        GameDebug.Log($"[TestManager] TestBuilding 결과: PASS {passed}, FAIL {failed}");
    }

    public void TestDamageNumber()
    {
        StartCoroutine(CoTestDamageNumber());
    }

    private IEnumerator CoTestDamageNumber()
    {
        GameDebug.Log("[TestManager] TestDamageNumber 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var worldUIWorker = controller.WorldUIWorker;
        var player = controller.PlayWorker?.LocalPlayer;
        if (worldUIWorker == null || player == null)
        {
            GameDebug.LogError("[TestManager] 의존성 없음");
            yield break;
        }

        // 시나리오 1: 플레이어 자가 피격 → 자기 머리 위에 데미지 숫자 노출
        GameDebug.Log("[TestManager] 시나리오 1: 플레이어 자가 피격");
        int beforeCount = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        player.TakeDamage(5, player);
        yield return new WaitForSeconds(0.1f);
        int afterCount = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        Mark(afterCount > beforeCount, $"피격 직후 활성 데미지 숫자 증가 ({beforeCount} → {afterCount})");

        // 시나리오 2: 0.6초 후 자동 사라짐 (풀 반환)
        yield return new WaitForSeconds(0.7f);
        int afterLifetime = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        Mark(afterLifetime <= beforeCount, $"수명 종료 후 활성 데미지 숫자 회수 ({afterLifetime} <= {beforeCount})");

        // 시나리오 3: 동시 다발 피격 — 좌우 분산 (오프셋 적용 확인)
        GameDebug.Log("[TestManager] 시나리오 3: 연속 피격 3회");
        for (int i = 0; i < 3; i++)
        {
            player.TakeDamage(3, player);
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(0.1f);
        var actives = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Mark(actives.Length >= 3, $"동시 활성 인스턴스 수 ({actives.Length} >= 3)");

        if (actives.Length >= 2)
        {
            float xSpread = 0f;
            float refX = actives[0].GetComponent<RectTransform>().localPosition.x;
            for (int i = 1; i < actives.Length; i++)
            {
                xSpread = Mathf.Max(xSpread, Mathf.Abs(actives[i].GetComponent<RectTransform>().localPosition.x - refX));
            }
            Mark(xSpread > 0f, $"좌우 분산 오프셋 적용 (최대 X 편차 {xSpread:F1})");
        }

        // 정리: 모두 사라질 때까지 대기
        yield return new WaitForSeconds(0.7f);

        // 시나리오 4: 몬스터 피격 → 몬스터 머리 위에 노출
        GameDebug.Log("[TestManager] 시나리오 4: 몬스터 피격");
        var monsters = Object.FindObjectsByType<MonsterStateMachine>(FindObjectsSortMode.None);
        if (monsters.Length > 0)
        {
            var monster = monsters[0].GetComponent<CharacterBase>();
            if (monster != null && !monster.IsDead)
            {
                int beforeMonster = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                monster.TakeDamage(2, player);
                yield return new WaitForSeconds(0.1f);
                int afterMonster = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                Mark(afterMonster > beforeMonster, $"몬스터 피격 시 데미지 숫자 노출 ({beforeMonster} → {afterMonster})");
            }
            else
            {
                GameDebug.LogWarning("[TestManager] 첫 몬스터가 없거나 사망 상태 — 시나리오 4 SKIP");
            }
        }
        else
        {
            GameDebug.LogWarning("[TestManager] 몬스터 미스폰 — 시나리오 4 SKIP");
        }

        yield return new WaitForSeconds(0.7f);

        GameDebug.Log($"[TestManager] TestDamageNumber 결과: PASS {passed}, FAIL {failed}");
    }
}
#endif
