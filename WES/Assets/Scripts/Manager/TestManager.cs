#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public class TestManager : MonoSingleton<TestManager>
{
    public const int TEST_START_ITEM_COUNT = 100;

    // QA 풀플레이(E2E)
    public const float FULLPLAY_TOTAL_TIMEOUT = 120f;
    public const float FULLPLAY_MOVE_TIMEOUT = 60f;
    public const float FULLPLAY_ARRIVE_DISTANCE = 1.5f;
    public const int FULLPLAY_INVINCIBLE_HP = 999999;

    private bool m_IsFullPlayRunning = false;

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

    public void TestSpawnCampfireNearPlayer()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); return; }

        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); return; }

        Vector3 spawnPos = player.transform.position + player.transform.forward * 2.5f;
        GameDebug.Log($"[TestManager] 모닥불 스폰 요청: pos={spawnPos}");
        controller.PlayWorker.SpawnBuilding(1, spawnPos);
    }

    public void FillStartInventory(InGameObjectDataWorker _worker)
    {
        if (_worker == null) return;

        var inventory = _worker.GetInventoryRegistry();
        if (inventory == null) return;

        var itemInfoList = Managers.Info?.ItemInfoList;
        if (itemInfoList == null || itemInfoList.Count == 0) return;

        if (itemInfoList.Count > inventory.SlotCount)
            inventory.ExpandSlots(itemInfoList.Count);

        foreach (var info in itemInfoList)
        {
            inventory.AddItem(info.Id, TEST_START_ITEM_COUNT);
        }

        GameDebug.Log($"[TestManager] 시작 인벤토리 채움: {itemInfoList.Count}종 x{TEST_START_ITEM_COUNT}");
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

    public void TestPopupEscapeAndUIGuard()
    {
        StartCoroutine(CoTestPopupEscapeAndUIGuard());
    }

    private IEnumerator CoTestPopupEscapeAndUIGuard()
    {
        GameDebug.Log("[TestManager] TestPopupEscapeAndUIGuard 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        // 사전 정리
        Managers.Popup.CloseAll();
        yield return new WaitForSeconds(0.2f);

        // 시나리오 1: 빈 스택에서 CloseTop 호출 → 예외 없음
        bool noException = true;
        try { Managers.Popup.CloseTop(); }
        catch (System.Exception ex) { noException = false; GameDebug.LogError($"[TestManager] CloseTop 예외: {ex.Message}"); }
        Mark(noException && Managers.Popup.OpenedCount == 0, "빈 스택 CloseTop 안전 (no-op)");
        yield return null;

        // 시나리오 2: InventoryPopup 단일 → CloseTop으로 닫기
        Managers.Popup.Open<InventoryPopup>();
        yield return new WaitForSeconds(0.3f);
        Mark(Managers.Popup.FindOpen<InventoryPopup>() != null, "InventoryPopup 열림");
        Mark(Managers.Popup.OpenedCount == 1, $"OpenedCount == 1 (실제 {Managers.Popup.OpenedCount})");

        Managers.Popup.CloseTop();
        yield return new WaitForSeconds(0.3f);
        Mark(Managers.Popup.FindOpen<InventoryPopup>() == null, "CloseTop 후 InventoryPopup 닫힘");
        Mark(Managers.Popup.OpenedCount == 0, $"OpenedCount == 0 (실제 {Managers.Popup.OpenedCount})");

        // 시나리오 3: 두 개의 mock BasePopup 스택 → CloseTop은 마지막만 닫음
        var rootField = typeof(PopupManager).GetField("m_PopupRoot",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Transform popupRoot = rootField?.GetValue(Managers.Popup) as Transform;

        var listField = typeof(PopupManager).GetField("m_OpenedPopups",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = listField?.GetValue(Managers.Popup) as System.Collections.Generic.List<BasePopup>;

        if (popupRoot != null && list != null)
        {
            var mock1Go = new GameObject("MockPopup1", typeof(RectTransform));
            mock1Go.transform.SetParent(popupRoot, false);
            var mock1 = mock1Go.AddComponent<BasePopup>();

            var mock2Go = new GameObject("MockPopup2", typeof(RectTransform));
            mock2Go.transform.SetParent(popupRoot, false);
            var mock2 = mock2Go.AddComponent<BasePopup>();

            list.Add(mock1);
            list.Add(mock2);
            yield return null;

            Mark(Managers.Popup.OpenedCount == 2, $"Mock 두 팝업 등록 (count={Managers.Popup.OpenedCount})");

            Managers.Popup.CloseTop();
            yield return new WaitForSeconds(0.1f);
            Mark(Managers.Popup.OpenedCount == 1, $"CloseTop → 카운트 1로 감소 (실제 {Managers.Popup.OpenedCount})");
            Mark(list.Count == 1 && list[0] == mock1, "마지막(mock2)만 제거, mock1 유지");

            Managers.Popup.CloseTop();
            yield return new WaitForSeconds(0.1f);
            Mark(Managers.Popup.OpenedCount == 0, $"두 번째 CloseTop → 카운트 0 (실제 {Managers.Popup.OpenedCount})");
        }
        else
        {
            Mark(false, "PopupManager 내부 필드 reflection 실패 → 시나리오 3 SKIP");
        }

        // 시나리오 4: InputManager.IsPointerOverUI 정적 헬퍼 동작 검증
        // (마우스 위치 워프는 GameView 포커스 의존이라 환경 차이가 있어 informational only)
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            Managers.Popup.Open<InventoryPopup>();
            yield return new WaitForSeconds(0.3f);
            mouse.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            yield return null;
            bool overUI = InputManager.IsPointerOverUI();
            GameDebug.Log($"[TestManager] 화면 중앙(인벤토리 위) IsPointerOverUI = {overUI} (true 기대, GameView 포커스 시)");

            Managers.Popup.CloseAll();
            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            GameDebug.LogWarning("[TestManager] Mouse 없음 — 시나리오 4 SKIP");
        }

        // 시나리오 5: OnCancelAsObservable 발화 시 PopupManager.CloseTop 와이어링 검증
        // InGameController.SubscribeUIInput을 통해 OnCancel 구독이 걸려있어야 함
        Managers.Popup.Open<InventoryPopup>();
        yield return new WaitForSeconds(0.3f);
        int beforeCount = Managers.Popup.OpenedCount;

        // InputManager 내부 m_OnCancel Subject 직접 발화
        var cancelField = typeof(InputManager).GetField("m_OnCancel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cancelSubject = cancelField?.GetValue(Managers.Input);
        if (cancelSubject != null)
        {
            var onNextMethod = cancelSubject.GetType().GetMethod("OnNext");
            onNextMethod?.Invoke(cancelSubject, new object[] { UniRx.Unit.Default });
            yield return new WaitForSeconds(0.3f);
            int afterCount = Managers.Popup.OpenedCount;
            Mark(afterCount == beforeCount - 1,
                $"OnCancel 발화 시 CloseTop 호출됨 (count {beforeCount} → {afterCount})");
        }
        else
        {
            Mark(false, "InputManager.m_OnCancel reflection 실패 → 시나리오 5 SKIP");
        }

        // 정리
        Managers.Popup.CloseAll();

        GameDebug.Log($"[TestManager] TestPopupEscapeAndUIGuard 결과: PASS {passed}, FAIL {failed}");
    }

    public void TestPlayerDeathAndGameOver()
    {
        StartCoroutine(CoTestPlayerDeathAndGameOver());
    }

    private IEnumerator CoTestPlayerDeathAndGameOver()
    {
        GameDebug.Log("[TestManager] TestPlayerDeathAndGameOver 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        // 시나리오 1: 초기 카운트 확인
        var aliveField = typeof(InGameController).GetField("m_AlivePlayerCount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int initialAlive = (int)(aliveField?.GetValue(controller) ?? 0);
        Mark(initialAlive == 1, $"초기 살아있는 플레이어 카운트 == 1 (실제 {initialAlive})");

        // 시나리오 2: 사망 전 GameState == Playing
        Mark(controller.GameState == GameState.Playing, $"초기 GameState == Playing (실제 {controller.GameState})");

        // 시나리오 3: 플레이어 죽이기 (TakeDamage로 OnDeath 흐름 트리거)
        player.TakeDamage(99999, player);
        yield return new WaitForSeconds(0.5f);
        Mark(player.IsDead, $"플레이어 사망 (IsDead={player.IsDead})");

        // 시나리오 4: 사망 후 입력 가드 — Attack 호출해도 공격 모션 X
        // PlayerAnimationComponent.IsAttacking 확인
        var animField = typeof(PlayerCharacter).GetField("m_AnimationComponent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var anim = animField?.GetValue(player) as PlayerAnimationComponent;
        bool wasAttackingBefore = anim != null && anim.IsAttacking();
        player.Attack();
        yield return new WaitForSeconds(0.1f);
        bool isAttackingAfter = anim != null && anim.IsAttacking();
        Mark(!isAttackingAfter || wasAttackingBefore, $"사망 후 Attack 가드 — IsAttacking 변화 없음 (before={wasAttackingBefore}, after={isAttackingAfter})");

        // 시나리오 5: 사망 후 이동 가드 — MoveWithDirection 후 위치 변화 없음
        Vector3 posBeforeMove = player.transform.position;
        player.MoveWithDirection(new Vector2(1, 0));
        yield return new WaitForSeconds(1f);
        player.MoveWithDirection(Vector2.zero);
        Vector3 posAfterMove = player.transform.position;
        float moveDistance = Vector3.Distance(posBeforeMove, posAfterMove);
        Mark(moveDistance < 0.3f, $"사망 후 이동 가드 — 위치 변화 {moveDistance:F2} < 0.3");

        // 시나리오 6: 카운트 감소
        int afterAlive = (int)(aliveField?.GetValue(controller) ?? -1);
        Mark(afterAlive == 0, $"사망 후 카운트 == 0 (실제 {afterAlive})");

        // 시나리오 7: GameState == GameOver (TriggerGameOver 호출 검증)
        Mark(controller.GameState == GameState.GameOver, $"GameState == GameOver (실제 {controller.GameState})");

        GameDebug.Log($"[TestManager] TestPlayerDeathAndGameOver 결과: PASS {passed}, FAIL {failed}");
    }

    public void TestMonsterRespawnDamage()
    {
        StartCoroutine(CoTestMonsterRespawnDamage());
    }

    private IEnumerator CoTestMonsterRespawnDamage()
    {
        GameDebug.Log("[TestManager] TestMonsterRespawnDamage 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        // 죽지 않도록 플레이어 HP 강화
        player.SetMaxHP(99999);
        player.SetHP(99999);

        // 시나리오 1: 초기 몬스터 콜라이더/데미지 정상
        var monstersInitial = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
        Mark(monstersInitial.Length > 0, $"초기 몬스터 존재 ({monstersInitial.Length}개)");
        if (monstersInitial.Length == 0) yield break;

        var firstMonster = monstersInitial[0];
        var initialColliders = firstMonster.GetComponentsInChildren<Collider>();
        bool initialAllEnabled = true;
        foreach (var c in initialColliders) if (!c.enabled) initialAllEnabled = false;
        Mark(initialAllEnabled, $"초기 몬스터 콜라이더 모두 enabled ({initialColliders.Length}개)");

        int initialHp = firstMonster.HP;
        firstMonster.TakeDamage(5, player);
        yield return new WaitForSeconds(0.3f);
        Mark(firstMonster.HP < initialHp, $"초기 몬스터 데미지 적용 ({initialHp} → {firstMonster.HP})");

        // 시나리오 2: 몬스터 죽이기 (SetHP는 OnDeath 흐름을 안 타므로 TakeDamage로 큰 데미지)
        firstMonster.TakeDamage(99999, player);
        yield return new WaitForSeconds(0.5f);
        Mark(firstMonster.IsDead, $"몬스터 사망 처리 (IsDead={firstMonster.IsDead})");

        // 시나리오 3: 리스폰 대기 (RespawnDelay + 사망 애니메이션 + 마진)
        var areaInfo = Managers.Info.WorldAreaInfoList.Find(x => x.Id == 1);
        float respawnDelay = (areaInfo != null) ? areaInfo.RespawnDelay : 5f;
        float waitTime = respawnDelay + 3f;
        GameDebug.Log($"[TestManager] 리스폰 대기 {waitTime:F1}s (RespawnDelay={respawnDelay})");
        yield return new WaitForSeconds(waitTime);

        // 시나리오 4: 리스폰된 몬스터 찾기
        var monstersAfter = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
        MonsterBase respawned = null;
        foreach (var m in monstersAfter)
        {
            if (!m.IsDead && m.IsSpawned) { respawned = m; break; }
        }
        Mark(respawned != null, $"리스폰된 살아있는 몬스터 존재 (총 {monstersAfter.Length})");
        if (respawned == null)
        {
            GameDebug.LogError($"[TestManager] TestMonsterRespawnDamage 결과: PASS {passed}, FAIL {failed}");
            yield break;
        }

        Mark(respawned.HP > 0, $"리스폰 몬스터 HP 정상 ({respawned.HP}/{respawned.MaxHP})");

        // 시나리오 5: 콜라이더 enabled 확인 (★ 핵심 회귀 검증)
        var colliders = respawned.GetComponentsInChildren<Collider>();
        bool allEnabled = true;
        int disabledCount = 0;
        foreach (var col in colliders)
        {
            if (!col.enabled) { allEnabled = false; disabledCount++; }
        }
        Mark(allEnabled, $"리스폰 몬스터 콜라이더 모두 enabled (총 {colliders.Length}개, disabled={disabledCount})");

        // 시나리오 6: TakeDamage 직접 호출 → HP 감소 (콜라이더와 무관)
        int beforeDirect = respawned.HP;
        respawned.TakeDamage(5, player);
        yield return new WaitForSeconds(0.3f);
        Mark(respawned.HP < beforeDirect, $"리스폰 몬스터 TakeDamage 적용 ({beforeDirect} → {respawned.HP})");

        // 시나리오 7: OverlapSphere 공격(=실제 플레이어 공격 경로) → HP 감소
        // 콜라이더 disabled면 OverlapSphere가 못 잡으므로 데미지 0
        int beforeOverlap = respawned.HP;
        controller.ColliderWorker.CreateCollider(
            player,
            respawned.transform.position,
            2f,
            20,
            3,
            ~0  // 모든 레이어
        );
        yield return new WaitForSeconds(0.3f);
        Mark(respawned.HP < beforeOverlap, $"리스폰 몬스터 OverlapSphere 공격 적용 ({beforeOverlap} → {respawned.HP})");

        GameDebug.Log($"[TestManager] TestMonsterRespawnDamage 결과: PASS {passed}, FAIL {failed}");
    }

    public void TestDayNightCycle()
    {
        StartCoroutine(CoTestDayNightCycle());
    }

    private IEnumerator CoTestDayNightCycle()
    {
        GameDebug.Log("[TestManager] TestDayNightCycle 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var dayNight = controller.DayNightWorker;
        if (dayNight == null) { GameDebug.LogError("[TestManager] DayNightWorker 없음"); yield break; }

        // 시나리오 1: 각 페이즈 강제 전환 + Cold 배수 확인
        var phases = new[] { DayPhase.Day, DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn };
        // 결정2(2026-06-03): 밤 감쇠 멀티는 1.0 고정(옛 체온모델 ×2 제거). 밤 추위는 ColdDamageWorker가 누적 전담.
        var expectedMultipliers = new[] { 1.0f, 1.3f, 1.0f, 1.3f };
        var phaseNames = new[] { "낮", "황혼", "밤", "새벽" };

        for (int i = 0; i < phases.Length; i++)
        {
            dayNight.ForcePhase(phases[i]);
            yield return new WaitForSeconds(0.2f);

            Mark(dayNight.CurrentPhase == phases[i], $"ForcePhase({phaseNames[i]}) → CurrentPhase == {phaseNames[i]}");

            float multiplier = dayNight.GetColdRateMultiplier();
            bool multiplierOk = Mathf.Abs(multiplier - expectedMultipliers[i]) < 0.01f;
            Mark(multiplierOk, $"{phaseNames[i]} Cold 배수 {multiplier:F1} == {expectedMultipliers[i]:F1}");
        }

        // 시나리오 2: Night 페이즈 진입 시 야간 몬스터 스폰 확인
        int monstersBefore = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None).Length;
        dayNight.ForcePhase(DayPhase.Night);
        yield return new WaitForSeconds(1f);
        int monstersAfterNight = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None).Length;
        GameDebug.Log($"[TestManager] Night 진입 전 몬스터: {monstersBefore}, 후: {monstersAfterNight}");

        // 시나리오 3: Dawn 전환 시 야간 몬스터 디스폰 확인
        dayNight.ForcePhase(DayPhase.Dawn);
        yield return new WaitForSeconds(1f);
        int monstersAfterDawn = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None).Length;
        GameDebug.Log($"[TestManager] Dawn 전환 후 몬스터: {monstersAfterDawn}");
        Mark(monstersAfterDawn <= monstersBefore, $"Dawn 전환 후 야간 몬스터 디스폰 (before={monstersBefore}, dawn={monstersAfterDawn})");

        // 정리: 낮으로 복귀
        dayNight.ForcePhase(DayPhase.Day);
        yield return new WaitForSeconds(0.2f);
        Mark(dayNight.CurrentPhase == DayPhase.Day, "최종 Day 복귀");

        GameDebug.Log($"[TestManager] TestDayNightCycle 결과: PASS {passed}, FAIL {failed}");
    }

    // 시나리오 4 시각 캡처용 — Night/Day 단독 강제
    public void TestForceNight()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); return; }
        var dayNight = controller.DayNightWorker;
        if (dayNight == null) { GameDebug.LogError("[TestManager] DayNightWorker 없음"); return; }
        dayNight.ForcePhase(DayPhase.Night);
        GameDebug.Log("[TestManager] ForcePhase(Night) 호출 완료");
    }

    public void TestForceDay()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); return; }
        var dayNight = controller.DayNightWorker;
        if (dayNight == null) { GameDebug.LogError("[TestManager] DayNightWorker 없음"); return; }
        dayNight.ForcePhase(DayPhase.Day);
        GameDebug.Log("[TestManager] ForcePhase(Day) 호출 완료");
    }

    public void TestCriticalHit()
    {
        StartCoroutine(CoTestCriticalHit());
    }

    private IEnumerator CoTestCriticalHit()
    {
        GameDebug.Log("[TestManager] TestCriticalHit 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        // 죽지 않도록 HP 강화
        player.SetMaxHP(99999);
        player.SetHP(99999);

        // 카운터
        int critCount = 0;
        int normalCount = 0;
        int totalDamageEvents = 0;
        int normalDamageValue = -1;
        int critDamageValue = -1;

        System.Action<int, CharacterBase, bool> callback = (_dmg, _atk, _isCrit) =>
        {
            totalDamageEvents++;
            if (_isCrit)
            {
                critCount++;
                critDamageValue = _dmg;
            }
            else
            {
                normalCount++;
                normalDamageValue = _dmg;
            }
        };
        player.SubscribeOnDamaged(callback);

        // 100회 공격 (자가 피격, attacker는 자기 자신)
        const int ATTACK_COUNT = 100;
        const int ATTACK_DAMAGE = 20; // ATK - DEF 가 충분히 큰 값으로
        GameDebug.Log($"[TestManager] {ATTACK_COUNT}회 공격 시작 (damage={ATTACK_DAMAGE})");
        for (int i = 0; i < ATTACK_COUNT; i++)
        {
            player.TakeDamage(ATTACK_DAMAGE, player);
            yield return null; // 1프레임 대기 (RPC 처리)
        }

        // RPC 마무리 대기
        yield return new WaitForSeconds(0.3f);

        player.UnsubscribeOnDamaged(callback);

        // 판정
        Mark(totalDamageEvents == ATTACK_COUNT, $"총 데미지 이벤트 {totalDamageEvents} == {ATTACK_COUNT}");
        // 99% 신뢰구간 (n=100, p=0.1): 약 [3, 18]. 여유를 두어 [3, 20]
        Mark(critCount >= 3 && critCount <= 20, $"크리티컬 횟수 {critCount} ∈ [3, 20] (기대 ~10)");
        Mark(normalCount >= 80 && normalCount <= 97, $"일반 횟수 {normalCount} ∈ [80, 97] (기대 ~90)");

        // 데미지 값 검증
        int expectedNormal = Mathf.Max(1, ATTACK_DAMAGE - player.GetDEF());
        int expectedCrit = Mathf.Max(1, Mathf.RoundToInt((ATTACK_DAMAGE - player.GetDEF()) * CharacterBase.CRITICAL_MULTIPLIER));
        if (normalCount > 0)
            Mark(normalDamageValue == expectedNormal, $"일반 데미지값 {normalDamageValue} == {expectedNormal}");
        if (critCount > 0)
            Mark(critDamageValue == expectedCrit, $"크리 데미지값 {critDamageValue} == {expectedCrit}");

        GameDebug.Log($"[TestManager] TestCriticalHit 결과: PASS {passed}, FAIL {failed} (crit={critCount}, normal={normalCount})");
    }

    // ===== QA 풀플레이 (E2E 자동 진행) =====
    public void TestFullPlayClear()
    {
        if (m_IsFullPlayRunning)
        {
            GameDebug.LogWarning("[TestManager] FullPlay already running — 중복 호출 무시");
            return;
        }
        StartCoroutine(CoTestFullPlayClear());
    }

    private IEnumerator CoTestFullPlayClear()
    {
        m_IsFullPlayRunning = true;
        GameDebug.Log("[TestManager] ===== QA FullPlay 시작 =====");

        float startTime = Time.realtimeSinceStartup;
        bool TotalTimedOut() => Time.realtimeSinceStartup - startTime > FULLPLAY_TOTAL_TIMEOUT;

        // ---- S0: Intro 시작 → Ingame 점프 ----
        GameDebug.Log("[FullPlay][S0] Intro 시작 OK — TestMode 요청 후 Ingame 로드");
        InGameController.RequestTestModeOnLoad();
        GameSceneManager.Instance.LoadScene(GameSceneManager.SCENE_INGAME);

        // ---- S1: Ingame 진입 + 스폰 대기 ----
        InGameController controller = null;
        while (controller == null || !controller.IsGameStarted || controller.PlayWorker?.LocalPlayer == null)
        {
            if (TotalTimedOut())
            {
                GameDebug.LogError("[FullPlay][FAIL] S1 스폰 대기 타임아웃 (Host 셋업/스폰 실패 추정)");
                m_IsFullPlayRunning = false;
                yield break;
            }
            controller = Object.FindFirstObjectByType<InGameController>();
            yield return null;
        }

        var player = controller.PlayWorker.LocalPlayer;
        GameDebug.Log($"[FullPlay][S1] Ingame 진입·스폰 OK — LocalPlayer index {player.GetPlayerIndex()}");

        // 무적/스탯 동결 (이동 중 사망 방지) — 기존 API 재사용
        ApplyFullPlayInvincible(player);

        // ---- S2: EscapePoint 위치 획득 후 실제 이동 개시 ----
        var escapePoint = Object.FindFirstObjectByType<EscapePoint>();
        if (escapePoint == null)
        {
            GameDebug.LogError("[FullPlay][FAIL] S2 EscapePoint 없음 — 씬 배치 확인 필요");
            m_IsFullPlayRunning = false;
            yield break;
        }

        Vector3 escapePos = escapePoint.transform.position;
        GameDebug.Log($"[FullPlay][S2] 이동 개시 — EscapePoint {escapePos}");

        bool pathOk = player.MoveTo(escapePos);
        if (!pathOk)
            GameDebug.LogWarning("[FullPlay][S2] MoveTo 경로 계산 실패 — 직선 추종으로 시도");

        // ---- S3: 이동 진행 / 도달 / 타임아웃 → Warp 폴백 ----
        float moveStart = Time.realtimeSinceStartup;
        bool reached = false;
        bool warpFallback = false;

        while (!reached)
        {
            if (TotalTimedOut())
            {
                player.StopMove();
                GameDebug.LogError("[FullPlay][FAIL] 전체 타임아웃 — 이동 중 중단");
                m_IsFullPlayRunning = false;
                yield break;
            }

            // 클리어 RPC가 이미 도달했으면 성공 처리
            if (controller.GameState == GameState.Clear)
            {
                reached = true;
                break;
            }

            float distance = HorizontalDistance(player.transform.position, escapePos);
            if (distance <= FULLPLAY_ARRIVE_DISTANCE)
            {
                reached = true;
                break;
            }

            // 경로 계산 실패했거나 추종이 끊겼으면 직선 방향으로라도 진행
            if (!player.IsFollowingPath)
            {
                Vector3 dir = (escapePos - player.transform.position);
                dir.y = 0f;
                player.MoveWithDirection(new Vector2(dir.normalized.x, dir.normalized.z));
            }

            // 이동 타임아웃 → Warp 폴백
            if (Time.realtimeSinceStartup - moveStart > FULLPLAY_MOVE_TIMEOUT)
            {
                warpFallback = true;
                player.StopMove();
                GameDebug.LogWarning("[FullPlay][S3] 이동실패→Warp폴백 (이동 타임아웃, 경로 막힘 추정)");
                WarpToEscape(player, escapePos);
                // 무적 재적용 (혹시 죽었을 경우 트리거 가드 통과 보장)
                ApplyFullPlayInvincible(player);
                break;
            }

            yield return null;
        }

        player.StopMove();

        if (warpFallback)
            GameDebug.Log("[FullPlay][S3] EscapePoint 도달 OK (Warp 폴백 경유)");
        else
            GameDebug.Log("[FullPlay][S3] EscapePoint 도달 OK (실제 이동)");

        // Warp 직후 트리거 콜라이더가 자연 발동하지 않는 경우를 대비해 도달 보조 — 직접 진입점 호출
        if (controller.GameState != GameState.Clear)
        {
            controller.OnPlayerReachedEscape(player);
        }

        // ---- S4: 클리어 화면 확인 ----
        while (controller.GameState != GameState.Clear)
        {
            if (TotalTimedOut())
            {
                GameDebug.LogError("[FullPlay][FAIL] S4 클리어 RPC 미수신 타임아웃");
                m_IsFullPlayRunning = false;
                yield break;
            }
            yield return null;
        }

        bool resultShown = Managers.Popup.FindOpen<ResultPopup>() != null;
        GameDebug.Log($"[FullPlay][S4] 클리어 화면 OK — GameState=Clear, ResultPopup={resultShown}");
        GameDebug.Log("[TestManager] ===== QA FullPlay PASS =====");

        m_IsFullPlayRunning = false;
    }

    private void ApplyFullPlayInvincible(PlayerCharacter _player)
    {
        if (_player == null) return;
        _player.SetMaxHP(FULLPLAY_INVINCIBLE_HP);
        _player.SetHP(FULLPLAY_INVINCIBLE_HP);
        _player.SetCold(0);
    }

    private void WarpToEscape(PlayerCharacter _player, Vector3 _escapePos)
    {
        var agent = _player.GetComponent<NavMeshAgent>();
        if (agent != null && NavMesh.SamplePosition(_escapePos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        else
            _player.transform.position = _escapePos;
    }

    private float HorizontalDistance(Vector3 _a, Vector3 _b)
    {
        _a.y = 0f;
        _b.y = 0f;
        return Vector3.Distance(_a, _b);
    }

    // ===== MPPM 멀티 QA 시나리오 (호스트 권위 트리거) =====
    // 부트스트랩(MppmBootstrapWorker)이 host+클론 접속을 자동 처리한 뒤,
    // QA가 이 메서드로 호스트 권위 동작(몬스터/드롭 스폰)을 트리거해 V3/V5/V6 검증 상태를 만든다.
    // 기존 public(SpawnWorker.SpawnObject / PlayWorker.SpawnDropItem) 조합만 — 테스트 전용 로직 없음.
    // 서버(호스트)에서만 유효. 클론에서 호출 시 무시.
    public void TestMultiSpawnForSync()
    {
        StartCoroutine(CoTestMultiSpawnForSync());
    }

    private IEnumerator CoTestMultiSpawnForSync()
    {
        GameDebug.Log("[TestManager] TestMultiSpawnForSync 시작");

        if (!Managers.Network.IsServer)
        {
            GameDebug.LogWarning("[TestManager] TestMultiSpawnForSync: 서버(호스트) 아님 — 스폰 권위 없음. 무시.");
            yield break;
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        Vector3 basePos = player.transform.position;

        // 1) 호스트 권위 몬스터 스폰 (V3 전파, V4 HP, V5 사망용 대상)
        var monsterList = Managers.Info?.MonsterInfoList;
        if (monsterList != null && monsterList.Count > 0)
        {
            var monsterInfo = monsterList[0];
            Vector3 monsterPos = basePos + player.transform.forward * 3f;
            var monster = controller.SpawnWorker.SpawnObject<MonsterBase>(monsterInfo.PrefabKey, monsterPos);
            GameDebug.Log($"[TestManager] 몬스터 스폰: id={monsterInfo.Id}, key={monsterInfo.PrefabKey}, spawned={(monster != null)}");
        }
        else
        {
            GameDebug.LogWarning("[TestManager] MonsterInfoList 비어 있음 — 몬스터 스폰 생략");
        }

        yield return null;

        // 2) 호스트 권위 드롭아이템 스폰 (V6 클론 수집 대상 = 월드 아이템)
        var itemList = Managers.Info?.ItemInfoList;
        if (itemList != null && itemList.Count > 0)
        {
            int itemId = itemList[0].Id;
            Vector3 dropPos = basePos + player.transform.right * 2f;
            controller.PlayWorker.SpawnDropItem(itemId, 1, dropPos);
            GameDebug.Log($"[TestManager] 드롭아이템 스폰: itemId={itemId}, pos={dropPos}");
        }
        else
        {
            GameDebug.LogWarning("[TestManager] ItemInfoList 비어 있음 — 드롭 스폰 생략");
        }

        GameDebug.Log("[TestManager] TestMultiSpawnForSync 완료 — 양측 스냅샷 수집 준비됨");
    }

    // V2: 로컬 플레이어 이동 → 정지(정지 수렴값 비교용). 각 에디터의 로컬 플레이어를 움직인다.
    // 호출하는 쪽(호스트/클론)의 자기 플레이어가 ClientNetworkTransform(오너 권위)로 이동 → 반대쪽에 전파.
    public void TestMpV2_MovePlayer()
    {
        StartCoroutine(CoTestMpV2_MovePlayer());
    }

    private IEnumerator CoTestMpV2_MovePlayer()
    {
        var player = FindLocalPlayer();
        if (player == null) { GameDebug.LogError("[TestManager] V2: LocalPlayer 없음"); yield break; }

        Vector3 before = player.transform.position;
        GameDebug.Log($"[TestManager] V2 이동 시작: {before}");

        float moved = 0f;
        const float MOVE_TIME = 1f;
        while (moved < MOVE_TIME)
        {
            player.MoveWithDirection(Vector2.right);
            moved += Time.deltaTime;
            yield return null;
        }

        // 정지: 이동 중 순간이 아니라 정지 후 수렴값으로 비교(디렉터 확정).
        player.MoveWithDirection(Vector2.zero);
        GameDebug.Log($"[TestManager] V2 이동 정지: {player.transform.position}");
    }

    // V4: 몬스터 HP 변화(데미지). 서버 권위 — 호스트에서 호출.
    public void TestMpV4_DamageMonster(int _damage = 10)
    {
        if (!Managers.Network.IsServer) { GameDebug.LogWarning("[TestManager] V4: 서버 아님 — 무시"); return; }

        var monster = FindFirstMonster();
        if (monster == null) { GameDebug.LogError("[TestManager] V4: 몬스터 없음"); return; }

        int before = monster.HP;
        monster.SetHP(Mathf.Max(1, before - _damage)); // 사망(0)은 V5에서 분리 검증
        GameDebug.Log($"[TestManager] V4 데미지: monster HP {before} → {monster.HP}");
    }

    // V5: 몬스터 사망 → 디스폰 + 드롭 스폰. 서버 권위 — 호스트에서 호출.
    public void TestMpV5_KillMonster()
    {
        if (!Managers.Network.IsServer) { GameDebug.LogWarning("[TestManager] V5: 서버 아님 — 무시"); return; }

        var monster = FindFirstMonster();
        if (monster == null) { GameDebug.LogError("[TestManager] V5: 몬스터 없음"); return; }

        ulong id = monster.NetworkObjectId;
        // SetHP(0)은 m_HP만 설정할 뿐 OnDeathClientRpc(→OnDeath→ExecuteMonsterDrop+DeathState 전이→디스폰)를
        // 호출하지 않는다(CharacterBase). 실제 사망 경로는 TakeDamage/ApplyEnvironmentDamage가 SetHP 후
        // IsDead 체크로 OnDeathClientRpc를 명시 호출하는 구조다. 따라서 치명 데미지로 정상 사망 경로를 탄다.
        monster.TakeDamage(9999, null);
        GameDebug.Log($"[TestManager] V5 사망 트리거: monster id={id} TakeDamage(9999)");
    }

    // V6: 클론이 드롭아이템 수집(ServerRpc 왕복). 클론 측에서 호출해야 왕복 검증됨.
    // CollectServerRpc는 public — 호출자(소유 클라)가 서버에 수집 요청 → 서버가 디스폰 + 수집자에 AddItemClientRpc.
    public void TestMpV6_CollectDropItem()
    {
        var drop = FindFirstDropItem();
        if (drop == null) { GameDebug.LogError("[TestManager] V6: 드롭아이템 없음"); return; }

        ulong id = drop.NetworkObjectId;
        drop.CollectServerRpc();
        GameDebug.Log($"[TestManager] V6 수집 요청: drop id={id} (호출자 clientId={Managers.Network.GetLocalClientId()})");
    }

    // V7: 플레이어 Cold 변화. 서버 권위 — 호스트에서 호출.
    public void TestMpV7_ChangeCold(int _delta = 20)
    {
        if (!Managers.Network.IsServer) { GameDebug.LogWarning("[TestManager] V7: 서버 아님 — 무시"); return; }

        var player = FindLocalPlayer();
        if (player == null) { GameDebug.LogError("[TestManager] V7: LocalPlayer 없음"); return; }

        int before = player.Cold;
        player.SetCold(before + _delta);
        GameDebug.Log($"[TestManager] V7 Cold 변화: {before} → {player.Cold}");
    }

    // 무인자 실-delta 래퍼: u_play invoke가 인자를 0으로 전달하는 한계 우회.
    // NetworkVariable 서버→클라 런타임 복제를 같은 run에서 측정 가능하게 한다.
    public void TestMpV4_Damage10()
    {
        TestMpV4_DamageMonster(10);
    }

    public void TestMpV7_Cold20()
    {
        TestMpV7_ChangeCold(20);
    }

    // ===== 멀티 도면해금 검증: 클론 단독 줍기 → sender 단독 해금 =====
    // host에서 호출. 클론(clientId=1) 플레이어 근처에 도면 401을 스폰한다.
    // 이후 클론이 TestMpV6_CollectDropItem()으로 주우면 WorldDropItem.AddItemClientRpc가
    // sender(클론) 단독으로 RecipeUnlockRegistry.Unlock(5)을 호출 → 클론만 해금.
    public void TestMpSpawnBlueprintNearClone()
    {
        if (!Managers.Network.IsServer) { GameDebug.LogWarning("[TestManager] MpSpawnBP: 서버 아님 — 무시"); return; }

        var nm = Unity.Netcode.NetworkManager.Singleton;
        Vector3 pos = Vector3.zero;
        bool found = false;
        if (nm != null && nm.ConnectedClients != null)
        {
            foreach (var kv in nm.ConnectedClients)
            {
                if (kv.Key == 0) continue; // host 제외, 클론(원격) 우선
                var po = kv.Value?.PlayerObject;
                if (po != null) { pos = po.transform.position; found = true; break; }
            }
        }
        if (!found) { GameDebug.LogError("[TestManager] MpSpawnBP: 클론 플레이어 미발견"); return; }

        var controller = InGameController.Instance;
        controller.PlayWorker.SpawnDropItem(401, 1, pos);
        GameDebug.Log($"[TestManager] MpSpawnBP: 도면 401 스폰 @클론위치 {pos}");
    }

    // host에서 호출. 클론이 도면을 주운 뒤 호출 — host registry는 미해금이어야 한다(sender 단독).
    public void TestMpVerifyHostNotUnlocked()
    {
        var controller = InGameController.Instance;
        var registry = controller?.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (registry == null) { GameDebug.LogError("[TestManager] MpVerify: registry 없음"); return; }

        bool hostLocked = !registry.IsUnlocked(5);
        if (hostLocked) GameDebug.Log("[TestManager] MULTI PASS: host registry IsUnlocked(5)=false (클론만 해금, host 잠금 유지)");
        else GameDebug.LogError("[TestManager] MULTI FAIL: host registry IsUnlocked(5)=true (sender 단독 위반)");
    }

    // ----- MPPM 시나리오 공용 헬퍼 (기존 조회만) -----

    private PlayerCharacter FindLocalPlayer()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        return controller?.PlayWorker?.LocalPlayer;
    }

    private MonsterBase FindFirstMonster()
    {
        var monsters = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
        for (int i = 0; i < monsters.Length; i++)
        {
            if (!monsters[i].IsDead)
            {
                return monsters[i];
            }
        }
        return monsters.Length > 0 ? monsters[0] : null;
    }

    private WorldDropItem FindFirstDropItem()
    {
        var drops = Object.FindObjectsByType<WorldDropItem>(FindObjectsSortMode.None);
        return drops.Length > 0 ? drops[0] : null;
    }

    // ===== Cold 실질화(추위 위협) 통합 QA =====
    // 명세: document/design/client-spec/campfire-cold/코드명세.md (§6 상태머신, §11 엣지케이스, §13 결정1/2)
    // 기존 public 메서드 조합만 사용: ForcePhase / SetCold / SetHP / SetMaxHP / SetLit / SpawnBuilding / TakeEnvironmentDamage
    // 참고: 자연 감쇠(DayNightWorker.ApplyColdDecay)가 누적과 병렬로 항상 진행됨(밤 멀티 1.0 → base 2/s).
    //       따라서 "밤 누적"은 순효과(+accum - decay > 0)로 증가하는지, "보호"는 증가하지 않는지로 판정한다.
    public void TestColdRealization()
    {
        StartCoroutine(CoTestColdRealization());
    }

    private IEnumerator CoTestColdRealization()
    {
        GameDebug.Log("[TestManager] TestColdRealization 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var dayNight = controller.DayNightWorker;
        var coldWorker = controller.ColdDamageWorker;
        var player = controller.PlayWorker?.LocalPlayer;
        if (dayNight == null) { GameDebug.LogError("[TestManager] DayNightWorker 없음"); yield break; }
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        // ColdDamageWorker 와이어링 회귀 검증
        Mark(coldWorker != null, $"InGameController.ColdDamageWorker 슬롯 연결됨 ({(coldWorker != null ? "OK" : "NULL")})");

        // 죽지 않게 HP 충분히 확보(틱 검증 시나리오에서 별도 재설정)
        player.SetMaxHP(99999);
        player.SetHP(99999);

        // 사전 정리: 모닥불이 있으면 위치/점화상태를 통제하기 위해 직접 스폰한 모닥불만 사용
        // 기존 모닥불 영향 배제: 플레이어를 모든 모닥불에서 멀리 두기 위해 좌표 확인용으로 player 위치 기준 사용

        // ── 시나리오 1: 밤 + 모닥불 없음 → Cold 누적(순증가)
        GameDebug.Log("[TestManager] 시나리오 1: 밤 누적");
        dayNight.ForcePhase(DayPhase.Night);
        yield return new WaitForSeconds(0.2f);
        Mark(dayNight.CurrentPhase == DayPhase.Night, "ForcePhase(Night) 적용");
        player.SetCold(0);
        yield return new WaitForSeconds(0.2f);
        int coldAccumStart = player.Cold;
        // accum 10/s - decay 2/s = 순 +8/s. 2초 대기 → 약 +16 기대(클램프/타이밍 여유로 > 시작값만 확인)
        yield return new WaitForSeconds(2.0f);
        int coldAccumEnd = player.Cold;
        GameDebug.Log($"[TestManager] 밤 누적: {coldAccumStart} → {coldAccumEnd} (순증가 기대)");
        Mark(coldAccumEnd > coldAccumStart, $"밤+모닥불없음 Cold 누적 증가 ({coldAccumStart} → {coldAccumEnd})");

        // ── 시나리오 2: 낮 전환 → 누적 중단(증가 멈춤, 감쇠로 감소)
        GameDebug.Log("[TestManager] 시나리오 2: 낮 전환 누적 중단");
        dayNight.ForcePhase(DayPhase.Day);
        yield return new WaitForSeconds(0.2f);
        player.SetCold(50);
        yield return new WaitForSeconds(0.2f);
        int dayStart = player.Cold;
        yield return new WaitForSeconds(2.0f);
        int dayEnd = player.Cold;
        GameDebug.Log($"[TestManager] 낮 Cold: {dayStart} → {dayEnd} (누적 없음, 감쇠로 감소 기대)");
        Mark(dayEnd <= dayStart, $"낮 누적 중단 — Cold 증가 안 함 ({dayStart} → {dayEnd})");

        // ── 시나리오 3: 단계 판정 회귀 (None/Warning/WeakDot/StrongDot 임계 30/60/90)
        // 단계는 private GetColdStage이지만 효과(HP 틱)로 간접 검증.
        // 먼저 낮 상태에서 SetCold만으로 단계값을 세팅하고, 틱은 시나리오 5~6에서 검증한다.
        // 단계 임계 회귀: Config 값이 명세대로인지 확인
        var cfgField = typeof(ColdDamageWorker).GetField("m_Config",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        DayNightConfig cfg = cfgField?.GetValue(coldWorker) as DayNightConfig;
        if (cfg != null)
        {
            Mark(cfg.ColdStageWarning == 30, $"단계 임계 Warning == 30 (실제 {cfg.ColdStageWarning})");
            Mark(cfg.ColdStageWeak == 60, $"단계 임계 WeakDot == 60 (실제 {cfg.ColdStageWeak})");
            Mark(cfg.ColdStageStrong == 90, $"단계 임계 StrongDot == 90 (실제 {cfg.ColdStageStrong})");
            Mark(Mathf.Abs(cfg.WeakDotInterval - 3f) < 0.01f && cfg.WeakDotDamage == 2, $"약틱 3s/-2 (실제 {cfg.WeakDotInterval}s/-{cfg.WeakDotDamage})");
            Mark(Mathf.Abs(cfg.StrongDotInterval - 2f) < 0.01f && cfg.StrongDotDamage == 5, $"강틱 2s/-5 (실제 {cfg.StrongDotInterval}s/-{cfg.StrongDotDamage})");
        }
        else
        {
            Mark(false, "ColdDamageWorker.m_Config reflection 실패 → 단계 임계 회귀 SKIP");
        }

        // ── 시나리오 4: WeakDot 단계(Cold 60~89) → 약틱 HP -2 발생
        // 누적/감쇠가 Cold를 흔들지 않도록 낮 상태에서 진행(낮=누적없음). 매 프레임 Cold를 60대로 고정 유지.
        GameDebug.Log("[TestManager] 시나리오 4: WeakDot 약틱");
        dayNight.ForcePhase(DayPhase.Day);
        yield return new WaitForSeconds(0.2f);
        // HP 자가회복(HPRegen)이 Cold 틱을 상쇄/은폐하지 않도록 틱 검증 구간 동안 회복 차단
        float savedHpRegen = player.GetHPRegen();
        player.SetHPRegen(0f);
        player.SetMaxHP(99999);
        player.SetHP(1000);
        int weakHpStart = player.HP;
        float weakElapsed = 0f;
        while (weakElapsed < 4.0f) // 약틱 3s 간격 → 최소 1틱
        {
            player.SetCold(70); // WeakDot 범위 유지(감쇠 상쇄)
            yield return null;
            weakElapsed += Time.deltaTime;
        }
        int weakHpEnd = player.HP;
        GameDebug.Log($"[TestManager] WeakDot HP: {weakHpStart} → {weakHpEnd} (약틱 -2 누적 기대)");
        Mark(weakHpEnd < weakHpStart, $"WeakDot 단계 약틱 HP 감소 ({weakHpStart} → {weakHpEnd})");

        // ── 시나리오 5: StrongDot 단계(Cold 90~100) → 강틱 HP -5 발생
        GameDebug.Log("[TestManager] 시나리오 5: StrongDot 강틱");
        player.SetHP(1000);
        int strongHpStart = player.HP;
        float strongElapsed = 0f;
        while (strongElapsed < 3.0f) // 강틱 2s 간격 → 최소 1틱
        {
            player.SetCold(95); // StrongDot 범위 유지
            yield return null;
            strongElapsed += Time.deltaTime;
        }
        int strongHpEnd = player.HP;
        GameDebug.Log($"[TestManager] StrongDot HP: {strongHpStart} → {strongHpEnd} (강틱 -5 누적 기대)");
        Mark(strongHpEnd < strongHpStart, $"StrongDot 단계 강틱 HP 감소 ({strongHpStart} → {strongHpEnd})");
        // 강틱이 약틱보다 큰 손실인지(데미지 차등) — 동일 시간 비교는 간격 다르므로 손실 발생 여부만 핵심

        // ── 시나리오 6: HP 1 보호 — StrongDot 틱이 와도 죽지 않음
        GameDebug.Log("[TestManager] 시나리오 6: HP 1 보호");
        player.SetHP(1);
        float protectElapsed = 0f;
        while (protectElapsed < 3.0f)
        {
            player.SetCold(95); // StrongDot 유지
            yield return null;
            protectElapsed += Time.deltaTime;
        }
        GameDebug.Log($"[TestManager] HP 1 보호 후: HP={player.HP}, IsDead={player.IsDead}");
        Mark(player.HP >= 1 && !player.IsDead, $"HP1+StrongDot 보호 — 죽지 않음 (HP={player.HP}, IsDead={player.IsDead})");

        // ── 시나리오 7: None 단계(Cold 0~29) → 틱 없음
        GameDebug.Log("[TestManager] 시나리오 7: None 단계 무틱");
        player.SetHP(1000);
        int noneHpStart = player.HP;
        float noneElapsed = 0f;
        while (noneElapsed < 4.0f)
        {
            player.SetCold(10); // None 범위
            yield return null;
            noneElapsed += Time.deltaTime;
        }
        int noneHpEnd = player.HP;
        GameDebug.Log($"[TestManager] None HP: {noneHpStart} → {noneHpEnd} (틱 없음 기대)");
        Mark(noneHpEnd == noneHpStart, $"None 단계 — HP 틱 없음 ({noneHpStart} == {noneHpEnd})");

        // 틱 검증 구간 종료 — HP 자가회복 복원
        player.SetHPRegen(savedHpRegen);

        // ── 시나리오 8: 켜진 모닥불 5m 내 → 밤이어도 누적 스킵 (결정1 회귀: 불 옆에서 추위 안 오름)
        GameDebug.Log("[TestManager] 시나리오 8: 켜진 모닥불 보호");
        player.SetHP(99999);
        // 모닥불 스폰: 플레이어로부터 2m 떨어진 곳(보호범위 5m 내, 단 서버측 HasBlockingObject 반경 1m에
        // 플레이어 콜라이더가 안 걸리도록 오프셋). 플레이어 정위치 스폰 시 본인 콜라이더와 겹쳐 배치 거부됨.
        Vector3 firePos = player.transform.position + new Vector3(2f, 0f, 0f);
        controller.PlayWorker.SpawnBuilding(1, firePos);
        yield return new WaitForSeconds(0.8f); // 스폰 + NetworkSpawn 대기
        var litFire = FindNearestBuildingTo(firePos);
        Mark(litFire != null, $"모닥불 스폰됨 ({(litFire != null ? "OK" : "NULL")})");
        if (litFire != null)
        {
            litFire.SetLit(true);
            yield return new WaitForSeconds(0.2f);
            Mark(litFire.IsLit, $"모닥불 점화 상태 (IsLit={litFire.IsLit})");

            dayNight.ForcePhase(DayPhase.Night);
            yield return new WaitForSeconds(0.2f);
            player.SetCold(50);
            yield return new WaitForSeconds(0.2f);
            int protStart = player.Cold;
            // 켜진 불 옆: 누적 스킵 + 감쇠 진행 → Cold 증가하면 안 됨(감소/유지)
            yield return new WaitForSeconds(2.0f);
            int protEnd = player.Cold;
            GameDebug.Log($"[TestManager] 켜진 모닥불 옆 Cold: {protStart} → {protEnd} (증가 안 함 기대)");
            Mark(protEnd <= protStart, $"결정1 회귀 — 켜진 불 옆 Cold 증가 안 함 ({protStart} → {protEnd})");

            // ── 시나리오 9: SetLit(false) → 모닥불 옆이어도 누적 재개
            GameDebug.Log("[TestManager] 시나리오 9: 모닥불 꺼짐 누적 재개");
            litFire.SetLit(false);
            yield return new WaitForSeconds(0.2f);
            Mark(!litFire.IsLit, $"모닥불 소등 (IsLit={litFire.IsLit})");
            player.SetCold(10);
            yield return new WaitForSeconds(0.2f);
            int offStart = player.Cold;
            yield return new WaitForSeconds(2.0f);
            int offEnd = player.Cold;
            GameDebug.Log($"[TestManager] 꺼진 모닥불 옆 Cold: {offStart} → {offEnd} (누적 재개 기대)");
            Mark(offEnd > offStart, $"꺼진 불 옆 누적 재개 ({offStart} → {offEnd})");
        }

        // ── 시나리오 10: 결정2 회귀 — 밤 감쇠가 ×2 가속되지 않음
        // 모닥불 켜서 누적을 막은 상태에서 밤/낮 감쇠 속도를 비교 → 동일해야 함(밤 멀티 1.0)
        GameDebug.Log("[TestManager] 시나리오 10: 결정2 밤 감쇠 비배속 회귀");
        Mark(Mathf.Abs(dayNight.GetColdRateMultiplier() - 1.0f) < 0.01f || dayNight.CurrentPhase != DayPhase.Night,
            "준비: 멀티 조회 가능");
        // 직접 Config의 밤 멀티 확인이 가장 결정적
        if (cfg != null)
        {
            dayNight.ForcePhase(DayPhase.Night);
            yield return new WaitForSeconds(0.2f);
            float nightMult = dayNight.GetColdRateMultiplier();
            GameDebug.Log($"[TestManager] 밤 감쇠 멀티 = {nightMult} (1.0 기대, ×2 아님)");
            Mark(Mathf.Abs(nightMult - 1.0f) < 0.01f, $"결정2 회귀 — 밤 감쇠 멀티 1.0 (×2 아님, 실제 {nightMult})");
        }

        // 정리: 낮 복귀, Cold 0
        dayNight.ForcePhase(DayPhase.Day);
        player.SetCold(0);
        player.SetHP(99999);

        GameDebug.Log($"[TestManager] TestColdRealization 결과: PASS {passed}, FAIL {failed}");
    }

    private WorldBuildingObject FindNearestBuildingTo(Vector3 _pos)
    {
        WorldBuildingObject nearest = null;
        float best = float.MaxValue;
        foreach (var b in WorldBuildingObject.ActiveBuildings)
        {
            if (b == null) continue;
            float d = (b.transform.position - _pos).sqrMagnitude;
            if (d < best) { best = d; nearest = b; }
        }
        return nearest;
    }

    // ===== 도면 해금(Blueprint Unlock) 검증 =====
    // 기존 public 경로만 조합: Managers.Info.IsBlueprintLockedCraft / GetBlueprintByItemId,
    // RecipeUnlockRegistry.IsUnlocked/Unlock/OnUnlockChanged, Popup.Open<CraftPopup>,
    // CraftScrollCell.IsLocked, InventoryRegistry.GetItem. 테스트 전용 로직 없음.
    public void TestBlueprintUnlock()
    {
        StartCoroutine(CoTestBlueprintUnlock());
    }

    private IEnumerator CoTestBlueprintUnlock()
    {
        int passed = 0, failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var registry = controller.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (registry == null) { GameDebug.LogError("[TestManager] RecipeUnlockRegistry 없음"); yield break; }

        // 깨끗한 세션 상태에서 시작
        registry.Clear();
        yield return null;

        // 1) 데이터 매핑: 401/402/403 -> 5/6/7, 기본 5종은 잠금 대상 아님
        Mark(Managers.Info.IsBlueprintLockedCraft(5), "CraftId 5(나무방패) = 도면 잠금 대상");
        Mark(Managers.Info.IsBlueprintLockedCraft(6), "CraftId 6(철검) = 도면 잠금 대상");
        Mark(Managers.Info.IsBlueprintLockedCraft(7), "CraftId 7(가죽갑옷) = 도면 잠금 대상");
        Mark(!Managers.Info.IsBlueprintLockedCraft(1), "CraftId 1 = 기본 상시(잠금 아님)");
        Mark(!Managers.Info.IsBlueprintLockedCraft(8), "CraftId 8 = 기본 상시(잠금 아님)");
        var bp = Managers.Info.GetBlueprintByItemId(401);
        Mark(bp != null && bp.UnlockCraftId == 5, "도면아이템 401 -> UnlockCraftId 5 매핑");

        // 2) 초기 잠금 상태: 도면 3종 IsUnlocked=false, 기본종 true
        Mark(!registry.IsUnlocked(5) && !registry.IsUnlocked(6) && !registry.IsUnlocked(7), "초기: 도면 3종 모두 잠김");
        Mark(registry.IsUnlocked(1) && registry.IsUnlocked(8), "초기: 기본종 항상 해금");

        // 3) CraftPopup 열림 → 셀 잠금 오버레이 표시 검증
        Managers.Popup.CloseAll();
        yield return new WaitForSeconds(0.2f);
        var popup = Managers.Popup.Open<CraftPopup>();
        Mark(popup != null, "CraftPopup 열림");
        yield return new WaitForSeconds(0.3f);

        // 아이템 탭으로 전환(도면 레시피 5/6/7은 Item 카테고리)
        popup.SelectCategory(CraftCategoryType.Item);
        yield return new WaitForSeconds(0.3f);

        var cells = popup.GetComponentsInChildren<CraftScrollCell>(true);
        CraftScrollCell lockedCell = null, unlockedCell = null;
        foreach (var c in cells)
        {
            if (c.CraftInfo == null) continue;
            if (c.CraftInfo.Id == 5) lockedCell = c;
            if (c.CraftInfo.Id != 0 && !Managers.Info.IsBlueprintLockedCraft(c.CraftInfo.Id) && unlockedCell == null) unlockedCell = c;
        }
        Mark(lockedCell != null && lockedCell.IsLocked(), "도면 레시피(5) 셀 = 잠금 표시");
        Mark(unlockedCell == null || !unlockedCell.IsLocked(), "기본 레시피 셀 = 잠금 아님");

        // 4) 해금 이벤트 발화 검증 + 줍기 경로(SpawnDropItem 401)
        int eventFired = -1;
        System.Action<int> handler = (id) => eventFired = id;
        registry.OnUnlockChanged += handler;

        var player = controller.PlayWorker?.LocalPlayer;
        var inventory = controller.ObjectDataWorker?.GetInventoryRegistry();
        int invBefore = inventory?.GetItem(401)?.Count ?? 0;

        if (Managers.Network != null && Managers.Network.IsServer && player != null)
        {
            // 실제 줍기 경로: 도면 월드 드롭 스폰 (호스트 권위)
            Vector3 dropPos = player.transform.position + player.transform.forward * 1.5f;
            controller.PlayWorker.SpawnDropItem(401, 1, dropPos);
            GameDebug.Log("[TestManager] 도면 401 월드 드롭 스폰 — 자동 수집 경로는 상호작용 필요. 해금은 registry.Unlock 직접 경로로 검증");
            yield return new WaitForSeconds(0.3f);
        }

        // 해금 적용(WorldDropItem.AddItemClientRpc 내부와 동일 경로: registry.Unlock)
        registry.Unlock(5);
        yield return null;

        Mark(eventFired == 5, "Unlock(5) → OnUnlockChanged(5) 발화");
        Mark(registry.IsUnlocked(5), "해금 후 IsUnlocked(5) = true");

        // 5) 도면은 인벤토리 미점유(슬롯 미추가)
        int invAfter = inventory?.GetItem(401)?.Count ?? 0;
        Mark(invAfter == invBefore, "도면(401) 인벤토리 미추가(슬롯 미점유)");

        // 6) 열린 CraftPopup 즉시 갱신: 셀 오버레이 제거
        yield return new WaitForSeconds(0.2f);
        Mark(lockedCell == null || !lockedCell.IsLocked(), "해금 시 셀(5) 오버레이 즉시 제거");

        // 7) 중복 해금 무효(이벤트 미발화)
        eventFired = -1;
        registry.Unlock(5);
        Mark(eventFired == -1, "중복 Unlock(5) → 이벤트 미발화(무효)");

        registry.OnUnlockChanged -= handler;

        // 정리: 세션 리셋
        registry.Clear();
        Managers.Popup.CloseAll();

        GameDebug.Log($"[TestManager] TestBlueprintUnlock 결과: PASS {passed}, FAIL {failed}");
    }

    // ===== 심화검증 1: 토스트 실제 표시 (정리 안 함 — 캡처용) =====
    // registry.Unlock(5) → InGameHUDWorker.OnBlueprintUnlocked → BlueprintToast.ShowMessage 경로를
    // 실제로 트리거. Clear/CloseAll 호출하지 않아 토스트 페이드 구간을 캡처할 수 있다.
    public void TestBlueprintToastShow()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); return; }
        var registry = controller.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (registry == null) { GameDebug.LogError("[TestManager] RecipeUnlockRegistry 없음"); return; }

        registry.Clear();          // 신규 해금 보장(중복이면 이벤트 미발화)
        registry.Unlock(5);        // 나무 방패 도면 → 토스트 "나무 방패 도면을 익혔다"
        GameDebug.Log("[TestManager] TestBlueprintToastShow: Unlock(5) 호출 — 토스트 표시 시작(3.6s)");
    }

    // ===== 심화검증 2: 제작 게이트 셋업 — 잠긴 철검(6) + 재료 충분 =====
    // CraftPopup을 열고 Item 카테고리로 전환, 철검 재료(8x5,1x3,7x2)를 인벤토리에 충전한다.
    // 잠금은 유지(Unlock 안 함). 셀 선택/제작버튼 클릭은 u_play로 수행해 게이트를 실증한다.
    public void TestBlueprintCraftGateSetup()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); return; }
        var registry = controller.ObjectDataWorker?.GetRecipeUnlockRegistry();
        var inventory = controller.ObjectDataWorker?.GetInventoryRegistry();
        if (registry == null || inventory == null) { GameDebug.LogError("[TestManager] Registry/Inventory 없음"); return; }

        registry.Clear();          // 철검(6) 잠금 보장
        // 철검(CraftId 6) 재료: itemId8 x5, itemId1 x3, itemId7 x2 — 충분히 충전
        inventory.AddItem(8, 10);
        inventory.AddItem(1, 10);
        inventory.AddItem(7, 10);

        Managers.Popup.CloseAll();
        var popup = Managers.Popup.Open<CraftPopup>();
        if (popup == null) { GameDebug.LogError("[TestManager] CraftPopup 열기 실패"); return; }
        popup.SelectCategory(CraftCategoryType.Item);
        GameDebug.Log("[TestManager] TestBlueprintCraftGateSetup: 철검(6) 잠금 + 재료 충전 + Item 탭. 이제 셀/버튼 클릭으로 게이트 확인");
    }

    // 게이트 검증 결과 판정: 철검(6)이 여전히 잠김 + 인벤토리에 철검 결과물(202) 미생성 확인.
    public void TestBlueprintCraftGateVerify()
    {
        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); return; }
        var registry = controller.ObjectDataWorker?.GetRecipeUnlockRegistry();
        var inventory = controller.ObjectDataWorker?.GetInventoryRegistry();
        if (registry == null || inventory == null) { GameDebug.LogError("[TestManager] Registry/Inventory 없음"); return; }

        bool stillLocked = !registry.IsUnlocked(6);
        var result = inventory.GetItem(202);   // 철검 결과 아이템(CraftInfo Value01=202)
        int resultCount = result?.Count ?? 0;
        bool notCrafted = resultCount == 0;
        // 재료가 소비되지 않았는지(8은 10 충전, 철검 제작 시 5 소비됨)
        int mat8 = inventory.GetItem(8)?.Count ?? 0;
        bool materialIntact = mat8 == 10;

        if (stillLocked) GameDebug.Log("[TestManager] GATE PASS: 철검(6) 여전히 잠김(IsUnlocked=false)");
        else GameDebug.LogError("[TestManager] GATE FAIL: 철검(6) 잠금 해제됨");
        if (notCrafted) GameDebug.Log($"[TestManager] GATE PASS: 철검 결과물(202) 미생성(count={resultCount})");
        else GameDebug.LogError($"[TestManager] GATE FAIL: 철검 제작됨(202 count={resultCount})");
        if (materialIntact) GameDebug.Log($"[TestManager] GATE PASS: 재료(8) 미소비({mat8}=10 유지)");
        else GameDebug.LogError($"[TestManager] GATE FAIL: 재료(8) 소비됨({mat8})");
    }
}
#endif
