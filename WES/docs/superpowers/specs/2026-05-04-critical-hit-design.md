# 크리티컬 히트 설계

## Overview
모든 캐릭터(플레이어/몬스터)의 공격에 10% 확률로 1.5배 데미지가 들어가는 크리티컬 히트 시스템을 추가한다. 데미지 숫자 World UI는 크리티컬일 때 노란색·1.4배 크기로 시각적으로 차별화한다.

## 요구사항
- 크리티컬 확률: **10%** (`CRITICAL_CHANCE = 0.1f`)
- 크리티컬 배율: **1.5배** (`CRITICAL_MULTIPLIER = 1.5f`)
- 적용 범위: 플레이어·몬스터 모든 공격
- 데미지 공식: `finalDamage = max(1, RoundToInt((ATK - DEF) * (isCritical ? 1.5 : 1)))`
  - DEF 차감 후 배율 적용 (정확히 1.5배 체감)
- 크리티컬 시각: 노란색(`Color(1, 0.85, 0.2)`) + 폰트 크기 1.4배

## 1. 아키텍처

### 롤 위치: 서버 권위
크리티컬 판정은 `CharacterBase.TakeDamageServerRpc` 내부에서 실행한다.
- 클라마다 따로 굴리면 데미지 숫자가 클라끼리 어긋남
- 멀티플레이 권위 일관성

### 데이터 흐름
```
공격자 BaseColliderObject.ProcessHit
  → target.TakeDamage(damage, attacker)
  → [Server] TakeDamageServerRpc
      ├─ isCritical = Random.value < CRITICAL_CHANCE
      ├─ finalDamage = max(1, Round((damage - DEF) * (isCritical ? 1.5 : 1)))
      ├─ SetHP(HP - finalDamage)
      └─ OnDamagedClientRpc(finalDamage, attackerId, isCritical)
  → [All clients] OnDamaged(finalDamage, attacker, isCritical)
      └─ WorldUIWorker.CreateDamageNumber(finalDamage, pos, isCritical)
          └─ DamageNumberWorldUI.SetData(... scaleMultiplier, color)
```

## 2. 변경 사항

### 2.1 `CharacterBase.cs`

**상수 추가** (클래스 상단):
```csharp
public const float CRITICAL_CHANCE = 0.1f;
public const float CRITICAL_MULTIPLIER = 1.5f;
```

**`TakeDamageServerRpc` 수정**:
```csharp
[Rpc(SendTo.Server)]
private void TakeDamageServerRpc(int _damage, ulong _attackerId)
{
    if (IsDead) return;

    bool isCritical = UnityEngine.Random.value < CRITICAL_CHANCE;
    float multiplier = isCritical ? CRITICAL_MULTIPLIER : 1f;
    int finalDamage = Mathf.Max(1, Mathf.RoundToInt((_damage - m_DEF) * multiplier));
    SetHP(m_HP.Value - finalDamage);

    OnDamagedClientRpc(finalDamage, _attackerId, isCritical);

    if (IsDead)
        OnDeathClientRpc();
}
```
- **버그 수정**: 기존 코드는 `OnDamagedClientRpc(_damage, ...)`로 원본 데미지를 보내고 있어, World UI에 표시되는 숫자가 실제 차감된 HP와 다른 문제가 있다. 이번 작업에서 `finalDamage`를 보내도록 같이 정정한다.

**`OnDamagedClientRpc` 시그니처 확장**:
```csharp
[Rpc(SendTo.Everyone)]
private void OnDamagedClientRpc(int _damage, ulong _attackerId, bool _isCritical)
```

**`OnDamaged` 가상 메서드 시그니처 확장**:
```csharp
protected virtual void OnDamaged(int _damage, CharacterBase _attacker, bool _isCritical)
{
    if (InGameController.Instance == null || InGameController.Instance.WorldUIWorker == null)
        return;

    Vector3 spawnPosition = transform.position + WorldUIOffset + Vector3.up * 0.3f;
    InGameController.Instance.WorldUIWorker.CreateDamageNumber(_damage, spawnPosition, _isCritical);
}
```

**`m_OnDamaged` 이벤트 시그니처 확장**:
```csharp
private event System.Action<int, CharacterBase, bool> m_OnDamaged;
public void SubscribeOnDamaged(System.Action<int, CharacterBase, bool> _callback) { ... }
public void UnsubscribeOnDamaged(System.Action<int, CharacterBase, bool> _callback) { ... }
```

### 2.2 `InGameWorldUIWorker.cs`

```csharp
private static readonly Color CRIT_COLOR = new(1f, 0.85f, 0.2f);
private const float CRIT_SCALE_MULTIPLIER = 1.4f;

public DamageNumberWorldUI CreateDamageNumber(int _damage, Vector3 _worldPosition, bool _isCritical = false)
{
    DamageNumberWorldUI worldUI = CreateWorldUI<DamageNumberWorldUI>();
    if (worldUI == null) return null;

    Vector2 screenOffset = new(UnityEngine.Random.Range(-20f, 20f), 0f);
    Color color = _isCritical ? CRIT_COLOR : Color.white;
    float scale = _isCritical ? CRIT_SCALE_MULTIPLIER : 1f;

    worldUI.SetData(_damage, _worldPosition, screenOffset, m_Camera, m_UICamera, m_CanvasRectTransform, color, scale);
    return worldUI;
}
```

### 2.3 `DamageNumberWorldUI.cs`

**`SetData` 시그니처 확장**:
```csharp
public void SetData(int _damage, Vector3 _worldPosition, Vector2 _screenOffset,
    Camera _camera, Camera _uiCamera, RectTransform _canvasRect, Color _textColor, float _scaleMultiplier)
```

**폰트 크기 적용**:
- `Awake`에서 `m_BaseFontSize = m_DamageText.fontSize` 캐싱
- `SetData`에서 `m_DamageText.fontSize = m_BaseFontSize * _scaleMultiplier`
- `OnRelease`에서 `m_DamageText.fontSize = m_BaseFontSize` 복원 (풀 재사용 시 영향 방지)

### 2.4 파생 클래스 시그니처 동기화

`OnDamaged`를 오버라이드한 모든 파생 클래스(`MonsterBase`, 기타)는 `bool _isCritical` 파라미터를 받도록 시그니처 수정. 몬스터 측에서는 별도 분기 없이 `base.OnDamaged(_damage, _attacker, _isCritical)` 호출만 유지.

`m_OnDamaged` 이벤트 구독자(있다면)도 `Action<int, CharacterBase, bool>` 시그니처로 동기화.

## 3. 엣지 케이스

| 케이스 | 처리 |
|---|---|
| `ATK ≤ DEF` (방어력이 공격력 이상) | `Max(1, ...)`로 최소 1 보장. 일반/크리 모두 1이지만 시각 차별화는 그대로 적용 |
| 반올림 | `Mathf.RoundToInt` (예: `(10-3)*1.5=10.5 → 11`) |
| 공격자가 죽거나 사라짐 | 기존 `_attackerId == 0` / `SpawnedObjects` 미존재 시 `attacker = null` 처리 유지. `_isCritical`은 attacker 존재와 무관 |
| HP가 0 이하 | 기존 `IsDead` → `OnDeathClientRpc` 분기 그대로 유지 |
| 풀 재사용으로 폰트 크기가 누적되는 경우 | `OnRelease`에서 `m_BaseFontSize`로 복원 |

## 4. YAGNI - 의도적으로 제외

- 캐릭터별 다른 크리율/배율 (`CharacterScriptable`에 필드 추가 안 함) — 요구사항이 단일 수치
- 무기/스킬별 크리 보정 — 현재 시스템에 없음
- 크리티컬 사운드/이펙트 — 시각 차별화만 요구됨
- `force critical` 테스트 훅 — `TestManager` 원칙(테스트 전용 로직 금지) 위반

## 5. 테스트 전략

### 5.1 자동 통계 검증 — `TestManager.TestCriticalHit`
1. 몬스터 1마리 스폰
2. 테스트 동안 죽지 않도록 `SetMaxHP(99999)` → `SetHP(99999)` (둘 다 기존 public 메서드)
3. `SubscribeOnDamaged`로 카운트 콜백 등록 (크리 카운트, 일반 카운트)
4. `TakeDamage(ATK, null)`을 100회 직접 호출 (서버 RPC 사용, 콜라이더 불필요)
5. 모든 RPC가 처리되도록 1프레임 대기 후 판정:
   - 크리 횟수 4~20회 (10% ± 합리적 변동성, 99% 신뢰구간 ≈ [3, 18])
   - 일반 데미지 값 = `max(1, ATK - DEF)`
   - 크리 데미지 값 = `max(1, Round((ATK - DEF) * 1.5))`
6. 콜백 해제, 몬스터 정리

### 5.2 시각 검증
- 플레이모드에서 크리 발생 직후 스크린샷 캡처
- 노란색·큰 글씨 데미지 숫자 확인

### 5.3 회귀 테스트
- 기존 `TestDamageNumber` 시나리오 재실행 (시그니처 변경에 따른 회귀 방지)

## 6. 변경 파일 목록

- `Assets/Scripts/WorldBaseObject/CharacterBase.cs` — 핵심 로직
- `Assets/Scripts/Worker/InGameWorldUIWorker.cs` — 색/크기 분기
- `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs` — 폰트 크기 적용
- `Assets/Scripts/WorldBaseObject/Monster/State/MonsterBase.cs` — 시그니처 동기화
- (필요 시) 기타 `OnDamaged`/`SubscribeOnDamaged` 사용처
- `Assets/Scripts/Manager/TestManager.cs` — `TestCriticalHit` 시나리오 추가
