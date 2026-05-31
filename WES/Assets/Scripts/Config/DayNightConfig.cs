using UnityEngine;

[CreateAssetMenu(fileName = "DayNightConfig", menuName = "WES/Config/DayNightConfig")]
public class DayNightConfig : ScriptableObject
{
    [Header("Phase Duration (seconds)")]
    [SerializeField] private float m_DayDuration = 360f;
    [SerializeField] private float m_DuskDuration = 120f;
    [SerializeField] private float m_NightDuration = 240f;
    [SerializeField] private float m_DawnDuration = 120f;

    [Header("Cold Rate Multiplier")]
    [SerializeField] private float m_ColdMultiplierDay = 1.0f;
    [SerializeField] private float m_ColdMultiplierDusk = 1.3f;
    [SerializeField] private float m_ColdMultiplierNight = 2.0f;
    [SerializeField] private float m_ColdMultiplierDawn = 1.3f;

    [Header("Cold Decay (per second, base)")]
    [SerializeField] private float m_BaseColdDecayPerSecond = 2f;

    [Header("Vision Radius")]
    [SerializeField] private float m_VisionRadiusDay = 1.0f;
    [SerializeField] private float m_VisionRadiusNight = 0.4f;
    [SerializeField] private float m_CampfireLightRadius = 5f;
    [SerializeField] private float m_TorchLightRadius = 3f;

    [Header("Ambient Light Color")]
    [SerializeField] private Color m_AmbientColorDay = new Color(0.6f, 0.65f, 0.7f);
    [SerializeField] private Color m_AmbientColorDusk = new Color(0.5f, 0.25f, 0.15f);
    [SerializeField] private Color m_AmbientColorNight = new Color(0.05f, 0.07f, 0.15f);
    [SerializeField] private Color m_AmbientColorDawn = new Color(0.3f, 0.3f, 0.35f);

    [Header("Directional Light Intensity")]
    [SerializeField] private float m_LightIntensityDay = 1.0f;
    [SerializeField] private float m_LightIntensityDusk = 0.5f;
    [SerializeField] private float m_LightIntensityNight = 0.05f;
    [SerializeField] private float m_LightIntensityDawn = 0.3f;

    [Header("Phase Transition Duration (seconds)")]
    [SerializeField] private float m_TransitionDuration = 5f;

    [Header("Night Vision Overlay")]
    [SerializeField] private float m_NightOverlayAlphaMax = 0.85f;

    [Header("Night Monster")]
    [SerializeField] private float m_NightMonsterCampfireAvoidRadius = 5f;

    public float DayDuration => m_DayDuration;
    public float DuskDuration => m_DuskDuration;
    public float NightDuration => m_NightDuration;
    public float DawnDuration => m_DawnDuration;
    public float BaseColdDecayPerSecond => m_BaseColdDecayPerSecond;
    public float VisionRadiusDay => m_VisionRadiusDay;
    public float VisionRadiusNight => m_VisionRadiusNight;
    public float CampfireLightRadius => m_CampfireLightRadius;
    public float TorchLightRadius => m_TorchLightRadius;
    public float TransitionDuration => m_TransitionDuration;
    public float NightOverlayAlphaMax => m_NightOverlayAlphaMax;
    public float NightMonsterCampfireAvoidRadius => m_NightMonsterCampfireAvoidRadius;

    public Color GetAmbientColor(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_AmbientColorDay,
            DayPhase.Dusk => m_AmbientColorDusk,
            DayPhase.Night => m_AmbientColorNight,
            DayPhase.Dawn => m_AmbientColorDawn,
            _ => m_AmbientColorDay,
        };
    }

    public float GetLightIntensity(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_LightIntensityDay,
            DayPhase.Dusk => m_LightIntensityDusk,
            DayPhase.Night => m_LightIntensityNight,
            DayPhase.Dawn => m_LightIntensityDawn,
            _ => m_LightIntensityDay,
        };
    }

    public float GetNightOverlayAlpha(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => 0f,
            DayPhase.Dusk => m_NightOverlayAlphaMax * 0.4f,
            DayPhase.Night => m_NightOverlayAlphaMax,
            DayPhase.Dawn => m_NightOverlayAlphaMax * 0.25f,
            _ => 0f,
        };
    }

    public float GetColdMultiplier(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_ColdMultiplierDay,
            DayPhase.Dusk => m_ColdMultiplierDusk,
            DayPhase.Night => m_ColdMultiplierNight,
            DayPhase.Dawn => m_ColdMultiplierDawn,
            _ => m_ColdMultiplierDay,
        };
    }

    public float GetPhaseDuration(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Day => m_DayDuration,
            DayPhase.Dusk => m_DuskDuration,
            DayPhase.Night => m_NightDuration,
            DayPhase.Dawn => m_DawnDuration,
            _ => m_DayDuration,
        };
    }
}
