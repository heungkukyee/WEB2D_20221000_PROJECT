using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 7주차 3단계 — Sprite Editor → Secondary Textures에 _NormalMap 연결.
///   플레이어는 학생이 Sprite Editor에서 직접 연결하고(3-1로 검사), 나머지는 3-2로 한 번에 연결한다.
/// 연결 규칙 (하나라도 틀리면 화면에 아무 변화가 없다 — 오류 메시지도 없다)
///   ① 이름이 정확히 "_NormalMap" (대소문자 · 밑줄까지)   ② 텍스처가 들어 있다
///   ③ 노멀 맵 sRGB OFF   ④ 원본 그림과 같은 크기   ⑤ Filter Point (픽셀 아트)
/// </summary>
public static class Week7Link
{
    public const string NAME = "_NormalMap";

    public struct Check
    {
        public string role, png, linkName, normal, problem;
        public bool ok;
    }

    public static Check Inspect(string role, string png)
    {
        var c = new Check { role = role, png = png, linkName = "—", normal = "—" };
        var ti = AssetImporter.GetAtPath(png) as TextureImporter;
        var sec = ti?.secondarySpriteTextures;
        if (sec == null || sec.Length == 0) { c.problem = "연결 없음"; return c; }

        SecondarySpriteTexture? hit = null, near = null;
        foreach (var s in sec)
        {
            if (s.name == NAME) hit = s;
            else if (s.name != null && s.name.Replace("_", "").ToLower() == "normalmap") near = s;
        }
        if (hit == null)
        {
            c.linkName = near?.name ?? sec[0].name;
            c.problem = near != null ? $"이름 오타 '{near.Value.name}' → '{NAME}'" : $"이름이 '{sec[0].name}' → '{NAME}'";
            return c;
        }
        c.linkName = NAME;
        var tex = hit.Value.texture;
        if (tex == null) { c.problem = "텍스처 칸이 비어 있음"; return c; }
        string np = AssetDatabase.GetAssetPath(tex);
        c.normal = Path.GetFileName(np);
        var nti = AssetImporter.GetAtPath(np) as TextureImporter;
        var src = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        var problems = new List<string>();
        if (nti != null && nti.sRGBTexture) problems.Add("sRGB ON → OFF");
        if (src != null && (src.width != tex.width || src.height != tex.height)) problems.Add($"크기 {tex.width}×{tex.height} ≠ 원본 {src.width}×{src.height}");
        if (nti != null && nti.filterMode != FilterMode.Point) problems.Add("Filter → Point");
        c.problem = string.Join(" · ", problems);
        c.ok = problems.Count == 0;
        return c;
    }

    static string Row(Check c) => $"| {c.role} | {Path.GetFileName(c.png)} | {c.linkName} | {c.normal} | {(c.ok ? "✓" : "⚠ " + c.problem)} |";
    const string HEAD = "| 그림 | 스프라이트 | 보조 텍스처 이름 | 노멀 맵 | 판정 |\n|---|---|---|---|---|\n";

    [MenuItem(Week7Common.MENU + "3-1. 연결 검사 — 플레이어 (Sprite Editor에서 직접 연결한 뒤)", priority = 31)]
    public static void CheckPlayer()
    {
        var t = Week7Make.GameTargets().Find(x => x.role == "플레이어");
        if (t == null) { EditorUtility.DisplayDialog("연결 검사", "게임 씬(SampleScene)에서 실행하세요.", "확인"); return; }
        var c = Inspect(t.role, t.png);
        Debug.Log("[검증] 3-1 플레이어 노멀 맵 연결\n" + HEAD + Row(c) + "\n" +
                  (c.ok ? "        ✓ 연결 완료 — 아직 화면 변화는 없다. 노멀 맵에 반응하는 조명이 없기 때문(4단계)."
                        : "        ⚠ 고칠 곳: " + c.problem + "\n        Sprite Editor → 왼쪽 위 드롭다운 'Secondary Textures' → + → Name '_NormalMap' · Texture에 _n.png 끌어 넣기 → Apply"));
    }

    [MenuItem(Week7Common.MENU + "3-2. 나머지 한 번에 연결 + 전체 검사", priority = 32)]
    public static void LinkRest()
    {
        var targets = Week7Make.GameTargets();
        if (targets.Count == 0) { EditorUtility.DisplayDialog("연결", "게임 씬(SampleScene)에서 실행하세요.", "확인"); return; }
        var sb = new StringBuilder("[검증] 3-2 노멀 맵 연결 — 게임에 쓰이는 그림 전체\n" + HEAD);
        int ok = 0, linked = 0;
        foreach (var t in targets)
        {
            string np = NormalMapGen.NormalPathFor(t.png);
            var before = Inspect(t.role, t.png);
            if (t.role != "플레이어" && !before.ok && File.Exists(np))
            {
                Week7Lab.Link(t.png, np);
                linked++;
            }
            var c = Inspect(t.role, t.png);
            if (c.ok) ok++;
            sb.AppendLine(Row(c));
        }
        sb.Append($"        {ok}/{targets.Count} 연결 완료 (이번에 연결 {linked}개)" +
                  (ok < targets.Count ? " — ⚠ 줄의 안내대로 고친다. 플레이어는 3-1처럼 Sprite Editor에서 직접." : " ✓"));
        Debug.Log(sb.ToString());
    }

    [MenuItem(Week7Common.MENU + "되돌리기 — 2·3단계 노멀 맵 (연결 해제 · _n.png 삭제)", priority = 91)]
    static void Unlink()
    {
        if (Week7Common.Busy("되돌리기")) return;
        var targets = Week7Make.GameTargets();
        if (targets.Count == 0) { EditorUtility.DisplayDialog("되돌리기", "게임 씬(SampleScene)에서 실행하세요.", "확인"); return; }
        int n = 0, files = 0;
        foreach (var t in targets)
        {
            var ti = AssetImporter.GetAtPath(t.png) as TextureImporter;
            if (ti != null && ti.secondarySpriteTextures != null && ti.secondarySpriteTextures.Length > 0)
            {
                ti.secondarySpriteTextures = new SecondarySpriteTexture[0];
                ti.SaveAndReimport();
                n++;
            }
            string np = NormalMapGen.NormalPathFor(t.png);
            if (File.Exists(np) && AssetDatabase.DeleteAsset(np)) files++;
        }
        Debug.Log($"[되돌리기] 노멀 맵 연결 해제 {n}개 · _n.png 삭제 {files}개");
    }
}
