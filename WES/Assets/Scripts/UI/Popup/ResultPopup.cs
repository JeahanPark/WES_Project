using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultPopup : BasePopup
{
    private static readonly Color CLEAR_COLOR = new Color(0.2f, 1f, 0.4f);
    private static readonly Color GAMEOVER_COLOR = new Color(1f, 0.3f, 0.3f);

    [SerializeField] private TMP_Text m_TitleText;
    [SerializeField] private TMP_Text m_SubtitleText;
    [SerializeField] private Button m_ConfirmButton;

    private void Awake()
    {
        if (m_ConfirmButton != null)
            m_ConfirmButton.onClick.AddListener(OnClickConfirm);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetIngameUIVisible(false);
    }

    private void OnDisable()
    {
        SetIngameUIVisible(true);
    }

    private void SetIngameUIVisible(bool _visible)
    {
        var controller = InGameController.Instance;
        if (controller == null)
            return;

        if (controller.HUDWorker != null)
            controller.HUDWorker.gameObject.SetActive(_visible);
        if (controller.WorldUIWorker != null)
            controller.WorldUIWorker.gameObject.SetActive(_visible);
    }

    public void ShowResult(GameState _state)
    {
        if (_state == GameState.Clear)
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = "탈출 성공!";
                m_TitleText.color = CLEAR_COLOR;
            }
            if (m_SubtitleText != null)
                m_SubtitleText.text = "마을에 무사히 도달했습니다.";
        }
        else
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = "전멸...";
                m_TitleText.color = GAMEOVER_COLOR;
            }
            if (m_SubtitleText != null)
                m_SubtitleText.text = "모두 쓰러졌습니다.";
        }
    }

    private void OnClickConfirm()
    {
        if (m_ConfirmButton != null)
            m_ConfirmButton.interactable = false;
        Close();
        Managers.Scene.LoadScene(GameSceneManager.SCENE_LOBBY);
    }
}
