using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area 기반 몬스터 스폰/리스폰을 관리하는 Worker.
/// 실제 스폰/디스폰은 InGameSpawnWorker에 위임한다.
/// </summary>
public class InGameAreaWorker : MonoBehaviour
{
    [SerializeField] private MonsterSpawnArea[] m_SpawnAreas;

    private Dictionary<int, int> m_AreaAliveCount = new();

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
            TrySpawnForArea(area);
        }

        GameDebug.Log($"[InGameAreaWorker] Initialized with {m_SpawnAreas.Length} spawn areas.");
    }

    public void OnMonsterDied(MonsterBase _monster, int _areaId)
    {
        if (!Managers.Network.IsServer)
            return;

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

    private void TrySpawnForArea(MonsterSpawnArea _area)
    {
        var areaInfo = Managers.Info.WorldAreaInfoList.Find(x => x.Id == _area.AreaId);
        if (areaInfo == null)
        {
            GameDebug.LogWarning($"[InGameAreaWorker] WorldAreaInfo not found: AreaId={_area.AreaId}");
            return;
        }

        if (m_AreaAliveCount[_area.AreaId] >= areaInfo.MaxCount)
            return;

        var areaMonsters = Managers.Info.WorldAreaMonsterInfoList.FindAll(x => x.AreaId == _area.AreaId);
        if (areaMonsters.Count == 0)
        {
            GameDebug.LogWarning($"[InGameAreaWorker] No monsters defined for AreaId={_area.AreaId}");
            return;
        }

        int monsterId = areaMonsters[Random.Range(0, areaMonsters.Count)].MonsterId;
        Vector3 spawnPos = _area.GetRandomSpawnPosition();
        SpawnMonster(monsterId, spawnPos, _area.AreaId);
    }

    private void SpawnMonster(int _monsterId, Vector3 _position, int _areaId)
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

        monster.SetSpawnAreaId(_areaId);

        if (!m_AreaAliveCount.ContainsKey(_areaId))
            m_AreaAliveCount[_areaId] = 0;

        m_AreaAliveCount[_areaId]++;

        GameDebug.Log($"[InGameAreaWorker] Spawned monster {_monsterId} at {_position} (Area={_areaId})");
    }

    private IEnumerator CoRespawnInArea(int _areaId, float _delay)
    {
        yield return new WaitForSeconds(_delay);

        var area = System.Array.Find(m_SpawnAreas, x => x.AreaId == _areaId);
        if (area == null)
            yield break;

        TrySpawnForArea(area);
    }
}
