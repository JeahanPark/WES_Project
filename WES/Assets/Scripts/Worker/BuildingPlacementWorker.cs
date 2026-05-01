using UnityEngine;
using UnityEngine.EventSystems;
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
    private const float OVERLAP_CHECK_RADIUS = 1.0f;

    [SerializeField] private LayerMask m_GroundLayerMask;
    [SerializeField] private LayerMask m_BlockingLayerMask;

    private int m_BuildingInfoId;
    private int m_ItemInfoId;
    private bool m_IsPlacing;
    private bool m_HasValidPosition;
    private bool m_IsPlacementValid;
    private Vector3 m_PlacementPosition;

    private Canvas m_Canvas;
    private Image m_CursorImage;
    private GameObject m_RangeIndicator;
    private LineRenderer m_RangeLineRenderer;

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
        UpdateValidationFeedback();

        // UI 위에 마우스가 있을 때 클릭은 무시 (P2)
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            ConfirmPlacement();

        if (Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    public void StartPlacement(int _itemInfoId)
    {
        var itemInfo = Managers.Info.ItemInfoList.Find(x => x.Id == _itemInfoId);
        if (itemInfo == null || !itemInfo.IsBuilding)
            return;

        // 인벤토리에 해당 아이템이 실제로 있는지 검증 (P6)
        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory == null)
            return;

        var ownedItem = inventory.GetItem(_itemInfoId);
        if (ownedItem == null || ownedItem.Count <= 0)
        {
            GameDebug.LogWarning($"[BuildingPlacementWorker] StartPlacement 차단: 아이템({_itemInfoId}) 보유 0");
            return;
        }

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
        m_HasValidPosition = false;
        m_IsPlacementValid = false;
    }

    public void CancelPlacement()
    {
        m_Canvas.gameObject.SetActive(false);
        m_RangeIndicator.SetActive(false);
        m_IsPlacing = false;
        m_HasValidPosition = false;
        m_IsPlacementValid = false;
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
        {
            m_HasValidPosition = false;
            return;
        }

        // P4: 평면 가정 대신 Ground 레이어 Physics.Raycast로 실제 지형 위에 위치
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        int mask = m_GroundLayerMask.value != 0
            ? m_GroundLayerMask.value
            : (1 << LayerMask.NameToLayer("Ground"));

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, mask))
        {
            m_PlacementPosition = hit.point;
            m_RangeIndicator.transform.position = m_PlacementPosition;
            m_HasValidPosition = true;
        }
        else
        {
            m_HasValidPosition = false;
        }
    }

    private void UpdateValidationFeedback()
    {
        if (!m_HasValidPosition)
        {
            m_IsPlacementValid = false;
            ApplyRangeColor(false);
            return;
        }

        m_IsPlacementValid = !HasOverlap(m_PlacementPosition);
        ApplyRangeColor(m_IsPlacementValid);
    }

    // P5: 다른 건물·플레이어·몬스터 등과 겹쳐 설치되는 것을 방지
    private bool HasOverlap(Vector3 _position)
    {
        int blockingMask = m_BlockingLayerMask.value;
        if (blockingMask == 0)
        {
            // 기본값: Default 레이어 외 모든 충돌 가능 레이어
            int groundLayer = LayerMask.NameToLayer("Ground");
            blockingMask = ~(1 << groundLayer);
        }

        var hits = Physics.OverlapSphere(_position, OVERLAP_CHECK_RADIUS, blockingMask, QueryTriggerInteraction.Ignore);
        return hits != null && hits.Length > 0;
    }

    private void ApplyRangeColor(bool _valid)
    {
        if (m_RangeLineRenderer == null)
            return;

        Color color = _valid
            ? new Color(0f, 1f, 0f, 0.8f)
            : new Color(1f, 0f, 0f, 0.8f);
        m_RangeLineRenderer.startColor = color;
        m_RangeLineRenderer.endColor = color;
    }

    private bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private void ConfirmPlacement()
    {
        if (!m_HasValidPosition || !m_IsPlacementValid)
            return;

        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory == null)
            return;

        // P6: 차감 시점에도 잔여 수량 재확인 (StartPlacement 후 다른 경로로 소비됐을 가능성)
        var ownedItem = inventory.GetItem(m_ItemInfoId);
        if (ownedItem == null || ownedItem.Count <= 0)
        {
            GameDebug.LogWarning($"[BuildingPlacementWorker] ConfirmPlacement 차단: 아이템({m_ItemInfoId}) 보유 0");
            CancelPlacement();
            return;
        }

        inventory.RemoveItem(m_ItemInfoId, 1);
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

        m_RangeLineRenderer = m_RangeIndicator.AddComponent<LineRenderer>();
        m_RangeLineRenderer.loop = true;
        m_RangeLineRenderer.widthMultiplier = 0.08f;
        m_RangeLineRenderer.positionCount = CIRCLE_SEGMENTS;
        m_RangeLineRenderer.useWorldSpace = false;
        m_RangeLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        m_RangeLineRenderer.startColor = new Color(0f, 1f, 0f, 0.8f);
        m_RangeLineRenderer.endColor = new Color(0f, 1f, 0f, 0.8f);

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = 2f * Mathf.PI * i / CIRCLE_SEGMENTS;
            m_RangeLineRenderer.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * RANGE_RADIUS,
                0.02f,
                Mathf.Sin(angle) * RANGE_RADIUS));
        }

        m_RangeIndicator.SetActive(false);
    }
}
