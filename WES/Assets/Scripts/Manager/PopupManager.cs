using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoSingleton<PopupManager>
{
    private Canvas m_Canvas;
    private Transform m_PopupRoot;

    public override void Init()
    {
        base.Init();
        CreateCanvas();
        CreatePopupRoot();
    }

    private void CreateCanvas()
    {
        m_Canvas = Object.FindFirstObjectByType<Canvas>();
        if (m_Canvas == null)
        {
            Debug.LogError("PopupManager: No Canvas found in scene!");
        }
    }

    private void CreatePopupRoot()
    {
        if (m_Canvas == null)
        {
            return;
        }

        Transform existingPopup = m_Canvas.transform.Find("Popup");
        if (existingPopup != null)
        {
            m_PopupRoot = existingPopup;
            return;
        }

        GameObject rootObj = new GameObject("Popup");
        RectTransform rectTransform = rootObj.AddComponent<RectTransform>();
        rectTransform.SetParent(m_Canvas.transform);

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        m_PopupRoot = rectTransform;
    }

    public T Open<T>() where T : MonoBehaviour
    {
        string popupName = typeof(T).Name;
        GameObject popupObj = Managers.Resource.InstantiateAddressable(popupName, m_PopupRoot);

        if (popupObj == null)
        {
            Debug.LogError($"Failed to open popup: {popupName}");
            return null;
        }

        T popup = popupObj.GetComponent<T>();
        if (popup == null)
        {
            Debug.LogError($"Popup prefab does not have component: {typeof(T).Name}");
            Object.Destroy(popupObj);
            return null;
        }

        return popup;
    }

    public void Close(GameObject _popup)
    {
        if (_popup == null)
        {
            return;
        }

        Managers.Resource.Destroy(_popup);
    }
}
