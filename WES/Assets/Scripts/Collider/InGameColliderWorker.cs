using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 내 충돌 판정을 관리하는 Worker
/// - BaseColliderObject 풀링 관리
/// - 충돌 객체 생성/반환
/// - 지속 판정 콜라이더 업데이트
/// - Gizmo 디버그 시각화
/// </summary>
public class InGameColliderWorker : MonoBehaviour
{
    private const int INITIAL_POOL_SIZE = 10;
    private const float GIZMO_DISPLAY_DURATION = 0.5f;

    private Queue<BaseColliderObject> m_Pool;
    private List<BaseColliderObject> m_ActiveColliders;
    private List<GizmoInfo> m_GizmoInfos;

    private void Awake()
    {
        InitializePool();
    }

    private void Update()
    {
        UpdateActiveColliders();
        UpdateGizmos();
    }

    /// <summary>
    /// 고정 위치 충돌 객체 생성
    /// Duration이 0이면 즉시 판정 후 반환
    /// Duration이 0보다 크면 지속 판정
    /// </summary>
    public void CreateCollider(CharacterBase _owner, Vector3 _position, float _radius, int _damage, int _maxHitCount, LayerMask _targetLayer, float _duration = 0f)
    {
        BaseColliderObject collider = GetFromPool();
        collider.Initialize(_owner, _position, _radius, _damage, _maxHitCount, _targetLayer, _duration);
        collider.PerformDetection();

        if (collider.IsInstant)
        {
            AddGizmoInfo(collider.Position, collider.Radius, collider.HasHit);
            ReturnCollider(collider);
        }
        else
        {
            m_ActiveColliders.Add(collider);
        }
    }

    /// <summary>
    /// Transform 따라가기 충돌 객체 생성
    /// Transform의 위치를 따라가며 지속 판정
    /// </summary>
    public void CreateCollider(CharacterBase _owner, Transform _followTarget, Vector3 _offset, float _radius, int _damage, int _maxHitCount, LayerMask _targetLayer, float _duration)
    {
        BaseColliderObject collider = GetFromPool();
        collider.Initialize(_owner, _followTarget, _offset, _radius, _damage, _maxHitCount, _targetLayer, _duration);
        collider.PerformDetection();

        m_ActiveColliders.Add(collider);
    }

    /// <summary>
    /// 충돌 객체 반환
    /// </summary>
    public void ReturnCollider(BaseColliderObject _collider)
    {
        if (_collider == null)
            return;

        _collider.Reset();
        m_Pool.Enqueue(_collider);
    }

    private void InitializePool()
    {
        m_Pool = new Queue<BaseColliderObject>();
        m_ActiveColliders = new List<BaseColliderObject>();
        m_GizmoInfos = new List<GizmoInfo>();

        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            BaseColliderObject collider = new BaseColliderObject();
            m_Pool.Enqueue(collider);
        }
    }

    private BaseColliderObject GetFromPool()
    {
        if (m_Pool.Count > 0)
        {
            return m_Pool.Dequeue();
        }

        return new BaseColliderObject();
    }

    private void UpdateActiveColliders()
    {
        for (int i = m_ActiveColliders.Count - 1; i >= 0; i--)
        {
            BaseColliderObject collider = m_ActiveColliders[i];
            collider.Update(Time.deltaTime);

            AddGizmoInfo(collider.Position, collider.Radius, collider.HasHit);

            if (collider.IsExpired)
            {
                m_ActiveColliders.RemoveAt(i);
                ReturnCollider(collider);
            }
        }
    }

    private void AddGizmoInfo(Vector3 _position, float _radius, bool _hasHit)
    {
        m_GizmoInfos.Add(new GizmoInfo
        {
            Position = _position,
            Radius = _radius,
            HasHit = _hasHit,
            RemainingTime = GIZMO_DISPLAY_DURATION
        });
    }

    private void UpdateGizmos()
    {
        for (int i = m_GizmoInfos.Count - 1; i >= 0; i--)
        {
            GizmoInfo info = m_GizmoInfos[i];
            info.RemainingTime -= Time.deltaTime;

            if (info.RemainingTime <= 0f)
            {
                m_GizmoInfos.RemoveAt(i);
            }
            else
            {
                m_GizmoInfos[i] = info;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (m_GizmoInfos == null)
            return;

        foreach (var info in m_GizmoInfos)
        {
            Gizmos.color = info.HasHit ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(info.Position, info.Radius);
        }
    }

    private struct GizmoInfo
    {
        public Vector3 Position;
        public float Radius;
        public bool HasHit;
        public float RemainingTime;
    }
}
