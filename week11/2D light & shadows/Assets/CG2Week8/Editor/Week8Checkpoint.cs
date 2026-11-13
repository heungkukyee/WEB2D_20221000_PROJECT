using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using C = Week8Common;

/// <summary>
/// 8주차 5-3 체크포인트 (강의계획서): Shadow Caster 2D가 정상 연동되어 빛 가림에 의한 그림자가 자연스럽게 생성되는가?
/// 게임 씬에서 실행한다. ✓ = 통과 · ⚠ = 고칠 것 · ℹ = 참고(경고로 세지 않음)
/// </summary>
public static class Week8Checkpoint
{
    [MenuItem(C.MENU + "5-3. 체크포인트 — 필드 · 조명 · 그림자 · 측정", priority = 53)]
    public static void Run()
    {
        int warn = 0;
        var sb = new StringBuilder();
        string Ok(bool ok, string good, string bad) { if (!ok) warn++; return ok ? "✓ " + good : "⚠ " + bad; }

        // 1. 필드
        float ground = C.Albedo(C.GROUND_PNG);
        var g = C.Named(C.GROUND_LIGHT); var e = C.Named(C.ENTITY_LIGHT);
        sb.AppendLine($"        [필드]   {Ok(ground >= 0.2f, $"바닥 반사율 {ground * 100f:0.0}%", $"바닥 반사율 {ground * 100f:0.0}% — 1-2")}  " +
                      Ok(g != null && g.intensity <= 0.1f, $"환경광 {(g != null ? g.intensity : 0):0.00}", "환경광이 밝다 — 0.06") + "  " +
                      (e != null && e.intensity <= 0.25f ? $"✓ 캐릭터 조명 {e.intensity:0.00}" : "ℹ 캐릭터 조명을 확인한다"));

        // 2. 조명
        var torch = C.Named(C.TORCH);
        var lamps = GameObject.Find(C.LAMPS);
        var lantern = C.Named(C.LANTERN);
        sb.AppendLine($"        [조명]   {Ok(torch != null, "횃불", "횃불 없음 — 이름을 Torch로 (2-1)")}  " +
                      Ok(lamps != null && lamps.transform.childCount >= 2, "가로등 2개", "가로등 없음 (2-2)") + "  " +
                      Ok(lantern != null, "플레이어 랜턴", "랜턴 없음 (4-1)"));

        // 3. 그림자
        var wall = GameObject.Find("Tilemap_Wall");
        var sc = wall != null ? wall.GetComponent<ShadowCaster2D>() : null;
        int shapes = -1;
        if (sc != null && new SerializedObject(sc).FindProperty("m_ShadowShape2DComponent").objectReferenceValue is Collider2D col) shapes = col.shapeCount;
        int shadowLights = 0;
        foreach (var l in C.DirectionalLights()) if (l.shadowsEnabled) shadowLights++;
        sb.AppendLine($"        [그림자] {Ok(sc != null, "Shadow Caster 2D", "캐스터 없음 (3단계)")}  " +
                      Ok(shapes > 0, $"도형 {shapes}개", "도형 0개 — Casting Source를 CompositeCollider2D로") + "  " +
                      Ok(shadowLights > 0, $"그림자 조명 {shadowLights}개", "Shadows를 켠 조명이 없다"));

        // 4. 색 공간
        sb.AppendLine($"        [색 공간] {Ok(PlayerSettings.colorSpace == ColorSpace.Linear, "Linear", "Gamma → 6주차 1-2로 Linear")}");

        // 5. 측정
        string M(string k) => C.Load(k);
        sb.AppendLine($"        [측정]   {Ok(M("m1") != "", "#1", "#1 없음 (1-1)")}  {Ok(M("m2") != "", "#2", "#2 없음 (1-4)")}  {Ok(M("m3") != "", "#3", "#3 없음 (2-4)")}  " +
                      $"{Ok(M("m4") != "", "#4", "#4 없음 (3-3)")}  {Ok(M("m5") != "", "#5", "#5 없음 (4-2)")}  {Ok(M("m6") != "", "#6", "#6 없음 (5-1)")}");
        if (M("m1_range") != "" && M("m3_range") != "")
            sb.AppendLine($"                 {Ok(float.Parse(M("m3_range")) >= float.Parse(M("m1_range")) * 2f, $"명암 폭 {M("m1_range")} → {M("m3_range")}", $"명암 폭이 약하다 {M("m1_range")} → {M("m3_range")}")}");
        if (M("m4_area") != "" && M("m4_depth") != "")
        {
            float area = float.Parse(M("m4_area")), depth = float.Parse(M("m4_depth"));
            sb.AppendLine($"                 {Ok(area >= 3f && depth >= 40f, $"그늘 면적 {area:0.0}% · 깊이 {depth:0}%", $"그림자가 약하다 — 면적 {area:0.0}% · 깊이 {depth:0}% (3-2)")}");
        }
        sb.AppendLine($"        [README] {(File.Exists(C.CAPTURES + "/week8_before_after.png") ? "✓ 전·후 캡처" : "ℹ 전·후 캡처 없음 (5-2)")}");

        Debug.Log($"[검증] 8주차 체크포인트 — 경고 {warn}개\n" + sb +
                  "        체크포인트: Shadow Caster 2D가 정상 연동되어 빛 가림에 의한 그림자가 자연스럽게 생성되는가?");
    }
}
