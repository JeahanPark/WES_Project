---
name: RecipeUnlockRegistry
category: Domain
parent: null
file_path: WES/Assets/Scripts/InGameObjectData/RecipeUnlockRegistry.cs
role: 레시피 도면 해금 상태(세션·클라이언트 로컬) — 도면 해금(②)의 핵심 상태
status: Active
signals: ["RecipeUnlockRegistry.OnUnlockChanged"]
---

# RecipeUnlockRegistry

도면(Blueprint)으로 해금된 레시피 집합을 **현재 세션·현재 클라이언트 로컬**로 관리. 직렬화·영구 저장 없음 → 세션 종료 시 `Clear`로 초기화(영구 진행 안티골 준수).

## 책임 영역

- `IsUnlocked(craftId)`: 도면 매핑 없는 CraftId(기본 5종)는 **항상 true**. 잠금 대상(도면 3종 = CraftId 5·6·7)이면 해금 집합 포함 여부. 판정은 [[InfoManager]]`.IsBlueprintLockedCraft` 위임.
- `Unlock(craftId)`: HashSet 추가. **신규일 때만** `OnUnlockChanged(craftId)` 발화(중복 해금 무효).
- `Clear()`: 세션 리셋 — `InGameObjectDataWorker.ResetSessionData()`에서 인벤토리 Clear와 일원 호출.
- 멀티 = 플레이어별 개인 해금(클라 로컬 + sender 단독 ClientRpc 구조라 자동 보장).

## 관련

- 보유: [[InGameObjectDataWorker]] (`GetRecipeUnlockRegistry`)
- 협력: [[InfoManager]] (BlueprintInfo 매핑) · [[WorldDropItem]] (줍기→Unlock) · [[BlueprintToast]]·CraftPopup (`OnUnlockChanged` 구독)
- 설계: `document/design/game-design/도면해금/기획.md` · `document/design/client-spec/도면해금/코드명세.md`
