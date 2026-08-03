using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class JellyAutoJumpPlatform2D : MonoBehaviour
{
    private const float TopSurfaceContactTolerance = 0.04f;

    [Header("Launch")]
    [Tooltip("Full-charge vertical power before PlayerCharacter jump-feel multipliers. Player1_Water currently uses 16.7.")]
    [SerializeField] private float maxChargeVerticalPower = 16.7f;

    [Header("Visual")]
    [SerializeField] private Transform visualPivot;
    [SerializeField] private Vector2 compressScale = new Vector2(1.14f, 0.72f);
    [SerializeField] private float compressTime = 0.045f;
    [SerializeField] private float compressHoldTime = 0.025f;
    [SerializeField] private Vector2 stretchScale = new Vector2(0.94f, 1.1f);
    [SerializeField] private float stretchTime = 0.05f;
    [SerializeField] private float settleTime = 0.09f;

    [Header("Contact")]
    [SerializeField] private Collider2D platformCollider;
    [SerializeField, Range(0f, 1f)] private float topNormalThreshold = 0.55f;
    [SerializeField] private float maxAllowedRisingVelocity = 0.1f;
    [SerializeField] private float cooldown = 0.15f;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip launchSfx;
    [SerializeField] private ParticleSystem launchVfx;

    private readonly HashSet<PlayerCharacter> pendingPlayers = new HashSet<PlayerCharacter>();
    private readonly HashSet<PlayerCharacter> blockedUntilExit = new HashSet<PlayerCharacter>();
    private Vector3 restScale = Vector3.one;
    private Coroutine launchRoutine;
    private float rearmTime;

    private void Awake()
    {
        ResolveReferences();
        restScale = visualPivot != null ? visualPivot.localScale : Vector3.one;
    }

    private void OnEnable()
    {
        if (visualPivot != null)
        {
            visualPivot.localScale = restScale;
        }
    }

    private void OnDisable()
    {
        if (launchRoutine != null)
        {
            StopCoroutine(launchRoutine);
            launchRoutine = null;
        }

        pendingPlayers.Clear();
        blockedUntilExit.Clear();
        if (visualPivot != null)
        {
            visualPivot.localScale = restScale;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryRegisterLanding(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        PlayerCharacter player = FindPlayer(collision.collider);
        if (player == null)
        {
            return;
        }

        pendingPlayers.Remove(player);
        blockedUntilExit.Remove(player);
    }

    private void TryRegisterLanding(Collision2D collision)
    {
        if (Time.time < rearmTime || collision == null)
        {
            return;
        }

        PlayerCharacter player = FindPlayer(collision.collider);
        if (player == null || blockedUntilExit.Contains(player) || !IsValidTopLanding(collision, player))
        {
            return;
        }

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody == null)
        {
            return;
        }

        playerBody.linearVelocity = new Vector2(playerBody.linearVelocity.x, 0f);
        pendingPlayers.Add(player);
        if (launchRoutine == null)
        {
            launchRoutine = StartCoroutine(LaunchSequence());
        }
    }

    private bool IsValidTopLanding(Collision2D collision, PlayerCharacter player)
    {
        if (!player.IsAliveLike || player.Velocity.y > maxAllowedRisingVelocity || player.BodyCollider == null)
        {
            return false;
        }

        Vector2 platformUp = transform.up;
        Vector2 playerCenter = player.BodyCollider.bounds.center;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (!IsOnColliderTopFace(contact.point))
            {
                continue;
            }

            Vector2 fromContactToPlayer = playerCenter - contact.point;
            if (fromContactToPlayer.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            if (Vector2.Dot(fromContactToPlayer.normalized, platformUp) >= topNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOnColliderTopFace(Vector2 contactPoint)
    {
        if (platformCollider is BoxCollider2D boxCollider)
        {
            Vector2 localPoint = boxCollider.transform.InverseTransformPoint(contactPoint);
            float topY = boxCollider.offset.y + boxCollider.size.y * 0.5f;
            float worldScaleY = Mathf.Max(0.0001f, Mathf.Abs(boxCollider.transform.lossyScale.y));
            return localPoint.y >= topY - TopSurfaceContactTolerance / worldScaleY;
        }

        Bounds bounds = platformCollider.bounds;
        Vector2 up = transform.up;
        float centerProjection = Vector2.Dot(bounds.center, up);
        float projectedHalfExtent =
            Mathf.Abs(up.x) * bounds.extents.x +
            Mathf.Abs(up.y) * bounds.extents.y;
        float contactProjection = Vector2.Dot(contactPoint, up);
        return contactProjection >= centerProjection + projectedHalfExtent - TopSurfaceContactTolerance;
    }

    private IEnumerator LaunchSequence()
    {
        yield return AnimateScale(GetScaled(compressScale), compressTime);
        if (compressHoldTime > 0f)
        {
            yield return new WaitForSeconds(compressHoldTime);
        }

        PlayerCharacter[] players = new PlayerCharacter[pendingPlayers.Count];
        pendingPlayers.CopyTo(players);
        pendingPlayers.Clear();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerCharacter player = players[i];
            if (player == null || !player.IsAliveLike)
            {
                continue;
            }

            blockedUntilExit.Add(player);
            player.ApplyMaxChargeAutoJump(player.CurrentMoveInput, maxChargeVerticalPower);
        }

        PlayLaunchFeedback();
        yield return AnimateScale(GetScaled(stretchScale), stretchTime);
        yield return AnimateScale(restScale, settleTime);

        rearmTime = Time.time + cooldown;
        launchRoutine = null;
    }

    private IEnumerator AnimateScale(Vector3 target, float duration)
    {
        if (visualPivot == null)
        {
            yield break;
        }

        Vector3 start = visualPivot.localScale;
        if (duration <= 0f)
        {
            visualPivot.localScale = target;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualPivot.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        visualPivot.localScale = target;
    }

    private Vector3 GetScaled(Vector2 multiplier)
    {
        return new Vector3(restScale.x * multiplier.x, restScale.y * multiplier.y, restScale.z);
    }

    private void PlayLaunchFeedback()
    {
        if (audioSource != null && launchSfx != null)
        {
            audioSource.PlayOneShot(launchSfx);
        }

        if (launchVfx != null)
        {
            launchVfx.Play();
        }
    }

    private static PlayerCharacter FindPlayer(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<PlayerCharacter>() : null;
    }

    private void ResolveReferences()
    {
        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }

        if (visualPivot == null)
        {
            visualPivot = transform.Find("VisualPivot");
        }
    }

    private void OnValidate()
    {
        maxChargeVerticalPower = Mathf.Max(0f, maxChargeVerticalPower);
        compressScale.x = Mathf.Max(0.01f, compressScale.x);
        compressScale.y = Mathf.Max(0.01f, compressScale.y);
        stretchScale.x = Mathf.Max(0.01f, stretchScale.x);
        stretchScale.y = Mathf.Max(0.01f, stretchScale.y);
        compressTime = Mathf.Max(0f, compressTime);
        compressHoldTime = Mathf.Max(0f, compressHoldTime);
        stretchTime = Mathf.Max(0f, stretchTime);
        settleTime = Mathf.Max(0f, settleTime);
        maxAllowedRisingVelocity = Mathf.Max(0f, maxAllowedRisingVelocity);
        cooldown = Mathf.Max(0f, cooldown);
        ResolveReferences();
    }
}
