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

    private RuntimeCheck waterCheck;
    private RuntimeCheck fireCheck;
    private bool active;
    private bool failed;
    private bool completed;
    private float checkpointStartedAt;
    private float checkpointDuration;
    private float successWindowSize;
    private float startDelay;
    private float successCenter;
    private int ignoreInputUntilFrame;
    private int checkpointIndex;
    private int totalCheckpoints;

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
        float checkDuration,
        float windowSize,
        float delay,
        int checkpointCount)
    {
        active = true;
        failed = false;
        completed = false;
        checkpointDuration = Mathf.Max(0.1f, checkDuration);
        successWindowSize = Mathf.Clamp(windowSize, 0.04f, 0.45f);
        startDelay = Mathf.Max(0f, delay);
        totalCheckpoints = Mathf.Max(1, checkpointCount);

        waterCheck = CreateCheck(waterPlayer, waterKey, waterLabel, waterCheckpointColor);
        fireCheck = CreateCheck(firePlayer, fireKey, fireLabel, fireCheckpointColor);
        StartCheckpoint(0);
    }

    public void Cancel()
    {
        active = false;
        failed = false;
        completed = false;
        checkpointIndex = 0;
        totalCheckpoints = 0;
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

    private void StartCheckpoint(int index)
    {
        checkpointIndex = index;
        if (checkpointIndex >= totalCheckpoints)
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
        successCenter = Random.Range(0.18f, 0.82f);
        ignoreInputUntilFrame = Time.frameCount + 1;
    }

    private RuntimeCheck GetCurrentCheck()
    {
        return checkpointIndex % 2 == 0 ? waterCheck : fireCheck;
    }

    private RuntimeCheck GetOtherCheck()
    {
        return checkpointIndex % 2 == 0 ? fireCheck : waterCheck;
    }

    private void Update()
    {
        if (!active || failed || completed)
        {
            return;
        }

        float elapsed = Time.unscaledTime - checkpointStartedAt;
        if (Time.frameCount <= ignoreInputUntilFrame)
        {
            return;
        }

        RuntimeCheck currentCheck = GetCurrentCheck();
        RuntimeCheck otherCheck = GetOtherCheck();
        bool currentPressed = Input.GetKeyDown(currentCheck.key);
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

        if (elapsed - startDelay > checkpointDuration)
        {
            failed = true;
        }
    }

    private float GetNeedleNormalized(float elapsed)
    {
        float activeElapsed = Mathf.Max(0f, elapsed - startDelay);
        return Mathf.Repeat(activeElapsed / checkpointDuration, 1f);
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

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
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
        GUI.Label(new Rect(center.x - 120f, center.y + 118f, 240f, 24f), $"Checkpoint {checkpointIndex + 1} / {totalCheckpoints}   {currentCheck.label}: {currentCheck.key}");
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
