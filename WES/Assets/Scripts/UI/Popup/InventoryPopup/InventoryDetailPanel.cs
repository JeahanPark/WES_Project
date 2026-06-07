using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDetailPanel : MonoBehaviour
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private TextMeshProUGUI m_DescriptionText;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private Button m_InstallButton;

    private ItemData m_CurrentItemData;

    public void Show(ItemData _itemData)
    {
        gameObject.SetActive(true);
        m_CurrentItemData = _itemData;

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

        if (m_InstallButton != null)
            m_InstallButton.gameObject.SetActive(_itemData.Info.IsBuilding);
    }

    public void OnClickInstall()
    {
        if (m_CurrentItemData == null || !m_CurrentItemData.Info.IsBuilding)
            return;

        InGameController.Instance?.BuildingPlacementWorker?.StartPlacement(m_CurrentItemData.Info.Id);

        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.Close();
    }

    // 셀 미선택 상태: 배경판(이 GameObject의 Image)은 유지하고 콘텐츠만 비운다.
    // 과거엔 SetActive(false)로 패널 전체를 꺼서 본문 배경까지 사라져 게임월드가 비쳤다(C-1).
    // 인벤 패널엔 별도 안내 텍스트가 없어 이름 슬롯을 안내문구로 재활용한다.
    public void ShowEmpty(string _hint)
    {
        gameObject.SetActive(true);
        m_CurrentItemData = null;
        Clear();

        if (m_NameText != null)
            m_NameText.text = _hint;

        if (m_IconImage != null)
            m_IconImage.enabled = false;

        if (m_InstallButton != null)
            m_InstallButton.gameObject.SetActive(false);
    }

    public void Hide()
    {
        // 배경판을 끄지 않는다. 콘텐츠만 비우는 ShowEmpty로 위임(C-1 수정).
        ShowEmpty(string.Empty);
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
