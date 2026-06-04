# WES 리소스 인벤토리 (정책·톤·자산 우선순위 트리)

> **대상 독자**: 디자이너 에이전트 (`.claude/agents/designer.md`)
> **갱신 주기**: 외부 에셋 패키지 신규 import 또는 폴더 정책 변경 시 수동 갱신
> **관련 문서**: [RESOURCE_USAGE_REPORT.md](RESOURCE_USAGE_REPORT.md) — 자동 생성되는 사용 통계

본 문서는 **거시 정책·톤·출처** 정리이며, 자산별 사용 여부는 별도 자동 생성 리포트(`RESOURCE_USAGE_REPORT.md`)에서 다룬다. 디자이너는 의뢰 시작 시 본 문서를 먼저 읽는다.

---

## 1. 폴더 정책

| 폴더 | 정책 | 톤 | 빌드 포함 |
|---|---|---|---|
| `Assets/GameResource/` | **게임 실제 사용** (정식 자산 보관소) | WES 공식 톤 | ✅ 전체 |
| `Assets/Synty/PolygonPrototype/` | 선택적 사용 — 프로토타입용 환경·건물·UI 아이콘 | 로우폴리, 채도 높음 | △ 참조되는 것만 |
| `Assets/Synty/PolygonGeneric/` | 선택적 사용 — 범용 props | 로우폴리, Prototype과 호환 | △ 참조되는 것만 |
| `Assets/polyperfect/Low Poly Animated People/` | 캐릭터 베이스 (Player·NPC) | 로우폴리 인간 | △ 참조되는 것만 |
| `Assets/Resources/` | 런타임 `Resources.Load` 대상 | — | ✅ 전체 |
| `Assets/Scenes/` | 빌드 씬 (`Intro`, `Lobby`, `Ingame`) | — | ✅ 활성 씬만 |
| `Assets/AddressableAssetsData/` | Addressable 설정 | — | ✅ 그룹 자산 |
| `Assets/CSVInfo/` | CSV 원본 (코드 모델로 변환되는 데이터 소스) | — | ❌ (CSV는 빌드 제외, 변환된 .cs만 포함) |

### `Assets/GameResource/` 내부 구조

| 하위 폴더 | 용도 |
|---|---|
| `UI/Popup/` | Popup 프리팹 (PopupManager 로드 대상) |
| `UI/HUD/` | HUD 프리팹 (InGameHUDWorker 자식) |
| `Character/` | 인게임 캐릭터·몬스터 프리팹 (Player, Test01Monster 등) |
| `Item/` | 아이템 아이콘 sprite + 메쉬 |
| `Image/` | 일반 UI 이미지 |
| `Config/` | ScriptableObject 에셋 (DayNightConfig 등) |
| (필요 시) `Sound/`, `Font/`, `Material/`, `Animation/` | 사운드·폰트·머티리얼·애니메이션 |

---

## 2. 외부 에셋 출처

| 패키지 | 카테고리 | WES 활용 정책 |
|---|---|---|
| **Synty PolygonPrototype** | 프로토타입용 메쉬 (건물·환경·primitive·UI 아이콘) | 환경·건물 메쉬 직접 사용 가능. UI 아이콘은 WES 톤 확인 후 사용. |
| **Synty PolygonGeneric** | 범용 props·환경·도시 | 보조용. 게임 톤 맞으면 차용. |
| **polyperfect Low Poly Animated People** | 인간 캐릭터 + 애니메이션 클립 | 플레이어/NPC 메쉬 베이스. 직접 부착 가능. |
| (향후 후보) **Synty PolygonFantasyRivals / Animal** | 4족 동물·판타지 캐릭터 | 어둠늑대 같은 동물 콘텐츠 시 구매 후보 |
| (향후 후보) **Quaternius 무료 팩** | 로우폴리 자산 | 4족 동물·무기 등 무료 대안 |
| (향후 후보) **Mixamo** | 캐릭터 애니메이션 | Idle/Walk/Run/Attack 등 |

---

## 3. 자산 우선순위 트리 (디자이너 의사결정 룰)

필요 리소스가 발생하면 **순서대로** 확인 후 결정:

```
[텍스처·2D 이미지] 0단계 — AI 생성 (Gemini × Playwright, 최우선)
   ↓ (3D 메쉬는 0단계 건너뜀)
1단계 — GameResource/ 재사용
   ↓ 없거나 불충분
2단계 — Synty/polyperfect 차용 (복제 후 GameResource로 이동·rename)
   ↓ 없거나 불충분
3단계 — Procedural 생성 (Texture2D + Sprite.Create / 머티리얼 변형 / SO 등)
   ↓ 불가능
4단계 — 임시 placeholder + document/asset-backlog/<주제>.md 정식 의뢰
```

### 0단계 — 텍스처·2D 이미지: AI 생성 (최우선)

- **텍스처·2D sprite·아이콘·UI 이미지**는 재사용·차용·Procedural보다 **먼저 AI(Gemini 웹, Playwright MCP)로 생성**한다.
- 3D 메쉬는 0단계 대상이 아니다 → 1~4단계 트리를 그대로 탄다.
- 생성·세션·아틀라스 규칙의 단일 출처는 `.claude/agents/designer.md`의 "AI 텍스처 생성 (Gemini × Playwright)" 섹션.
- 세트별 스타일 프리픽스는 `document/asset-style-guide/<세트명>.md`에 보관.
- 아이콘류 sprite는 카테고리 Sprite Atlas(`Assets/GameResource/UI/Atlas/`)에 편입.
- 최대 5회 재시도 후 실패 시 보류(placeholder + 백로그).

### 1단계 — GameResource 재사용

- `Glob`/`Grep`으로 동일·유사 자산 검색
- 발견 시 즉시 사용. 머티리얼 색상·스케일·노멀 변경으로 변형판 가능

### 2단계 — Synty/polyperfect 차용

- 외부 에셋 폴더에서 검색
- 발견 시 복제 → `Assets/GameResource/<카테고리>/<이름>/`로 이동 + rename
- 라이선스 안전 (이미 사용 권한이 확보된 패키지)
- 머티리얼은 WES 톤에 정렬되도록 변경 (필요 시)

### 3단계 — Procedural 생성

LLM/코드만으로 가능한 자산:

| 가능한 것 | 방법 |
|---|---|
| 단순 sprite (흰 원·단색 사각형·그라데이션) | Texture2D + 픽셀 채우기 + Sprite.Create |
| 머티리얼 색상·강도 변형 | Material 인스턴스 생성 + property override |
| Transform Scale·Rotation 변형 (시각 구분) | SerializedObject 또는 `u_set_transform` |
| ScriptableObject 에셋 | `ScriptableObject.CreateInstance<T>()` + `AssetDatabase.CreateAsset` |
| UI 프리팹 1차 구조 (Canvas·Image·Text·Layout) | `generate_ui_with_gpt` MCP 또는 Editor 스크립트 |

Editor 메뉴 스크립트(`Assets/Scripts/Editor/<주제>Setup.cs`)에 묶어 한 메뉴로 자동화.

### 4단계 — 임시 placeholder + 외부 자산 백로그

LLM/코드로 불가능한 자산:
- 캐릭터·몬스터 메쉬 (4족 동물, 신규 인간 등)
- 시각적 아이덴티티 sprite (페이즈 아이콘, 게임 아이콘 등)
- 정교한 텍스처·머티리얼
- 애니메이션 클립
- 사운드

처리:
- 가장 비슷한 기존 자산으로 임시 placeholder
- `document/asset-backlog/<주제>.md` 작성·갱신 (자산명·용도·우선순위·시각 요구·임시 처리·대체 출처)
- team-lead에게 SendMessage로 백로그 보고

---

## 4. 외부 자산 도착 후 import 자동화

사용자가 외부 자산을 `Assets/GameResource/<카테고리>/<이름>/`에 드롭한 뒤 디자이너에게 의뢰하면:

1. Import 설정 점검 (sprite slice, FBX import settings, Animation type 등)
2. 머티리얼 WES 톤 정렬 (색상·셰이더 조정)
3. 기존 임시 placeholder 자산 → 정식 자산 교체
4. 프리팹 메쉬·sprite·머티리얼 슬롯 자동 교체 (인터페이스는 유지)
5. `document/asset-backlog/<주제>.md`에서 해당 항목 해결 처리(`✅`)
6. 본 인벤토리 폴더 정책에 신규 항목 반영 (신규 폴더가 생긴 경우)

---

## 5. 인벤토리 갱신 트리거

본 문서를 디자이너가 직접 갱신하는 시점:

- 사용자가 외부 에셋 패키지를 신규 import (`Assets/<새폴더>/`)
- 폴더 정책 변경 (예: `GameResource/Sound/` 폴더 신설)
- 외부 에셋 출처 추가 (Synty 추가 패키지 구매 등)

평소엔 정적 — 자주 안 바뀐다.

---

## 6. 사용 여부 확인이 필요할 때

**사용 통계**가 필요하면 `RESOURCE_USAGE_REPORT.md`(자동 생성) 참조. 없거나 24시간 이상 stale이면 디자이너가 `WES/Tools/Generate Resource Usage Report` 메뉴 호출해 갱신.

호출 시점은 `designer.md`의 "리소스 명세서 갱신 의무" 섹션 참조.
