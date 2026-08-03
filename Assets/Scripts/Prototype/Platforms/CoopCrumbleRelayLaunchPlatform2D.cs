using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CoopCrumbleRelayLaunchPlatform2D : MonoBehaviour
{
    public enum RelayPlatformState
    {
        Ready,
        OccupiedCrumbling,
        InflatingToLaunch,
        BounceActive,
        BreakingAfterSignal,
        BreakingOnTimeout,
        Broken
    }

    private const float TopSurfaceContactTolerance = 0.04f;

    [Header("Relay")]
    [SerializeField] private CoopCrumbleRelayLaunchPlatform2D launchTargetPlatform;
    [Tooltip("0 uses the launched character's own Max Jump Power. Set a positive value to override it for this platform.")]
    [SerializeField] private float launchVerticalPowerOverride;

    [Header("Crumble Timing")]
    [SerializeField] private float crumbleDuration = 1.6f;
    [SerializeField, Range(0f, 1f)] private float warningStartNormalized = 0.55f;
    [SerializeField, Range(0f, 1f)] private float finalWarningNormalized = 0.8f;
    [SerializeField] private float respawnDelay = 2f;

    [Header("Bounce Visual")]
    [SerializeField] private Transform visualPivot;
    [SerializeField] private Vector2 waitingPressScale = new Vector2(1.1f, 0.76f);
    [SerializeField] private Vector2 successCompressScale = new Vector2(1.28f, 0.48f);
    [SerializeField] private float successCompressTime = 0.035f;
    [SerializeField] private Vector2 successInflateScale = new Vector2(0.8f, 1.7f);
    [SerializeField] private float successInflateTime = 0.055f;
    [Tooltip("Small visual-only upward lift applied while the platform is inflated. Collision remains unchanged.")]
    [SerializeField] private float successInflateLift = 0.06f;
    [Tooltip("The brief window where this platform behaves like a JellyAutoJumpPlatform before breaking.")]
    [SerializeField, Range(0.08f, 0.2f)] private float bounceActiveDuration = 0.12f;

    [Header("Crumble Visual")]
    [SerializeField] private float maxCrumbleShakeX = 0.055f;
    [SerializeField] private float maxCrumbleShakeY = 0.018f;
    [SerializeField] private float crumbleShakeFrequency = 22f;
    [SerializeField] private GameObject[] crackStageVisuals;
    [SerializeField] private ParticleSystem warningVfx;
    [SerializeField] private ParticleSystem breakVfx;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warningSfx;
    [SerializeField] private AudioClip breakSfx;
    [SerializeField] private AudioClip launchSfx;

    [Header("Collision")]
    [SerializeField] private Collider2D solidCollider;
    [SerializeField, Range(0f, 1f)] private float topNormalThreshold = 0.55f;
    [SerializeField] private float maxAllowedRisingVelocity = 0.1f;

    private readonly HashSet<PlayerCharacter> playersOnTop = new HashSet<PlayerCharacter>();
    private RelayPlatformState state = RelayPlatformState.Ready;
    private Vector3 restLocalScale = Vector3.one;
    private Vector3 restLocalPosition;
    private Coroutine stateRoutine;
    private Coroutine respawnRoutine;
    private bool landingCommitted;
    private bool launchSignalSent;
    private bool warningStarted;
    private bool finalWarningStarted;

    public RelayPlatformState State => state;
    public CoopCrumbleRelayLaunchPlatform2D LaunchTargetPlatform => launchTargetPlatform;

    private void Awake()
    {
        ResolveReferences();
        restLocalScale = visualPivot != null ? visualPivot.localScale : Vector3.one;
        restLocalPosition = visualPivot != null ? visualPivot.localPosition : Vector3.zero;
        ResetPlatform();
    }

    private void OnDisable()
    {
        StopAllPlatformCoroutines();
        RestoreReadyVisuals();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerCharacter player = FindPlayer(collision.collider);
        if (player == null || !IsValidTopLanding(collision, player))
        {
            return;
        }

        playersOnTop.Add(player);
        TryCommitExpectedLanding(player);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        PlayerCharacter player = FindPlayer(collision.collider);
        if (player != null && IsValidTopLanding(collision, player))
        {
            playersOnTop.Add(player);
        }
    }

    private void FixedUpdate()
    {
        if (state == RelayPlatformState.BounceActive)
        {
            LaunchPlayersStandingOnPlatform();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        PlayerCharacter player = FindPlayer(collision.collider);
        if (player != null)
        {
            playersOnTop.Remove(player);
        }
    }

    public void ReceivePartnerLandingSignal(CoopCrumbleRelayLaunchPlatform2D sender)
    {
        if (state == RelayPlatformState.Broken || state == RelayPlatformState.BreakingAfterSignal ||
            state == RelayPlatformState.BreakingOnTimeout ||
            state == RelayPlatformState.InflatingToLaunch || state == RelayPlatformState.BounceActive)
        {
            return;
        }

        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
        }

        stateRoutine = StartCoroutine(BounceSequence());
    }

    public void ResetPlatform()
    {
        StopAllPlatformCoroutines();
        playersOnTop.Clear();
        landingCommitted = false;
        launchSignalSent = false;
        warningStarted = false;
        finalWarningStarted = false;
        state = RelayPlatformState.Ready;
        RestoreReadyVisuals();
    }

    private void TryCommitExpectedLanding(PlayerCharacter player)
    {
        if (state != RelayPlatformState.Ready || landingCommitted || player == null)
        {
            return;
        }

        landingCommitted = true;
        state = RelayPlatformState.OccupiedCrumbling;
        stateRoutine = StartCoroutine(CrumbleSequence());
        SendSignalToTargetOnce();
    }

    private void SendSignalToTargetOnce()
    {
        if (launchSignalSent || launchTargetPlatform == null)
        {
            return;
        }

        launchSignalSent = true;
        launchTargetPlatform.ReceivePartnerLandingSignal(this);
    }

    private IEnumerator CrumbleSequence()
    {
        if (visualPivot != null)
        {
            yield return AnimateVisual(GetScaled(waitingPressScale), restLocalPosition, 0.06f);
        }

        float elapsed = 0f;
        while (elapsed < crumbleDuration && state == RelayPlatformState.OccupiedCrumbling)
        {
            elapsed += Time.deltaTime;
            float normalized = crumbleDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / crumbleDuration);
            UpdateCrumbleWarning(normalized);
            yield return null;
        }

        if (state == RelayPlatformState.OccupiedCrumbling)
        {
            BeginBreak(RelayPlatformState.BreakingOnTimeout);
        }
    }

    private IEnumerator BounceSequence()
    {
        state = RelayPlatformState.InflatingToLaunch;
        yield return AnimateVisual(GetScaled(successCompressScale), restLocalPosition, successCompressTime);

        // The upward visual motion and the Jelly-style launch start in the same physics window.
        state = RelayPlatformState.BounceActive;
        LaunchPlayersStandingOnPlatform();
        float activeEndTime = Time.time + bounceActiveDuration;
        Vector3 inflatedPosition = restLocalPosition + Vector3.up * successInflateLift;
        yield return AnimateVisual(GetScaled(successInflateScale), inflatedPosition, successInflateTime);

        float remainingActiveTime = activeEndTime - Time.time;
        if (remainingActiveTime > 0f)
        {
            yield return new WaitForSeconds(remainingActiveTime);
        }

        BeginBreak(RelayPlatformState.BreakingAfterSignal);
    }

    private void BeginBreak(RelayPlatformState breakState)
    {
        if (state == RelayPlatformState.Broken)
        {
            return;
        }

        state = breakState;
        if (solidCollider != null)
        {
            solidCollider.enabled = false;
        }

        if (visualPivot != null)
        {
            visualPivot.gameObject.SetActive(false);
        }

        if (breakVfx != null)
        {
            breakVfx.Play();
        }

        PlayOneShot(breakSfx);
        state = RelayPlatformState.Broken;
        respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        respawnRoutine = null;
        ResetPlatform();
    }

    private void UpdateCrumbleWarning(float normalized)
    {
        bool shouldWarn = normalized >= warningStartNormalized;
        bool shouldFinalWarn = normalized >= finalWarningNormalized;

        if (shouldWarn && !warningStarted)
        {
            warningStarted = true;
            if (warningVfx != null)
            {
                warningVfx.Play();
            }

            PlayOneShot(warningSfx);
            SetCrackStage(1);
        }

        if (shouldFinalWarn && !finalWarningStarted)
        {
            finalWarningStarted = true;
            SetCrackStage(2);
        }

        if (visualPivot == null || !shouldWarn)
        {
            return;
        }

        float warningProgress = Mathf.InverseLerp(warningStartNormalized, 1f, normalized);
        float amplitude = Mathf.Lerp(0f, 1f, warningProgress);
        float phase = Time.time * crumbleShakeFrequency;
        visualPivot.localPosition = restLocalPosition + new Vector3(
            Mathf.Sin(phase) * maxCrumbleShakeX * amplitude,
            Mathf.Cos(phase * 1.37f) * maxCrumbleShakeY * amplitude,
            0f);
    }

    private void SetCrackStage(int stage)
    {
        if (crackStageVisuals == null)
        {
            return;
        }

        for (int i = 0; i < crackStageVisuals.Length; i++)
        {
            if (crackStageVisuals[i] != null)
            {
                crackStageVisuals[i].SetActive(i < stage);
            }
        }
    }

    private void LaunchPlayersStandingOnPlatform()
    {
        bool launchedAnyPlayer = false;
        foreach (PlayerCharacter player in playersOnTop)
        {
            if (IsPlayerStandingOnThisPlatform(player))
            {
                LaunchPlayer(player);
                launchedAnyPlayer = true;
            }
        }

        // Ground contact data can arrive one physics step later than the relay signal.
        PlayerCharacter[] allPlayers = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerCharacter player = allPlayers[i];
            if (IsPlayerStandingOnThisPlatform(player) && !playersOnTop.Contains(player))
            {
                LaunchPlayer(player);
                launchedAnyPlayer = true;
            }
        }

        if (launchedAnyPlayer)
        {
            PlayOneShot(launchSfx);
        }
    }

    private void LaunchPlayer(PlayerCharacter player)
    {
        float launchPower = launchVerticalPowerOverride > 0f ? launchVerticalPowerOverride : player.MaxJumpPower;
        player.ApplyMaxChargeAutoJump(player.CurrentMoveInput, launchPower);
    }

    private bool IsPlayerStandingOnThisPlatform(PlayerCharacter player)
    {
        if (player == null || !player.IsAliveLike || player.Velocity.y > maxAllowedRisingVelocity ||
            solidCollider == null || !solidCollider.enabled)
        {
            return false;
        }

        if (player.IsStandingOnCollider(solidCollider))
        {
            return true;
        }

        // The player's ground cast can update one frame after a physics contact.
        // Keep the relay responsive while still requiring an actual top-surface overlap.
        Bounds playerBounds = player.BodyCollider.bounds;
        Bounds platformBounds = solidCollider.bounds;
        bool horizontallyOverlapping =
            playerBounds.max.x > platformBounds.min.x + 0.03f &&
            playerBounds.min.x < platformBounds.max.x - 0.03f;
        float verticalGap = playerBounds.min.y - platformBounds.max.y;
        bool abovePlatform = playerBounds.center.y > platformBounds.center.y;
        return horizontallyOverlapping && abovePlatform && verticalGap >= -0.08f && verticalGap <= 0.1f &&
               player.Velocity.y <= maxAllowedRisingVelocity;
    }

    private bool IsValidTopLanding(Collision2D collision, PlayerCharacter player)
    {
        if (player == null || !player.IsAliveLike || player.Velocity.y > maxAllowedRisingVelocity ||
            player.BodyCollider == null || solidCollider == null || !solidCollider.enabled)
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
            if (fromContactToPlayer.sqrMagnitude > 0.0001f &&
                Vector2.Dot(fromContactToPlayer.normalized, platformUp) >= topNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOnColliderTopFace(Vector2 contactPoint)
    {
        if (solidCollider is BoxCollider2D boxCollider)
        {
            Vector2 localPoint = boxCollider.transform.InverseTransformPoint(contactPoint);
            float topY = boxCollider.offset.y + boxCollider.size.y * 0.5f;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(boxCollider.transform.lossyScale.y));
            return localPoint.y >= topY - TopSurfaceContactTolerance / scaleY;
        }

        Bounds bounds = solidCollider.bounds;
        Vector2 up = transform.up;
        float centerProjection = Vector2.Dot(bounds.center, up);
        float halfExtent = Mathf.Abs(up.x) * bounds.extents.x + Mathf.Abs(up.y) * bounds.extents.y;
        return Vector2.Dot(contactPoint, up) >= centerProjection + halfExtent - TopSurfaceContactTolerance;
    }

    private IEnumerator AnimateVisual(Vector3 targetScale, Vector3 targetPosition, float duration)
    {
        if (visualPivot == null)
        {
            yield break;
        }

        Vector3 startScale = visualPivot.localScale;
        Vector3 startPosition = visualPivot.localPosition;
        if (duration <= 0f)
        {
            visualPivot.localScale = targetScale;
            visualPivot.localPosition = targetPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualPivot.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            visualPivot.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            yield return null;
        }

        visualPivot.localScale = targetScale;
        visualPivot.localPosition = targetPosition;
    }

    private Vector3 GetScaled(Vector2 scaleMultiplier)
    {
        return new Vector3(restLocalScale.x * scaleMultiplier.x, restLocalScale.y * scaleMultiplier.y, restLocalScale.z);
    }

    private void RestoreReadyVisuals()
    {
        if (solidCollider != null)
        {
            solidCollider.enabled = true;
            solidCollider.isTrigger = false;
        }

        if (visualPivot != null)
        {
            visualPivot.gameObject.SetActive(true);
            visualPivot.localScale = restLocalScale;
            visualPivot.localPosition = restLocalPosition;
        }

        SetCrackStage(0);
    }

    private void StopAllPlatformCoroutines()
    {
        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
            stateRoutine = null;
        }

        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void ResolveReferences()
    {
        if (solidCollider == null)
        {
            solidCollider = GetComponentInChildren<Collider2D>();
        }

        if (visualPivot == null)
        {
            visualPivot = transform.Find("VisualPivot");
        }
    }

    private static PlayerCharacter FindPlayer(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<PlayerCharacter>() : null;
    }

    private void OnValidate()
    {
        crumbleDuration = Mathf.Max(0f, crumbleDuration);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        warningStartNormalized = Mathf.Clamp01(warningStartNormalized);
        finalWarningNormalized = Mathf.Clamp(finalWarningNormalized, warningStartNormalized, 1f);
        launchVerticalPowerOverride = Mathf.Max(0f, launchVerticalPowerOverride);
        successCompressTime = Mathf.Max(0f, successCompressTime);
        successInflateTime = Mathf.Max(0f, successInflateTime);
        successInflateLift = Mathf.Max(0f, successInflateLift);
        bounceActiveDuration = Mathf.Clamp(bounceActiveDuration, 0.08f, 0.2f);
        ResolveReferences();
    }

    private void OnDrawGizmosSelected()
    {
        if (launchTargetPlatform == null)
        {
            return;
        }

        Gizmos.color = new Color(0.4f, 1f, 0.76f, 0.9f);
        Vector3 start = transform.position;
        Vector3 end = launchTargetPlatform.transform.position;
        Gizmos.DrawLine(start, end);
        Vector3 direction = (end - start).normalized;
        Gizmos.DrawLine(end, end - Quaternion.Euler(0f, 0f, 25f) * direction * 0.28f);
        Gizmos.DrawLine(end, end - Quaternion.Euler(0f, 0f, -25f) * direction * 0.28f);
    }
}
