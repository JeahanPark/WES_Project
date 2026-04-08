using UnityEngine;

/// <summary>
/// 인게임 HUD를 관리하는 Worker
/// - 플레이어 상태 HUD
/// </summary>
public class InGameHUDWorker : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private PlayerStatusHUD m_PlayerStatusHUD;
    [SerializeField] private CraftHUDTab m_CraftHUDTab;

    private PlayerCharacter m_LocalPlayer;

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 로컬 플레이어 설정 및 HUD 초기화
    /// </summary>
    public void SetLocalPlayer(PlayerCharacter _player)
    {
        UnsubscribeEvents();

        m_LocalPlayer = _player;

        SubscribeEvents();
        RefreshHUD();

        GameDebug.Log($"[InGameHUDWorker] Local player set: PlayerIndex {_player.GetPlayerIndex()}");
    }

    /// <summary>
    /// 로컬 플레이어 해제 (PlayerCharacter 파괴 시 호출)
    /// </summary>
    public void ClearLocalPlayer()
    {
        UnsubscribeEvents();
        m_LocalPlayer = null;

        GameDebug.Log("[InGameHUDWorker] Local player cleared");
    }

    private void SubscribeEvents()
    {
        if (m_LocalPlayer == null)
            return;

        m_LocalPlayer.SubscribeOnHPChanged(OnHPChanged);
        m_LocalPlayer.SubscribeOnColdChanged(OnColdChanged);
    }

    private void UnsubscribeEvents()
    {
        if (m_LocalPlayer == null)
            return;

        m_LocalPlayer.UnsubscribeOnHPChanged(OnHPChanged);
        m_LocalPlayer.UnsubscribeOnColdChanged(OnColdChanged);
    }

    private void RefreshHUD()
    {
        if (m_LocalPlayer == null || m_PlayerStatusHUD == null)
            return;

        m_PlayerStatusHUD.UpdateStat(CharacterStat.HP, m_LocalPlayer.HP, m_LocalPlayer.MaxHP);
        m_PlayerStatusHUD.UpdateStat(CharacterStat.Cold, m_LocalPlayer.Cold, m_LocalPlayer.MaxCold);
    }

    private void OnHPChanged(int _hp, int _maxHP)
    {
        if (m_PlayerStatusHUD != null)
        {
            m_PlayerStatusHUD.UpdateStat(CharacterStat.HP, _hp, _maxHP);
        }
    }

    private void OnColdChanged(int _cold, int _maxCold)
    {
        if (m_PlayerStatusHUD != null)
        {
            m_PlayerStatusHUD.UpdateStat(CharacterStat.Cold, _cold, _maxCold);
        }
    }
}
