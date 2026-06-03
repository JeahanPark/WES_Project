using Unity.Netcode;

public class InGameObjectDataWorker : NetworkBehaviour
{
    private CharacterRegistry m_CharacterRegistry = new();
    private InventoryRegistry m_InventoryRegistry = new();
    private QuickSlotRegistry m_QuickSlotRegistry = new();
    private RecipeUnlockRegistry m_RecipeUnlockRegistry = new();

    public CharacterRegistry GetCharacterRegistry()
    {
        return m_CharacterRegistry;
    }

    public InventoryRegistry GetInventoryRegistry()
    {
        return m_InventoryRegistry;
    }

    public QuickSlotRegistry GetQuickSlotRegistry()
    {
        return m_QuickSlotRegistry;
    }

    public RecipeUnlockRegistry GetRecipeUnlockRegistry()
    {
        return m_RecipeUnlockRegistry;
    }

    /// <summary>
    /// 세션 리셋 일원화 지점. 인벤토리와 도면 해금을 함께 초기화한다.
    /// 사망/세션 종료/재시작 등 "다음 판은 기본 5종부터" 리셋은 반드시 이 메서드를 호출해
    /// 해금 리셋 누락(영구 진행 안티골 위반)을 방지한다.
    /// </summary>
    public void ResetSessionData()
    {
        m_InventoryRegistry.Clear();
        m_RecipeUnlockRegistry.Clear();
    }
}
