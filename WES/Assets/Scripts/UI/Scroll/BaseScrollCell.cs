using UnityEngine;

public abstract class BaseScrollCell<TData> : MonoBehaviour
{
    private RectTransform m_RectTransform;
    public RectTransform RectTransform
    {
        get
        {
            if (m_RectTransform == null)
                m_RectTransform = GetComponent<RectTransform>();
            return m_RectTransform;
        }
    }

    public int Index { get; private set; }

    public void UpdateCell(int _index, TData _data)
    {
        Index = _index;
        OnUpdateCell(_index, _data);
    }

    public void SetPosition(Vector2 _position)
    {
        RectTransform.anchoredPosition = _position;
    }

    protected abstract void OnUpdateCell(int _index, TData _data);

    public virtual void OnRecycle()
    {
    }
}
