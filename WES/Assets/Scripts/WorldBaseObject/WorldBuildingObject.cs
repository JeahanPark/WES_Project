using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class WorldBuildingObject : WorldBaseObject
{
    private const float EFFECT_INTERVAL = 1f;
    private const float CAMPFIRE_RANGE = 5f;
    private const int CAMPFIRE_COLD_RECOVERY = 2;

    private static readonly List<WorldBuildingObject> s_ActiveBuildings = new();

    private int m_BuildingInfoId;
    private float m_EffectTimer;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        s_ActiveBuildings.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        s_ActiveBuildings.Remove(this);
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        if (m_BuildingInfoId != 1)
            return;

        m_EffectTimer += Time.deltaTime;
        if (m_EffectTimer < EFFECT_INTERVAL)
            return;

        m_EffectTimer = 0f;
        ApplyCampfireEffect();
    }

    public int BuildingInfoId => m_BuildingInfoId;

    public static IReadOnlyList<WorldBuildingObject> ActiveBuildings => s_ActiveBuildings;

    public void SetBuildingInfoId(int _buildingInfoId)
    {
        m_BuildingInfoId = _buildingInfoId;
    }

    private void ApplyCampfireEffect()
    {
        var colliders = Physics.OverlapSphere(transform.position, CAMPFIRE_RANGE);
        foreach (var col in colliders)
        {
            var player = col.GetComponent<PlayerCharacter>();
            if (player != null)
            {
                player.SetCold(player.Cold + CAMPFIRE_COLD_RECOVERY);
            }
        }
    }
}
