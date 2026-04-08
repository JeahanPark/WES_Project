using UnityEngine;
using UnityEngine.UI;

public class CraftPopup : BasePopup
{
    [SerializeField] private Button m_BuildingTabButton;
    [SerializeField] private Button m_ItemTabButton;
    [SerializeField] private CraftScroll m_CraftScroll;
    [SerializeField] private CraftDetailPanel m_DetailPanel;

    private CraftCategoryType m_CurrentCategory = CraftCategoryType.Building;

    private void Start()
    {
        m_CraftScroll.SetCellClickCallback(OnCellClicked);
        SelectCategory(CraftCategoryType.Building);
    }

    public void OnClickClose()
    {
        Close();
    }

    public void OnClickBuildingTab()
    {
        SelectCategory(CraftCategoryType.Building);
    }

    public void OnClickItemTab()
    {
        SelectCategory(CraftCategoryType.Item);
    }

    public void SelectCategory(CraftCategoryType _category)
    {
        m_CurrentCategory = _category;

        var list = Managers.Info.GetCraftInfosByCategory(_category);
        m_CraftScroll.SetData(list);
        m_DetailPanel.Hide();
    }

    private void OnCellClicked(CraftInfo _craftInfo)
    {
        m_DetailPanel.Show(_craftInfo);
    }
}
