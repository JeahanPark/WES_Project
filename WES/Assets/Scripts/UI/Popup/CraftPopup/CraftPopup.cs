using UnityEngine;
using UnityEngine.UI;

public class CraftPopup : BasePopup
{
    [SerializeField] private Button m_BuildingTabButton;
    [SerializeField] private Button m_ItemTabButton;
    [SerializeField] private Button m_CloseButton;
    [SerializeField] private CraftScroll m_CraftScroll;
    [SerializeField] private CraftDetailPanel m_DetailPanel;

    private CraftCategoryType m_CurrentCategory = CraftCategoryType.Building;

    private void Awake()
    {
        m_CloseButton.onClick.AddListener(OnClickClose);
        m_BuildingTabButton.onClick.AddListener(OnClickBuildingTab);
        m_ItemTabButton.onClick.AddListener(OnClickItemTab);
    }

    private void Start()
    {
        m_CraftScroll.SetCellClickCallback(OnCellClicked);
        SelectCategory(CraftCategoryType.Building);
    }

    public void SelectCategory(CraftCategoryType _category)
    {
        m_CurrentCategory = _category;

        var list = Managers.Info.GetCraftInfosByCategory(_category);
        m_CraftScroll.SetData(list);
        m_DetailPanel.Hide();
    }

    private void OnClickClose()
    {
        Close();
    }

    private void OnClickBuildingTab()
    {
        SelectCategory(CraftCategoryType.Building);
    }

    private void OnClickItemTab()
    {
        SelectCategory(CraftCategoryType.Item);
    }

    private void OnCellClicked(CraftInfo _craftInfo)
    {
        m_DetailPanel.Show(_craftInfo);
    }
}
