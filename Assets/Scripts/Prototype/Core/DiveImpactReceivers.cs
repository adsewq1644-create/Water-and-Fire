using UnityEngine;

public interface IDiveImpactReceiver
{
    void OnDiveImpact(Vector2 impactPoint, GameObject instigator);
}

public interface IShockwaveReceiver
{
    void OnShockwave(Vector2 origin, float distance, GameObject instigator);
}

public readonly struct ShockwaveContext
{
    public readonly Vector2 Origin;
    public readonly PlayerCharacter Instigator;
    public readonly float Radius;
    public readonly float Distance;

    public ShockwaveContext(Vector2 origin, PlayerCharacter instigator, float radius, float distance)
    {
        Origin = origin;
        Instigator = instigator;
        Radius = radius;
        Distance = distance;
    }
}

public interface IShockwaveContextReceiver
{
    void OnShockwaveReceived(ShockwaveContext context);
}
