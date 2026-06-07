using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코어텐션 풀스크린 오버레이 구동 Worker (클라이언트 로컬 연출 전용).
/// 리소스리스트 G-2(추위)/G-3(낮밤)/G-13(저체력 비네팅)/G-14(사망)/I-5(앰비언트 안개) 와이어링.
///
/// 모든 오버레이는 로컬 플레이어의 이미 동기화된 상태(Cold/HP NetworkVariable)와
/// DayNightWorker.OnPhaseChanged(서버 권한, 전원 동기화) 이벤트를 클라이언트가 읽어
/// 화면 연출만 한다. 신규 NetworkVariable/Rpc 없음.
///
/// 텍스처 소스: Assets/GameResource/UI/CoreTension/ (CoreTensionTextureSetup이 생성).
/// 실제 sprite/GameObject 슬롯 연결은 designer-b가 프리팹에서 수행.
/// </summary>
public class CoreTensionOverlayWorker : MonoBehaviour
{
    private const float COLD_FADE_DURATION = 0.6f;       // 추위 단계 전환 페이드
    private const float DAYNIGHT_FADE_DURATION = 1.5f;   // 낮밤 톤 크로스페이드
    private const float DEATH_FADE_DURATION = 1.8f;      // 사망 암전 페이드
    private const float VIGNETTE_PULSE_PERIOD = 1.1f;    // 저체력 적색 비네팅 심박 주기
    private const float FOG_SCROLL_SPEED = 0.015f;       // 앰비언트 안개 가로 스크롤(UV/sec)
    private const int VIGNETTE_HP_THRESHOLD = 30;        // 저체력 비네팅 발동 HP 임계(퍼센트)
    private const float COLD_OVERLAY_MAX_ALPHA = 0.85f;  // 추위 오버레이 완전차폐 금지 천장(공정성: 중앙 시야 여백 보증). director 결정 2026-06-06.

    // 임계 단일소스 폴백값(m_Config 미연결 시에만 사용). 정상 운용은 DayNightConfig 참조.
    private const int FALLBACK_COLD_STAGE_WARNING = 30;
    private const int FALLBACK_COLD_STAGE_WEAK = 60;
    private const int FALLBACK_COLD_STAGE_STRONG = 90;

    // ── G-2 추위 오버레이 (3단계: Warning / WeakDot / StrongDot) ──
    // 단계 임계는 DayNightConfig 단일소스(서버 ColdDamageWorker와 동일 SO). director 결정 2026-06-06.
    [Header("Cold Overlay (G-2)")]
    [SerializeField] private DayNightConfig m_Config; // 서버 ColdDamageWorker와 동일 SO 연결(단일소스)
    [SerializeField] private Image m_ColdOverlay1; // cold_overlay_1 (Warning)
    [SerializeField] private Image m_ColdOverlay2; // cold_overlay_2 (WeakDot)
    [SerializeField] private Image m_ColdOverlay3; // cold_overlay_3 (StrongDot, 펄스)
    [SerializeField] private float m_ColdStage3PulseAmplitude = 0.15f;
    [SerializeField] private float m_ColdStage3PulsePeriod = 1.4f;

    // ── G-13 저체력 적색 비네팅 (전투 채널, 추위와 분리) ──
    [Header("HP Vignette (G-13)")]
    [SerializeField] private Image m_HpVignette; // vignette_red

    // ── G-14 사망 오버레이 (암전 페이드) ──
    [Header("Death Overlay (G-14)")]
    [SerializeField] private Image m_DeathOverlay; // death_overlay

    // ── G-3 낮밤 톤 (글로벌 화면 틴트, daynight_gradient U샘플) ──
    // 단일 레이어(m_DayNightTint)의 색·알파만 페이즈별로 크로스페이드 → 주/야 톤이 합산 겹칠 수 없다(배타 자동 보장).
    // 알파를 페이즈별로 분리해 designer가 "차갑고 흐린 낮"(D-3) 등을 페이즈마다 독립 튜닝한다.
    [Header("DayNight Tint (G-3)")]
    [SerializeField] private Image m_DayNightTint;            // daynight_gradient 단색 틴트 레이어
    [SerializeField] private Color m_TintDay = new Color(0.49f, 0.54f, 0.59f);
    [SerializeField] private Color m_TintDusk = new Color(0.60f, 0.42f, 0.32f);
    [SerializeField] private Color m_TintNight = new Color(0.12f, 0.15f, 0.25f);
    [SerializeField] private Color m_TintDawn = new Color(0.56f, 0.65f, 0.72f);
    // 페이즈별 틴트 알파(기존 단일값 0.18 유지가 기본 → 회귀 0). designer는 주로 m_TintAlphaDay를 튜닝한다(D-3).
    [SerializeField, Range(0f, 1f)] private float m_TintAlphaDay = 0.18f;
    [SerializeField, Range(0f, 1f)] private float m_TintAlphaDusk = 0.18f;
    [SerializeField, Range(0f, 1f)] private float m_TintAlphaNight = 0.18f;
    [SerializeField, Range(0f, 1f)] private float m_TintAlphaDawn = 0.18f;

    // ── I-5 앰비언트 안개 (가로 스크롤 타일) ──
    [Header("Ambient Fog (I-5)")]
    [SerializeField] private RawImage m_AmbientFog; // ambient_fog (Repeat, uvRect 스크롤)

    private PlayerCharacter m_LocalPlayer;
    private ColdStage m_CurrentColdStage = ColdStage.None;
    private bool m_HpVignetteActive;
    private bool m_DeathTriggered;
    private float m_FogScrollOffset;

    private Coroutine m_ColdFadeCoroutine;
    private Coroutine m_DayNightFadeCoroutine;
    private Coroutine m_DeathFadeCoroutine;

    private void OnEnable()
    {
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
        UnsubscribePlayer();
    }

    private void Update()
    {
        UpdateFogScroll();
        UpdateColdStage3Pulse();
        UpdateHpVignettePulse();
    }

    // ── public 계약 (InGameHUDWorker가 호출) ────────────────────

    public void SetLocalPlayer(PlayerCharacter _player)
    {
        UnsubscribePlayer();
        m_LocalPlayer = _player;

        if (m_LocalPlayer == null)
            return;

        m_LocalPlayer.SubscribeOnColdChanged(OnColdChanged);
        m_LocalPlayer.SubscribeOnHPChanged(OnHPChanged);

        // 초기 상태 즉시 반영
        OnColdChanged(m_LocalPlayer.Cold, m_LocalPlayer.MaxCold);
        OnHPChanged(m_LocalPlayer.HP, m_LocalPlayer.MaxHP);
        ApplyDayNightTint(GetCurrentPhase(), _instant: true);
    }

    public void ClearLocalPlayer()
    {
        UnsubscribePlayer();
        m_LocalPlayer = null;
    }

    /// <summary>
    /// 전멸(GameOver) 확정 시 InGameController가 호출. 1.8초 carbon 암전 페이드 후
    /// _onComplete(=ResultPopup 표시)를 호출한다(BgDefeat 전환 연출).
    /// 이미 암전 진행/완료 중이면 콜백만 보장한다(중복 트리거 방지).
    /// </summary>
    public void PlayDeathFade(System.Action _onComplete)
    {
        if (m_DeathTriggered)
        {
            // 이미 암전 중/완료 — 새 페이드 시작하지 않고 콜백만 즉시 보장
            _onComplete?.Invoke();
            return;
        }

        StartCoroutine(CoPlayDeathFade(_onComplete));
    }

    // ── private 처리 ────────────────────────────────────────────

    private void UnsubscribePlayer()
    {
        if (m_LocalPlayer == null)
            return;

        m_LocalPlayer.UnsubscribeOnColdChanged(OnColdChanged);
        m_LocalPlayer.UnsubscribeOnHPChanged(OnHPChanged);
    }

    private void OnColdChanged(int _cold, int _maxCold)
    {
        ColdStage stage = EvaluateColdStage(_cold);
        if (stage == m_CurrentColdStage)
            return;

        m_CurrentColdStage = stage;
        ApplyColdStage(stage);
    }

    private ColdStage EvaluateColdStage(int _cold)
    {
        // 임계는 DayNightConfig 단일소스(서버 데미지와 동일). 미연결 시 폴백 상수.
        int warning = m_Config != null ? m_Config.ColdStageWarning : FALLBACK_COLD_STAGE_WARNING;
        int weak = m_Config != null ? m_Config.ColdStageWeak : FALLBACK_COLD_STAGE_WEAK;
        int strong = m_Config != null ? m_Config.ColdStageStrong : FALLBACK_COLD_STAGE_STRONG;

        if (_cold >= strong)
            return ColdStage.StrongDot;
        if (_cold >= weak)
            return ColdStage.WeakDot;
        if (_cold >= warning)
            return ColdStage.Warning;
        return ColdStage.None;
    }

    private void ApplyColdStage(ColdStage _stage)
    {
        // 완전차폐 금지: 활성 단계 목표 알파를 COLD_OVERLAY_MAX_ALPHA로 캡(공정성 천장).
        float a1 = _stage >= ColdStage.Warning ? COLD_OVERLAY_MAX_ALPHA : 0f;
        float a2 = _stage >= ColdStage.WeakDot ? COLD_OVERLAY_MAX_ALPHA : 0f;
        float a3 = _stage >= ColdStage.StrongDot ? COLD_OVERLAY_MAX_ALPHA : 0f;

        if (m_ColdFadeCoroutine != null)
            StopCoroutine(m_ColdFadeCoroutine);
        m_ColdFadeCoroutine = StartCoroutine(CoFadeColdStage(a1, a2, a3));
    }

    private IEnumerator CoFadeColdStage(float _t1, float _t2, float _t3)
    {
        float s1 = GetAlpha(m_ColdOverlay1);
        float s2 = GetAlpha(m_ColdOverlay2);
        float s3 = GetAlpha(m_ColdOverlay3);
        float elapsed = 0f;

        while (elapsed < COLD_FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / COLD_FADE_DURATION);
            SetAlpha(m_ColdOverlay1, Mathf.Lerp(s1, _t1, t));
            SetAlpha(m_ColdOverlay2, Mathf.Lerp(s2, _t2, t));
            SetAlpha(m_ColdOverlay3, Mathf.Lerp(s3, _t3, t));
            yield return null;
        }

        SetAlpha(m_ColdOverlay1, _t1);
        SetAlpha(m_ColdOverlay2, _t2);
        SetAlpha(m_ColdOverlay3, _t3);
        m_ColdFadeCoroutine = null;
    }

    private void UpdateColdStage3Pulse()
    {
        if (m_ColdOverlay3 == null || m_CurrentColdStage < ColdStage.StrongDot)
            return;
        if (m_ColdFadeCoroutine != null)
            return; // 페이드 중에는 펄스 미적용

        // 펄스도 완전차폐 금지 천장(COLD_OVERLAY_MAX_ALPHA) 안에서 진동. 진폭은 천장 아래로 흔든다.
        float pulse = COLD_OVERLAY_MAX_ALPHA + Mathf.Sin(Time.time * Mathf.PI * 2f / m_ColdStage3PulsePeriod) * m_ColdStage3PulseAmplitude;
        SetAlpha(m_ColdOverlay3, Mathf.Clamp(pulse, 0f, COLD_OVERLAY_MAX_ALPHA));
    }

    private void OnHPChanged(int _hp, int _maxHP)
    {
        // G-14 사망 암전은 개인 HP=0 자동발화가 아니라 전멸(GameOver) 확정 시
        // InGameController가 PlayDeathFade로 트리거한다(BgDefeat 전환 연출, director 결정 2026-06-05).
        // 여기서는 저체력 적색 비네팅(G-13)만 처리.
        if (_hp <= 0)
        {
            m_HpVignetteActive = false;
            SetAlpha(m_HpVignette, 0f);
            return;
        }

        // G-13 저체력 적색 비네팅: HP 비율 임계 이하면 활성(펄스는 Update)
        float ratio = _maxHP > 0 ? (float)_hp / _maxHP * 100f : 100f;
        m_HpVignetteActive = ratio <= VIGNETTE_HP_THRESHOLD;
        if (!m_HpVignetteActive)
            SetAlpha(m_HpVignette, 0f);
    }

    private void UpdateHpVignettePulse()
    {
        if (m_HpVignette == null || !m_HpVignetteActive)
            return;

        float pulse = (Mathf.Sin(Time.time * Mathf.PI * 2f / VIGNETTE_PULSE_PERIOD) * 0.5f + 0.5f);
        SetAlpha(m_HpVignette, Mathf.Lerp(0.35f, 1f, pulse));
    }

    private IEnumerator CoPlayDeathFade(System.Action _onComplete)
    {
        // 1.8초 carbon 암전 페이드(director 지정, DEATH_FADE_DURATION 상수로 0.2초 단위 미세조정).
        // death_overlay GameObject는 HUD 비활성에도 살아있는 별 Canvas에 위치(designer-b 이동).
        // 암전이 완전히 도달한 뒤 _onComplete(=ResultPopup 표시)를 호출해 BgDefeat가 어둠에서 떠오르게 한다.
        m_DeathTriggered = true;

        if (m_DeathFadeCoroutine != null)
            StopCoroutine(m_DeathFadeCoroutine);
        m_DeathFadeCoroutine = StartCoroutine(CoFadeImage(m_DeathOverlay, GetAlpha(m_DeathOverlay), 1f, DEATH_FADE_DURATION));
        yield return m_DeathFadeCoroutine;

        _onComplete?.Invoke();
    }

    private IEnumerator CoFadeImage(Image _image, float _from, float _to, float _duration)
    {
        if (_image == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(_image, Mathf.Lerp(_from, _to, Mathf.Clamp01(elapsed / _duration)));
            yield return null;
        }
        SetAlpha(_image, _to);
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _current)
    {
        ApplyDayNightTint(_current, _instant: false);
    }

    private void ApplyDayNightTint(DayPhase _phase, bool _instant)
    {
        if (m_DayNightTint == null)
            return;

        Color target = GetPhaseTint(_phase);
        target.a = GetPhaseAlpha(_phase);

        if (_instant)
        {
            m_DayNightTint.color = target;
            return;
        }

        if (m_DayNightFadeCoroutine != null)
            StopCoroutine(m_DayNightFadeCoroutine);
        m_DayNightFadeCoroutine = StartCoroutine(CoFadeTint(target));
    }

    private IEnumerator CoFadeTint(Color _target)
    {
        Color start = m_DayNightTint.color;
        float elapsed = 0f;

        while (elapsed < DAYNIGHT_FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            m_DayNightTint.color = Color.Lerp(start, _target, Mathf.Clamp01(elapsed / DAYNIGHT_FADE_DURATION));
            yield return null;
        }

        m_DayNightTint.color = _target;
        m_DayNightFadeCoroutine = null;
    }

    private Color GetPhaseTint(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_TintDay,
            DayPhase.Dusk => m_TintDusk,
            DayPhase.Night => m_TintNight,
            DayPhase.Dawn => m_TintDawn,
            _ => m_TintDay,
        };
    }

    private float GetPhaseAlpha(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_TintAlphaDay,
            DayPhase.Dusk => m_TintAlphaDusk,
            DayPhase.Night => m_TintAlphaNight,
            DayPhase.Dawn => m_TintAlphaDawn,
            _ => m_TintAlphaDay,
        };
    }

    private DayPhase GetCurrentPhase()
    {
        var worker = InGameController.Instance?.DayNightWorker;
        return worker != null ? worker.CurrentPhase : DayPhase.Day;
    }

    private void UpdateFogScroll()
    {
        if (m_AmbientFog == null)
            return;

        m_FogScrollOffset += FOG_SCROLL_SPEED * Time.deltaTime;
        if (m_FogScrollOffset > 1f)
            m_FogScrollOffset -= 1f;

        var rect = m_AmbientFog.uvRect;
        rect.x = m_FogScrollOffset;
        m_AmbientFog.uvRect = rect;
    }

    private static float GetAlpha(Graphic _graphic)
    {
        return _graphic != null ? _graphic.color.a : 0f;
    }

    private static void SetAlpha(Graphic _graphic, float _alpha)
    {
        if (_graphic == null)
            return;

        Color c = _graphic.color;
        c.a = _alpha;
        _graphic.color = c;
    }
}
