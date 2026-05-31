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
    public int SpawnAreaId => m_SpawnAreaId;
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
            m_StateMachine.SetCollisionEnabled(true);
            m_StateMachine.ChangeState(MonsterStateType.Idle);
        }

        SetHitColor(false);
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

    protected override void OnDamaged(int _damage, CharacterBase _attacker, bool _isCritical)
    {
        base.OnDamaged(_damage, _attacker, _isCritical);

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
            ExecuteMonsterDrop(transform.position);
            InGameController.Instance.AreaWorker.OnMonsterDied(this, m_SpawnAreaId);
        }

        if (m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Death);
        }
    }

    private void ExecuteMonsterDrop(Vector3 _position)
    {
        if (m_MonsterInfo == null || m_MonsterInfo.DropTableId == 0)
            return;

        var dropItems = Managers.Info.DropTableItemInfoList.FindAll(x => x.DropTableId == m_MonsterInfo.DropTableId);
        foreach (var dropItem in dropItems)
        {
            if (dropItem.RewardType != RewardType.Item)
                continue;

            int count = UnityEngine.Random.Range(dropItem.Min, dropItem.Max + 1);
            if (count <= 0)
                continue;

            Vector3 spawnPos = _position + new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));

            InGameController.Instance.PlayWorker.SpawnDropItem(dropItem.RewardId, count, spawnPos);
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
