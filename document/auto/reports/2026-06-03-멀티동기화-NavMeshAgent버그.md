---
title: 멀티 Transform 미동기화 버그 — NavMeshAgent 비권위 덮어쓰기 (mppm V2~V7)
date: 2026-06-03
area: [Component, WorldBaseObject, Worker]
status: Done
affected:
  - "[[ClientNetworkTransform]]"
  - "[[CharacterBase]]"
  - "[[PlayerCharacter]]"
  - "[[MppmBootstrapWorker]]"
  - "[[InfoManager]]"
team: "단명 subagent 케어 루프 (systematic-debugging, client↔qa 다라운드, run_in_background+워치독)"
---

# 멀티 Transform 미동기화 버그 — 근본원인 NavMeshAgent

mppm-qa V2~V7 멀티 동기화 검증 중 발견. 클론(비권위)에서 host 권위 오브젝트(플레이어·몬스터)의 위치가 스폰 기본값에 frozen. **systematic-debugging으로 근본원인 확정.**

## 근본원인

`CharacterBase.InitializeNavAgent()`가 IsServer/IsOwner 분기 없이 **모든 인스턴스에서 NavMeshAgent를 활성**. 비권위(클론) 인스턴스의 살아있는 NavMeshAgent가 매 프레임 `transform.position`을 자기 내부 위치(스폰 기본값)로 덮어써, NetworkTransform이 수신·적용한 위치를 즉시 되돌림. Player·Monster 공통 원인.

## 진단 경로 (가설 소거)

| 단계 | 결과 |
|---|---|
| 데이터 로드(MPPM Host LoadAllInfo 누락) | 별건 수정([[InfoManager]] `LoadAllInfoOnce`) |
| 버그1: 서버 SetHP/SetCold 미반영 | ❌ 비버그 — `u_play invoke` 0-인자 아티팩트(실-delta는 정상). NetVar 서버→클라 복제 정상 확인(클론 HP가 host regen 추적) |
| 가설: AuthorityMode 권위 | ❌ 기각 |
| 가설: 보간/네트워크시간 (Interpolate=0 테스트) | ❌ 기각 (스냅서도 frozen) |
| 판별: 수신 vs 적용 (`OnNetworkTransformStateUpdated` 계측) | 수신 OK (NTRecv 426건, host 추적) |
| 판별: apply 경로 (`OnTransformUpdated`/`[NTApply]` 계측) | **apply는 pos를 정확히 씀(z→-42)** but 다음 프레임 transform이 -2.666으로 되돌아감 → 후처리 덮어쓰기 |
| **근본원인: NavMeshAgent** | ✅ 확정 — 비권위 Agent가 transform 점유 |

> 핵심 교훈: NTRecv(메시지 핸들러) 발화 ≠ apply 경로 실행 ≠ 최종 transform 값. 단계 분리 계측으로 "수신은 되는데 적용 후 덮어써짐"을 특정.

## Fix

- [[CharacterBase]]: `InitializeNavAgent`에 `ShouldEnableNavAgent()` 게이트. `protected virtual bool ShouldEnableNavAgent() => IsServer`(서버권위=몬스터 기본).
- [[PlayerCharacter]]: `ShouldEnableNavAgent() => IsOwner`(ClientNetworkTransform 오너권위와 일치).
- `MonsterStateMachine.Update`: `IsServer` 게이트(비권위 상태머신 이동 차단 = 2차 재충돌 경로 차단).
- 비권위 인스턴스 = NavMeshAgent 비활성 → NetworkTransform이 transform 구동. 권위 인스턴스 이동은 회귀 없음(Agent 참조부 전부 null/isOnNavMesh 가드).
- [[ClientNetworkTransform]]: 표준 최소형(`OnIsServerAuthoritative()=>false`)으로 정리. 디버그 중 넣은 AuthorityMode 강제·전 계측 제거.

## 검증 (QA, mode: function)

mppm V2~V7 **전부 PASS** + 권위 이동 회귀 없음:
- V2 host player 이동 → 클론 추적(2.610≈2.622). V3 몬스터 스폰위치 전파. V4 HP-10 동기. V5 사망·디스폰·드롭(QA가 `SetHP(0)`→`TakeDamage`로 실사망경로 보정). V6 클론 수집+itemId 식별(클론 Info 로드). V7 Cold 동기.

## 잔여 (멀티 동기화 무관, 클라 확인 권고)

- V6 클론 수집 시 인벤토리 집계 100→99 관찰 — 디버그 사전충전 환경 탓 추정, `AddItem` 집계 경로 확인 권고.
- `WES/NightDarknessOverlay` 셰이더 `_MainTex` 누락 경고 — 기존 UI 셰이더 별건.
- mppm `mppm_collect` 경로 불일치(`Temp/mppm-qa` vs `%TEMP%/wes-mppm-qa`) — probe 직독 우회 중, `MPPM_QA_DIR` 통일 권고.
