using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 날씨 시스템 (날씨_시스템 기획). 서버 권위 NetworkVariable로 전원 동기(§9 협동 동기화).
// 낮밤 페이즈 전환마다 1틱(§5.3 시간대 동기) — 지역 분포(WorldAreaWeatherInfo, T1)에서 목표를 샘플링하고
// 심각도 사다리에서 현재 날씨를 목표 방향으로 '한 단계만' 이동(§5.3 급격한 점프 금지 = 전조 보장).
// 빗나감(후회)의 원천 = 목표 샘플링의 확률성. 시각 전조/오버레이는 designer·sound 영역(백로그).
public class WeatherWorker : NetworkBehaviour
{
    // 심각도 사다리 (단계적 악화 순서). 지역 분포에 존재하는 날씨만 골라 부분 사다리를 구성한다.
    private static readonly WeatherType[] s_SeverityLadder =
    {
        WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain, WeatherType.Fog, WeatherType.Snowstorm,
    };

    [SerializeField] private int m_AreaId = 1; // 분포 출처 지역. R2 6지역에서 진입 지역으로 갱신(SetArea).

    public static event Action<WeatherType, WeatherType> OnWeatherChanged;

    // R3-C: 현재 활성 날씨의 정적 스냅샷(서버·클라 공통, OnWeatherChanged로 갱신).
    // 몬스터 행동(은신 안개 노출·날씨강화)이 매 프레임 Find 없이 참조하기 위한 전역 접근.
    private static WeatherType s_CurrentWeather = WeatherType.Clear;
    public static WeatherType GlobalWeather => s_CurrentWeather;

    private NetworkVariable<int> m_Weather = new(
        (int)WeatherType.Clear,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public WeatherType CurrentWeather => (WeatherType)m_Weather.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_Weather.OnValueChanged += OnWeatherValueChanged;
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
        s_CurrentWeather = CurrentWeather; // 스폰 시점 스냅샷 동기.
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_Weather.OnValueChanged -= OnWeatherValueChanged;
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _cur)
    {
        if (!IsServer)
            return;
        StepWeather();
    }

    // 지역 분포에서 목표 샘플 → 심각도 사다리에서 현재→목표로 한 단계 이동.
    public void StepWeather()
    {
        if (!IsSpawned || !IsServer)
            return;

        List<WeatherType> ladder = BuildAreaLadder(m_AreaId); // 분포에 있는 날씨만, 심각도 순
        if (ladder.Count == 0)
            return;

        WeatherType cur = CurrentWeather;
        int curIdx = ladder.IndexOf(cur);
        if (curIdx < 0)
            curIdx = 0; // 현재 날씨가 지역 분포 밖이면 가장 약한 쪽으로 스냅

        WeatherType target = SampleAreaWeather(m_AreaId, ladder);
        int targetIdx = ladder.IndexOf(target);

        int nextIdx = curIdx;
        if (targetIdx > curIdx)
            nextIdx = curIdx + 1;      // 한 단계 악화
        else if (targetIdx < curIdx)
            nextIdx = curIdx - 1;      // 한 단계 완화
        // 같으면 유지(전조 지속)

        WeatherType next = ladder[nextIdx];
        if ((int)next != m_Weather.Value)
            m_Weather.Value = (int)next;
    }

    // R2 지역 진입 시 서버가 호출 — 분포 출처 갱신.
    public void SetArea(int _areaId)
    {
        if (IsSpawned && !IsServer)
            return;
        m_AreaId = _areaId;
    }

    private List<WeatherType> BuildAreaLadder(int _areaId)
    {
        List<WorldAreaWeatherInfo> rows = GetAreaRows(_areaId);
        HashSet<WeatherType> set = new HashSet<WeatherType>();
        for (int i = 0; i < rows.Count; i++)
            set.Add(rows[i].WeatherType);

        List<WeatherType> ladder = new List<WeatherType>();
        for (int i = 0; i < s_SeverityLadder.Length; i++)
            if (set.Contains(s_SeverityLadder[i]))
                ladder.Add(s_SeverityLadder[i]);
        return ladder;
    }

    private WeatherType SampleAreaWeather(int _areaId, List<WeatherType> _ladder)
    {
        List<WorldAreaWeatherInfo> rows = GetAreaRows(_areaId);
        float total = 0f;
        for (int i = 0; i < rows.Count; i++)
            total += rows[i].Chance;

        if (total <= 0f)
            return _ladder[0];

        float roll = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            acc += rows[i].Chance;
            if (roll <= acc)
                return rows[i].WeatherType;
        }
        return rows[rows.Count - 1].WeatherType;
    }

    private List<WorldAreaWeatherInfo> GetAreaRows(int _areaId)
    {
        List<WorldAreaWeatherInfo> result = new List<WorldAreaWeatherInfo>();
        List<WorldAreaWeatherInfo> all = InfoManager.Instance?.WorldAreaWeatherInfoList;
        if (all == null)
            return result;

        for (int i = 0; i < all.Count; i++)
            if (all[i].AreaId == _areaId)
                result.Add(all[i]);
        return result;
    }

    private void OnWeatherValueChanged(int _prev, int _cur)
    {
        WeatherType prev = (WeatherType)_prev;
        WeatherType cur = (WeatherType)_cur;
        s_CurrentWeather = cur; // 전역 스냅샷 갱신(몬스터 행동 참조용).
        GameDebug.Log($"[WeatherWorker] Weather changed: {prev} -> {cur} (area {m_AreaId})");
        OnWeatherChanged?.Invoke(prev, cur);
    }
}
