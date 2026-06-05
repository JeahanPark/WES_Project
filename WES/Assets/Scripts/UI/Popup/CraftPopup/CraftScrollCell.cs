using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftScrollCell : BaseScrollCell<CraftInfo>
{
    private const float UNLOCK_FLASH_FADE = 0.35f;   // 해금 시 잠금 오버레이 페이드아웃 시간
    private const float UNLOCK_FLASH_PULSE = 0.55f;  // 테두리 반짝 1회 길이

    private static readonly Color SELECTED_COLOR = new Color(0.5f, 0.35f, 0.18f, 1f);
    private static readonly Color NORMAL_COLOR = new Color(0.18f, 0.16f, 0.14f, 1f);

    [SerializeField] private TextMeshProUGUI m_NameText;
    [SerializeField] private Image m_IconImage;
    [SerializeField] private Button m_Button;
    [SerializeField] private GameObject m_SelectedFrame;
    [SerializeField] private Image m_BackgroundImage;
    [SerializeField] private GameObject m_LockOverlay;
    [SerializeField] private Image m_UnlockFlashFrame; // 해금 순간 테두리 반짝(선택, designer 제공). 없으면 페이드만.

    private CraftInfo m_CraftInfo;
    private Coroutine m_UnlockFlashCoroutine;

    public CraftInfo CraftInfo => m_CraftInfo;

    private void Awake()
    {
        m_Button.onClick.AddListener(OnClickCell);

        if (m_UnlockFlashFrame != null)
            SetGraphicAlpha(m_UnlockFlashFrame, 0f);
    }

    protected override void OnUpdateCell(int _index, CraftInfo _data)
    {
        m_CraftInfo = _data;

        // 셀 재활용(스크롤) 시 이전 셀의 반짝 연출을 정리한다.
        if (m_UnlockFlashCoroutine != null)
        {
            StopCoroutine(m_UnlockFlashCoroutine);
            m_UnlockFlashCoroutine = null;
        }
        if (m_UnlockFlashFrame != null)
            SetGraphicAlpha(m_UnlockFlashFrame, 0f);

        if (m_CraftInfo == null)
        {
            SetEmpty();
            return;
        }

        if (m_NameText != null)
            m_NameText.text = m_CraftInfo.Name;

        if (m_IconImage != null)
        {
            string iconKey = m_CraftInfo.IconKey;
            m_IconImage.sprite = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }

        SetSelected(false);
        RefreshLockState();
    }

    public void SetSelected(bool _selected)
    {
        if (m_SelectedFrame != null)
            m_SelectedFrame.SetActive(_selected);
        if (m_BackgroundImage != null)
            m_BackgroundImage.color = _selected ? SELECTED_COLOR : NORMAL_COLOR;
    }

    // 도면 해금 상태에 따라 잠금 오버레이를 갱신한다. 목록 재배치는 하지 않는다.
    // 해금 반짝 코루틴이 진행 중인 셀은 코루틴이 잠금 오버레이를 전담하므로 즉시 토글하지 않는다.
    public void RefreshLockState()
    {
        if (m_UnlockFlashCoroutine != null)
            return;

        SetLocked(IsLocked());
    }

    public void SetLocked(bool _locked)
    {
        if (m_LockOverlay != null)
            m_LockOverlay.SetActive(_locked);
    }

    // 현재 셀의 레시피가 도면 잠금 상태인지 판정.
    public bool IsLocked()
    {
        if (m_CraftInfo == null)
            return false;

        if (!Managers.Info.IsBlueprintLockedCraft(m_CraftInfo.Id))
            return false;

        var registry = InGameController.Instance?.ObjectDataWorker?.GetRecipeUnlockRegistry();
        if (registry == null)
            return true;

        return !registry.IsUnlocked(m_CraftInfo.Id);
    }

    // 해당 셀이 방금 해금됐을 때: 잠금 오버레이를 페이드아웃하고 테두리를 1회 반짝인다(목록 재배치 없음).
    // 팝업이 닫혀 있어 셀이 비활성이면 코루틴 미실행 → RefreshLockState로 정상색만 표시(반짝 생략).
    public void PlayUnlockFlash()
    {
        if (!isActiveAndEnabled)
        {
            RefreshLockState();
            return;
        }

        if (m_UnlockFlashCoroutine != null)
            StopCoroutine(m_UnlockFlashCoroutine);
        m_UnlockFlashCoroutine = StartCoroutine(CoPlayUnlockFlash());
    }

    private IEnumerator CoPlayUnlockFlash()
    {
        // 1) 잠금 오버레이 페이드아웃(없으면 즉시 비활성).
        yield return CoFadeOutLockOverlay();
        SetLocked(false);

        // 2) 테두리 1회 반짝(프레임 미연결 시 스킵 — 양보가능 미세강조).
        if (m_UnlockFlashFrame != null)
            yield return CoPulseFlashFrame();

        m_UnlockFlashCoroutine = null;
    }

    private IEnumerator CoFadeOutLockOverlay()
    {
        var overlayGraphic = m_LockOverlay != null ? m_LockOverlay.GetComponent<Graphic>() : null;
        if (overlayGraphic == null)
            yield break;

        float from = overlayGraphic.color.a;
        float elapsed = 0f;
        while (elapsed < UNLOCK_FLASH_FADE)
        {
            elapsed += Time.deltaTime;
            SetGraphicAlpha(overlayGraphic, Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / UNLOCK_FLASH_FADE)));
            yield return null;
        }
        SetGraphicAlpha(overlayGraphic, 0f);
    }

    private IEnumerator CoPulseFlashFrame()
    {
        float elapsed = 0f;
        while (elapsed < UNLOCK_FLASH_PULSE)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / UNLOCK_FLASH_PULSE);
            // 0→1→0 외형(빠르게 떴다 천천히 가라앉음).
            float alpha = Mathf.Sin(t * Mathf.PI);
            SetGraphicAlpha(m_UnlockFlashFrame, alpha);
            yield return null;
        }
        SetGraphicAlpha(m_UnlockFlashFrame, 0f);
    }

    private static void SetGraphicAlpha(Graphic _graphic, float _alpha)
    {
        if (_graphic == null)
            return;

        Color c = _graphic.color;
        c.a = _alpha;
        _graphic.color = c;
    }

    private void SetEmpty()
    {
        if (m_NameText != null)
            m_NameText.text = string.Empty;

        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }

        SetSelected(false);
        SetLocked(false);
    }

    private void OnClickCell()
    {
        if (m_CraftInfo == null)
            return;

        var scroll = GetComponentInParent<CraftScroll>(true);
        scroll?.NotifyCellClicked(m_CraftInfo, this);
    }
}
