using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 게임 내 충돌 판정을 관리하는 Worker
/// - BaseColliderObject 풀링 관리
/// - 충돌 객체 생성/반환
/// - 지속 판정 콜라이더 업데이트
/// - LineRenderer 디버그 시각화
/// </summary>
public class InGameColliderWorker : MonoBehaviour
{
    private const int INITIAL_POOL_SIZE = 10;
    private const int DEBUG_CIRCLE_POOL_SIZE = 10;
    private const int DEBUG_CIRCLE_SEGMENTS = 32;
    private const float DEBUG_CIRCLE_WIDTH = 0.05f;
    private const float DEBUG_CIRCLE_HEIGHT = 0.05f;
    private const float DEBUG_DISPLAY_DURATION = 0.5f;

    private Queue<BaseColliderObject> m_Pool;
    private List<BaseColliderObject> m_ActiveColliders;

    private Queue<DebugCircle> m_DebugCirclePool;
    private List<DebugCircle> m_ActiveDebugCircles;
    private Material m_DebugMaterial;

    private void Awake()
    {
        InitializePool();
        InitializeDebugCirclePool();
    }

    private void Update()
    {
        UpdateActiveColliders();
        UpdateDebugCircles();
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
            ShowDebugCircle(collider.Position, collider.Radius, collider.HasHit);
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

        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            BaseColliderObject collider = new BaseColliderObject();
            m_Pool.Enqueue(collider);
        }
    }

    [Conditional("DEBUG_COLLIDER")]
    private void InitializeDebugCirclePool()
    {
        m_DebugCirclePool = new Queue<DebugCircle>();
        m_ActiveDebugCircles = new List<DebugCircle>();
        m_DebugMaterial = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < DEBUG_CIRCLE_POOL_SIZE; i++)
        {
            DebugCircle circle = CreateDebugCircleObject();
            m_DebugCirclePool.Enqueue(circle);
        }
    }

    private DebugCircle CreateDebugCircleObject()
    {
        GameObject obj = new GameObject("DebugCircle");
        obj.transform.SetParent(transform);
        obj.SetActive(false);

        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = DEBUG_CIRCLE_WIDTH;
        line.endWidth = DEBUG_CIRCLE_WIDTH;
        line.material = m_DebugMaterial;
        line.positionCount = DEBUG_CIRCLE_SEGMENTS + 1;

        return new DebugCircle(obj, line);
    }

    private BaseColliderObject GetFromPool()
    {
        if (m_Pool.Count > 0)
        {
            return m_Pool.Dequeue();
        }

        return new BaseColliderObject();
    }

    private DebugCircle GetDebugCircleFromPool()
    {
        if (m_DebugCirclePool.Count > 0)
        {
            return m_DebugCirclePool.Dequeue();
        }

        return CreateDebugCircleObject();
    }

    private void ReturnDebugCircle(DebugCircle _circle)
    {
        _circle.Hide();
        m_DebugCirclePool.Enqueue(_circle);
    }

    private void UpdateActiveColliders()
    {
        for (int i = m_ActiveColliders.Count - 1; i >= 0; i--)
        {
            BaseColliderObject collider = m_ActiveColliders[i];
            collider.Update(Time.deltaTime);

            ShowDebugCircle(collider.Position, collider.Radius, collider.HasHit);

            if (collider.IsExpired)
            {
                m_ActiveColliders.RemoveAt(i);
                ReturnCollider(collider);
            }
        }
    }

    [Conditional("DEBUG_COLLIDER")]
    private void ShowDebugCircle(Vector3 _position, float _radius, bool _hasHit)
    {
        DebugCircle circle = GetDebugCircleFromPool();
        Color color = _hasHit ? Color.red : Color.yellow;
        circle.Show(_position, _radius, color, DEBUG_DISPLAY_DURATION);
        m_ActiveDebugCircles.Add(circle);
    }

    [Conditional("DEBUG_COLLIDER")]
    private void UpdateDebugCircles()
    {
        for (int i = m_ActiveDebugCircles.Count - 1; i >= 0; i--)
        {
            DebugCircle circle = m_ActiveDebugCircles[i];
            circle.Update(Time.deltaTime);

            if (circle.IsExpired)
            {
                m_ActiveDebugCircles.RemoveAt(i);
                ReturnDebugCircle(circle);
            }
        }
    }

    private class DebugCircle
    {
        private GameObject m_Object;
        private LineRenderer m_LineRenderer;
        private float m_RemainingTime;

        public bool IsExpired => m_RemainingTime <= 0f;

        public DebugCircle(GameObject _object, LineRenderer _lineRenderer)
        {
            m_Object = _object;
            m_LineRenderer = _lineRenderer;
        }

        public void Show(Vector3 _position, float _radius, Color _color, float _duration)
        {
            m_Object.transform.position = new Vector3(_position.x, DEBUG_CIRCLE_HEIGHT, _position.z);
            m_Object.SetActive(true);

            m_LineRenderer.startColor = _color;
            m_LineRenderer.endColor = _color;

            for (int i = 0; i <= DEBUG_CIRCLE_SEGMENTS; i++)
            {
                float angle = i * 2f * Mathf.PI / DEBUG_CIRCLE_SEGMENTS;
                float x = Mathf.Cos(angle) * _radius;
                float z = Mathf.Sin(angle) * _radius;
                m_LineRenderer.SetPosition(i, new Vector3(x, 0f, z));
            }

            m_RemainingTime = _duration;
        }

        public void Hide()
        {
            m_Object.SetActive(false);
            m_RemainingTime = 0f;
        }

        public void Update(float _deltaTime)
        {
            m_RemainingTime -= _deltaTime;
        }
    }
}
