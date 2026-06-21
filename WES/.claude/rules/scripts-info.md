---
paths:
  - "Assets/Scripts/Info/**"
  - "Assets/CSVInfo/**"
---

# Info / CSV 데이터 시스템

CSV 기획 데이터를 C# 클래스로 변환하는 파이프라인.

## 흐름
`CSVInfo/*.csv` → `InfoConvertEditor` (에디터 도구, `Tools/InfoConvert`) → `Scripts/Info/Generator/ConvertInfo/*.cs` (자동 생성, `partial class`) → 런타임 Reflection 파싱(`InfoLoader.LoadInfo<T>`)

- **CSV 파일명은 반드시 `*Info.csv`** — `LoadInfo<T>`가 `typeof(T).Name`으로 경로를 만든다(클래스명=파일명).
- **`,` split** — STRING 필드에 콤마 불가. 분포/리스트류는 별도 행 테이블로.
- 헤더 형식 `필드명.TYPE` (INT/LONG/FLOAT/DOUBLE/STRING/BOOL/ENUM). ENUM은 `필드명.ENUM` → 타입=필드명(예: `WeatherType.ENUM`).

## CSVInfo/ — 기획 데이터 (13개)
BuildingInfo, CraftConditionInfo, CraftInfo, CraftMaterialInfo, DropSourceInfo, DropTableItemInfo, ItemInfo, MonsterInfo, WeatherInfo, WorldAreaInfo, WorldAreaMonsterInfo, WorldAreaWeatherInfo, WorldObjectInfo

## Scripts/Info/
- `InfoManager.cs` — Info 통합 매니저
- `InfoCustom.cs` — 자동생성 클래스의 커스텀 멤버(계산 프로퍼티 등) 보존용 `partial`. 예: `ItemInfo.IsBuilding`. **재생성에 안전한 커스텀은 여기.**
- `Generator/InfoLoader.cs` — CSV 로드 (자동 생성)
- `Generator/ConvertInfo/` — CSV에서 자동 생성된 `partial class` 들 (직접 수정 금지 — 커스텀은 InfoCustom.cs)
