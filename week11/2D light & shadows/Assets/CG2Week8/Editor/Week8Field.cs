using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using C = Week8Common;

/// <summary>
/// 8주차 1단계 — 어두운 필드.
///   1-2 그림 다시 칠하기 : 바닥 · 벽 PNG의 픽셀만 밝게 바꾼다. 슬라이스 · 타일 · _NormalMap 연결은 그대로다.
///                          2D 조명은 그림 × 빛 곱셈이다. 그림이 어두우면 빛을 세게 줘도 화면이 밝아지지 않는다.
///   테마 Apply Scene Tone 끄기 : 켜 두면 Play 시작 때 전역 조명 색과 타일 색을 테마가 다시 덮어쓴다.
/// </summary>
public static class Week8Field
{
    [MenuItem(C.MENU + "1-2. 그림 다시 칠하기 — 바닥 · 벽을 밝은 돌로", priority = 12)]
    public static void Repaint()
    {
        if (C.Busy("그림 다시 칠하기")) return;
        Directory.CreateDirectory(C.BACKUP);
        var sb = new StringBuilder("[실습] 1-2 그림 다시 칠하기 — 픽셀만 바꾼다 (슬라이스 · 타일 · 노멀 맵 연결은 그대로)\n| 그림 | 반사율 전 | 반사율 후 | 원본 백업 |\n|---|---|---|---|\n");
        sb.AppendLine(Paint("바닥", C.GROUND_PNG, new Color32(178, 164, 140, 255), new Color32(214, 200, 174, 255)));
        sb.AppendLine(Paint("벽", C.WALL_PNG, new Color32(96, 94, 102, 255), new Color32(216, 216, 226, 255)));

        var theme = Object.FindAnyObjectByType<CG2Theme>();
        if (theme != null)
        {
            Undo.RecordObject(theme, "테마 씬 톤 끄기");
            theme.applySceneTone = false;
            EditorUtility.SetDirty(theme);
        }
        foreach (var tm in Object.FindObjectsByType<Tilemap>())
        {
            Undo.RecordObject(tm, "타일 틴트 끄기");
            tm.color = Color.white;
            EditorUtility.SetDirty(tm);
        }
        sb.Append("        테마 Apply Scene Tone 끔 · 타일맵 색 흰색 — 이제 어둠과 색은 조명이 정한다\n" +
                  "        ── 다음 ── (직접) Global Light 2D · CG2 Entity Light 의 Color와 Intensity를 바꾼다 → 1-3");
        Debug.Log(sb.ToString());
    }

    static string Paint(string role, string png, Color32 low, Color32 high)
    {
        string name = Path.GetFileName(png), bak = $"{C.BACKUP}/{name}.bytes";
        float before = C.Albedo(png);
        bool first = !File.Exists(bak);
        if (first) File.Copy(png, bak);
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        src.LoadImage(File.ReadAllBytes(bak));     // 항상 원본에서 다시 칠한다 → 두 번 눌러도 결과가 같다
        var px = src.GetPixels32();
        var hist = new int[256];
        int total = 0;
        foreach (var c in px) if (c.a >= 128) { hist[(int)C.Gray(c)]++; total++; }
        int lo = C.Percentile(hist, total, 0.02f), hi = C.Percentile(hist, total, 0.98f);
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 128) continue;
            float t = Mathf.Clamp01((C.Gray(px[i]) - lo) / Mathf.Max(1, hi - lo));
            var c = Color32.Lerp(low, high, t);
            c.a = px[i].a;
            px[i] = c;
        }
        src.SetPixels32(px);
        src.Apply();
        File.WriteAllBytes(png, src.EncodeToPNG());
        Object.DestroyImmediate(src);
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
        return $"| {role} {name} | {before * 100f:0.0}% | {C.Albedo(png) * 100f:0.0}% | {(first ? "만듦" : "이미 있음")} |";
    }

    [MenuItem(C.MENU + "되돌리기 — 1단계 그림 · 테마 (원본 그림 복구)", priority = 91)]
    public static void Revert()
    {
        if (C.Busy("되돌리기")) return;
        int n = 0;
        foreach (var png in new[] { C.GROUND_PNG, C.WALL_PNG })
        {
            string bak = $"{C.BACKUP}/{Path.GetFileName(png)}.bytes";
            if (!File.Exists(bak)) continue;
            File.Copy(bak, png, true);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            n++;
        }
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        if (theme != null)
        {
            theme.applySceneTone = true;
            EditorUtility.SetDirty(theme);
            theme.enabled = false;   // OnEnable에서 테마를 다시 칠한다
            theme.enabled = true;
        }
        Debug.Log($"[되돌리기] 원본 그림 {n}장 복구 · 테마 Apply Scene Tone 켬");
    }
}
