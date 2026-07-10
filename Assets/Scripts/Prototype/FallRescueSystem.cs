using UnityEngine;

public class FallRescueSystem : MonoBehaviour
{
    private enum RescueState
    {
        Idle,
        SkillCheck,
        FailedFalling
    }

    private struct SkillCheckRuntime
    {
        public PlayerCharacter player;
        public KeyCode key;
        public string label;
        public bool required;
        public bool succeeded;
        public bool failed;
        public float successCenter;
    }

    [Header("Players")]
    [SerializeField] private PlayerCharacter waterPlayer;
    [SerializeField] private PlayerCharacter firePlayer;

    [Header("Request")]
    [SerializeField] private KeyCode rescueRequestKey = KeyCode.F;
    [SerializeField] private float requiredDropBelowSafePlatform = 2.5f;
    [SerializeField] private float minFallingSpeed = 1.5f;

    [Header("Skill Check")]
    [SerializeField] private KeyCode waterSkillCheckKey = KeyCode.F;
    [SerializeField] private KeyCode fireSkillCheckKey = KeyCode.J;
    [SerializeField] private float skillCheckDuration = 2.2f;
    [SerializeField, Range(0.04f, 0.45f)] private float successWindowSize = 0.16f;
    [SerializeField] private float skillCheckStartDelay = 0.15f;

    [Header("Failure Join")]
    [SerializeField] private Vector2 partnerJoinOffset = new Vector2(1.25f, 0.35f);
    [SerializeField] private float minFailedFallViewTime = 0.35f;

    [Header("Camera")]
    [SerializeField] private PrototypeCameraFollow cameraFollow;
    [SerializeField] private Camera fallbackCamera;
    [SerializeField] private Vector3 fallbackCameraOffset = new Vector3(0f, 1.5f, -10f);
    [SerializeField] private float fallbackCameraSmoothTime = 0.15f;
    [SerializeField] private bool restoreCameraTargetAfterRescue = true;

    [Header("Debug")]
    [SerializeField] private bool showRequestHint = true;
    [SerializeField] private bool showDebugState;

    private RescueState state;
    private PlayerCharacter fallingPlayer;
    private PlayerCharacter partnerPlayer;
    private SkillCheckRuntime waterCheck;
    private SkillCheckRuntime fireCheck;
    private Transform savedCameraTarget;
    private bool hasSavedCameraTarget;
    private Vector3 fallbackCameraVelocity;
    private Vector3 rescueSafePosition;
    private float skillCheckStartedAt;
    private float failedAt;

    private bool IsSkillCheckActive => state == RescueState.SkillCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindFirstObjectByType<FallRescueSystem>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("FallRescueSystem");
        runtimeObject.AddComponent<FallRescueSystem>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        switch (state)
        {
            case RescueState.Idle:
                UpdateIdle();
                break;
            case RescueState.SkillCheck:
                UpdateSkillCheck();
                break;
            case RescueState.FailedFalling:
                UpdateFailedFalling();
                break;
        }
    }

    private void LateUpdate()
    {
        if ((state == RescueState.SkillCheck || state == RescueState.FailedFalling) && cameraFollow == null && fallbackCamera != null && fallingPlayer != null)
        {
            Vector3 desired = fallingPlayer.transform.position + fallbackCameraOffset;
            fallbackCamera.transform.position = Vector3.SmoothDamp(
                fallbackCamera.transform.position,
                desired,
                ref fallbackCameraVelocity,
                fallbackCameraSmoothTime);
        }
    }

    private void UpdateIdle()
    {
        if (!Input.GetKeyDown(rescueRequestKey))
        {
            return;
        }

        PlayerCharacter requester = GetEligibleRequester();
        if (requester == null)
        {
            return;
        }

        BeginRescueSkillCheck(requester);
    }

    private PlayerCharacter GetEligibleRequester()
    {
        bool waterEligible = CanRequestRescue(waterPlayer);
        bool fireEligible = CanRequestRescue(firePlayer);

        if (waterEligible && fireEligible)
        {
            return waterPlayer.transform.position.y <= firePlayer.transform.position.y ? waterPlayer : firePlayer;
        }

        if (waterEligible)
        {
            return waterPlayer;
        }

        return fireEligible ? firePlayer : null;
    }

    private bool CanRequestRescue(PlayerCharacter player)
    {
        if (player == null || !player.IsAliveLike || player.IsGrounded || !player.HasLastSafePosition)
        {
            return false;
        }

        if (player.Velocity.y > -minFallingSpeed)
        {
            return false;
        }

        return player.transform.position.y <= player.LastSafePosition.y - requiredDropBelowSafePlatform;
    }

    private void BeginRescueSkillCheck(PlayerCharacter requester)
    {
        fallingPlayer = requester;
        partnerPlayer = GetPartnerFor(fallingPlayer);
        rescueSafePosition = fallingPlayer.LastSafePosition;
        skillCheckStartedAt = Time.time;

        waterCheck = CreateSkillCheck(waterPlayer, waterSkillCheckKey, "Water", 0.68f);
        fireCheck = CreateSkillCheck(firePlayer, fireSkillCheckKey, "Fire", 0.32f);

        FocusCameraOnFallingPlayer();
        state = RescueState.SkillCheck;
    }

    private SkillCheckRuntime CreateSkillCheck(PlayerCharacter player, KeyCode key, string label, float fallbackCenter)
    {
        bool required = player != null && player.IsAliveLike;
        return new SkillCheckRuntime
        {
            player = player,
            key = key,
            label = label,
            required = required,
            successCenter = required ? Random.Range(0.18f, 0.82f) : fallbackCenter
        };
    }

    private void UpdateSkillCheck()
    {
        float elapsed = Time.time - skillCheckStartedAt;
        if (elapsed < skillCheckStartDelay)
        {
            return;
        }

        float activeElapsed = elapsed - skillCheckStartDelay;
        float normalizedNeedle = Mathf.Repeat(activeElapsed / Mathf.Max(0.01f, skillCheckDuration), 1f);

        UpdateSingleSkillCheck(ref waterCheck, normalizedNeedle);
        UpdateSingleSkillCheck(ref fireCheck, normalizedNeedle);

        if (waterCheck.failed || fireCheck.failed || activeElapsed > skillCheckDuration)
        {
            FailSkillCheck();
            return;
        }

        if (IsCheckComplete(waterCheck) && IsCheckComplete(fireCheck))
        {
            CompleteSkillCheckSuccess();
        }
    }

    private void UpdateSingleSkillCheck(ref SkillCheckRuntime check, float normalizedNeedle)
    {
        if (!check.required || check.succeeded || check.failed)
        {
            return;
        }

        if (!Input.GetKeyDown(check.key))
        {
            return;
        }

        if (IsNeedleInSuccessWindow(normalizedNeedle, check.successCenter))
        {
            check.succeeded = true;
        }
        else
        {
            check.failed = true;
        }
    }

    private bool IsCheckComplete(SkillCheckRuntime check)
    {
        return !check.required || check.succeeded;
    }

    private bool IsNeedleInSuccessWindow(float needle, float center)
    {
        float halfWindow = successWindowSize * 0.5f;
        float delta = Mathf.Abs(Mathf.DeltaAngle(needle * 360f, center * 360f)) / 360f;
        return delta <= halfWindow;
    }

    private void CompleteSkillCheckSuccess()
    {
        if (fallingPlayer != null)
        {
            fallingPlayer.TeleportForFallRescue(rescueSafePosition);
        }

        EndRescueSequence(true);
    }

    private void FailSkillCheck()
    {
        failedAt = Time.time;
        state = RescueState.FailedFalling;
        FocusCameraOnFallingPlayer();
    }

    private void UpdateFailedFalling()
    {
        if (fallingPlayer == null)
        {
            EndRescueSequence(false);
            return;
        }

        FocusCameraOnFallingPlayer();

        if (Time.time - failedAt < minFailedFallViewTime || !fallingPlayer.IsGrounded)
        {
            return;
        }

        if (partnerPlayer != null && partnerPlayer.IsAliveLike)
        {
            Vector2 sideOffset = fallingPlayer == waterPlayer ? partnerJoinOffset : new Vector2(-partnerJoinOffset.x, partnerJoinOffset.y);
            Vector3 joinPosition = fallingPlayer.transform.position + (Vector3)sideOffset;
            partnerPlayer.TeleportForFallRescue(joinPosition);
        }

        EndRescueSequence(false);
    }

    private void EndRescueSequence(bool rescued)
    {
        if (restoreCameraTargetAfterRescue)
        {
            RestoreCameraTarget();
        }

        state = RescueState.Idle;
        fallingPlayer = null;
        partnerPlayer = null;
        waterCheck = default;
        fireCheck = default;
        rescueSafePosition = default;
    }

    private PlayerCharacter GetPartnerFor(PlayerCharacter player)
    {
        if (player == null)
        {
            return null;
        }

        if (player == waterPlayer)
        {
            return firePlayer;
        }

        if (player == firePlayer)
        {
            return waterPlayer;
        }

        return null;
    }

    private void FocusCameraOnFallingPlayer()
    {
        if (fallingPlayer == null)
        {
            return;
        }

        ResolveCameraReferences();
        if (cameraFollow != null)
        {
            if (!hasSavedCameraTarget)
            {
                savedCameraTarget = cameraFollow.Target;
                hasSavedCameraTarget = true;
            }

            cameraFollow.SetTarget(fallingPlayer.transform);
        }
    }

    private void RestoreCameraTarget()
    {
        if (cameraFollow != null && hasSavedCameraTarget)
        {
            cameraFollow.SetTarget(savedCameraTarget != null ? savedCameraTarget : fallingPlayer != null ? fallingPlayer.transform : null);
        }

        hasSavedCameraTarget = false;
        savedCameraTarget = null;
    }

    private void ResolveReferences()
    {
        GamePrototypeManager manager = GamePrototypeManager.Instance;
        if (manager != null)
        {
            if (waterPlayer == null)
            {
                waterPlayer = manager.WaterPlayer;
            }

            if (firePlayer == null)
            {
                firePlayer = manager.FirePlayer;
            }
        }

        ResolveCameraReferences();
    }

    private void ResolveCameraReferences()
    {
        if (cameraFollow == null)
        {
            cameraFollow = FindFirstObjectByType<PrototypeCameraFollow>();
        }

        if (fallbackCamera == null)
        {
            fallbackCamera = Camera.main;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (IsSkillCheckActive)
        {
            DrawSkillCheckUI();
            return;
        }

        if (showRequestHint)
        {
            DrawRequestHint();
        }

        if (showDebugState)
        {
            DrawDebugState();
        }
    }

    private void DrawSkillCheckUI()
    {
        float elapsed = Mathf.Max(0f, Time.time - skillCheckStartedAt - skillCheckStartDelay);
        float normalizedNeedle = Mathf.Repeat(elapsed / Mathf.Max(0.01f, skillCheckDuration), 1f);
        float centerY = Screen.height * 0.55f;
        DrawSingleSkillCheck(waterCheck, new Vector2(Screen.width * 0.38f, centerY), normalizedNeedle);
        DrawSingleSkillCheck(fireCheck, new Vector2(Screen.width * 0.62f, centerY), normalizedNeedle);

        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 170f, centerY + 88f, 340f, 24f), "Both players must hit inside the bright arc");
    }

    private void DrawSingleSkillCheck(SkillCheckRuntime check, Vector2 center, float normalizedNeedle)
    {
        Color baseColor = check.required
            ? new Color(0f, 0f, 0f, 0.72f)
            : new Color(0f, 0f, 0f, 0.25f);
        Color successColor = check.succeeded
            ? new Color(0.25f, 1f, 0.35f, 0.95f)
            : new Color(1f, 0.93f, 0.25f, 0.95f);
        Color needleColor = check.failed
            ? new Color(1f, 0.15f, 0.08f, 1f)
            : Color.white;

        DrawCircle(center, 62f, baseColor, 8f, 64);
        DrawArc(center, 62f, check.successCenter - successWindowSize * 0.5f, successWindowSize, successColor, 11f, 20);
        DrawNeedle(center, 56f, normalizedNeedle, needleColor, 4f);

        GUI.color = Color.white;
        string stateText = check.required
            ? check.succeeded ? "SUCCESS" : check.failed ? "FAIL" : check.key.ToString()
            : "N/A";
        GUI.Label(new Rect(center.x - 70f, center.y - 98f, 140f, 24f), check.label + " : " + stateText);
    }

    private void DrawRequestHint()
    {
        PlayerCharacter eligible = GetEligibleRequester();
        if (eligible == null)
        {
            return;
        }

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        Rect background = new Rect(Screen.width * 0.5f - 165f, Screen.height - 74f, 330f, 34f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(background.x + 12f, background.y + 8f, background.width - 24f, 22f), eligible.PlayerId + " falling - Press " + rescueRequestKey + " to request rescue");
    }

    private void DrawDebugState()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(12f, 12f, 420f, 22f), "Fall Rescue State: " + state);
    }

    private static void DrawCircle(Vector2 center, float radius, Color color, float width, int segments)
    {
        DrawArc(center, radius, 0f, 1f, color, width, segments);
    }

    private static void DrawArc(Vector2 center, float radius, float startNormalized, float sizeNormalized, Color color, float width, int segments)
    {
        float previous = startNormalized;
        for (int i = 1; i <= segments; i++)
        {
            float current = startNormalized + sizeNormalized * i / segments;
            Vector2 a = PointOnCircle(center, radius, previous);
            Vector2 b = PointOnCircle(center, radius, current);
            DrawLine(a, b, color, width);
            previous = current;
        }
    }

    private static void DrawNeedle(Vector2 center, float radius, float normalized, Color color, float width)
    {
        DrawLine(center, PointOnCircle(center, radius, normalized), color, width);
    }

    private static Vector2 PointOnCircle(Vector2 center, float radius, float normalized)
    {
        float angle = normalized * Mathf.PI * 2f - Mathf.PI * 0.5f;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        GUI.color = color;

        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        float length = Vector2.Distance(a, b);
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), Texture2D.whiteTexture);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }
}
