using System.Collections.Generic;

// 난이도 공식 D = TP / CAP (출시_9시간 §8 / 6지역 밸런스 §1).
// 순수 계산 모듈 — 런타임 어디서나 현재 값으로 호출 가능. 입력 조립(현재 지역/날씨/장비)은 호출부 책임.
// 계수는 DifficultyParams(기본값=레벨명세 초기 제안값, R5 튜닝).
public static class DifficultyFormula
{
    // ── 공식 계수 (튜닝 대상, R5). 기본값 = 6지역 밸런스 §1 ──
    public struct DifficultyParams
    {
        public float BaseEnemy;   // 적 위협 기준
        public float Ke;          // 지역당 위협 증가율
        public float Ks;          // 희소성 압력 계수
        public float W1;          // ToolTier 가중
        public float W2;          // GearScore 가중
        public float W3;          // TechUnlocked 가중
        public float W4;          // StockpileBuffer 가중

        public static DifficultyParams Default => new DifficultyParams
        {
            BaseEnemy = 1.0f,
            Ke = 0.40f,
            Ks = 1.0f,
            W1 = 1.0f,
            W2 = 1.0f,
            W3 = 0.8f,
            W4 = 0.6f,
        };
    }

    // ── 공식 입력 (호출부가 현재 상태로 채움) ──
    public struct DifficultyInputs
    {
        public int Depth;             // d 지역 깊이 0~5
        public float ResourceDensity; // 지역 자원 밀도 (0~1, 작을수록 희소)
        public float ColdDrain;       // 환경 압력 통화 (체온 소모 강도, 기준 1.0)
        public float PhaseMul;        // 시간대 배수 (낮 1.0 / 밤 1.3)
        public float WeatherMul;      // 날씨 위협 배수 (WeatherInfo.ThreatMul)
        public float ToolTier;        // ① 도구 등급 (T3)
        public float GearScore;       // 장비 점수 (장비 ATK/DEF 보정 합)
        public float TechUnlocked;    // ② 도면 해금 진척
        public float StockpileBuffer; // 자원 비축 여유
    }

    // ── 계산 결과 (구성요소 포함 — QA/디버그 가독) ──
    public struct DifficultyResult
    {
        public float EnemyScale;
        public float ScarcityPressure;
        public float EnvPressure;
        public float TP;
        public float CAP;
        public float D;
    }

    // TP = EnemyScale(d) + EnvPressure(t) + ScarcityPressure(d)
    // CAP = w1·ToolTier + w2·GearScore + w3·TechUnlocked + w4·StockpileBuffer
    public static DifficultyResult Compute(in DifficultyInputs _in, in DifficultyParams _p)
    {
        float enemyScale = _p.BaseEnemy * (1f + _p.Ke * _in.Depth);
        float density = _in.ResourceDensity <= 0f ? 1f : _in.ResourceDensity; // 0/음수 방어
        float scarcity = _p.Ks / density;
        float env = _in.ColdDrain * _in.PhaseMul * _in.WeatherMul;

        float tp = enemyScale + env + scarcity;
        float cap = _p.W1 * _in.ToolTier
                  + _p.W2 * _in.GearScore
                  + _p.W3 * _in.TechUnlocked
                  + _p.W4 * _in.StockpileBuffer;

        float d = cap <= 0f ? float.PositiveInfinity : tp / cap; // CAP 0이면 무한대(역량 전무)

        return new DifficultyResult
        {
            EnemyScale = enemyScale,
            ScarcityPressure = scarcity,
            EnvPressure = env,
            TP = tp,
            CAP = cap,
            D = d,
        };
    }

    public static DifficultyResult Compute(in DifficultyInputs _in)
        => Compute(_in, DifficultyParams.Default);

    // ── 어댑터 헬퍼 ──

    // 시간대 배수 (6지역 밸런스 §1: 낮 1.0 / 밤 1.3). Dusk/Dawn 은 전이 구간이라 중간값.
    public static float PhaseMul(DayPhase _phase)
    {
        switch (_phase)
        {
            case DayPhase.Day: return 1.0f;
            case DayPhase.Night: return 1.3f;
            case DayPhase.Dusk: return 1.15f;
            case DayPhase.Dawn: return 1.15f;
            default: return 1.0f;
        }
    }

    // 날씨 위협 배수 — T1 WeatherInfo CSV(ThreatMul)에서 조회. 미로드/미정의 시 1.0.
    public static float WeatherMul(WeatherType _weather)
    {
        List<WeatherInfo> list = InfoManager.Instance?.WeatherInfoList;
        if (list == null) return 1.0f;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].WeatherType == _weather)
                return list[i].ThreatMul;
        }
        return 1.0f;
    }
}
