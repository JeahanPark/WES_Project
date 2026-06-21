/// <summary>
/// R4 ③ 사운드 트리거 키 단일소스. 호출부는 이 상수를 쓰고, sound가 Addressable에 같은 키로
/// AudioClip을 등록하면 소리가 난다(키 미등록 = 무음, AudioManager null 가드).
/// Addressable Address = 키 문자열 그대로(예: "sfx_hit").
/// 기획 §6 트리거 약 12개 + BGM/Ambient/Stinger.
/// </summary>
public static class AudioKey
{
    // ── SFX (원샷) ──
    public const string SFX_HIT = "sfx_hit";              // 타격/피격
    public const string SFX_COLLECT = "sfx_collect";      // 수집(채집 완료)
    public const string SFX_CRAFT = "sfx_craft";          // 제작 성공
    public const string SFX_UI_OPEN = "sfx_ui_open";      // UI 열기
    public const string SFX_UI_CLOSE = "sfx_ui_close";    // UI 닫기
    public const string SFX_UI_CLICK = "sfx_ui_click";    // 버튼 클릭
    public const string SFX_FOOTSTEP = "sfx_footstep";    // 발소리

    // ── Ambient (루프) — 날씨/페이즈 연동 ──
    public const string AMBIENT_DEFAULT = "ambient_default"; // 기본 환경음
    public const string AMBIENT_RAIN = "ambient_rain";       // 비
    public const string AMBIENT_SNOWSTORM = "ambient_snowstorm"; // 눈보라
    public const string AMBIENT_NIGHT = "ambient_night";     // 밤(밤벌레/바람)

    // ── BGM (루프) ──
    public const string BGM_FIELD = "bgm_field";          // 기본 필드 배경음

    // ── Stinger (원샷 최우선) ──
    public const string STINGER_BOSS_APPEAR = "stinger_boss_appear"; // 보스 등장
    public const string STINGER_ALLY_DOWN = "stinger_ally_down";     // 동료 사망
    public const string STINGER_ESCAPE = "stinger_escape";           // 탈출 성공
}
