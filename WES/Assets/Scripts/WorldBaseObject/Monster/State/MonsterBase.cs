using UnityEngine;

/// <summary>
/// 몬스터 기본 클래스
/// </summary>
public abstract class MonsterBase : CharacterBase
{
    [SerializeField] private StateAnimationComponent m_StateAnimationComponent;

    public StateAnimationComponent StateAnimationComponent => m_StateAnimationComponent;
}
