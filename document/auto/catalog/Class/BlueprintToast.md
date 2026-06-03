---
name: BlueprintToast
category: UI
parent: "[[MonoBehaviour]]"
file_path: WES/Assets/Scripts/UI/HUD/BlueprintToast.cs
role: 도면 해금 알림 토스트 ("○○ 도면을 익혔다" 1줄 페이드)
status: Active
signals: []
---

# BlueprintToast

도면 해금 순간 화면 하단에 절제된 1줄("○○ 도면을 익혔다")을 페이드 인→표시→페이드 아웃으로 보여주는 HUD 토스트. 톤: 파티클·팡파레 금지(기획 §11.1).

## 책임 영역

- `ShowMessage(message)`: 텍스트 설정 후 `CoShow` 코루틴(페이드 0.3s → 표시 3.0s → 페이드 0.3s).
- 슬롯: `m_CanvasGroup`(알파 제어), `m_MessageText`(TMP).
- 프리팹 `Assets/GameResource/UI/HUD/BlueprintToast.prefab`, [[InGameHUDWorker]] 하위 배치.

## 관련

- 부모: [[MonoBehaviour]]
- 구동: [[InGameHUDWorker]]가 [[RecipeUnlockRegistry]]`.OnUnlockChanged` 구독 → `ShowMessage` 호출
