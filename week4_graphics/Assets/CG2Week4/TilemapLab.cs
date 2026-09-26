using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 4주차 타일맵 렌더링 최적화 실습 — 3주차 DrawCallLab과 같은 방식으로 조작한다.
///
/// 3주차에서 배운 것: "개체 수가 같아도 텍스처가 흩어지면 Batches가 폭증한다."
/// 4주차에서 볼 것: "타일맵은 수천 개 타일을 어떻게 소수 드로우콜로 그리는가,
///                   그리고 그 구조를 깨뜨리면 어떻게 되는가."
///
/// 관찰 순서 (권장)
///   1) Chunk 모드 · 100×100  → Batches가 한 자릿수인 것을 확인. 이게 정상이다.
///   2) Individual 모드로 전환 → Batches가 타일 수만큼 늘어난다. 청크 병합의 가치.
///   3) 텍스처를 Mixed로     → 청크 안에서도 텍스처가 갈리면 다시 쪼개진다. (3주차 회수)
///   4) Sprite Atlas 적용    → Mixed인데도 다시 합쳐진다. (3주차 회수)
///
/// 맵 생성과 타일 배정에 고정 시드를 쓰므로 누가 실행해도 같은 수치가 나온다.
/// 사용법: Inspector에서 값을 정하고 Play → Game View 우상단 Stats 확인 → Stop → 값 변경.
/// </summary>
public class TilemapLab : MonoBehaviour
{
    public enum TextureSpread
    {
        /// <summary>바닥 시트 1장만 사용 — 텍스처 1종</summary>
        Single,
        /// <summary>바닥·벽·장식·위험 4장을 섞어 사용 — 텍스처 4종</summary>
        Mixed,
    }

    [Header("맵 규모")]
    [Tooltip("가로/세로 타일 수. 100이면 10,000타일이다. " +
             "Individual 모드에서 200을 넣으면 40,000 드로우콜이 되어 에디터가 멈출 수 있다.")]
    [SerializeField] int mapSize = 100;

    [Header("측정 조건")]
    [Tooltip("Chunk = 인접 타일을 하나의 메시로 병합해 그린다(기본값). " +
             "Individual = 타일마다 개별로 그린다. 이 실습의 핵심 변수다.")]
    [SerializeField] TilemapRenderer.Mode renderMode = TilemapRenderer.Mode.Chunk;

    [Tooltip("Single = 타일이 전부 같은 텍스처 / Mixed = 4장으로 흩어짐. " +
             "3주차 Sprite Atlas 실습의 회수 포인트다.")]
    [SerializeField] TextureSpread textureSpread = TextureSpread.Single;

    [Tooltip("청크 컬링 경계를 자동 산출한다. 끄면 수동 경계를 쓰며, " +
             "경계가 실제보다 크면 화면 밖 청크까지 그려 낭비가 생긴다.")]
    [SerializeField] bool autoChunkCulling = true;

    [Tooltip("autoChunkCulling이 꺼졌을 때 쓰는 수동 경계. 일부러 크게 잡아 " +
             "컬링이 안 먹는 상황을 재현해볼 수 있다.")]
    [SerializeField] Vector3 manualCullingBounds = new(4f, 4f, 4f);

    [Header("비교용 — 타일맵을 쓰지 않았다면")]
    [Tooltip("켜면 타일맵 대신 SpriteRenderer를 타일 수만큼 생성한다. " +
             "타일맵이 없을 때 얼마나 나빠지는지 보여주는 대조군이다. " +
             "⚠ mapSize 100이면 10,000개 GameObject가 생긴다. 50 이하를 권장한다.")]
    [SerializeField] bool useSpriteRenderersInstead = false;

    [Header("배치")]
    [SerializeField] int randomSeed = 20260401;
    [Tooltip("비어 있으면 Play 시 아래 폴더에서 자동으로 수집한다.")]
    [SerializeField] string tileFolder = "Assets/CG2Week4/Tiles";
    [SerializeField] TileBase[] groundTiles = new TileBase[0];
    [SerializeField] TileBase[] accentTiles = new TileBase[0];

    [Header("화면 표시")]
    [Tooltip("켜면 조건이 화면에 표시되지만, 오버레이 자체가 드로우콜을 더한다. " +
             "정밀 측정 시에는 꺼둘 것. 조건과 측정값은 Console에도 한 줄로 출력된다.")]
    [SerializeField] bool showOverlay = false;

    Tilemap _tilemap;
    TilemapRenderer _renderer;
    int _tileCount;
    int _distinctTextures;
    float _fps;
#if UNITY_EDITOR
    int _frame;
#endif

    void Start()
    {
        if (groundTiles.Length == 0 || accentTiles.Length == 0) CollectFromFolder();
        if (groundTiles.Length == 0)
        {
            Debug.LogError($"[TilemapLab] 타일 에셋을 찾지 못했습니다: {tileFolder}\n" +
                           "먼저 'CG2 Lab → 4주차 준비 → 4. 타일 에셋 생성' 을 실행하세요.");
            return;
        }

        if (useSpriteRenderersInstead) BuildWithSpriteRenderers();
        else BuildTilemap();

        FitCamera();
    }

    void Update()
    {
        _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        // 수치가 안정된 뒤 한 번만 기록한다. 이 줄을 그대로 보고서에 옮겨 적으면 된다.
        if (_frame++ != 120) return;
        Debug.Log(
            $"[측정] {mapSize}×{mapSize} = {_tileCount:N0}타일 · " +
            $"{(useSpriteRenderersInstead ? "SpriteRenderer(대조군)" : renderMode.ToString())} · " +
            $"텍스처 {textureSpread} · 청크컬링 {(autoChunkCulling ? "Auto" : "Manual")}\n" +
            $"        DrawCalls {UnityStats.drawCalls} · " +
            $"SetPass {UnityStats.setPassCalls} · " +
            $"Tris {UnityStats.triangles:N0} · " +
            $"FPS {_fps:F1}\n" +
            $"        타일이 참조하는 텍스처 {_distinctTextures}종" +
            (textureSpread == TextureSpread.Mixed
                ? "   ← Sprite Atlas를 적용하면 1종으로 떨어진다"
                : ""));
#endif
    }

    // ────────────────────────────────────────────────────────────── 생성

    void BuildTilemap()
    {
        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(transform, false);
        var grid = gridGo.AddComponent<Grid>();
        // 타일 32px @ PPU 64 = 0.5유닛. 기본값 1로 두면 타일 사이가 벌어진다.
        grid.cellSize = new Vector3(0.5f, 0.5f, 0f);

        var mapGo = new GameObject("Tilemap_Ground");
        mapGo.transform.SetParent(gridGo.transform, false);
        _tilemap = mapGo.AddComponent<Tilemap>();
        _renderer = mapGo.AddComponent<TilemapRenderer>();

        _renderer.mode = renderMode;
        _renderer.detectChunkCullingBounds = autoChunkCulling
            ? TilemapRenderer.DetectChunkCullingBounds.Auto
            : TilemapRenderer.DetectChunkCullingBounds.Manual;
        if (!autoChunkCulling) _renderer.chunkCullingBounds = manualCullingBounds;

        if (SortingLayer.NameToID("Ground") != 0) _renderer.sortingLayerName = "Ground";

        // SetTilesBlock으로 한 번에 채운다. SetTile을 10,000번 부르면 눈에 띄게 느리다.
        int half = mapSize / 2;
        var bounds = new BoundsInt(-half, -half, 0, mapSize, mapSize, 1);
        var tiles = new TileBase[mapSize * mapSize];
        var rng = new System.Random(randomSeed);
        var used = new HashSet<Texture>();

        for (int i = 0; i < tiles.Length; i++)
        {
            TileBase t;
            if (textureSpread == TextureSpread.Mixed && rng.Next(100) < 25)
                t = accentTiles[rng.Next(accentTiles.Length)];   // 다른 시트에서 뽑는다
            else
                t = groundTiles[rng.Next(groundTiles.Length)];

            tiles[i] = t;
            TrackTexture(t, used);
        }

        _tilemap.SetTilesBlock(bounds, tiles);
        _tileCount = tiles.Length;
        _distinctTextures = used.Count;

        // 요청값이 아니라 컴포넌트에서 되읽은 값을 찍는다. 여기가 다르면 적용 실패다.
        Debug.Log($"[검증] TilemapRenderer.mode = {_renderer.mode} (요청 {renderMode}) · " +
                  $"detectChunkCullingBounds = {_renderer.detectChunkCullingBounds} · " +
                  $"Grid.cellSize = {grid.cellSize} · " +
                  $"실제 배치된 타일 {_tilemap.GetUsedTilesCount()}종");
    }

    void BuildWithSpriteRenderers()
    {
        // 대조군 — 타일맵을 안 썼다면 어떻게 되는가.
        var root = new GameObject("SpriteGrid");
        root.transform.SetParent(transform, false);

        var rng = new System.Random(randomSeed);
        var used = new HashSet<Texture>();
        int half = mapSize / 2;

        for (int y = 0; y < mapSize; y++)
            for (int x = 0; x < mapSize; x++)
            {
                var t = textureSpread == TextureSpread.Mixed && rng.Next(100) < 25
                    ? accentTiles[rng.Next(accentTiles.Length)]
                    : groundTiles[rng.Next(groundTiles.Length)];

                var sprite = SpriteOf(t);
                if (sprite == null) continue;

                var go = new GameObject($"T_{x}_{y}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3((x - half) * 0.5f, (y - half) * 0.5f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (SortingLayer.NameToID("Ground") != 0) sr.sortingLayerName = "Ground";
                used.Add(sprite.texture);
            }

        _tileCount = mapSize * mapSize;
        _distinctTextures = used.Count;
        Debug.LogWarning($"[TilemapLab] 대조군 모드 — SpriteRenderer {_tileCount:N0}개를 생성했습니다. " +
                         "타일맵과 비교해 보세요.");
    }

    void TrackTexture(TileBase t, HashSet<Texture> into)
    {
        var s = SpriteOf(t);
        if (s != null && s.texture != null) into.Add(s.texture);
    }

    static Sprite SpriteOf(TileBase t) => t switch
    {
        Tile tile => tile.sprite,
        RuleTile rule => rule.m_DefaultSprite,
        _ => null,
    };

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        // 2주차와 같은 화각을 유지한다. 맵 전체가 아니라 게임과 같은 시야에서 재야
        // 측정값이 실제 플레이 상황을 반영한다.
        cam.orthographicSize = 7f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    void OnValidate()
    {
        mapSize = Mathf.Clamp(mapSize, 8, 300);
        if (useSpriteRenderersInstead && mapSize > 60)
            Debug.LogWarning("[TilemapLab] 대조군 모드에서 mapSize가 60을 넘으면 " +
                             "GameObject가 3,600개 이상 생성되어 에디터가 크게 느려집니다.");
        if (renderMode == TilemapRenderer.Mode.Individual && mapSize > 120)
            Debug.LogWarning("[TilemapLab] Individual 모드에서 mapSize가 120을 넘으면 " +
                             "드로우콜이 14,000개를 넘어 프레임이 멈춘 것처럼 보일 수 있습니다.");
    }

    void CollectFromFolder()
    {
#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(tileFolder))
        {
            Debug.LogError($"[TilemapLab] 폴더가 없습니다: {tileFolder}");
            return;
        }

        var ground = new List<TileBase>();
        var accent = new List<TileBase>();

        foreach (var guid in AssetDatabase.FindAssets("t:TileBase", new[] { tileFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile == null) continue;
            // 바닥은 한 시트에서 나오므로 텍스처가 1종이다. 나머지는 다른 시트라 텍스처가 갈린다.
            if (tile.name.StartsWith("ground")) ground.Add(tile);
            else accent.Add(tile);
        }

        // 이름순 정렬 — 누가 실행해도 같은 맵이 나와야 한다.
        ground.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        accent.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        groundTiles = ground.ToArray();
        accentTiles = accent.ToArray();

        Debug.Log($"[TilemapLab] 타일 수집 완료 — 바닥 {groundTiles.Length}종 · 그 외 {accentTiles.Length}종");
#endif
    }

    void OnGUI()
    {
        if (!showOverlay) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
        const int w = 360, h = 160;
        GUI.Box(new Rect(10, 10, w, h), GUIContent.none);
        GUI.Label(new Rect(24, 20, w - 24, h), string.Join("\n", new[]
        {
            $"맵  :  {mapSize} × {mapSize}  =  {_tileCount:N0} 타일",
            $"모드  :  {(useSpriteRenderersInstead ? "SpriteRenderer (대조군)" : renderMode.ToString())}",
            $"텍스처 분산  :  {textureSpread}   ({_distinctTextures}종)",
            $"청크 컬링  :  {(autoChunkCulling ? "Auto" : "Manual " + manualCullingBounds)}",
            $"FPS  :  {_fps:F1}",
            "",
            "Game View 우상단 Stats 에서",
            "Batches / SetPass Calls 확인",
        }), style);
    }
}
