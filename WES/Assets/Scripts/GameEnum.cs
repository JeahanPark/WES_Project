using UnityEngine;

public class GameEnum
{

}

public enum CharacterStat
{
    HP,
    HPRegen,
    ATK,
    DEF,
    MoveSpeed,
    Cold
}

public enum AnimationType
{
    Idle,
    Walk,
    Attack,
    Interact,
    Hit,
    Death
}

public enum MonsterStateType
{
    Idle,
    Walk,
    Chase,
    Attack,
    Hit,
    Death
}

// 몬스터 행동 분기 (R3-B: None/Pack/Charge, R3-C: Poison/Ranged/Stealth/WeatherBuff/Boss 구현 완료).
public enum MonsterBehaviorType
{
    None,        // 평화 — Chase/Attack 미사용(기존 배회). DetectRange=0과 함께 비파괴 디폴트.
    Pack,        // 무리 — Chase 진입 시 같은 SpawnAreaId 몬스터에 타깃 전파
    Charge,      // 돌진 — Attack 진입 시 1회 가속 직선 돌진(예고)
    Poison,      // 독 — 근접 공격 적중 시 대상에 DoT(지속 피해) 부여
    Ranged,      // 원거리 — AttackRange 밖에서 히트스캔 즉시판정(투사체 프리팹은 백로그)
    Stealth,     // 은신 — 평시 반투명(NetworkVariable 가시성). 안개·근접·추격 시 노출
    WeatherBuff, // 날씨강화 — 특정 날씨에서 ATK/MoveSpeed 강화(WeatherWorker 연동)
    Boss,        // 보스 — HP 66%/33% 페이즈 전환(가속·공격 강화)
}

public enum WorldObjectType
{
    Monster,
    WorldObject,
    NPC,
}

public enum RewardType
{
    Item,
    Currency,
}

public enum CraftCategoryType
{
    Building,
    Item,
}

public enum CraftConditionType
{
    None,
    MaxCold,
    MinCold,
}

public enum GameState
{
    Playing,
    Clear,
    GameOver,
}

public enum DayPhase
{
    Day,
    Dusk,
    Night,
    Dawn,
}

public enum ColdStage
{
    None,       // Cold 0~29
    Warning,    // Cold 30~59
    WeakDot,    // Cold 60~89
    StrongDot,  // Cold 90~100
}

// 날씨 5종 (날씨_시스템 기획 §5.1). 불완전 정보 + 4분기 결정 트리거.
public enum WeatherType
{
    Clear,      // 맑음 — 기준
    Cloudy,     // 흐림 — 악천후 전조
    Rain,       // 비 — 시야↓, 모닥불 효율↓
    Fog,        // 안개 — 시야 극↓, 몬스터 은폐
    Snowstorm,  // 눈보라 — 체온 극대 압박, 이동비용↑
}
