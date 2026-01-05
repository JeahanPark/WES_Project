using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoginPopup : BasePopup
{
    [SerializeField] private Button m_StartButton;
    [SerializeField] private Button m_OptionButton;
    [SerializeField] private Button m_ExitButton;

    private void Awake()
    {
        m_StartButton.onClick.AddListener(OnClickStart);
        m_OptionButton.onClick.AddListener(OnClickOption);
        m_ExitButton.onClick.AddListener(OnClickExit);
    }

    private void OnClickStart()
    {
        SceneManager.LoadScene("Lobby");
    }

    private void OnClickOption()
    {
        Debug.Log("Option button clicked");
    }

    private void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
