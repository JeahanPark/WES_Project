using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건물 설치 배치 모드를 관리하는 Worker.
/// 마우스 커서에 건물 아이콘 UI를 표시하고,
/// 지면에 범위 인디케이터를 표시한다. 클릭 시 해당 위치에 건물을 설치한다.
/// </summary>
public class BuildingPlacementWorker : MonoBehaviour
{
    private const float RANGE_RADIUS = 1.5f;
    private const int CIRCLE_SEGMENTS = 48;
    private const float CURSOR_ICON_SIZE = 64f;

    private int m_BuildingInfoId;
    private int m_ItemInfoId;
    private bool m_IsPlacing;
    private Vector3 m_PlacementPosition;

    private Canvas m_Canvas;
    private Image m_CursorImage;
    private GameObject m_RangeIndicator;

    public bool IsPlacing => m_IsPlacing;

    private void Awake()
    {
        CreateCursorUI();
        CreateRangeIndicator();
    }

    private void Update()
    {
        if (!m_IsPlacing)
            return;

        UpdateCursorPosition();
        UpdateGroundPosition();

        if (Input.GetMouseButtonDown(0))
            ConfirmPlacement();

        if (Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    public void StartPlacement(int _itemInfoId)
    {
        var itemInfo = Managers.Info.ItemInfoList.Find(x => x.Id == _itemInfoId);
        if (itemInfo == null || !itemInfo.IsBuilding)
            return;

        var buildingInfo = Managers.Info.BuildingInfoList.Find(x => x.Id == itemInfo.BuildingInfoId);
        if (buildingInfo == null)
            return;

        m_ItemInfoId = _itemInfoId;
        m_BuildingInfoId = itemInfo.BuildingInfoId;

        if (m_CursorImage != null && !string.IsNullOrEmpty(itemInfo.IconKey))
            m_CursorImage.sprite = Managers.Resource.LoadAddressable<Sprite>(itemInfo.IconKey);

        m_Canvas.gameObject.SetActive(true);
        m_RangeIndicator.SetActive(true);
        m_IsPlacing = true;
    }

    public void CancelPlacement()
    {
        m_Canvas.gameObject.SetActive(false);
        m_RangeIndicator.SetActive(false);
        m_IsPlacing = false;
    }

    private void UpdateCursorPosition()
    {
        if (m_CursorImage != null)
            m_CursorImage.rectTransform.position = Input.mousePosition;
    }

    private void UpdateGroundPosition()
    {
        var camera = InGameController.Instance?.CameraWorker?.GetCamera();
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float dist))
        {
            m_PlacementPosition = ray.GetPoint(dist);
            m_RangeIndicator.transform.position = m_PlacementPosition;
        }
    }

    private void ConfirmPlacement()
    {
        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        inventory?.RemoveItem(m_ItemInfoId, 1);

        InGameController.Instance?.PlayWorker?.SpawnBuilding(m_BuildingInfoId, m_PlacementPosition);
        CancelPlacement();
    }

    private void CreateCursorUI()
    {
        var canvasGo = new GameObject("BuildingPlacementCanvas");
        canvasGo.transform.SetParent(transform);

        m_Canvas = canvasGo.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var iconGo = new GameObject("CursorIcon");
        iconGo.transform.SetParent(canvasGo.transform, false);

        m_CursorImage = iconGo.AddComponent<Image>();
        m_CursorImage.raycastTarget = false;

        var rt = m_CursorImage.rectTransform;
        rt.sizeDelta = new Vector2(CURSOR_ICON_SIZE, CURSOR_ICON_SIZE);
        rt.pivot = new Vector2(0f, 1f);

        m_Canvas.gameObject.SetActive(false);
    }

    private void CreateRangeIndicator()
    {
        m_RangeIndicator = new GameObject("RangeIndicator");
        m_RangeIndicator.transform.SetParent(transform);

        var lr = m_RangeIndicator.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.widthMultiplier = 0.08f;
        lr.positionCount = CIRCLE_SEGMENTS;
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0f, 1f, 0f, 0.8f);
        lr.endColor = new Color(0f, 1f, 0f, 0.8f);

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = 2f * Mathf.PI * i / CIRCLE_SEGMENTS;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * RANGE_RADIUS,
                0.02f,
                Mathf.Sin(angle) * RANGE_RADIUS));
        }

        m_RangeIndicator.SetActive(false);
    }
}
