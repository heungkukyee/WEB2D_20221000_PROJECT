#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 벽 Rule Tile이 제대로 자동 연결되는지 눈으로 확인하는 검증 도구.
///
/// 왜 필요한가 —
/// Rule Tile은 "이웃에 따라 다른 그림을 고르는" 규칙 묶음이라, 규칙이 하나만 틀려도
/// 특정 모양에서만 벽이 깨진다. 그런데 그 특정 모양은 학생이 맵을 그리다가 우연히
/// 만들기 전까지 드러나지 않는다. 수업 중에 발견하면 늦다.
///
/// 이 도구는 16가지 이웃 조합이 전부 등장하는 패턴을 자동으로 그린다.
/// 하나라도 그림이 어긋나면 그 자리에서 바로 보인다.
///
/// 추가로, 그려진 패턴에서 실제로 몇 가지 마스크가 나왔는지 코드로 세어 로그에 찍는다.
/// "16/16" 이 아니면 패턴이 부족한 것이므로 검증 자체가 불완전하다는 뜻이다.
/// </summary>
public static class RuleTileVerifier
{
    const string RULE_TILE = "Assets/CG2Week4/Tiles/wall_rule.asset";
    const string GROUND_TILE = "Assets/CG2Week4/Tiles/ground_00.asset";
    const string ROOT_NAME = "RuleTile 검증";

    // 4방향 비트 — TilemapAssetBuilder와 반드시 같아야 한다.
    const int N = 1, E = 2, S = 4, W = 8;

    [MenuItem("CG2 Lab/4주차 준비/6. 벽 Rule Tile 연결 검증", priority = 6)]
    public static void Verify()
    {
        var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RULE_TILE);
        if (rule == null)
        {
            EditorUtility.DisplayDialog("Rule Tile 검증",
                "벽 Rule Tile이 없습니다.\n먼저 'CG2 Lab → 4주차 준비 → 4. 타일 에셋 생성'을 실행하세요.", "확인");
            return;
        }

        var old = GameObject.Find(ROOT_NAME);
        if (old != null) Object.DestroyImmediate(old);

        var cells = BuildPattern();

        // ── 씬 구성 ────────────────────────────────────────────────
        var root = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(root, "RuleTile 검증");

        var grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(0.5f, 0.5f, 0f);   // 타일 32px @ PPU 64

        // 바닥을 깔아 벽 실루엣이 또렷하게 보이도록 한다.
        var ground = AssetDatabase.LoadAssetAtPath<Tile>(GROUND_TILE);
        if (ground != null) PaintGround(root.transform, ground, cells);

        var wallGo = new GameObject("Tilemap_Wall");
        wallGo.transform.SetParent(root.transform, false);
        var wallMap = wallGo.AddComponent<Tilemap>();
        var wallRenderer = wallGo.AddComponent<TilemapRenderer>();
        if (SortingLayer.NameToID("Wall") != 0) wallRenderer.sortingLayerName = "Wall";
        wallRenderer.sortingOrder = 1;

        foreach (var c in cells) wallMap.SetTile(new Vector3Int(c.x, c.y, 0), rule);

        // ── 마스크 커버리지 검산 ───────────────────────────────────
        var covered = new HashSet<int>();
        foreach (var c in cells) covered.Add(MaskOf(cells, c));

        var missing = new List<int>();
        for (int m = 0; m < 16; m++) if (!covered.Contains(m)) missing.Add(m);

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();

        string verdict = missing.Count == 0
            ? "16/16 — 모든 이웃 조합이 화면에 나타났습니다."
            : $"{covered.Count}/16 — 누락된 마스크: {string.Join(", ", missing)} (검증 패턴이 불완전합니다)";

        Debug.Log(
            $"[검증] 벽 Rule Tile 연결 패턴 생성 완료 — 타일 {cells.Count}개\n" +
            $"        마스크 커버리지 : {verdict}\n" +
            $"        Rule Tile 규칙 수 : {rule.m_TilingRules.Count} (16이어야 정상)\n" +
            "        ── 눈으로 확인할 것 ──\n" +
            "        ① 직선 구간에서 벽이 끊기지 않고 하나로 이어지는가\n" +
            "        ② ㄱ자·T자·십자 교차점에서 테두리가 어색하게 남아 있지 않은가\n" +
            "        ③ 사각형 방의 안쪽 면에 바깥쪽 테두리가 잘못 그려지지 않았는가\n" +
            "        ④ 외따로 떨어진 타일 하나는 사방이 테두리로 막혀 있는가\n" +
            "        Scene 뷰에서 확대해 보세요. Game 뷰가 아니라 Scene 뷰입니다.");
    }

    [MenuItem("CG2 Lab/4주차 준비/되돌리기 — 벽 검증 패턴 삭제", priority = 43)]
    public static void Clear()
    {
        var old = GameObject.Find(ROOT_NAME);
        if (old == null) { Debug.Log("[CG2] 검증 패턴이 씬에 없습니다."); return; }
        Undo.DestroyObjectImmediate(old);
        Debug.Log("[CG2] 벽 검증 패턴을 삭제했습니다.");
    }

    // ────────────────────────────────────────────────────────────── 패턴

    /// <summary>16가지 이웃 조합이 전부 등장하도록 설계한 도형 모음.</summary>
    static HashSet<Vector2Int> BuildPattern()
    {
        var c = new HashSet<Vector2Int>();

        // 고립 1개 → mask 0
        c.Add(new Vector2Int(0, 0));

        // 가로 직선 → E(2) · EW(10) · W(8)
        HLine(c, 4, 8, 0);

        // 세로 직선 → N(1) · NS(5) · S(4)
        VLine(c, 0, 4, 8);

        // 사각형 방 → 네 모서리 NE(3) · NW(9) · SE(6) · SW(12)
        Rect(c, 4, 4, 9, 9);

        // 십자 → 중앙 NESW(15)
        HLine(c, 11, 15, 2);
        VLine(c, 13, 0, 4);

        // T자 4방향 → NEW(11) · NES(7) · NSW(13) · ESW(14)
        HLine(c, 18, 22, 7); VLine(c, 20, 8, 10);   // 위로 뻗음 → NEW(11)
        VLine(c, 26, 0, 4); c.Add(new Vector2Int(27, 2));   // 오른쪽 → NES(7)
        VLine(c, 31, 0, 4); c.Add(new Vector2Int(30, 2));   // 왼쪽  → NSW(13)
        HLine(c, 26, 30, 7); c.Add(new Vector2Int(28, 6));  // 아래로 → ESW(14)

        return c;
    }

    static void HLine(HashSet<Vector2Int> c, int x0, int x1, int y)
    { for (int x = x0; x <= x1; x++) c.Add(new Vector2Int(x, y)); }

    static void VLine(HashSet<Vector2Int> c, int x, int y0, int y1)
    { for (int y = y0; y <= y1; y++) c.Add(new Vector2Int(x, y)); }

    static void Rect(HashSet<Vector2Int> c, int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++) { c.Add(new Vector2Int(x, y0)); c.Add(new Vector2Int(x, y1)); }
        for (int y = y0; y <= y1; y++) { c.Add(new Vector2Int(x0, y)); c.Add(new Vector2Int(x1, y)); }
    }

    /// <summary>TilemapAssetBuilder가 쓴 것과 같은 규칙으로 이웃 마스크를 계산한다.</summary>
    static int MaskOf(HashSet<Vector2Int> cells, Vector2Int p)
    {
        int m = 0;
        if (cells.Contains(p + Vector2Int.up)) m |= N;
        if (cells.Contains(p + Vector2Int.right)) m |= E;
        if (cells.Contains(p + Vector2Int.down)) m |= S;
        if (cells.Contains(p + Vector2Int.left)) m |= W;
        return m;
    }

    /// <summary>벽 주변에 바닥을 한 칸 여유 있게 깔아 실루엣을 또렷하게 만든다.</summary>
    static void PaintGround(Transform parent, Tile ground, HashSet<Vector2Int> walls)
    {
        var go = new GameObject("Tilemap_Ground");
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        if (SortingLayer.NameToID("Ground") != 0) r.sortingLayerName = "Ground";

        var floor = new HashSet<Vector2Int>();
        foreach (var w in walls)
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    floor.Add(new Vector2Int(w.x + dx, w.y + dy));

        foreach (var f in floor) map.SetTile(new Vector3Int(f.x, f.y, 0), ground);
    }
}
#endif
