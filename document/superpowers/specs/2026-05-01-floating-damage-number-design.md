# Floating Damage Number — 설계

> **작성일**: 2026-05-01
> **상태**: 설계 완료 (구현 대기)
> **연관 마일스톤**: M2 마무리 보강 작업

---

## 1. 개요

피격 캐릭터(플레이어/몬스터) 머리 위에 데미지 숫자를 띄워 전투 피드백을 강화한다. 4인 코옵 환경에서 모든 클라이언트가 동일하게 데미지 숫자를 본다.

---

## 2. 확정 사양

| 항목 | 내용 |
|------|------|
| 형태 | 피격 캐릭터 머리 위에 떠오르는 숫자 (Floating Damage Number) |
| 비주얼 | TMP, 흰 채움(#FFFFFF) + 검은 외곽선(#000000), 색상 구분 없음 |
| 표시 대상 | 모든 클라이언트 (`OnDamagedClientRpc` 활용) |
| 회복 표시 | 이번 작업 제외 (확장 가능 구조 유지) |
| 풀링 | 기존 `InGameWorldUIWorker` 풀(Type 기반) 그대로 활용 |

---

## 3. 아키텍처

기존 인프라를 최대한 재활용한다. 신규 코드는 **WorldUI 클래스 1개 + 워커 메서드 1개 + CharacterBase 한 줄 호출**.

### 3.1 `DamageNumberWorldUI : BaseWorldUI` (신규)

- 위치: `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs`
- 프리팹: `Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab`
  - Addressable 등록 (Address = `DamageNumberWorldUI`, 타입명과 동일 — `InGameWorldUIWorker.InstantiateWorldUI<T>()`가 타입명으로 로드함)
- 책임: 단일 데미지 숫자 인스턴스의 표시 / 애니메이션 / 자동 해제
- 좌표: **캐릭터 transform을 추적하지 않음**. 스폰 시점의 월드 좌표를 고정 보관 후 LateUpdate에서 매 프레임 World→Screen 변환만 수행 (카메라 이동 대응)
- 핵심 API:
  - `void SetData(int _damage, Vector3 _worldPosition, Vector2 _screenOffset, Camera _camera, Camera _uiCamera, RectTransform _canvasRect)`
- 동작: `SetData()` 호출 후 `CoPlayAnimation` 코루틴 시작 → 0.6초 동안 위로 이동 + 페이드 → 코루틴 종료 시 `InGameController.Instance.WorldUIWorker.ReleaseWorldUI(this)` 자체 호출
- 다중 인스턴스 지원 (한 캐릭터가 동시에 여러 발 맞아도 OK)

### 3.2 `InGameWorldUIWorker.CreateDamageNumber()` (메서드 추가)

- 위치: 기존 `Assets/Scripts/Worker/InGameWorldUIWorker.cs`
- 신규 API:
  - `public DamageNumberWorldUI CreateDamageNumber(int _damage, Vector3 _worldPosition)`
- 내부 구현:
  - 기존 `CreateWorldUI<DamageNumberWorldUI>()`로 풀에서 꺼내기
  - 좌우 ±20px 랜덤 화면 오프셋 계산 후 `SetData()` 호출
  - 캐릭터 등록/관리 로직은 추가하지 않음

### 3.3 데미지 이벤트 → 숫자 노출

- 위치: 기존 `Assets/Scripts/WorldBaseObject/CharacterBase.cs`
- 기존 `OnDamagedClientRpc` 안에서 이미 `OnDamaged(_damage, attacker)` 가상 훅 호출됨
- `CharacterBase.OnDamaged` (베이스 구현)에 한 줄 추가:
  ```csharp
  protected virtual void OnDamaged(int _damage, CharacterBase _attacker)
  {
      Vector3 spawnPosition = transform.position + WorldUIOffset + Vector3.up * 0.3f;
      InGameController.Instance?.WorldUIWorker?.CreateDamageNumber(_damage, spawnPosition);
  }
  ```
- `MonsterBase.OnDamaged`는 이미 `base.OnDamaged()` 호출 → 자동 적용
- 자식 클래스 별도 수정 불필요 (모든 캐릭터 공통)

---

## 4. 데이터 흐름

```
[서버]
  TakeDamageServerRpc → 데미지 적용 → OnDamagedClientRpc(_damage, _attackerId)

[모든 클라이언트] (이미 존재)
  OnDamagedClientRpc
    → m_OnDamaged?.Invoke(_damage, attacker)   (외부 구독자)
    → OnDamaged(_damage, attacker)              (가상 훅, 베이스 구현)
        → InGameWorldUIWorker.CreateDamageNumber(_damage, headWorldPos)
        → 풀에서 DamageNumberWorldUI 꺼내 SetData()
        → 0.6초 후 자체 ReleaseWorldUI() → 풀 반환
```

기존 네트워크 코드 / 이벤트 구조 수정 **없음**.

---

## 5. 애니메이션 / 비주얼 디테일

| 항목 | 값 |
|------|------|
| 수명 | 0.6초 |
| 이동 | 0~0.6초 동안 위로 40px (Ease-Out) |
| 페이드 | 0.4~0.6초 구간에 알파 1 → 0 |
| 스폰 위치 (월드) | `target.position + WorldUIOffset + Vector3.up * 0.3f` |
| 스폰 오프셋 (스크린) | 좌우 ±20px 랜덤 |
| 폰트 | TMP, 크기 24 |
| 채움 색 | #FFFFFF |
| 외곽선 색 | #000000, 두께 0.2 |

---

## 6. 리소스 / 폴더 구조

CLAUDE.md 리소스 규칙 준수:
- 프리팹: `Assets/GameResource/UI/WorldUI/DamageNumberWorldUI.prefab`
- 스크립트: `Assets/Scripts/UI/WorldUI/DamageNumberWorldUI.cs`
- Addressable 등록: 필요 (Address = `DamageNumberWorldUI`)
  - 기존 `CharacterWorldUI`와 동일 패턴 (`InGameWorldUIWorker.InstantiateWorldUI<T>`가 타입명으로 Addressable 로드)

---

## 7. 코딩 규칙 준수 사항

- 모든 멤버 변수 `private`, `m_` 접두사
- `[SerializeField] private` 로 Inspector 노출 (TMP 텍스트, CanvasGroup 등)
- 코루틴 메서드는 `Co` 접두사 (`CoPlayAnimation`)
- 클래스 레이아웃 순서 준수 (const → SerializeField → private → 라이프사이클 → public → private)
- 파라미터는 `_` 접두사

---

## 8. 확장성 (회복 표시 대비)

향후 회복(+30) 표시까지 동일 시스템으로 확장 가능하도록:
- `DamageNumberWorldUI.SetData`에 `Color _textColor` 파라미터를 두되, 이번 작업에선 호출 시 항상 흰색 전달
- 부호(`+`) 처리는 향후 회복 작업에서 추가 (이번엔 음수/양수 분기 없음)

---

## 9. 테스트 / QA

### TestManager 시나리오 (`TestDamageNumber`)

CLAUDE.md Dev-QA 워크플로우 준수:
- `Managers.Test`의 `TestDamageNumber` 시나리오 추가
- 검증 항목:
  1. 플레이어 → 몬스터 공격 시 몬스터 머리 위에 숫자 노출
  2. 몬스터 → 플레이어 공격 시 플레이어 머리 위에 숫자 노출
  3. 동시 다발 피격 시 숫자가 좌우 분산되어 가독성 유지
  4. 0.6초 후 자동 사라짐
  5. 풀 재사용 검증 (반복 피격 시 인스턴스 수가 안정적)
- **TestManager 원칙 준수**: 테스트 전용 로직 금지. 기존 public 메서드 조합만 사용 (`TakeDamage` 호출)

### MCP 자동 검증
- `u_screenshot`으로 피격 순간 캡처 → 숫자 노출 확인
- `u_console` 로그로 풀 동작 확인 (옵션: 디버그 로그)

### 멀티 환경 검증
- 호스트/클라이언트 양쪽 화면에 동일 숫자 노출 확인

---

## 10. 범위 밖 (Out of Scope)

- 회복(+) 표시 — 구조만 확장 가능, 호출은 이번 작업 제외
- 치명타/약점 강조 — 게임에 치명타 시스템 부재
- 데미지 누적 / 콤보 표시
- 화면 측면/하단 텍스트 로그
- 데미지 타입(물리/마법/속성) 구분
- 사운드 / 이펙트 추가 (별도 작업)

---

## 11. 리스크 / 주의 사항

| 리스크 | 대응 |
|--------|------|
| 다수 몬스터가 동시에 피격될 때 풀 부족 | `InGameWorldUIWorker` 풀이 동적 확장 (필요 시 새 인스턴스 생성) |
| 카메라 각도에 따라 숫자가 시야 밖으로 갈 수 있음 | 기존 `CharacterWorldUI`와 동일 좌표 변환 사용 → HP바와 동일하게 동작 |
| 캐릭터 사망 직전 데미지 노출 누락 가능성 | 사망 처리 직전에 `OnDamagedClientRpc` 가 이미 호출되므로 정상 노출됨 (기존 흐름 활용) |
| 풀 반환 시점에 캐릭터가 이미 디스폰된 경우 | 데미지 숫자는 캐릭터 transform을 추적하지 않고 스폰 시점의 월드 좌표만 사용 — 캐릭터 디스폰과 무관하게 정상 동작 |
