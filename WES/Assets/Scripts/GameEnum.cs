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
    Hit,
    Death
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
