using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NightVisionComponent : MonoBehaviour
{
    private const int CAMPFIRE_BUILDING_ID = 1;
    private const int TORCH_BUILDING_ID = 2;
    private const int MAX_LIGHT_SOURCES = 16;
    private const string SHADER_CIRCLE_COUNT = "_CircleCount";
    private const string SHADER_CIRCLES = "_Circles";

    [SerializeField] private DayNightConfig m_Config;
    [SerializeField] private Image m_DarknessOverlay;

    private Camera m_Camera;
    private DayPhase m_CurrentPhase = DayPhase.Day;
    private Coroutine m_AlphaCoroutine;
    private Material m_OverlayMaterial;
    private readonly Vector4[] m_CircleBuffer = new Vector4[MAX_LIGHT_SOURCES];

    private void OnEnable()
    {
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
    }

    private void Start()
    {
        m_Camera = Camera.main;
        InitializeOverlayMaterial();
        ApplyPhase(DayPhase.Day, _instant: true);
    }

    private void OnDestroy()
    {
        if (m_OverlayMaterial != null)
            Destroy(m_OverlayMaterial);
    }

    public void Initialize(Camera _camera)
    {
        m_Camera = _camera;
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _current)
    {
        m_CurrentPhase = _current;
        ApplyPhase(_current, _instant: false);
    }

    private void ApplyPhase(DayPhase _phase, bool _instant)
    {
        if (m_Config == null)
            return;

        float targetAlpha = m_Config.GetNightOverlayAlpha(_phase);

        if (_instant)
        {
            SetOverlayAlpha(targetAlpha);
            return;
        }

        if (m_AlphaCoroutine != null)
            StopCoroutine(m_AlphaCoroutine);

        m_AlphaCoroutine = StartCoroutine(CoLerpOverlayAlpha(targetAlpha));
    }

    private IEnumerator CoLerpOverlayAlpha(float _targetAlpha)
    {
        if (m_DarknessOverlay == null)
            yield break;

        float startAlpha = m_DarknessOverlay.color.a;
        float duration = m_Config != null ? m_Config.TransitionDuration : 5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetOverlayAlpha(Mathf.Lerp(startAlpha, _targetAlpha, t));
            yield return null;
        }

        SetOverlayAlpha(_targetAlpha);
        m_AlphaCoroutine = null;
    }

    private void LateUpdate()
    {
        if (m_CurrentPhase == DayPhase.Day)
        {
            ClearShaderCircles();
            return;
        }

        UpdateLightSources();
    }

    private void InitializeOverlayMaterial()
    {
        if (m_DarknessOverlay == null)
            return;

        var shader = Shader.Find("WES/NightDarknessOverlay");
        if (shader == null)
        {
            Debug.LogWarning("[NightVisionComponent] Shader 'WES/NightDarknessOverlay' not found. Cutout disabled.");
            return;
        }

        m_OverlayMaterial = new Material(shader);
        m_DarknessOverlay.material = m_OverlayMaterial;
    }

    private void UpdateLightSources()
    {
        if (m_Camera == null || m_Config == null || m_OverlayMaterial == null)
            return;

        var buildings = WorldBuildingObject.ActiveBuildings;
        int circleIndex = 0;

        foreach (var building in buildings)
        {
            if (circleIndex >= MAX_LIGHT_SOURCES)
                break;

            int id = building.BuildingInfoId;
            if (id != CAMPFIRE_BUILDING_ID && id != TORCH_BUILDING_ID)
                continue;

            float worldRadius = id == CAMPFIRE_BUILDING_ID
                ? m_Config.CampfireLightRadius
                : m_Config.TorchLightRadius;

            Vector3 screenPos = m_Camera.WorldToScreenPoint(building.transform.position);
            if (screenPos.z < 0f)
                continue;

            float screenRadiusPx = WorldRadiusToScreenRadius(worldRadius, building.transform.position);

            // 셰이더는 UV 좌표(0~1) 기준으로 원을 받음
            // x는 aspect 보정: 원이 타원이 되지 않도록
            float uvX = screenPos.x / Screen.width;
            float uvY = screenPos.y / Screen.height;
            float uvRadius = screenRadiusPx / Screen.height;
            float aspect = (float)Screen.width / Screen.height;

            // aspect를 w에 저장하여 셰이더에서 거리 계산에 사용
            m_CircleBuffer[circleIndex] = new Vector4(uvX, uvY, uvRadius, aspect);
            circleIndex++;
        }

        m_OverlayMaterial.SetInt(SHADER_CIRCLE_COUNT, circleIndex);
        m_OverlayMaterial.SetVectorArray(SHADER_CIRCLES, m_CircleBuffer);
    }

    private void ClearShaderCircles()
    {
        if (m_OverlayMaterial == null)
            return;

        m_OverlayMaterial.SetInt(SHADER_CIRCLE_COUNT, 0);
    }

    private float WorldRadiusToScreenRadius(float _worldRadius, Vector3 _worldCenter)
    {
        Vector3 edgeWorld = _worldCenter + m_Camera.transform.right * _worldRadius;
        Vector3 centerScreen = m_Camera.WorldToScreenPoint(_worldCenter);
        Vector3 edgeScreen = m_Camera.WorldToScreenPoint(edgeWorld);
        return Vector3.Distance(centerScreen, edgeScreen);
    }

    private void SetOverlayAlpha(float _alpha)
    {
        if (m_DarknessOverlay == null)
            return;

        Color c = m_DarknessOverlay.color;
        c.a = _alpha;
        m_DarknessOverlay.color = c;
    }
}
