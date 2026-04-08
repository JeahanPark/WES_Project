using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftScrollCell : BaseScrollCell<CraftInfo>
{
    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private Image m_IconImage;

    private CraftInfo m_CraftInfo;

    protected override void OnUpdateCell(int _index, CraftInfo _data)
    {
        m_CraftInfo = _data;

        if (m_CraftInfo == null)
        {
            SetEmpty();
            return;
        }

        if (m_NameText != null)
            m_NameText.text = m_CraftInfo.Name;

        if (m_IconImage != null)
        {
            string iconKey = m_CraftInfo.IconKey;
            m_IconImage.sprite = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }
    }

    private void SetEmpty()
    {
        if (m_NameText != null)
            m_NameText.text = string.Empty;

        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }
    }

    public void OnClickCell()
    {
        if (m_CraftInfo == null)
            return;

        var scroll = GetComponentInParent<CraftScroll>(true);
        scroll?.NotifyCellClicked(m_CraftInfo);
    }
}
