using UnityEngine;

public interface IDiveImpactReceiver
{
    void OnDiveImpact(Vector2 impactPoint, GameObject instigator);
}

public interface IShockwaveReceiver
{
    void OnShockwave(Vector2 origin, float distance, GameObject instigator);
}
