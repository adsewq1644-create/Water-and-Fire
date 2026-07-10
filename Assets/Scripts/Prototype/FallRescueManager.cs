using UnityEngine;

public class FallRescueManager : MonoBehaviour
{
    private enum RescueState
    {
        Idle,
        SkillCheck,
        FailedFalling
    }

    [Header("Players")]
    [SerializeField] private PlayerCharacter waterPlayer;
    [SerializeField] private PlayerCharacter firePlayer;

    [Header("Request")]
    [SerializeField] private KeyCode rescueRequestKey = KeyCode.F;
    [SerializeField] private float requiredDropBelowSafePlatform = 2.5f;
    [SerializeField] private float minFallingSpeed = 1.5f;

    [Header("Skill Check")]
    [SerializeField] private RescueSkillCheckUI skillCheckUI;
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
    private Transform savedCameraTarget;
    private bool hasSavedCameraTarget;
    private Vector3 fallbackCameraVelocity;
    private Vector3 rescueSafePosition;
    private float failedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindFirstObjectByType<FallRescueManager>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("FallRescueManager");
        runtimeObject.AddComponent<FallRescueManager>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        SetPlayersInputLocked(false);
        if (skillCheckUI != null)
        {
            skillCheckUI.Cancel();
        }
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
        if ((state == RescueState.SkillCheck || state == RescueState.FailedFalling)
            && cameraFollow == null
            && fallbackCamera != null
            && fallingPlayer != null)
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
        return player != null && player.CanRequestFallRescue(requiredDropBelowSafePlatform, minFallingSpeed);
    }

    private void BeginRescueSkillCheck(PlayerCharacter requester)
    {
        fallingPlayer = requester;
        partnerPlayer = GetPartnerFor(fallingPlayer);
        rescueSafePosition = fallingPlayer.LastSafePosition;

        FocusCameraOnFallingPlayer();
        SetPlayersInputLocked(true);

        if (skillCheckUI != null)
        {
            skillCheckUI.Begin(
                waterPlayer,
                waterSkillCheckKey,
                "Water",
                firePlayer,
                fireSkillCheckKey,
                "Fire",
                skillCheckDuration,
                successWindowSize,
                skillCheckStartDelay);
        }

        state = RescueState.SkillCheck;
    }

    private void UpdateSkillCheck()
    {
        if (skillCheckUI == null)
        {
            FailSkillCheck();
            return;
        }

        if (skillCheckUI.HasFailed)
        {
            FailSkillCheck();
            return;
        }

        if (skillCheckUI.HasSucceeded)
        {
            CompleteSkillCheckSuccess();
        }
    }

    private void CompleteSkillCheckSuccess()
    {
        if (fallingPlayer != null)
        {
            fallingPlayer.TeleportForFallRescue(rescueSafePosition);
        }

        EndRescueSequence();
    }

    private void FailSkillCheck()
    {
        failedAt = Time.time;
        state = RescueState.FailedFalling;
        SetPlayersInputLocked(false);
        if (skillCheckUI != null)
        {
            skillCheckUI.Cancel();
        }

        FocusCameraOnFallingPlayer();
    }

    private void UpdateFailedFalling()
    {
        if (fallingPlayer == null)
        {
            EndRescueSequence();
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

        EndRescueSequence();
    }

    private void EndRescueSequence()
    {
        SetPlayersInputLocked(false);

        if (skillCheckUI != null)
        {
            skillCheckUI.Cancel();
        }

        if (restoreCameraTargetAfterRescue)
        {
            RestoreCameraTarget();
        }

        state = RescueState.Idle;
        fallingPlayer = null;
        partnerPlayer = null;
        rescueSafePosition = default;
    }

    private void SetPlayersInputLocked(bool locked)
    {
        if (waterPlayer != null)
        {
            waterPlayer.SetInputLocked(locked);
        }

        if (firePlayer != null)
        {
            firePlayer.SetInputLocked(locked);
        }
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
        if (cameraFollow == null)
        {
            return;
        }

        if (!hasSavedCameraTarget)
        {
            savedCameraTarget = cameraFollow.Target;
            hasSavedCameraTarget = true;
        }

        cameraFollow.SetTarget(fallingPlayer.transform);
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

        if (skillCheckUI == null)
        {
            skillCheckUI = FindFirstObjectByType<RescueSkillCheckUI>();
        }

        if (skillCheckUI == null)
        {
            GameObject uiObject = new GameObject("RescueSkillCheckUI");
            skillCheckUI = uiObject.AddComponent<RescueSkillCheckUI>();
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

        if (state == RescueState.Idle && showRequestHint)
        {
            DrawRequestHint();
        }

        if (showDebugState)
        {
            DrawDebugState();
        }
    }

    private void DrawRequestHint()
    {
        PlayerCharacter eligible = GetEligibleRequester();
        if (eligible == null)
        {
            return;
        }

        Color previous = GUI.color;
        Rect background = new Rect(Screen.width * 0.5f - 165f, Screen.height - 74f, 330f, 34f);
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(background.x + 12f, background.y + 8f, background.width - 24f, 22f), eligible.PlayerId + " falling - Press " + rescueRequestKey + " to request rescue");
        GUI.color = previous;
    }

    private void DrawDebugState()
    {
        Color previous = GUI.color;
        GUI.color = Color.white;
        GUI.Label(new Rect(12f, 12f, 420f, 22f), "Fall Rescue State: " + state);
        GUI.color = previous;
    }

    private void OnValidate()
    {
        requiredDropBelowSafePlatform = Mathf.Max(0f, requiredDropBelowSafePlatform);
        minFallingSpeed = Mathf.Max(0f, minFallingSpeed);
        skillCheckDuration = Mathf.Max(0.1f, skillCheckDuration);
        successWindowSize = Mathf.Clamp(successWindowSize, 0.04f, 0.45f);
        skillCheckStartDelay = Mathf.Max(0f, skillCheckStartDelay);
        minFailedFallViewTime = Mathf.Max(0f, minFailedFallViewTime);
        fallbackCameraSmoothTime = Mathf.Max(0.01f, fallbackCameraSmoothTime);
    }
}
