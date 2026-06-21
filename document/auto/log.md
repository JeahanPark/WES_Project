---
title: WES Auto-Doc Log
category: log
updated: 2026-05-14
---

# WES Auto-Doc Log

vault 변경 이력. 시간 역순(최신이 위).
형식: `## [YYYY-MM-DD] 작업유형 | 제목`
작업유형: `ingest` / `query` / `lint` / `setup` / `update`

추출 팁: `grep "^## \[" log.md | head -10` 으로 최근 10건 확인.

---

## [2026-06-21] ingest | Stop hook auto-update - new class=2, new signal=0

## [2026-06-21] ingest | Stop hook auto-update - new class=2, new signal=1

## [2026-06-21] ingest | Stop hook auto-update - new class=1, new signal=0

## [2026-06-21] ingest | Stop hook auto-update - new class=1, new signal=1

## [2026-06-08] update | 기획문서 구조 재편 — M2W 평면 → game-design/<주제>/ 통일

- `design/` 직속 M2W 8개 → `game-design/<주제>/기획.md`로 git mv (client-spec 쌍 구조와 1:1 정합). design/ 직속엔 CORE 비전 2개만 잔존.
- 시점성·완료 문서 3개(NEXT_기획작업_스케줄·M2W2_콘텐츠확장_구현계획·mppm-qa-design-brief) → `design/_archive/`.
- 신규 출시 기획: [[출시_9시간_관통구조]](트레이드오프 압박 곡선+난이도 공식) · [[날씨_시스템]](불완전정보 척추).
- 링크 수정: catalog/Design/index.md 경로·상태 갱신, 지형경사 코드링크 깊이 보정, director.md 경로 규칙 갱신.

## [2026-06-05] ingest | Stop hook auto-update - new class=1, new signal=0

## [2026-06-04] fix | 클리어 난이도(EscapePoint 종단) + 이동 경로추종 근본버그 수정

- EscapePoint `(-3,1,-38)`→`(0,1.5,35)`: 스폰 옆 4u→종단 77u, Beach·Forest 몬스터 관통해야 클리어
- 근본버그: [[PlayerCharacter]] `HandleInput`이 매 프레임 `MoveWithDirection(zero)` 호출 → [[CharacterBase]] 경로추종이 첫 프레임 취소됨
- fix(3): `MoveWithDirection` 실입력 시만 추종 취소 / `MoveTo` 목적지 NavMesh 투영+PathComplete만 / `UpdateMovement` 게이트 수직흡수(3.0)+수평차단(0.6) 언덕 통과
- infra: `ProjectSettings.runInBackground` 0→1 (플레이 중 포커스 상실 시 McpBridge 무응답 해소)
- 검증: FullPlay E2E "실제 이동 도달 OK"(Warp 폴백 아님) PASS
- 리포트: [[reports/2026-06-04-클리어난이도-이동추종버그]]

## [2026-06-03] ingest | 멀티 Transform frozen 버그 근본수정 (mppm V2~V7 PASS)

- 근본원인: 비권위 인스턴스 NavMeshAgent가 NetworkTransform 적용 위치를 매 프레임 덮어씀(Player·Monster 공통)
- fix: [[CharacterBase]] `ShouldEnableNavAgent()` 권위 게이트(몬스터 IsServer/[[PlayerCharacter]] IsOwner), MonsterStateMachine 서버 게이트, [[ClientNetworkTransform]] 표준 최소형 정리
- 부수: [[InfoManager]] `LoadAllInfoOnce`(MPPM Host/Client Info 로드 일원화, 버그3), 버그1=비버그 확정
- systematic-debugging 가설소거(권위✗·보간✗→수신/적용 분리계측→NavMeshAgent 확정), 리포트 [[2026-06-03-멀티동기화-NavMeshAgent버그]]
- 스케줄 mppm V2~V7 [x]

## [2026-06-03] ingest | 도면 해금(②) 구현·검증 — QA PASS 16/0

- 신규 [[RecipeUnlockRegistry]](세션·클라 로컬 해금)·[[BlueprintToast]] 카탈로그, BlueprintInfo.csv·도면3종(401~403)
- 수정: [[InfoManager]](Blueprint 조회), [[WorldDropItem]](줍기→Unlock 분기), [[InGameObjectDataWorker]](ResetSessionData), CraftPopup 잠금표시·제작게이트
- 리포트 [[2026-06-03-도면해금]], Design index 도면해금 ❌→✅ · ② 분기 🔧
- 단명 케어 루프: director→client(명세)→[designer∥client코어]→client(UI)→qa

## [2026-06-03] ingest | Stop hook auto-update - new class=5, new signal=0

## [2026-06-03] ingest | Cold 실질화(추위 위협) 구현·검증 — QA PASS 20/0

- 신규 [[ColdDamageWorker]](밤 Cold 누적+단계 HP틱), [[DayNightConfig]]·[[WorldBuildingObject]]·[[InfoManager]] 카탈로그
- [[DayNightWorker]] role 갱신(자연 감쇠+밤멀티 1.0)
- 버그2건 수정: 밤멀티 에셋 2.0→1.0, MPPM Host Info 로드 누락(`LoadAllInfoOnce` 멱등 래퍼)
- 리포트 [[2026-06-03-Cold실질화]], Design index Cold/시간낮밤 → ✅
- 단명 subagent 순차 케어 루프(run_in_background+워치독) 첫 실전

## [2026-06-03] update | Design 카탈로그 레이어 신설 — 게임 기획을 위키로 추적

- 신규: [[catalog/Design/index]](기획 카탈로그 — 시스템별 구현상태 표), [[catalog/Design/코어비전_4분기]]
- index.md에 Design Catalog 섹션 추가
- 연계: `design/CORE_갭보완_로드맵.md`(신규 로드맵), `WES_Schedule.md`(현재상태+갭로드맵 반영)

## [2026-06-03] ingest | mppm-qa 멀티QA 도구 Ingest — new class=2, updated=3, deleted=1

- 신규: [[MppmBootstrapWorker]](Worker), [[MultiplayerQaProbe]](Component)
- 갱신: [[GameNetworkManager]](로컬 직결+IsNetworkConfigured), [[InGameController]](m_NetworkObject 제거·parent를 NetworkGameController로 정정), [[TestManager]](MPPM 트리거)
- 삭제: MultiplayerTestBootstrap.md (소스 클래스 삭제됨 — 고아 페이지)
- 리포트: [[reports/2026-06-03-mppm-qa-멀티QA도구]]

## [2026-05-31] ingest | Stop hook auto-update - new class=1, new signal=0

## [2026-05-31] ingest | Stop hook auto-update - new class=1, new signal=0

## [2026-05-17] update | 낮밤 모닥불 컷아웃 셰이더 재작성 (라운드4) — PASS

## [2026-05-17] update | 낮밤 시나리오 4 시각 캡처 (라운드3) — 모닥불 컷아웃 FAIL 신규 이슈

## [2026-05-17] update | 낮밤 게임플레이 검증

## [2026-05-17] query | 주간 작업 우선순위 제안

## [2026-05-14] setup | Karpathy 패턴 도입 (index + log + Sources)

- `auto/index.md` 생성 — LLM 진입점 표준화
- `auto/log.md` 생성 — 이 파일
- `auto/Sources/` 폴더 신설
- 첫 외부 소스로 [[Sources/karpathy-llm-wiki|Karpathy LLM Wiki gist]] 보관 (.original.md = 원본 전문 archive)
- `_templates/Source.md` 신규 — 외부 소스 페이지 양식

## [2026-05-14] ingest | WES 메인 클래스 카탈로그 초기 시드 (26)

- `Assets/Scripts/{Manager,Controller,Worker,Component}/` 스캔
- 클래스 26개 → `catalog/Class/*.md` 생성 (Manager 10, Controller 4, Worker 8, Component 4)
- 이벤트 1개 → `catalog/Signal/DayNightWorker.OnPhaseChanged.md`
- 카테고리별 클래스 다이어그램 4개 (Mermaid) — 이후 Canvas 도입으로 사실상 deprecated
- Canvas 시각화 [[diagrams/class/WES-Class-Overview]] 생성

## [2026-05-14] setup | Obsidian + Dataview + Discord 환경 검증

- Obsidian v1.12.7 사일런트 설치
- vault 등록 (`%APPDATA%\obsidian\obsidian.json`)
- Dataview v0.5.68 사전 설치, community-plugins.json에서 활성화
- Discord webhook 테스트 메시지 전송 성공
- 샘플 카탈로그 2개 + 샘플 시그널 1개 + 샘플 리포트 1개 + Dataview 뷰 3개 생성

## [2026-05-14] setup | vault 초기화

- `document/` 를 vault root로 결정
- `auto/` 폴더 신설 (에이전트 자동 생성 영역)
- 폴더 트리: `catalog/{Class,Signal}/`, `diagrams/{class,sequence}/`, `reports/`, `views/`, `_templates/`
- `auto/README.md` 작성
- 상위 [[../obsidian_agent_plan|스펙 문서]] v3.1로 정착 (Notion → Obsidian, lat.md 제거)
2026-06-21 R1-T1 CSV 스키마 확장(신규 컬럼 5종+신규 CSV 2종 WeatherInfo/WorldAreaWeatherInfo)·InfoConvert partial화·파싱 검증 — reports/2026-06-21-R1T1-CSV스키마확장.md
2026-06-21 R1-T2 난이도 공식 D=TP/CAP 코드화 — DifficultyFormula(순수 모듈, PhaseMul/WeatherMul 어댑터, WeatherMul은 T1 WeatherInfo 조회) + TestManager.TestLogDifficulty 프로브. 런타임 검증 PASS(D 구성요소 명세 일치)
2026-06-21 R1-T6 날씨 시스템 — WeatherWorker(서버권위 NetworkVariable, 페이즈 전환 틱, 지역 분포 샘플→심각도 사다리 1단계 이동=전조 보장) Ingame씬 DayNightWorker GO에 부착. TestWeatherTransitions 검증 PASS(점프0·분포내)
2026-06-21 R1-T5 이동비용 — MoveCostWorker(서버, 위치델타 이동감지→Cold 가속, WorldAreaInfo.MoveCostMultiplier·WeatherInfo.MoveCostMul·야간 배수) Ingame씬 부착. TestMoveCostProbe 검증 PASS(정지-6 vs 이동-1, 이동비용 격리)
2026-06-21 R1-T3 도구 등급(① 효율) — ToolTierSystem(보유 최대 ItemInfo.ToolTier→채집 배수 1+0.5·tier) + WorldEntityBase.ExecuteDrop 채집 스케일 연동. ItemInfo.ToolTier 컬럼(전 항목 0). TestLogToolTier 검증 PASS. 실제 도구 아이템은 R3
2026-06-21 R1-T4(부분) 덫 — TrapSystem.TriggerTrapDamage(범위 몬스터 피해 코어) + TestTrapDamage 검증 PASS(적중1, HP100→83). 덫 건물 프리팹·설치·근접발동·표지 UI는 R2~R4
2026-06-21 R1-T7 통합 QA — 전 시스템 1세션 공존·프로브 일괄 에러0. R1 통합보고(reports/2026-06-21-R1-통합보고.md). 코드 토대 완료, 콘텐츠·6지역 연동 R2~R4
2026-06-21 R2 Phase1 합의(6지역 공간, 미결0)·Phase2 슬라이스1 코드토대 — InGameAreaBandWorker(선두Z→지역d→Weather/MoveCost.SetArea, 단조증가+히스테리시스)·AreaGateComponent 스켈레톤·WorldAreaInfo AxisMin/Max(CSV 단일진실원). 씬 부착+와이어링(team-lead MCP)·플레이검증 PASS(z-40→a1, z10→a2, z51→a3, 역행 a3유지). 프로브 Warp 수정.
2026-06-21 R2 슬라이스2a — MapGenerator 6지역 선형종단 회랑 개편(원형섬→Z -70~203 6밴드, CSV WorldAreaInfo 단일진실원 파싱, 지형벽 시야차폐) + CSV 6지역(WorldArea/Weather17/Monster13행+신규몬스터10종 placeholder). 메뉴 Validate(PASS16/0)·Generate·Bake 실행. 플레이검증 6지역 밴드전환 PASS(a1~a6 단조증가). 지형아트·관문배치·몬스터모델은 2b.
2026-06-21 R2 2b-1 level-design — Area3~6 마커에 MonsterSpawnArea 부착(AreaId 1~6 유니크)+InGameAreaWorker.m_SpawnAreas 6개 등록. 플레이검증 PASS(Initialized 6 spawn areas, Area6 z184 몬스터 스폰). 보고는 포맷붕괴였으나 디스크검증으로 작업 확인. 잔여: stale Area3_Mountain 마커 정리(2b-2)
2026-06-21 R2 2b-2 designer — 6지역 procedural 틴트 머티리얼(Generic_01_A_Beach~Ruins, Z밴드 할당 메뉴 Release6RegionTerrainSetup)·stale 마커 정리(빈 Area3_Swamp 삭제+Area3_Mountain→Area3_Swamp rename)·몬스터모델 백로그. 지역 색구분 캡처 확인. ⚠MapGenerator 재실행 시 틴트 소실→메뉴 재실행 필요.
2026-06-21 R2 Phase3 통합 QA PASS(6지역 밴드전환·6스폰·에러0, 2b 편집 후 무회귀) + R2 통합보고(reports/2026-06-21-R2-6지역공간-통합보고.md). R2 기능코어 완료, 아트/몬스터메쉬/관문/튜닝 R3~백로그.
2026-06-21 R3 Phase1 콘텐츠 스코프 합의(슬라이스 A/B/C, director 결정: 등급만/leash×1.5/도면중복무효/평화반격) + R3-A 도구 실아이템(도끼501~503·곡괭이511~513 ToolTier1/2/3 + 레시피6종). 검증 PASS(인벤24종, 철도구 보유→채집배수 x2.5, R1-T3 실아이템 활성화). 실수치 R5·아이콘 designer 백로그.
2026-06-21 R3-B 몬스터 추격/공격 행동 — MonsterChase/AttackState+Perception+BehaviorType(Pack/Charge)+MonsterInfo 5컬럼(MoveSpeed/DetectRange/AttackRange/AttackCooldown/BehaviorType)+leash×1.5+피격반격. ★근본버그 수정: 전 몬스터가 placeholder프리팹 baked id=1로 고정→SetMonsterId 스폰주입(몬스터별 HP/ATK/드롭/행동 활성, R1/R2부터 잠재). 검증 PASS6/0(추격 4.0→1.0·공격 HP감소·leash·반격, 몬스터별 스탯).
2026-06-21 R3-C 완료 — 몬스터 행동 5종(Poison DoT/Ranged 히트스캔/Stealth 가시성NV/WeatherBuff 가속/Boss 페이즈66·33%) + 도면 월드스폰(A안 세션1회 area스폰·결핍) + 산~폐허 BehaviorType. 검증 PASS: 도면스폰5/0·Poison3/0·Boss4/0·Stealth3/0. 투사체·보스모델·Transparent머티리얼·ATK강화 백로그.
2026-06-21 R3 통합보고 — 콘텐츠 기능 완료(도구등급·몬스터행동6+보스·도면스폰), 근본버그(MonsterId) 수정으로 R1/R2 몬스터 차별화 소급 유효. 비주얼/메쉬 R4 백로그. reports/2026-06-21-R3-콘텐츠-통합보고.md
2026-06-21 R4 ①②③ 골격 — ①CoreTensionOverlayWorker 날씨(OnWeatherChanged)/이동 연출+가시성천장0.6 ②NarrationData SO(폴백25)+ItemInfo 플레이버8+BlueprintToast 오버로드 ③AudioManager(4채널 무음 null가드)+AudioKey+Managers.Audio. AudioManager 미등록 Addressable키 InvalidKeyException→location 사전확인으로 무음완주 수정(team-lead). 검증: 컴파일0, 날씨전환 시 훅 무에러. MCP 무응답 중 client_r4 코드레벨작업→복구 후 검증. 후킹12·designer슬롯3·실음원 백로그.
2026-06-21 R4 날씨 오버레이 시각화(designer) — rain_edge/fog_veil/snowstorm_white procedural 텍스처 + Canvas_CoreTension(-10)에 Image3 생성·CoreTensionOverlayWorker m_RainEdge/FogVeil/SnowstormOverlay 와이어링. 검증: 날씨전환 시 fog_veil 화면 렌더 확인(실경로 OnWeatherChanged→Worker, 가시성천장). 에러0.
