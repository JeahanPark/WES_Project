# 디자이너 AI 텍스처 생성 (Gemini × Playwright MCP) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** designer 에이전트가 텍스처·2D 이미지를 Gemini 웹(Playwright MCP)으로 직접 생성·다운로드·import·아틀라스 편입하도록 능력을 확장한다.

**Architecture:** (1) `.mcp.json`에 영구 프로필 Playwright MCP 서버 추가, (2) 일회성 Google 로그인으로 세션 영속화, (3) `designer.md`에 생성 루프·세션/스타일 관리·아틀라스 규칙 인코딩, (4) C# Editor 유틸로 import·아틀라스 자동화. 검증은 이 프로젝트의 표준대로 `claude mcp list` + MCP(`u_console`/`u_screenshot`) 기반.

**Tech Stack:** Claude Code MCP (`@playwright/mcp`, `mcp-unity`), Unity 6 Editor 스크립팅(C#, `SpriteAtlas`/`TextureImporter`), Markdown 에이전트 정의.

> **검증 모델 주의:** 본 작업물의 핵심은 마크다운 에이전트 정의·MCP 설정·일회성 Editor 유틸이라 pytest식 단위 테스트가 성립하지 않는다. 각 태스크는 **실행 가능한 검증 명령 + 기대 출력**으로 "실패→통과"를 확인한다(이 프로젝트의 dev-qa 모델).

---

## File Structure

| 파일 | 책임 | 변경 |
|---|---|---|
| `.mcp.json` | MCP 서버 등록 | playwright 서버 추가 |
| `.gitignore` | 브라우저 프로필 제외 | 프로필/출력 폴더 ignore |
| `.claude/agents/designer.md` | designer 능력 정의 | tools + 절차 섹션 + 트리 개정 |
| `Assets/Scripts/Editor/AiTextureImportSetup.cs` | 생성 이미지 import + 아틀라스 편입 | 신규 |
| `Assets/GameResource/Texture/` | 텍스처 저장 | 폴더 신규 |
| `Assets/GameResource/UI/Atlas/Icons.spriteatlas` | 아이콘 카테고리 아틀라스 | 신규 |
| `document/asset-style-guide/` | 세트별 스타일 프리픽스 영속화 | 폴더 신규 |
| `document/RESOURCE_INVENTORY.md` | 정책 갱신 | AI 생성·아틀라스 정책 추가 |

---

## Task 1: Playwright MCP 서버 등록 (영구 프로필)

**Files:**
- Modify: `.mcp.json`
- Modify: `.gitignore`

- [ ] **Step 1: `.mcp.json`에 playwright 서버 추가**

`.mcp.json` 전체를 다음으로 교체:

```json
{
  "mcpServers": {
    "mcp-unity": {
      "type": "stdio",
      "command": "c:/GitFork/MCP_Unity/MCP/MCP/MCP/bin/Release/net8.0/MCP.exe"
    },
    "playwright": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y",
        "@playwright/mcp@latest",
        "--user-data-dir",
        "c:/GitFork/WES_Project/.playwright-profile",
        "--output-dir",
        "c:/GitFork/WES_Project/.playwright-output"
      ]
    }
  }
}
```

> `--user-data-dir`로 Google 로그인 세션을, `--output-dir`로 다운로드 파일 위치를 고정한다.

- [ ] **Step 2: 프로필·출력 폴더를 `.gitignore`에 추가**

`.gitignore` 끝에 추가:

```gitignore

# Playwright MCP (designer AI texture gen)
/.playwright-profile/
/.playwright-output/
```

(경로는 리포 루트 `WES_Project/` 기준이므로 `WES/.gitignore`가 아니라 리포 루트의 ignore에 들어가야 한다. 루트에 `.gitignore`가 없으면 `c:/GitFork/WES_Project/.gitignore`를 신규 생성한다.)

- [ ] **Step 3: 커밋**

```bash
git add .mcp.json
git commit -m "Playwright MCP 서버 추가 (designer AI 텍스처 생성용, 영구 프로필)"
```

- [ ] **Step 4: 검증 — MCP 인식**

> Claude Code는 `.mcp.json` 변경을 **재시작 후** 인식한다. 사용자에게 Claude Code 재시작을 요청한다(`/mcp` 승인 프롬프트가 뜨면 승인).

재시작 후 실행:
```bash
claude mcp list
```
Expected: 출력에 `playwright: ... - ✓ Connected` 라인이 포함된다.

---

## Task 2: 최초 Google 로그인 (세션 영속화)

**Files:** (코드 변경 없음 — 일회성 사용자 액션 + 절차 확정)

- [ ] **Step 1: Gemini 접속 스모크**

Playwright MCP로 실행:
- `browser_navigate` → `https://gemini.google.com/app`
- `browser_snapshot`

기대: 접근성 스냅샷이 반환된다.

- [ ] **Step 2: 로그인 상태 판정**

스냅샷에 "Sign in"/"로그인" 버튼이 있으면 **미로그인**, 프롬프트 입력창("Ask Gemini" 등)이 있으면 **로그인됨**.

- [ ] **Step 3: (미로그인 시) 사용자 수동 로그인**

미로그인이면 사용자에게: "열린 브라우저에서 Google 로그인을 완료해 주세요(2FA 포함). 완료하면 알려주세요." → 사용자 완료 후 진행.

- [ ] **Step 4: 영속화 검증**

`browser_close` 후 다시 `browser_navigate` → `https://gemini.google.com/app` → `browser_snapshot`.
Expected: 재접속에도 프롬프트 입력창이 보이고 "Sign in"이 없다(프로필 영속 확인).

---

## Task 3: GameResource 폴더 + 아틀라스 + 스타일 가이드 스캐폴딩

**Files:**
- Create: `Assets/GameResource/Texture/` (폴더)
- Create: `Assets/GameResource/UI/Atlas/` (폴더)
- Create: `document/asset-style-guide/README.md`

- [ ] **Step 1: 폴더 생성**

```bash
mkdir -p "Assets/GameResource/Texture"
mkdir -p "Assets/GameResource/UI/Atlas"
mkdir -p "../document/asset-style-guide"
```

- [ ] **Step 2: 스타일 가이드 폴더 설명 파일 작성**

`../document/asset-style-guide/README.md` 생성:

```markdown
# Asset Style Guide (AI 생성 세트별 스타일 프리픽스)

세트마다 `<세트명>.md` 파일을 만들어 Gemini 생성 시 첫 메시지로 투입할 **스타일 기준 프리픽스**를 보관한다.

## 규칙
- 동일 세트 후속 생성 시 이 프리픽스를 재사용해 일관성을 유지한다.
- Gemini 세션은 15~20장마다 끊고, 새 채팅에 이 프리픽스 + 직전 베스트 이미지를 투입해 체인한다.

## 예시 (`icons.md`)
> 앞으로 모든 아이콘은 2D 다크 판타지 스타일, 검은 외곽선, 저채도 흙빛 팔레트,
> 약한 상단 광원, 256×256 정사각, 단색 배경으로 통일해줘. (Don't Starve 류 어둡고 외로운 톤)
```

- [ ] **Step 3: 커밋**

```bash
git add ../document/asset-style-guide/README.md
git commit -m "AI 생성 스타일 가이드 폴더 + GameResource 텍스처/아틀라스 폴더 스캐폴딩"
```

> 빈 폴더(`Texture/`, `Atlas/`)는 git에 안 잡히므로, Task 4 아틀라스 생성·Task 7 이미지 저장 시점에 실체 파일이 생기며 추적된다.

---

## Task 4: AiTextureImportSetup.cs — import + 아틀라스 편입 Editor 유틸

**Files:**
- Create: `Assets/Scripts/Editor/AiTextureImportSetup.cs`

- [ ] **Step 1: Editor 유틸 작성**

`Assets/Scripts/Editor/AiTextureImportSetup.cs` 생성:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// AI(Gemini)로 생성·다운로드한 이미지를 GameResource로 들여오고
/// 카테고리 Sprite Atlas에 편입하는 디자이너용 Editor 유틸.
/// - 외부 다운로드 PNG → 지정 카테고리 폴더로 복사
/// - TextureImporter를 Sprite(2D UI 표준)로 설정
/// - 카테고리 폴더를 Sprite Atlas의 packables에 등록(폴더 단위 자동 편입)
/// 톤·실제 생성은 designer 에이전트가 수행. 본 유틸은 import 표준화만 담당.
/// </summary>
public static class AiTextureImportSetup
{
    public const string ATLAS_DIR = "Assets/GameResource/UI/Atlas";

    /// <summary>
    /// 다운로드 PNG를 카테고리 폴더로 복사·import하고 아틀라스에 편입한다.
    /// </summary>
    /// <param name="srcPng">다운로드된 원본 PNG 절대경로</param>
    /// <param name="categoryDir">대상 폴더 (예: "Assets/GameResource/Image/ItemIcon")</param>
    /// <param name="assetName">확장자 제외 자산명 (예: "campfire_icon")</param>
    /// <param name="atlasName">카테고리 아틀라스명 (예: "Icons"), 비우면 아틀라스 생략</param>
    /// <returns>생성된 에셋 경로</returns>
    public static string ImportAndPack(string srcPng, string categoryDir, string assetName, string atlasName)
    {
        EnsureDir(categoryDir);
        string destPath = $"{categoryDir}/{assetName}.png";
        File.Copy(srcPng, Path.GetFullPath(destPath), overwrite: true);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(destPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();

        if (!string.IsNullOrEmpty(atlasName))
            PackFolderIntoAtlas(categoryDir, atlasName);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AiTexture] imported {destPath} (atlas={atlasName})");
        return destPath;
    }

    /// <summary>카테고리 폴더를 아틀라스 packables에 등록(중복 시 무시).</summary>
    public static void PackFolderIntoAtlas(string categoryDir, string atlasName)
    {
        EnsureDir(ATLAS_DIR);
        string atlasPath = $"{ATLAS_DIR}/{atlasName}.spriteatlas";
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }

        var folder = AssetDatabase.LoadAssetAtPath<Object>(categoryDir);
        if (folder == null) return;

        foreach (var packed in atlas.GetPackables())
            if (packed == folder) return; // 이미 등록됨

        SpriteAtlasExtensions.Add(atlas, new Object[] { folder });
        EditorUtility.SetDirty(atlas);
    }

    private static void EnsureDir(string dir)
    {
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
            string leaf = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    // ── 스모크 검증용 메뉴 (아틀라스 생성·폴더 편입만 단독 확인) ──
    [MenuItem("WES/AI Texture/Smoke - Create Icons Atlas From ItemIcon")]
    public static void SmokeCreateIconsAtlas()
    {
        PackFolderIntoAtlas("Assets/GameResource/Image/ItemIcon", "Icons");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AiTexture] smoke: Icons.spriteatlas created/updated with ItemIcon folder");
    }
}
```

- [ ] **Step 2: 에셋 refresh**

MCP `u_editor_asset(action: refresh)` 호출(컴파일 트리거).

- [ ] **Step 3: 컴파일 에러 확인**

MCP `u_console` 로 컴파일 에러 조회.
Expected: `AiTextureImportSetup` 관련 컴파일 에러 0건.

- [ ] **Step 4: 스모크 실행 — 아틀라스 생성 검증**

MCP `u_editor_menu` 로 `WES/AI Texture/Smoke - Create Icons Atlas From ItemIcon` 실행.
이어서:
```bash
ls Assets/GameResource/UI/Atlas/Icons.spriteatlas
```
Expected: `Icons.spriteatlas` 파일 존재. `u_console`에 `smoke: Icons.spriteatlas created/updated` 로그.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Editor/AiTextureImportSetup.cs Assets/Scripts/Editor/AiTextureImportSetup.cs.meta Assets/GameResource/UI/Atlas
git commit -m "AiTextureImportSetup Editor 유틸: 생성 이미지 import + 카테고리 아틀라스 편입"
```

---

## Task 5: designer.md 갱신 (능력·절차 인코딩)

**Files:**
- Modify: `.claude/agents/designer.md`

- [ ] **Step 1: frontmatter tools에 playwright 도구 추가**

`tools:` 라인 끝(현재 `... mcp__mcp-unity__u_editor_menu`)에 이어서 추가:

```
, mcp__playwright__browser_navigate, mcp__playwright__browser_snapshot, mcp__playwright__browser_click, mcp__playwright__browser_type, mcp__playwright__browser_take_screenshot, mcp__playwright__browser_close, mcp__playwright__browser_wait_for
```

> 실제 playwright MCP 도구 prefix는 `claude mcp list`/`/mcp` 연결 후 노출되는 이름과 일치시킨다(`mcp__playwright__*`). Task 1 검증 후 정확한 도구명을 확인해 반영한다.

- [ ] **Step 2: 자산 우선순위 트리 §개정 — "1단계" 위에 0단계 추가**

`## 자산 우선순위 트리` 섹션의 `### 1단계 — GameResource/ 재사용` 바로 앞에 삽입:

```markdown
### 0단계 — 텍스처·2D 이미지: AI 생성 (최우선)
- **텍스처·2D sprite·아이콘·UI 이미지**는 재사용·차용·Procedural보다 **먼저 AI(Gemini)로 생성**한다.
- 3D 메쉬는 0단계 대상이 아니다 → 기존 1~4단계(재사용→차용→placeholder+백로그) 유지.
- 생성 절차는 아래 "## AI 텍스처 생성 (Gemini × Playwright)" 섹션을 따른다.
```

- [ ] **Step 3: "AI 텍스처 생성" 섹션 신설**

`## 워크플로우` 섹션 바로 앞에 삽입:

```markdown
## AI 텍스처 생성 (Gemini × Playwright)

텍스처·2D 이미지가 필요하면 다음 루프를 실행한다.

### 생성 루프 (최대 5회 재시도)
1. **프롬프트 작성** — WES 다크 톤 반영. 동일 세트면 `document/asset-style-guide/<세트명>.md`의 프리픽스를 앞에 붙인다.
2. `browser_navigate` → `https://gemini.google.com/app` (영구 프로필=로그인 유지)
3. `browser_type`으로 프롬프트 입력 → 전송 → `browser_wait_for`로 이미지 생성 대기
4. `browser_take_screenshot`으로 결과 자가 평가 (톤·용도 적합?)
5. 부적합 → 프롬프트 보정 후 2~4 반복. **최대 5회**.
6. 5회 실패 → **보류**: 가장 비슷한 기존/무료 자산으로 placeholder, `document/asset-backlog/<주제>.md`에 정식 의뢰 등록.
7. 성공 → 다운로드 버튼 클릭(`browser_click`) → 파일이 `.playwright-output/`에 저장됨 →
   `AiTextureImportSetup.ImportAndPack(srcPng, categoryDir, assetName, atlasName)` 호출(Editor 메뉴/스크립트 경유)로 GameResource 저장·import·아틀라스 편입.
8. 결과(성공·보류) team-lead에게 `SendMessage`로 보고.

> Gemini DOM(전송 버튼·다운로드 버튼)은 변동 가능. 셀렉터를 하드코딩하지 말고 매 실행 `browser_snapshot`으로 현재 접근성 트리에서 대상을 식별한다.

### Gemini 세션 / 스타일 관리
- **세트 일관성**: 같은 세트 아이콘은 **같은 채팅**에서 이어 생성.
- **스타일 고정**: 세션 첫 메시지에 스타일 기준 명시(스타일 가이드 프리픽스 사용).
- **세션 한도**: **15~20장**마다 채팅을 끊는다(초과 시 스타일 왜곡·지연·에러).
- **체인**: 새 채팅을 열고 직전 베스트 이미지 + 프리픽스를 투입해 "이 스타일로 이어서" 요청.
- **새 채팅**: 완전히 다른 화풍을 원할 때만.

### 저장 위치 결정 규칙
- 텍스처: `Assets/GameResource/Texture/`
- 일반 이미지: `Assets/GameResource/Image/`
- UI 이미지: `Assets/GameResource/UI/...`
- 아이콘류 sprite는 카테고리 아틀라스(`Assets/GameResource/UI/Atlas/Icons.spriteatlas`)에 편입.
```

- [ ] **Step 4: 검증 — frontmatter 유효성**

```bash
grep -n "mcp__playwright__browser_navigate" .claude/agents/designer.md
grep -n "0단계 — 텍스처·2D 이미지: AI 생성" .claude/agents/designer.md
grep -n "AI 텍스처 생성 (Gemini × Playwright)" .claude/agents/designer.md
```
Expected: 세 grep 모두 라인 매치 1건 이상.

- [ ] **Step 5: 커밋**

```bash
git add .claude/agents/designer.md
git commit -m "designer.md: AI 텍스처 생성 능력 추가(트리 0단계·생성 루프·세션 관리·아틀라스)"
```

---

## Task 6: RESOURCE_INVENTORY.md 정책 갱신

**Files:**
- Modify: `../document/RESOURCE_INVENTORY.md`

- [ ] **Step 1: 정책 섹션 추가**

`RESOURCE_INVENTORY.md`의 "자산 우선순위 트리" 관련 섹션(없으면 문서 끝)에 추가:

```markdown
## AI 텍스처 생성 정책 (2026-06-04 신설)

- **텍스처·2D 이미지**는 AI(Gemini 웹, Playwright MCP)로 직접 생성하는 것이 최우선이다.
- 생성·세션·아틀라스 규칙은 `.claude/agents/designer.md`의 "AI 텍스처 생성" 섹션을 단일 출처로 한다.
- 세트별 스타일 프리픽스는 `document/asset-style-guide/<세트명>.md`에 보관한다.
- 아이콘류 sprite는 카테고리 Sprite Atlas(`Assets/GameResource/UI/Atlas/`)에 편입한다.
- 3D 메쉬는 AI 생성 대상이 아니며 기존 백로그 트리를 유지한다.
```

- [ ] **Step 2: 검증**

```bash
grep -n "AI 텍스처 생성 정책" ../document/RESOURCE_INVENTORY.md
```
Expected: 매치 1건.

- [ ] **Step 3: 커밋**

```bash
cd ../document && git add RESOURCE_INVENTORY.md && git commit -m "RESOURCE_INVENTORY: AI 텍스처 생성 정책 추가" && cd -
```
(document가 별도 git이면 위처럼, 같은 리포면 일반 add/commit.)

---

## Task 7: End-to-End 스모크 (실제 1장 생성→저장→아틀라스)

**Files:** (코드 변경 없음 — 통합 검증)

- [ ] **Step 1: designer 에이전트로 단일 텍스처 의뢰**

designer 에이전트에게 의뢰: "모닥불 아이콘 1개 생성 — 다크 판타지 톤, `Assets/GameResource/Image/ItemIcon/campfire_icon.png`, Icons 아틀라스 편입."

- [ ] **Step 2: 검증 — 생성·import·아틀라스**

```bash
ls Assets/GameResource/Image/ItemIcon/campfire_icon.png
```
MCP `u_console`: import 로그 `[AiTexture] imported ... atlas=Icons` 확인.
MCP `u_editor_asset` 로 `Icons.spriteatlas`의 packables에 ItemIcon 폴더 포함 확인.
Expected: 파일 존재 + sprite 타입 + 아틀라스 편입.

- [ ] **Step 3: 실패 경로 검증 (보류)**

(선택) 의도적으로 불가능/모호한 의뢰를 주어 5회 재시도 후 `document/asset-backlog/`에 항목이 등록되고 team-lead 보고가 가는지 확인.

- [ ] **Step 4: 커밋**

```bash
git add Assets/GameResource/Image/ItemIcon/campfire_icon.png Assets/GameResource/Image/ItemIcon/campfire_icon.png.meta Assets/GameResource/UI/Atlas
git commit -m "E2E 스모크: Gemini 생성 campfire_icon import + Icons 아틀라스 편입"
```

---

## Self-Review Notes

- **스펙 커버리지**: §3 트리개정→Task5, §4 구성요소→Task1·4·5, §5 생성루프→Task5·7, §5.5 세션/스타일→Task3·5, §5.6 아틀라스→Task4·5, §7 선결/리스크→Task1·2, §8 DoD→Task2·4·7. 누락 없음.
- **fragile 영역**: Gemini DOM·다운로드 동작은 사전 확정 불가 → Task2 스모크로 실제 동작을 관찰한 뒤 designer.md 절차를 보정한다(셀렉터 하드코딩 금지 명시).
- **타입 일관성**: `ImportAndPack`/`PackFolderIntoAtlas` 시그니처가 Task4 정의 ↔ Task5·7 호출에서 일치.
- **MCP 도구명**: playwright 도구 prefix(`mcp__playwright__*`)는 Task1 연결 후 실제 노출명으로 확정(Task5 Step1 주석).
