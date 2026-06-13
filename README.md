# 🏝️ WES — 야생 생존 탈출 (Wild Escape Survival)

> **Unity 6 · Netcode for GameObjects 기반 1~4인 협동 생존 게임 (PC / Steam 목표)**
> 기획 · 클라이언트 · 네트워크 · QA를 **1인 개발**하며, **AI를 개발 파이프라인에 직접 결합**한 프로젝트

![Unity](https://img.shields.io/badge/Unity-6000.0.60f1-black?logo=unity)
![NGO](https://img.shields.io/badge/Netcode_for_GameObjects-2.8.0-blue)
![Lang](https://img.shields.io/badge/Language-C%23-178600)
![Platform](https://img.shields.io/badge/Platform-PC(Steam)-1b2838?logo=steam)

| | |
|---|---|
| **장르** | 탑뷰 생존 / 협동 / 탈출 (Don't Starve Together · Escape from Duckov · 워크3 야생탈출 UMS 참고) |
| **플레이 인원** | 1 ~ 4인 협동 (호스트-클라이언트) |
| **세션 방식** | 저장 없는 로그라이크 — 매 판이 새로운 도전 |
| **개발 형태** | 1인 개발 (기획·클라이언트·네트워크·QA 전 영역) |
| **규모** | C# 약 127개 스크립트 / Manager·Controller·Worker·Component 아키텍처 |

---

## 1. 게임 개요

폭풍에 난파되어 섬 해변에 떠밀려 온 생존자들이, 혹한과 맹수·끝없는 밤을 견디며 **섬 반대편의 안전한 마을(목표지점)까지 협력해 탈출**하는 게임입니다.

- 야생 환경에서 제한된 자원으로 **생존** (HP·체온·자원 관리)
- 자원 채집 → **제작** → **건축** → 전투 → **탈출**로 이어지는 핵심 루프
- 저장 없음 — 세션 종료 시 진행도 초기화 (로그라이크)

**게임 흐름**

```
Intro (START)  →  Lobby (방 생성/참가 · 방장 START)  →  InGame
                                                          │
                          ┌───────────────────────────────┤
                          │ 생존: 이동 / 전투 / 채집 / 제작 / 건축
                          │
                          ├─ [탈출 성공] 목표지점 도달 → 클리어 → 로비
                          └─ [전멸] 전원 사망 → 게임오버 → 로비
```

---

## 2. 기술 스택

| 분류 | 기술 |
|------|------|
| **엔진** | Unity 6 (6000.0.60f1), URP |
| **언어** | C# |
| **네트워크** | Netcode for GameObjects 2.8.0, Unity Transport, Relay, Authentication |
| **입력** | Input System 1.14 |
| **카메라** | Cinemachine 3.1 |
| **에셋 로딩** | Addressables 2.7 |
| **비동기 / 리액티브** | UniTask, UniRx |
| **AI 네비게이션** | Unity AI Navigation (NavMesh) |
| **데이터** | CSV → C# 클래스 코드 생성 (커스텀 에디터) + 런타임 Reflection 파싱 |

---

## 3. 아키텍처

게임 로직을 **역할별 4계층**으로 분리해, 1인 개발에서도 책임이 섞이지 않도록 구성했습니다.

| 계층 | 역할 | 예시 |
|------|------|------|
| **Manager** (`MonoSingleton`) | 전역 시스템 — 씬·입력·리소스·네트워크·팝업·테스트 | `Managers`, `GameNetworkManager`, `InputManager`, `PopupManager`, `ResourceManager` |
| **Controller** | 씬 단위 흐름 제어 | `IntroController`, `LobbyController`, `InGameController`(네트워크) |
| **Worker** | 인게임 도메인별 실무 로직 | `InGamePlayWorker`(스폰·드롭), `InGameSpawnWorker`(네트워크 풀링), `DayNightWorker`(낮밤), `BuildingPlacementWorker`(건축) |
| **Component** | 개별 오브젝트 동작 | `ClientNetworkTransform`, `NightVisionComponent`, `MonsterSpawnArea` |

**핵심 설계 포인트**
- **씬 컨트롤러 제네릭 베이스**: `GameController<T>` / `NetworkGameController<T> : NetworkBehaviour` 로 일반 씬과 네트워크 씬을 분리
- **데이터 주도**: 아이템·제작·건물·몬스터·드롭테이블·지역을 모두 CSV로 관리(`WES/Assets/CSVInfo/`) — 커스텀 에디터(`InfoConvertEditor`)가 CSV를 C# 클래스로 코드 생성하고, 런타임에 `InfoManager`가 Reflection으로 파싱해 `List<T>`로 적재
- **코딩 규칙**: `m_` 멤버 접두사, `_` 매개변수 접두사, public 필드 금지, 9단계 클래스 레이아웃 통일

---

## 4. 주요 시스템

| 시스템 | 내용 |
|--------|------|
| **멀티플레이 동기화** | NGO 기반 호스트-클라이언트. `ServerRpc`/`ClientRpc` + `ClientNetworkTransform`으로 이동·상태 동기화, 네트워크 프리팹 런타임 등록 |
| **생존 / 스탯** | HP·HPRegen·ATK·DEF·MoveSpeed·Cold(체온). 피해 공식 `ATK - DEF`(최소 1) |
| **인벤토리 / 퀵슬롯** | 그리드 인벤토리(드래그 이동), 1~8 퀵슬롯(장비·소비·건물 공용), 레지스트리 기반 데이터 관리 |
| **제작 / 건축** | CSV 제작 레시피 + 조건(체온 등), `BuildingPlacementWorker`로 월드 배치 |
| **낮밤 / 생존 압박** | `DayNightWorker`(네트워크 동기화 페이즈), 밤 전용 몬스터·시야(`NightVisionComponent`) |
| **탈출** | `EscapePoint` 도달 시 클리어 판정 |

---

## 5. 🤖 AI 개발 파이프라인 (이 프로젝트의 핵심 실험)

1인 개발의 속도·완성도를 끌어올리기 위해, **AI를 단순 코드 생성 도구가 아니라 개발 파이프라인 전체에 결합**했습니다. 핵심 설계와 판단은 직접 쥐되, 반복 작업·검증·문서화를 AI가 자동화하는 구조입니다.

### 5.1 MCP — AI가 Unity Editor를 직접 제어

직접 구현한 **MCP(Model Context Protocol) 서버**(C# / .NET)를 통해 AI가 Unity Editor를 명령으로 조작합니다. Named Pipe로 에디터 내 `McpBridge`와 통신하며, **14종 도구**를 제공합니다.

| 영역 | 도구 | 하는 일 |
|------|------|---------|
| GameObject | `u_editor_gameobject` | 생성·삭제·계층 조회·검색 |
| 컴포넌트 | `u_editor_component` | 컴포넌트 추가·Inspector 참조 연결·버튼 onClick 바인딩 |
| 프리팹 | `u_editor_prefab` | 프리팹 인스턴스 배치 |
| 에셋 | `u_editor_asset` | 검색·정보 조회·AssetDatabase 갱신 |
| 씬 | `u_editor_scene` | 열기·저장·생성 |
| 입력 | `u_editor_input` | InputAction 추가·제거·조회 |
| 메뉴 | `u_editor_menu` | 커스텀 에디터 메뉴 실행 (NavMesh 베이크·맵 생성 등) |
| 태그/레이어 | `u_editor_tag_layer` | 태그·레이어 관리 |
| Transform | `u_set_transform` | 위치·회전·스케일 (에디터/플레이 모드) |
| 플레이모드 | `u_play` | 플레이 진입·종료·UI 클릭·런타임 메서드 호출 |
| 콘솔 | `u_console` | 컴파일/런타임 로그 조회 |
| 캡처 | `u_screenshot`, `u_editor_sceneview` | 게임뷰·씬뷰 스크린샷, 카메라 시점 제어 |
| UI 생성 | `generate_ui_with_gpt` | 자연어 → UGUI 프리팹 자동 생성 |

→ 덕분에 AI가 **프리팹 구성 → 컴포넌트 와이어링 → 플레이모드 진입 → 콘솔 에러 확인 → 수정**을 사람 개입 없이 한 사이클로 수행합니다.

### 5.2 다중 에이전트 협업

게임 개발 역할을 **전문 에이전트 풀**(director·client·designer·qa·sound·story·level-design)로 나누고, 표준 프로세스(부트스트랩 → 기획 합의 → 슬라이스 구현 → 통합 QA → 마무리)에 따라 주제별로 선택 협업하도록 구성했습니다.

| 에이전트 | 역할 |
|----------|------|
| **director** | 게임플레이·콘텐츠·톤만 사고 → 기획서 작성 |
| **client** | Unity/C#/아키텍처 관점에서 구현 가능성·비용 검토 → 코드명세 작성 |
| **designer** | 리소스·UI 프리팹 (자산 우선순위 트리로 재사용/차용/생성 결정) |
| **qa** | 기능 QA(플레이모드 시나리오) + UI QA(시각 검수) |
| **sound / story / level-design** | 사운드 생성 · 서사 텍스트 · 레벨/밸런스 데이터 |

각 에이전트가 메일박스로 토론하며 기획↔구현을 합의하고, 진행 상황은 **Discord로 공유**됩니다.

### 5.3 플레이모드 QA 자동화

**개발 → QA → 판정** 사이클로, AI가 실제 플레이모드에서 기능을 검증합니다. 핵심은 단언(assert) 기반 시나리오와 MCP 구동 루프입니다.

**① 시나리오 — `TestManager`** (`#if UNITY_EDITOR` 전용 `MonoSingleton`)
- **테스트 전용 게임 로직을 새로 만들지 않고 기존 public 메서드만 조합**하는 원칙 (`inventory.AddItem`, `player.TakeDamage`, `Managers.Popup.Open` 등)
- 각 시나리오는 코루틴으로 동작을 실행하고 단계마다 `Mark(기대조건, 라벨)`로 **결과 상태를 단언**한 뒤 `PASS n / FAIL n`으로 집계
- 검증 대상 예: HP·스탯 변화, 제작 시 **재료 차감 + 결과 아이템 지급**, 팝업 스택 `OpenedCount`, 건축 `IsPlacing` 전이, NavMesh 적재·carving — 정상 경로뿐 아니라 **재료 부족·조건 미충족·빈 스택 같은 엣지 케이스까지** 시나리오로 분리

**② 구동 — MCP 루프**
- `u_play` 로 플레이모드 진입 후 런타임에서 테스트 메서드 호출
- `u_console` 로 `PASS/FAIL`·런타임 에러 로그 수집 → 판정
- `u_screenshot` / `u_editor_sceneview` 로 시각 결과까지 교차 확인

**③ 범위 — QA 에이전트 3모드**
- **function**: 위 `TestManager` 시나리오 + 컴파일·씬 와이어링 검증
- **ui**: UI 자동 검사 + 시각 검수 리포트 (수정 없이 리포트 전용)
- **e2e**: 자작 도구 `wesqa`로 유저 관점 풀플레이(스폰→탈출 도달, 타임아웃·도착 판정) 및 seeded-bug 효과 측정

### 5.4 Obsidian 자동 문서화 (Karpathy LLM Wiki 패턴)

코드 변경에 맞춰 에이전트가 **클래스 카탈로그·작업 리포트·변경 로그를 Obsidian 위키(`document/auto/`)로 자동 갱신**합니다. 혼자 개발하면서도 문서가 코드와 함께 살아 있도록 유지하는 구조입니다.

- **Wiki-First**: 작업 전 vault부터 확인 → 부족하면 코드 탐색 후 vault 반영
- **Trust-but-Verify**: vault는 빠른 참조용, 진실은 항상 코드 — 코드 수정 시 실제 코드 재검증
- **작업 어휘**: Ingest(코드 변경 시 카탈로그 갱신) / Query(작업 전 파악) / Lint(주기적 모순 점검)

### 5.5 자작 E2E QA 도구 — `wesqa`

플레이모드 QA(5.3)가 코드 내부 상태를 단언한다면, `wesqa`는 **유저 관점에서 화면을 보고·누르고·검증**하는 E2E 자동화 도구입니다. 오픈소스 [Poco](https://github.com/AirtestProject/Poco)에서 모바일 드라이버·airtest 의존을 걷어낸 **최소 포크**에, WES 전용으로 직접 만든 게임 내장 서버를 붙여 구성했습니다.

**① 게임 내장 C# 서버** (`Assets/WesQA/Runtime/`)
- `WesPocoServer` — 게임에 TCP 서버(`localhost:5001`)를 직접 내장, `JsonRpc`로 통신
- `HierarchyDumper` — 런타임 UI 계층을 덤프해 **이름으로 노드 조회**를 노출
- `InputInjector` — 조회한 노드에 클릭·입력 주입
- `Screenshotter` — 화면을 캡처해 base64로 전송
- `InvokeBridge` / `RpcMethods` — 런타임 메서드 원격 호출

**② Python 클라이언트** (`tools/wesqa/`)
- `poco/` — Poco 최소 포크 (위 자작 서버에만 TCP로 접속)
- `aircv/` + `vision.py` — 스크린샷 base64를 OpenCV로 디코드하고, **템플릿 매칭·SIFT 키포인트 매칭으로 "특정 UI가 화면에 떴는지"를 이미지로 판정**

```
Python(WesPoco)  ──TCP / JSON-RPC──▶  게임 내장 서버(WesPocoServer)
  game('btn_inventory').click()         HierarchyDumper(노드 조회) + InputInjector(클릭)
  vision.find_template(shot, tpl)  ◀──  Screenshotter(base64 스크린샷) → aircv 매칭
```

MCP `u_screenshot`(에디터 단발 캡처)과 달리, **노드 조회 → 입력 → 이미지 매칭**을 코드로 묶어 스폰부터 탈출까지 유저 플로우를 자동 검증하고, seeded-bug로 검출력까지 측정합니다.

---

## 6. 저장소 구조

```
WES_Project/
├─ WES/                      # Unity 프로젝트 루트
│  └─ Assets/
│     ├─ Scripts/
│     │  ├─ Manager/         # 전역 매니저 (MonoSingleton)
│     │  ├─ Controller/      # 씬 흐름 제어 (Intro/Lobby/InGame)
│     │  ├─ Worker/          # 인게임 도메인 로직 (스폰·낮밤·건축·카메라)
│     │  ├─ Component/       # 오브젝트 단위 동작 (네트워크 트랜스폼 등)
│     │  ├─ WorldBaseObject/ # 캐릭터·몬스터·건물·드롭아이템 베이스
│     │  ├─ UI/              # HUD · Popup · Scroll · WorldUI
│     │  ├─ Info/            # CSV → C# 클래스 코드 생성 + Reflection 파싱 데이터 레이어
│     │  └─ Editor/          # 맵 생성·아이콘 생성 등 커스텀 에디터 툴
│     ├─ CSVInfo/            # 아이템·제작·건물·몬스터·드롭·지역 데이터
│     ├─ GameResource/       # 게임 리소스 (프리팹·이미지·애니메이션)
│     ├─ MCP_Unity_Plugin/   # AI ↔ Unity Editor 브리지 (MCP)
│     └─ WesQA/Runtime/      # E2E QA 도구의 게임 내장 TCP 서버 (자작)
├─ tools/
│  └─ wesqa/                 # E2E QA Python 클라이언트 (Poco 최소 포크 + aircv 비전)
└─ document/
   ├─ WES_GDD.md             # 게임 기획서
   └─ auto/                  # AI 자동 문서 위키 (Obsidian)
```

---

## 7. 개발 현황

핵심 루프(채집·제작·건축·전투·탈출)와 멀티플레이 동기화, 낮밤 시스템이 동작하는 상태이며, AI 파이프라인을 활용해 콘텐츠를 지속 확장 중입니다. (현재 버전 0.1.0)

**콘텐츠**

| 분류 | 수량 | 비고 |
|------|------|------|
| 아이템 | 15종 | 자원(1~99) · 소비(101~) · 장비(201~) · 건물(301~) |
| 제작 레시피 | 8종 | 건물/아이템 카테고리, 재료 차감 + 결과 지급 |
| 몬스터 | 3종 | 해안가/숲/산지, 상태머신(Idle/Walk/Hit/Death) + NavMesh |
| 건물 | 2종 | 모닥불(체온 회복) · 횃불 |
| 맵 | 1 | Synty 기반 섬 (해안가→숲→산지), NavMesh Baked |

**구현 완료** — 이동·전투·스탯·인벤토리(그리드 드래그)·퀵슬롯(전 아이템)·제작·건물배치·몬스터AI·드롭테이블·탈출·낮밤·CSV로딩·NGO Host/Client
**다음 단계** — UI/UX 정리 · 사운드 · 네트워크 안정화(4인) · 채팅

> 전체 시스템 명세는 [document/WES_GDD.md](document/WES_GDD.md) 참조.
