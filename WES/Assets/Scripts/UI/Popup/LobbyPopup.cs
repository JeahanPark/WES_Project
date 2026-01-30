using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPopup : BasePopup
{
    [SerializeField] private Button m_EnterButton;
    [SerializeField] private Button m_CreateButton;
    [SerializeField] private GameObject m_CodeInputPanel;
    [SerializeField] private TMP_InputField m_CodeInputField;
    [SerializeField] private Button m_CodeConfirmButton;

    private void Awake()
    {
        m_EnterButton.onClick.AddListener(OnClickEnter);
        m_CreateButton.onClick.AddListener(OnClickCreate);
        m_CodeConfirmButton.onClick.AddListener(OnClickCodeConfirm);

        m_CodeInputPanel.SetActive(false);
    }

    private void OnClickEnter()
    {
        m_CodeInputPanel.SetActive(true);
        m_CodeInputField.text = string.Empty;
        m_CodeInputField.ActivateInputField();
    }

    private void OnClickCreate()
    {
        StartCoroutine(CoCreateRoom());
    }

    private void OnClickCodeConfirm()
    {
        string code = m_CodeInputField.text;

        if (string.IsNullOrEmpty(code))
        {
            GameDebug.LogWarning("Room code is empty");
            return;
        }

        StartCoroutine(CoJoinRoom(code));
    }

    private IEnumerator CoCreateRoom()
    {
        m_CreateButton.interactable = false;

        var task = Managers.Network.HostRelayAsync(destroyCancellationToken);
        yield return new WaitUntil(() => task.Status.IsCompleted());

        if (task.Status == UniTaskStatus.Succeeded)
        {
            string roomCode = task.GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(roomCode))
            {
                GameDebug.Log($"Room created with code: {roomCode}");
                Managers.Popup.Open<LobbyRoomPopup>();
            }
            else
            {
                GameDebug.LogError("Failed to create room");
            }
        }
        else
        {
            GameDebug.LogError($"Error creating room: {task.AsTask().Exception}");
        }

        m_CreateButton.interactable = true;
    }

    private IEnumerator CoJoinRoom(string _code)
    {
        m_CodeConfirmButton.interactable = false;

        var task = Managers.Network.JoinRelayAsync(_code, destroyCancellationToken);
        yield return new WaitUntil(() => task.Status.IsCompleted());

        if (task.Status == UniTaskStatus.Succeeded)
        {
            bool success = task.GetAwaiter().GetResult();
            if (success)
            {
                GameDebug.Log($"Successfully joined room with code: {_code}");
                Managers.Popup.Open<LobbyRoomPopup>();
            }
            else
            {
                GameDebug.LogError("Failed to join room");
            }
        }
        else
        {
            GameDebug.LogError($"Error joining room: {task.AsTask().Exception}");
        }

        m_CodeConfirmButton.interactable = true;
    }
}
