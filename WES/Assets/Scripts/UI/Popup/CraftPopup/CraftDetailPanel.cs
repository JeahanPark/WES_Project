using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftDetailPanel : MonoBehaviour
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private TextMeshProUGUI m_DescriptionText;
    [SerializeField] private TextMeshProUGUI m_MaterialsLabel;
    [SerializeField] private TextMeshProUGUI m_ConditionsLabel;
    [SerializeField] private Transform m_MaterialsContainer;
    [SerializeField] private Transform m_ConditionsContainer;
    [SerializeField] private TextMeshProUGUI m_MaterialItemTemplate;
    [SerializeField] private TextMeshProUGUI m_ConditionItemTemplate;
    [SerializeField] private Button m_CraftButton;

    private CraftInfo m_CurrentCraftInfo;

    private void Awake()
    {
        m_CraftButton.onClick.AddListener(OnClickCraft);
    }

    public void Show(CraftInfo _craftInfo)
    {
        gameObject.SetActive(true);
        m_CurrentCraftInfo = _craftInfo;

        // ShowEmptyState에서 꺼졌을 수 있는 콘텐츠 요소를 복구한다.
        if (m_CraftButton != null)
            m_CraftButton.gameObject.SetActive(true);

        if (_craftInfo == null)
        {
            Clear();
            return;
        }

        if (m_NameText != null)
            m_NameText.text = _craftInfo.Name;

        if (m_DescriptionText != null)
            m_DescriptionText.text = _craftInfo.Description;

        if (m_IconImage != null)
        {
            string iconKey = _craftInfo.IconKey;
            Sprite next = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            if (next != null)
            {
                m_IconImage.sprite = next;
                m_IconImage.enabled = true;
            }
            else
            {
                m_IconImage.enabled = false;
            }
        }

        if (IsLocked(_craftInfo.Id))
        {
            ShowLockedNotice();
        }
        else
        {
            RefreshMaterials(_craftInfo.Id);
            RefreshConditions(_craftInfo.Id);
        }
        RefreshCraftButton(_craftInfo.Id);
    }

    // 잠긴 레시피: 재료/조건 대신 도면 필요 안내 1줄을 표시한다(기획 §11.2).
    private void ShowLockedNotice()
    {
        if (m_MaterialsContainer != null && m_MaterialItemTemplate != null)
        {
            foreach (Transform child in m_MaterialsContainer)
            {
                if (child.gameObject != m_MaterialItemTemplate.gameObject)
                    Destroy(child.gameObject);
            }

            m_MaterialItemTemplate.gameObject.SetActive(false);

            var row = Instantiate(m_MaterialItemTemplate, m_MaterialsContainer);
            row.text = "이 레시피는 도면이 필요합니다";
            row.gameObject.SetActive(true);
        }

        // 도면 안내가 재료 영역에 표시되므로 "필요 자원" 헤더는 유지한다.
        if (m_MaterialsLabel != null)
            m_MaterialsLabel.gameObject.SetActive(true);
        if (m_ConditionsLabel != null)
            m_ConditionsLabel.gameObject.SetActive(false);
        if (m_ConditionsContainer != null)
            m_ConditionsContainer.gameObject.SetActive(false);
    }

    // 셀 미선택 상태: 배경판(이 GameObject의 Image)은 유지하고 콘텐츠만 비운다.
    // 과거엔 SetActive(false)로 패널 전체를 꺼서 본문 배경까지 사라져 게임월드가 비쳤다(C-1).
    // 안내문구는 CraftPopup의 HintText가 이 패널 위에 겹쳐 표시한다.
    public void ShowEmptyState()
    {
        gameObject.SetActive(true);
        m_CurrentCraftInfo = null;
        Clear();

        if (m_IconImage != null)
            m_IconImage.enabled = false;

        ClearContainer(m_MaterialsContainer, m_MaterialItemTemplate);
        ClearContainer(m_ConditionsContainer, m_ConditionItemTemplate);

        // 미선택(빈) 상태에서는 "필요 자원"·"제작 조건" 헤더를 숨긴다(안내문구만 노출).
        if (m_MaterialsLabel != null)
            m_MaterialsLabel.gameObject.SetActive(false);
        if (m_ConditionsLabel != null)
            m_ConditionsLabel.gameObject.SetActive(false);
        if (m_CraftButton != null)
            m_CraftButton.gameObject.SetActive(false);
    }

    public void Hide()
    {
        // 배경판을 끄지 않는다. 콘텐츠만 비우는 ShowEmptyState로 위임(C-1 수정).
        ShowEmptyState();
    }

    // 컨테이너의 동적 생성 행을 제거하고 템플릿은 비활성으로 남긴다.
    private void ClearContainer(Transform _container, TextMeshProUGUI _template)
    {
        if (_container == null || _template == null)
            return;

        foreach (Transform child in _container)
        {
            if (child.gameObject != _template.gameObject)
                Destroy(child.gameObject);
        }
        _template.gameObject.SetActive(false);
    }

    private void OnClickCraft()
    {
        if (m_CurrentCraftInfo == null)
            return;

        if (!CanCraft(m_CurrentCraftInfo.Id))
            return;

        ExecuteCraft(m_CurrentCraftInfo.Id);
    }

    private bool CanCraft(int _craftId)
    {
        if (IsLocked(_craftId))
            return false;

        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory == null)
            return false;

        var materials = Managers.Info.GetMaterialsByCraftId(_craftId);
        foreach (var material in materials)
        {
            var item = inventory.GetItem(material.MaterialItemId);
            if (item == null || item.Count < material.RequiredCount)
                return false;
        }

        var player = InGameController.Instance?.PlayWorker?.LocalPlayer;
        var conditions = Managers.Info.GetConditionsByCraftId(_craftId);
        foreach (var condition in conditions)
        {
            if (player == null)
                return false;

            bool conditionMet = condition.ConditionType switch
            {
                CraftConditionType.MaxCold => player.Cold <= condition.ConditionValue,
                CraftConditionType.MinCold => player.Cold >= condition.ConditionValue,
                _ => true
            };

            if (!conditionMet)
                return false;
        }

        return true;
    }

    private void ExecuteCraft(int _craftId)
    {
        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory == null)
            return;

        var materials = Managers.Info.GetMaterialsByCraftId(_craftId);
        foreach (var material in materials)
        {
            inventory.RemoveItem(material.MaterialItemId, material.RequiredCount);
        }

        if (m_CurrentCraftInfo.Value01 > 0)
        {
            inventory.AddItem(m_CurrentCraftInfo.Value01, m_CurrentCraftInfo.ResultCount);
        }

        Managers.Audio?.PlaySfx(AudioKey.SFX_CRAFT); // R4 ③ 제작 성공 SFX(로컬, 음원0=무음)

        Show(m_CurrentCraftInfo);
    }

    private void RefreshCraftButton(int _craftId)
    {
        if (m_CraftButton == null)
            return;

        bool locked = IsLocked(_craftId);
        bool canCraft = CanCraft(_craftId);
        m_CraftButton.interactable = canCraft;

        var image = m_CraftButton.GetComponent<Image>();
        if (image != null)
            image.color = canCraft ? new Color(0.298f, 0.686f, 0.314f, 1f) : new Color(0.32f, 0.32f, 0.32f, 1f);

        var label = m_CraftButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = locked ? "도면 필요" : (canCraft ? "제작하기" : "재료 부족");
    }

    // 해당 레시피가 도면 잠금 상태인지.
    private bool IsLocked(int _craftId)
    {
        if (!Managers.Info.IsBlueprintLockedCraft(_craftId))
            return false;

        var registry = InGameController.Instance?.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (registry == null)
            return true;

        return !registry.IsUnlocked(_craftId);
    }

    private void RefreshMaterials(int _craftId)
    {
        if (m_MaterialsContainer == null || m_MaterialItemTemplate == null)
            return;

        foreach (Transform child in m_MaterialsContainer)
        {
            if (child.gameObject != m_MaterialItemTemplate.gameObject)
                Destroy(child.gameObject);
        }

        m_MaterialItemTemplate.gameObject.SetActive(false);

        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        var materials = Managers.Info.GetMaterialsByCraftId(_craftId);

        // 재료가 있는 레시피를 선택했을 때만 "필요 자원" 헤더를 노출한다.
        if (m_MaterialsLabel != null)
            m_MaterialsLabel.gameObject.SetActive(materials != null && materials.Count > 0);

        foreach (var material in materials)
        {
            var itemInfo = Managers.Info.ItemInfoList.Find(_i => _i.Id == material.MaterialItemId);
            string itemName = itemInfo != null ? itemInfo.Name : $"Item({material.MaterialItemId})";

            int owned = 0;
            if (inventory != null)
            {
                var item = inventory.GetItem(material.MaterialItemId);
                owned = item?.Count ?? 0;
            }

            var row = Instantiate(m_MaterialItemTemplate, m_MaterialsContainer);
            row.text = $"{itemName} {owned}/{material.RequiredCount}";
            row.gameObject.SetActive(true);

            var iconChild = row.transform.Find("IconImage");
            if (iconChild != null && itemInfo != null && !string.IsNullOrEmpty(itemInfo.IconKey))
            {
                var iconImg = iconChild.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = Managers.Resource.LoadAddressable<Sprite>(itemInfo.IconKey);
                    iconImg.enabled = iconImg.sprite != null;
                }
            }
        }
    }

    private void RefreshConditions(int _craftId)
    {
        if (m_ConditionsContainer == null || m_ConditionItemTemplate == null)
            return;

        foreach (Transform child in m_ConditionsContainer)
        {
            if (child.gameObject != m_ConditionItemTemplate.gameObject)
                Destroy(child.gameObject);
        }

        m_ConditionItemTemplate.gameObject.SetActive(false);

        var conditions = Managers.Info.GetConditionsByCraftId(_craftId);
        bool hasAnyCondition = conditions != null && conditions.Count > 0;
        if (m_ConditionsLabel != null)
            m_ConditionsLabel.gameObject.SetActive(hasAnyCondition);
        m_ConditionsContainer.gameObject.SetActive(hasAnyCondition);

        foreach (var condition in conditions)
        {
            string conditionText = condition.ConditionType switch
            {
                CraftConditionType.MaxCold => $"추위 수치 {condition.ConditionValue} 이하",
                CraftConditionType.MinCold => $"추위 수치 {condition.ConditionValue} 이상",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(conditionText))
                continue;

            var row = Instantiate(m_ConditionItemTemplate, m_ConditionsContainer);
            row.text = conditionText;
            row.gameObject.SetActive(true);
        }
    }

    private void Clear()
    {
        if (m_NameText != null) m_NameText.text = string.Empty;
        if (m_DescriptionText != null) m_DescriptionText.text = string.Empty;
        if (m_IconImage != null) m_IconImage.sprite = null;
    }
}
