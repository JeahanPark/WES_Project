using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 절제된 1줄 토스트(페이드 인/표시/페이드 아웃). 도면 해금 외에 R4 ②에서
/// 지역 진입 내레이션·이벤트 대사도 같은 위젯을 표시시간만 달리해 재활용한다(director 확정 2026-06-21).
/// 톤: 1줄·건조. 파티클·팡파레·느낌표 금지(기획 §11.1·R4 §7-2).
/// </summary>
public class BlueprintToast : MonoBehaviour
{
    // director 확정 표시시간(2026-06-21): 지역진입 4.0 / 이벤트 3.5 / 도면 통지 3.0초.
    public const float DISPLAY_TIME_AREA_ENTER = 4.0f;
    public const float DISPLAY_TIME_EVENT = 3.5f;
    public const float DISPLAY_TIME_NOTIFY = 3.0f;

    private const float DEFAULT_DISPLAY_TIME = 3.0f;
    private const float FADE_TIME = 0.3f;

    [SerializeField] private CanvasGroup m_CanvasGroup;
    [SerializeField] private TextMeshProUGUI m_MessageText;

    private Coroutine m_ShowCoroutine;

    private void Awake()
    {
        if (m_CanvasGroup != null)
            m_CanvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void ShowMessage(string _message)
    {
        ShowMessage(_message, DEFAULT_DISPLAY_TIME);
    }

    public void ShowMessage(string _message, float _displayTime)
    {
        if (m_MessageText != null)
            m_MessageText.text = _message;

        gameObject.SetActive(true);

        if (m_ShowCoroutine != null)
            StopCoroutine(m_ShowCoroutine);
        m_ShowCoroutine = StartCoroutine(CoShow(_displayTime));
    }

    private IEnumerator CoShow(float _displayTime)
    {
        yield return CoFade(0f, 1f, FADE_TIME);

        yield return new WaitForSeconds(_displayTime);

        yield return CoFade(1f, 0f, FADE_TIME);

        gameObject.SetActive(false);
        m_ShowCoroutine = null;
    }

    private IEnumerator CoFade(float _from, float _to, float _duration)
    {
        if (m_CanvasGroup == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            m_CanvasGroup.alpha = Mathf.Lerp(_from, _to, elapsed / _duration);
            yield return null;
        }
        m_CanvasGroup.alpha = _to;
    }
}
