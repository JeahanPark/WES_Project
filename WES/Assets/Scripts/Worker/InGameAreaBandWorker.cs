using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// R2 6지역 공간 (출시_6지역_공간 코드명세). 서버 권위.
// 선두 플레이어(살아있는 플레이어 중 종단축 진행 깊이 최대)의 좌표 → WorldAreaInfo.AxisMin/AxisMax(CSV 단일 진실원)로
// 현재 지역 d 판정 → 변경 시 WeatherWorker.SetArea(d)·MoveCostWorker.SetArea(d) 호출 → 전원이 그 지역 환경 공유.
// [확정] 전역 1분포(개별 아님). m_CurrentAreaId 단조증가(전진만) — 선두 사망 승계로 선두가 얕아져도 후퇴 안 함.
// [확정] 경계 깜빡임 방지 = 종단축 데드존 히스테리시스(경계를 m_BandHysteresis 이상 넘어야 전환).
// DayNightWorker GameObject(WeatherWorker/MoveCostWorker와 동일 오브젝트, SetArea 타깃)에 부착.
public class InGameAreaBandWorker : NetworkBehaviour
{
    [SerializeField] private float m_BandHysteresis = 1.5f;   // 경계 데드존 폭(units). 연타 방지.
    [SerializeField] private float m_AreaCheckInterval = 0f;  // 판정 주기(초). 0=매 프레임.
    [SerializeField] private int m_StartAreaId = 1;           // 게임 시작 시 강제 초기 지역(해안가).

    public static event Action<int, int> OnAreaChanged; // (prev, cur) — UI/사운드 구독점.

    private NetworkVariable<int> m_CurrentAreaId = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private WeatherWorker m_WeatherWorker;
    private MoveCostWorker m_MoveCostWorker;
    private bool m_Initialized;
    private float m_CheckAccum;

    public int CurrentAreaId => m_CurrentAreaId.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_WeatherWorker = GetComponent<WeatherWorker>();
        m_MoveCostWorker = GetComponent<MoveCostWorker>();
        m_CurrentAreaId.OnValueChanged += OnAreaValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_CurrentAreaId.OnValueChanged -= OnAreaValueChanged;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || !m_Initialized)
            return;
        if (InGameController.Instance == null || InGameController.Instance.GameState != GameState.Playing)
            return;

        if (m_AreaCheckInterval > 0f)
        {
            m_CheckAccum += Time.deltaTime;
            if (m_CheckAccum < m_AreaCheckInterval)
                return;
            m_CheckAccum = 0f;
        }

        EvaluateArea();
    }

    // 서버가 게임 시작 시 호출 — 초기 지역 강제 + SetArea 1회 동기.
    public void Initialize()
    {
        if (!IsServer)
            return;

        m_CurrentAreaId.Value = m_StartAreaId;
        ApplyAreaToWorkers(m_StartAreaId);
        m_Initialized = true;
        GameDebug.Log($"[InGameAreaBandWorker] Initialized. StartArea={m_StartAreaId}");
    }

    private void EvaluateArea()
    {
        float? leadAxis = GetLeadAxisPosition();
        if (!leadAxis.HasValue)
            return; // 살아있는 플레이어 0 → no-op

        int resolved = ResolveAreaId(leadAxis.Value);
        if (resolved <= 0)
            return;

        // 단조증가(전진만): 현재보다 깊은 지역으로만 전진.
        if (resolved <= m_CurrentAreaId.Value)
            return;

        ApplyAreaChange(resolved);
    }

    // 살아있는 플레이어 중 종단축(Z) 좌표 최대값 = 선두.
    private float? GetLeadAxisPosition()
    {
        var registry = InGameController.Instance?.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return null;

        List<PlayerCharacter> players = registry.GetAlivePlayers();
        if (players.Count == 0)
            return null;

        float maxAxis = float.NegativeInfinity;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCharacter player = players[i];
            if (player == null)
                continue;
            float axis = player.transform.position.z; // 종단축 = Z (MapGenerator Z밴드 구조)
            if (axis > maxAxis)
                maxAxis = axis;
        }

        if (float.IsNegativeInfinity(maxAxis))
            return null;
        return maxAxis;
    }

    // WorldAreaInfo.AxisMin/AxisMax(CSV 단일 진실원)로 좌표→지역 매핑.
    // 히스테리시스: 현재 지역의 상한을 m_BandHysteresis만큼 넘어야 다음 지역으로 전환.
    private int ResolveAreaId(float _axisPos)
    {
        var all = InfoManager.Instance?.WorldAreaInfoList;
        if (all == null || all.Count == 0)
            return 0;

        int current = m_CurrentAreaId.Value;

        // 1) 데드존: 현재 지역 범위(+상단 히스테리시스) 안이면 현재 유지.
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Id != current)
                continue;
            if (_axisPos < all[i].AxisMax + m_BandHysteresis)
                return current; // 아직 경계+데드존을 못 넘음 → 유지
            break;
        }

        // 2) 경계를 넘었으면 좌표가 실제로 속한 지역을 반환.
        for (int i = 0; i < all.Count; i++)
        {
            if (_axisPos >= all[i].AxisMin && _axisPos < all[i].AxisMax)
                return all[i].Id;
        }

        // 3) 마지막 지역 상한 초과(종단 끝/마을 직전) → 가장 깊은 지역으로 clamp.
        int deepestId = 0;
        float deepestMax = float.NegativeInfinity;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].AxisMax > deepestMax)
            {
                deepestMax = all[i].AxisMax;
                deepestId = all[i].Id;
            }
        }
        return deepestId;
    }

    private void ApplyAreaChange(int _newAreaId)
    {
        m_CurrentAreaId.Value = _newAreaId; // OnValueChanged → OnAreaChanged 이벤트
        ApplyAreaToWorkers(_newAreaId);
    }

    private void ApplyAreaToWorkers(int _areaId)
    {
        if (m_WeatherWorker != null)
            m_WeatherWorker.SetArea(_areaId);
        if (m_MoveCostWorker != null)
            m_MoveCostWorker.SetArea(_areaId);
    }

    private void OnAreaValueChanged(int _prev, int _cur)
    {
        GameDebug.Log($"[InGameAreaBandWorker] Area changed: {_prev} -> {_cur}");
        OnAreaChanged?.Invoke(_prev, _cur);

        // R4 ③ 지역 진입 stinger(모든 클라 로컬, 음원0=무음). 초기화(prev==cur) 시엔 생략.
        if (_prev != _cur)
            Managers.Audio?.PlayStinger(AudioKey.STINGER_AREA_ENTER);
    }
}
