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

    [Header("Skill Check")]
    [SerializeField] private RescueSkillCheckUI skillCheckUI;
    [SerializeField] private KeyCode waterSkillCheckKey = KeyCode.F;
    [SerializeField] private KeyCode fireSkillCheckKey = KeyCode.J;
    [SerializeField] private float skillCheckStartDelay = 0.15f;

    [Header("Skill Check Sequence")]
    [SerializeField] private int baseSkillCheckCount = 2;
    [SerializeField] private bool requireEachPlayerAtLeastOnce = true;
    [SerializeField] private bool randomizeSkillCheckOrder = true;
    [SerializeField] private bool allowSamePlayerConsecutive = true;

    [Header("Rescue Pressure")]
    [SerializeField] private int rescuePressure;
    [SerializeField] private int maxRescuePressure = 3;
    [SerializeField] private float baseNeedleSpeed = 180f;
    [SerializeField] private float pressureNeedleSpeedAdd = 35f;
    [SerializeField, Range(0.02f, 0.5f)] private float baseSuccessZoneSize = 0.25f;
    [SerializeField] private float pressureSuccessZoneShrink = 0.04f;
    [SerializeField, Range(0.02f, 0.5f)] private float minSuccessZoneSize = 0.1f;

    [Header("Progress Reset")]
    [SerializeField] private float stableStandTime = 0.3f;

    [Header("Resonance Mist Zone")]
    [SerializeField] private bool useResonanceMistZone = true;
    [SerializeField] private GameObject resonanceMistZonePrefab;
    [SerializeField] private Vector2 mistZoneSize = new Vector2(8f, 4f);
    [SerializeField] private Vector2 mistZoneOffset = new Vector2(0f, 1f);
    [SerializeField] private bool drawMistZoneGizmo = true;

    [Header("Success Zone Placement")]
    [SerializeField] private bool randomizeSuccessZonePlacement = true;
    [SerializeField] private float successZoneFrontAngle = 55f;
    [SerializeField] private float successZoneBackAngle = 300f;
    [SerializeField] private float pressureFrontBiasPower = 2f;
    [SerializeField] private bool showSuccessZonePlacementDebug = true;

    [Header("Skill Check Slow Motion")]
    [SerializeField] private bool slowWorldDuringSkillCheck = true;
    [SerializeField, Range(0.01f, 1f)] private float skillCheckWorldTimeScale = 0.35f;
    [SerializeField] private bool scaleFixedDeltaTime = true;

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
    private float savedTimeScale = 1f;
    private float savedFixedDeltaTime = 0.02f;
    private bool timeScaleOverrideActive;
    private float stableStandTimer;
    private Vector3 pendingMistZoneBasePosition;
    private bool hasPendingMistZoneBasePosition;
    private Vector3 activeMistZoneCenter;
    private bool hasActiveMistZoneCenter;
    private GameObject activeMistZoneObject;
    private ParticleSystem[] activeMistZoneParticles;
    private Material activeMistZoneMaterial;

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
        SetRescuePressure(rescuePressure);
    }

    private void OnDisable()
    {
        RestoreSkillCheckTimeScale();
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
                UpdateProgressReset();
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

    public bool RequestRescue(PlayerCharacter requester)
    {
        ResolveReferences();
        if (!CanRequestRescue(requester))
        {
            return false;
        }

        BeginRescueSkillCheck(requester);
        return true;
    }

    private bool CanRequestRescue(PlayerCharacter requester)
    {
        return waterPlayer != null
            && firePlayer != null
            && requester != null
            && state == RescueState.Idle
            && (requester == waterPlayer || requester == firePlayer)
            && waterPlayer.IsAliveLike
            && firePlayer.IsAliveLike;
    }

    private void BeginRescueSkillCheck(PlayerCharacter requester)
    {
        fallingPlayer = requester;
        partnerPlayer = GetPartnerFor(fallingPlayer);
        rescueSafePosition = fallingPlayer.LastSafePosition;

        FocusCameraOnFallingPlayer();
        SetPlayersInputLocked(true);
        ApplySkillCheckTimeScale();

        if (skillCheckUI != null)
        {
            skillCheckUI.Begin(
                waterPlayer,
                waterSkillCheckKey,
                "Water",
                firePlayer,
                fireSkillCheckKey,
                "Fire",
                GetCurrentNeedleSpeed(),
                GetCurrentSuccessZoneSize(),
                skillCheckStartDelay,
                baseSkillCheckCount,
                requireEachPlayerAtLeastOnce,
                randomizeSkillCheckOrder,
                allowSamePlayerConsecutive,
                rescuePressure,
                maxRescuePressure,
                randomizeSuccessZonePlacement,
                successZoneFrontAngle,
                successZoneBackAngle,
                pressureFrontBiasPower,
                showSuccessZonePlacementDebug);
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

        QueueMistZoneAt(rescueSafePosition);
        SetRescuePressure(rescuePressure + 1);
        EndRescueSequence();
    }

    private void FailSkillCheck()
    {
        RestoreSkillCheckTimeScale();
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

        SetRescuePressure(0);
        EndRescueSequence();
    }

    private void EndRescueSequence()
    {
        RestoreSkillCheckTimeScale();
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

    private float GetCurrentNeedleSpeed()
    {
        return Mathf.Max(1f, baseNeedleSpeed + rescuePressure * pressureNeedleSpeedAdd);
    }

    private float GetCurrentSuccessZoneSize()
    {
        float size = baseSuccessZoneSize - rescuePressure * pressureSuccessZoneShrink;
        return Mathf.Max(minSuccessZoneSize, size);
    }

    private void SetRescuePressure(int value)
    {
        int previousPressure = rescuePressure;
        rescuePressure = Mathf.Clamp(value, 0, Mathf.Max(0, maxRescuePressure));
        ApplyResonanceMistZone(previousPressure);
    }

    private void QueueMistZoneAt(Vector3 basePosition)
    {
        pendingMistZoneBasePosition = basePosition;
        hasPendingMistZoneBasePosition = true;
    }

    private void ApplyResonanceMistZone(int previousPressure)
    {
        if (!useResonanceMistZone || rescuePressure <= 0)
        {
            ClearResonanceMistZone();
            return;
        }

        if (previousPressure <= 0 || !hasActiveMistZoneCenter || activeMistZoneObject == null)
        {
            Vector3 basePosition = hasPendingMistZoneBasePosition
                ? pendingMistZoneBasePosition
                : fallingPlayer != null
                    ? fallingPlayer.LastSafePosition
                    : new Vector3(0f, GetTeamHeight(), 0f);
            activeMistZoneCenter = basePosition + (Vector3)mistZoneOffset;
            hasActiveMistZoneCenter = true;
            hasPendingMistZoneBasePosition = false;
        }

        EnsureResonanceMistZoneObject();
        ConfigureResonanceMistZone(rescuePressure);
    }

    private void EnsureResonanceMistZoneObject()
    {
        if (activeMistZoneObject == null)
        {
            activeMistZoneObject = resonanceMistZonePrefab != null
                ? Instantiate(resonanceMistZonePrefab)
                : CreateDefaultMistZoneObject();
            activeMistZoneObject.name = "ResonanceMistZone";
            activeMistZoneObject.hideFlags = HideFlags.DontSave;
            activeMistZoneObject.transform.SetParent(transform, false);
        }

        activeMistZoneObject.SetActive(true);
        activeMistZoneObject.transform.position = activeMistZoneCenter;
        activeMistZoneParticles = activeMistZoneObject.GetComponentsInChildren<ParticleSystem>(true);

        if (activeMistZoneParticles.Length == 0)
        {
            activeMistZoneParticles = new[] { activeMistZoneObject.AddComponent<ParticleSystem>() };
        }
    }

    private GameObject CreateDefaultMistZoneObject()
    {
        GameObject mistObject = new GameObject("ResonanceMistZone");
        mistObject.AddComponent<ParticleSystem>();
        return mistObject;
    }

    private void ConfigureResonanceMistZone(int level)
    {
        if (activeMistZoneObject == null)
        {
            return;
        }

        activeMistZoneObject.transform.position = activeMistZoneCenter;

        if (activeMistZoneParticles == null || activeMistZoneParticles.Length == 0)
        {
            activeMistZoneParticles = activeMistZoneObject.GetComponentsInChildren<ParticleSystem>(true);
        }

        Color mistColor = new Color(0.48f, 0.34f, 1f, Mathf.Clamp01(0.14f + level * 0.1f));
        for (int i = 0; i < activeMistZoneParticles.Length; i++)
        {
            ParticleSystem mist = activeMistZoneParticles[i];
            if (mist == null)
            {
                continue;
            }

            mist.gameObject.SetActive(true);

            ParticleSystem.MainModule main = mist.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 1.2f + level * 0.25f;
            main.startSpeed = 0.04f + level * 0.04f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f + level * 0.05f, 0.65f + level * 0.12f);
            main.startColor = mistColor;
            main.maxParticles = 80 + level * 45;

            ParticleSystem.EmissionModule emission = mist.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f + level * 16f;

            ParticleSystem.ShapeModule shape = mist.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(0.01f, mistZoneSize.x), Mathf.Max(0.01f, mistZoneSize.y), 0.1f);
            shape.randomDirectionAmount = 0.18f + level * 0.08f;

            ParticleSystem.NoiseModule noise = mist.noise;
            noise.enabled = level >= 2;
            noise.strength = 0.05f + level * 0.08f;
            noise.frequency = 0.35f + level * 0.25f;
            noise.scrollSpeed = 0.15f + level * 0.08f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = mist.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            Color end = mistColor;
            end.a = 0f;
            gradient.SetKeys(
                new[] { new GradientColorKey(mistColor, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(mistColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer mistRenderer = mist.GetComponent<ParticleSystemRenderer>();
            mistRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            mistRenderer.sortingOrder = 5;
            if (activeMistZoneMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    activeMistZoneMaterial = new Material(shader)
                    {
                        name = "Runtime_ResonanceMistZone"
                    };
                }
            }
            if (activeMistZoneMaterial != null)
            {
                mistRenderer.sharedMaterial = activeMistZoneMaterial;
            }

            if (!mist.isPlaying)
            {
                mist.Play(true);
            }
        }
    }

    private void ClearResonanceMistZone()
    {
        hasActiveMistZoneCenter = false;
        hasPendingMistZoneBasePosition = false;

        if (activeMistZoneParticles != null)
        {
            for (int i = 0; i < activeMistZoneParticles.Length; i++)
            {
                if (activeMistZoneParticles[i] != null)
                {
                    activeMistZoneParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        if (activeMistZoneObject != null)
        {
            GameObject objectToDestroy = activeMistZoneObject;
            activeMistZoneObject.SetActive(false);
            activeMistZoneObject = null;
            activeMistZoneParticles = null;

            if (Application.isPlaying)
            {
                Destroy(objectToDestroy);
            }
            else
            {
                DestroyImmediate(objectToDestroy);
            }
        }
    }

    private void UpdateProgressReset()
    {
        if (rescuePressure <= 0)
        {
            stableStandTimer = 0f;
            return;
        }

        if (waterPlayer == null || firePlayer == null || !waterPlayer.IsAliveLike || !firePlayer.IsAliveLike)
        {
            stableStandTimer = 0f;
            return;
        }

        if (!useResonanceMistZone || !hasActiveMistZoneCenter)
        {
            stableStandTimer = 0f;
            return;
        }

        if (IsPlayerInsideActiveMistZone(waterPlayer) || IsPlayerInsideActiveMistZone(firePlayer))
        {
            stableStandTimer = 0f;
            return;
        }

        stableStandTimer += Time.deltaTime;
        if (stableStandTimer < stableStandTime)
        {
            return;
        }

        stableStandTimer = 0f;
        SetRescuePressure(0);
    }

    private bool IsPlayerInsideActiveMistZone(PlayerCharacter player)
    {
        if (player == null || !hasActiveMistZoneCenter)
        {
            return false;
        }

        Vector3 position = player.transform.position;
        Vector2 halfSize = mistZoneSize * 0.5f;
        return Mathf.Abs(position.x - activeMistZoneCenter.x) <= halfSize.x
            && Mathf.Abs(position.y - activeMistZoneCenter.y) <= halfSize.y;
    }

    private float GetTeamHeight()
    {
        if (waterPlayer == null || firePlayer == null)
        {
            return 0f;
        }

        return Mathf.Min(waterPlayer.transform.position.y, firePlayer.transform.position.y);
    }

    private void ApplySkillCheckTimeScale()
    {
        if (!slowWorldDuringSkillCheck || timeScaleOverrideActive)
        {
            return;
        }

        savedTimeScale = Time.timeScale;
        savedFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = Mathf.Clamp(skillCheckWorldTimeScale, 0.01f, 1f);

        if (scaleFixedDeltaTime)
        {
            Time.fixedDeltaTime = Mathf.Max(0.001f, savedFixedDeltaTime * Time.timeScale);
        }

        timeScaleOverrideActive = true;
    }

    private void RestoreSkillCheckTimeScale()
    {
        if (!timeScaleOverrideActive)
        {
            return;
        }

        Time.timeScale = savedTimeScale;
        Time.fixedDeltaTime = savedFixedDeltaTime;
        timeScaleOverrideActive = false;
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
        if (!CanTeamRequestRescue())
        {
            return;
        }

        Color previous = GUI.color;
        Rect background = new Rect(Screen.width * 0.5f - 165f, Screen.height - 74f, 330f, 34f);
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = Color.white;
        string waterKey = waterPlayer != null ? waterPlayer.RescueRequestKey.ToString() : "-";
        string fireKey = firePlayer != null ? firePlayer.RescueRequestKey.ToString() : "-";
        GUI.Label(new Rect(background.x + 12f, background.y + 8f, background.width - 24f, 22f), "Rescue ready - Water [" + waterKey + "] / Fire [" + fireKey + "]");
        GUI.color = previous;
    }

    private bool CanTeamRequestRescue()
    {
        return state == RescueState.Idle
            && waterPlayer != null
            && firePlayer != null
            && waterPlayer.IsAliveLike
            && firePlayer.IsAliveLike;
    }

    private void DrawDebugState()
    {
        Color previous = GUI.color;
        GUI.color = Color.white;
        GUI.Label(new Rect(12f, 12f, 520f, 22f), "Fall Rescue State: " + state + "  Pressure: " + rescuePressure);
        GUI.color = previous;
    }

    private void OnValidate()
    {
        skillCheckStartDelay = Mathf.Max(0f, skillCheckStartDelay);
        baseSkillCheckCount = Mathf.Max(1, baseSkillCheckCount);
        maxRescuePressure = Mathf.Max(0, maxRescuePressure);
        rescuePressure = Mathf.Clamp(rescuePressure, 0, maxRescuePressure);
        baseNeedleSpeed = Mathf.Max(1f, baseNeedleSpeed);
        pressureNeedleSpeedAdd = Mathf.Max(0f, pressureNeedleSpeedAdd);
        baseSuccessZoneSize = Mathf.Clamp(baseSuccessZoneSize, 0.02f, 0.5f);
        pressureSuccessZoneShrink = Mathf.Max(0f, pressureSuccessZoneShrink);
        minSuccessZoneSize = Mathf.Clamp(minSuccessZoneSize, 0.02f, baseSuccessZoneSize);
        stableStandTime = Mathf.Max(0f, stableStandTime);
        mistZoneSize.x = Mathf.Max(0.01f, mistZoneSize.x);
        mistZoneSize.y = Mathf.Max(0.01f, mistZoneSize.y);
        successZoneFrontAngle = Mathf.Repeat(successZoneFrontAngle, 360f);
        successZoneBackAngle = Mathf.Repeat(successZoneBackAngle, 360f);
        pressureFrontBiasPower = Mathf.Max(0.01f, pressureFrontBiasPower);
        skillCheckWorldTimeScale = Mathf.Clamp(skillCheckWorldTimeScale, 0.01f, 1f);
        minFailedFallViewTime = Mathf.Max(0f, minFailedFallViewTime);
        fallbackCameraSmoothTime = Mathf.Max(0.01f, fallbackCameraSmoothTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawMistZoneGizmo)
        {
            return;
        }

        Vector3 center = hasActiveMistZoneCenter
            ? activeMistZoneCenter
            : transform.position + (Vector3)mistZoneOffset;
        Gizmos.color = new Color(0.48f, 0.34f, 1f, 0.28f);
        Gizmos.DrawWireCube(center, new Vector3(Mathf.Max(0.01f, mistZoneSize.x), Mathf.Max(0.01f, mistZoneSize.y), 0.1f));
    }

    private void OnDestroy()
    {
        if (activeMistZoneParticles != null)
        {
            for (int i = 0; i < activeMistZoneParticles.Length; i++)
            {
                if (activeMistZoneParticles[i] != null)
                {
                    activeMistZoneParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        if (activeMistZoneMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(activeMistZoneMaterial);
        }
        else
        {
            DestroyImmediate(activeMistZoneMaterial);
        }
    }
}
