using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 머리 위 체력바 WorldUI
/// </summary>
public class CharacterWorldUI : BaseWorldUI
{
    private const float ANIMATION_SPEED = 5f;

    [SerializeField] private Image m_HPBarFill;

    private Transform m_TargetTransform;
    private Camera m_Camera;
    private CharacterBase m_Character;
    private float m_TargetFillAmount;
    private float m_CurrentFillAmount;

    public void SetTarget(Transform _target, Camera _camera)
    {
        m_TargetTransform = _target;
        m_Camera = _camera;
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
            m_TargetFillAmount = ratio;
            m_CurrentFillAmount = ratio;
            UpdateFillImmediate();
        }
    }

    protected override void OnRelease()
    {
        if (m_Character != null)
        {
            m_Character.UnsubscribeOnHPChanged(OnHPChanged);
            m_Character.UnsubscribeOnDeath(OnDeath);
            m_Character = null;
        }

        m_TargetTransform = null;
    }

    private void Update()
    {
        if (!IsActive)
            return;

        if (!Mathf.Approximately(m_CurrentFillAmount, m_TargetFillAmount))
        {
            m_CurrentFillAmount = Mathf.Lerp(m_CurrentFillAmount, m_TargetFillAmount, Time.deltaTime * ANIMATION_SPEED);
            UpdateFillImmediate();
        }
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    private void OnHPChanged(int _currentHP, int _maxHP)
    {
        m_TargetFillAmount = _maxHP > 0 ? (float)_currentHP / _maxHP : 0f;
    }

    private void OnDeath()
    {
        m_TargetFillAmount = 0f;
    }

    private void UpdatePosition()
    {
        if (!IsActive || m_TargetTransform == null || m_Camera == null)
            return;

        Vector3 worldPosition = m_TargetTransform.position;
        Vector3 screenPosition = m_Camera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z < 0f)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        m_RectTransform.position = screenPosition;
    }

    private void UpdateFillImmediate()
    {
        if (m_HPBarFill != null)
        {
            m_HPBarFill.fillAmount = m_CurrentFillAmount;
        }
    }
}
