using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 플레이어가 제작 후 월드에 설치하는 오브젝트 베이스 클래스
/// </summary>
public class WorldBuildingObject : WorldBaseObject
{
    private const float EFFECT_INTERVAL = 1f;
    private const float CAMPFIRE_RANGE = 5f;
    private const int CAMPFIRE_COLD_RECOVERY = 2;

    private int m_BuildingInfoId;
    private float m_EffectTimer;

    private void Update()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        if (m_BuildingInfoId != 1) // 모닥불만 효과 적용
            return;

        m_EffectTimer += Time.deltaTime;
        if (m_EffectTimer < EFFECT_INTERVAL)
            return;

        m_EffectTimer = 0f;
        ApplyCampfireEffect();
    }

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
