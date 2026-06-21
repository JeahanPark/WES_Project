using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 이동 비용 (이동비용 기획, ④분기). 서버 전용.
// 이동 = 체온 비용 지불 — 이동 중 Cold(추위)가 가속 누적된다(신규 스탯 없이 기존 Cold 재사용).
// 이동 감지는 NavMeshAgent가 오너에만 활성이라 서버에서 직접 못 보므로, NetworkTransform이 동기한
// 위치 델타로 판정(전 플레이어 서버측 가능). 지역 배수(WorldAreaInfo.MoveCostMultiplier, T1) ×
// 날씨 배수(WeatherInfo.MoveCostMul, T1) × 야간 배수로 가중. DayNightWorker와 동일 GameObject에 부착.
public class MoveCostWorker : NetworkBehaviour
{
    [SerializeField] private float m_BaseColdCostPerSec = 2f;   // 이동 중 기준 Cold 누적/초 (R5 튜닝)
    [SerializeField] private float m_MoveSpeedThreshold = 0.5f; // 이동 판정 속도(units/sec)
    [SerializeField] private float m_NightMul = 1.5f;           // 야간 이동 가중
    [SerializeField] private float m_DuskDawnMul = 1.2f;        // 황혼/새벽 가중
    [SerializeField] private int m_AreaId = 1;                  // 분포/배수 출처 지역. R2 진입 지역으로 갱신.

    private WeatherWorker m_WeatherWorker;

    // 플레이어별 직전 위치 / Cold 누적 소수부 (PlayerIndex 키)
    private readonly Dictionary<int, Vector3> m_LastPos = new();
    private readonly Dictionary<int, float> m_ColdAccum = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_WeatherWorker = GetComponent<WeatherWorker>();
    }

    public void SetArea(int _areaId)
    {
        if (IsSpawned && !IsServer)
            return;
        m_AreaId = _areaId;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;
        if (InGameController.Instance == null || InGameController.Instance.GameState != GameState.Playing)
            return;

        float delta = Time.deltaTime;
        if (delta <= 0f)
            return;

        var registry = InGameController.Instance.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return;

        float areaMul = GetAreaMoveCostMul(m_AreaId);
        float weatherMul = GetWeatherMoveCostMul();
        float phaseMul = GetPhaseMul();

        List<PlayerCharacter> players = registry.GetAlivePlayers();
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCharacter player = players[i];
            if (player == null)
                continue;

            int key = player.GetPlayerIndex();
            Vector3 pos = player.transform.position;

            if (!m_LastPos.TryGetValue(key, out Vector3 last))
            {
                m_LastPos[key] = pos;
                continue;
            }
            m_LastPos[key] = pos;

            // 수평 이동 속도 (y 무시 — 경사/낙하 제외)
            Vector3 horiz = pos - last;
            horiz.y = 0f;
            float speed = horiz.magnitude / delta;
            if (speed < m_MoveSpeedThreshold)
                continue; // 정지 시 비용 없음(유지)

            float cost = m_BaseColdCostPerSec * areaMul * weatherMul * phaseMul * delta;
            float acc = (m_ColdAccum.TryGetValue(key, out float prev) ? prev : 0f) + cost;

            int whole = (int)acc;
            if (whole > 0)
            {
                player.SetCold(player.Cold + whole);
                acc -= whole;
            }
            m_ColdAccum[key] = acc;
        }
    }

    private float GetAreaMoveCostMul(int _areaId)
    {
        var all = InfoManager.Instance?.WorldAreaInfoList;
        if (all == null)
            return 1f;
        for (int i = 0; i < all.Count; i++)
            if (all[i].Id == _areaId)
                return all[i].MoveCostMultiplier <= 0f ? 1f : all[i].MoveCostMultiplier;
        return 1f;
    }

    private float GetWeatherMoveCostMul()
    {
        if (m_WeatherWorker == null)
            return 1f;
        WeatherType weather = m_WeatherWorker.CurrentWeather;
        var all = InfoManager.Instance?.WeatherInfoList;
        if (all == null)
            return 1f;
        for (int i = 0; i < all.Count; i++)
            if (all[i].WeatherType == weather)
                return all[i].MoveCostMul <= 0f ? 1f : all[i].MoveCostMul;
        return 1f;
    }

    private float GetPhaseMul()
    {
        DayNightWorker dn = InGameController.Instance?.DayNightWorker;
        if (dn == null)
            return 1f;
        switch (dn.CurrentPhase)
        {
            case DayPhase.Night: return m_NightMul;
            case DayPhase.Dusk: return m_DuskDawnMul;
            case DayPhase.Dawn: return m_DuskDawnMul;
            default: return 1f;
        }
    }
}
