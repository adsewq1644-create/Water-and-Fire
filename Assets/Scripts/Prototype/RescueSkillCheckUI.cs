using UnityEngine;

public class RescueSkillCheckUI : MonoBehaviour
{
    private struct RuntimeCheck
    {
        public PlayerCharacter player;
        public KeyCode key;
        public string label;
        public bool required;
        public bool succeeded;
        public bool failed;
        public float successCenter;
    }

    private const float CircleRadius = 62f;

    [Header("Visual")]
    [SerializeField] private bool showInstruction = true;
    [SerializeField] private Color circleColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color inactiveCircleColor = new Color(0f, 0f, 0f, 0.25f);
    [SerializeField] private Color successZoneColor = new Color(1f, 0.93f, 0.25f, 0.95f);
    [SerializeField] private Color completedColor = new Color(0.25f, 1f, 0.35f, 0.95f);
    [SerializeField] private Color failedColor = new Color(1f, 0.15f, 0.08f, 1f);

    private RuntimeCheck waterCheck;
    private RuntimeCheck fireCheck;
    private bool active;
    private bool failed;
    private float startedAt;
    private float duration;
    private float successWindowSize;
    private float startDelay;
    private int ignoreInputUntilFrame;

    public bool IsActive => active;
    public bool HasFailed => active && failed;
    public bool HasSucceeded => active && !failed && IsComplete(waterCheck) && IsComplete(fireCheck);

    public void Begin(
        PlayerCharacter waterPlayer,
        KeyCode waterKey,
        string waterLabel,
        PlayerCharacter firePlayer,
        KeyCode fireKey,
        string fireLabel,
        float checkDuration,
        float windowSize,
        float delay)
    {
        active = true;
        failed = false;
        startedAt = Time.time;
        duration = Mathf.Max(0.1f, checkDuration);
        successWindowSize = Mathf.Clamp(windowSize, 0.04f, 0.45f);
        startDelay = Mathf.Max(0f, delay);
        ignoreInputUntilFrame = Time.frameCount + 1;

        waterCheck = CreateCheck(waterPlayer, waterKey, waterLabel, 0.68f);
        fireCheck = CreateCheck(firePlayer, fireKey, fireLabel, 0.32f);
    }

    public void Cancel()
    {
        active = false;
        failed = false;
        waterCheck = default;
        fireCheck = default;
    }

    private RuntimeCheck CreateCheck(PlayerCharacter player, KeyCode key, string label, float fallbackCenter)
    {
        bool required = player != null && player.IsAliveLike;
        return new RuntimeCheck
        {
            player = player,
            key = key,
            label = label,
            required = required,
            successCenter = required ? Random.Range(0.18f, 0.82f) : fallbackCenter
        };
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        float elapsed = Time.time - startedAt;
        if (Time.frameCount <= ignoreInputUntilFrame)
        {
            return;
        }

        UpdateSingleCheck(ref waterCheck, elapsed);
        UpdateSingleCheck(ref fireCheck, elapsed);

        if (failed)
        {
            return;
        }

        float activeElapsed = elapsed - startDelay;
        if (activeElapsed > duration && (!IsComplete(waterCheck) || !IsComplete(fireCheck)))
        {
            failed = true;
        }
    }

    private void UpdateSingleCheck(ref RuntimeCheck check, float elapsed)
    {
        if (!check.required || check.succeeded || check.failed)
        {
            return;
        }

        if (!Input.GetKeyDown(check.key))
        {
            return;
        }

        if (elapsed < startDelay)
        {
            check.failed = true;
            failed = true;
            return;
        }

        float normalizedNeedle = GetNeedleNormalized(elapsed);
        if (IsNeedleInSuccessWindow(normalizedNeedle, check.successCenter))
        {
            check.succeeded = true;
            return;
        }

        check.failed = true;
        failed = true;
    }

    private float GetNeedleNormalized(float elapsed)
    {
        float activeElapsed = Mathf.Max(0f, elapsed - startDelay);
        return Mathf.Repeat(activeElapsed / duration, 1f);
    }

    private bool IsNeedleInSuccessWindow(float needle, float center)
    {
        float halfWindow = successWindowSize * 0.5f;
        float delta = Mathf.Abs(Mathf.DeltaAngle(needle * 360f, center * 360f)) / 360f;
        return delta <= halfWindow;
    }

    private bool IsComplete(RuntimeCheck check)
    {
        return !check.required || check.succeeded;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !active)
        {
            return;
        }

        float elapsed = Time.time - startedAt;
        float normalizedNeedle = GetNeedleNormalized(elapsed);
        float centerY = Screen.height * 0.55f;
        DrawSingleCheck(waterCheck, new Vector2(Screen.width * 0.38f, centerY), normalizedNeedle);
        DrawSingleCheck(fireCheck, new Vector2(Screen.width * 0.62f, centerY), normalizedNeedle);

        if (!showInstruction)
        {
            return;
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 185f, centerY + 88f, 370f, 24f), "Both players must hit inside the bright arc");
    }

    private void DrawSingleCheck(RuntimeCheck check, Vector2 center, float normalizedNeedle)
    {
        Color baseColor = check.required ? circleColor : inactiveCircleColor;
        Color zoneColor = check.succeeded ? completedColor : successZoneColor;
        Color needleColor = check.failed ? failedColor : Color.white;

        DrawCircle(center, CircleRadius, baseColor, 8f, 64);
        DrawArc(center, CircleRadius, check.successCenter - successWindowSize * 0.5f, successWindowSize, zoneColor, 11f, 20);
        DrawNeedle(center, 56f, normalizedNeedle, needleColor, 4f);

        GUI.color = Color.white;
        string stateText = check.required
            ? check.succeeded ? "SUCCESS" : check.failed ? "FAIL" : check.key.ToString()
            : "N/A";
        GUI.Label(new Rect(center.x - 70f, center.y - 98f, 140f, 24f), check.label + " : " + stateText);
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
