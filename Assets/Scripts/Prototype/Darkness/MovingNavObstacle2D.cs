using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovingNavObstacle2D : MonoBehaviour
{
    public enum ObstacleMode
    {
        Continuous,
        StateBased
    }

    [Header("Obstacle")]
    [SerializeField] private ObstacleMode mode = ObstacleMode.Continuous;
    [SerializeField] private Collider2D[] obstacleColliders;
    [SerializeField] private Rigidbody2D body;

    [Header("Settling")]
    [SerializeField] private float settleVelocityThreshold = 0.03f;
    [SerializeField] private float settledDuration = 0.4f;
    [SerializeField] private bool updateNavMeshWhenSettled;
    [SerializeField] private string movingObstacleLayer = "BatMovingObstacle";
    [SerializeField] private string settledStaticLayer = "BatStaticObstacle";

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private static readonly List<MovingNavObstacle2D> Active = new List<MovingNavObstacle2D>(16);

    private Vector2 previousPosition;
    private Vector2 velocity;
    private float stationaryElapsed;
    private bool settled;

    public static IReadOnlyList<MovingNavObstacle2D> ActiveObstacles => Active;
    public static event Action<MovingNavObstacle2D> GeometryChangedForOptionalRebuild;
    public static event Action<MovingNavObstacle2D> SettledForOptionalRebuild;

    public ObstacleMode Mode => mode;
    public Vector2 Velocity => velocity;
    public float Speed => velocity.magnitude;
    public bool IsSettled => settled;
    public bool UpdateNavMeshWhenSettled => updateNavMeshWhenSettled;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        previousPosition = GetPosition();
    }

    private void OnEnable()
    {
        CacheReferences();
        previousPosition = GetPosition();
        velocity = Vector2.zero;
        settled = mode == ObstacleMode.StateBased &&
            updateNavMeshWhenSettled &&
            AreObstacleCollidersOnLayer(settledStaticLayer);
        stationaryElapsed = settled ? settledDuration : 0f;
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void FixedUpdate()
    {
        Vector2 position = GetPosition();
        velocity = (position - previousPosition) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        previousPosition = position;

        bool wasSettled = settled;
        if (velocity.sqrMagnitude <= settleVelocityThreshold * settleVelocityThreshold)
        {
            stationaryElapsed += Time.fixedDeltaTime;
            settled = stationaryElapsed >= settledDuration;
        }
        else
        {
            stationaryElapsed = 0f;
            settled = false;
        }

        if (mode != ObstacleMode.StateBased || !updateNavMeshWhenSettled)
        {
            return;
        }

        if (wasSettled && !settled)
        {
            SetObstacleColliderLayer(movingObstacleLayer);
            GeometryChangedForOptionalRebuild?.Invoke(this);
        }
        else if (!wasSettled && settled)
        {
            SetObstacleColliderLayer(settledStaticLayer);
            SettledForOptionalRebuild?.Invoke(this);
        }
    }

    public bool TryGetClosestPoint(Vector2 point, out Vector2 closestPoint, out Collider2D source)
    {
        closestPoint = point;
        source = null;
        float closestSqrDistance = float.PositiveInfinity;

        if (obstacleColliders == null)
        {
            return false;
        }

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D candidate = obstacleColliders[i];
            if (candidate == null || !candidate.enabled || candidate.isTrigger || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 candidatePoint = candidate.ClosestPoint(point);
            float sqrDistance = (candidatePoint - point).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closestPoint = candidatePoint;
            source = candidate;
        }

        return source != null;
    }

    public bool Owns(Collider2D candidate)
    {
        if (candidate == null || obstacleColliders == null)
        {
            return false;
        }

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            if (obstacleColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private bool AreObstacleCollidersOnLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0 || obstacleColliders == null || obstacleColliders.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D candidate = obstacleColliders[i];
            if (candidate != null && !candidate.isTrigger && candidate.gameObject.layer != layer)
            {
                return false;
            }
        }

        return true;
    }

    private void SetObstacleColliderLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0 || obstacleColliders == null)
        {
            return;
        }

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D candidate = obstacleColliders[i];
            if (candidate != null && !candidate.isTrigger)
            {
                candidate.gameObject.layer = layer;
            }
        }
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (obstacleColliders == null || obstacleColliders.Length == 0)
        {
            Collider2D[] all = GetComponentsInChildren<Collider2D>(true);
            var solids = new List<Collider2D>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !all[i].isTrigger)
                {
                    solids.Add(all[i]);
                }
            }

            obstacleColliders = solids.ToArray();
        }
    }

    private void OnValidate()
    {
        settleVelocityThreshold = Mathf.Max(0f, settleVelocityThreshold);
        settledDuration = Mathf.Max(0f, settledDuration);
        if (string.IsNullOrWhiteSpace(movingObstacleLayer))
        {
            movingObstacleLayer = "BatMovingObstacle";
        }
        if (string.IsNullOrWhiteSpace(settledStaticLayer))
        {
            settledStaticLayer = "BatStaticObstacle";
        }
        CacheReferences();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        CacheReferences();
        Gizmos.color = settled ? Color.green : new Color(1f, 0.55f, 0.1f, 0.9f);
        if (obstacleColliders != null)
        {
            for (int i = 0; i < obstacleColliders.Length; i++)
            {
                Collider2D candidate = obstacleColliders[i];
                if (candidate != null && !candidate.isTrigger)
                {
                    Gizmos.DrawWireCube(candidate.bounds.center, candidate.bounds.size);
                }
            }
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)velocity);
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.5f,
            $"{mode} | Speed {Speed:0.00} | Settled {settled} | Rebuild {updateNavMeshWhenSettled}");
    }
#endif
}
