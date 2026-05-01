---
name: commit-push
description: Use when the user asks to commit and push. Auto commits and pushes both WES_Project and MCP_Unity repos with proper commit messages and logs.
---

# Auto Commit & Push

두 프로젝트를 자동으로 커밋하고 푸쉬한다.

- **WES_Project**: `C:\GitFork\WES_Project`
- **MCP_Unity**: `C:\GitFork\MCP_Unity`

## 절차

### 1. 변경사항 확인

두 저장소 모두 `git status`로 변경사항을 확인한다.

```bash
git -C /c/GitFork/WES_Project status
git -C /c/GitFork/MCP_Unity status
```

- 변경사항이 없는 저장소는 건너뛴다.
- 변경사항이 있는 저장소만 아래 절차를 진행한다.

### 2. 변경 내용 분석

각 저장소에서 `git diff`와 `git diff --cached`로 변경 내용을 파악한다.

### 3. 커밋 메시지 작성 규칙

- 한국어로 작성한다.
- 변경 내용을 요약하여 1~2줄로 작성한다.
- "무엇을 했는지"가 아니라 "왜 했는지" 중심으로 작성한다.
- 말미에 `Co-Authored-By: Claude <noreply@anthropic.com>`를 추가한다.

### 4. `.claude/rules/` 동기화 체크

커밋 전에 `Assets/Scripts/`, `Assets/GameResource/`, `Assets/Scenes/`, `Assets/CSVInfo/` 폴더 구조를 스캔하여 `.claude/rules/` 룰 파일들과 비교한다.

- 새로운 스크립트/폴더/프리팹/씬이 추가되었으면 해당 룰 파일에 항목 추가
- 기존 항목이 삭제/이름변경 되었으면 룰 파일에서 제거/수정
- 새로운 카테고리 폴더가 생겼으면 새 룰 파일 생성
- 변경사항이 없으면 건너뛴다
- 룰 파일이 수정되었으면 커밋에 함께 포함한다

### 5. 스테이징 & 커밋

```bash
# 변경된 파일을 개별적으로 add (git add -A 금지)
git -C /c/GitFork/WES_Project add <changed files>
git -C /c/GitFork/WES_Project commit -m "커밋 메시지"

git -C /c/GitFork/MCP_Unity add <changed files>
git -C /c/GitFork/MCP_Unity commit -m "커밋 메시지"
```

- 민감한 파일(.env, credentials 등)은 커밋하지 않는다.
- `.meta` 파일은 해당 에셋과 함께 커밋한다.

### 6. 푸쉬

```bash
git -C /c/GitFork/WES_Project push
git -C /c/GitFork/MCP_Unity push
```

### 7. 결과 보고

아래 형식으로 결과를 보고한다:

```
## 커밋 & 푸쉬 완료

### WES_Project
- 브랜치: <branch>
- 커밋: <hash> <message>
- 변경: +N파일 / -N파일 / ~N파일

### MCP_Unity
- 브랜치: <branch>
- 커밋: <hash> <message>
- 변경: +N파일 / -N파일 / ~N파일
```

변경사항이 없는 저장소는 "변경사항 없음"으로 표시한다.
