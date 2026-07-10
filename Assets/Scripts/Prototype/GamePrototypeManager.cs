using UnityEngine;

public class GamePrototypeManager : MonoBehaviour
{
    public static GamePrototypeManager Instance { get; private set; }

    [Header("Players")]
    [SerializeField] private PlayerCharacter waterPlayer;
    [SerializeField] private PlayerCharacter firePlayer;
    [SerializeField] private float overlapDeathSeconds = 5f;

    [Header("Runtime Sprites")]
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Sprite boxSprite;

    private float playerOverlapTimer;

    public Sprite ProjectileSprite => projectileSprite;
    public Sprite BoxSprite => boxSprite;
    public PlayerCharacter WaterPlayer => waterPlayer;
    public PlayerCharacter FirePlayer => firePlayer;

    public void Configure(PlayerCharacter water, PlayerCharacter fire)
    {
        waterPlayer = water;
        firePlayer = fire;
        if (waterPlayer != null)
        {
            waterPlayer.SetPartner(firePlayer);
        }
        if (firePlayer != null)
        {
            firePlayer.SetPartner(waterPlayer);
        }
    }

    public void NotifyPlayerDied(PlayerCharacter player)
    {
        if (waterPlayer == null || firePlayer == null)
        {
            return;
        }

        if (waterPlayer.IsDeadLike && firePlayer.IsDeadLike)
        {
            ResetPlayersToStart();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        projectileSprite = projectileSprite != null ? projectileSprite : BuildRuntimeSprite(32, true);
        boxSprite = boxSprite != null ? boxSprite : BuildRuntimeSprite(16, false);

    }

    private void Start()
    {
        Configure(waterPlayer, firePlayer);
    }

    private void Update()
    {
        UpdatePlayerOverlapDeathRule();
    }

    private void UpdatePlayerOverlapDeathRule()
    {
        if (waterPlayer == null || firePlayer == null || waterPlayer.BodyCollider == null || firePlayer.BodyCollider == null)
        {
            return;
        }

        if (!waterPlayer.IsAliveLike || !firePlayer.IsAliveLike)
        {
            playerOverlapTimer = 0f;
            return;
        }

        bool overlapping = waterPlayer.BodyCollider.bounds.Intersects(firePlayer.BodyCollider.bounds);
        if (!overlapping)
        {
            playerOverlapTimer = 0f;
            return;
        }

        playerOverlapTimer += Time.deltaTime;
        if (playerOverlapTimer >= overlapDeathSeconds)
        {
            waterPlayer.Kill("Long contact with opposite element player");
            firePlayer.Kill("Long contact with opposite element player");
            playerOverlapTimer = 0f;
        }
    }

    private void ResetPlayersToStart()
    {
        playerOverlapTimer = 0f;
        waterPlayer.ResetForFullPartyDeath();
        firePlayer.ResetForFullPartyDeath();
    }

    private Sprite BuildRuntimeSprite(int size, bool circle)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool filled = !circle || Vector2.Distance(new Vector2(x, y), center) <= radius;
                texture.SetPixel(x, y, filled ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
