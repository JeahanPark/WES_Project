using System;
using Unity.Netcode;
using UnityEngine;
using UniRx;

public class ChatManager : MonoSingleton<ChatManager>
{
    public IObservable<ChatMessage> OnMessageReceivedAsObservable => m_OnMessageReceived;

    private readonly Subject<ChatMessage> m_OnMessageReceived = new Subject<ChatMessage>();

    public override void Init()
    {
        base.Init();
    }

    public override void Clear()
    {
        base.Clear();
        m_OnMessageReceived?.Dispose();
    }

    public void SendChatMessage(string _message)
    {
        if (string.IsNullOrWhiteSpace(_message))
        {
            GameDebug.LogWarning("[ChatManager] Cannot send empty message");
            return;
        }

        if (!Managers.Network.IsRunning)
        {
            GameDebug.LogError("[ChatManager] Network is not running");
            return;
        }

        ulong senderId = Managers.Network.GetLocalClientId();
        SendChatMessageServerRpc(_message, senderId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendChatMessageServerRpc(string _message, ulong _senderId)
    {
        BroadcastChatMessageClientRpc(_message, _senderId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastChatMessageClientRpc(string _message, ulong _senderId)
    {
        ChatMessage chatMessage = new ChatMessage
        {
            SenderId = _senderId,
            Message = _message,
            Timestamp = DateTime.Now
        };

        m_OnMessageReceived.OnNext(chatMessage);
        GameDebug.Log($"[ChatManager] Message from {_senderId}: {_message}");
    }
}

public struct ChatMessage
{
    public ulong SenderId;
    public string Message;
    public DateTime Timestamp;
}
