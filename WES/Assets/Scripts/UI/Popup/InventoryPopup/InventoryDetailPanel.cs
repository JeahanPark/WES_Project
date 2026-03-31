using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDetailPanel : MonoBehaviour
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private TextMeshProUGUI m_DescriptionText;
    [SerializeField] private TextMeshProUGUI m_CountText;

    public void Show(ItemData _itemData)
    {
        gameObject.SetActive(true);

        if (_itemData == null || _itemData.Info == null)
        {
            Clear();
            return;
        }

        if (m_NameText != null)
            m_NameText.text = _itemData.Info.Name;

        if (m_CountText != null)
            m_CountText.text = $"x{_itemData.Count}";

        if (m_DescriptionText != null)
            m_DescriptionText.text = _itemData.Info.Description;

        if (m_IconImage != null)
        {
            string iconKey = _itemData.Info.IconKey;
            m_IconImage.sprite = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Clear()
    {
        if (m_NameText != null)
            m_NameText.text = string.Empty;

        if (m_DescriptionText != null)
            m_DescriptionText.text = string.Empty;

        if (m_CountText != null)
            m_CountText.text = string.Empty;

        if (m_IconImage != null)
            m_IconImage.sprite = null;
    }
}
