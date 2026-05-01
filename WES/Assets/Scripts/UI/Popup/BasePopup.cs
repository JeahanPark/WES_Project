using UnityEngine;
using UnityEngine.UI;

public enum PopupOpenPolicy
{
    CloseAllAndOpen,   // 기존 팝업 전부 닫고 단독으로 연다
    StackOnTop         // 기존 팝업 위에 쌓아서 연다
}

public class BasePopup : MonoBehaviour
{
    private static readonly Color DIM_COLOR = new Color(0f, 0f, 0f, 0.55f);

    [SerializeField] private PopupOpenPolicy m_OpenPolicy = PopupOpenPolicy.StackOnTop;
    [SerializeField] private bool m_UseDimBackdrop = true;

    private GameObject m_DimBackdrop;

    protected virtual void OnEnable()
    {
        if (m_UseDimBackdrop)
            EnsureDimBackdrop();
    }

    public PopupOpenPolicy GetOpenPolicy()
    {
        return m_OpenPolicy;
    }

    public virtual void Close()
    {
        PopupManager.Instance.Close(this);
    }

    private void EnsureDimBackdrop()
    {
        if (m_DimBackdrop != null)
            return;

        var rt = transform as RectTransform;
        if (rt == null)
            return;

        var dimGo = new GameObject("DimBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimGo.transform.SetParent(transform, false);
        dimGo.transform.SetAsFirstSibling();

        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dimRt.sizeDelta = Vector2.zero;

        var image = dimGo.GetComponent<Image>();
        image.color = DIM_COLOR;
        image.raycastTarget = true; // 뒤 클릭 차단

        m_DimBackdrop = dimGo;
    }
}
