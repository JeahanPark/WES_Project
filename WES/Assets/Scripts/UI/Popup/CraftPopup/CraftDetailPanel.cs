using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftDetailPanel : MonoBehaviour
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private TextMeshProUGUI m_DescriptionText;
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

        RefreshMaterials(_craftInfo.Id);
        RefreshConditions(_craftInfo.Id);
        RefreshCraftButton(_craftInfo.Id);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
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

        Show(m_CurrentCraftInfo);
    }

    private void RefreshCraftButton(int _craftId)
    {
        if (m_CraftButton == null)
            return;

        bool canCraft = CanCraft(_craftId);
        m_CraftButton.interactable = canCraft;

        var image = m_CraftButton.GetComponent<Image>();
        if (image != null)
            image.color = canCraft ? new Color(0.298f, 0.686f, 0.314f, 1f) : new Color(0.32f, 0.32f, 0.32f, 1f);

        var label = m_CraftButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = canCraft ? "제작하기" : "재료 부족";
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
