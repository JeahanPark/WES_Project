using UniRx;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 전용 로직
/// 입력 처리(클라 입력 → 서버 요청), 카메라 타겟
/// </summary>
public class PlayerCharacter : CharacterBase
{
    public const int DEFAULT_MAX_COLD = 100;
    private const float DEFAULT_ATTACK_RANGE = 1.5f;
    private const int DEFAULT_MAX_HIT_COUNT = 3;
    private const float COLLECT_RADIUS = 2f;

    [SerializeField] private PlayerAnimationComponent m_AnimationComponent;
    [SerializeField] private float m_AttackRange = DEFAULT_ATTACK_RANGE;
    [SerializeField] private int m_MaxHitCount = DEFAULT_MAX_HIT_COUNT;
    [SerializeField] private LayerMask m_TargetLayer;
    [SerializeField] private LayerMask m_GroundLayerMask;

    // Network Variables
    private readonly NetworkVariable<int> m_PlayerIndex = new();
    private NetworkVariable<int> m_Cold = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> m_MaxCold = new(DEFAULT_MAX_COLD, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private event System.Action<int, int> m_OnColdChanged;

    // Properties
    public int GetPlayerIndex() => m_PlayerIndex.Value;
    public int Cold => m_Cold.Value;
    public int MaxCold => m_MaxCold.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        m_Cold.OnValueChanged += OnColdValueChanged;

        if (IsOwner)
        {
            SetupLocalPlayer();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        m_Cold.OnValueChanged -= OnColdValueChanged;

        if (IsOwner)
        {
            CleanupLocalPlayer();
        }
    }

    public void SubscribeOnColdChanged(System.Action<int, int> _callback)
    {
        m_OnColdChanged += _callback;
    }

    public void UnsubscribeOnColdChanged(System.Action<int, int> _callback)
    {
        m_OnColdChanged -= _callback;
    }

    public void RecalculateEquipmentStats()
    {
        int bonusATK = 0;
        int bonusDEF = 0;

        var controller = InGameController.Instance;
        if (controller == null) return;

        var inventory = controller.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory == null) return;

        var slots = inventory.GetSlots();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            if (slots[i] == null) continue;

            int itemId = slots[i].Info.Id;
            switch (itemId)
            {
                case 3:   // 검: ATK +5
                    bonusATK = System.Math.Max(bonusATK, 5);
                    break;
                case 201: // 나무 방패: DEF +5
                    bonusDEF = System.Math.Max(bonusDEF, 5);
                    break;
                case 202: // 철검: ATK +8
                    bonusATK = System.Math.Max(bonusATK, 8);
                    break;
                case 203: // 가죽 갑옷: DEF +3
                    bonusDEF = System.Math.Max(bonusDEF, 3);
                    break;
            }
        }

        SetATK(DEFAULT_ATK + bonusATK);
        SetDEF(DEFAULT_DEF + bonusDEF);
    }

    public void SetCold(int _value)
    {
        if (!IsServer) return;
        m_Cold.Value = System.Math.Clamp(_value, 0, m_MaxCold.Value);
    }

    /// <summary>
    /// 플레이어는 ClientNetworkTransform(오너 권위)으로 이동하므로
    /// NavMeshAgent는 오너 인스턴스에서만 활성화한다. 비오너 클론은 Agent를 끄고
    /// NetworkTransform이 위치를 구동하게 둔다.
    /// </summary>
    protected override bool ShouldEnableNavAgent()
    {
        return IsOwner;
    }

    public void SetMaxCold(int _value)
    {
        if (!IsServer) return;
        m_MaxCold.Value = _value;
    }

    /// <summary>
    /// 환경 데미지 (추위 등). 서버 전용. DEF/크리티컬 무시.
    /// _allowDeath == false면 HP가 1 미만으로 내려가지 않는다(추위로는 즉사하지 않음).
    /// </summary>
    public void TakeEnvironmentDamage(int _damage, bool _allowDeath)
    {
        ApplyEnvironmentDamageServer(_damage, _allowDeath);
    }

    /// <summary>
    /// Cold 증감 (양수: 증가, 음수: 감소)
    /// </summary>
    public void AddCold(int _amount)
    {
        AddColdServerRpc(_amount);
    }

    [Rpc(SendTo.Server)]
    private void AddColdServerRpc(int _amount)
    {
        SetCold(m_Cold.Value + _amount);
    }

    protected override void Update()
    {
        base.Update();

        if (!IsOwner || !IsSpawned)
            return;

        HandleInput();
    }

    public void SetPlayerIndex(int _index)
    {
        if (IsServer)
        {
            m_PlayerIndex.Value = _index;
        }
    }

    public override void Attack()
    {
        if (IsDead)
            return;

        if (TryCollectClickedDropItem())
            return;

        if (m_AnimationComponent == null)
            return;

        if (m_AnimationComponent.IsAttacking())
            return;

        m_AnimationComponent.PlayAttack();
    }

    public void OnAttackHit()
    {
        if (!IsOwner)
            return;

        if (InGameController.Instance == null || InGameController.Instance.ColliderWorker == null)
            return;

        Vector3 attackPosition = transform.position + transform.forward * (m_AttackRange * 0.5f);

        InGameController.Instance.ColliderWorker.CreateCollider(
            this,
            attackPosition,
            m_AttackRange,
            GetATK(),
            m_MaxHitCount,
            m_TargetLayer
        );
    }

    public override void Interact()
    {
        if (IsDead)
            return;

        if (TryCollectNearbyDropItem())
            return;

        if (m_AnimationComponent == null)
            return;

        if (m_AnimationComponent.IsInteracting())
            return;

        m_AnimationComponent.PlayInteract();
    }

    private bool m_HasReportedDeath = false;

    protected override void OnDeath()
    {
        base.OnDeath();

        if (!IsOwner)
            return;

        if (m_HasReportedDeath)
            return;
        m_HasReportedDeath = true;

        SwitchCameraToAliveTeammate();

        if (InGameController.Instance != null)
            InGameController.Instance.NotifyPlayerDiedServerRpc();
    }

    private void SwitchCameraToAliveTeammate()
    {
        var controller = InGameController.Instance;
        if (controller == null || controller.CameraWorker == null)
            return;

        if (controller.CameraWorker.GetTarget() != transform)
            return;

        var registry = controller.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return;

        foreach (var alive in registry.GetAlivePlayers())
        {
            if (alive == this)
                continue;
            controller.CameraWorker.SetTarget(alive.transform);
            return;
        }
    }

    private bool TryCollectClickedDropItem()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        WorldDropItem dropItem = hit.collider.GetComponentInParent<WorldDropItem>();
        if (dropItem == null)
            return false;

        if (Vector3.Distance(transform.position, dropItem.transform.position) > COLLECT_RADIUS)
            return false;

        dropItem.CollectServerRpc();
        return true;
    }

    private bool TryCollectNearbyDropItem()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, COLLECT_RADIUS);
        foreach (var hit in hits)
        {
            WorldDropItem dropItem = hit.GetComponent<WorldDropItem>();
            if (dropItem == null)
                continue;

            dropItem.CollectServerRpc();
            return true;
        }

        return false;
    }

    protected override void OnWalkChanged(bool _isWalking)
    {
        if (m_AnimationComponent == null)
            return;

        m_AnimationComponent.SetWalk(_isWalking);
    }

    private void SetupLocalPlayer()
    {
        if (InGameController.Instance != null)
        {
            // InGamePlayWorker에 로컬 플레이어 등록
            if (InGameController.Instance.PlayWorker != null)
            {
                InGameController.Instance.PlayWorker.RegisterLocalPlayer(this);
            }

            // InGameHUDWorker에 로컬 플레이어 등록
            if (InGameController.Instance.HUDWorker != null)
            {
                InGameController.Instance.HUDWorker.SetLocalPlayer(this);
            }

            // 카메라 타겟 설정
            if (InGameController.Instance.CameraWorker != null)
            {
                InGameController.Instance.CameraWorker.SetTarget(transform);
            }
        }

        SubscribeInputEvents();
        SubscribeInventoryEvents();

        GameDebug.Log($"Local Player Setup: PlayerIndex {m_PlayerIndex.Value}");
    }

    private void CleanupLocalPlayer()
    {
        if (InGameController.Instance != null)
        {
            // HUDWorker 구독 해제
            if (InGameController.Instance.HUDWorker != null)
            {
                InGameController.Instance.HUDWorker.ClearLocalPlayer();
            }

            // 카메라 타겟 해제
            if (InGameController.Instance.CameraWorker != null)
            {
                InGameController.Instance.CameraWorker.SetTarget(null);
            }
        }

        UnsubscribeInventoryEvents();

        GameDebug.Log($"Local Player Cleanup: PlayerIndex {m_PlayerIndex.Value}");
    }

    private void SubscribeInventoryEvents()
    {
        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory != null)
        {
            inventory.OnInventoryChanged += RecalculateEquipmentStats;
        }
    }

    private void UnsubscribeInventoryEvents()
    {
        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= RecalculateEquipmentStats;
        }
    }

    private void HandleInput()
    {
        if (Managers.Input == null)
            return;

        // WASD 이동 처리
        Vector2 moveInput = Managers.Input.MoveInput;
        MoveWithDirection(moveInput);

        // 마우스 방향으로 회전
        UpdateMouseLook();
    }

    private void SubscribeInputEvents()
    {
        if (Managers.Input == null)
            return;

        Managers.Input.OnAttackAsObservable.Subscribe(_ => Attack()).AddTo(this);
        Managers.Input.OnInteractAsObservable.Subscribe(_ => Interact()).AddTo(this);
        Managers.Input.OnInventoryAsObservable.Subscribe(_ => ToggleInventory()).AddTo(this);
    }

    private void ToggleInventory()
    {
        var existing = Managers.Popup.FindOpen<InventoryPopup>();
        if (existing != null)
            existing.Close();
        else
            Managers.Popup.Open<InventoryPopup>();
    }

    private void UpdateMouseLook()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        int mask = m_GroundLayerMask.value != 0 ? m_GroundLayerMask.value : (1 << LayerMask.NameToLayer("Ground"));
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, mask))
        {
            LookAtPosition(hit.point);
        }
    }

    private void OnColdValueChanged(int _prev, int _current)
    {
        m_OnColdChanged?.Invoke(_current, m_MaxCold.Value);
    }
}
