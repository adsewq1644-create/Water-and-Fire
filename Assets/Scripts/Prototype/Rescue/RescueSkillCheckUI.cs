using System.Collections.Generic;
using UnityEngine;

public class RescueSkillCheckUI : MonoBehaviour
{
    private struct RuntimeCheck
    {
        public PlayerCharacter player;
        public KeyCode key;
        public string label;
        public bool required;
        public Color color;
    }

    private const float CircleRadius = 68f;

    [Header("Visual")]
    [SerializeField] private bool showInstruction = true;
    [SerializeField] private Color circleColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color waterCheckpointColor = new Color(0.25f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color fireCheckpointColor = new Color(1f, 0.2f, 0.08f, 0.95f);
    [SerializeField] private Color completedColor = new Color(0.25f, 1f, 0.35f, 0.95f);
    [SerializeField] private Color failedColor = new Color(1f, 0.15f, 0.08f, 1f);

    private readonly List<RuntimeCheck> skillCheckSequence = new List<RuntimeCheck>();
    private RuntimeCheck waterCheck;
    private RuntimeCheck fireCheck;
    private bool active;
    private bool failed;
    private bool completed;
    private bool randomizeSuccessZonePlacement;
    private bool showSuccessZonePlacementDebug;
    private float checkpointStartedAt;
    private float needleDegreesPerSecond;
    private float successWindowSize;
    private float startDelay;
    private float successCenter;
    private float successZoneFrontAngle;
    private float successZoneBackAngle;
    private float pressureFrontBiasPower;
    private int ignoreInputUntilFrame;
    private int checkpointIndex;
    private int pressureLevel;
    private int maxPressureLevel;
    private float lastSuccessZoneAngle;

    public bool IsActive => active;
    public bool HasFailed => active && failed;
    public bool HasSucceeded => active && completed;

    public void Begin(
        PlayerCharacter waterPlayer,
        KeyCode waterKey,
        string waterLabel,
        PlayerCharacter firePlayer,
        KeyCode fireKey,
        string fireLabel,
        float needleSpeed,
        float windowSize,
        float delay,
        int baseSkillCheckCount,
        bool requireEachPlayerAtLeastOnce,
        bool randomizeSkillCheckOrder,
        bool allowSamePlayerConsecutive,
        int rescuePressure,
        int maxRescuePressure,
        bool randomizeSuccessZonePlacement,
        float successZoneFrontAngle,
        float successZoneBackAngle,
        float pressureFrontBiasPower,
        bool showSuccessZonePlacementDebug)
    {
        active = true;
        failed = false;
        completed = false;
        needleDegreesPerSecond = Mathf.Max(1f, needleSpeed);
        successWindowSize = Mathf.Clamp(windowSize, 0.04f, 0.45f);
        startDelay = Mathf.Max(0f, delay);
        pressureLevel = Mathf.Max(0, rescuePressure);
        maxPressureLevel = Mathf.Max(0, maxRescuePressure);
        this.randomizeSuccessZonePlacement = randomizeSuccessZonePlacement;
        this.successZoneFrontAngle = Mathf.Repeat(successZoneFrontAngle, 360f);
        this.successZoneBackAngle = Mathf.Repeat(successZoneBackAngle, 360f);
        this.pressureFrontBiasPower = Mathf.Max(0.01f, pressureFrontBiasPower);
        this.showSuccessZonePlacementDebug = showSuccessZonePlacementDebug;

        waterCheck = CreateCheck(waterPlayer, waterKey, waterLabel, waterCheckpointColor);
        fireCheck = CreateCheck(firePlayer, fireKey, fireLabel, fireCheckpointColor);
        BuildSkillCheckSequence(
            Mathf.Max(1, baseSkillCheckCount),
            requireEachPlayerAtLeastOnce,
            randomizeSkillCheckOrder,
            allowSamePlayerConsecutive);
        StartCheckpoint(0);
    }

    public void Cancel()
    {
        active = false;
        failed = false;
        completed = false;
        checkpointIndex = 0;
        pressureLevel = 0;
        maxPressureLevel = 0;
        skillCheckSequence.Clear();
        waterCheck = default;
        fireCheck = default;
    }

    private RuntimeCheck CreateCheck(PlayerCharacter player, KeyCode key, string label, Color color)
    {
        return new RuntimeCheck
        {
            player = player,
            key = key,
            label = label,
            required = player != null && player.IsAliveLike,
            color = color
        };
    }

    private void BuildSkillCheckSequence(
        int baseSkillCheckCount,
        bool requireEachPlayerAtLeastOnce,
        bool randomizeSkillCheckOrder,
        bool allowSamePlayerConsecutive)
    {
        skillCheckSequence.Clear();

        var availableChecks = new List<RuntimeCheck>();
        if (waterCheck.required)
        {
            availableChecks.Add(waterCheck);
        }
        if (fireCheck.required)
        {
            availableChecks.Add(fireCheck);
        }

        if (availableChecks.Count == 0)
        {
            return;
        }

        int targetCount = Mathf.Max(baseSkillCheckCount, requireEachPlayerAtLeastOnce ? availableChecks.Count : 1);
        if (requireEachPlayerAtLeastOnce)
        {
            for (int i = 0; i < availableChecks.Count; i++)
            {
                skillCheckSequence.Add(availableChecks[i]);
            }
        }

        while (skillCheckSequence.Count < targetCount)
        {
            RuntimeCheck nextCheck = PickRandomCheck(availableChecks, skillCheckSequence, allowSamePlayerConsecutive);
            skillCheckSequence.Add(nextCheck);
        }

        if (randomizeSkillCheckOrder)
        {
            ShuffleSequence(allowSamePlayerConsecutive);
        }
    }

    private RuntimeCheck PickRandomCheck(List<RuntimeCheck> availableChecks, List<RuntimeCheck> currentSequence, bool allowSamePlayerConsecutive)
    {
        if (availableChecks.Count <= 1 || allowSamePlayerConsecutive || currentSequence.Count == 0)
        {
            return availableChecks[Random.Range(0, availableChecks.Count)];
        }

        RuntimeCheck previous = currentSequence[currentSequence.Count - 1];
        var candidates = new List<RuntimeCheck>();
        for (int i = 0; i < availableChecks.Count; i++)
        {
            if (availableChecks[i].player != previous.player)
            {
                candidates.Add(availableChecks[i]);
            }
        }

        return candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : availableChecks[Random.Range(0, availableChecks.Count)];
    }

    private void ShuffleSequence(bool allowSamePlayerConsecutive)
    {
        for (int i = skillCheckSequence.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (skillCheckSequence[i], skillCheckSequence[swapIndex]) = (skillCheckSequence[swapIndex], skillCheckSequence[i]);
        }

        if (allowSamePlayerConsecutive || skillCheckSequence.Count <= 2)
        {
            return;
        }

        for (int attempt = 0; attempt < 8 && HasConsecutiveSamePlayer(); attempt++)
        {
            for (int i = skillCheckSequence.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (skillCheckSequence[i], skillCheckSequence[swapIndex]) = (skillCheckSequence[swapIndex], skillCheckSequence[i]);
            }
        }
    }

    private bool HasConsecutiveSamePlayer()
    {
        for (int i = 1; i < skillCheckSequence.Count; i++)
        {
            if (skillCheckSequence[i].player == skillCheckSequence[i - 1].player)
            {
                return true;
            }
        }

        return false;
    }

    private void StartCheckpoint(int index)
    {
        checkpointIndex = index;
        if (checkpointIndex >= skillCheckSequence.Count)
        {
            completed = true;
            return;
        }

        RuntimeCheck currentCheck = GetCurrentCheck();
        if (!currentCheck.required)
        {
            StartCheckpoint(checkpointIndex + 1);
            return;
        }

        checkpointStartedAt = Time.unscaledTime;
        successCenter = PickSuccessZoneCenter();
        ignoreInputUntilFrame = Time.frameCount + 1;
    }

    private RuntimeCheck GetCurrentCheck()
    {
        return skillCheckSequence.Count > 0 && checkpointIndex >= 0 && checkpointIndex < skillCheckSequence.Count
            ? skillCheckSequence[checkpointIndex]
            : default;
    }

    private RuntimeCheck GetOtherCheck(RuntimeCheck currentCheck)
    {
        if (waterCheck.player == currentCheck.player)
        {
            return fireCheck;
        }

        return waterCheck;
    }

    private float PickSuccessZoneCenter()
    {
        float front = successZoneFrontAngle;
        float back = successZoneBackAngle;
        if (back < front)
        {
            back += 360f;
        }

        float t = randomizeSuccessZonePlacement ? Random.value : 0.5f;
        float pressure01 = maxPressureLevel <= 0 ? 0f : Mathf.Clamp01((float)pressureLevel / maxPressureLevel);
        float biasPower = Mathf.Lerp(1f, pressureFrontBiasPower, pressure01);
        float biasedT = randomizeSuccessZonePlacement ? Mathf.Pow(t, biasPower) : t;
        lastSuccessZoneAngle = Mathf.Repeat(Mathf.Lerp(front, back, biasedT), 360f);
        return lastSuccessZoneAngle / 360f;
    }

    private void Update()
    {
        if (!active || failed || completed)
        {
            return;
        }

        if (skillCheckSequence.Count == 0)
        {
            failed = true;
            return;
        }

        float elapsed = Time.unscaledTime - checkpointStartedAt;
        if (Time.frameCount <= ignoreInputUntilFrame)
        {
            return;
        }

        RuntimeCheck currentCheck = GetCurrentCheck();
        RuntimeCheck otherCheck = GetOtherCheck(currentCheck);
        bool currentPressed = currentCheck.required && Input.GetKeyDown(currentCheck.key);
        bool otherPressed = otherCheck.required && Input.GetKeyDown(otherCheck.key);

        if (elapsed < startDelay)
        {
            if (currentPressed || otherPressed)
            {
                failed = true;
            }
            return;
        }

        if (otherPressed)
        {
            failed = true;
            return;
        }

        if (currentPressed)
        {
            if (IsNeedleInSuccessWindow(GetNeedleNormalized(elapsed), successCenter))
            {
                StartCheckpoint(checkpointIndex + 1);
                return;
            }

            failed = true;
            return;
        }

        if (elapsed - startDelay > GetCheckpointDuration())
        {
            failed = true;
        }
    }

    private float GetNeedleNormalized(float elapsed)
    {
        float activeElapsed = Mathf.Max(0f, elapsed - startDelay);
        return Mathf.Repeat(activeElapsed * needleDegreesPerSecond / 360f, 1f);
    }

    private float GetCheckpointDuration()
    {
        return 360f / Mathf.Max(1f, needleDegreesPerSecond);
    }

    private bool IsNeedleInSuccessWindow(float needle, float center)
    {
        float halfWindow = successWindowSize * 0.5f;
        float delta = Mathf.Abs(Mathf.DeltaAngle(needle * 360f, center * 360f)) / 360f;
        return delta <= halfWindow;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !active)
        {
            return;
        }

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f) + GetPressureShake();
        if (failed)
        {
            DrawResult(center, "FAIL", failedColor);
            return;
        }

        if (completed)
        {
            DrawResult(center, "SUCCESS", completedColor);
            return;
        }

        float elapsed = Time.unscaledTime - checkpointStartedAt;
        float normalizedNeedle = GetNeedleNormalized(elapsed);
        DrawSingleCheckpoint(center, normalizedNeedle);

        if (!showInstruction)
        {
            return;
        }

        RuntimeCheck currentCheck = GetCurrentCheck();
        GUI.color = Color.white;
        GUI.Label(new Rect(center.x - 190f, center.y + 96f, 380f, 24f), "Hit the key only when the needle enters the colored arc");
        GUI.Label(new Rect(center.x - 135f, center.y + 118f, 270f, 24f), $"Checkpoint {checkpointIndex + 1} / {skillCheckSequence.Count}   {currentCheck.label}: {currentCheck.key}");
        if (pressureLevel > 0)
        {
            GUI.Label(new Rect(center.x - 90f, center.y + 140f, 180f, 24f), $"Resonance Pressure: {pressureLevel}");
        }

        if (showSuccessZonePlacementDebug)
        {
            GUI.Label(new Rect(center.x - 110f, center.y + 162f, 220f, 24f), $"Zone Angle: {lastSuccessZoneAngle:0.#} deg");
        }
    }

    private Vector2 GetPressureShake()
    {
        if (pressureLevel <= 0)
        {
            return Vector2.zero;
        }

        float amount = pressureLevel * 1.6f;
        return new Vector2(
            Mathf.Sin(Time.unscaledTime * 31f) * amount,
            Mathf.Cos(Time.unscaledTime * 23f) * amount);
    }

    private void DrawSingleCheckpoint(Vector2 center, float normalizedNeedle)
    {
        RuntimeCheck currentCheck = GetCurrentCheck();
        DrawCircle(center, CircleRadius, circleColor, 8f, 64);
        DrawArc(center, CircleRadius, successCenter - successWindowSize * 0.5f, successWindowSize, currentCheck.color, 12f, 20);
        DrawNeedle(center, 60f, normalizedNeedle, Color.white, 4f);

        GUI.color = currentCheck.color;
        GUI.Label(new Rect(center.x - 80f, center.y - 108f, 160f, 24f), currentCheck.label + " TURN");
    }

    private void DrawResult(Vector2 center, string text, Color color)
    {
        DrawCircle(center, CircleRadius, circleColor, 8f, 64);
        GUI.color = color;
        GUI.Label(new Rect(center.x - 70f, center.y - 12f, 140f, 24f), text);
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
