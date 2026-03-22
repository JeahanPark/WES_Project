using UnityEngine;

/// <summary>
/// 몬스터 기본 클래스
/// </summary>
public abstract class MonsterBase : CharacterBase
{
    [SerializeField] private int m_MonsterId;
    [SerializeField] private StateAnimationComponent m_StateAnimationComponent;
    [SerializeField] private MonsterStateMachine m_StateMachine;
    [SerializeField] private Renderer m_Renderer;

    private MonsterInfo m_MonsterInfo;
    private Color m_OriginalColor;
    private int m_SpawnAreaId;

    public int MonsterId => m_MonsterId;
    public StateAnimationComponent StateAnimationComponent => m_StateAnimationComponent;

    protected virtual void Awake()
    {
        if (m_Renderer != null)
        {
            m_OriginalColor = m_Renderer.material.color;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            LoadMonsterInfo();
        }

        if (m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Idle);
        }
    }

    public void SetSpawnAreaId(int _areaId)
    {
        m_SpawnAreaId = _areaId;
    }

    public void SetHitColor(bool _isHit)
    {
        if (m_Renderer == null)
            return;

        m_Renderer.material.color = _isHit ? Color.red : m_OriginalColor;
    }

    protected override void OnDamaged(int _damage, CharacterBase _attacker)
    {
        base.OnDamaged(_damage, _attacker);

        if (!IsDead && m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Hit);
        }
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (IsServer)
        {
            ExecuteDrop(transform.position);
            InGameController.Instance.AreaWorker.OnMonsterDied(this, m_SpawnAreaId);
        }

        if (m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Death);
        }
    }

    private void LoadMonsterInfo()
    {
        if (m_MonsterId == 0)
            return;

        m_MonsterInfo = Managers.Info.MonsterInfoList.Find(x => x.Id == m_MonsterId);
        if (m_MonsterInfo == null)
        {
            GameDebug.LogWarning($"[MonsterBase] MonsterInfo not found: Id={m_MonsterId}");
            return;
        }

        SetMaxHP(m_MonsterInfo.MaxHP);
        SetHP(m_MonsterInfo.MaxHP);
    }
}
