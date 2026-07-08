using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class BouncePlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    [Header("Bounce")]
    [FormerlySerializedAs("bounceVelocity")]
    [SerializeField] private float bounceVerticalVelocity = 14f;
    [SerializeField] private float bounceHorizontalVelocity = 6f;
    [SerializeField] private float releaseDelay = 0.08f;
    [SerializeField] private float cooldown = 0.15f;

    [Header("Visual Compression")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float compressDepth = 0.18f;
    [SerializeField] private float compressTime = 0.06f;
    [SerializeField] private float recoverTime = 0.12f;

    private Vector3 visualRestLocalPosition;
    private Coroutine bounceRoutine;
    private float lastBounceTime = -999f;

    private void Awake()
    {
        ResolveVisualRoot();
        visualRestLocalPosition = visualRoot.localPosition;
    }

    private void OnEnable()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRestLocalPosition;
        }
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        if (Time.time < lastBounceTime + cooldown || bounceRoutine != null)
        {
            return;
        }

        Rigidbody2D instigatorBody = instigator != null ? instigator.GetComponent<Rigidbody2D>() : null;
        PlayerCharacter player = instigator != null ? instigator.GetComponent<PlayerCharacter>() : null;
        bounceRoutine = StartCoroutine(BounceAfterCompression(instigatorBody, player));
    }

    private IEnumerator BounceAfterCompression(Rigidbody2D instigatorBody, PlayerCharacter player)
    {
        lastBounceTime = Time.time;

        Vector3 compressedPosition = visualRestLocalPosition + Vector3.down * compressDepth;
        yield return MoveVisual(visualRestLocalPosition, compressedPosition, compressTime);

        if (releaseDelay > 0f)
        {
            yield return new WaitForSeconds(releaseDelay);
        }

        if (instigatorBody != null)
        {
            float horizontalInput = player != null ? player.CurrentMoveInput : 0f;
            Vector2 velocity = Vector2.up * bounceVerticalVelocity;
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                velocity.x = Mathf.Sign(horizontalInput) * bounceHorizontalVelocity;
            }

            instigatorBody.linearVelocity = velocity;
        }

        yield return MoveVisual(compressedPosition, visualRestLocalPosition, recoverTime);

        bounceRoutine = null;
    }

    private IEnumerator MoveVisual(Vector3 from, Vector3 to, float duration)
    {
        if (visualRoot == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            visualRoot.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            visualRoot.localPosition = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        visualRoot.localPosition = to;
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform visual = transform.Find("Visual");
        visualRoot = visual != null ? visual : transform;
    }

    private void OnValidate()
    {
        bounceVerticalVelocity = Mathf.Max(0f, bounceVerticalVelocity);
        bounceHorizontalVelocity = Mathf.Max(0f, bounceHorizontalVelocity);
        releaseDelay = Mathf.Max(0f, releaseDelay);
        cooldown = Mathf.Max(0f, cooldown);
        compressDepth = Mathf.Max(0f, compressDepth);
        compressTime = Mathf.Max(0f, compressTime);
        recoverTime = Mathf.Max(0f, recoverTime);
        ResolveVisualRoot();
    }
}
