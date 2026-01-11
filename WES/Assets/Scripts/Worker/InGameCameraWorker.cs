using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

/// <summary>
/// Cinemachine Virtual Camera를 사용한 3인칭 카메라 컨트롤러
/// - Cinemachine으로 타겟 추적
/// - 마우스 휠로 줌 제어
/// - 맵 경계 제한
/// </summary>
public class InGameCameraWorker : MonoBehaviour
{
    public const float DEFAULT_DISTANCE = 15f;
    public const float DEFAULT_ZOOM_SPEED = 2f;
    public const float DEFAULT_MIN_DISTANCE = 5f;
    public const float DEFAULT_MAX_DISTANCE = 30f;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera m_VirtualCamera;

    [Header("Target Settings")]
    [SerializeField] private Transform m_Target;

    [Header("Zoom Settings")]
    [SerializeField] private float m_ZoomSpeed = DEFAULT_ZOOM_SPEED;
    [SerializeField] private float m_MinDistance = DEFAULT_MIN_DISTANCE;
    [SerializeField] private float m_MaxDistance = DEFAULT_MAX_DISTANCE;

    [Header("Boundaries")]
    [SerializeField] private bool m_UseBoundaries = false;
    [SerializeField] private Vector2 m_MinBoundary = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 m_MaxBoundary = new Vector2(50f, 50f);

    private Camera m_Camera;
    private CinemachineFollow m_Follow;
    private CinemachineRotationComposer m_RotationComposer;
    private float m_CurrentDistance = DEFAULT_DISTANCE;
    private GameObject m_TargetProxy;

    private void Awake()
    {
        m_Camera = Camera.main;

        if (m_VirtualCamera == null)
        {
            m_VirtualCamera = GetComponentInChildren<CinemachineCamera>();
        }

        if (m_VirtualCamera == null)
        {
            Debug.LogError("[InGameCameraWorker] CinemachineCamera not found!");
            return;
        }

        // CinemachineFollow 컴포넌트 가져오기
        m_Follow = m_VirtualCamera.GetComponent<CinemachineFollow>();
        if (m_Follow == null)
        {
            m_Follow = m_VirtualCamera.gameObject.AddComponent<CinemachineFollow>();
        }

        // CinemachineRotationComposer 컴포넌트 가져오기 (카메라 회전 제어)
        m_RotationComposer = m_VirtualCamera.GetComponent<CinemachineRotationComposer>();
        if (m_RotationComposer == null)
        {
            m_RotationComposer = m_VirtualCamera.gameObject.AddComponent<CinemachineRotationComposer>();
        }

        // 초기 거리 설정
        m_CurrentDistance = DEFAULT_DISTANCE;
        UpdateFollowOffset();
    }

    private void Start()
    {
        // 타겟이 없으면 프록시 생성
        if (m_Target == null)
        {
            m_TargetProxy = new GameObject("CameraTargetProxy");
            m_TargetProxy.transform.position = transform.position;
            m_Target = m_TargetProxy.transform;
        }

        SetTarget(m_Target);
    }

    private void Update()
    {
        HandleZoom();
        HandleBoundaries();
    }

    private void OnDestroy()
    {
        if (m_TargetProxy != null)
        {
            Destroy(m_TargetProxy);
        }
    }

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        if (scrollDelta != 0f)
        {
            m_CurrentDistance -= scrollDelta * m_ZoomSpeed;
            m_CurrentDistance = Mathf.Clamp(m_CurrentDistance, m_MinDistance, m_MaxDistance);
            UpdateFollowOffset();
        }
    }

    private void HandleBoundaries()
    {
        if (!m_UseBoundaries || m_Target == null) return;

        Vector3 targetPos = m_Target.position;
        targetPos.x = Mathf.Clamp(targetPos.x, m_MinBoundary.x, m_MaxBoundary.x);
        targetPos.z = Mathf.Clamp(targetPos.z, m_MinBoundary.y, m_MaxBoundary.y);

        // 타겟이 프록시인 경우에만 위치 제한
        if (m_TargetProxy != null && m_Target == m_TargetProxy.transform)
        {
            m_Target.position = targetPos;
        }
    }

    private void UpdateFollowOffset()
    {
        if (m_Follow == null) return;

        // WorldSpace 바인딩 모드 설정 (타겟 회전에 영향받지 않음)
        m_Follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;

        float angle = 45f * Mathf.Deg2Rad;

        // 수직 높이와 수평 거리 계산
        float height = m_CurrentDistance * Mathf.Sin(angle);
        float horizontalDistance = m_CurrentDistance * Mathf.Cos(angle);

        // 타겟 뒤에서 위를 보는 오프셋 (월드 좌표계)
        m_Follow.FollowOffset = new Vector3(0f, height, -horizontalDistance);

        // Damping 설정 (부드러운 추적)
        m_Follow.TrackerSettings.PositionDamping = new Vector3(1f, 1f, 1f);
    }

    public void SetTarget(Transform _target)
    {
        m_Target = _target;

        if (m_VirtualCamera != null && m_Target != null)
        {
            // TrackingTarget 설정 (Follow 컴포넌트가 위치 추적에 사용)
            m_VirtualCamera.Target.TrackingTarget = m_Target;

            // LookAtTarget 설정 (RotationComposer가 카메라 회전에 사용)
            m_VirtualCamera.Target.LookAtTarget = m_Target;
        }
    }

    public Transform GetTarget()
    {
        return m_Target;
    }

    public Camera GetCamera()
    {
        return m_Camera;
    }

    public CinemachineCamera GetVirtualCamera()
    {
        return m_VirtualCamera;
    }

    public void SetDistance(float _distance)
    {
        m_CurrentDistance = Mathf.Clamp(_distance, m_MinDistance, m_MaxDistance);
        UpdateFollowOffset();
    }

    public void SetBoundaries(Vector2 _min, Vector2 _max)
    {
        m_MinBoundary = _min;
        m_MaxBoundary = _max;
        m_UseBoundaries = true;
    }

    public void SetZoomRange(float _min, float _max)
    {
        m_MinDistance = Mathf.Max(0f, _min);
        m_MaxDistance = Mathf.Max(m_MinDistance, _max);
        m_CurrentDistance = Mathf.Clamp(m_CurrentDistance, m_MinDistance, m_MaxDistance);
        UpdateFollowOffset();
    }
}

