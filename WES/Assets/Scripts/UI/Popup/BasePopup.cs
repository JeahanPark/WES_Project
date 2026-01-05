using UnityEngine;

public enum PopupOpenPolicy
{
    CloseAllAndOpen,   // 기존 팝업 전부 닫고 단독으로 연다
    StackOnTop         // 기존 팝업 위에 쌓아서 연다
}

public class BasePopup : MonoBehaviour
{
    [SerializeField] private PopupOpenPolicy m_OpenPolicy = PopupOpenPolicy.StackOnTop;

    public PopupOpenPolicy GetOpenPolicy()
    {
        return m_OpenPolicy;
    }
    public virtual void Close()
    {
        PopupManager.Instance.Close(this);
    }
}
