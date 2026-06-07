# 디렉터 / 클라이언트 / 디자이너 / QA / 사운드 / 스토리 / 레벨디자인 팀 에이전트 표준 프로세스

> **대상 독자**: WES 프로젝트에서 팀 에이전트 작업을 시작하려는 메인 세션(team-lead)
> **참조 문서**: [director.md](director.md), [client.md](client.md), [designer.md](designer.md), [qa.md](qa.md), [sound.md](sound.md), [story.md](story.md), [level-design.md](level-design.md), [RESOURCE_INVENTORY.md](../../../document/RESOURCE_INVENTORY.md)
> **에이전트 풀은 최대 7인. 매번 전원 spawn하지 않는다 — 주제에 필요한 멤버만 골라 띄운다 (아래 "에이전트 풀" 표 참조).**

새로운 게임 시스템 주제(예: "Cold 실질화", "도면 해금")가 들어왔을 때, **본 문서의 4 Phase를 순차로 따른다**. 즉흥 운영 금지.

---

## 0. 운영 모델 선택 — 깨짐·낭비 방지 (최우선, Phase 0보다 먼저)

> **2026-06-03 추가.** 멀티QA 작업에서 지속형 팀의 `client`가 컨텍스트 누적(366턴·누적 처리량 ~10.5만 메시지)으로 **도구호출 포맷이 붕괴(XML이 텍스트로 누출)되어 멈춘** 사례에서 도출. 이 절을 따르지 않으면 같은 실패가 재발한다.

### 0-1. 이 작업에 팀이 맞는가? (먼저 분기)
```
Q1. 독립 워커가 동시(병렬)에 할 수 있나?
    아니오(순차·종속·같은 파일 편집) → ❌ 팀 금지 → 단일 세션
    예 ↓
Q2. 워커끼리 논의·반박이 필요한가?
    아니오(결과만 받으면 됨) → 🔹 단명 subagent 병렬 호출
    예(서로 도전·조율)         → 🔸 에이전트 팀 (아래 Phase 0~4)
```
- 공식 문서 명시: **"순차·종속·같은 파일 편집 = 팀 비효율 → 단일 세션/subagent."**
- 예) 멀티QA(기획→코드명세→구현→검증)는 순차·종속 → **단일 세션이 정답**(실증됨). 팀으로 돌렸더니 client가 멈춤.
- 팀이 빛나는 경우: 병렬 리뷰(보안/성능/테스트 렌즈), 경쟁 가설 디버깅, 독립 모듈 분담.

### 0-2. 팀/subagent를 쓰더라도 — 단명 특화 원칙 (N² 깨짐 방지)
비용·깨짐 ∝ **N²** (N = 한 에이전트 수명/턴 수). 역할 특화(렌즈)는 품질을 올리므로 **유지**하되, **수명만 짧게**:
- **1 작업 = 1 에이전트** — 자기완결 산출물(파일 1·코드 1·판정 1)을 내고 **즉시 종료**. 프로젝트 내내 살려두지 말 것.
- **기억은 파일에** — 상태를 `기획.md`·`코드명세.md`·코드·상태파일에 둔다. 에이전트가 죽어도 다음이 파일 읽고 이어감.
- **수명 캡** — 한 멤버가 ~30턴 또는 체크인 없이 길게 돌면 강제 점검(장수 = 깨짐).
- **토론도 파일 릴레이로** — 채팅 누적 대신 문서를 주고받는다(핑퐁이 전원 N을 폭증시킴).

### 0-3. 멈춤 감지 + 재-spawn (공식 실패모드 대응)
공식 문서: *"팀원은 오류 후 복구 없이 멈출 수 있다 → 대체 팀원 생성."*
- **감지 신호**: 도구 실행 0인 빈 턴 / 같은 idle 반복 / 같은 작업 무진전 3회.
- **확인법**: 트랜스크립트 `~/.claude/projects/<sess>/subagents/agent-*.jsonl` tail — assistant **텍스트 블록에 `invoke name=`가 박혀 있으면** = 도구호출이 텍스트로 누출(포맷 붕괴). 같은 컨텍스트로 더 깨워도 무한 반복.
- **복구**: 그 멤버 종료 → **짧은 핸드오프(현재상태 + 남은작업 + 파일경로)로 재-spawn.** 전체 history를 물려주지 말 것 — 새 컨텍스트가 곧 치료.

### 0-3b. 자동 케어 루프 — 워치독 폴링 (2026-06-03 채택)

> 감지를 사람 눈/디스코드 미러에 의존하지 않는다. 미러는 *정상 메시지*만 보내 멈춤을 못 잡는다. 진짜 신호는 **서브에이전트 트랜스크립트**에 있다.

**전제 — 에이전트는 `run_in_background: true`로 띄운다.** foreground로 띄우면 메인 턴이 블록돼 진행 중 점검 자체가 불가능하다.

**케어 루프 (team-lead/메인 세션이 수행):**
1. 작업 에이전트를 `run_in_background`로 dispatch. 반환된 agentId를 in-flight 목록에 보관.
2. `ScheduleWakeup`(120~180초)마다 워치독 실행:
   ```
   powershell -ExecutionPolicy Bypass -File .claude/scripts/agent-watchdog.ps1 -Ids <in-flight-ids> -StaleSec 180
   ```
   - 실경로: `~/.claude/projects/c--GitFork-WES-Project-WES/<세션>/subagents/agent-*.jsonl` (스크립트가 최신 세션 자동탐지)
   - 산출: `STALL_BREAKDOWN`(포맷붕괴=최강신호) / `STALL_HUNG`(마지막 tool_use 무응답) / `IDLE` / `OK`. 멈춤 존재 시 **exit 2**.
   - **`-Ids`로 in-flight만 검사** — 완료된 에이전트는 IDLE로 보여 오탐되므로 반드시 한정.
3. `STALL_*` 판정 시 → 해당 id `TaskStop` → §0-3 복구(짧은 핸드오프 재-spawn).
4. 정상 완료는 background 완료 알림으로 자동 수신.

- 주기는 **10초 아님**(프롬프트 캐시 5분 창 낭비). 멈춤은 분 단위라 120~180초로 충분.
- 폴링 누락 방지: 에이전트 dispatch 직후 첫 `ScheduleWakeup`를 반드시 건다.

### 0-4. 라운드 캡 · 린 메시징
- 기획 합의 **3라운드 초과 금지** — 초과 시 team-lead 강제 결정.
- 메시지에 명세·코드 **통째 첨부 금지** → **파일 경로로 참조**. (도구호출처럼 생긴 텍스트가 포맷붕괴 오염원)
- team-lead는 idle 알림에 **일일이 응답하지 말 것** — team-lead 컨텍스트도 같이 누적된다.

---

## 핵심 원칙 (모든 Phase 공통)

1. **에이전트 자체 능력을 신뢰**: director는 게임플레이만, client는 코드만, designer는 리소스·UI 프리팹만, qa는 검증만, sound는 음원·재생명세만, story는 서사텍스트만, level-design은 데이터값·배치만. team-lead가 그 영역을 침범하지 않는다.
2. **메일박스 wake 사각지대 방지**: 응답 대기 측을 먼저 spawn해 idle 진입시킨 뒤, 발신 측을 마지막에 spawn (메시지가 자동 wake 트리거).
3. **명세 기반 검증**: QA는 코드 grep보다 디렉터의 기획서·클라이언트의 코드명세를 우선 근거로 사용.
4. **자산 우선순위 트리**: 디자이너는 신규 자산 의뢰를 받으면 GameResource 재사용 → Synty/polyperfect 차용 → Procedural 생성 → 외부 자산 백로그 순서로 의사결정.
5. **사용자 체크포인트**: Phase 1/Phase 2 슬라이스/Phase 3 사이에 사용자 확인. 자동 진행도 가능하지만 큰 위험 결정은 사용자에게 보고.
6. **선택적 spawn**: 7인은 *후보 풀*이다. 주제에 필요한 멤버만 띄운다 — 안 부르면 비용 0. 미사용 멤버는 spawn하지 않는다.

---

## 에이전트 풀 — 7인 구성과 Phase별 관여

> 매 작업마다 전원 spawn 금지. team-lead가 주제를 분석해 **필요한 멤버만** 고른다.

| 에이전트 | 영역 | 주 관여 Phase | spawn 트리거 |
|---|---|---|---|
| `director` | 게임플레이·콘텐츠·톤 결정 | Phase 1 | 거의 항상 (기획 주제) |
| `client` | 코드·CSV 스키마·구현 | Phase 1~2 | 코드/데이터 구조 변경 시 |
| `designer` | 리소스·UI 프리팹·아트 | Phase 2 | 신규 자산·UI 필요 시 |
| `qa` | 검증(기능·UI·E2E) | Phase 2~3 | 구현 검증 필요 시 |
| `sound` | SFX·BGM·AI 음원 생성 | Phase 2 | 음원 필요 시 |
| `story` | 서사 텍스트(대사·플레이버·lore) | **Phase 1~2** | 서사·대면 텍스트 필요 시 |
| `level-design` | 레벨/밸런스 데이터·씬 배치 | Phase 2 | 몬스터·드롭·지형 배치 필요 시 |

**선택 예시:**
- "늑대 밸런스 조정" → `director` + `level-design` + `qa`
- "엔딩 컷신 대사" → `director` + `story` (+ `sound`)
- "신규 던전 + 사운드" → `director` + `level-design` + `designer` + `sound` + `client` + `qa`
- "Cold 시스템 신설" → 기존 4인 (`director`/`client`/`designer`/`qa`)

**경계 주의 (신규 3인):**
- `story` ↔ `director`: director=시스템·세계관 *결정*, story=그 안에서 *문장 집필*. 결정권 충돌 시 director 우선.
- `level-design` ↔ `client`/`designer`: level-design은 *수치 컬럼·기존 프리팹 배치*만 직접. 스키마·코드는 client, 새 자산은 designer. 텍스트 컬럼(Name/Description)은 story→client.
- `sound` ↔ `designer`: 개별 음원 import는 sound, 외부 *패키지* import 자동화는 designer.
- **같은 CSV 동시편집 금지**: level-design·client·story가 한 CSV를 동시에 건드리지 않는다(순차, team-lead가 순서 조정).

---

## Phase 0 — 팀 부트스트랩

### 전제
- 사용자가 "<주제>로 4-에이전트 팀 가동해줘" 또는 동등한 의뢰
- 코어 비전 문서(`document/design/CORE_*.md`)와 매핑 분석을 메인 세션이 인지

### 표준 단계

1. **주제 슬러그 결정** (ASCII): 예) `cold-realization`, `blueprint-unlock`. Discord 스레드 이름에 사용됨.

2. **TeamCreate**:
   ```
   TeamCreate(
     team_name: "<slug>",
     agent_type: "team-lead",
     description: "<주제> 기획·구현·QA"
   )
   ```

3. **spawn 대상 선택 후 순서: (수신 대기측) → … → director** (응답 대기 측이 먼저 idle 진입하도록 순서·시점 보장. director를 마지막에 띄워 메시지가 자동 wake 트리거):
   - 먼저 **주제에 필요한 멤버만 고른다** (위 "에이전트 풀" 표). 미사용 멤버는 spawn 안 함.
   - **client**: 사전 코드 조사 후 idle 대기. director 메시지를 받아 코드명세 작성.
   - **qa**: idle 대기. Phase 2/3에서 검증 의뢰 받아 활성.
   - **designer**: spawn 시 `document/RESOURCE_INVENTORY.md`를 읽어 정책·톤 숙지. Phase 1·2에서 시각 의도/리소스 의뢰 받아 활성.
   - **sound**: spawn 시 `RESOURCE_INVENTORY.md` 숙지. Phase 2에서 음원 의뢰 받아 활성. (음원 없는 주제면 미spawn)
   - **level-design**: 대상 CSV 스키마·기존 값 파악 후 idle. Phase 2에서 배치·데이터 의뢰 받아 활성.
   - **story**: 세계관 캐논·톤 파악 후 idle. Phase 1~2에서 서사 집필 의뢰 받아 활성.
   - **director**: 기획서 초안 작성 후 client에게 검토 요청 SendMessage. (보통 마지막 spawn)

4. **각 에이전트 spawn 프롬프트에 다음 명시**:
   - 팀 이름, 본인 이름, 이번 주제에 spawn된 다른 멤버 이름 (전체 풀: `director`, `client`, `designer`, `qa`, `sound`, `story`, `level-design`. 안 띄운 멤버는 굳이 알릴 필요 없음)
   - team-lead 이름 = `team-lead` (메인 세션)
   - 본 정의서(`.claude/agents/<name>.md`)의 규칙을 따를 것

### 체크리스트
- [ ] team_name이 ASCII 슬러그인가
- [ ] 주제에 **필요한 멤버만** 골랐는가 (전원 spawn 금지)
- [ ] 응답 대기측 먼저 → director 마지막 순서로 spawn했는가
- [ ] 각 프롬프트에 이번 주제에 spawn된 멤버 이름을 명시했는가
- [ ] director에게 기획 초안 작업 의뢰까지 포함했는가

### 실패 시 대처
- **다른 팀이 이미 존재**: TeamList로 확인 후 의도된 팀이면 TeamDelete 후 재생성. 아니면 기존 팀 재사용.

---

## Phase 1 — 기획 합의 (디렉터 ↔ 클라이언트 페어 토론)

### 전제
- Phase 0 완료
- 디렉터가 기획서 초안을 작성하고 client에게 검토 요청 SendMessage 송신

### 표준 단계

1. **디렉터**: `document/design/game-design/<주제>/기획.md` 작성
   - 11개 표준 섹션 (한줄 컨셉 / 디자인 골 / 안티 골 / 플레이어 경험 흐름 / 핵심 디자인 결정 / 콘텐츠 분량 / 톤 가이드 / 밸런스 / 엣지 케이스 / 향후 확장 / 참고)
   - **UI/리소스 영향이 있는 주제는 "UI 시각 의도" 섹션 추가** (디자이너가 와이어프레임으로 변환할 톤·색상·레이아웃 가이드)
   - 클라이언트에게 SendMessage: 핵심 요약 5~8 bullet + 구체 질문 3개 + 양보 가능/불가 정리

2. **클라이언트**: `document/design/client-spec/<주제>/코드명세.md` v0.1 작성
   - 12개 표준 섹션 (영향 범위 / 재활용 가능 시스템 / 신규 클래스 / 데이터 레이어 / 네트워크 동기화 / 상태 머신 / 이벤트·Manager 계약 / 의존성 / 구현 단계 / 비용 추정 / 엣지 케이스 / 권고)
   - **리소스 요청 목록 명시** (필요 프리팹·sprite·머티리얼 + 기대 슬롯명) — 디자이너가 Phase 2에서 처리
   - 디렉터에게 SendMessage: 질문 답변 + 단순화 권고 + 미결 사항

3. **디렉터 결정 라운드 2** → 클라이언트 v0.2

4. **합의 또는 위임**:
   - 3라운드 안에 합의되면 양쪽이 team-lead에게 "기획·코드명세 확정" 보고
   - 안 되면 team-lead에게 결정 위임 (team-lead가 사용자 확인 후 결정 SendMessage)

5. **designer·qa·sound·level-design는 idle 유지** — Phase 1(기획 합의)에서는 관여 안 함 (단, 톤·시각 의문 질문은 받을 수 있음). **`story`는 예외** — 서사 비중이 큰 주제면 Phase 1부터 director와 함께 세계관·톤 집필을 시작할 수 있다.

### 체크리스트
- [ ] 기획서 파일이 game-design/ 하위에 작성됐는가
- [ ] 기획서에 "UI 시각 의도" 섹션이 있는가 (UI/리소스 영향 시)
- [ ] 코드명세 파일이 client-spec/ 하위에 작성됐는가
- [ ] 코드명세에 리소스 요청 목록이 있는가
- [ ] 양쪽이 "확정" 상태를 명시 보고했는가
- [ ] 3라운드 초과한 경우 team-lead가 결정 위임받았는가

### 실패 시 대처
- **무한 토론**: 3라운드 초과 시 team-lead가 강제 결정. 합의 안 된 항목을 명시적으로 사용자에게 보고하고 결정 받음.
- **inbox wake 사각지대**: 응답 대기 측이 메시지를 안 받으면 team-lead가 직접 SendMessage 푸시 (자동 wake 실패 케이스)

### 사용자 체크포인트 (권장)
Phase 1 합의 직후, 두 문서(기획서·코드명세) 위치를 사용자에게 보고하고 검토 요청. 사용자가 OK 하면 Phase 2 진입.

---

## Phase 2 — 슬라이스 구현 사이클

### 전제
- Phase 1 합의 완료
- 코드명세 9장(구현 단계)에 슬라이스 1, 2, 3, ... 명시됨
- 코드명세에 리소스 요청 목록 있음 (있는 경우)

### 표준 단계 (슬라이스마다 반복)

1. **team-lead → 자산·콘텐츠 의뢰 SendMessage** (client 구현 전, 필요한 멤버만 우선 처리):
   - 슬라이스에 필요한 만큼 **designer**(리소스·UI) / **sound**(음원) / **level-design**(데이터·씬 배치) / **story**(텍스트)에 의뢰. 아래는 designer 기준 예시 — 나머지도 각 정의서 워크플로우로 동일 패턴(우선순위 트리·자가검수·인터페이스 명세·백로그).
   - 슬라이스 N 범위에서 필요한 리소스 목록
   - 디렉터의 "UI 시각 의도" 참조 (기획서)
   - 클라이언트가 기대하는 슬롯명 (코드명세)
   - **디자이너 워크플로우** (designer.md): 자산 우선순위 트리 적용 → 자동화 가능 부분 즉시 처리 → 4단계 자산은 `document/asset-backlog/<주제>.md` 등록
   - 자산·콘텐츠 멤버(designer/sound/level-design/story) 완료 보고 후 → 클라이언트 의뢰 단계로

2. **team-lead → SendMessage to client** (슬라이스 의뢰):
   - 슬라이스 N 범위 (코드명세 9장 참조)
   - 디자이너가 만든 자산 위치·슬롯명 인터페이스 명세 (디자이너가 클라에게 직접 SendMessage로 전달했을 수도 있음)
   - WES 코딩 규칙 강조 (`m_`, `_`, public 금지, 9단계 레이아웃, `Co` 코루틴)
   - 기존 시스템 분석 가이드 (관련 grep 키워드)
   - 모호함 발견 시 디렉터·디자이너·team-lead에게 SendMessage로 질문할 것

3. **클라이언트**:
   - 기존 시스템 grep
   - 코드 작성 + 자체 점검 (WES 코딩 규칙)
   - 디자이너 자산이 있다면 SerializeField로 슬롯 참조만 정의 (와이어링은 디자이너)
   - team-lead에게 보고 (생성/수정 파일 목록 + 자체 점검 결과 + 다음 Unity 작업 안내)

4. **team-lead → QA 검증 의뢰 SendMessage** (`mode: function`):
   - 의뢰 메시지에 슬라이스 범위와 변경 파일 목록 명시

5. **QA**:
   - 디렉터/클라이언트에게 명세 위치 요청 → Read
   - `u_editor_asset(refresh)` → `u_console(error)` → 컴파일 검증
   - 에러 있으면 클라이언트에게 SendMessage로 수정 요청 → 클라이언트 수정 → QA 재검증
   - 통과 시 씬·와이어링 검증 (`u_editor_gameobject(get)`, `u_editor_component(list)`)
   - team-lead에게 "슬라이스 N 검증 통과" 보고

6. **Unity 측 자동화 (선택)**:
   - 디자이너 또는 클라이언트가 Editor 메뉴 스크립트 작성 (`Assets/Scripts/Editor/<주제>Setup.cs`)
   - 디자이너 영역: 자산 생성·머티리얼·프리팹 구조·Inspector 와이어링
   - 클라이언트 영역: 코드 컴포넌트 부착·SerializeField 슬롯 연결
   - 참조 사례: `Assets/Scripts/Editor/DayNightConfigCreator.cs` (2026-05-11 시간/낮밤 시스템)

7. **사용자 체크포인트 (옵션)**: 슬라이스 단위로 사용자에게 진행 상황 보고

### 표준 메시지 템플릿 (team-lead → designer, 리소스 의뢰)

```
슬라이스 N의 리소스 의뢰. 코드명세의 리소스 요청 목록 + 디렉터 기획서의 UI 시각 의도를 참조해 처리해라.

### 필요 리소스
1. <리소스 명·용도·예상 슬롯명>
2. ...

### 작업 가이드
- 자산 우선순위 트리 적용 (1→2→3→4단계)
- 자동화 가능 부분은 Editor 메뉴 스크립트로 일괄 처리
- 4단계(외부 자산 필요) 항목은 임시 placeholder + document/asset-backlog/<주제>.md 등록
- 클라이언트에게 인터페이스 명세 SendMessage (프리팹 위치·GameObject 트리·슬롯명)

### 결과 보고
team-lead에게:
- 처리한 자산 목록 + 의사결정 (1~4단계 어느 분기인지)
- 외부 자산 백로그 신규 항목
- 클라이언트에게 전달한 인터페이스 명세 요약
- 디렉터 톤 확인 요청 (있다면)
```

### 표준 메시지 템플릿 (team-lead → client, 슬라이스 의뢰)

```
슬라이스 N 구현 의뢰. 코드명세 9장의 슬라이스 N을 따라 다음 구현해라.

### 구현 항목
1. ...
2. ...

### 디자이너가 준비한 자산 (있다면)
- 프리팹 위치: ...
- 슬롯 인터페이스: m_Foo, m_Bar, ...

### 작업 가이드
- WES 코딩 규칙 절대 준수 (m_, _, public 금지, 레이아웃 9단계)
- 기존 시스템 분석 먼저: <grep 가이드>
- 컴파일 에러 만들지 말 것
- 모호하면 director/designer/team-lead에게 SendMessage

### 결과 보고
작업 완료 후 team-lead에게:
- 생성/수정 파일 목록
- WES 코딩 규칙 자체 점검
- 다음 Unity 작업 안내 (있다면)
```

### 체크리스트 (슬라이스마다)
- [ ] 디자이너 자산 처리 완료 (해당하는 경우)
- [ ] 컴파일 에러 0
- [ ] 신규 GameObject가 씬에 배치됐는가 (해당하는 경우)
- [ ] Inspector 참조가 모두 와이어링됐는가
- [ ] QA가 "통과" 보고했는가
- [ ] 외부 자산 백로그가 갱신됐는가 (4단계 자산이 있는 경우)

### 실패 시 대처
- **컴파일 에러 재발**: 클라이언트 → QA 사이클 무한 반복 방지. 3회 재실패 시 team-lead가 직접 코드 확인 또는 디렉터에게 명세 모호함 질문.
- **씬 배치 자동화 실패**: 디자이너 Editor 스크립트 재작성. 그래도 실패면 사용자에게 수동 작업 안내.
- **외부 자산 필요 누락**: 디자이너가 4단계 의사결정을 빠뜨린 경우 → team-lead가 디자이너에게 백로그 재확인 요청.

---

## Phase 2.5 — 리소스 명세서 갱신 (조건부)

### 전제
- Phase 2의 슬라이스 진행 중 또는 종료 직후
- 자산 신규 생성·삭제 작업이 있었음

### 표준 단계

1. **디자이너**: 작업 종료 직전 메뉴 `WES/Tools/Generate Resource Usage Report` 한 번 호출
2. 출력: `document/RESOURCE_USAGE_REPORT.md` 갱신 (사용/미사용 통계 + 외부 자산 백로그 통합)
3. 외부 에셋 신규 패키지가 추가됐다면 `document/RESOURCE_INVENTORY.md`의 폴더 정책 섹션도 수동 갱신

### 호출 안 함 (Skip)
- 코드만 수정한 슬라이스 (자산 변동 없음)
- 24시간 이내 이미 갱신됨

---

## Phase 3 — 통합 QA

### 전제
- 모든 슬라이스 Phase 2 통과
- 시스템 전체가 동작 가능한 상태

### 표준 단계

1. **team-lead → QA 검증 의뢰 SendMessage** (`mode: function | ui | both`):
   - 의뢰 메시지에 모드와 범위 명시

2. **QA**: 자체 워크플로우 따름 (qa.md 모드 A 또는 모드 B)
   - 명세 기반 시나리오 도출
   - **모드 A — 기능 QA**: TestManager 시나리오 추가 → 컴파일 → 씬·와이어링 → 플레이모드 실행 → 판정
   - **모드 B — UI QA**: 자동 검사 A1~A9 + 시각 검수 B1~B10 → `document/ui-review/<YYYY-MM-DD-HHMM>.md` 리포트
   - **모드 `both`**: A 먼저 → 통과 후 B (or 병행)

3. **결과 보고**: QA가 team-lead에게 통과/실패/잔여 수동 작업 정리

### 체크리스트
- [ ] 명세된 모든 분기에 대해 시나리오가 있는가 (러프 1개 금지)
- [ ] TestManager 시나리오가 모두 통과했는가 (모드 A)
- [ ] UI 자동 플래그 없음 + 시각 검수 통과 (모드 B)
- [ ] 잔여 수동 작업이 사용자에게 명확히 전달됐는가

### 실패 시 대처
- **기획적으로 진행 불가**: QA가 team-lead에게 보고 → team-lead가 디렉터/사용자에게 협의 (예: "공격 수단이 없어 몬스터 처치 불가" 같은 케이스)
- **자동 수정 사이클 무한**: 같은 항목 3회 재실패 시 team-lead 에스컬레이션

---

## Phase 4 — 마무리

### 표준 단계

1. **team-lead → 사용자 최종 보고**:
   - 산출 파일 목록 (기획.md, 코드명세.md, 코드 파일들, 리포트 등)
   - Phase 3 통과 항목 / 실패 항목
   - **외부 자산 백로그** (`document/asset-backlog/<주제>.md`) — 사용자가 외부에서 마련해야 할 자산 목록
   - 잔여 수동 작업 (있다면)

2. **사용자 승인 시 팀 정리**:
   - 각 멤버에게 SendMessage `shutdown_request`
   - 응답 후 TeamDelete

3. **커밋 (사용자가 명시적으로 요청한 경우만)**:
   - `commit-push` 스킬 호출

### 체크리스트
- [ ] 모든 산출 파일 위치가 사용자에게 명확히 전달됐는가
- [ ] 외부 자산 백로그가 사용자에게 명확히 전달됐는가
- [ ] 잔여 수동 작업이 빠짐없이 정리됐는가
- [ ] 사용자가 명시적으로 팀 정리 승인했는가

### 실패 시 대처
- **사용자가 추가 작업 요구**: TeamDelete 보류, 새 Phase 2 슬라이스 추가 또는 Phase 3 재검증 사이클
- **외부 자산 도착 후 추가 작업**: 별도 디자이너 의뢰(새 팀 또는 재spawn) — 디자이너가 import 자동화 + 임시 placeholder 교체

---

## 부록 — 참조 자산

- `.claude/skills/dev-qa/skill.md` — qa.md 모드 A의 절차 모델
- `.claude/skills/ui-review/skill.md` — qa.md 모드 B의 절차 모델
- `.claude/skills/commit-push/` — Phase 4 커밋 단계
- `Assets/Scripts/Editor/DayNightConfigCreator.cs` — Phase 2 Unity 자동화 패턴 참조 사례
- `document/design/CORE_자원투자_트레이드오프_설계.md` — 코어 비전 (기획 주제 선정의 근거)
- `document/design/game-design/자원투자_현재콘텐츠_매핑/기획.md` — 주제 우선순위 분석
