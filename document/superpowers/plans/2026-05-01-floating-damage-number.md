# Floating Damage Number 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 피격 캐릭터 머리 위에 흰 숫자(검은 외곽선)가 0.6초 동안 떠올랐다 사라지는 데미지 노출 시스템 구현. 모든 클라이언트에 동일하게 표시.

**Architecture:** 신규 `DamageNumberWorldUI : BaseWorldUI` 클래스 + 프리팹 1개. 기존 `InGameWorldUIWorker`(Type 기반 풀링·Addressable 자동 로드)에 스폰 메서드 1개 추가. `CharacterBase.OnDamaged()` 가상 훅에서 호출 (이미 모든 클라이언트에서 호출됨). 캐릭터 등록/관리·신규 워커 모두 불필요.

**Tech Stack:** Unity 6, C#, Netcode for GameObjects, Addressables, TextMeshPro, UniRx (기존 인프라).

**Spec:** `document/superpowers/specs/2026-05-01-floating-damage-number-design.md`

---

## File Structure

| 파일 | 변경 종류 | 책임 |
|------|----------|------|
| `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs` | **Create** | 단일 데미지 숫자 인스턴스 (애니메이션·자동 해제) |
| `Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab` | **Create** | 프리팹 (TMP + CanvasGroup + DamageNumberWorldUI 컴포넌트) |
| `Assets/Scripts/Worker/InGameWorldUIWorker.cs` | **Modify** | `CreateDamageNumber(int, Vector3)` 메서드 추가 |
| `Assets/Scripts/WorldBaseObject/CharacterBase.cs` | **Modify** | `OnDamaged()` 베이스 구현 한 줄 추가 |
| `Assets/Scripts/Manager/TestManager.cs` | **Modify** | `TestDamageNumber` QA 시나리오 추가 |
| Addressable Group | **Modify** | 프리팹 등록 (Address = `DamageNumberWorldUI`) |

---

## Task 1: DamageNumberWorldUI 스크립트 작성

**Files:**
- Create: `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs`

- [ ] **Step 1: 스크립트 작성**

내용:

```csharp
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 피격 시 머리 위에 떠오르는 데미지 숫자
/// </summary>
public class DamageNumberWorldUI : BaseWorldUI
{
    private const float LIFETIME = 0.6f;
    private const float RISE_DISTANCE = 40f;
    private const float FADE_START_RATIO = 0.667f;

    [SerializeField] private TextMeshProUGUI m_DamageText;
    [SerializeField] private CanvasGroup m_CanvasGroup;

    private Vector3 m_WorldPosition;
    private Vector2 m_ScreenOffset;
    private Camera m_Camera;
    private Camera m_UICamera;
    private RectTransform m_CanvasRectTransform;
    private Coroutine m_AnimationCoroutine;

    public void SetData(
        int _damage,
        Vector3 _worldPosition,
        Vector2 _screenOffset,
        Camera _camera,
        Camera _uiCamera,
        RectTransform _canvasRect,
        Color _textColor)
    {
        m_WorldPosition = _worldPosition;
        m_ScreenOffset = _screenOffset;
        m_Camera = _camera;
        m_UICamera = _uiCamera;
        m_CanvasRectTransform = _canvasRect;

        if (m_DamageText != null)
        {
            m_DamageText.text = _damage.ToString();
            m_DamageText.color = _textColor;
        }

        if (m_CanvasGroup != null)
            m_CanvasGroup.alpha = 1f;

        StopAnimation();
        m_AnimationCoroutine = StartCoroutine(CoPlayAnimation());
    }

    protected override void OnRelease()
    {
        StopAnimation();
        m_Camera = null;
        m_UICamera = null;
        m_CanvasRectTransform = null;
    }

    private void LateUpdate()
    {
        UpdatePosition(0f);
    }

    private IEnumerator CoPlayAnimation()
    {
        float elapsed = 0f;

        while (elapsed < LIFETIME)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LIFETIME);
            float eased = 1f - (1f - t) * (1f - t); // EaseOutQuad
            float riseY = eased * RISE_DISTANCE;

            UpdatePosition(riseY);

            if (m_CanvasGroup != null)
            {
                if (t >= FADE_START_RATIO)
                {
                    float fadeT = (t - FADE_START_RATIO) / (1f - FADE_START_RATIO);
                    m_CanvasGroup.alpha = 1f - fadeT;
                }
                else
                {
                    m_CanvasGroup.alpha = 1f;
                }
            }

            yield return null;
        }

        m_AnimationCoroutine = null;

        if (InGameController.Instance != null && InGameController.Instance.WorldUIWorker != null)
            InGameController.Instance.WorldUIWorker.ReleaseWorldUI(this);
    }

    private void StopAnimation()
    {
        if (m_AnimationCoroutine != null)
        {
            StopCoroutine(m_AnimationCoroutine);
            m_AnimationCoroutine = null;
        }
    }

    private void UpdatePosition(float _riseY)
    {
        if (!IsActive || m_Camera == null || m_CanvasRectTransform == null)
            return;

        Vector3 screenPosition = m_Camera.WorldToScreenPoint(m_WorldPosition);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_CanvasRectTransform,
            screenPosition,
            m_UICamera,
            out Vector2 localPoint);

        localPoint += m_ScreenOffset;
        localPoint.y += _riseY;

        m_RectTransform.localPosition = localPoint;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity 에디터로 돌아가 컴파일 오류 없는지 확인.
명령: `mcp__mcp-unity__u_console` (action: read, lastNSeconds: 30) 호출 후 `error` 키워드 없음 확인.

기대: 컴파일 성공, 콘솔에 컴파일 에러 없음.

- [ ] **Step 3: Asset refresh**

명령: `mcp__mcp-unity__u_editor_asset` (action: refresh) 호출.

기대: `"refreshed": true`.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs.meta
git commit -m "데미지 노출: DamageNumberWorldUI 스크립트 추가

- 단일 인스턴스로 데미지 숫자 표시
- 0.6초간 위로 40px 이동 + 후반 1/3 구간 페이드 아웃
- 캐릭터 transform 추적 안 함 (스폰 시점 월드 좌표 고정)
- 코루틴 종료 시 InGameWorldUIWorker로 자체 반환"
```

---

## Task 2: DamageNumberWorldUI 프리팹 + Addressable 등록

**Files:**
- Create: `Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab`
- Modify: Addressable Group (UI 또는 WorldUI 그룹)

이 작업은 MCP 도구로 진행한다.

- [ ] **Step 1: 신규 GameObject 생성**

Unity Editor에서 빈 GameObject `DamageNumberWorldUI` 생성.
- `RectTransform` 자동 부여 (UI 요소)
- `CanvasGroup` 컴포넌트 추가
- `DamageNumberWorldUI` 컴포넌트 추가

MCP 명령:
```
mcp__mcp-unity__u_editor_gameobject(action: create, name: "DamageNumberWorldUI")
mcp__mcp-unity__u_editor_component(action: add, gameObject: "DamageNumberWorldUI", componentType: "RectTransform")
mcp__mcp-unity__u_editor_component(action: add, gameObject: "DamageNumberWorldUI", componentType: "CanvasGroup")
mcp__mcp-unity__u_editor_component(action: add, gameObject: "DamageNumberWorldUI", componentType: "DamageNumberWorldUI")
```

- [ ] **Step 2: 자식 TMP 텍스트 생성**

`Text (TMP)` 자식 GameObject 생성:
- 이름: `DamageText`
- 위치: 부모 RectTransform 중심 (anchoredPosition 0,0)
- 폰트 크기: 24
- Color: 흰색 #FFFFFFFF
- Outline (TMP 머티리얼 인스턴스에서 설정):
  - Outline Color: #000000FF
  - Outline Thickness: 0.2
- 텍스트: `0` (실행 시 덮어씀)
- Alignment: Center / Middle

MCP 명령 (UI 생성):
```
mcp__mcp-unity__generate_ui_with_gpt(prompt: "TextMeshPro 자식 'DamageText' 생성, 폰트 24, 흰색, 검은 외곽선 0.2 두께, 중앙 정렬, 부모 RectTransform 중심에 anchoredPosition 0,0")
```
또는 수동으로 TMP > Text 추가 후 위 설정.

- [ ] **Step 3: 컴포넌트 참조 연결**

`DamageNumberWorldUI` 컴포넌트의 SerializeField 연결:
- `m_DamageText` ← `DamageText` 자식의 TextMeshProUGUI
- `m_CanvasGroup` ← 자기 자신의 CanvasGroup

MCP 명령 (참조 연결):
```
mcp__mcp-unity__u_editor_component(action: set_field, gameObject: "DamageNumberWorldUI", componentType: "DamageNumberWorldUI", field: "m_DamageText", valueRef: "DamageNumberWorldUI/DamageText")
mcp__mcp-unity__u_editor_component(action: set_field, gameObject: "DamageNumberWorldUI", componentType: "DamageNumberWorldUI", field: "m_CanvasGroup", valueRef: "DamageNumberWorldUI")
```

- [ ] **Step 4: 프리팹으로 저장**

위치: `Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab`

MCP 명령:
```
mcp__mcp-unity__u_editor_prefab(action: save, gameObject: "DamageNumberWorldUI", path: "Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab")
```

씬에 남은 임시 GameObject 삭제:
```
mcp__mcp-unity__u_editor_gameobject(action: delete, name: "DamageNumberWorldUI")
```

- [ ] **Step 5: Addressable 그룹에 등록**

기존 `CharacterWorldUI.prefab`이 등록된 Addressable Group을 확인하고 같은 그룹에 추가:
- Address: `DamageNumberWorldUI` (타입명과 일치 — `InGameWorldUIWorker`가 type.Name으로 로드함)
- Labels: 기존 `CharacterWorldUI`와 동일 라벨 사용

수동 작업: Unity Editor → `Window > Asset Management > Addressables > Groups` → 프리팹 드래그 후 Address 변경.

검증:
```
mcp__mcp-unity__u_editor_asset(action: query, path: "Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab")
```
출력 JSON에 Addressable address가 `DamageNumberWorldUI`로 표시되는지 확인.

- [ ] **Step 6: Asset refresh**

```
mcp__mcp-unity__u_editor_asset(action: refresh)
```

- [ ] **Step 7: 커밋**

```bash
git add Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab.meta Assets/AddressableAssetsData
git commit -m "데미지 노출: DamageNumberWorldUI 프리팹 + Addressable 등록

- TMP(흰 채움 + 검은 외곽선 0.2) + CanvasGroup 구조
- Address: DamageNumberWorldUI (타입명 일치)"
```

---

## Task 3: InGameWorldUIWorker.CreateDamageNumber() 메서드 추가

**Files:**
- Modify: `Assets/Scripts/Worker/InGameWorldUIWorker.cs`

- [ ] **Step 1: 신규 public 메서드 추가**

`CreateCharacterWorldUI` 메서드 바로 아래(`public` 메서드 영역, line 68 다음)에 추가:

```csharp
public DamageNumberWorldUI CreateDamageNumber(int _damage, Vector3 _worldPosition)
{
    DamageNumberWorldUI worldUI = CreateWorldUI<DamageNumberWorldUI>();

    if (worldUI == null)
        return null;

    Vector2 screenOffset = new Vector2(Random.Range(-20f, 20f), 0f);
    worldUI.SetData(_damage, _worldPosition, screenOffset, m_Camera, m_UICamera, m_CanvasRectTransform, Color.white);

    return worldUI;
}
```

- [ ] **Step 2: 컴파일 확인**

```
mcp__mcp-unity__u_console(action: read, lastNSeconds: 30)
```
기대: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Worker/InGameWorldUIWorker.cs
git commit -m "데미지 노출: InGameWorldUIWorker.CreateDamageNumber 추가

- 풀에서 DamageNumberWorldUI 인스턴스 가져와 SetData 호출
- 좌우 ±20px 랜덤 화면 오프셋 적용 (겹침 방지)"
```

---

## Task 4: CharacterBase.OnDamaged() 베이스 구현 — 호출 연결

**Files:**
- Modify: `Assets/Scripts/WorldBaseObject/CharacterBase.cs:213-215`

- [ ] **Step 1: 베이스 구현 변경**

기존 코드 (현재 비어있음):
```csharp
protected virtual void OnDamaged(int _damage, CharacterBase _attacker)
{
}
```

변경 후:
```csharp
protected virtual void OnDamaged(int _damage, CharacterBase _attacker)
{
    if (InGameController.Instance == null || InGameController.Instance.WorldUIWorker == null)
        return;

    Vector3 spawnPosition = transform.position + WorldUIOffset + Vector3.up * 0.3f;
    InGameController.Instance.WorldUIWorker.CreateDamageNumber(_damage, spawnPosition);
}
```

> 자식 클래스 `MonsterBase.OnDamaged`는 이미 `base.OnDamaged(_damage, _attacker)` 호출 중 → 자동 적용. `PlayerCharacter`는 별도 오버라이드 없음 → 베이스 호출됨.

- [ ] **Step 2: 컴파일 확인**

```
mcp__mcp-unity__u_console(action: read, lastNSeconds: 30)
```
기대: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/WorldBaseObject/CharacterBase.cs
git commit -m "데미지 노출: CharacterBase.OnDamaged에서 데미지 숫자 스폰 연결

- WorldUIOffset + Vector3.up * 0.3f 위치에 스폰
- OnDamagedClientRpc는 모든 클라이언트에서 호출되므로 멀티 자동 동기화"
```

---

## Task 5: TestManager.TestDamageNumber 시나리오 추가

**Files:**
- Modify: `Assets/Scripts/Manager/TestManager.cs`

CLAUDE.md Dev-QA 워크플로우 + TestManager 원칙 준수: 테스트 전용 로직 금지, 기존 public 메서드(`TakeDamage`)만 조합 사용.

- [ ] **Step 1: 시나리오 메서드 추가**

`TestBuilding` 메서드 다음(파일 끝부분, `#endif` 직전)에 추가:

```csharp
    public void TestDamageNumber()
    {
        StartCoroutine(CoTestDamageNumber());
    }

    private IEnumerator CoTestDamageNumber()
    {
        GameDebug.Log("[TestManager] TestDamageNumber 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var worldUIWorker = controller.WorldUIWorker;
        var player = controller.PlayWorker?.LocalPlayer;
        if (worldUIWorker == null || player == null)
        {
            GameDebug.LogError("[TestManager] 의존성 없음");
            yield break;
        }

        // 시나리오 1: 플레이어 자가 피격 → 자기 머리 위에 데미지 숫자 노출
        GameDebug.Log("[TestManager] 시나리오 1: 플레이어 자가 피격");
        int beforeCount = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        player.TakeDamage(5, player);
        yield return new WaitForSeconds(0.1f);
        int afterCount = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        Mark(afterCount > beforeCount, $"피격 직후 활성 데미지 숫자 증가 ({beforeCount} → {afterCount})");

        // 시나리오 2: 0.6초 후 자동 사라짐 (풀 반환)
        yield return new WaitForSeconds(0.7f);
        int afterLifetime = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        Mark(afterLifetime <= beforeCount, $"수명 종료 후 활성 데미지 숫자 회수 ({afterLifetime} <= {beforeCount})");

        // 시나리오 3: 동시 다발 피격 — 좌우 분산 (오프셋 적용 확인)
        GameDebug.Log("[TestManager] 시나리오 3: 연속 피격 3회");
        for (int i = 0; i < 3; i++)
        {
            player.TakeDamage(3, player);
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(0.1f);
        var actives = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Mark(actives.Length >= 3, $"동시 활성 인스턴스 수 ({actives.Length} >= 3)");

        if (actives.Length >= 2)
        {
            float xSpread = 0f;
            float refX = actives[0].GetComponent<RectTransform>().localPosition.x;
            for (int i = 1; i < actives.Length; i++)
            {
                xSpread = Mathf.Max(xSpread, Mathf.Abs(actives[i].GetComponent<RectTransform>().localPosition.x - refX));
            }
            Mark(xSpread > 0f, $"좌우 분산 오프셋 적용 (최대 X 편차 {xSpread:F1})");
        }

        // 정리: 모두 사라질 때까지 대기
        yield return new WaitForSeconds(0.7f);

        // 시나리오 4: 몬스터 피격 → 몬스터 머리 위에 노출
        GameDebug.Log("[TestManager] 시나리오 4: 몬스터 피격");
        var monsters = Object.FindObjectsByType<MonsterStateMachine>(FindObjectsSortMode.None);
        if (monsters.Length > 0)
        {
            var monster = monsters[0].GetComponent<CharacterBase>();
            if (monster != null && !monster.IsDead)
            {
                int beforeMonster = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                monster.TakeDamage(2, player);
                yield return new WaitForSeconds(0.1f);
                int afterMonster = Object.FindObjectsByType<DamageNumberWorldUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                Mark(afterMonster > beforeMonster, $"몬스터 피격 시 데미지 숫자 노출 ({beforeMonster} → {afterMonster})");
            }
            else
            {
                GameDebug.LogWarning("[TestManager] 첫 몬스터가 없거나 사망 상태 — 시나리오 4 SKIP");
            }
        }
        else
        {
            GameDebug.LogWarning("[TestManager] 몬스터 미스폰 — 시나리오 4 SKIP");
        }

        yield return new WaitForSeconds(0.7f);

        GameDebug.Log($"[TestManager] TestDamageNumber 결과: PASS {passed}, FAIL {failed}");
    }
```

- [ ] **Step 2: 컴파일 확인**

```
mcp__mcp-unity__u_console(action: read, lastNSeconds: 30)
```
기대: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Manager/TestManager.cs
git commit -m "데미지 노출: TestDamageNumber QA 시나리오 추가

시나리오 4종:
1. 플레이어 자가 피격 시 데미지 숫자 활성 인스턴스 증가
2. 0.6초 후 인스턴스 회수
3. 연속 피격 시 좌우 오프셋 분산
4. 몬스터 피격 시 데미지 숫자 노출"
```

---

## Task 6: 플레이모드 QA 실행 + 스크린샷 검증

**Files:** (없음 — 검증 단계)

CLAUDE.md Dev-QA 워크플로우 그대로 따른다.

- [ ] **Step 1: 플레이모드 진입**

```
mcp__mcp-unity__u_play(action: enter)
```
기대: `"isPlaying": true`.

- [ ] **Step 2: 인게임 씬까지 진입 (필요 시)**

`InGameController` + `LocalPlayer`가 활성화되었는지 확인:
```
mcp__mcp-unity__u_console(action: read, lastNSeconds: 30)
```
로그에서 `[InGameController] Local player spawned` 확인.

> 인게임 씬 진입이 안 되어 있으면 사용자 안내 후 중단 (테스트 시나리오는 인게임에서만 의미 있음).

- [ ] **Step 3: TestDamageNumber 실행**

```
mcp__mcp-unity__u_play(action: invoke, target: "TestManager", method: "TestDamageNumber")
```

- [ ] **Step 4: 콘솔 로그 확인**

3초 대기 후:
```
mcp__mcp-unity__u_console(action: read, lastNSeconds: 10)
```

기대 로그:
- `[TestManager] PASS: 피격 직후 활성 데미지 숫자 증가`
- `[TestManager] PASS: 수명 종료 후 활성 데미지 숫자 회수`
- `[TestManager] PASS: 동시 활성 인스턴스 수`
- `[TestManager] PASS: 좌우 분산 오프셋 적용`
- `[TestManager] TestDamageNumber 결과: PASS N, FAIL 0`

FAIL이 있으면:
- 컴파일/런타임 에러 → 자동 수정 후 재시도
- 풀 미반환 / 카메라 좌표 변환 실패 → 코드 점검
- 인게임 씬 미진입 → 사용자에 안내 후 중단

- [ ] **Step 5: 스크린샷 검증 (피격 순간)**

플레이어 자가 피격 직후 캡처:
```
mcp__mcp-unity__u_play(action: invoke, target: "TestManager", method: "TestDamageNumber")
```
0.2초 대기 후 (수명 0.6초 중간):
```
mcp__mcp-unity__u_screenshot(view: "game")
```

육안 확인:
- 캐릭터 머리 위에 흰 숫자 노출 (검은 외곽선)
- HP바보다 위에 위치
- 가독성 OK

- [ ] **Step 6: 플레이모드 종료**

```
mcp__mcp-unity__u_play(action: exit)
```

- [ ] **Step 7: QA 결과 요약을 사용자에게 보고**

PASS/FAIL 카운트 + 스크린샷 경로 보고. 모두 PASS이고 시각 검증 OK면 작업 완료.

- [ ] **Step 8: 최종 커밋 (스크린샷 포함, 선택)**

스크린샷이 워킹디렉토리에 남아있다면 정리. 일반 스크린샷은 `.gitignore` 처리되거나 첨부 용도. 별도 커밋 불필요.

---

## 완료 기준 (Definition of Done)

- [ ] Task 1~5의 코드/프리팹/Addressable 모두 커밋됨
- [ ] `TestDamageNumber` 시나리오 모두 PASS
- [ ] 스크린샷에서 데미지 숫자가 캐릭터 머리 위에 정상 표시되는 것을 육안 확인
- [ ] 호스트/클라이언트 양쪽에서 동일 노출 확인 (멀티 환경 검증, 사용자 합류 시점에 진행 가능)
- [ ] 회복 표시는 의도적으로 미구현 (out of scope), `SetData(Color)` 인자만 확장 대비

---

## Self-Review 결과

**Spec 커버리지 점검:**
- §3.1 `DamageNumberWorldUI` → Task 1 (스크립트) + Task 2 (프리팹)
- §3.2 `InGameWorldUIWorker.CreateDamageNumber` → Task 3
- §3.3 `CharacterBase.OnDamaged` → Task 4
- §5 애니메이션·비주얼 디테일 → Task 1 (코드 상수) + Task 2 (프리팹 폰트)
- §6 리소스 폴더 구조 → Task 2
- §8 확장성(`Color` 파라미터) → Task 1 `SetData` 시그니처
- §9 테스트/QA → Task 5 (TestManager) + Task 6 (실행/검증)

**누락 없음.**

**타입 일관성:**
- `SetData(int, Vector3, Vector2, Camera, Camera, RectTransform, Color)` — Task 1 정의, Task 3에서 동일 시그니처로 호출. ✓
- `CreateDamageNumber(int, Vector3)` — Task 3 정의, Task 4에서 동일 시그니처로 호출. ✓
- `WorldUIOffset` — `CharacterBase`의 기존 public property 활용. ✓
- `ReleaseWorldUI(BaseWorldUI)` — `InGameWorldUIWorker`의 기존 public 메서드. ✓

**Placeholder 검사:**
- TBD/TODO 없음
- 모든 코드 블록 완전체로 작성
- 모든 MCP 명령에 구체적 인자 제공
