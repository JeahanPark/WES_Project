using System.Collections.Generic;

/// <summary>
/// 캐릭터(플레이어/몬스터) 데이터 관리
/// </summary>
/// <remarks>
/// 현재: 로컬 Dictionary로 관리 (NetworkObject 스폰 시 각 클라이언트에서 등록)
/// 고려사항: 늦게 접속한 클라이언트 처리 필요 시 NetworkList 또는 RPC 동기화 검토
/// </remarks>
public class CharacterRegistry
{
    private Dictionary<ulong, CharacterBase> m_CharacterMap = new();

    public void RegisterCharacter(CharacterBase _character)
    {
        ulong id = _character.NetworkObjectId;

        if (m_CharacterMap.ContainsKey(id))
            return;

        m_CharacterMap[id] = _character;
    }

    public void UnregisterCharacter(ulong _networkObjectId)
    {
        m_CharacterMap.Remove(_networkObjectId);
    }

    public CharacterBase GetCharacter(ulong _networkObjectId)
    {
        if (m_CharacterMap.TryGetValue(_networkObjectId, out CharacterBase character))
        {
            return character;
        }

        return null;
    }

    public PlayerCharacter GetPlayer(ulong _networkObjectId)
    {
        CharacterBase character = GetCharacter(_networkObjectId);

        if (character is PlayerCharacter player)
        {
            return player;
        }

        return null;
    }

    public MonsterBase GetMonster(ulong _networkObjectId)
    {
        CharacterBase character = GetCharacter(_networkObjectId);

        if (character is MonsterBase monster)
        {
            return monster;
        }

        return null;
    }

    public ulong[] GetAllCharacterIds()
    {
        ulong[] ids = new ulong[m_CharacterMap.Count];
        m_CharacterMap.Keys.CopyTo(ids, 0);
        return ids;
    }

    public List<PlayerCharacter> GetAlivePlayers()
    {
        var result = new List<PlayerCharacter>();
        foreach (var character in m_CharacterMap.Values)
        {
            if (character is PlayerCharacter player && !player.IsDead)
                result.Add(player);
        }
        return result;
    }

    public void Clear()
    {
        m_CharacterMap.Clear();
    }
}
