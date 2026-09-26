#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 게임 씬에 실제로 걸어다닐 수 있는 타일맵을 깐다.
///
/// TilemapLab과의 차이 —
///   TilemapLab : 측정 전용. 플레이어가 없고, 벽도 충돌하지 않는다.
///   GameMapBuilder : 2주차 게임 위에 깐다. 벽에 콜라이더가 붙어 실제로 막힌다.
///
/// 맵 크기를 80×80으로 잡은 것은 의도적이다. 측정 실습(02_측정기록.md)과 같은 규모라
/// "아까 잰 6,400타일이 바로 이 맵이다"라고 이어서 설명할 수 있다.
///
/// 콜라이더 구성 — TilemapCollider2D + CompositeCollider2D
///   TilemapCollider2D만 쓰면 벽 타일마다 박스 콜라이더가 하나씩 생긴다.
///   Composite로 병합하면 인접한 벽들이 하나의 외곽선으로 합쳐진다.
///   렌더링의 배칭과 정확히 같은 발상이라, 4주차 수업에서 곁들여 언급하기 좋다.
/// </summary>
public static class GameMapBuilder
{
    const string TILES = "Assets/CG2Week4/Tiles";
    const string RULE_TILE = TILES + "/wall_rule.asset";
    const string ROOT_NAME = "CG2 GameMap";
    const string BOUNDS_NAME = "CG2 CameraBounds";

    const int BORDER = 2;             // 바깥 벽 두께 (타일)
    const float CELL = 0.5f;          // 타일 32px @ PPU 64
    const int SEED = 20260401;

    // 카메라 시야는 orthographicSize 7 · 16:9 기준 약 24.9 × 14유닛 = 50 × 28타일이다.
    //
    // 맵은 이보다 반드시 커야 한다. 작으면 Confiner2D가 "카메라를 경계 안에 넣는" 문제를
    // 풀 수 없어(해가 존재하지 않는다) 카메라가 어디로 갈지 보장되지 않는다.
    // 처음에 48로 잡았다가 ±12유닛 < 카메라 반폭 12.4유닛이 되어 퇴화 상태에 빠졌다.
    // 그래서 최소값을 56(±14유닛)으로 올렸다.
    [MenuItem("CG2 Lab/4주차 준비/7. 게임 맵 — 작게 (56×56)", priority = 7)]
    public static void BuildSmall() => BuildGameMap(56);

    [MenuItem("CG2 Lab/4주차 준비/7. 게임 맵 — 보통 (60×60, 권장)", priority = 8)]
    public static void BuildMedium() => BuildGameMap(60);

    [MenuItem("CG2 Lab/4주차 준비/7. 게임 맵 — 크게 (80×80, 측정 실습과 동일)", priority = 9)]
    public static void BuildLarge() => BuildGameMap(80);

    static void BuildGameMap(int MAP_SIZE)
    {
        var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RULE_TILE);
        var grounds = LoadGroundTiles();
        if (rule == null || grounds.Count == 0)
        {
            EditorUtility.DisplayDialog("게임 맵 생성",
                "타일 에셋이 없습니다.\n먼저 'CG2 Lab → 4주차 준비 → 4. 타일 에셋 생성'을 실행하세요.", "확인");
            return;
        }

        // Sorting Layer가 없거나 순서가 틀리면 타일맵이 캐릭터를 덮는다.
        // 사용자가 메뉴를 순서대로 눌렀는지에 기대지 않고 여기서 직접 보장한다.
        if (SortingLayer.NameToID("Ground") == 0 || SortingLayer.NameToID("Wall") == 0)
        {
            Debug.Log("[CG2] Sorting Layer가 없어 먼저 등록합니다.");
            CG2ProjectSetup.EnsureSortingLayers();
        }

        // 타일맵은 Ground·Wall 레이어에 올라간다. 씬의 Light2D가 그 레이어를 안 비추면
        // 타일이 통째로 검게 그려진다. 맵을 까는 시점에 한 번 더 보장한다.
        CG2Diagnostics.RepairAllLight2DTargetLayers();

        var old = GameObject.Find(ROOT_NAME);
        if (old != null) Undo.DestroyObjectImmediate(old);

        // ── 벽 배치 결정 ───────────────────────────────────────────
        var walls = BuildWallLayout(MAP_SIZE);

        // ── 씬 구성 ────────────────────────────────────────────────
        var root = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(root, "CG2 게임 맵");
        var grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(CELL, CELL, 0f);

        int half = MAP_SIZE / 2;
        var bounds = new BoundsInt(-half, -half, 0, MAP_SIZE, MAP_SIZE, 1);

        var groundMap = MakeLayer(root.transform, "Tilemap_Ground", "Ground", 0);
        var groundTiles = new TileBase[MAP_SIZE * MAP_SIZE];
        var rng = new System.Random(SEED);
        for (int i = 0; i < groundTiles.Length; i++) groundTiles[i] = grounds[rng.Next(grounds.Count)];
        groundMap.SetTilesBlock(bounds, groundTiles);

        var wallMap = MakeLayer(root.transform, "Tilemap_Wall", "Wall", 0);
        foreach (var w in walls) wallMap.SetTile(new Vector3Int(w.x, w.y, 0), rule);

        int colliderCount = AddWallColliders(wallMap.gameObject);
        float halfExtent = UpdateCameraBounds(MAP_SIZE);

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log(
            $"[검증] 게임 맵 생성 완료 — {MAP_SIZE}×{MAP_SIZE} = {MAP_SIZE * MAP_SIZE:N0}타일\n" +
            $"        바닥 {groundTiles.Length:N0}칸 · 벽 {walls.Count:N0}칸 · " +
            $"월드 크기 ±{MAP_SIZE * CELL * 0.5f:F1}유닛\n" +
            $"        Grid.cellSize = {grid.cellSize} (0.5여야 타일 사이가 안 벌어진다)\n" +
            $"        벽 콜라이더 : {(colliderCount > 0 ? $"CompositeCollider2D 경로 {colliderCount}개로 병합" : "Composite 실패 — TilemapCollider2D 단독")}\n" +
            $"        카메라 경계 : {(halfExtent > 0f ? $"±{halfExtent:F1}유닛으로 갱신" : "Cinemachine 경계 없음 (건너뜀)")}\n" +
            $"        {DiagnosePlayerVisibility()}\n" +
            "        ── Play 해서 확인 ──\n" +
            "        ① 벽에 부딪히면 막히는가\n" +
            "        ② 맵 끝으로 가면 카메라가 멈추는가");
    }

    [MenuItem("CG2 Lab/4주차 준비/되돌리기 — 게임 씬 타일맵 제거", priority = 44)]
    public static void RemoveGameMap()
    {
        var old = GameObject.Find(ROOT_NAME);
        if (old == null) { Debug.Log("[CG2] 씬에 게임 맵이 없습니다."); return; }
        Undo.DestroyObjectImmediate(old);
        Debug.Log("[CG2] 게임 맵을 제거했습니다. 카메라 경계는 그대로 두었습니다.");
    }

    // ────────────────────────────────────────────────────────────── 맵 설계

    /// <summary>
    /// 바깥 테두리 벽 + 안쪽 기둥 블록.
    ///
    /// 기둥은 10타일 간격 격자 위에 흩어 놓는다. 그래야 통로가 최소 4타일(2유닛) 확보된다.
    /// 플레이어 콜라이더 지름이 0.9유닛이므로 여유 있게 지나갈 수 있다.
    /// 원점 주변은 비워둔다 — 시작하자마자 벽에 끼면 안 된다.
    /// </summary>
    static HashSet<Vector2Int> BuildWallLayout(int MAP_SIZE)
    {
        const int SPAWN_SAFE_RADIUS = 8;   // 원점 주변은 비워둔다 (플레이어 시작 지점)
        var walls = new HashSet<Vector2Int>();
        int half = MAP_SIZE / 2;
        var rng = new System.Random(SEED);

        // 바깥 테두리
        for (int x = -half; x < half; x++)
            for (int t = 0; t < BORDER; t++)
            {
                walls.Add(new Vector2Int(x, -half + t));
                walls.Add(new Vector2Int(x, half - 1 - t));
            }
        for (int y = -half; y < half; y++)
            for (int t = 0; t < BORDER; t++)
            {
                walls.Add(new Vector2Int(-half + t, y));
                walls.Add(new Vector2Int(half - 1 - t, y));
            }

        // 안쪽 기둥
        const int STEP = 10;
        for (int cy = -half + STEP; cy < half - STEP; cy += STEP)
            for (int cx = -half + STEP; cx < half - STEP; cx += STEP)
            {
                if (rng.Next(100) >= 55) continue;             // 55% 확률로만 배치

                int w = 2 + rng.Next(3);                        // 2~4
                int h = 2 + rng.Next(3);
                int ox = cx + rng.Next(4);
                int oy = cy + rng.Next(4);

                // 시작 지점 주변은 건드리지 않는다.
                if (Mathf.Abs(ox) < SPAWN_SAFE_RADIUS && Mathf.Abs(oy) < SPAWN_SAFE_RADIUS) continue;

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        walls.Add(new Vector2Int(ox + x, oy + y));
            }

        return walls;
    }

    // ────────────────────────────────────────────────────────────── 씬 구성

    static Tilemap MakeLayer(Transform parent, string name, string sortingLayer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        // Sorting Layer가 등록돼 있을 때만 지정한다. 없으면 Default로 남겨 에러를 피한다.
        if (SortingLayer.NameToID(sortingLayer) != 0) r.sortingLayerName = sortingLayer;
        r.sortingOrder = order;
        return map;
    }

    /// <summary>
    /// 벽에 물리 콜라이더를 붙인다.
    ///
    /// TilemapCollider2D 단독이면 벽 타일마다 박스가 하나씩 생긴다.
    /// CompositeCollider2D로 병합하면 인접한 벽이 하나의 외곽선 경로로 합쳐진다.
    /// 렌더링에서 청크가 타일을 하나의 메시로 합치는 것과 같은 발상이다.
    /// </summary>
    static int AddWallColliders(GameObject wallGo)
    {
        var tc = wallGo.AddComponent<TilemapCollider2D>();

        var rb = wallGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;      // 벽은 움직이지 않는다

        var composite = wallGo.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        // Unity 6에서 usedByComposite는 compositeOperation으로 대체되었다.
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;

        // 에디터에서는 즉시 갱신되지 않을 수 있어 강제로 한 번 돌린다.
        composite.GenerateGeometry();
        return composite.pathCount;
    }

    /// <summary>
    /// Cinemachine Confiner가 쓰는 경계를 맵 크기에 맞춘다.
    ///
    /// CG2.Camera 어셈블리를 참조하지 않는다(autoReferenced = false).
    /// 이름으로 찾아 PolygonCollider2D만 건드리므로 Cinemachine이 없어도 안전하다.
    /// </summary>
    static float UpdateCameraBounds(int MAP_SIZE)
    {
        var go = GameObject.Find(BOUNDS_NAME);
        if (go == null) return 0f;

        var poly = go.GetComponent<PolygonCollider2D>();
        if (poly == null) return 0f;

        float h = MAP_SIZE * CELL * 0.5f;          // 60타일 × 0.5유닛 ÷ 2 = ±15유닛

        // 경계가 카메라 시야보다 작으면 Confiner가 풀 수 없는 문제가 된다.
        // 그 경우 맵보다 경계를 넓혀서라도 퇴화 상태만은 피한다(맵 밖이 조금 보이는 편이 낫다).
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float needW = cam.orthographicSize * cam.aspect + 0.5f;
            float needH = cam.orthographicSize + 0.5f;
            float need = Mathf.Max(needW, needH);
            if (h < need)
            {
                Debug.LogWarning($"[CG2] 맵(±{h:F1}유닛)이 카메라 시야(±{needW:F1})보다 작아 " +
                                 $"경계를 ±{need:F1}유닛으로 넓혔습니다. 맵 바깥이 조금 보일 수 있습니다.\n" +
                                 "        더 큰 맵을 쓰거나 카메라 orthographicSize를 줄이세요.");
                h = need;
            }
        }

        Undo.RecordObject(poly, "CG2 카메라 경계 갱신");
        poly.points = new[]
        {
            new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h),
        };
        EditorUtility.SetDirty(poly);
        return h;
    }

    /// <summary>
    /// 플레이어가 타일맵보다 위에 그려지는지 지금 확인해서 알려준다.
    ///
    /// 이건 Play해봐야 아는 문제가 아니라 지금 계산할 수 있는 문제다.
    /// Sorting Layer 목록에서 뒤에 있을수록 위에 그려지므로 인덱스만 비교하면 된다.
    /// 실제로 이 순서를 잘못 잡아 캐릭터가 통째로 사라진 적이 있어 진단을 넣어둔다.
    /// </summary>
    static string DiagnosePlayerVisibility()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return "플레이어 : 씬에 없음 (Player 태그 확인)";

        var sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) return "플레이어 : SpriteRenderer 없음";

        int playerIdx = LayerIndex(sr.sortingLayerName);
        int wallIdx = LayerIndex("Wall");

        return playerIdx > wallIdx
            ? $"플레이어 : '{sr.sortingLayerName}' 레이어 — 타일맵보다 위 ✓"
            : $"플레이어 : '{sr.sortingLayerName}' 레이어 — ⚠ 타일맵에 가려집니다. " +
              "'3. 기존 오브젝트를 Entity·FX 레이어로 이동'을 실행하세요";
    }

    static int LayerIndex(string name)
    {
        var layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++) if (layers[i].name == name) return i;
        return -1;
    }

    static List<TileBase> LoadGroundTiles()
    {
        var list = new List<TileBase>();
        if (!AssetDatabase.IsValidFolder(TILES)) return list;

        foreach (var guid in AssetDatabase.FindAssets("t:TileBase", new[] { TILES }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile != null && tile.name.StartsWith("ground")) list.Add(tile);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }
}
#endif
