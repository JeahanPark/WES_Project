using System.Linq;
using UnityEngine;

public class PlayerStatusHUD : MonoBehaviour
{
    [SerializeField] private StatBar[] m_StatBar;

    public void UpdateStat(CharacterStat _stat, int _current, int _max)
    {
        var StatBar = m_StatBar.FirstOrDefault(_ => _.GetCharacterStat == _stat);

        if (StatBar != null)
            StatBar.UpdateValue(_current, _max);
    }
}
