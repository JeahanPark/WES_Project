# 디자이너 AI 텍스처 생성 (Gemini × Playwright MCP) — 설계서

- **작성일**: 2026-06-04
- **대상**: `designer` 에이전트 능력 확장
- **상태**: 승인됨 (사용자 합의 완료)

## 1. 목적

`designer` 에이전트가 텍스처·2D 이미지 리소스가 필요할 때, **외부 자산 백로그로 미루지 않고 직접 AI(Gemini 웹)로 이미지를 생성**해 즉시 프로젝트에 투입한다. 생성은 Playwright MCP로 브라우저를 구동해 Gemini 웹에 접속하고, 디자이너가 작성한 프롬프트로 이미지를 만든 뒤 다운로드·저장·Unity import까지 자동화한다.

## 2. 범위

| 포함 | 제외 |
|---|---|
| 텍스처·2D sprite·아이콘·UI 이미지 생성 | 3D 메쉬 (Gemini 생성 불가 → 기존 백로그 유지) |
| Gemini 웹 자동화 (프롬프트 입력 → 생성 → 다운로드) | 사운드·애니메이션 |
| GameResource 경로 저장 + Unity texture/sprite import | 게임 로직 코드 (클라이언트 영역) |

## 3. 자산 우선순위 트리 개정

기존 트리(재사용 → 차용 → Procedural → 백로그)를 다음과 같이 개정한다:

- **텍스처·2D 이미지**: **AI 생성을 최우선**으로 한다. 재사용·차용·Procedural보다 먼저 AI 생성을 시도한다.
- **3D 메쉬**: 기존 트리(재사용 → 차용 → placeholder + 백로그) 그대로 유지.

> 결정 근거: 사용자 지시 — "텍스처류는 무조건 AI한테 만들어 달라고 하자."

## 4. 구성 요소

### 4.1 Playwright MCP 서버
- 패키지: `@playwright/mcp` (Microsoft 공식).
- **영구 프로필**로 구동(`--user-data-dir`)하여 Google 로그인 세션을 유지한다.
- 사용자가 **최초 1회 수동 로그인** → 이후 세션 자동 유지.

### 4.2 designer 에이전트 정의 변경
- `designer.md` frontmatter `tools`에 Playwright MCP 도구 추가.
- `designer.md` 본문에 **"AI 텍스처 생성" 절차 섹션** 신설.
- 자산 우선순위 트리 섹션을 §3대로 개정.

### 4.3 Gemini 웹 자동화 절차
navigate → 프롬프트 입력 → 이미지 생성 대기 → 다운로드.

### 4.4 저장·임포트 자동화
다운로드 → GameResource 경로 결정 → `u_editor_asset`으로 import → texture/sprite import 설정.

## 5. 생성 루프

| # | 단계 | 도구 |
|---|---|---|
| 1 | 디자이너가 WES 다크 톤 반영 프롬프트 작성 | (사고) |
| 2 | gemini.google.com 이동 (영구 프로필 = 로그인 유지) | Playwright |
| 3 | 프롬프트 입력 → 이미지 생성 대기 | Playwright |
| 4 | 결과 자가 평가 (톤·용도 적합?) | Playwright screenshot + 판단 |
| 5 | 부적합 → 프롬프트 보정 후 재생성 (**최대 5회**) | Playwright |
| 6 | 5회 실패 → **보류**: placeholder + asset-backlog 등록 | Write |
| 7 | 성공 → 다운로드 → GameResource 경로 저장 → Unity import | Playwright + u_editor_asset |
| 8 | 결과(성공·보류) team-lead에게 **메일 보고** | SendMessage |

### 저장 위치 결정 규칙
디자이너가 리소스 카테고리를 보고 경로를 결정한다:
- 텍스처: `Assets/GameResource/Texture/`
- 일반 이미지: `Assets/GameResource/Image/`
- UI 이미지: `Assets/GameResource/UI/...`

## 5.5 Gemini 세션 / 스타일 관리

세트 일관성과 컨텍스트 윈도우 한계를 위해 다음 규칙을 따른다:

| 항목 | 규칙 |
|---|---|
| 세트 일관성 | 같은 카테고리/세트 아이콘은 **같은 Gemini 채팅**에서 이어 생성한다. |
| 스타일 고정 | 세션 첫 메시지에 스타일 기준을 명시 (예: "앞으로 모든 아이콘은 [2D 다크 판타지, 검은 테두리, 저채도 톤]으로 통일"). |
| 세션 한도 | **15~20장**마다 채팅을 끊는다 (초과 시 스타일 왜곡·응답 지연·생성 에러). |
| 체인 방식 | 새 채팅을 열고 직전 세션의 베스트 이미지+프롬프트를 붙여넣어 "이 스타일로 이어서" 요청. |
| 새 채팅 사용 | 완전히 다른 화풍을 원할 때만 새 채팅을 연다. |

### 스타일 가이드 영속화
- 각 세트의 스타일 기준 프롬프트(프리픽스)를 `document/asset-style-guide/<세트명>.md`에 저장한다.
- 동일 세트 후속 생성 시 이 프리픽스를 재사용해 일관성을 유지한다.
- 세션 한도(15~20장) 초과로 채팅을 끊을 때, 이 파일의 프리픽스 + 직전 베스트 이미지를 새 채팅에 투입한다.

## 5.55 탭 풀링 — 병렬 생성 (최대 3탭)

여러 이미지를 한 번에 만들 때 탭 풀로 병렬 생성한다(탭 = 하나의 Gemini 채팅 세션).

- 동시 최대 **3탭**. 초과분은 큐 → 탭 빌 때 투입.
- `browser_tabs(new/select/close)`로 탭 풀 관리. 3탭에 fire → 각 탭 wait → 탭별 다운로드·import.
- 한 탭이 끝나면 닫지 말고 `navigate /app`(새 채팅)으로 다음 큐 항목 재사용. 배치 종료 시 1탭만 남김.
- **제약**: 탭 간 컨텍스트 비공유 → 동일 세트(스타일 일관성)는 한 탭 순차. 병렬 풀은 독립 이미지/세트별(탭당 1세트)에 적용.

## 5.6 리소스 관리 — Sprite Atlas

- 생성된 2D sprite/아이콘은 **카테고리별 Sprite Atlas**에 팩한다 (드로우콜 절감).
- Atlas 위치: `Assets/GameResource/UI/Atlas/<카테고리>.spriteatlas` (예: `Icons.spriteatlas`).
- 신규 sprite 생성·import 후 해당 카테고리 Atlas에 자동 포함시킨다 (Atlas의 Objects for Packing에 폴더 등록 방식 권장 — 폴더 추가만으로 자동 편입).
- Addressable로 런타임 로드하는 경우 Atlas와의 중복 포함을 점검한다.

## 6. 인터페이스 / 보고

- 모든 결과(성공·보류)는 `SendMessage`로 team-lead에게 보고("메일").
- 성공 시 보고 내용: 생성 자산 경로, 사용 프롬프트, import 설정.
- 보류 시 보고 내용: 시도 횟수(5), 실패 사유, placeholder 처리·백로그 항목.

## 7. 선결 조건 / 리스크

| 항목 | 내용 | 영향 |
|---|---|---|
| Playwright MCP 설치 | `@playwright/mcp` 등록 + 영구 프로필 설정 | 사용자 액션 1회 |
| Google 로그인 | Gemini 접근용 1회 수동 로그인 | 사용자 액션 1회 |
| Gemini DOM 변동 | 셀렉터/버튼 위치 변경 시 절차 수정 필요 | 자동화 fragile, 유지보수 필요 |
| 다운로드 경로 | 브라우저 다운로드 폴더 → GameResource 이동 처리 | 절차에 명시 필요 |

## 8. 검증 기준 (Definition of Done)

- [ ] Playwright MCP가 영구 프로필로 연결되고, Gemini 웹에 로그인 유지된 채 접속된다.
- [ ] designer 에이전트가 텍스처 의뢰 수신 시 AI 생성 루프를 실행한다.
- [ ] 생성 이미지가 GameResource 하위 올바른 카테고리 경로에 저장된다.
- [ ] 저장된 이미지가 Unity에 texture/sprite로 import된다.
- [ ] 5회 실패 시 보류·백로그·메일 보고가 정상 동작한다.
- [ ] 동일 세트 생성 시 스타일 가이드 프리픽스가 재사용되고, 15~20장 한도로 세션이 체인된다.
- [ ] 생성 sprite가 카테고리별 Sprite Atlas에 자동 편입된다.
