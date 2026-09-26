#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 4주차 타일 에셋 생성기 — 타일시트 PNG와 타일 에셋을 코드로 만든다.
///
/// 왜 코드로 만드는가.
///   32px 픽셀 퍼펙트 타일시트는 그림 파일로 배포하면 임포트 설정 하나만 어긋나도
///   타일 사이에 틈이 생기고 수업이 멈춘다. 특히 Rule Tile 16-mask는 셀 경계 픽셀이
///   정확히 맞물려야 하는데, 이걸 손이나 생성형 AI로 맞추는 것은 신뢰할 수 없다.
///   코드로 그리면 정렬이 수학적으로 보장된다.
///   2주차 StarterSceneBuilder의 Circle()/Square()와 같은 방식이다.
///
/// 규격은 Docs/Week4/00_규격확정서.md 를 따른다.
///   타일 32px · PPU 64 · 한 칸 0.5유닛 · Full Rect · 압축 없음 · Point 필터
///
/// 생성물
///   Assets/CG2Week4/Art/tile_ground_32.png   256×64  (8×2, 16셀)
///   Assets/CG2Week4/Art/tile_wall_32.png     128×128 (4×4, 16셀 = 4방향 마스크)
///   Assets/CG2Week4/Art/tile_props_32.png    128×64  (4×2, 8셀)
///   Assets/CG2Week4/Art/tile_hazard_32.png   128×64  (4×2, 8셀)
///   Assets/CG2Week4/Tiles/*.asset            Tile / RuleTile
///
/// 셀 번호는 좌상단부터 행 우선(0,1,2… → 다음 줄)이다.
/// Claude Design으로 만든 시트로 교체해도 같은 순서면 그대로 동작한다.
/// </summary>
public static class TilemapAssetBuilder
{
    public const int TILE = 32;
    public const int PPU = 64;

    const string ROOT = "Assets/CG2Week4";
    const string ART = ROOT + "/Art";
    const string TILES = ROOT + "/Tiles";

    // 규격확정서 §6 팔레트. 채도 낮은 어두운 색으로 고정해 2주차 엔티티(고채도)가 위에서 튀게 한다.
    static readonly Color32 GROUND_BASE = Hex(0x2A2E3A);
    static readonly Color32 GROUND_LIGHT = Hex(0x31353F);
    static readonly Color32 GROUND_DARK = Hex(0x242832);
    static readonly Color32 GROUND_CRACK = Hex(0x1E212B);
    // 벽은 바닥과 명도가 뚜렷이 갈려야 구조가 읽힌다.
    // 초안(#3D4250)은 바닥 대비 1.4배뿐이라 확대해도 흐릿했다. 1.8배로 올렸다.
    //   바닥 #2A2E3A 명도 46  ·  벽면 #4A5165 명도 81  ·  테두리 #6E7794 명도 120
    // 채도는 그대로 낮게 유지하므로 2주차 엔티티(고채도 파랑·빨강)와는 여전히 안 겹친다.
    static readonly Color32 WALL_FACE = Hex(0x4A5165);
    static readonly Color32 WALL_EDGE = Hex(0x6E7794);
    static readonly Color32 WALL_SHADOW = Hex(0x1E212B);
    static readonly Color32 WALL_DITHER = Hex(0x535B72);
    static readonly Color32 PROP_BASE = Hex(0x4A4F5E);
    static readonly Color32 PROP_LIGHT = Hex(0x5A6076);
    static readonly Color32 PROP_SHADOW = Hex(0x1E212B);
    static readonly Color32 HAZARD_BASE = Hex(0x8A3A2A);
    static readonly Color32 HAZARD_LIGHT = Hex(0xA85038);
    static readonly Color32 HAZARD_DARK = Hex(0x5A2418);
    static readonly Color32 CLEAR = new(0, 0, 0, 0);

    // 4방향 마스크 비트. 규격확정서 §4-1과 동일해야 한다.
    const int N = 1, E = 2, S = 4, W = 8;
    static readonly Vector3Int[] DIRS = { new(0, 1, 0), new(1, 0, 0), new(0, -1, 0), new(-1, 0, 0) };
    static readonly int[] BITS = { N, E, S, W };

    // ────────────────────────────────────────────────────────────── 메뉴

    [MenuItem("CG2 Lab/4주차 준비/4. 타일 에셋 생성", priority = 3)]
    public static void BuildAll()
    {
        EnsureFolders();

        var ground = BuildSheet("tile_ground_32", 8, 2, PaintGroundCell);
        var wall = BuildSheet("tile_wall_32", 4, 4, PaintWallCell);
        var props = BuildSheet("tile_props_32", 4, 2, PaintPropCell);
        var hazard = BuildSheet("tile_hazard_32", 4, 2, PaintHazardCell);

        var groundSprites = LoadSlices(ground);
        var wallSprites = LoadSlices(wall);
        var propSprites = LoadSlices(props);
        var hazardSprites = LoadSlices(hazard);

        // 슬라이스가 기대한 개수만큼 나왔는지 먼저 확인한다. 여기서 틀리면 뒤가 전부 무너진다.
        if (!Expect(groundSprites, 16, ground) || !Expect(wallSprites, 16, wall) ||
            !Expect(propSprites, 8, props) || !Expect(hazardSprites, 8, hazard))
            return;

        var groundTiles = BuildTiles(groundSprites, "ground", Tile.ColliderType.None);
        var propTiles = BuildTiles(propSprites, "prop", Tile.ColliderType.None);
        var hazardTiles = BuildTiles(hazardSprites, "hazard", Tile.ColliderType.None);
        var wallRule = BuildWallRuleTile(wallSprites);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[검증] 4주차 타일 에셋 생성 완료\n" +
            $"        시트 4장 · 스프라이트 {groundSprites.Count + wallSprites.Count + propSprites.Count + hazardSprites.Count}장\n" +
            $"        Tile 에셋 {groundTiles.Count + propTiles.Count + hazardTiles.Count}개 + RuleTile 1개\n" +
            $"        벽 RuleTile 규칙 {wallRule.m_TilingRules.Count}개 (16이어야 정상)\n" +
            $"        PPU {groundSprites[0].pixelsPerUnit} · 타일 {TILE}px = {TILE / (float)PPU}유닛\n" +
            $"        → Grid의 Cell Size를 ({TILE / (float)PPU}, {TILE / (float)PPU}, 0)으로 두어야 틈이 없습니다.");

        Selection.activeObject = wallRule;
        EditorGUIUtility.PingObject(wallRule);
    }

    [MenuItem("CG2 Lab/4주차 준비/되돌리기 — 타일 에셋 삭제", priority = 42)]
    public static void DeleteAll()
    {
        if (!EditorUtility.DisplayDialog("타일 에셋 삭제",
                $"{ART} 와 {TILES} 를 삭제합니다.\n다시 생성하면 동일한 결과가 나옵니다.", "삭제", "취소"))
            return;
        AssetDatabase.DeleteAsset(ART);
        AssetDatabase.DeleteAsset(TILES);
        AssetDatabase.Refresh();
        Debug.Log("[CG2 4주차] 타일 에셋을 삭제했습니다.");
    }

    // ────────────────────────────────────────────────────────────── 시트 생성

    /// <summary>셀을 그리는 함수를 받아 시트 PNG를 굽고 임포트 설정까지 맞춘다.</summary>
    static string BuildSheet(string name, int cols, int rows, System.Action<Color32[], int, int, int, int, int> paintCell)
    {
        int w = cols * TILE, h = rows * TILE;
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = CLEAR;

        for (int i = 0; i < cols * rows; i++)
        {
            // 셀 번호는 좌상단부터. Texture2D는 좌하단 원점이므로 y를 뒤집는다.
            int col = i % cols, rowFromTop = i / cols;
            int x0 = col * TILE;
            int y0 = h - (rowFromTop + 1) * TILE;
            paintCell(px, w, x0, y0, i, cols * rows);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();

        string path = $"{ART}/{name}.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        ApplyImportSettings(path, name, cols, rows, h);
        return path;
    }

    /// <summary>규격확정서 §2를 그대로 적용한다. 하나라도 빠지면 타일 사이에 틈이 생긴다.</summary>
    static void ApplyImportSettings(string path, string name, int cols, int rows, int h)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter ti) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = PPU;
        ti.filterMode = FilterMode.Point;          // 픽셀아트 필수
        ti.mipmapEnabled = false;                  // 2D 직교 카메라에서 불필요 + 메모리 33% 낭비
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;
        ti.npotScale = TextureImporterNPOTScale.None;

        // Mesh Type이 Tight면 타일마다 정점 수가 달라져 seam이 생기고 삼각형 수도 왜곡된다.
        // 4주차 측정값을 믿으려면 Full Rect가 반드시 필요하다.
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteExtrude = 0;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteGenerateFallbackPhysicsShape = false;
        ti.SetTextureSettings(s);

        // 압축을 켜면 4×4 블록 단위로 뭉개져 32px 타일의 경계가 무너진다.
        var ps = ti.GetDefaultPlatformTextureSettings();
        ps.format = TextureImporterFormat.RGBA32;
        ps.textureCompression = TextureImporterCompression.Uncompressed;
        ps.maxTextureSize = 2048;
        ps.overridden = true;
        ti.SetPlatformTextureSettings(ps);

        // 데이터 프로바이더는 임포터 설정이 확정된 뒤에 열어야 한다. 먼저 반영한다.
        ti.SaveAndReimport();

        Slice(ti, name, cols, rows, h);
    }

    /// <summary>
    /// 시트를 32×32 격자로 자른다.
    ///
    /// TextureImporter.spritesheet은 쓰지 않는다. Unity 6에서 이 경로는 obsolete가 아니라
    /// 아예 제거되어(“has been removed”), 대입해도 조용히 무시된다. 컴파일은 통과하지만
    /// 슬라이스가 안 되어 시트가 통짜 스프라이트 1장이 된다.
    /// 정식 경로인 ISpriteEditorDataProvider를 쓴다.
    ///
    /// 셀 번호는 좌상단부터 행 우선. Texture2D는 좌하단 원점이므로 y를 뒤집어 계산한다.
    /// </summary>
    static void Slice(TextureImporter ti, string name, int cols, int rows, int texHeight)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(ti);
        if (provider == null)
        {
            Debug.LogError($"[TilemapAssetBuilder] 스프라이트 데이터 프로바이더를 열 수 없습니다: {ti.assetPath}");
            return;
        }

        provider.InitSpriteEditorDataProvider();

        var rects = new SpriteRect[cols * rows];
        for (int i = 0; i < rects.Length; i++)
        {
            int col = i % cols, rowFromTop = i / cols;
            rects[i] = new SpriteRect
            {
                name = $"{name}_{i}",
                spriteID = GUID.Generate(),
                rect = new Rect(col * TILE, texHeight - (rowFromTop + 1) * TILE, TILE, TILE),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
            };
        }
        provider.SetSpriteRects(rects);

        // 이름↔파일ID 표를 함께 갱신하지 않으면, 다시 슬라이스할 때 기존 스프라이트를
        // 참조하던 에셋(Tile, 프리팹)의 연결이 끊어진다.
        var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameIds != null)
        {
            var pairs = new List<SpriteNameFileIdPair>(rects.Length);
            foreach (var r in rects) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
            nameIds.SetNameFileIdPairs(pairs);
        }

        provider.Apply();
        ti.SaveAndReimport();
    }

    static List<Sprite> LoadSlices(string path)
    {
        var list = new List<Sprite>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp) list.Add(sp);
        // 이름 끝 숫자로 정렬한다. LoadAllAssetsAtPath는 순서를 보장하지 않는다.
        list.Sort((a, b) => TrailingIndex(a.name).CompareTo(TrailingIndex(b.name)));
        return list;
    }

    static int TrailingIndex(string n)
    {
        int i = n.LastIndexOf('_');
        return i >= 0 && int.TryParse(n[(i + 1)..], out var v) ? v : 0;
    }

    static bool Expect(List<Sprite> sprites, int count, string path)
    {
        if (sprites.Count == count) return true;
        Debug.LogError($"[TilemapAssetBuilder] 슬라이스 실패 — {path} 에서 {count}개를 기대했으나 " +
                       $"{sprites.Count}개가 나왔습니다. 임포트 설정이 적용되지 않았습니다.\n" +
                       "Sprite Editor에서 Grid By Cell Size 32×32로 수동 슬라이스한 뒤 다시 실행하세요.");
        return false;
    }

    // ────────────────────────────────────────────────────────────── 타일 에셋

    static List<TileBase> BuildTiles(List<Sprite> sprites, string prefix, Tile.ColliderType collider)
    {
        var result = new List<TileBase>();
        for (int i = 0; i < sprites.Count; i++)
        {
            string p = $"{TILES}/{prefix}_{i:D2}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(p);
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, p); }
            tile.sprite = sprites[i];
            tile.colliderType = collider;
            tile.color = Color.white;
            EditorUtility.SetDirty(tile);
            result.Add(tile);
        }
        return result;
    }

    /// <summary>
    /// 벽 RuleTile — 상/우/하/좌 4방향 이웃 조합 16가지를 규칙으로 등록한다.
    ///
    /// 16개 규칙이 4방향을 전부 명시하므로 서로 배타적이다. 따라서 규칙 순서가
    /// 결과에 영향을 주지 않는다. 순서 의존적인 Rule Tile은 디버깅이 어려운데,
    /// 이 구성은 그 문제가 원천적으로 없다.
    /// </summary>
    static RuleTile BuildWallRuleTile(List<Sprite> sprites)
    {
        const string p = TILES + "/wall_rule.asset";
        var rt = AssetDatabase.LoadAssetAtPath<RuleTile>(p);
        if (rt == null) { rt = ScriptableObject.CreateInstance<RuleTile>(); AssetDatabase.CreateAsset(rt, p); }

        rt.m_DefaultSprite = sprites[15];                 // 사방이 막힌 중앙 타일
        rt.m_DefaultColliderType = Tile.ColliderType.Grid; // 벽은 충돌한다
        rt.m_TilingRules = new List<RuleTile.TilingRule>(16);

        for (int mask = 0; mask < 16; mask++)
        {
            var rule = new RuleTile.TilingRule
            {
                m_Sprites = new[] { sprites[mask] },
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                m_ColliderType = Tile.ColliderType.Grid,
                m_Neighbors = new List<int>(4),
                m_NeighborPositions = new List<Vector3Int>(4),
            };

            for (int d = 0; d < 4; d++)
            {
                rule.m_NeighborPositions.Add(DIRS[d]);
                rule.m_Neighbors.Add((mask & BITS[d]) != 0
                    ? RuleTile.TilingRuleOutput.Neighbor.This
                    : RuleTile.TilingRuleOutput.Neighbor.NotThis);
            }

            rt.m_TilingRules.Add(rule);
        }

        EditorUtility.SetDirty(rt);
        return rt;
    }

    // ────────────────────────────────────────────────────────────── 셀 그리기

    /// <summary>바닥 — 0~7 기본 변형, 8~11 균열, 12~15 자갈.</summary>
    static void PaintGroundCell(Color32[] px, int texW, int x0, int y0, int idx, int total)
    {
        uint seed = (uint)(0x9E3779B1u * (uint)(idx + 1));

        for (int y = 0; y < TILE; y++)
            for (int x = 0; x < TILE; x++)
            {
                // 셀 로컬 좌표로만 해싱하므로 같은 타일을 반복해 깔아도 이음매가 튀지 않는다.
                uint h = Hash((uint)x, (uint)y, seed);
                var c = (h % 100) switch
                {
                    < 12 => GROUND_LIGHT,
                    < 26 => GROUND_DARK,
                    _ => GROUND_BASE,
                };
                Set(px, texW, x0 + x, y0 + y, c);
            }

        if (idx is >= 8 and <= 11)
        {
            // 균열 — 셀 안에서만 이어지는 짧은 사선
            int cx = 6 + (int)(Hash(3, 7, seed) % 8);
            int cy = 4 + (int)(Hash(11, 5, seed) % 8);
            for (int t = 0; t < 20; t++)
            {
                Set(px, texW, x0 + Clamp(cx), y0 + Clamp(cy), GROUND_CRACK);
                cx += (int)(Hash((uint)t, 1, seed) % 3) - 1 + 1;
                cy += (int)(Hash((uint)t, 2, seed) % 3) - 1;
            }
        }
        else if (idx >= 12)
        {
            // 자갈 — 2×2 점 몇 개. 밝은 면과 그림자를 같이 찍어 입체감을 낸다.
            for (int k = 0; k < 5; k++)
            {
                int px0 = 3 + (int)(Hash((uint)k, 21, seed) % (TILE - 8));
                int py0 = 3 + (int)(Hash((uint)k, 22, seed) % (TILE - 8));
                Rect2(px, texW, x0 + px0, y0 + py0, 2, 2, GROUND_LIGHT);
                Rect2(px, texW, x0 + px0, y0 + py0 - 1, 2, 1, GROUND_CRACK);
            }
        }
    }

    /// <summary>
    /// 벽 — 셀 번호가 곧 4방향 마스크다.
    /// 연결된 방향은 벽 면을 셀 끝까지 채우고, 끊긴 방향에만 테두리를 그린다.
    /// 그래야 두 셀을 붙였을 때 하나의 벽으로 이어져 보인다.
    /// </summary>
    static void PaintWallCell(Color32[] px, int texW, int x0, int y0, int mask, int total)
    {
        Rect2(px, texW, x0, y0, TILE, TILE, WALL_FACE);

        // 돌 블록 무늬. 16개 셀에서 완전히 동일해야 이어붙였을 때 무늬가 흐르듯 연결된다.
        for (int y = 0; y < TILE; y++)
            for (int x = 0; x < TILE; x++)
            {
                bool mortarH = y % 11 == 0;
                bool mortarV = x % 16 == (y / 11 % 2 == 0 ? 0 : 8);
                if (mortarH || (mortarV && y % 11 != 0))
                    Set(px, texW, x0 + x, y0 + y, WALL_SHADOW);
                else if ((x + y) % 7 == 0)
                    Set(px, texW, x0 + x, y0 + y, WALL_DITHER); // 아주 옅은 디더 노이즈
            }

        // 끊긴 방향에만 2px 밝은 테두리 + 안쪽 1px 그림자
        bool up = (mask & N) != 0, right = (mask & E) != 0, down = (mask & S) != 0, left = (mask & W) != 0;

        if (!up)
        {
            Rect2(px, texW, x0, y0 + TILE - 2, TILE, 2, WALL_EDGE);
            Rect2(px, texW, x0, y0 + TILE - 3, TILE, 1, WALL_SHADOW);
        }
        if (!down)
        {
            Rect2(px, texW, x0, y0, TILE, 2, WALL_EDGE);
            Rect2(px, texW, x0, y0 + 2, TILE, 1, WALL_SHADOW);
        }
        if (!left)
        {
            Rect2(px, texW, x0, y0, 2, TILE, WALL_EDGE);
            Rect2(px, texW, x0 + 2, y0, 1, TILE, WALL_SHADOW);
        }
        if (!right)
        {
            Rect2(px, texW, x0 + TILE - 2, y0, 2, TILE, WALL_EDGE);
            Rect2(px, texW, x0 + TILE - 3, y0, 1, TILE, WALL_SHADOW);
        }
    }

    /// <summary>장식 — 셀 중앙에 놓이고 가장자리에서 2px 이상 떨어진다.</summary>
    static void PaintPropCell(Color32[] px, int texW, int x0, int y0, int idx, int total)
    {
        switch (idx)
        {
            case 0: Blob(px, texW, x0 + 12, y0 + 12, 6); break;                       // 작은 바위
            case 1: Blob(px, texW, x0 + 8, y0 + 8, 11); break;                        // 큰 바위
            case 2: Grass(px, texW, x0 + 16, y0 + 6, 1); break;                       // 풀 한 포기
            case 3: Grass(px, texW, x0 + 16, y0 + 6, 3); break;                       // 풀 세 포기
            case 4:                                                                    // 뼈 조각
                Rect2(px, texW, x0 + 10, y0 + 15, 12, 2, PROP_LIGHT);
                Rect2(px, texW, x0 + 9, y0 + 14, 3, 4, PROP_LIGHT);
                Rect2(px, texW, x0 + 20, y0 + 14, 3, 4, PROP_LIGHT);
                Rect2(px, texW, x0 + 10, y0 + 13, 12, 1, PROP_SHADOW);
                break;
            case 5:                                                                    // 해골
                Rect2(px, texW, x0 + 11, y0 + 12, 10, 9, PROP_LIGHT);
                Rect2(px, texW, x0 + 13, y0 + 16, 2, 2, PROP_SHADOW);
                Rect2(px, texW, x0 + 17, y0 + 16, 2, 2, PROP_SHADOW);
                Rect2(px, texW, x0 + 13, y0 + 12, 6, 2, PROP_SHADOW);
                Rect2(px, texW, x0 + 11, y0 + 11, 10, 1, PROP_SHADOW);
                break;
            case 6:                                                                    // 깨진 항아리
                Rect2(px, texW, x0 + 11, y0 + 8, 10, 10, PROP_BASE);
                Rect2(px, texW, x0 + 12, y0 + 18, 8, 2, PROP_LIGHT);
                Rect2(px, texW, x0 + 15, y0 + 14, 3, 6, CLEAR);   // 깨진 부분
                Rect2(px, texW, x0 + 11, y0 + 7, 10, 1, PROP_SHADOW);
                break;
            default:                                                                   // 나무 상자
                Rect2(px, texW, x0 + 8, y0 + 8, 16, 16, PROP_BASE);
                Rect2(px, texW, x0 + 8, y0 + 8, 16, 2, PROP_SHADOW);
                Rect2(px, texW, x0 + 8, y0 + 22, 16, 2, PROP_LIGHT);
                Rect2(px, texW, x0 + 15, y0 + 8, 2, 16, PROP_SHADOW);
                Rect2(px, texW, x0 + 8, y0 + 15, 16, 2, PROP_SHADOW);
                break;
        }
    }

    /// <summary>위험지대 — 0~3 용암 4변형(심리스), 4~7 가시 4단계.</summary>
    static void PaintHazardCell(Color32[] px, int texW, int x0, int y0, int idx, int total)
    {
        if (idx < 4)
        {
            uint seed = (uint)(0x85EBCA6Bu * (uint)(idx + 1));
            for (int y = 0; y < TILE; y++)
                for (int x = 0; x < TILE; x++)
                {
                    uint h = Hash((uint)x, (uint)y, seed);
                    var c = (h % 100) switch
                    {
                        < 14 => HAZARD_LIGHT,
                        < 34 => HAZARD_DARK,
                        _ => HAZARD_BASE,
                    };
                    Set(px, texW, x0 + x, y0 + y, c);
                }
            // 기포
            for (int k = 0; k < 4; k++)
            {
                int bx = 2 + (int)(Hash((uint)k, 31, seed) % (TILE - 6));
                int by = 2 + (int)(Hash((uint)k, 32, seed) % (TILE - 6));
                Rect2(px, texW, x0 + bx, y0 + by, 3, 3, HAZARD_LIGHT);
                Rect2(px, texW, x0 + bx, y0 + by, 3, 1, HAZARD_DARK);
            }
        }
        else
        {
            // 가시 — 4단계로 솟아오른다. 0단계는 바닥 구멍만 보인다.
            int stage = idx - 4;                 // 0..3
            Rect2(px, texW, x0, y0, TILE, TILE, GROUND_BASE);
            Rect2(px, texW, x0 + 6, y0 + 6, 20, 6, GROUND_CRACK);   // 구멍

            int height = stage * 6;              // 0, 6, 12, 18
            for (int s = 0; s < 3; s++)
            {
                int sx = 8 + s * 7;
                for (int hgt = 0; hgt < height; hgt++)
                {
                    int width = Mathf.Max(1, 5 - hgt * 5 / Mathf.Max(1, height));
                    Rect2(px, texW, x0 + sx + (5 - width) / 2, y0 + 8 + hgt, width, 1,
                          hgt > height / 2 ? PROP_LIGHT : PROP_BASE);
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────── 픽셀 유틸

    static void Set(Color32[] px, int texW, int x, int y, Color32 c)
    {
        int i = y * texW + x;
        if (i >= 0 && i < px.Length) px[i] = c;
    }

    static void Rect2(Color32[] px, int texW, int x0, int y0, int w, int h, Color32 c)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Set(px, texW, x0 + x, y0 + y, c);
    }

    static void Blob(Color32[] px, int texW, int cx, int cy, int size)
    {
        float r = size * 0.5f, ox = cx + r, oy = cy + r;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = cx + x - ox + 0.5f, dy = cy + y - oy + 0.5f;
                if (dx * dx + dy * dy > r * r) continue;
                Set(px, texW, cx + x, cy + y, dy > 0 ? PROP_LIGHT : PROP_BASE);
            }
        Rect2(px, texW, cx + 1, cy - 1, size - 2, 1, PROP_SHADOW);
    }

    static void Grass(Color32[] px, int texW, int cx, int cy, int count)
    {
        int[] offsets = { 0, -6, 6 };
        for (int k = 0; k < count; k++)
        {
            int bx = cx + offsets[k];
            int height = 9 - k * 2;
            for (int y = 0; y < height; y++)
                Set(px, texW, bx + y / 4, cy + y, y > height / 2 ? PROP_LIGHT : PROP_BASE);
        }
    }

    static int Clamp(int v) => Mathf.Clamp(v, 0, TILE - 1);

    /// <summary>결정적 해시. 누가 실행해도 같은 타일이 나와야 실습 수치가 재현된다.</summary>
    static uint Hash(uint x, uint y, uint seed)
    {
        uint h = seed ^ (x * 0x27D4EB2Du) ^ (y * 0x165667B1u);
        h ^= h >> 15; h *= 0x2545F491u; h ^= h >> 13;
        return h;
    }

    static Color32 Hex(uint rgb) =>
        new((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ROOT)) AssetDatabase.CreateFolder("Assets", "CG2Week4");
        if (!AssetDatabase.IsValidFolder(ART)) AssetDatabase.CreateFolder(ROOT, "Art");
        if (!AssetDatabase.IsValidFolder(TILES)) AssetDatabase.CreateFolder(ROOT, "Tiles");
        AssetDatabase.Refresh();
    }
}
#endif
