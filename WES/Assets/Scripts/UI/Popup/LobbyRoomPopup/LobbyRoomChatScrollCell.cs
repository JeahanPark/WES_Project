using UnityEngine;
using TMPro;

public class LobbyRoomChatScrollCell : BaseScrollCell<LobbyRoomChatScrollData>
{
    [SerializeField] private TMP_Text m_MessageText;

    private void Awake()
    {
        if (m_MessageText != null)
        {
            m_MessageText.raycastTarget = false;
        }
    }

    protected override void OnUpdateCell(int _index, LobbyRoomChatScrollData _data)
    {
        if (_data == null)
            return;

        if (m_MessageText != null)
        {
            string formattedMessage = $"{_data.SenderId} : {_data.Message}";
            m_MessageText.text = formattedMessage;
        }
    }
}
