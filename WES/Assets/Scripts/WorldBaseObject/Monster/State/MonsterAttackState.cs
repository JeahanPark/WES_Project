using UnityEngine;

/// <summary>
/// 몬스터 공격 상태.
/// 사거리 유지 시 쿨다운마다 TakeDamage + Attack 애니. 사거리 이탈 → Chase, 타깃 null → Idle.
/// Charge: 진입 시 1회 가속 직선 돌진(예고 후 충돌/시간만료까지) 후 일반 공격.
/// </summary>
public class MonsterAttackState : MonsterStateBase
{
    // Charge 돌진 골격값(director 확인 중 — 상수). 속도/피해 배수, 예고·돌진 지속.
    private const float CHARGE_SPEED_MULTIPLIER = 2.5f;
    private const float CHARGE_DAMAGE_MULTIPLIER = 2.5f;
    private const float CHARGE_TELEGRAPH_TIME = 1f;   // 돌진 예고(정지) 시간
    private const float CHARGE_DASH_TIME = 0.6f;      // 직선 돌진 지속

    private float m_CooldownTimer;
    private bool m_IsCharger;

    // Charge 단계: 0=예고, 1=돌진, 2=완료(일반 공격)
    private int m_ChargePhase;
    private float m_ChargeTimer;
    private Vector3 m_ChargeDirection;
    private bool m_ChargeHit;

    public override void Enter()
    {
        var owner = m_StateMachine.Owner;
        m_CooldownTimer = 0f;
        m_IsCharger = owner.BehaviorType == MonsterBehaviorType.Charge;
        m_ChargePhase = 0;
        m_ChargeTimer = 0f;
        m_ChargeHit = false;

        if (m_IsCharger)
        {
            var target = owner.Perception != null ? owner.Perception.CurrentTarget : null;
            if (target != null)
            {
                Vector3 dir = target.transform.position - owner.transform.position;
                dir.y = 0f;
                m_ChargeDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : owner.transform.forward;
            }
            else
            {
                m_ChargeDirection = owner.transform.forward;
            }
        }
    }

    public override void Update()
    {
        var owner = m_StateMachine.Owner;
        var perception = owner.Perception;

        if (perception == null || !perception.HasTarget)
        {
            m_StateMachine.ChangeState(MonsterStateType.Idle);
            return;
        }

        if (m_IsCharger && m_ChargePhase < 2)
        {
            UpdateCharge(owner, perception.CurrentTarget);
            return;
        }

        var target = perception.CurrentTarget;
        float distance = m_StateMachine.DistanceTo(target.transform.position);

        // 사거리 이탈 → 다시 추격.
        if (distance > owner.AttackRange)
        {
            m_StateMachine.ChangeState(MonsterStateType.Chase);
            return;
        }

        m_CooldownTimer -= Time.deltaTime;
        if (m_CooldownTimer <= 0f)
        {
            PerformAttack(owner, target, owner.GetATK());
            m_CooldownTimer = owner.AttackCooldown;
        }
    }

    public override void Exit()
    {
    }

    private void UpdateCharge(MonsterBase _owner, PlayerCharacter _target)
    {
        m_ChargeTimer += Time.deltaTime;

        if (m_ChargePhase == 0)
        {
            // 예고: 제자리 정지(애니 재사용 — Attack 모션으로 예고 표현).
            _owner.StateAnimationComponent.PlayAnimation(AnimationType.Attack);
            if (m_ChargeTimer >= CHARGE_TELEGRAPH_TIME)
            {
                m_ChargePhase = 1;
                m_ChargeTimer = 0f;
            }
            return;
        }

        // 돌진: 고정 방향 직선 가속 이동.
        float dashSpeed = _owner.ConfiguredMoveSpeed * CHARGE_SPEED_MULTIPLIER;
        m_StateMachine.MoveStraight(m_ChargeDirection, dashSpeed);

        // 돌진 중 타깃 접촉 → 1회 가중 피해.
        if (!m_ChargeHit && _target != null && !_target.IsDead)
        {
            if (m_StateMachine.DistanceTo(_target.transform.position) <= _owner.AttackRange)
            {
                int chargeDamage = Mathf.RoundToInt(_owner.GetATK() * CHARGE_DAMAGE_MULTIPLIER);
                PerformAttack(_owner, _target, chargeDamage);
                m_ChargeHit = true;
            }
        }

        if (m_ChargeTimer >= CHARGE_DASH_TIME)
        {
            m_ChargePhase = 2;
            m_CooldownTimer = _owner.AttackCooldown;
        }
    }

    private void PerformAttack(MonsterBase _owner, PlayerCharacter _target, int _damage)
    {
        if (_target == null || _target.IsDead)
            return;

        _owner.StateAnimationComponent.PlayAnimation(AnimationType.Attack);
        _target.TakeDamage(_damage, _owner);
    }
}
