using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R4 ② 인게임 텍스트(내레이션·통지·이벤트·엔딩) 데이터.
/// story v0.1(narrative 텍스트.md) 원문을 담는다. 토스트/화면 문구 — 콤마 자유(CSV 미적용),
/// 동기화 무관 로컬 표시.
///
/// CSV 파이프라인은 STRING 콤마 금지가 고정 → 콤마 포함 문장이 다수인 내레이션/이벤트/엔딩은
/// ScriptableObject로 둔다(코딩규칙 "CSV 우선, SO 차선"의 정당한 차선). director 승인 2026-06-21.
///
/// 운용: .asset을 만들어 m_Entries를 인스펙터에서 채우면 그 값을 우선 사용.
/// .asset이 비어있으면 코드 내장 폴백 테이블(BuildFallback)을 사용해 .asset 없이도 동작한다.
/// (MCP 불가 환경에서 .asset 자동생성이 막혀 폴백을 둔다 — 추후 .asset로 이관 가능.)
/// </summary>
[CreateAssetMenu(fileName = "NarrationData", menuName = "WES/NarrationData")]
public class NarrationData : ScriptableObject
{
    [Serializable]
    public class NarrationEntry
    {
        [SerializeField] private string m_Key;
        [SerializeField] private NarrationCategory m_Category;
        [SerializeField, TextArea] private string m_Text;

        public NarrationEntry(string _key, NarrationCategory _category, string _text)
        {
            m_Key = _key;
            m_Category = _category;
            m_Text = _text;
        }

        public string Key => m_Key;
        public NarrationCategory Category => m_Category;
        public string Text => m_Text;
    }

    [SerializeField] private List<NarrationEntry> m_Entries = new List<NarrationEntry>();

    private Dictionary<string, NarrationEntry> m_Lookup;

    /// <summary>키로 텍스트 조회. 없으면 빈 문자열.</summary>
    public string Get(string _key)
    {
        EnsureLookup();
        if (string.IsNullOrEmpty(_key))
            return string.Empty;
        return m_Lookup.TryGetValue(_key, out NarrationEntry entry) ? entry.Text : string.Empty;
    }

    /// <summary>카테고리별 엔트리(읽기 전용).</summary>
    public IReadOnlyList<NarrationEntry> GetByCategory(NarrationCategory _category)
    {
        List<NarrationEntry> result = new List<NarrationEntry>();
        List<NarrationEntry> source = GetActiveEntries();
        for (int i = 0; i < source.Count; i++)
            if (source[i].Category == _category)
                result.Add(source[i]);
        return result;
    }

    private void EnsureLookup()
    {
        if (m_Lookup != null)
            return;

        m_Lookup = new Dictionary<string, NarrationEntry>();
        List<NarrationEntry> source = GetActiveEntries();
        for (int i = 0; i < source.Count; i++)
        {
            NarrationEntry e = source[i];
            if (e == null || string.IsNullOrEmpty(e.Key))
                continue;
            m_Lookup[e.Key] = e; // 중복 키는 마지막 우선
        }
    }

    private List<NarrationEntry> GetActiveEntries()
    {
        // 인스펙터 .asset에 채워진 값이 있으면 그것을, 없으면 코드 폴백을 사용.
        return (m_Entries != null && m_Entries.Count > 0) ? m_Entries : BuildFallback();
    }

    // story v0.1 원문(텍스트.md). 콤마 포함 그대로 유지.
    private static List<NarrationEntry> BuildFallback()
    {
        return new List<NarrationEntry>
        {
            // ── 지역 진입 내레이션 (6지역 × 2줄). 키 = area_{areaId}_{a|b} ──
            new("area_1_a", NarrationCategory.AreaEnter, "검은 파도가 너를 모래 위에 뱉어냈다."),
            new("area_1_b", NarrationCategory.AreaEnter, "절벽 위 등대는, 불 꺼진 지 오래다."),
            new("area_2_a", NarrationCategory.AreaEnter, "나무들이 길을 삼킨다."),
            new("area_2_b", NarrationCategory.AreaEnter, "긁힌 자국과 흩어진 뼈 — 주인은 따로 있다."),
            new("area_3_a", NarrationCategory.AreaEnter, "발이 빠진다. 안개가 무엇을 숨기는지 모른다."),
            new("area_3_b", NarrationCategory.AreaEnter, "부서진 다리. 누군가 건너려 했었다."),
            new("area_4_a", NarrationCategory.AreaEnter, "버려진 갱도가 입을 벌린다."),
            new("area_4_b", NarrationCategory.AreaEnter, "광부들의 도구는 그대로인데, 그들은 없다."),
            new("area_5_a", NarrationCategory.AreaEnter, "눈보라가 시야를 지운다."),
            new("area_5_b", NarrationCategory.AreaEnter, "눈 속의 형체들 — 여기서 멈춘 자들이다."),
            new("area_6_a", NarrationCategory.AreaEnter, "무너진 성벽 너머, 희미한 불빛."),
            new("area_6_b", NarrationCategory.AreaEnter, "거의 다 왔다. 그런데 왜 아무도 마중 나오지 않는가."),

            // ── 통지 (도면). {도면명} 치환은 호출부에서 string.Format. ──
            new("notify_blueprint_new", NarrationCategory.Notify, "{0} 도면을 익혔다."),
            new("notify_blueprint_dup", NarrationCategory.Notify, "이미 아는 도면이다."),

            // ── 관문·이벤트 대사 ──
            new("event_first_campfire", NarrationCategory.Event, "불이 붙는다. 잠시뿐이라는 걸 안다."),
            new("event_river_block", NarrationCategory.Event, "물이 깊다. 이대로는 건널 수 없다."),
            new("event_snowstorm_nocoat", NarrationCategory.Event, "살을 에는 바람. 몸이 빠르게 식는다."),
            new("event_boss_appear", NarrationCategory.Event, "무언가 길을 막아선다. 마지막 문턱이다."),
            new("event_ally_down", NarrationCategory.Event, "{0}이(가) 쓰러졌다."),     // {0}=player_name
            new("event_ally_revive", NarrationCategory.Event, "{0}이(가) 다시 일어선다."), // {0}=player_name

            // ── 엔딩/게임오버 (5라인) ──
            new("ending_clear", NarrationCategory.Ending, "마을의 문이 열린다. 너는 살아남았다 — 이번엔."),
            // "다회차 불안" 변종(2회차+, director 채택). 빈도 R5 튜닝.
            new("ending_anxiety", NarrationCategory.Ending, "불빛 아래, 낯선 눈들이 너를 본다. 이곳은 정말 안전한가."),
            new("ending_dead_cold", NarrationCategory.Ending, "추위가 마지막 숨을 가져갔다. 다음 난파자가 너의 흔적을 보게 될 것이다."),
            new("ending_dead_combat", NarrationCategory.Ending, "더는 일어설 수 없다. 섬은 또 하나의 이름을 삼켰다."),
        };
    }
}
