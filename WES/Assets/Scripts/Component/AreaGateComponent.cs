using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// R2 6지역 공간 관문 펌프(일방향) — 출시_6지역_공간 코드명세 §3.2. 서버 권위.
// [확정] 펌프 = 좌표게이트. 통과 후 역행 차단. 전원통과까지 관문 유지(뒤처진 인원 통과 허용 후 봉쇄).
// 슬라이스1: 판정 로직(통과 추적 + 역행 차단 판정 + 봉쇄벽 토글)까지. 실제 씬 배치/지오메트리는 슬라이스2(level-design).
// 봉쇄는 보이지 않는 BoxCollider 벽(m_BlockWall) on/off — 서버 위치 푸시백보다 클라 이동예측 떨림이 적음(§11 권고).
[RequireComponent(typeof(NetworkObject))]
public class AreaGateComponent : NetworkBehaviour
{
    [SerializeField] private float m_GateAxis;                 // 관문 종단축(Z) 좌표.
    [SerializeField] private bool m_ForwardIsPositive = true;  // 통과 방향(+Z 전진).
    [SerializeField] private bool m_OpenUntilAllPassed = true; // 전원 통과까지 봉쇄벽 비활성.
    [SerializeField] private GameObject m_BlockWall;           // 역행 봉쇄용 보이지 않는 콜라이더 벽(슬라이스2 배치).

    private readonly HashSet<int> m_PassedPlayers = new(); // PlayerIndex 키.
    private bool m_Sealed;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
            SetWall(false); // 시작 시 개방
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;
        if (InGameController.Instance == null || InGameController.Instance.GameState != GameState.Playing)
            return;

        EvaluateGate();
    }

    private void EvaluateGate()
    {
        var registry = InGameController.Instance?.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return;

        List<PlayerCharacter> players = registry.GetAlivePlayers();
        if (players.Count == 0)
            return;

        int passedCount = 0;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCharacter player = players[i];
            if (player == null)
                continue;

            int key = player.GetPlayerIndex();
            bool isPast = IsPastGate(player.transform.position.z);

            if (isPast)
            {
                m_PassedPlayers.Add(key);
                passedCount++;
            }
        }

        // 전원통과(살아있는 인원 기준) 판정 → 봉쇄.
        if (m_OpenUntilAllPassed)
        {
            if (!m_Sealed && passedCount >= players.Count)
                Seal();
        }
        else
        {
            // 누구든 통과하면 즉시 봉쇄(단순 모드).
            if (!m_Sealed && passedCount > 0)
                Seal();
        }
    }

    private bool IsPastGate(float _axisPos)
    {
        return m_ForwardIsPositive ? _axisPos > m_GateAxis : _axisPos < m_GateAxis;
    }

    private void Seal()
    {
        m_Sealed = true;
        SetWall(true);
        GameDebug.Log($"[AreaGateComponent] Gate sealed at axis={m_GateAxis} (passed {m_PassedPlayers.Count}).");
    }

    private void SetWall(bool _active)
    {
        if (m_BlockWall != null)
            m_BlockWall.SetActive(_active);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = m_Sealed ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.25f);
        Vector3 c = transform.position;
        c.z = m_GateAxis;
        Gizmos.DrawCube(c, new Vector3(20f, 6f, 0.5f));
    }
#endif
}
