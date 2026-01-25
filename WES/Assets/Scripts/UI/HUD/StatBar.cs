using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatBar : MonoBehaviour
{
    private const float ANIMATION_DURATION = 0.2f;

    [SerializeField] private CharacterStat m_StatType;
    [SerializeField] private Image m_BarImage;
    [SerializeField] private TMP_Text m_Text;

    private Coroutine m_AnimationCoroutine;
    private int m_CurrentDisplayValue;

    public CharacterStat GetCharacterStat => m_StatType;

    public void UpdateValue(int _current, int _max)
    {
        int startValue = m_CurrentDisplayValue;

        if (m_AnimationCoroutine != null)
        {
            StopCoroutine(m_AnimationCoroutine);
        }

        m_AnimationCoroutine = StartCoroutine(CoAnimate(startValue, _current, _max));
    }

    private IEnumerator CoAnimate(int _startValue, int _targetValue, int _max)
    {
        float startRatio = m_BarImage != null ? m_BarImage.rectTransform.anchorMax.x : 0f;
        float targetRatio = _max > 0 ? (float)_targetValue / _max : 0f;
        float elapsedTime = 0f;

        while (elapsedTime < ANIMATION_DURATION)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / ANIMATION_DURATION;

            // Bar 애니메이션
            if (m_BarImage != null)
            {
                float currentRatio = Mathf.Lerp(startRatio, targetRatio, t);
                var anchorMax = m_BarImage.rectTransform.anchorMax;
                anchorMax.x = currentRatio;
                m_BarImage.rectTransform.anchorMax = anchorMax;
            }

            // Text 애니메이션
            if (m_Text != null)
            {
                m_CurrentDisplayValue = Mathf.RoundToInt(Mathf.Lerp(_startValue, _targetValue, t));
                m_Text.text = $"{m_CurrentDisplayValue} / {_max}";
            }

            yield return null;
        }

        // 최종값 설정
        if (m_BarImage != null)
        {
            var finalAnchorMax = m_BarImage.rectTransform.anchorMax;
            finalAnchorMax.x = targetRatio;
            m_BarImage.rectTransform.anchorMax = finalAnchorMax;
        }

        if (m_Text != null)
        {
            m_CurrentDisplayValue = _targetValue;
            m_Text.text = $"{_targetValue} / {_max}";
        }

        m_AnimationCoroutine = null;
    }
}
