using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area 기반 몬스터 스폰/리스폰을 관리하는 Worker.
/// 실제 스폰/디스폰은 InGameSpawnWorker에 위임한다.
/// </summary>
public class InGameAreaWorker : MonoBehaviour
{
    private const string PHASE_ANY = "Any";
    private const string PHASE_NIGHT = "Night";
    private const string PHASE_DAY = "Day";

    [SerializeField] private MonsterSpawnArea[] m_SpawnAreas;

    private Dictionary<int, int> m_AreaAliveCount = new();
    private List<MonsterBase> m_NightMonsters = new();
    private List<MonsterBase> m_AliveMonsters = new();

    private void OnEnable()
    {
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
    }

    public void Initialize()
    {
        if (!Managers.Network.IsServer)
            return;

        foreach (var area in m_SpawnAreas)
        {
            if (!m_AreaAliveCount.ContainsKey(area.AreaId))
                m_AreaAliveCount[area.AreaId] = 0;
        }

        foreach (var area in m_SpawnAreas)
        {
            TrySpawnForArea(area, false);
        }

        GameDebug.Log($"[InGameAreaWorker] Initialized with {m_SpawnAreas.Length} spawn areas.");
    }

    public void OnMonsterDied(MonsterBase _monster, int _areaId)
    {
        if (!Managers.Network.IsServer)
            return;

        m_NightMonsters.Remove(_monster);
        m_AliveMonsters.Remove(_monster);

        var monsterInfo = Managers.Info.MonsterInfoList.Find(x => x.Id == _monster.MonsterId);
        if (monsterInfo != null)
        {
            InGameController.Instance.SpawnWorker.DespawnObject(_monster.NetworkObject, monsterInfo.PrefabKey);
        }

        if (_areaId == 0)
            return;

        if (m_AreaAliveCount.ContainsKey(_areaId))
            m_AreaAliveCount[_areaId]--;

        var areaInfo = Managers.Info.WorldAreaInfoList.Find(x => x.Id == _areaId);
        if (areaInfo == null)
            return;

        StartCoroutine(CoRespawnInArea(_areaId, areaInfo.RespawnDelay));
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _current)
    {
        if (!Managers.Network.IsServer)
            return;

        if (_current == DayPhase.Night)
        {
            SpawnNightMonsters();
        }
        else if (_prev == DayPhase.Night && _current == DayPhase.Dawn)
        {
            DespawnNightMonsters();
        }
        else if (_current == DayPhase.Day)
        {
            DespawnNightMonsters();
        }
    }

    private void SpawnNightMonsters()
    {
        foreach (var area in m_SpawnAreas)
        {
            TrySpawnForArea(area, true);
        }
    }

    private void DespawnNightMonsters()
    {
        for (int i = m_NightMonsters.Count - 1; i >= 0; i--)
        {
            var monster = m_NightMonsters[i];
            if (monster == null || !monster.IsSpawned)
                continue;

            var monsterInfo = Managers.Info.MonsterInfoList.Find(x => x.Id == monster.MonsterId);
            if (monsterInfo != null)
            {
                InGameController.Instance.SpawnWorker.DespawnObject(monster.NetworkObject, monsterInfo.PrefabKey);
            }

            if (m_AreaAliveCount.ContainsKey(monster.SpawnAreaId))
                m_AreaAliveCount[monster.SpawnAreaId]--;

            m_AliveMonsters.Remove(monster);
        }
        m_NightMonsters.Clear();
    }

    /// <summary>
    /// Pack(무리) 어그로: 같은 SpawnAreaId의 살아있는 몬스터에게 동일 타깃을 전파한다(서버 권위 내부).
    /// 자기 자신과 평화 몬스터(Perception 없음/null)는 제외. 추가 네트워크 동기화 없음.
    /// </summary>
    public void PropagatePackTarget(MonsterBase _source, int _areaId, PlayerCharacter _target)
    {
        if (!Managers.Network.IsServer || _target == null || _target.IsDead)
            return;

        for (int i = m_AliveMonsters.Count - 1; i >= 0; i--)
        {
            var monster = m_AliveMonsters[i];
            if (monster == null || !monster.IsSpawned || monster.IsDead)
                continue;
            if (monster == _source || monster.SpawnAreaId != _areaId)
                continue;
            if (monster.BehaviorType != MonsterBehaviorType.Pack)
                continue;
            if (monster.Perception == null)
                continue;

            monster.Perception.SetForcedTarget(_target);
        }
    }

    private void TrySpawnForArea(MonsterSpawnArea _area, bool _spawnNightOnly)
    {
        var areaInfo = Managers.Info.WorldAreaInfoList.Find(x => x.Id == _area.AreaId);
        if (areaInfo == null)
        {
            GameDebug.LogWarning($"[InGameAreaWorker] WorldAreaInfo not found: AreaId={_area.AreaId}");
            return;
        }

        if (m_AreaAliveCount[_area.AreaId] >= areaInfo.MaxCount)
            return;

        var allAreaMonsters = Managers.Info.WorldAreaMonsterInfoList.FindAll(x => x.AreaId == _area.AreaId);
        if (allAreaMonsters.Count == 0)
        {
            GameDebug.LogWarning($"[InGameAreaWorker] No monsters defined for AreaId={_area.AreaId}");
            return;
        }

        List<WorldAreaMonsterInfo> candidates;
        if (_spawnNightOnly)
        {
            candidates = allAreaMonsters.FindAll(x => x.PhaseCondition == PHASE_NIGHT);
        }
        else
        {
            candidates = allAreaMonsters.FindAll(x =>
                x.PhaseCondition == PHASE_ANY ||
                x.PhaseCondition == PHASE_DAY ||
                string.IsNullOrEmpty(x.PhaseCondition));
        }

        if (candidates.Count == 0)
            return;

        int monsterId = candidates[Random.Range(0, candidates.Count)].MonsterId;
        Vector3 spawnPos = _area.GetRandomSpawnPosition();
        SpawnMonster(monsterId, spawnPos, _area.AreaId, _spawnNightOnly);
    }

    private void SpawnMonster(int _monsterId, Vector3 _position, int _areaId, bool _isNightMonster)
    {
        var monsterInfo = Managers.Info.MonsterInfoList.Find(x => x.Id == _monsterId);
        if (monsterInfo == null)
        {
            GameDebug.LogError($"[InGameAreaWorker] MonsterInfo not found: Id={_monsterId}");
            return;
        }

        var monster = InGameController.Instance.SpawnWorker.SpawnObject<MonsterBase>(monsterInfo.PrefabKey, _position);
        if (monster == null)
        {
            GameDebug.LogError($"[InGameAreaWorker] MonsterBase component not found on spawned object: {monsterInfo.PrefabKey}");
            return;
        }

        // 의도 MonsterId 주입 → MonsterInfo 재로딩(HP/행동/드롭 차별화). placeholder 프리팹은 id=1 직렬화.
        monster.SetMonsterId(_monsterId);
        monster.SetSpawnAreaId(_areaId);

        var spawnArea = System.Array.Find(m_SpawnAreas, x => x.AreaId == _areaId);
        if (spawnArea != null)
            monster.SetLeashBaseRadius(spawnArea.SpawnRadius);

        m_AliveMonsters.Add(monster);

        if (_isNightMonster)
            m_NightMonsters.Add(monster);

        if (!m_AreaAliveCount.ContainsKey(_areaId))
            m_AreaAliveCount[_areaId] = 0;

        m_AreaAliveCount[_areaId]++;

        GameDebug.Log($"[InGameAreaWorker] Spawned monster {_monsterId} at {_position} (Area={_areaId}, Night={_isNightMonster})");
    }

    private IEnumerator CoRespawnInArea(int _areaId, float _delay)
    {
        yield return new WaitForSeconds(_delay);

        var area = System.Array.Find(m_SpawnAreas, x => x.AreaId == _areaId);
        if (area == null)
            yield break;

        DayPhase currentPhase = InGameController.Instance?.DayNightWorker?.CurrentPhase ?? DayPhase.Day;
        bool isNight = currentPhase == DayPhase.Night;
        TrySpawnForArea(area, isNight);
    }
}
