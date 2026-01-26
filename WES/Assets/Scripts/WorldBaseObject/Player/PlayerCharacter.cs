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

    [SerializeField] private PlayerAnimationComponent m_AnimationComponent;

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

    public void SetCold(int _value)
    {
        if (!IsServer) return;
        m_Cold.Value = System.Math.Clamp(_value, 0, m_MaxCold.Value);
    }

    public void SetMaxCold(int _value)
    {
        if (!IsServer) return;
        m_MaxCold.Value = _value;
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
        if (m_AnimationComponent == null)
            return;

        if (m_AnimationComponent.IsAttacking())
            return;

        m_AnimationComponent.PlayAttack();
    }

    public override void Interact()
    {
        if (m_AnimationComponent == null)
            return;

        if (m_AnimationComponent.IsInteracting())
            return;

        m_AnimationComponent.PlayInteract();
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

        Debug.Log($"Local Player Setup: PlayerIndex {m_PlayerIndex.Value}");
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

        Debug.Log($"Local Player Cleanup: PlayerIndex {m_PlayerIndex.Value}");
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
    }

    private void UpdateMouseLook()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            LookAtPosition(hitPoint);
        }
    }

    private void OnColdValueChanged(int _prev, int _current)
    {
        m_OnColdChanged?.Invoke(_current, m_MaxCold.Value);
    }
}
