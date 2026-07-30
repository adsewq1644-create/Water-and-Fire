using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShockwaveSourceType
{
    Player,
    CoopPlayer,
    CreatureSnore
}

public enum ShockwaveShape
{
    FullCircle,
    UpperSemicircle
}

[Serializable]
public struct ShockwaveRequest
{
    public Vector2 Origin;
    public GameObject SourceObject;
    public PlayerCharacter Instigator;
    public ShockwaveSourceType SourceType;
    public ShockwaveShape Shape;
    public float Radius;
    public float Duration;
    public LayerMask TargetMask;
    public bool DelayByDistance;
    public bool ShowVisual;
    public float VisualWidthMultiplier;
    public Vector2 ArcDirection;
    public float ArcAngle;
    public float ArcUndulationStrength;
    public float ArcUndulationFrequency;
    public float ArcUndulationSpeed;
}

[DisallowMultipleComponent]
public sealed class ShockwaveEmitter2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private ShockwaveVisual2D visualPrefab;
    [SerializeField] private bool emitVisual = true;
    [SerializeField] private bool requireDarkZoneForVisual = true;

    private readonly HashSet<MonoBehaviour> invokedReceivers = new HashSet<MonoBehaviour>();

    public void Emit(ShockwaveRequest request)
    {
        request = Sanitize(request);
        if (request.Radius <= 0f)
        {
            return;
        }

        PlayVisualOnly(request);
        Dispatch(request);
    }

    public void PlayVisualOnly(ShockwaveRequest request)
    {
        request = Sanitize(request);
        if (!emitVisual || !request.ShowVisual || request.Radius <= 0f || request.Duration <= 0f)
        {
            return;
        }

        if (requireDarkZoneForVisual && !DarkZone.ContainsWorldPoint(request.Origin))
        {
            return;
        }

        ShockwaveVisual2D visual;
        if (visualPrefab != null)
        {
            visual = Instantiate(visualPrefab, request.Origin, Quaternion.identity);
        }
        else
        {
            var visualObject = new GameObject("ShockwaveVisual2D");
            visualObject.transform.position = request.Origin;
            visual = visualObject.AddComponent<ShockwaveVisual2D>();
        }

        visual.Play(request);
    }

    private void Dispatch(ShockwaveRequest request)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            request.Origin,
            request.Radius,
            request.TargetMask);

        invokedReceivers.Clear();
        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            Collider2D hit = hits[hitIndex];
            if (!IsEligibleHit(hit, request) || !IsInsideShape(hit, request))
            {
                continue;
            }

            Vector2 closestPoint = hit.ClosestPoint(request.Origin);
            float distance = Vector2.Distance(request.Origin, closestPoint);
            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null || !invokedReceivers.Add(behaviour))
                {
                    continue;
                }

                float delay = request.DelayByDistance
                    ? Mathf.Clamp01(distance / request.Radius) * request.Duration
                    : 0f;

                if (behaviour is IShockwaveContextReceiver contextReceiver)
                {
                    var context = new ShockwaveContext(
                        request.Origin,
                        request.Instigator,
                        request.SourceObject,
                        request.SourceType,
                        request.Shape,
                        request.Radius,
                        distance);

                    if (delay > 0f)
                    {
                        StartCoroutine(DispatchAfterDelay(behaviour, contextReceiver, context, delay));
                    }
                    else
                    {
                        contextReceiver.OnShockwaveReceived(context);
                    }
                }
                else if (behaviour is IShockwaveReceiver receiver)
                {
                    if (delay > 0f)
                    {
                        StartCoroutine(DispatchAfterDelay(
                            behaviour,
                            receiver,
                            request.Origin,
                            distance,
                            request.SourceObject,
                            delay));
                    }
                    else
                    {
                        receiver.OnShockwave(request.Origin, distance, request.SourceObject);
                    }
                }
            }
        }
    }

    private static ShockwaveRequest Sanitize(ShockwaveRequest request)
    {
        request.Radius = Mathf.Max(0f, request.Radius);
        request.Duration = Mathf.Max(0f, request.Duration);
        request.VisualWidthMultiplier = Mathf.Max(0.1f, request.VisualWidthMultiplier);
        request.ArcUndulationStrength = Mathf.Clamp01(
            request.ArcUndulationStrength);
        request.ArcUndulationFrequency = Mathf.Clamp(
            request.ArcUndulationFrequency,
            0f,
            12f);
        request.ArcUndulationSpeed = Mathf.Clamp(
            request.ArcUndulationSpeed,
            0f,
            4f);
        request.ArcAngle = request.Shape == ShockwaveShape.FullCircle
            ? 360f
            : Mathf.Clamp(request.ArcAngle <= 0f ? 180f : request.ArcAngle, 1f, 360f);

        if (request.SourceType != ShockwaveSourceType.CreatureSnore ||
            request.Shape == ShockwaveShape.FullCircle)
        {
            request.ArcUndulationStrength = 0f;
        }

        if (request.ArcDirection.sqrMagnitude < 0.0001f)
        {
            request.ArcDirection = Vector2.up;
        }
        else
        {
            request.ArcDirection.Normalize();
        }

        return request;
    }

    private static bool IsEligibleHit(Collider2D hit, ShockwaveRequest request)
    {
        if (hit == null || hit.GetComponentInParent<PlayerCharacter>() != null)
        {
            return false;
        }

        if (request.SourceObject == null)
        {
            return true;
        }

        Transform sourceTransform = request.SourceObject.transform;
        return hit.transform != sourceTransform &&
               !hit.transform.IsChildOf(sourceTransform);
    }

    private static bool IsInsideShape(Collider2D hit, ShockwaveRequest request)
    {
        if (request.Shape == ShockwaveShape.FullCircle)
        {
            return true;
        }

        Vector2 closestPoint = hit.ClosestPoint(request.Origin);
        Vector2 direction = closestPoint - request.Origin;

        // A collider crossing the origin line belongs to the upper half if any part
        // of it reaches above that line.
        if (request.Shape == ShockwaveShape.UpperSemicircle &&
            hit.bounds.max.y < request.Origin.y)
        {
            return false;
        }

        if (request.Shape == ShockwaveShape.UpperSemicircle && direction.y < 0f)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = (Vector2)hit.bounds.center - request.Origin;
            if (request.Shape == ShockwaveShape.UpperSemicircle)
            {
                direction.y = Mathf.Max(0f, direction.y);
            }
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float halfAngle = Mathf.Clamp(request.ArcAngle, 1f, 360f) * 0.5f;
        float minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        return Vector2.Dot(direction.normalized, request.ArcDirection) >= minimumDot;
    }

    private static IEnumerator DispatchAfterDelay(
        MonoBehaviour receiverBehaviour,
        IShockwaveContextReceiver receiver,
        ShockwaveContext context,
        float delay)
    {
        yield return new WaitForSeconds(delay);
        if (receiverBehaviour != null)
        {
            receiver.OnShockwaveReceived(context);
        }
    }

    private static IEnumerator DispatchAfterDelay(
        MonoBehaviour receiverBehaviour,
        IShockwaveReceiver receiver,
        Vector2 origin,
        float distance,
        GameObject sourceObject,
        float delay)
    {
        yield return new WaitForSeconds(delay);
        if (receiverBehaviour != null)
        {
            receiver.OnShockwave(origin, distance, sourceObject);
        }
    }
}
