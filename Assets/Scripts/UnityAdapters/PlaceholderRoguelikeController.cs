using System.Collections.Generic;
using System.IO;
using TacticalRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalRoguelike.UnityAdapters
{
    public sealed class PlaceholderRoguelikeController : MonoBehaviour
    {
        private const int DefaultSeed = 12345;
        private const float TileSize = 1f;

        private static PlaceholderRoguelikeController activeController;

        private readonly List<SpriteRenderer> enemyRenderers = new List<SpriteRenderer>();
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        private DungeonLayout layout;
        private RunState runState;
        private GameObject tileRoot;
        private GameObject entityRoot;
        private Sprite floorSprite;
        private Sprite wallSprite;
        private Sprite stairsSprite;
        private Sprite playerSprite;
        private Sprite enemySprite;
        private SpriteRenderer playerRenderer;
        private int currentSeed = DefaultSeed;
        private string status = "Ready.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (FindAnyObjectByType<PlaceholderRoguelikeController>() != null)
            {
                return;
            }

            var controllerObject = new GameObject(nameof(PlaceholderRoguelikeController));
            controllerObject.AddComponent<PlaceholderRoguelikeController>();
        }

        private void Awake()
        {
            if (activeController != null && activeController != this)
            {
                Destroy(gameObject);
                return;
            }

            activeController = this;
            CreateSprites();
        }

        private void Start()
        {
            StartRun(currentSeed);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || runState == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                StartRun(currentSeed);
                status = "Restarted same seed.";
                return;
            }

            if (keyboard.nKey.wasPressedThisFrame)
            {
                StartRun(Random.Range(1, int.MaxValue));
                status = "Started new seed.";
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                SaveRun();
                return;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                LoadRun();
                return;
            }

            if (!runState.IsOngoing)
            {
                return;
            }

            bool acted = false;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(() => TurnSystem.TryMovePlayer(runState, 0, 1), "Moved north.");
            }
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(() => TurnSystem.TryMovePlayer(runState, 0, -1), "Moved south.");
            }
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(() => TurnSystem.TryMovePlayer(runState, -1, 0), "Moved west.");
            }
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(() => TurnSystem.TryMovePlayer(runState, 1, 0), "Moved east.");
            }
            else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(() => TurnSystem.WaitPlayerTurn(runState), "Waited.");
            }

            if (acted)
            {
                RefreshEntities();
            }
        }

        private void OnGUI()
        {
            if (runState == null)
            {
                GUI.Label(new Rect(10, 10, 600, 24), "Roguelike placeholder is starting...");
                return;
            }

            int aliveEnemies = CountAliveEnemies();
            string text = string.Format(
                "Seed: {0} | Config: default DungeonGeneratorConfig | Turn: {1}\nPlayer HP: {2}/{3} | Enemies: {4}/{5} alive | Status: {6}\n{7}\nControls: WASD/Arrows move, Space/Enter wait, R restart, N new, F5 save, F9 load",
                currentSeed,
                runState.TurnNumber,
                runState.Player.HitPoints,
                runState.Player.MaxHitPoints,
                aliveEnemies,
                runState.Enemies.Count,
                runState.Status,
                status);

            GUI.Box(new Rect(10, 10, 760, 96), text);
        }

        private void OnDestroy()
        {
            if (activeController == this)
            {
                activeController = null;
            }

            for (int i = 0; i < generatedSprites.Count; i++)
            {
                Destroy(generatedSprites[i]);
            }

            for (int i = 0; i < generatedTextures.Count; i++)
            {
                Destroy(generatedTextures[i]);
            }
        }

        private void StartRun(int seed)
        {
            currentSeed = seed;
            layout = new DungeonGenerator().Generate(currentSeed);
            runState = new RunState(layout);
            ClearWorld();
            RenderDungeon();
            CreateEntityRenderers();
            RefreshEntities();
            ConfigureCamera();
            status = string.Format("Started seed {0}.", currentSeed);
        }

        private void SaveRun()
        {
            string json = JsonUtility.ToJson(SaveGame.Capture(runState), true);
            File.WriteAllText(SavePath, json);
            status = "Saved run to " + SavePath;
        }

        private void LoadRun()
        {
            if (!File.Exists(SavePath))
            {
                status = "No save found at " + SavePath;
                return;
            }

            string json = File.ReadAllText(SavePath);
            SaveGame save = JsonUtility.FromJson<SaveGame>(json);
            runState = save.Restore();
            currentSeed = runState.Seed;
            layout = null;
            ClearWorld();
            RenderDungeon();
            CreateEntityRenderers();
            RefreshEntities();
            ConfigureCamera();
            status = "Loaded saved run.";
        }

        private bool TryPlayerAction(System.Func<bool> action, string successMessage)
        {
            bool succeeded = action();
            status = succeeded ? successMessage : "Blocked.";

            if (runState.Status == RunStatus.Lost)
            {
                status = "You died. Press R to retry this seed or N for a new seed.";
            }
            else if (runState.Status == RunStatus.Won)
            {
                status = "You reached the stairs after clearing enemies. Press N for a new seed.";
            }

            return succeeded;
        }

        private void CreateSprites()
        {
            floorSprite = CreateSolidSprite(new Color(0.18f, 0.18f, 0.2f, 1f), "FloorSprite");
            wallSprite = CreateSolidSprite(new Color(0.04f, 0.04f, 0.05f, 1f), "WallSprite");
            stairsSprite = CreateSolidSprite(new Color(0.95f, 0.75f, 0.25f, 1f), "StairsSprite");
            playerSprite = CreateSolidSprite(new Color(0.2f, 0.65f, 1f, 1f), "PlayerSprite");
            enemySprite = CreateSolidSprite(new Color(1f, 0.22f, 0.18f, 1f), "EnemySprite");
        }

        private Sprite CreateSolidSprite(Color color, string spriteName)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.name = spriteName + "Texture";
            generatedTextures.Add(texture);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = spriteName;
            generatedSprites.Add(sprite);
            return sprite;
        }

        private void ClearWorld()
        {
            if (tileRoot != null)
            {
                Destroy(tileRoot);
            }

            if (entityRoot != null)
            {
                Destroy(entityRoot);
            }

            enemyRenderers.Clear();
            playerRenderer = null;
        }

        private void RenderDungeon()
        {
            tileRoot = new GameObject("Placeholder Dungeon Tiles");
            tileRoot.transform.SetParent(transform, false);

            GameGrid grid = runState.Grid;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    GridTileKind tileKind = grid.GetTile(position);
                    GameObject tile = new GameObject(tileKind.ToString());
                    tile.transform.SetParent(tileRoot.transform, false);
                    tile.transform.position = ToWorld(position, 0f);

                    SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = SpriteForTile(tileKind);
                    renderer.sortingOrder = 0;
                }
            }
        }

        private Sprite SpriteForTile(GridTileKind tileKind)
        {
            switch (tileKind)
            {
                case GridTileKind.Floor:
                    return floorSprite;
                case GridTileKind.StairsDown:
                    return stairsSprite;
                default:
                    return wallSprite;
            }
        }

        private void CreateEntityRenderers()
        {
            entityRoot = new GameObject("Placeholder Entities");
            entityRoot.transform.SetParent(transform, false);

            playerRenderer = CreateEntityRenderer("Player", playerSprite, 10, 0.72f);

            for (int i = 0; i < runState.Enemies.Count; i++)
            {
                SpriteRenderer enemyRenderer = CreateEntityRenderer("Enemy " + i, enemySprite, 9, 0.66f);
                enemyRenderers.Add(enemyRenderer);
            }
        }

        private SpriteRenderer CreateEntityRenderer(string objectName, Sprite sprite, int sortingOrder, float scale)
        {
            var entity = new GameObject(objectName);
            entity.transform.SetParent(entityRoot.transform, false);
            entity.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = entity.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void RefreshEntities()
        {
            playerRenderer.transform.position = ToWorld(runState.Player.Position, -0.1f);
            playerRenderer.gameObject.SetActive(runState.Player.IsAlive);

            for (int i = 0; i < runState.Enemies.Count; i++)
            {
                EntityState enemy = runState.Enemies[i];
                SpriteRenderer renderer = enemyRenderers[i];
                renderer.transform.position = ToWorld(enemy.Position, -0.1f);
                renderer.gameObject.SetActive(enemy.IsAlive);
            }
        }

        private void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.transform.position = new Vector3((runState.Grid.Width - 1) * 0.5f, (runState.Grid.Height - 1) * 0.5f, -10f);
            camera.orthographicSize = Mathf.Max(runState.Grid.Height * 0.55f, runState.Grid.Width * 0.32f, 6f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        private Vector3 ToWorld(GridPosition position, float z)
        {
            return new Vector3(position.X * TileSize, position.Y * TileSize, z);
        }

        private int CountAliveEnemies()
        {
            int alive = 0;
            for (int i = 0; i < runState.Enemies.Count; i++)
            {
                if (runState.Enemies[i].IsAlive)
                {
                    alive++;
                }
            }

            return alive;
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "tactical-roguelike-save.json");
    }
}
