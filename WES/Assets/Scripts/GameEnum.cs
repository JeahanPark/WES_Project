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

// 몬스터 행동 분기 (R3-B: None/Pack/Charge만 코드 처리, 나머지는 R3-C 분기 추가용 선언만).
public enum MonsterBehaviorType
{
    None,        // 평화 — Chase/Attack 미사용(기존 배회). DetectRange=0과 함께 비파괴 디폴트.
    Pack,        // 무리 — Chase 진입 시 같은 SpawnAreaId 몬스터에 타깃 전파
    Charge,      // 돌진 — Attack 진입 시 1회 가속 직선 돌진(예고)
    Poison,      // R3-C
    Ranged,      // R3-C
    Stealth,     // R3-C
    WeatherBuff, // R3-C
    Boss,        // R3-C
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
