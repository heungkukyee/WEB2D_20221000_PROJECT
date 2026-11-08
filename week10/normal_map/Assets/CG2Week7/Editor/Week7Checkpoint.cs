using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 7주차 5-3 체크포인트 (강의계획서): 2D 라이트의 위치가 바뀔 때 스프라이트 표면 질감이 Normal Map에 의해 실시간 입체 반응을 일으키는가?
/// 게임 씬에서 실행한다. ✓ = 통과 · ⚠ = 고칠 것 · ℹ = 참고(경고로 세지 않음)
/// </summary>
public static class Week7Checkpoint
{
    [MenuItem(Week7Common.MENU + "5-3. 체크포인트 — 노멀 맵 · 조명 · 측정", priority = 53)]
    public static void Run()
    {
        int warn = 0;
        var sb = new StringBuilder();
        string Ok(bool ok, string good, string bad) { if (!ok) warn++; return ok ? "✓ " + good : "⚠ " + bad; }

        // 1. 노멀 맵
        var targets = Week7Make.GameTargets();
        int linked = 0;
        string firstProblem = null;
        foreach (var t in targets)
        {
            var c = Week7Link.Inspect(t.role, t.png);
            if (c.ok) linked++; else firstProblem ??= $"{t.role}: {c.problem}";
        }
        sb.AppendLine($"        [노멀 맵] {Ok(targets.Count > 0 && linked == targets.Count, $"게임 그림 {linked}/{targets.Count} 연결 (_NormalMap · sRGB OFF · 같은 크기)", $"{linked}/{targets.Count} — {firstProblem ?? "게임 씬에서 실행"} (3-2)")}");

        // 2. 조명
        Light2D test = null;
        foreach (var l in Object.FindObjectsByType<Light2D>())
            if (l.lightType != Light2D.LightType.Global && (test == null || l.name == Week7Light.TEST)) test = l;
        sb.Append("        [조명]   ");
        sb.Append(Ok(test != null, $"테스트 조명 '{test?.name}'", "테스트 조명 없음 (4-2)"));
        if (test != null)
        {
            sb.Append("  " + Ok(test.normalMapQuality != Light2D.NormalMapQuality.Disabled, $"Normal Maps {test.normalMapQuality}", "Normal Maps Disabled → Accurate"));
            sb.Append("  " + (test.normalMapDistance <= 1.5f ? $"✓ Distance {test.normalMapDistance:0.0}" : $"ℹ Distance {test.normalMapDistance:0.0} (1 권장)"));
            sb.Append("  " + (test.GetComponent<CG2MouseLight>() ? "✓ 마우스 추적" : "ℹ 마우스 추적 없음"));
        }
        sb.AppendLine();
        float maxGlobal = 0;
        foreach (var l in Object.FindObjectsByType<Light2D>())
            if (l.lightType == Light2D.LightType.Global && Week7Common.TargetNames(l).Contains("Entity")) maxGlobal = Mathf.Max(maxGlobal, l.intensity);
        sb.AppendLine($"                 {(maxGlobal <= 0.35f ? $"✓ 캐릭터 전역 조명 {maxGlobal:0.00} (밤 장면)" : $"ℹ 캐릭터 전역 조명 {maxGlobal:0.00} — 밝으면 명암이 묻힌다 (4-1)")}");

        // 3. 색 공간 (6주차 연결)
        sb.AppendLine($"        [색 공간] {Ok(PlayerSettings.colorSpace == ColorSpace.Linear, "Linear", "Gamma → 6주차 1-2로 Linear")}");

        // 4. 측정
        string m1 = Week7Common.Load("measure1_lab"), m2 = Week7Common.Load("measure2"), m3 = Week7Common.Load("measure3_game");
        sb.AppendLine($"        [측정]   {Ok(m1 != "", "#1 실험 씬", "#1 없음 (1-4)")}  {Ok(m2 != "", "#2 방법 비교", "#2 없음 (2-1)")}  {Ok(m3 != "", "#3 우리 게임", "#3 없음 (5-1)")}");
        if (m3 != "")
        {
            // #3-1 플레이어 Accurate 점수가 Disabled보다 확실히 큰가 = 체크포인트의 핵심
            float dis = Pick(m3, "| 플레이어 | Disabled"), acc = Pick(m3, "| 플레이어 | Accurate");
            sb.AppendLine($"                 {Ok(acc - dis >= 8f, $"입체 반응 — 플레이어 입체감 점수 {dis:0.0} → {acc:0.0}", $"입체 반응이 약하다 — {dis:0.0} → {acc:0.0} (노멀 맵 세기 · Distance 확인)")}");
        }
        bool readme = System.IO.File.Exists(Week7Make.CAPTURES + "/week7_normalmap_before_after.png");
        sb.AppendLine($"        [README] {(readme ? "✓ 전·후 캡처" : "ℹ 전·후 캡처 없음 (5-2)")}");

        Debug.Log($"[검증] 7주차 체크포인트 — 경고 {warn}개\n" + sb +
                  "        체크포인트: 2D 라이트의 위치가 바뀔 때 스프라이트 표면 질감이 Normal Map에 의해 실시간 입체 반응을 일으키는가?");
    }

    /// <summary>측정 표에서 해당 줄의 마지막 굵은 숫자(**n**)를 읽는다.</summary>
    static float Pick(string table, string rowStart)
    {
        foreach (var line in table.Split('\n'))
        {
            if (!line.StartsWith(rowStart)) continue;
            var m = System.Text.RegularExpressions.Regex.Match(line, @"\*\*(-?[0-9.]+)\*\*");
            if (m.Success && float.TryParse(m.Groups[1].Value, out var v)) return v;
        }
        return 0;
    }
}
