---
작성일: 2026-05-05
주제: 게임 종료 결과 팝업 (Clear / GameOver 공용)
상태: 승인됨 (옵션 A 미니멀)
---

# 결과 팝업 — 설계 문서

## 1. 배경

현재 클리어/게임오버 시 `CoReturnToLobby(3f)` 코루틴으로 3초 후 자동 로비 복귀 — 결과 화면이 없어서 무엇이 일어났는지 사용자가 인지하기 어렵다.

## 2. 결정된 동작 (미니멀 A)

- 클리어 또는 게임오버 발화 시 `ResultPopup` 띄움 (자동 로비 복귀 제거)
- 팝업 내용:
  - 큰 타이틀: "탈출 성공!" (Clear, 녹색) / "전멸..." (GameOver, 빨간색)
  - 부제: "마을에 무사히 도달했습니다." / "모두 쓰러졌습니다."
  - [확인] 버튼 → 로비 복귀
- 통계(처치 몬스터/플레이 시간 등)는 후속 작업

## 3. 변경 범위 (4개 파일 + 1개 프리팹)

| 파일 | 변경 |
|------|------|
| `Assets/Scripts/UI/Popup/ResultPopup.cs` (신규) | `BasePopup` 상속, `ShowResult(GameState)` 메서드, 확인 버튼 핸들러 |
| `Assets/GameResource/UI/Popup/ResultPopup.prefab` (신규) | UGUI 패널 + 타이틀/부제 텍스트 + 확인 버튼 |
| Addressable 등록 (key: `ResultPopup`) | `Default Local Group` |
| `Assets/Scripts/Controller/InGameController.cs` | `TriggerClearClientRpc`/`TriggerGameOverClientRpc`에서 `CoReturnToLobby(3f)` 제거 → `Managers.Popup.Open<ResultPopup>().ShowResult(state)` |
| `Assets/Scripts/Manager/TestManager.cs` | QA 시나리오 — 사망 시 `ResultPopup` 열림 검증 |

## 4. 데이터 흐름

```
InGameController.TriggerGameOverClientRpc (모든 클라이언트)
  m_GameState = GameOver
  Managers.Popup.Open<ResultPopup>().ShowResult(GameState.GameOver)
    └─ 타이틀 "전멸...", 빨간색
    └─ 부제 "모두 쓰러졌습니다."

[확인 버튼]
  Managers.Scene.LoadScene(SCENE_LOBBY)
```

## 5. ResultPopup API

```csharp
public class ResultPopup : BasePopup
{
    [SerializeField] private TMP_Text m_TitleText;
    [SerializeField] private TMP_Text m_SubtitleText;
    [SerializeField] private Button m_ConfirmButton;

    private void Awake()
    {
        m_ConfirmButton.onClick.AddListener(OnClickConfirm);
    }

    public void ShowResult(GameState _state)
    {
        if (_state == GameState.Clear)
        {
            m_TitleText.text = "탈출 성공!";
            m_TitleText.color = new Color(0.2f, 1f, 0.4f);
            m_SubtitleText.text = "마을에 무사히 도달했습니다.";
        }
        else
        {
            m_TitleText.text = "전멸...";
            m_TitleText.color = new Color(1f, 0.3f, 0.3f);
            m_SubtitleText.text = "모두 쓰러졌습니다.";
        }
    }

    private void OnClickConfirm()
    {
        Close();
        Managers.Scene.LoadScene(GameSceneManager.SCENE_LOBBY);
    }
}
```

## 6. 엣지 케이스

| 케이스 | 처리 |
|--------|------|
| 팝업이 이미 열려있는 상태에서 GameOver | `Open<ResultPopup>()`이 새 인스턴스 생성 — 정책 `CloseAllAndOpen`으로 설정해 기존 팝업 닫고 결과만 표시 |
| ESC 키로 결과 팝업 닫힘 | 결과 팝업의 `OpenPolicy = StackOnTop` + ESC로 닫히면 게임 그대로 멈춤 — 차라리 `CloseAllAndOpen`으로 하여 다른 팝업 다 닫고, ESC 비활성은 별도 작업. 본 스펙은 단순화로 닫혀도 게임 진행 안 됨(3초 자동 복귀가 없으므로 무한 정지) — 따라서 ResultPopup은 **ESC로 닫히지 않게 BasePopup에 SuppressEsc 옵션 추가**하거나, OnClick만 받도록 처리. 가장 안전한 단순화: ResultPopup이 닫히면 자동으로 로비 복귀하도록 OnDisable에서 처리. |
| 확인 버튼 두 번 빠르게 클릭 | `m_ConfirmButton.interactable = false` 설정 후 LoadScene |

## 7. QA 시나리오

`TestResultPopupOnGameOver`:
1. 플레이어 사망 → 게임오버 발화
2. `Managers.Popup.FindOpen<ResultPopup>() != null` 확인
3. `ResultPopup`의 타이틀 텍스트 == "전멸..."
4. `Managers.Popup.OpenedCount > 0`

## 8. 후속 작업

- 통계 표시 (처치 몬스터, 플레이 시간, 수집 자원 등)
- 클리어 전용 BGM/효과음
- 결과 화면 애니메이션 (타이틀 페이드 인 등)
- "다시 시작" 버튼 (현재는 "확인 → 로비"만)
