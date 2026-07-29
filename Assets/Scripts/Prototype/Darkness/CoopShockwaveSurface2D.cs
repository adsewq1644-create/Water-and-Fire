using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoopShockwaveSurface2D : MonoBehaviour, IDiveImpactReceiver
{
    [Header("Cooperative Shockwave")]
    [SerializeField, Min(0.05f)] private float coopImpactWindow = 1f;
    [SerializeField] private bool useImpactMidpoint = true;
    [SerializeField] private bool debugCoopShockwave;

    private ShockwaveHiddenPlatform2D revealFeedback;
    private PlayerCharacter firstPlayer;
    private Vector2 firstImpactPoint;
    private float firstImpactExpiresAt;

    private void Awake()
    {
        ResolveRevealFeedback();
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        PlayerCharacter player = instigator != null
            ? instigator.GetComponentInParent<PlayerCharacter>()
            : null;
        if (player == null || !player.IsAliveLike)
        {
            return;
        }

        float now = Time.time;
        if (firstPlayer == null || now > firstImpactExpiresAt)
        {
            RecordFirstImpact(player, impactPoint, now);
            return;
        }

        if (firstPlayer == player && !AllowsSoloTestResonance(player))
        {
            Log($"Repeated impact from {player.name} cancelled the primed state.");
            ResetPendingImpact();
            return;
        }

        PlayerCharacter shockwaveInstigator = firstPlayer;
        Vector2 origin = useImpactMidpoint
            ? (firstImpactPoint + impactPoint) * 0.5f
            : impactPoint;

        ResetPendingImpact();
        Log($"Resonance succeeded at {origin}.");

        if (shockwaveInstigator == null || !shockwaveInstigator.IsAliveLike)
        {
            Log("Resonance cancelled because the first player is no longer active.");
            return;
        }

        player.SuppressBaseDiveShockwaveThisImpact();
        shockwaveInstigator.EmitCoopShockwave(origin);
    }

    private void Update()
    {
        if (firstPlayer != null && Time.time > firstImpactExpiresAt)
        {
            Log("First impact expired.");
            ResetPendingImpact();
        }
    }

    private void OnDisable()
    {
        ResetPendingImpact();
    }

    private void RecordFirstImpact(PlayerCharacter player, Vector2 impactPoint, float now)
    {
        firstPlayer = player;
        firstImpactPoint = impactPoint;
        firstImpactExpiresAt = now + Mathf.Max(0.05f, coopImpactWindow);
        ResolveRevealFeedback();
        if (revealFeedback != null)
        {
            revealFeedback.ShowCoopPrimed(coopImpactWindow);
        }
        Log($"Waiting for a different player after {player.name}.");
    }

    private void ResetPendingImpact()
    {
        if (revealFeedback != null)
        {
            revealFeedback.ClearCoopPrimed();
        }
        firstPlayer = null;
        firstImpactPoint = default;
        firstImpactExpiresAt = 0f;
    }

    private static bool AllowsSoloTestResonance(PlayerCharacter player)
    {
        TestCharacterElementSwitcher testSwitcher =
            player != null ? player.GetComponent<TestCharacterElementSwitcher>() : null;
        return testSwitcher != null && testSwitcher.AllowsSoloCoopShockwaveTest(player);
    }

    private void ResolveRevealFeedback()
    {
        if (revealFeedback == null)
        {
            revealFeedback = GetComponent<ShockwaveHiddenPlatform2D>();
        }
    }

    private void Log(string message)
    {
        if (debugCoopShockwave)
        {
            Debug.Log($"[CoopShockwaveSurface2D] {name}: {message}", this);
        }
    }

    private void OnValidate()
    {
        coopImpactWindow = Mathf.Max(0.05f, coopImpactWindow);
        ResolveRevealFeedback();
    }
}
