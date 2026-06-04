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
