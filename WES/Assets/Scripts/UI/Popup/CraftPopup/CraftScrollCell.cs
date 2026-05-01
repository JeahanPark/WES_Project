using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftScrollCell : BaseScrollCell<CraftInfo>
{
    private static readonly Color SELECTED_COLOR = new Color(0.5f, 0.35f, 0.18f, 1f);
    private static readonly Color NORMAL_COLOR = new Color(0.18f, 0.16f, 0.14f, 1f);

    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private Image m_IconImage;
    [SerializeField] private Button m_Button;
    [SerializeField] private GameObject m_SelectedFrame;
    [SerializeField] private Image m_BackgroundImage;

    private CraftInfo m_CraftInfo;

    public CraftInfo CraftInfo => m_CraftInfo;

    private void Awake()
    {
        m_Button.onClick.AddListener(OnClickCell);
    }

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

        SetSelected(false);
    }

    public void SetSelected(bool _selected)
    {
        if (m_SelectedFrame != null)
            m_SelectedFrame.SetActive(_selected);
        if (m_BackgroundImage != null)
            m_BackgroundImage.color = _selected ? SELECTED_COLOR : NORMAL_COLOR;
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

        SetSelected(false);
    }

    private void OnClickCell()
    {
        if (m_CraftInfo == null)
            return;

        var scroll = GetComponentInParent<CraftScroll>(true);
        scroll?.NotifyCellClicked(m_CraftInfo, this);
    }
}
