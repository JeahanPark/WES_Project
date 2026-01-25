using System;
using System.Collections.Generic;
using Unity.Netcode;

public class InGameObjectDataWorker : NetworkBehaviour
{
    private Dictionary<ulong, PlayerCharacter> m_PlayerMap = new();

    // Events
    private event Action<ulong> m_OnPlayerDead;

    public void SubscribeOnPlayerDead(Action<ulong> _callback)
    {
        m_OnPlayerDead += _callback;
    }

    public void UnsubscribeOnPlayerDead(Action<ulong> _callback)
    {
        m_OnPlayerDead -= _callback;
    }

    public PlayerCharacter GetPlayer(ulong _networkObjectId)
    {
        if (m_PlayerMap.TryGetValue(_networkObjectId, out PlayerCharacter player))
        {
            return player;
        }

        return null;
    }

    public void RegisterPlayer(PlayerCharacter _player)
    {
        ulong id = _player.NetworkObjectId;

        if (m_PlayerMap.ContainsKey(id)) return;

        m_PlayerMap[id] = _player;
    }

    public void UnregisterPlayer(ulong _networkObjectId)
    {
        m_PlayerMap.Remove(_networkObjectId);
    }

    public void NotifyPlayerDead(ulong _networkObjectId)
    {
        OnPlayerDeadClientRpc(_networkObjectId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnPlayerDeadClientRpc(ulong _networkObjectId)
    {
        m_OnPlayerDead?.Invoke(_networkObjectId);
    }
}
