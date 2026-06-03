using System.Collections.Generic;
using Unity.Netcode;

public class WorldBuildingObject : WorldBaseObject
{
    private static readonly List<WorldBuildingObject> s_ActiveBuildings = new();

    private NetworkVariable<bool> m_IsLit = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int m_BuildingInfoId;

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

    public int BuildingInfoId => m_BuildingInfoId;
    public bool IsLit => m_IsLit.Value;

    public static IReadOnlyList<WorldBuildingObject> ActiveBuildings => s_ActiveBuildings;

    public void SetBuildingInfoId(int _buildingInfoId)
    {
        m_BuildingInfoId = _buildingInfoId;
    }

    public void SetLit(bool _lit)
    {
        if (!IsServer)
            return;

        m_IsLit.Value = _lit;
    }
}
