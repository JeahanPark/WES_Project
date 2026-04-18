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
