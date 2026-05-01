using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 피격 시 머리 위에 떠오르는 데미지 숫자
/// </summary>
public class DamageNumberWorldUI : BaseWorldUI
{
    private const float LIFETIME = 0.6f;
    private const float RISE_DISTANCE = 40f;
    private const float FADE_START_RATIO = 0.667f;

    [SerializeField] private TextMeshProUGUI m_DamageText;
    [SerializeField] private CanvasGroup m_CanvasGroup;

    private Vector3 m_WorldPosition;
    private Vector2 m_ScreenOffset;
    private Camera m_Camera;
    private Camera m_UICamera;
    private RectTransform m_CanvasRectTransform;
    private Coroutine m_AnimationCoroutine;

    public void SetData(
        int _damage,
        Vector3 _worldPosition,
        Vector2 _screenOffset,
        Camera _camera,
        Camera _uiCamera,
        RectTransform _canvasRect,
        Color _textColor)
    {
        m_WorldPosition = _worldPosition;
        m_ScreenOffset = _screenOffset;
        m_Camera = _camera;
        m_UICamera = _uiCamera;
        m_CanvasRectTransform = _canvasRect;

        if (m_DamageText != null)
        {
            m_DamageText.text = _damage.ToString();
            m_DamageText.color = _textColor;
        }

        if (m_CanvasGroup != null)
            m_CanvasGroup.alpha = 1f;

        StopAnimation();
        m_AnimationCoroutine = StartCoroutine(CoPlayAnimation());
    }

    protected override void OnRelease()
    {
        StopAnimation();
        m_Camera = null;
        m_UICamera = null;
        m_CanvasRectTransform = null;
    }

    private void LateUpdate()
    {
        UpdatePosition(0f);
    }

    private IEnumerator CoPlayAnimation()
    {
        float elapsed = 0f;

        while (elapsed < LIFETIME)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LIFETIME);
            float eased = 1f - (1f - t) * (1f - t); // EaseOutQuad
            float riseY = eased * RISE_DISTANCE;

            UpdatePosition(riseY);

            if (m_CanvasGroup != null)
            {
                if (t >= FADE_START_RATIO)
                {
                    float fadeT = (t - FADE_START_RATIO) / (1f - FADE_START_RATIO);
                    m_CanvasGroup.alpha = 1f - fadeT;
                }
                else
                {
                    m_CanvasGroup.alpha = 1f;
                }
            }

            yield return null;
        }

        m_AnimationCoroutine = null;

        if (InGameController.Instance != null && InGameController.Instance.WorldUIWorker != null)
            InGameController.Instance.WorldUIWorker.ReleaseWorldUI(this);
    }

    private void StopAnimation()
    {
        if (m_AnimationCoroutine != null)
        {
            StopCoroutine(m_AnimationCoroutine);
            m_AnimationCoroutine = null;
        }
    }

    private void UpdatePosition(float _riseY)
    {
        if (!IsActive || m_Camera == null || m_CanvasRectTransform == null)
            return;

        Vector3 screenPosition = m_Camera.WorldToScreenPoint(m_WorldPosition);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_CanvasRectTransform,
            screenPosition,
            m_UICamera,
            out Vector2 localPoint);

        localPoint += m_ScreenOffset;
        localPoint.y += _riseY;

        m_RectTransform.localPosition = localPoint;
    }
}
