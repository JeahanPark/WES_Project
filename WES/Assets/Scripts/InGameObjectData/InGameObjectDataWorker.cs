using Unity.Netcode;

public class InGameObjectDataWorker : NetworkBehaviour
{
    private CharacterRegistry m_CharacterRegistry = new();
    private InventoryRegistry m_InventoryRegistry = new();

    public CharacterRegistry GetCharacterRegistry()
    {
        return m_CharacterRegistry;
    }

    public InventoryRegistry GetInventoryRegistry()
    {
        return m_InventoryRegistry;
    }
}
