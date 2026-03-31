using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoSingleton<PopupManager>
{
    private Canvas m_Canvas;
    private Transform m_PopupRoot;
    private List<BasePopup> m_OpenedPopups = new List<BasePopup>();

    public override void Init()
    {
        base.Init();
    }

    public void InitializeForScene(Canvas _canvas)
    {
        m_Canvas = _canvas;
        m_OpenedPopups.Clear();
        CreatePopupRoot();
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
        rectTransform.localScale = Vector3.one;
        rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0f);

        m_PopupRoot = rectTransform;
    }

    public T Open<T>() where T : BasePopup
    {
        string popupName = typeof(T).Name;
        GameObject popupObj = Managers.Resource.InstantiateAddressable(popupName, m_PopupRoot);

        if (popupObj == null)
        {
            GameDebug.LogError($"Failed to open popup: {popupName}");
            return null;
        }

        T popup = popupObj.GetComponent<T>();
        if (popup == null)
        {
            GameDebug.LogError($"Popup prefab does not have component: {typeof(T).Name}");
            Object.Destroy(popupObj);
            return null;
        }

        PopupOpenPolicy policy = popup.GetOpenPolicy();
        if (policy == PopupOpenPolicy.CloseAllAndOpen)
        {
            CloseAll();
        }

        m_OpenedPopups.Add(popup);

        return popup;
    }

    public T FindOpen<T>() where T : BasePopup
    {
        foreach (var popup in m_OpenedPopups)
        {
            if (popup is T typed)
                return typed;
        }
        return null;
    }

    public void Close(BasePopup _popup)
    {
        if (_popup == null)
            return;

        m_OpenedPopups.Remove(_popup);
        Managers.Resource.Destroy(_popup.gameObject);
    }

    public void CloseAll()
    {
        for (int i = m_OpenedPopups.Count - 1; i >= 0; i--)
        {
            if (m_OpenedPopups[i] != null)
            {
                Managers.Resource.Destroy(m_OpenedPopups[i].gameObject);
            }
        }
        m_OpenedPopups.Clear();
    }
}
