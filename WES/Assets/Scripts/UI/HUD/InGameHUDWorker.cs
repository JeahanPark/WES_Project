using UnityEngine;
using UniRx;

/// <summary>
/// 인게임 HUD를 관리하는 Worker
/// </summary>
public class InGameHUDWorker : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private PlayerStatusHUD m_PlayerStatusHUD;
    [SerializeField] private CraftHUDTab m_CraftHUDTab;
    [SerializeField] private QuickSlotHUD m_QuickSlotHUD;
    [SerializeField] private PhaseIconHUD m_PhaseIconHUD;
    [SerializeField] private BlueprintToast m_BlueprintToast;

    private PlayerCharacter m_LocalPlayer;
    private System.IDisposable m_QuickSlotSubscription;
    private RecipeUnlockRegistry m_UnlockRegistry;

    private void OnDisable()
    {
        UnsubscribeEvents();
        UnsubscribeUnlockEvents();
    }

    public void SetLocalPlayer(PlayerCharacter _player)
    {
        UnsubscribeEvents();

        m_LocalPlayer = _player;

        SubscribeEvents();
        RefreshHUD();
        InitializeQuickSlot();
        InitializeBlueprintToast();

        GameDebug.Log($"[InGameHUDWorker] Local player set: PlayerIndex {_player.GetPlayerIndex()}");
    }

    public void ClearLocalPlayer()
    {
        UnsubscribeEvents();
        UnsubscribeUnlockEvents();
        m_LocalPlayer = null;
        m_QuickSlotSubscription?.Dispose();

        GameDebug.Log("[InGameHUDWorker] Local player cleared");
    }

    private void InitializeBlueprintToast()
    {
        UnsubscribeUnlockEvents();

        m_UnlockRegistry = InGameController.Instance?.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (m_UnlockRegistry != null)
            m_UnlockRegistry.OnUnlockChanged += OnBlueprintUnlocked;
    }

    private void UnsubscribeUnlockEvents()
    {
        if (m_UnlockRegistry != null)
            m_UnlockRegistry.OnUnlockChanged -= OnBlueprintUnlocked;
        m_UnlockRegistry = null;
    }

    private void OnBlueprintUnlocked(int _craftId)
    {
        if (m_BlueprintToast == null)
            return;

        var craftInfo = Managers.Info.CraftInfoList.Find(_info => _info.Id == _craftId);
        string craftName = craftInfo != null ? craftInfo.Name : $"Craft({_craftId})";
        m_BlueprintToast.ShowMessage($"{craftName} 도면을 익혔다");
    }

    private void InitializeQuickSlot()
    {
        if (m_QuickSlotHUD == null)
            return;

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        var quickSlotRegistry = objectData.GetQuickSlotRegistry();
        var inventoryRegistry = objectData.GetInventoryRegistry();

        m_QuickSlotHUD.Initialize(quickSlotRegistry, inventoryRegistry);

        // 퀵슬롯 키 입력 구독
        m_QuickSlotSubscription?.Dispose();
        m_QuickSlotSubscription = Managers.Input.OnQuickSlotAsObservable
            .Subscribe(_slotIndex =>
            {
                quickSlotRegistry.UseSlot(_slotIndex, inventoryRegistry);
                m_QuickSlotHUD.RefreshSlot(_slotIndex);
            });
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
