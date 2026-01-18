using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 전용 로직
/// 입력 처리(클라 입력 → 서버 요청), 카메라 타겟
/// </summary>
public class PlayerCharacter : CharacterBase
{
    // Network Variables
    private readonly NetworkVariable<int> m_PlayerIndex = new();

    // Properties
    public int GetPlayerIndex() => m_PlayerIndex.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            SetupLocalPlayer();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsOwner)
        {
            CleanupLocalPlayer();
        }
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

    private void SetupLocalPlayer()
    {
        // InGameController에 로컬 플레이어 등록
        if (InGameController.Instance != null)
        {
            InGameController.Instance.RegisterLocalPlayer(this);

            // 카메라 타겟 설정
            if (InGameController.Instance.CameraWorker != null)
            {
                InGameController.Instance.CameraWorker.SetTarget(transform);
            }
        }

        Debug.Log($"Local Player Setup: PlayerIndex {m_PlayerIndex.Value}");
    }

    private void CleanupLocalPlayer()
    {
        // 카메라 타겟 해제
        if (InGameController.Instance != null && InGameController.Instance.CameraWorker != null)
        {
            InGameController.Instance.CameraWorker.SetTarget(null);
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

        // 마우스 좌클릭 공격
        if (Input.GetMouseButtonDown(0))
        {
            Attack();
        }
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
}
