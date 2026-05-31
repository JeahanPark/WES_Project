using UnityEngine;
using UnityEngine.UI;

public class PhaseIconHUD : MonoBehaviour
{
    [Header("Phase Sprites")]
    [SerializeField] private Sprite m_DaySprite;
    [SerializeField] private Sprite m_DuskSprite;
    [SerializeField] private Sprite m_NightSprite;
    [SerializeField] private Sprite m_DawnSprite;

    [SerializeField] private Image m_PhaseIcon;

    private void OnEnable()
    {
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
    }

    public void RefreshPhase(DayPhase _phase)
    {
        if (m_PhaseIcon == null)
            return;

        Sprite sprite = GetSpriteForPhase(_phase);
        m_PhaseIcon.sprite = sprite;
        m_PhaseIcon.enabled = sprite != null;
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _current)
    {
        RefreshPhase(_current);
    }

    private Sprite GetSpriteForPhase(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_DaySprite,
            DayPhase.Dusk => m_DuskSprite,
            DayPhase.Night => m_NightSprite,
            DayPhase.Dawn => m_DawnSprite,
            _ => null,
        };
    }
}
