using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class BaseScroll<TData> : ScrollRect
{
    [SerializeField] private float m_CellSize = 100f;
    [SerializeField] private float m_Spacing = 10f;
    [SerializeField] private bool m_ReverseDirection = false;

    private BaseScrollCell<TData> m_CellPrefab;

    private List<TData> m_DataList = new List<TData>();
    private int m_DataCount;
    private readonly List<BaseScrollCell<TData>> m_ActiveCells = new List<BaseScrollCell<TData>>();
    private readonly Queue<BaseScrollCell<TData>> m_CellPool = new Queue<BaseScrollCell<TData>>();
    private int m_FirstVisibleIndex = -1;
    private int m_LastVisibleIndex = -1;

    private float m_ViewportSize;
    private int m_GridColumnCount = 1;
    private bool m_IsGridMode = false;

    protected override void Awake()
    {
        base.Awake();
        AutoSetupScrollComponents();

        UpdateViewportSize();

        onValueChanged.AddListener(OnScrollValueChanged);

        OnAwake();
    }

    private void AutoSetupScrollComponents()
    {
        if (content == null)
        {
            Debug.LogError($"[BaseScroll] Content RectTransform not found in ScrollRect on {gameObject.name}.");
            return;
        }

        m_CellPrefab = content.GetComponentInChildren<BaseScrollCell<TData>>(true);
        if (m_CellPrefab == null)
        {
            Debug.LogError($"[BaseScroll] CellPrefab not found in Content's children on {gameObject.name}. Please add a cell prefab as a child of Content.");
            return;
        }

        if (m_CellSize <= 0f)
        {
            if (vertical && !horizontal)
            {
                m_CellSize = m_CellPrefab.RectTransform.sizeDelta.y;
            }
            else if (!vertical && horizontal)
            {
                m_CellSize = m_CellPrefab.RectTransform.sizeDelta.x;
            }
            else if (vertical && horizontal)
            {
                m_CellSize = Mathf.Max(m_CellPrefab.RectTransform.sizeDelta.x, m_CellPrefab.RectTransform.sizeDelta.y);
            }

            if (m_CellSize <= 0f)
            {
                m_CellSize = 100f;
                Debug.LogWarning($"[BaseScroll] CellPrefab size is 0 or negative. Using default size: {m_CellSize}");
            }
        }

        m_IsGridMode = vertical && horizontal;

        if (m_IsGridMode)
        {
            CalculateGridLayout();
        }
        m_CellPrefab.gameObject.SetActive(false);
    }

    private void CalculateGridLayout()
    {
        float contentWidth = content.rect.width;
        float cellWithSpacing = m_CellSize + m_Spacing;

        m_GridColumnCount = Mathf.Max(1, Mathf.FloorToInt((contentWidth + m_Spacing) / cellWithSpacing));
    }

    protected virtual void OnAwake()
    {
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    public void SetData(List<TData> _dataList)
    {
        m_DataList.Clear();
        if (_dataList != null)
        {
            m_DataList.AddRange(_dataList);
        }
        m_DataCount = m_DataList.Count;
        UpdateContentSize();
        RecycleAllCells();
        RefreshVisibleCells();
    }

    public void Initialize(int _dataCount)
    {
        m_DataCount = _dataCount;
        UpdateContentSize();
        RecycleAllCells();
        RefreshVisibleCells();
    }

    public void RefreshData(int _dataCount)
    {
        m_DataCount = _dataCount;
        UpdateContentSize();
        RefreshVisibleCells();
    }

    public TData GetData(int _index)
    {
        if (_index < 0 || _index >= m_DataList.Count)
            return default;

        return m_DataList[_index];
    }

    public List<TData> GetDataList()
    {
        return m_DataList;
    }

    public void RefreshVisibleCells()
    {
        UpdateVisibleRange();
        UpdateCells();
    }

    private void UpdateContentSize()
    {
        if (m_IsGridMode)
        {
            int totalRows = Mathf.CeilToInt((float)m_DataCount / m_GridColumnCount);
            float totalHeight = totalRows * m_CellSize + Mathf.Max(0, totalRows - 1) * m_Spacing;
            content.sizeDelta = new Vector2(content.sizeDelta.x, totalHeight);
        }
        else if (vertical)
        {
            float totalSize = m_DataCount * m_CellSize + Mathf.Max(0, m_DataCount - 1) * m_Spacing;
            content.sizeDelta = new Vector2(content.sizeDelta.x, totalSize);
        }
        else if (horizontal)
        {
            float totalSize = m_DataCount * m_CellSize + Mathf.Max(0, m_DataCount - 1) * m_Spacing;
            content.sizeDelta = new Vector2(totalSize, content.sizeDelta.y);
        }
    }

    private void UpdateViewportSize()
    {
        m_ViewportSize = vertical ? viewport.rect.height : viewport.rect.width;
    }

    private void OnScrollValueChanged(Vector2 _scrollPosition)
    {
        RefreshVisibleCells();
    }

    private void UpdateVisibleRange()
    {
        if (m_DataCount == 0)
        {
            m_FirstVisibleIndex = -1;
            m_LastVisibleIndex = -1;
            return;
        }

        float cellWithSpacing = m_CellSize + m_Spacing;

        if (m_IsGridMode)
        {
            float scrollPosition = m_ReverseDirection ? content.anchoredPosition.y : -content.anchoredPosition.y;
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollPosition / cellWithSpacing));
            int visibleRows = Mathf.CeilToInt(m_ViewportSize / cellWithSpacing) + 2;
            int lastRow = firstRow + visibleRows;

            m_FirstVisibleIndex = firstRow * m_GridColumnCount;
            m_LastVisibleIndex = Mathf.Min(m_DataCount - 1, (lastRow + 1) * m_GridColumnCount - 1);
        }
        else if (vertical)
        {
            float scrollPosition = m_ReverseDirection ? content.anchoredPosition.y : -content.anchoredPosition.y;
            int firstIndex = Mathf.Max(0, Mathf.FloorToInt(scrollPosition / cellWithSpacing));
            int visibleCount = Mathf.CeilToInt(m_ViewportSize / cellWithSpacing) + 2;
            int lastIndex = Mathf.Min(m_DataCount - 1, firstIndex + visibleCount);

            m_FirstVisibleIndex = firstIndex;
            m_LastVisibleIndex = lastIndex;
        }
        else if (horizontal)
        {
            float scrollPosition = m_ReverseDirection ? -content.anchoredPosition.x : content.anchoredPosition.x;
            int firstIndex = Mathf.Max(0, Mathf.FloorToInt(scrollPosition / cellWithSpacing));
            int visibleCount = Mathf.CeilToInt(m_ViewportSize / cellWithSpacing) + 2;
            int lastIndex = Mathf.Min(m_DataCount - 1, firstIndex + visibleCount);

            m_FirstVisibleIndex = firstIndex;
            m_LastVisibleIndex = lastIndex;
        }
    }

    private void UpdateCells()
    {
        if (m_FirstVisibleIndex == -1 || m_LastVisibleIndex == -1)
        {
            RecycleAllCells();
            return;
        }

        for (int i = m_ActiveCells.Count - 1; i >= 0; i--)
        {
            BaseScrollCell<TData> cell = m_ActiveCells[i];
            if (cell.Index < m_FirstVisibleIndex || cell.Index > m_LastVisibleIndex)
            {
                RecycleCell(cell);
            }
        }

        for (int i = m_FirstVisibleIndex; i <= m_LastVisibleIndex; i++)
        {
            if (!IsCellActive(i))
            {
                BaseScrollCell<TData> cell = GetOrCreateCell();
                TData data = GetData(i);
                cell.UpdateCell(i, data);
                cell.SetPosition(GetCellPosition(i));
                cell.gameObject.SetActive(true);
#if UNITY_EDITOR
                cell.gameObject.name = $"Cell [{i}]";
#endif
                m_ActiveCells.Add(cell);
            }
        }
    }

    private bool IsCellActive(int _index)
    {
        foreach (BaseScrollCell<TData> cell in m_ActiveCells)
        {
            if (cell.Index == _index)
                return true;
        }
        return false;
    }

    private Vector2 GetCellPosition(int _index)
    {
        float cellWithSpacing = m_CellSize + m_Spacing;

        if (m_IsGridMode)
        {
            int row = _index / m_GridColumnCount;
            int column = _index % m_GridColumnCount;

            float xPos = column * cellWithSpacing;
            float yPos = row * cellWithSpacing;

            return new Vector2(xPos, -yPos);
        }
        else if (vertical)
        {
            float position = _index * cellWithSpacing;
            return new Vector2(0, -position);
        }
        else if (horizontal)
        {
            float position = _index * cellWithSpacing;
            return new Vector2(position, 0);
        }

        return Vector2.zero;
    }

    private BaseScrollCell<TData> GetOrCreateCell()
    {
        if (m_CellPool.Count > 0)
        {
            return m_CellPool.Dequeue();
        }

        BaseScrollCell<TData> newCell = Instantiate(m_CellPrefab, content);

        if (m_IsGridMode)
        {
            newCell.RectTransform.anchorMin = new Vector2(0, 1);
            newCell.RectTransform.anchorMax = new Vector2(0, 1);
            newCell.RectTransform.pivot = new Vector2(0, 1);
            newCell.RectTransform.sizeDelta = new Vector2(m_CellSize, m_CellSize);
        }
        else if (vertical)
        {
            newCell.RectTransform.anchorMin = new Vector2(0, 1);
            newCell.RectTransform.anchorMax = new Vector2(1, 1);
            newCell.RectTransform.pivot = new Vector2(0.5f, 1f);
            newCell.RectTransform.sizeDelta = new Vector2(0, m_CellSize);
        }
        else if (horizontal)
        {
            newCell.RectTransform.anchorMin = new Vector2(0, 0);
            newCell.RectTransform.anchorMax = new Vector2(0, 1);
            newCell.RectTransform.pivot = new Vector2(0, 0.5f);
            newCell.RectTransform.sizeDelta = new Vector2(m_CellSize, 0);
        }

        return newCell;
    }

    private void RecycleCell(BaseScrollCell<TData> _cell)
    {
        _cell.OnRecycle();
        _cell.gameObject.SetActive(false);
        m_ActiveCells.Remove(_cell);
        m_CellPool.Enqueue(_cell);
    }

    private void RecycleAllCells()
    {
        for (int i = m_ActiveCells.Count - 1; i >= 0; i--)
        {
            RecycleCell(m_ActiveCells[i]);
        }
    }

    public void ScrollToIndex(int _index, bool _immediate = false)
    {
        if (_index < 0 || _index >= m_DataCount)
            return;

        float targetPosition = _index * (m_CellSize + m_Spacing);

        if (vertical)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetPosition);
        }
        else
        {
            content.anchoredPosition = new Vector2(-targetPosition, content.anchoredPosition.y);
        }

        if (_immediate)
        {
            Canvas.ForceUpdateCanvases();
            RefreshVisibleCells();
        }
    }

    public BaseScrollCell<TData> GetCellAtIndex(int _index)
    {
        foreach (BaseScrollCell<TData> cell in m_ActiveCells)
        {
            if (cell.Index == _index)
                return cell;
        }
        return null;
    }

    public void Clear()
    {
        RecycleAllCells();
        m_DataCount = 0;
        UpdateContentSize();
    }
}
