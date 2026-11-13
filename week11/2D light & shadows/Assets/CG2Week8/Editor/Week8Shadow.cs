using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using C = Week8Common;

/// <summary>
/// 8주차 3단계 — 벽이 빛을 가리게 한다.
///   3-1 기둥 세우기 : 화면 안에 벽이 거의 없으면 그림자를 볼 곳도 없다. 플레이어 주변에 기둥을 칠한다.
///   (직접)          : Tilemap_Wall에 Shadow Caster 2D를 붙이고 Casting Source를 CompositeCollider2D로 바꾼다.
///   3-2 그림자 검사 : 기본값 Tilemap Collider 2D는 도형이 0개라서 오류 없이 그림자가 안 생긴다 — 이것을 잡는다.
/// </summary>
public static class Week8Shadow
{
    const string RULE = "Assets/CG2Week4/Tiles/wall_rule.asset";
    const string WALL = "Tilemap_Wall";

    // 칸 단위 (한 칸 = 0.5유닛): 왼쪽 아래 x, y, 너비, 높이
    public static readonly int[][] PILLARS =
    {
        // 플레이어 둘레 3~4유닛 — 빛이 센 곳에 있어야 그림자가 눈에 띈다(멀리 두면 그늘 면적이 0.8%뿐이었다)
        new[] { -6, 3, 3, 3 }, new[] { 3, 3, 3, 3 }, new[] { -7, -5, 3, 3 },
        new[] { 3, -6, 3, 3 }, new[] { 9, 0, 3, 6 }, new[] { -2, 9, 8, 2 }
    };

    static Tilemap Wall()
    {
        var go = GameObject.Find(WALL);
        return go != null ? go.GetComponent<Tilemap>() : null;
    }

    /// <summary>편집 모드에서 SetTile만 하면 충돌 도형이 갱신되지 않는다(테스트: 기둥에 충돌도 그림자도 없었다) → 직접 다시 만든다.</summary>
    static void RebuildCollider(Tilemap tm)
    {
        var tc = tm.GetComponent<TilemapCollider2D>();
        var cc = tm.GetComponent<CompositeCollider2D>();
        if (tc != null) { tc.enabled = false; tc.enabled = true; tc.ProcessTilemapChanges(); }
        if (cc != null) cc.GenerateGeometry();
    }

    [MenuItem(C.MENU + "3-1. 전투 공간 기둥 세우기 — 벽 타일 칠하기", priority = 31)]
    public static void Pillars()
    {
        if (C.Busy("기둥")) return;
        var tm = Wall();
        var rule = AssetDatabase.LoadAssetAtPath<TileBase>(RULE);
        if (tm == null || rule == null) { Debug.LogWarning("[실습] 3-1 Tilemap_Wall 또는 wall_rule을 찾지 못했다."); return; }
        Undo.RegisterCompleteObjectUndo(tm, "기둥");
        int n = 0;
        foreach (var p in PILLARS)
            for (int x = 0; x < p[2]; x++)
                for (int y = 0; y < p[3]; y++)
                {
                    var c = new Vector3Int(p[0] + x, p[1] + y, 0);
                    if (tm.HasTile(c)) continue;
                    tm.SetTile(c, rule);
                    n++;
                }
        RebuildCollider(tm);
        EditorSceneManager.MarkSceneDirty(tm.gameObject.scene);
        Debug.Log($"[실습] 3-1 기둥 {PILLARS.Length}개 — 벽 타일 {n}칸 추가 (Tilemap_Wall · 충돌도 함께 생긴다)\n" +
                  "        아직 그림자는 없다. 벽이 빛을 가린다는 정보를 Unity에 알려 주지 않았다\n" +
                  "        ── 다음 ── (직접) Tilemap_Wall 선택 → Add Component → Shadow Caster 2D → 3-2");
    }

    [MenuItem(C.MENU + "3-2. 그림자 검사 — 캐스터와 조명", priority = 32)]
    public static void Check()
    {
        if (C.Busy("그림자 검사")) return;
        var tm = Wall();
        if (tm == null) { Debug.LogWarning("[검증] 3-2 Tilemap_Wall을 찾지 못했다."); return; }
        Selection.activeGameObject = tm.gameObject;
        var sc = tm.GetComponent<ShadowCaster2D>();
        var sb = new StringBuilder("[검증] 3-2 그림자 — 벽 쪽\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        if (sc == null) sb.AppendLine("| Shadow Caster 2D | 없음 | ⚠ Tilemap_Wall → Add Component → Shadow Caster 2D |");
        else
        {
            var so = new SerializedObject(sc);
            var source = so.FindProperty("m_ShadowShape2DComponent").objectReferenceValue;
            int shapes = source is Collider2D col ? col.shapeCount : -1;
            bool ok = shapes > 0;
            sb.AppendLine("| Shadow Caster 2D | 있음 | ✓ |");
            sb.AppendLine($"| Casting Source | {(source != null ? source.GetType().Name : "없음")} | {(ok ? "✓" : "⚠ Casting Source → CompositeCollider2D로 바꾼다")} |");
            sb.AppendLine($"| 그림자 도형 수 | {shapes} | {(ok ? "✓" : "⚠ 0개면 오류 없이 그림자가 안 생긴다")} |");
            sb.AppendLine($"| Casting Option | {sc.castingOption} | {(sc.castingOption == ShadowCaster2D.ShadowCastingOptions.CastShadow ? "✓" : "⚠ Cast Shadow로")} |");
        }
        sb.Append("[검증] 3-2 그림자 — 조명 쪽\n| 조명 | Shadows | Strength | Softness | 판정 |\n|---|---|---|---|---|\n");
        foreach (var l in C.DirectionalLights())
        {
            bool ok = l.shadowsEnabled && l.shadowIntensity >= 0.9f;
            sb.AppendLine($"| {l.name} | {(l.shadowsEnabled ? "켬" : "끔")} | {l.shadowIntensity:0.00} | {C.ShadowSoftness(l):0.00} | {(ok ? "✓" : "⚠ Shadows 켜고 Strength 1로")} |");
        }
        sb.Append("        ── 다음 ── 모두 ✓이면 Play → 3-3");
        Debug.Log(sb.ToString());
    }

    [MenuItem(C.MENU + "4-3. (비교) 그림자 끄기 ↔ 켜기", priority = 43)]
    public static void Toggle()
    {
        var lights = C.DirectionalLights();
        if (lights.Count == 0) return;
        bool on = false;
        foreach (var l in lights) on |= l.shadowsEnabled;
        foreach (var l in lights)
        {
            Undo.RecordObject(l, "그림자 비교");
            l.shadowsEnabled = !on;
        }
        Debug.Log($"[비교] 4-3 조명 {lights.Count}개 Shadows → {(on ? "끔 (빛이 벽을 뚫는다)" : "켬 (벽 뒤가 어둡다)")}");
    }

    [MenuItem(C.MENU + "되돌리기 — 3단계 기둥 · Shadow Caster 2D", priority = 93)]
    public static void Revert()
    {
        if (C.Busy("되돌리기")) return;
        var tm = Wall();
        if (tm == null) return;
        Undo.RegisterCompleteObjectUndo(tm, "기둥 지우기");
        foreach (var p in PILLARS)
            for (int x = 0; x < p[2]; x++)
                for (int y = 0; y < p[3]; y++)
                    tm.SetTile(new Vector3Int(p[0] + x, p[1] + y, 0), null);
        RebuildCollider(tm);
        var sc = tm.GetComponent<ShadowCaster2D>();
        if (sc != null) Undo.DestroyObjectImmediate(sc);
        EditorSceneManager.MarkSceneDirty(tm.gameObject.scene);
        Debug.Log("[되돌리기] 기둥 타일 삭제 · Tilemap_Wall의 Shadow Caster 2D 삭제");
    }
}
