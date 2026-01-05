using UnityEngine;

public abstract class BaseScrollCell<TData> : MonoBehaviour
{
    public RectTransform RectTransform { get; private set; }
    public int Index { get; private set; }
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        OnAwake();
    }

    protected virtual void OnAwake()
    {
    }

    public void Initialize(int _index, TData _data)
    {
        Index = _index;
        IsInitialized = true;
        OnInitialize(_index, _data);
    }

    public void UpdateCell(int _index, TData _data)
    {
        Index = _index;
        OnUpdateCell(_index, _data);
    }

    public void SetPosition(Vector2 _position)
    {
        RectTransform.anchoredPosition = _position;
    }

    protected abstract void OnInitialize(int _index, TData _data);
    protected abstract void OnUpdateCell(int _index, TData _data);

    public virtual void OnRecycle()
    {
    }
}
