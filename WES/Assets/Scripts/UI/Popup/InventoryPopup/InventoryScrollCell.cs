using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryScrollCell : BaseScrollCell<ItemData>
{
    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private Image m_IconImage;

    private ItemData m_ItemData;

    protected override void OnUpdateCell(int _index, ItemData _data)
    {
        m_ItemData = _data;

        if (m_ItemData == null || m_ItemData.Info == null)
        {
            SetEmpty();
            return;
        }

        if (m_NameText != null)
        {
            m_NameText.text = m_ItemData.Info.Name;
        }

        if (m_CountText != null)
        {
            m_CountText.text = m_ItemData.Count.ToString();
        }
    }

    private void SetEmpty()
    {
        if (m_NameText != null)
        {
            m_NameText.text = string.Empty;
        }

        if (m_CountText != null)
        {
            m_CountText.text = string.Empty;
        }
    }

    public void OnClickCell()
    {
        if (m_ItemData == null)
            return;

        var scroll = GetComponentInParent<InventoryScroll>(true);
        scroll?.NotifyCellClicked(m_ItemData);
    }
}
