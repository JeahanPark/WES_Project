using UnityEngine;

/// <summary>
/// WorldUI 기본 클래스
/// </summary>
public abstract class BaseWorldUI : MonoBehaviour
{
    private bool m_IsActive;

    protected RectTransform m_RectTransform;

    public bool IsActive => m_IsActive;

    protected virtual void Awake()
    {
        m_RectTransform = GetComponent<RectTransform>();
    }

    public virtual void Initialize()
    {
        m_IsActive = true;
        gameObject.SetActive(true);

        OnInitialize();
    }

    public virtual void Release()
    {
        m_IsActive = false;

        OnRelease();

        gameObject.SetActive(false);
    }

    protected virtual void OnInitialize()
    {
    }

    protected virtual void OnRelease()
    {
    }

    protected void SetVisible(bool _visible)
    {
        if (gameObject.activeSelf != _visible)
        {
            gameObject.SetActive(_visible);
        }
    }
}
