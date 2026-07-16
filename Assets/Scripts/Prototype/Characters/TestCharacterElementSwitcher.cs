using UnityEngine;

[DisallowMultipleComponent]
public class TestCharacterElementSwitcher : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerCharacter target;
    [SerializeField] private KeyCode switchKey = KeyCode.R;
    [SerializeField] private KeyCode returnToStartKey = KeyCode.Q;
    [SerializeField] private bool updatePlayerId = true;
    [SerializeField] private string waterPlayerId = "TestPlayer_Water";
    [SerializeField] private string firePlayerId = "TestPlayer_Fire";

    [Header("Solo Test")]
    [SerializeField] private bool disableOtherPlayersOnPlay = true;

    [Header("Element Side Effects")]
    [SerializeField] private bool syncDarknessLights = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLabel = true;

    private WaterCharacterLight waterLight;
    private FireLightSource fireLight;

    private void Reset()
    {
        target = GetComponent<PlayerCharacter>();
    }

    private void Awake()
    {
        ResolveTarget();
        ApplyCurrentElementSideEffects();
    }

    private void Start()
    {
        if (disableOtherPlayersOnPlay)
        {
            DisableOtherPlayerCharacters();
        }
    }

    private void Update()
    {
        ResolveTarget();
        if (target == null)
        {
            return;
        }

        if (returnToStartKey != KeyCode.None && Input.GetKeyDown(returnToStartKey))
        {
            target.ReturnToStartForTesting();
        }

        if (switchKey != KeyCode.None && Input.GetKeyDown(switchKey))
        {
            ToggleElement();
        }
    }

    public void ToggleElement()
    {
        if (target == null)
        {
            return;
        }

        ElementType nextElement = target.Element == ElementType.Water
            ? ElementType.Fire
            : ElementType.Water;

        string nextPlayerId = null;
        if (updatePlayerId)
        {
            nextPlayerId = nextElement == ElementType.Water ? waterPlayerId : firePlayerId;
        }

        target.SetElementForTesting(nextElement, nextPlayerId);
        ApplyCurrentElementSideEffects();
    }

    private void ResolveTarget()
    {
        if (target == null)
        {
            target = GetComponent<PlayerCharacter>();
        }
    }

    private void DisableOtherPlayerCharacters()
    {
        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PlayerCharacter player in players)
        {
            if (player == null || player == target)
            {
                continue;
            }

            player.gameObject.SetActive(false);
        }
    }

    private void ApplyCurrentElementSideEffects()
    {
        if (!syncDarknessLights || target == null)
        {
            return;
        }

        bool isWater = target.Element == ElementType.Water;
        bool isFire = target.Element == ElementType.Fire;

        if (isWater)
        {
            waterLight = EnsureComponent<WaterCharacterLight>();
            waterLight.enabled = true;

            fireLight = GetComponent<FireLightSource>();
            if (fireLight != null)
            {
                fireLight.enabled = false;
            }
        }
        else if (isFire)
        {
            fireLight = EnsureComponent<FireLightSource>();
            fireLight.enabled = true;

            waterLight = GetComponent<WaterCharacterLight>();
            if (waterLight != null)
            {
                waterLight.enabled = false;
            }
        }
    }

    private T EnsureComponent<T>() where T : Component
    {
        if (!TryGetComponent(out T component))
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private void OnGUI()
    {
        if (!showDebugLabel || target == null || !Application.isPlaying)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 1.45f);
        if (screenPosition.z < 0f)
        {
            return;
        }

        string label = $"TEST {target.Element}  [{switchKey}]";
        Rect rect = new Rect(screenPosition.x - 58f, Screen.height - screenPosition.y - 12f, 116f, 20f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = target.Element == ElementType.Water
            ? new Color(0.55f, 0.9f, 1f, 1f)
            : new Color(1f, 0.55f, 0.22f, 1f);
        GUI.Label(rect, label);
        GUI.color = previousColor;
    }
}
