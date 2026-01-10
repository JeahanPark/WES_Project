using UnityEngine;

public class LobbyRoomChatScrollData
{
    public ulong SenderId;
    public string Message;
    public bool IsMyMessage;
}

public class LobbyRoomChatScroll : BaseScroll<LobbyRoomChatScrollData>
{
}
