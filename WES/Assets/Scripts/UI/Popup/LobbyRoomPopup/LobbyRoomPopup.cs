using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class LobbyRoomPopup : BasePopup
{
    [Header("Title")]
    [SerializeField] private TMP_Text m_TitleText;

    [Header("Room Info")]
    [SerializeField] private TMP_Text m_RoomCodeText;
    [SerializeField] private Button m_InviteButton;

    [Header("Player Slots")]
    [SerializeField] private TMP_Text m_Player1Text;
    [SerializeField] private TMP_Text m_Player2Text;
    [SerializeField] private TMP_Text m_Player3Text;
    [SerializeField] private TMP_Text m_Player4Text;

    [Header("Chat")]
    [SerializeField] private LobbyRoomChatScroll m_ChatScroll;
    [SerializeField] private TMP_InputField m_ChatInputField;

    [Header("Actions")]
    [SerializeField] private Button m_StartGameButton;

    private const string EMPTY_SLOT_TEXT = "[EMPTY]";
    private const string HOST_PREFIX = "[HOST] ";
    private const string ME_SUFFIX = " (Me)";

    private CompositeDisposable m_Disposables = new CompositeDisposable();
    private List<LobbyRoomChatScrollData> m_ChatMessages = new List<LobbyRoomChatScrollData>();

    private void Awake()
    {
        m_InviteButton.onClick.AddListener(OnClickInvite);
        m_StartGameButton.onClick.AddListener(OnClickStartGame);
        m_ChatInputField.onSubmit.AddListener(OnChatInputSubmit);

        InitializeUI();
    }

    private void OnEnable()
    {
        Managers.Network.OnPlayerJoinedAsObservable.Subscribe(_ => UpdatePlayerSlots()).AddTo(m_Disposables);
        Managers.Network.OnPlayerLeftAsObservable.Subscribe(_ => UpdatePlayerSlots()).AddTo(m_Disposables);
        Managers.Chat.OnMessageReceivedAsObservable.Subscribe(OnChatMessageReceived).AddTo(m_Disposables);
        Managers.Input.OnEnterAsObservable.Subscribe(_ => OnEnterKeyPressed()).AddTo(m_Disposables);
    }

    private void OnDisable()
    {
        m_Disposables.Clear();
    }

    private void Start()
    {
        UpdateRoomCode();
        UpdatePlayerSlots();
        TestAddChatMessages();
    }

    private void TestAddChatMessages()
    {
        for (int i = 1; i <= 20; i++)
        {
            LobbyRoomChatScrollData testData = new LobbyRoomChatScrollData
            {
                SenderId = (ulong)i,
                Message = $"Test message {i}",
                IsMyMessage = i % 3 == 0
            };
            m_ChatMessages.Add(testData);
        }
        m_ChatScroll.SetData(m_ChatMessages);
    }

    private void InitializeUI()
    {
        m_Player1Text.text = EMPTY_SLOT_TEXT;
        m_Player2Text.text = EMPTY_SLOT_TEXT;
        m_Player3Text.text = EMPTY_SLOT_TEXT;
        m_Player4Text.text = EMPTY_SLOT_TEXT;
    }

    private void UpdateRoomCode()
    {
        string code = Managers.Network.GetCode;
        m_RoomCodeText.text = string.IsNullOrEmpty(code) ? "ROOM CODE : ------" : $"ROOM CODE : {code}";
    }

    private void UpdatePlayerSlots()
    {
        ulong[] clientIds = Managers.Network.GetConnectedClientIds();
        ulong localClientId = Managers.Network.GetLocalClientId();

        m_Player1Text.text = GetPlayerSlotText(clientIds, 0, localClientId);
        m_Player2Text.text = GetPlayerSlotText(clientIds, 1, localClientId);
        m_Player3Text.text = GetPlayerSlotText(clientIds, 2, localClientId);
        m_Player4Text.text = GetPlayerSlotText(clientIds, 3, localClientId);
    }

    private string GetPlayerSlotText(ulong[] _clientIds, int _slotIndex, ulong _localClientId)
    {
        if (_slotIndex >= _clientIds.Length)
            return EMPTY_SLOT_TEXT;

        bool isHost = _slotIndex == 0;
        bool isMe = _clientIds[_slotIndex] == _localClientId;

        string prefix = isHost ? HOST_PREFIX : "";
        string suffix = isMe ? ME_SUFFIX : "";

        return $"{prefix}Player {_slotIndex + 1}{suffix}";
    }

    private void OnClickInvite()
    {
        string code = Managers.Network.GetCode;
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"Room code copied to clipboard: {code}");
        }
    }

    private void OnClickStartGame()
    {
        if (!Managers.Network.IsServer)
        {
            Debug.LogWarning("Only host can start the game");
            return;
        }

        Debug.Log("Starting game...");
        GameSceneManager.Instance.LoadScene(GameSceneManager.SCENE_INGAME);
    }
    private void OnChatInputSubmit(string _text)
    {
        SendChatMessage();
    }

    private void SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(m_ChatInputField.text))
            return;

        Managers.Chat.SendChatMessage(m_ChatInputField.text);
        m_ChatInputField.text = string.Empty;
        m_ChatInputField.ActivateInputField();
    }

    private void OnChatMessageReceived(ChatMessage _chatMessage)
    {
        ulong localClientId = Managers.Network.GetLocalClientId();

        LobbyRoomChatScrollData chatData = new LobbyRoomChatScrollData
        {
            SenderId = _chatMessage.SenderId,
            Message = _chatMessage.Message,
            IsMyMessage = _chatMessage.SenderId == localClientId
        };

        m_ChatMessages.Add(chatData);
        m_ChatScroll.SetData(m_ChatMessages);
    }

    private void OnEnterKeyPressed()
    {
        // InputField가 포커스되어 있을 때만 채팅 전송
        if (m_ChatInputField.isFocused)
            SendChatMessage();
    }
}
