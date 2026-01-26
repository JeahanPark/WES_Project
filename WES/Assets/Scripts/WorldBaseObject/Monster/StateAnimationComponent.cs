using UnityEngine;

/// <summary>
/// 상태 패턴으로 제어되는 엔티티의 애니메이션 컴포넌트
/// CrossFade 방식으로 상태 전환
/// </summary>
public class StateAnimationComponent : GameAnimationComponent
{
    private const float DEFAULT_CROSS_FADE_DURATION = 0.1f;

    public void PlayAnimation(AnimationType _type)
    {
        PlayAnimation(_type, DEFAULT_CROSS_FADE_DURATION);
    }

    public bool IsPlayingAnimation(AnimationType _type, int _layer = 0)
    {
        if (Animator == null)
            return false;

        string stateName = _type.ToString();

        if (Animator.IsInTransition(_layer))
        {
            var next = Animator.GetNextAnimatorStateInfo(_layer);
            if (next.IsName(stateName))
                return true;
        }

        var cur = Animator.GetCurrentAnimatorStateInfo(_layer);
        return cur.IsName(stateName);
    }

    private void PlayAnimation(AnimationType _type, float _crossFadeDuration)
    {
        if (Animator == null)
            return;

        Animator.CrossFade(_type.ToString(), _crossFadeDuration);
    }
}
