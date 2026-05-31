using System.Collections;
using UnityEngine;

public class DayNightRenderWorker : MonoBehaviour
{
    [SerializeField] private DayNightConfig m_Config;
    [SerializeField] private Light m_DirectionalLight;

    private Coroutine m_TransitionCoroutine;

    private void OnEnable()
    {
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _current)
    {
        if (m_Config == null)
            return;

        if (m_TransitionCoroutine != null)
            StopCoroutine(m_TransitionCoroutine);

        m_TransitionCoroutine = StartCoroutine(CoTransitionToPhase(_current));

        // TODO: 사운드 에셋 추가 시 활성화
        // PlayPhaseEnterSound(_current);
    }

    private IEnumerator CoTransitionToPhase(DayPhase _phase)
    {
        if (m_Config == null)
            yield break;

        Color startAmbient = RenderSettings.ambientLight;
        Color targetAmbient = m_Config.GetAmbientColor(_phase);

        float startLightIntensity = m_DirectionalLight != null ? m_DirectionalLight.intensity : 1f;
        float targetLightIntensity = m_Config.GetLightIntensity(_phase);

        float elapsed = 0f;
        float duration = m_Config.TransitionDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, t);

            if (m_DirectionalLight != null)
                m_DirectionalLight.intensity = Mathf.Lerp(startLightIntensity, targetLightIntensity, t);

            yield return null;
        }

        RenderSettings.ambientLight = targetAmbient;
        if (m_DirectionalLight != null)
            m_DirectionalLight.intensity = targetLightIntensity;

        m_TransitionCoroutine = null;
    }
}
