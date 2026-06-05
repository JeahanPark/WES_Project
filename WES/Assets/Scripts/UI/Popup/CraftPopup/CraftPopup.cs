using UnityEngine;
using UnityEngine.UI;

public class CraftPopup : BasePopup
{
    private static readonly Color TAB_ACTIVE_BG = new Color(0.45f, 0.32f, 0.18f, 1f);
    private static readonly Color TAB_INACTIVE_BG = new Color(0.18f, 0.16f, 0.14f, 1f);
    private static readonly Color TAB_ACTIVE_TEXT = new Color(1f, 0.92f, 0.65f, 1f);
    private static readonly Color TAB_INACTIVE_TEXT = new Color(0.65f, 0.6f, 0.5f, 1f);

    [SerializeField] private Button m_BuildingTabButton;
    [SerializeField] private Button m_ItemTabButton;
    [SerializeField] private Button m_CloseButton;
    [SerializeField] private CraftScroll m_CraftScroll;
    [SerializeField] private CraftDetailPanel m_DetailPanel;
    [SerializeField] private TMPro.TextMeshProUGUI m_HintText;
    [SerializeField] private RectTransform m_LeftPanel;

    private CraftCategoryType m_CurrentCategory = CraftCategoryType.Building;
    private CraftInfo m_SelectedCraftInfo;
    private RecipeUnlockRegistry m_UnlockRegistry;

    private void Awake()
    {
        m_CloseButton.onClick.AddListener(OnClickClose);
        m_BuildingTabButton.onClick.AddListener(OnClickBuildingTab);
        m_ItemTabButton.onClick.AddListener(OnClickItemTab);
        RebalanceLayout();
        AlignHintToDetailPanel();
    }

    private void OnEnable()
    {
        m_UnlockRegistry = InGameController.Instance?.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (m_UnlockRegistry != null)
            m_UnlockRegistry.OnUnlockChanged += OnUnlockChanged;
    }

    private void OnDisable()
    {
        if (m_UnlockRegistry != null)
            m_UnlockRegistry.OnUnlockChanged -= OnUnlockChanged;
        m_UnlockRegistry = null;
    }

    // 도면 해금 발생 시: 열린 목록에서 해금된 칸만 반짝 연출(페이드+테두리), 나머지 칸은 잠금 상태 동기화.
    // 현재 선택 디테일이 해금 대상이면 즉시 갱신한다.
    private void OnUnlockChanged(int _craftId)
    {
        m_CraftScroll.RefreshLockStates();
        m_CraftScroll.PlayUnlockFlashFor(_craftId);

        if (m_SelectedCraftInfo != null && m_SelectedCraftInfo.Id == _craftId)
            m_DetailPanel.Show(m_SelectedCraftInfo);
    }

    private void RebalanceLayout()
    {
        // 좌측 셀 영역과 우측 디테일 영역의 가로 비율 (3.2:6.8 — 디테일 가독성 우선)
        const float SPLIT = 0.32f;
        const float PANEL_GAP = 6f;

        if (m_LeftPanel != null)
        {
            var anchorMin = m_LeftPanel.anchorMin;
            var anchorMax = m_LeftPanel.anchorMax;
            anchorMin.x = 0f;
            anchorMax.x = SPLIT;
            m_LeftPanel.anchorMin = anchorMin;
            m_LeftPanel.anchorMax = anchorMax;

            var offMin = m_LeftPanel.offsetMin;
            var offMax = m_LeftPanel.offsetMax;
            offMin.x = 0f;
            offMax.x = -PANEL_GAP;
            m_LeftPanel.offsetMin = offMin;
            m_LeftPanel.offsetMax = offMax;
        }

        var detailRt = m_DetailPanel != null ? m_DetailPanel.transform as RectTransform : null;
        if (detailRt != null)
        {
            var anchorMin = detailRt.anchorMin;
            var anchorMax = detailRt.anchorMax;
            anchorMin.x = SPLIT;
            anchorMax.x = 1f;
            detailRt.anchorMin = anchorMin;
            detailRt.anchorMax = anchorMax;

            var offMin = detailRt.offsetMin;
            var offMax = detailRt.offsetMax;
            offMin.x = PANEL_GAP;
            offMax.x = 0f;
            detailRt.offsetMin = offMin;
            detailRt.offsetMax = offMax;
        }
    }

    private void Start()
    {
        m_CraftScroll.SetCellClickCallback(OnCellClicked);
        SelectCategory(CraftCategoryType.Building);
    }

    private void AlignHintToDetailPanel()
    {
        if (m_HintText == null || m_DetailPanel == null)
            return;

        var hintRt = m_HintText.rectTransform;
        var detailRt = m_DetailPanel.transform as RectTransform;
        if (detailRt == null)
            return;

        hintRt.anchorMin = detailRt.anchorMin;
        hintRt.anchorMax = detailRt.anchorMax;
        hintRt.pivot = detailRt.pivot;
        hintRt.anchoredPosition = detailRt.anchoredPosition;
        hintRt.sizeDelta = detailRt.sizeDelta;

        m_HintText.alignment = TMPro.TextAlignmentOptions.Center;
        m_HintText.fontSize = 24;
        m_HintText.color = new Color(0.7f, 0.7f, 0.75f, 1f);
        m_HintText.enableWordWrapping = true;
    }

    public void SelectCategory(CraftCategoryType _category)
    {
        m_CurrentCategory = _category;

        var list = Managers.Info.GetCraftInfosByCategory(_category);
        m_CraftScroll.SetData(list);
        m_CraftScroll.ClearSelection();
        m_SelectedCraftInfo = null;
        m_DetailPanel.Hide();
        SetHintVisible(true);
        ApplyTabVisualState();
    }

    private void ApplyTabVisualState()
    {
        ApplyTabStyle(m_BuildingTabButton, m_CurrentCategory == CraftCategoryType.Building);
        ApplyTabStyle(m_ItemTabButton, m_CurrentCategory == CraftCategoryType.Item);
    }

    private void ApplyTabStyle(Button _button, bool _active)
    {
        if (_button == null)
            return;

        var image = _button.GetComponent<Image>();
        if (image != null)
            image.color = _active ? TAB_ACTIVE_BG : TAB_INACTIVE_BG;

        var label = _button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (label != null)
        {
            label.color = _active ? TAB_ACTIVE_TEXT : TAB_INACTIVE_TEXT;
            label.fontStyle = _active ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
        }
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
        m_SelectedCraftInfo = _craftInfo;
        m_DetailPanel.Show(_craftInfo);
        SetHintVisible(false);
    }

    private void SetHintVisible(bool _visible)
    {
        if (m_HintText != null)
            m_HintText.gameObject.SetActive(_visible);
    }
}
