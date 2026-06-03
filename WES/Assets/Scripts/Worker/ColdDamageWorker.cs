using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 추위(Cold) 실질화 워커. 서버 전용.
/// - 밤 + 모닥불 보호 밖에서 Cold 누적 (UpdateColdAccumulation)
/// - Cold 단계(ColdStage)에 따라 HP 틱 데미지 (UpdateColdDamageTick)
/// DayNightWorker와 동일 GameObject에 부착하며, 자연 감쇠(DayNightWorker.ApplyColdDecay)와 병렬로 동작한다.
/// 결정(2026-06-03): 켜진 모닥불 근처에서는 누적만 스킵(적극 감소 없음), 자연 감쇠가 Cold를 줄인다.
/// </summary>
public class ColdDamageWorker : NetworkBehaviour
{
    [SerializeField] private DayNightConfig m_Config;
    [SerializeField] private DayNightWorker m_DayNightWorker;

    private float m_AccumAccumulator;
    private float m_WeakDotTimer;
    private float m_StrongDotTimer;

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        if (m_Config == null)
            return;

        if (InGameController.Instance == null || InGameController.Instance.GameState != GameState.Playing)
            return;

        float delta = Time.deltaTime;
        UpdateColdAccumulation(delta);
        UpdateColdDamageTick(delta);
    }

    private void UpdateColdAccumulation(float _deltaTime)
    {
        if (!IsNight())
            return;

        m_AccumAccumulator += m_Config.ColdAccumPerSecondNight * _deltaTime;
        if (m_AccumAccumulator < 1f)
            return;

        int accumAmount = (int)m_AccumAccumulator;
        m_AccumAccumulator -= accumAmount;

        var registry = GetCharacterRegistry();
        if (registry == null)
            return;

        foreach (var player in registry.GetAlivePlayers())
        {
            // 켜진 모닥불 보호 범위 내면 누적 스킵 (자연 감쇠는 DayNightWorker가 계속 진행)
            if (IsNearActiveCampfire(player))
                continue;

            player.SetCold(player.Cold + accumAmount);
        }
    }

    private void UpdateColdDamageTick(float _deltaTime)
    {
        m_WeakDotTimer += _deltaTime;
        m_StrongDotTimer += _deltaTime;

        bool weakReady = m_WeakDotTimer >= m_Config.WeakDotInterval;
        bool strongReady = m_StrongDotTimer >= m_Config.StrongDotInterval;

        if (!weakReady && !strongReady)
            return;

        if (weakReady)
            m_WeakDotTimer -= m_Config.WeakDotInterval;
        if (strongReady)
            m_StrongDotTimer -= m_Config.StrongDotInterval;

        var registry = GetCharacterRegistry();
        if (registry == null)
            return;

        foreach (var player in registry.GetAlivePlayers())
        {
            ColdStage stage = GetColdStage(player.Cold);

            if (stage == ColdStage.StrongDot && strongReady)
            {
                // 추위로는 즉사하지 않음 — HP 1 보호
                player.TakeEnvironmentDamage(m_Config.StrongDotDamage, false);
            }
            else if (stage == ColdStage.WeakDot && weakReady)
            {
                player.TakeEnvironmentDamage(m_Config.WeakDotDamage, false);
            }
        }
    }

    private ColdStage GetColdStage(int _cold)
    {
        if (_cold >= m_Config.ColdStageStrong)
            return ColdStage.StrongDot;
        if (_cold >= m_Config.ColdStageWeak)
            return ColdStage.WeakDot;
        if (_cold >= m_Config.ColdStageWarning)
            return ColdStage.Warning;
        return ColdStage.None;
    }

    private bool IsNearActiveCampfire(PlayerCharacter _player)
    {
        if (_player == null)
            return false;

        float range = m_Config.CampfireProtectRange;
        float rangeSqr = range * range;
        Vector3 playerPos = _player.transform.position;

        foreach (var building in WorldBuildingObject.ActiveBuildings)
        {
            if (building == null)
                continue;
            if (!building.IsLit)
                continue;

            float distSqr = (building.transform.position - playerPos).sqrMagnitude;
            if (distSqr <= rangeSqr)
                return true;
        }

        return false;
    }

    private bool IsNight()
    {
        DayNightWorker worker = m_DayNightWorker != null ? m_DayNightWorker : InGameController.Instance?.DayNightWorker;
        if (worker == null)
            return false;
        return worker.CurrentPhase == DayPhase.Night;
    }

    private CharacterRegistry GetCharacterRegistry()
    {
        return InGameController.Instance?.ObjectDataWorker?.GetCharacterRegistry();
    }
}
