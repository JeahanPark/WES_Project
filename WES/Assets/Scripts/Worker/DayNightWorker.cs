using System;
using Unity.Netcode;
using UnityEngine;

public class DayNightWorker : NetworkBehaviour
{
    [SerializeField] private DayNightConfig m_Config;

    public static event Action<DayPhase, DayPhase> OnPhaseChanged;

    private NetworkVariable<int> m_Phase = new(
        (int)DayPhase.Day,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float m_Elapsed;
    private float m_ColdDecayAccumulator;

#if UNITY_EDITOR
    private int m_EditorPhaseOverride = -1;
#endif

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_Phase.OnValueChanged += OnPhaseValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_Phase.OnValueChanged -= OnPhaseValueChanged;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        if (m_Config == null)
            return;

        if (InGameController.Instance == null || InGameController.Instance.GameState != GameState.Playing)
            return;

        float delta = Time.deltaTime;
        m_Elapsed += delta;

        float phaseDuration = m_Config.GetPhaseDuration(CurrentPhase);
        if (m_Elapsed >= phaseDuration)
        {
            m_Elapsed -= phaseDuration;
            AdvancePhase();
        }

        ApplyColdDecay(delta);
    }

    public DayPhase CurrentPhase
    {
        get
        {
#if UNITY_EDITOR
            if (m_EditorPhaseOverride >= 0)
                return (DayPhase)m_EditorPhaseOverride;
#endif
            return (DayPhase)m_Phase.Value;
        }
    }

    public float GetColdRateMultiplier()
    {
        if (m_Config == null)
            return 1f;
        return m_Config.GetColdMultiplier(CurrentPhase);
    }

#if UNITY_EDITOR
    public void ForcePhase(DayPhase _phase)
    {
        if (IsSpawned && !IsServer)
            return;

        m_Elapsed = 0f;

        if (IsSpawned)
        {
            m_EditorPhaseOverride = -1;
            m_Phase.Value = (int)_phase;
        }
        else
        {
            DayPhase prev = CurrentPhase;
            m_EditorPhaseOverride = (int)_phase;
            OnPhaseChanged?.Invoke(prev, _phase);
        }
    }
#endif

    private void AdvancePhase()
    {
        DayPhase next = CurrentPhase switch
        {
            DayPhase.Day => DayPhase.Dusk,
            DayPhase.Dusk => DayPhase.Night,
            DayPhase.Night => DayPhase.Dawn,
            DayPhase.Dawn => DayPhase.Day,
            _ => DayPhase.Day,
        };

        if (next == CurrentPhase)
            return;

        m_Phase.Value = (int)next;
    }

    private void ApplyColdDecay(float _deltaTime)
    {
        if (m_Config == null)
            return;

        float decayRate = m_Config.BaseColdDecayPerSecond * GetColdRateMultiplier();
        m_ColdDecayAccumulator += decayRate * _deltaTime;

        if (m_ColdDecayAccumulator < 1f)
            return;

        int decayAmount = (int)m_ColdDecayAccumulator;
        m_ColdDecayAccumulator -= decayAmount;

        var registry = InGameController.Instance?.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return;

        foreach (var player in registry.GetAlivePlayers())
        {
            player.SetCold(player.Cold - decayAmount);
        }
    }

    private void OnPhaseValueChanged(int _prev, int _current)
    {
        DayPhase prevPhase = (DayPhase)_prev;
        DayPhase currentPhase = (DayPhase)_current;
        GameDebug.Log($"[DayNightWorker] Phase changed: {prevPhase} -> {currentPhase}");
        OnPhaseChanged?.Invoke(prevPhase, currentPhase);
    }
}
