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
    [SerializeField] private GameObject m_LockOverlay;

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
        RefreshLockState();
    }

    public void SetSelected(bool _selected)
    {
        if (m_SelectedFrame != null)
            m_SelectedFrame.SetActive(_selected);
        if (m_BackgroundImage != null)
            m_BackgroundImage.color = _selected ? SELECTED_COLOR : NORMAL_COLOR;
    }

    // 도면 해금 상태에 따라 잠금 오버레이를 갱신한다. 목록 재배치는 하지 않는다.
    public void RefreshLockState()
    {
        SetLocked(IsLocked());
    }

    public void SetLocked(bool _locked)
    {
        if (m_LockOverlay != null)
            m_LockOverlay.SetActive(_locked);
    }

    // 현재 셀의 레시피가 도면 잠금 상태인지 판정.
    public bool IsLocked()
    {
        if (m_CraftInfo == null)
            return false;

        if (!Managers.Info.IsBlueprintLockedCraft(m_CraftInfo.Id))
            return false;

        var registry = InGameController.Instance?.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (registry == null)
            return true;

        return !registry.IsUnlocked(m_CraftInfo.Id);
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
        SetLocked(false);
    }

    private void OnClickCell()
    {
        if (m_CraftInfo == null)
            return;

        var scroll = GetComponentInParent<CraftScroll>(true);
        scroll?.NotifyCellClicked(m_CraftInfo, this);
    }
}
