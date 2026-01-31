using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 머리 위 체력바 WorldUI
/// </summary>
public class CharacterWorldUI : BaseWorldUI
{
    private const float ANIMATION_DURATION = 0.2f;

    [SerializeField] private Image m_HPBarImage;

    private Transform m_TargetTransform;
    private Camera m_Camera;
    private Camera m_UICamera;
    private RectTransform m_CanvasRectTransform;
    private CharacterBase m_Character;
    private Coroutine m_AnimationCoroutine;

    public void SetTarget(Transform _target, Camera _camera, Camera _uiCamera, RectTransform _canvasRectTransform)
    {
        m_TargetTransform = _target;
        m_Camera = _camera;
        m_UICamera = _uiCamera;
        m_CanvasRectTransform = _canvasRectTransform;
    }

    public void SetCharacter(CharacterBase _character)
    {
        if (m_Character != null)
        {
            m_Character.UnsubscribeOnHPChanged(OnHPChanged);
            m_Character.UnsubscribeOnDeath(OnDeath);
        }

        m_Character = _character;

        if (m_Character != null)
        {
            m_Character.SubscribeOnHPChanged(OnHPChanged);
            m_Character.SubscribeOnDeath(OnDeath);

            float ratio = m_Character.MaxHP > 0 ? (float)m_Character.HP / m_Character.MaxHP : 0f;
            SetBarImmediate(ratio);
        }
    }

    protected override void OnRelease()
    {
        StopAnimation();

        if (m_Character != null)
        {
            m_Character.UnsubscribeOnHPChanged(OnHPChanged);
            m_Character.UnsubscribeOnDeath(OnDeath);
            m_Character = null;
        }

        m_TargetTransform = null;
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    private void OnHPChanged(int _currentHP, int _maxHP)
    {
        float targetRatio = _maxHP > 0 ? (float)_currentHP / _maxHP : 0f;
        StartAnimation(targetRatio);
    }

    private void OnDeath()
    {
        StartAnimation(0f);
    }

    private void StartAnimation(float _targetRatio)
    {
        StopAnimation();
        m_AnimationCoroutine = StartCoroutine(CoAnimateBar(_targetRatio));
    }

    private void StopAnimation()
    {
        if (m_AnimationCoroutine != null)
        {
            StopCoroutine(m_AnimationCoroutine);
            m_AnimationCoroutine = null;
        }
    }

    private IEnumerator CoAnimateBar(float _targetRatio)
    {
        if (m_HPBarImage == null)
            yield break;

        float startRatio = m_HPBarImage.rectTransform.anchorMax.x;
        float elapsed = 0f;

        while (elapsed < ANIMATION_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / ANIMATION_DURATION;

            float currentRatio = Mathf.Lerp(startRatio, _targetRatio, t);
            Vector2 anchorMax = m_HPBarImage.rectTransform.anchorMax;
            anchorMax.x = currentRatio;
            m_HPBarImage.rectTransform.anchorMax = anchorMax;

            yield return null;
        }

        Vector2 finalAnchorMax = m_HPBarImage.rectTransform.anchorMax;
        finalAnchorMax.x = _targetRatio;
        m_HPBarImage.rectTransform.anchorMax = finalAnchorMax;
        m_AnimationCoroutine = null;
    }

    private void SetBarImmediate(float _ratio)
    {
        StopAnimation();

        if (m_HPBarImage != null)
        {
            Vector2 anchorMax = m_HPBarImage.rectTransform.anchorMax;
            anchorMax.x = _ratio;
            m_HPBarImage.rectTransform.anchorMax = anchorMax;
        }
    }

    private void UpdatePosition()
    {
        if (!IsActive || m_TargetTransform == null || m_Camera == null || m_CanvasRectTransform == null)
            return;

        Vector3 offset = m_Character != null ? m_Character.WorldUIOffset : Vector3.zero;
        Vector3 worldPosition = m_TargetTransform.position + offset;
        Vector3 screenPosition = m_Camera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z < 0f)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_CanvasRectTransform,
            screenPosition,
            m_UICamera,
            out Vector2 localPoint);

        m_RectTransform.localPosition = localPoint;
    }
}
