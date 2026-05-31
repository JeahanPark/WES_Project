# auto/ — 에이전트 자동 생성 구역

이 폴더 하위의 모든 파일은 **AI 에이전트가 자동으로 생성·갱신**한다.

## 원칙

- 사람이 수동으로 작성한 문서는 `document/auto/` **밖**에 둔다
- 에이전트는 `auto/` 하위 파일을 자유롭게 덮어쓴다
- `obsidian_agent_plan.md` 스펙에 따라 폴더 구조가 결정됨

## 폴더

| 경로 | 용도 |
|---|---|
| `catalog/Class/` | 클래스 1개당 .md 파일 1개 (frontmatter에 메타데이터) |
| `catalog/Signal/` | C# event / NGO RPC / NetworkVariable 등 코드 간 통신 채널 1개당 .md 파일 1개 |
| `diagrams/class/` | 카테고리별 Mermaid 클래스 다이어그램 |
| `diagrams/sequence/` | 플로우별 Mermaid 시퀀스 다이어그램 |
| `reports/` | `YYYY-MM-DD-제목.md` 형식의 작업 리포트 |
| `views/` | Dataview 쿼리 페이지 (카테고리별 / 영역별 / 상태별 뷰) |
| `_templates/` | 시드/에이전트 참고용 템플릿 (`Class.md`, `Signal.md`, `Report.md`) — 뷰 쿼리에서 자동 제외됨 |

## 사용자 setup

1. Obsidian 설치: https://obsidian.md
2. `document/` 폴더를 vault로 열기 (Open folder as vault)
3. Settings → Community plugins → Browse → "Dataview" 설치 + Enable
4. `auto/views/클래스카탈로그.md` 열어서 Dataview 쿼리가 표 형태로 렌더링되면 OK

상세 스펙: [[../obsidian_agent_plan]]
