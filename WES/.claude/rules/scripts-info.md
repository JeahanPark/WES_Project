---
paths:
  - "Assets/Scripts/Info/**"
  - "Assets/CSVInfo/**"
---

# Info / CSV 데이터 시스템

CSV 기획 데이터를 C# 클래스로 변환하는 파이프라인.

## 흐름
`CSVInfo/*.csv` → `InfoConvertEditor` (에디터 도구) → `Scripts/Info/Generator/ConvertInfo/*.cs` (자동 생성)

## CSVInfo/ — 기획 데이터 (11개)
BuildingInfo, CraftConditionInfo, CraftInfo, CraftMaterialInfo, DropSourceInfo, DropTableItemInfo, ItemInfo, MonsterInfo, WorldAreaInfo, WorldAreaMonsterInfo, WorldObjectInfo

## Scripts/Info/
- `InfoManager.cs` — Info 통합 매니저
- `InfoLoader.cs` — CSV 로드
- `Generator/ConvertInfo/` — CSV에서 자동 생성된 C# 클래스들 (직접 수정 금지)
