using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 도면 해금 알림 토스트. "○○ 도면을 익혔다" 1줄을 페이드 인/표시/페이드 아웃으로 보여준다.
/// 톤: 절제된 1줄. 파티클·팡파레 금지(기획 §11.1).
/// </summary>
public class BlueprintToast : MonoBehaviour
{
    private const float DISPLAY_TIME = 3.0f;
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
        if (m_MessageText != null)
            m_MessageText.text = _message;

        gameObject.SetActive(true);

        if (m_ShowCoroutine != null)
            StopCoroutine(m_ShowCoroutine);
        m_ShowCoroutine = StartCoroutine(CoShow());
    }

    private IEnumerator CoShow()
    {
        yield return CoFade(0f, 1f, FADE_TIME);

        yield return new WaitForSeconds(DISPLAY_TIME);

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
