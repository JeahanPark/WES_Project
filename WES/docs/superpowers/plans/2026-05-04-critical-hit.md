# 크리티컬 히트 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 모든 캐릭터의 공격에 10% 확률로 1.5배 데미지를 적용하고, 크리티컬일 때 데미지 숫자를 빨간색·1.4배 크기로 차별화한다.

**Architecture:** 크리티컬 판정은 서버 권위 (`CharacterBase.TakeDamageServerRpc`) 내부에서 굴리고, 결과 `isCritical` 플래그를 `OnDamagedClientRpc`로 모든 클라이언트에 브로드캐스트해 World UI 데미지 숫자에 시각 차별화를 적용한다.

**Tech Stack:** Unity 6, Unity Netcode for GameObjects, TextMeshPro, Addressables, MCP Unity Plugin (Dev-QA)

**Spec:** [docs/superpowers/specs/2026-05-04-critical-hit-design.md](../specs/2026-05-04-critical-hit-design.md)

---

## File Structure

| 파일 | 역할 | 작업 |
|---|---|---|
| `Assets/Scripts/WorldBaseObject/CharacterBase.cs` | 데미지/HP 핵심 로직 | 수정 |
| `Assets/Scripts/WorldBaseObject/Monster/State/MonsterBase.cs` | 몬스터 베이스 (`OnDamaged` 오버라이드) | 수정 |
| `Assets/Scripts/Worker/InGameWorldUIWorker.cs` | World UI 풀링/생성 | 수정 |
| `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs` | 데미지 숫자 표시 | 수정 |
| `Assets/Scripts/Manager/TestManager.cs` | QA 시나리오 (UNITY_EDITOR 전용) | 수정 |

---

## Task 1: 크리티컬 롤 로직 및 시그니처 확장

`CharacterBase`에 크리티컬 판정 로직과 `_isCritical` 파라미터를 추가하고, 시그니처 변경을 따르는 모든 코드(파생 클래스, World UI 생성 호출처)를 함께 수정한다. 이 단계는 컴파일 단위로 묶여 있어 한 번에 처리해야 한다. 시각 차별화는 Task 2에서 추가하며, 이번 단계에서는 `_isCritical`을 받기만 하고 시각적으로는 변화 없음.

**Files:**
- Modify: `Assets/Scripts/WorldBaseObject/CharacterBase.cs`
- Modify: `Assets/Scripts/WorldBaseObject/Monster/State/MonsterBase.cs`
- Modify: `Assets/Scripts/Worker/InGameWorldUIWorker.cs`

- [ ] **Step 1: `CharacterBase.cs` 상수 추가**

`CharacterBase.cs` 클래스 상단의 상수 블록 (라인 10~13 근처) 에 추가:

```csharp
public const int DEFAULT_MAX_HP = 100;
public const int DEFAULT_ATK = 10;
public const int DEFAULT_DEF = 3;
public const float DEFAULT_HP_REGEN = 0.5f;
public const float DEFAULT_MOVE_SPEED = 5.0f;
public const float CRITICAL_CHANCE = 0.1f;
public const float CRITICAL_MULTIPLIER = 1.5f;
```

- [ ] **Step 2: `m_OnDamaged` 이벤트 시그니처 확장**

`CharacterBase.cs` 라인 29 수정:

```csharp
private event System.Action<int, CharacterBase, bool> m_OnDamaged;
```

라인 111~119의 Subscribe/Unsubscribe도 함께 수정:

```csharp
public void SubscribeOnDamaged(System.Action<int, CharacterBase, bool> _callback)
{
    m_OnDamaged += _callback;
}

public void UnsubscribeOnDamaged(System.Action<int, CharacterBase, bool> _callback)
{
    m_OnDamaged -= _callback;
}
```

- [ ] **Step 3: `TakeDamageServerRpc` 크리티컬 롤 + 버그 수정**

`CharacterBase.cs` 라인 169~184의 `TakeDamageServerRpc`를 다음으로 교체:

```csharp
[Rpc(SendTo.Server)]
private void TakeDamageServerRpc(int _damage, ulong _attackerId)
{
    if (IsDead)
        return;

    bool isCritical = UnityEngine.Random.value < CRITICAL_CHANCE;
    float multiplier = isCritical ? CRITICAL_MULTIPLIER : 1f;
    int finalDamage = Mathf.Max(1, Mathf.RoundToInt((_damage - m_DEF) * multiplier));
    SetHP(m_HP.Value - finalDamage);

    OnDamagedClientRpc(finalDamage, _attackerId, isCritical);

    if (IsDead)
    {
        OnDeathClientRpc();
    }
}
```

**참고**: 기존 코드는 `OnDamagedClientRpc(_damage, _attackerId)`로 원본 데미지를 보내고 있어 World UI 표시값과 실제 차감 HP가 불일치하는 버그가 있었음. `finalDamage`로 교체.

- [ ] **Step 4: `OnDamagedClientRpc` 시그니처 확장**

`CharacterBase.cs` 라인 186~201의 `OnDamagedClientRpc`를 다음으로 교체:

```csharp
[Rpc(SendTo.Everyone)]
private void OnDamagedClientRpc(int _damage, ulong _attackerId, bool _isCritical)
{
    CharacterBase attacker = null;

    if (_attackerId != 0 && NetworkManager.Singleton != null)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_attackerId, out var attackerObj))
        {
            attacker = attackerObj.GetComponent<CharacterBase>();
        }
    }

    m_OnDamaged?.Invoke(_damage, attacker, _isCritical);
    OnDamaged(_damage, attacker, _isCritical);
}
```

- [ ] **Step 5: `OnDamaged` 가상 메서드 시그니처 확장**

`CharacterBase.cs` 라인 213~220의 `OnDamaged`를 다음으로 교체:

```csharp
protected virtual void OnDamaged(int _damage, CharacterBase _attacker, bool _isCritical)
{
    if (InGameController.Instance == null || InGameController.Instance.WorldUIWorker == null)
        return;

    Vector3 spawnPosition = transform.position + WorldUIOffset + Vector3.up * 0.3f;
    InGameController.Instance.WorldUIWorker.CreateDamageNumber(_damage, spawnPosition, _isCritical);
}
```

- [ ] **Step 6: `MonsterBase.OnDamaged` 오버라이드 시그니처 동기화**

`Assets/Scripts/WorldBaseObject/Monster/State/MonsterBase.cs` 라인 56~64를 다음으로 교체:

```csharp
protected override void OnDamaged(int _damage, CharacterBase _attacker, bool _isCritical)
{
    base.OnDamaged(_damage, _attacker, _isCritical);

    if (!IsDead && m_StateMachine != null)
    {
        m_StateMachine.ChangeState(MonsterStateType.Hit);
    }
}
```

- [ ] **Step 7: `InGameWorldUIWorker.CreateDamageNumber` 시그니처 확장**

`Assets/Scripts/Worker/InGameWorldUIWorker.cs` 라인 70~81을 다음으로 교체. 이 단계에서는 `_isCritical`을 받기만 하고 색상은 그대로 흰색 유지 (Task 2에서 활용):

```csharp
public DamageNumberWorldUI CreateDamageNumber(int _damage, Vector3 _worldPosition, bool _isCritical = false)
{
    DamageNumberWorldUI worldUI = CreateWorldUI<DamageNumberWorldUI>();

    if (worldUI == null)
        return null;

    Vector2 screenOffset = new Vector2(UnityEngine.Random.Range(-20f, 20f), 0f);
    worldUI.SetData(_damage, _worldPosition, screenOffset, m_Camera, m_UICamera, m_CanvasRectTransform, Color.white);

    return worldUI;
}
```

- [ ] **Step 8: 컴파일 확인 — Unity 에셋 리프레시**

Bash:
```
echo "asset refresh via MCP"
```

MCP 도구 호출:
- `mcp__mcp-unity__u_editor_asset` action=`refresh`
- `mcp__mcp-unity__u_console` action=`read` lines=`200` filter=`error`

Expected: 컴파일 에러 0건. 워닝은 무시 가능 (단, 본 변경에서 발생한 신규 워닝이면 점검).

**다른 `SubscribeOnDamaged` 사용처 검색**: Step 8 직후 grep으로 잔여 호출자 확인:

```
Grep pattern="SubscribeOnDamaged|UnsubscribeOnDamaged|m_OnDamaged" path="Assets/Scripts"
```

`CharacterBase.cs` 외의 사용처가 있다면 시그니처 동기화 필요 (현재 코드베이스에는 외부 사용처 없음 확인됨).

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/WorldBaseObject/CharacterBase.cs Assets/Scripts/WorldBaseObject/Monster/State/MonsterBase.cs Assets/Scripts/Worker/InGameWorldUIWorker.cs
git commit -m "크리티컬 히트: 서버 롤 로직 + 시그니처 확장 (시각 차별화는 Task 2)"
```

---

## Task 2: 데미지 숫자 시각 차별화 (색상 + 크기)

`InGameWorldUIWorker`에서 크리티컬일 때 빨간색·1.4배 크기를 결정하고, `DamageNumberWorldUI.SetData`가 스케일을 받아 폰트 크기에 적용한다. 풀 재사용 시 폰트 크기가 누적되지 않도록 `OnRelease`에서 복원한다.

**Files:**
- Modify: `Assets/Scripts/Worker/InGameWorldUIWorker.cs`
- Modify: `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs`

- [ ] **Step 1: `DamageNumberWorldUI` baseFontSize 캐싱 + `SetData` 시그니처 확장**

`Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs` 전체를 다음으로 교체 (변경 부분만 따로 가능하지만, 검토 명확성을 위해 클래스 전체 제시):

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
    private float m_CurrentRiseY;
    private float m_BaseFontSize;
    private bool m_BaseFontSizeCached;

    public void SetData(
        int _damage,
        Vector3 _worldPosition,
        Vector2 _screenOffset,
        Camera _camera,
        Camera _uiCamera,
        RectTransform _canvasRect,
        Color _textColor,
        float _scaleMultiplier)
    {
        m_WorldPosition = _worldPosition;
        m_ScreenOffset = _screenOffset;
        m_Camera = _camera;
        m_UICamera = _uiCamera;
        m_CanvasRectTransform = _canvasRect;

        if (m_DamageText != null)
        {
            if (!m_BaseFontSizeCached)
            {
                m_BaseFontSize = m_DamageText.fontSize;
                m_BaseFontSizeCached = true;
            }
            m_DamageText.text = _damage.ToString();
            m_DamageText.color = _textColor;
            m_DamageText.fontSize = m_BaseFontSize * _scaleMultiplier;
        }

        if (m_CanvasGroup != null)
            m_CanvasGroup.alpha = 1f;

        m_CurrentRiseY = 0f;

        StopAnimation();
        m_AnimationCoroutine = StartCoroutine(CoPlayAnimation());
    }

    protected override void OnRelease()
    {
        StopAnimation();
        m_Camera = null;
        m_UICamera = null;
        m_CanvasRectTransform = null;
        m_CurrentRiseY = 0f;

        if (m_DamageText != null && m_BaseFontSizeCached)
        {
            m_DamageText.fontSize = m_BaseFontSize;
        }
    }

    private void LateUpdate()
    {
        UpdatePosition(m_CurrentRiseY);
    }

    private IEnumerator CoPlayAnimation()
    {
        float elapsed = 0f;

        while (elapsed < LIFETIME)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LIFETIME);
            float eased = 1f - (1f - t) * (1f - t); // EaseOutQuad
            m_CurrentRiseY = eased * RISE_DISTANCE;

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

        if (!IsActive)
            yield break;

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

- [ ] **Step 2: `InGameWorldUIWorker.CreateDamageNumber` 색상/크기 분기**

`Assets/Scripts/Worker/InGameWorldUIWorker.cs` 클래스 멤버 영역 (라인 14 근처, 다른 `private` 멤버들과 함께)에 상수 추가:

```csharp
private static readonly Color CRIT_COLOR = Color.red;
private const float CRIT_SCALE_MULTIPLIER = 1.4f;
```

`CreateDamageNumber`를 다음으로 교체:

```csharp
public DamageNumberWorldUI CreateDamageNumber(int _damage, Vector3 _worldPosition, bool _isCritical = false)
{
    DamageNumberWorldUI worldUI = CreateWorldUI<DamageNumberWorldUI>();

    if (worldUI == null)
        return null;

    Vector2 screenOffset = new Vector2(UnityEngine.Random.Range(-20f, 20f), 0f);
    Color color = _isCritical ? CRIT_COLOR : Color.white;
    float scale = _isCritical ? CRIT_SCALE_MULTIPLIER : 1f;

    worldUI.SetData(_damage, _worldPosition, screenOffset, m_Camera, m_UICamera, m_CanvasRectTransform, color, scale);

    return worldUI;
}
```

- [ ] **Step 3: 컴파일 확인**

MCP 도구 호출:
- `mcp__mcp-unity__u_editor_asset` action=`refresh`
- `mcp__mcp-unity__u_console` action=`read` lines=`200` filter=`error`

Expected: 컴파일 에러 0건.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Worker/InGameWorldUIWorker.cs Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs
git commit -m "크리티컬 히트: 데미지 숫자 노란색·1.4배 크기 시각 차별화"
```

---

## Task 3: TestManager에 `TestCriticalHit` 시나리오 추가

기존 `TestDamageNumber` 시나리오와 동일한 패턴으로, 100회 공격을 시뮬레이션하고 `SubscribeOnDamaged` 콜백으로 크리/일반 카운트를 통계 검증한다.

**Files:**
- Modify: `Assets/Scripts/Manager/TestManager.cs`

- [ ] **Step 1: `TestCriticalHit` 메서드 추가**

`Assets/Scripts/Manager/TestManager.cs` 의 `CoTestDamageNumber` 메서드 끝 (라인 845 `}` 다음, 라인 846 `}` 클래스 닫는 중괄호 바로 앞)에 다음 두 메서드를 추가:

```csharp
    public void TestCriticalHit()
    {
        StartCoroutine(CoTestCriticalHit());
    }

    private IEnumerator CoTestCriticalHit()
    {
        GameDebug.Log("[TestManager] TestCriticalHit 시작");

        int passed = 0;
        int failed = 0;
        void Mark(bool _condition, string _label)
        {
            if (_condition) { passed++; GameDebug.Log($"[TestManager] PASS: {_label}"); }
            else { failed++; GameDebug.LogError($"[TestManager] FAIL: {_label}"); }
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

        var player = controller.PlayWorker?.LocalPlayer;
        if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

        // 죽지 않도록 HP 강화
        player.SetMaxHP(99999);
        player.SetHP(99999);

        // 카운터
        int critCount = 0;
        int normalCount = 0;
        int totalDamageEvents = 0;
        int normalDamageValue = -1;
        int critDamageValue = -1;

        System.Action<int, CharacterBase, bool> callback = (_dmg, _atk, _isCrit) =>
        {
            totalDamageEvents++;
            if (_isCrit)
            {
                critCount++;
                critDamageValue = _dmg;
            }
            else
            {
                normalCount++;
                normalDamageValue = _dmg;
            }
        };
        player.SubscribeOnDamaged(callback);

        // 100회 공격 (자가 피격, attacker는 자기 자신)
        const int ATTACK_COUNT = 100;
        const int ATTACK_DAMAGE = 20; // ATK - DEF 가 충분히 큰 값으로
        GameDebug.Log($"[TestManager] {ATTACK_COUNT}회 공격 시작 (damage={ATTACK_DAMAGE})");
        for (int i = 0; i < ATTACK_COUNT; i++)
        {
            player.TakeDamage(ATTACK_DAMAGE, player);
            yield return null; // 1프레임 대기 (RPC 처리)
        }

        // RPC 마무리 대기
        yield return new WaitForSeconds(0.3f);

        player.UnsubscribeOnDamaged(callback);

        // 판정
        Mark(totalDamageEvents == ATTACK_COUNT, $"총 데미지 이벤트 {totalDamageEvents} == {ATTACK_COUNT}");
        // 99% 신뢰구간 (n=100, p=0.1): 약 [3, 18]. 여유를 두어 [3, 20]
        Mark(critCount >= 3 && critCount <= 20, $"크리티컬 횟수 {critCount} ∈ [3, 20] (기대 ~10)");
        Mark(normalCount >= 80 && normalCount <= 97, $"일반 횟수 {normalCount} ∈ [80, 97] (기대 ~90)");

        // 데미지 값 검증
        int expectedNormal = Mathf.Max(1, ATTACK_DAMAGE - player.GetDEF());
        int expectedCrit = Mathf.Max(1, Mathf.RoundToInt((ATTACK_DAMAGE - player.GetDEF()) * CharacterBase.CRITICAL_MULTIPLIER));
        if (normalCount > 0)
            Mark(normalDamageValue == expectedNormal, $"일반 데미지값 {normalDamageValue} == {expectedNormal}");
        if (critCount > 0)
            Mark(critDamageValue == expectedCrit, $"크리 데미지값 {critDamageValue} == {expectedCrit}");

        GameDebug.Log($"[TestManager] TestCriticalHit 결과: PASS {passed}, FAIL {failed} (crit={critCount}, normal={normalCount})");
    }
```

- [ ] **Step 2: 컴파일 확인**

MCP 도구 호출:
- `mcp__mcp-unity__u_editor_asset` action=`refresh`
- `mcp__mcp-unity__u_console` action=`read` lines=`200` filter=`error`

Expected: 컴파일 에러 0건.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Manager/TestManager.cs
git commit -m "크리티컬 히트: TestCriticalHit QA 시나리오 추가"
```

---

## Task 4: 플레이모드 QA 실행

Unity 에디터에서 플레이모드를 진입하고, `TestManager.TestCriticalHit`을 실행한 뒤 콘솔 로그·스크린샷으로 검증한다. 또한 회귀를 위해 기존 `TestDamageNumber`도 재실행한다.

**Files:** (코드 변경 없음, QA만)

- [ ] **Step 1: 플레이모드 진입 + TestCriticalHit 실행**

**전제**: 기존 `TestDamageNumber` 시나리오와 동일하게, 인게임 씬에 LocalPlayer가 이미 스폰되어 있어야 한다 (`InGameController.PlayWorker.LocalPlayer != null`). 일반적으로 로그인→방 입장→게임 시작 흐름으로 진입한다. 이 단계는 기존 Dev-QA 워크플로우 문서 [docs/superpowers/specs/2026-04-15-dev-qa-workflow-design.md](../specs/2026-04-15-dev-qa-workflow-design.md)를 따른다.

MCP 도구 호출 순서:
1. `mcp__mcp-unity__u_console` action=`clear` (콘솔 비우기)
2. `mcp__mcp-unity__u_play` action=`enter` (플레이모드 진입)
3. 인게임 씬 진입 대기 — `mcp__mcp-unity__u_editor_query`로 `InGameController.PlayWorker.LocalPlayer` 존재 확인까지 수동 또는 폴링 (대기 시간 30초 한계). 이미 InGame 씬에서 시작하는 셋업이라면 약 5초 대기로 충분.
4. `mcp__mcp-unity__u_play` action=`invoke` target=`TestManager` method=`TestCriticalHit`
5. 시나리오 완료 대기 (약 8초 — 100회 × 1프레임 + 0.3s 마진)

**LocalPlayer 미존재 시 중단**: 콘솔에 `[TestManager] LocalPlayer 없음` 에러가 뜨면, 인게임 씬 진입 절차 점검 후 재시도. CLAUDE.md의 Dev-QA 중단 조건에 해당하면 사용자에게 보고.

- [ ] **Step 2: 콘솔 로그 검증**

`mcp__mcp-unity__u_console` action=`read` lines=`500` filter=`TestCriticalHit|TestManager`

확인 항목:
- `[TestManager] TestCriticalHit 시작` 로그 존재
- `[TestManager] PASS: 총 데미지 이벤트 100 == 100`
- `[TestManager] PASS: 크리티컬 횟수 N ∈ [3, 20]` (N은 약 10)
- `[TestManager] PASS: 일반 횟수 M ∈ [80, 97]`
- `[TestManager] PASS: 일반 데미지값 ...`
- `[TestManager] PASS: 크리 데미지값 ...`
- `[TestManager] TestCriticalHit 결과: PASS X, FAIL 0`

`FAIL` 이 1개라도 있으면 stop. 원인 분석:
- 크리 횟수가 0 또는 100 → 롤 로직 미작동 (Random 시드/조건 점검)
- 데미지 값 불일치 → 공식 검증 (`(ATK - DEF) * 1.5` 반올림)
- `total != 100` → RPC 누락 (대기 시간 늘리기)

- [ ] **Step 3: 시각 검증 스크린샷**

크리티컬 데미지 숫자가 화면에 떠오르는 타이밍을 잡기 위해, 시나리오를 한 번 더 짧게 재실행한 뒤 스크린샷 캡처:

1. `mcp__mcp-unity__u_play` action=`invoke` target=`TestManager` method=`TestCriticalHit` (재실행)
2. 약 1초 대기 (충분한 데미지 숫자가 떠 있을 시점)
3. `mcp__mcp-unity__u_screenshot` size=`game` filename=`screenshot_critical.png`

확인:
- 화면에 데미지 숫자 다수 노출
- 빨간색·큰 글씨의 크리티컬 숫자가 1개 이상 보임
- 흰색·기본 크기의 일반 숫자도 함께 보임 (대조)

스크린샷에서 빨간색 큰 숫자가 안 보이면 (확률상 0.1^k로 안 뜨는 경우 가능) Step 1~3을 한 번 더 반복.

- [ ] **Step 4: 회귀 — 기존 `TestDamageNumber` 재실행**

`OnDamaged`/`SubscribeOnDamaged` 시그니처 변경의 회귀를 확인:

1. `mcp__mcp-unity__u_console` action=`clear`
2. `mcp__mcp-unity__u_play` action=`invoke` target=`TestManager` method=`TestDamageNumber`
3. 약 5초 대기
4. `mcp__mcp-unity__u_console` action=`read` lines=`500` filter=`TestDamageNumber|TestManager`

확인:
- `[TestManager] TestDamageNumber 결과: PASS X, FAIL 0` — 기존 4개 시나리오 (자가피격/풀반환/연속피격/몬스터피격) 모두 PASS

- [ ] **Step 5: 플레이모드 종료**

`mcp__mcp-unity__u_play` action=`exit`

- [ ] **Step 6: QA 결과 기록 커밋**

스크린샷이 `screenshot_critical.png`로 저장되어 있다면 `screenshot_qa_critical.png`로 정리하여 커밋:

```bash
git add screenshot_qa_critical.png
git commit -m "크리티컬 히트: QA 통과 스크린샷"
```

(스크린샷이 큰 바이너리이므로, 저장소 정책상 commit 보류라면 Step 6 생략 가능. 단순히 결과를 사용자에게 텍스트로 보고.)
