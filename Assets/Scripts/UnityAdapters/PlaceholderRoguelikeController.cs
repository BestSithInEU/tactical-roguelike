using System.Collections.Generic;
using System.IO;
using System.Text;
using TacticalRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalRoguelike.UnityAdapters
{
    public sealed class PlaceholderRoguelikeController : MonoBehaviour
    {
        private const int DefaultSeed = 12345;
        private const float TileSize = 1f;
        private const float HudMargin = 12f;
        private const float HudLineHeight = 18f;
        private const float HudPadding = 20f;
        private const float MinimumWorldViewportHeight = 0.62f;

        private static PlaceholderRoguelikeController activeController;

        private readonly Pathfinding debugPathfinding = new Pathfinding();
        private readonly List<SpriteRenderer> enemyRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> debugRenderers = new List<SpriteRenderer>();
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        private DungeonLayout layout;
        private RunState runState;
        private GameObject tileRoot;
        private GameObject entityRoot;
        private GameObject debugRoot;
        private Sprite floorSprite;
        private Sprite wallSprite;
        private Sprite stairsSprite;
        private Sprite playerSprite;
        private Sprite enemySprite;
        private Sprite debugPathSprite;
        private Sprite debugLastKnownSprite;
        private Sprite debugHomeSprite;
        private SpriteRenderer playerRenderer;
        private int currentSeed = DefaultSeed;
        private bool debugOverlayEnabled = true;
        private string status = "Ready.";

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

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                debugOverlayEnabled = !debugOverlayEnabled;
                ConfigureCamera();
                RefreshDebugOverlay();
                status = debugOverlayEnabled ? "Debug overlay enabled." : "Debug overlay disabled.";
                return;
            }

            if (!runState.IsOngoing)
            {
                return;
            }

            bool acted = false;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(
                    () => TurnSystem.TryMovePlayer(runState, 0, 1),
                    "Moved north."
                );
            }
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(
                    () => TurnSystem.TryMovePlayer(runState, 0, -1),
                    "Moved south."
                );
            }
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                acted = TryPlayerAction(
                    () => TurnSystem.TryMovePlayer(runState, -1, 0),
                    "Moved west."
                );
            }
            else if (
                keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
            )
            {
                acted = TryPlayerAction(
                    () => TurnSystem.TryMovePlayer(runState, 1, 0),
                    "Moved east."
                );
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
                GUI.Label(
                    new Rect(HudMargin, HudMargin, 600, 24),
                    "Roguelike placeholder is starting..."
                );
                return;
            }

            ConfigureCamera();

            int aliveEnemies = CountAliveEnemies();
            string text = string.Format(
                "Seed: {0} | Floor: {1} | Config: default DungeonGeneratorConfig | Turn: {2}\nPlayer HP: {3}/{4} | Enemies: {5}/{6} alive | Status: {7} | Debug: {8}\n{9}\nControls: WASD/Arrows move, Space/Enter wait, R restart, N new, F5 save, F9 load, F1 debug",
                currentSeed,
                runState.FloorNumber,
                runState.TurnNumber,
                runState.Player.HitPoints,
                runState.Player.MaxHitPoints,
                aliveEnemies,
                runState.Enemies.Count,
                runState.Status,
                debugOverlayEnabled ? "on" : "off",
                status
            );

            if (debugOverlayEnabled)
            {
                text += "\n\n" + BuildDebugText();
            }

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10),
            };

            GUI.Box(GetHudRect(), text, boxStyle);
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
            debugPathSprite = CreateSolidSprite(
                new Color(0.2f, 1f, 0.45f, 0.45f),
                "DebugPathSprite"
            );
            debugLastKnownSprite = CreateSolidSprite(
                new Color(1f, 0.95f, 0.1f, 0.7f),
                "DebugLastKnownSprite"
            );
            debugHomeSprite = CreateSolidSprite(
                new Color(0.65f, 0.35f, 1f, 0.45f),
                "DebugHomeSprite"
            );
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

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.5f),
                16f
            );
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

            if (debugRoot != null)
            {
                Destroy(debugRoot);
            }

            enemyRenderers.Clear();
            debugRenderers.Clear();
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
                SpriteRenderer enemyRenderer = CreateEntityRenderer(
                    "Enemy " + i,
                    enemySprite,
                    9,
                    0.66f
                );
                enemyRenderers.Add(enemyRenderer);
            }
        }

        private SpriteRenderer CreateEntityRenderer(
            string objectName,
            Sprite sprite,
            int sortingOrder,
            float scale
        )
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

            RefreshDebugOverlay();
        }

        private void RefreshDebugOverlay()
        {
            if (debugRoot != null)
            {
                Destroy(debugRoot);
            }

            debugRenderers.Clear();

            if (!debugOverlayEnabled || runState == null)
            {
                return;
            }

            debugRoot = new GameObject("Debug Overlay");
            debugRoot.transform.SetParent(transform, false);

            for (int i = 0; i < runState.Enemies.Count; i++)
            {
                EntityState enemy = runState.Enemies[i];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                CreateDebugMarker(
                    "Enemy " + i + " Home",
                    enemy.HomePosition,
                    debugHomeSprite,
                    2,
                    0.45f
                );

                GridPosition? target = GetDebugPathTarget(enemy);
                if (target.HasValue)
                {
                    IReadOnlyList<GridPosition> path = debugPathfinding.FindPath(
                        runState.Grid,
                        enemy.Position,
                        target.Value
                    );
                    for (int step = 1; step < path.Count; step++)
                    {
                        CreateDebugMarker(
                            "Enemy " + i + " Path " + step,
                            path[step],
                            debugPathSprite,
                            3,
                            0.34f
                        );
                    }
                }

                if (enemy.LastKnownPlayerPosition.HasValue)
                {
                    CreateDebugMarker(
                        "Enemy " + i + " Last Known Player",
                        enemy.LastKnownPlayerPosition.Value,
                        debugLastKnownSprite,
                        4,
                        0.55f
                    );
                }
            }
        }

        private void CreateDebugMarker(
            string objectName,
            GridPosition position,
            Sprite sprite,
            int sortingOrder,
            float scale
        )
        {
            var marker = new GameObject(objectName);
            marker.transform.SetParent(debugRoot.transform, false);
            marker.transform.position = ToWorld(position, -0.05f);
            marker.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            debugRenderers.Add(renderer);
        }

        private string BuildDebugText()
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "Debug overlay: green=enemy path, yellow=last known player, purple=home."
            );

            for (int i = 0; i < runState.Enemies.Count; i++)
            {
                EntityState enemy = runState.Enemies[i];
                GridPosition? target = GetDebugPathTarget(enemy);
                IReadOnlyList<GridPosition> path =
                    enemy.IsAlive && target.HasValue
                        ? debugPathfinding.FindPath(runState.Grid, enemy.Position, target.Value)
                        : new List<GridPosition>();

                builder.Append("Enemy ");
                builder.Append(i);
                builder.Append(": ");
                builder.Append(GetEnemyDebugState(enemy));
                builder.Append(" pos=");
                builder.Append(enemy.Position);
                builder.Append(" hp=");
                builder.Append(enemy.HitPoints);
                builder.Append('/');
                builder.Append(enemy.MaxHitPoints);
                builder.Append(" lkp=");
                builder.Append(
                    enemy.LastKnownPlayerPosition.HasValue
                        ? enemy.LastKnownPlayerPosition.Value.ToString()
                        : "none"
                );
                builder.Append(" pathSteps=");
                builder.Append(path.Count > 0 ? path.Count - 1 : 0);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private GridPosition? GetDebugPathTarget(EntityState enemy)
        {
            if (!enemy.IsAlive)
            {
                return null;
            }

            if (enemy.IsReturningHome)
            {
                return enemy.HomePosition;
            }

            if (enemy.LastKnownPlayerPosition.HasValue)
            {
                return enemy.LastKnownPlayerPosition.Value;
            }

            return enemy.IsAlerted ? runState.Player.Position : (GridPosition?)null;
        }

        private static string GetEnemyDebugState(EntityState enemy)
        {
            if (!enemy.IsAlive)
            {
                return "Dead";
            }

            if (enemy.IsReturningHome)
            {
                return "Returning home";
            }

            if (enemy.LastKnownPlayerPosition.HasValue && enemy.SearchTurnsRemaining > 0)
            {
                return "Searching";
            }

            if (enemy.IsAlerted)
            {
                return "Chasing";
            }

            return "Patrolling";
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
            Rect viewport = GetWorldViewportRect();
            camera.rect = viewport;
            camera.transform.position = new Vector3(
                (runState.Grid.Width - 1) * 0.5f,
                (runState.Grid.Height - 1) * 0.5f,
                -10f
            );
            float viewportAspect = CalculateViewportAspect(viewport);
            camera.orthographicSize = Mathf.Max(
                runState.Grid.Height * 0.55f,
                (runState.Grid.Width * 0.5f / viewportAspect) * 1.08f,
                6f
            );
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        private Rect GetHudRect()
        {
            return new Rect(
                HudMargin,
                HudMargin,
                Mathf.Max(320f, Screen.width - HudMargin * 2f),
                GetHudHeight()
            );
        }

        private float GetHudHeight()
        {
            int lineCount = 4;
            if (debugOverlayEnabled && runState != null)
            {
                lineCount += 2 + runState.Enemies.Count;
            }

            return Mathf.Min(Screen.height * 0.34f, HudPadding + lineCount * HudLineHeight);
        }

        private Rect GetWorldViewportRect()
        {
            float screenHeight = Mathf.Max(1f, Screen.height);
            float reservedTop = (GetHudHeight() + HudMargin * 2f) / screenHeight;
            float viewportHeight = Mathf.Clamp(1f - reservedTop, MinimumWorldViewportHeight, 1f);
            return new Rect(0f, 0f, 1f, viewportHeight);
        }

        private static float CalculateViewportAspect(Rect viewport)
        {
            float viewportPixelWidth = Mathf.Max(1f, Screen.width * viewport.width);
            float viewportPixelHeight = Mathf.Max(1f, Screen.height * viewport.height);
            return viewportPixelWidth / viewportPixelHeight;
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

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "tactical-roguelike-save.json");
    }
}
