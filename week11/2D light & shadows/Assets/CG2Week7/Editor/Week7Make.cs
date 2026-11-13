using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 7주차 2단계 — 노멀 맵 만들기.
///   2-1 측정 #2 : 같은 그림에서 방법 3가지(밝기 · 외곽 · 혼합)로 만들고 숫자로 비교한다
///   2-2 스튜디오 : 플레이어 노멀 맵을 직접 만든다 (방법 · 세기 · 경사 · 디테일 조절, 미리보기)
///   2-3 일괄     : 나머지(적 · 투사체 · 벽 · 바닥)를 권장 설정으로 한 번에 만든다
/// </summary>
public static class Week7Make
{
    public const string CAPTURES = Week7Common.ROOT + "/Captures";

    /// <summary>게임에 쓰이는 그림과 권장 설정. 캐릭터 = 혼합, 이어지는 타일 = 밝기.</summary>
    public class Target
    {
        public string role, png;
        public NormalMapGen.Settings settings;
    }

    public static List<Target> GameTargets()
    {
        var list = new List<Target>();
        var seen = new HashSet<string>();
        void Add(string role, Sprite s, NormalMapGen.Settings st)
        {
            if (s == null) return;
            string p = AssetDatabase.GetAssetPath(s);
            if (string.IsNullOrEmpty(p) || !p.EndsWith(".png") || !seen.Add(p)) return;
            list.Add(new Target { role = role, png = p, settings = st });
        }

        var player = GameObject.FindWithTag("Player");
        Add("플레이어", player ? player.GetComponent<SpriteRenderer>()?.sprite : null, new NormalMapGen.Settings());
        Add("적", Prefab("Assets/StarterProject/Prefabs/Enemy.prefab"), new NormalMapGen.Settings());
        Add("투사체", Prefab("Assets/StarterProject/Prefabs/Projectile.prefab"), new NormalMapGen.Settings { bevel = 6 });
        foreach (var (name, role, strength) in new[] { ("Tilemap_Wall", "벽 타일", 4f), ("Tilemap_Ground", "바닥 타일", 2f) })
        {
            var go = GameObject.Find(name);
            var tm = go ? go.GetComponent<Tilemap>() : null;
            if (tm == null) continue;
            var sprites = new Sprite[tm.GetUsedSpritesCount()];
            tm.GetUsedSpritesNonAlloc(sprites);
            foreach (var s in sprites)
                Add(role, s, new NormalMapGen.Settings { method = NormalMapGen.Method.밝기, strength = strength, edgeIsAir = false });
        }
        return list;
    }

    static Sprite Prefab(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return go ? go.GetComponent<SpriteRenderer>()?.sprite : null;
    }

    // ────────────────────────────────────────────────────────── 2-1 측정 #2

    [MenuItem(Week7Common.MENU + "2-1. 측정 #2 — 노멀 맵 만드는 방법 3가지 비교", priority = 21)]
    public static void CompareMethods()
    {
        var targets = GameTargets();
        if (targets.Count == 0) { EditorUtility.DisplayDialog("측정 #2", "게임 씬(SampleScene)에서 실행하세요.", "확인"); return; }
        Directory.CreateDirectory(CAPTURES);

        var def = new NormalMapGen.Settings();
        var sb = new StringBuilder($"[측정] #2 노멀 맵 만드는 방법 비교 — 세 방법 모두 세기 {def.strength:0} · 경사 {def.bevel}px · 디테일 {def.detail:0.0#}\n");
        sb.AppendLine("| 그림 | 방법 | 평균 기울기 | 울퉁불퉁 (안쪽 30° 넘게 기운 픽셀) | 빛 ← 일 때 왼쪽 − 오른쪽 (캐릭터만) |");
        sb.AppendLine("|---|---|---|---|---|");
        var shown = new List<string>();
        foreach (var t in targets)
        {
            if (t.role == "투사체" || t.role == "바닥 타일") continue;   // 표가 길어지지 않게 대표만
            var tex = NormalMapGen.LoadPng(t.png);
            var px = tex.GetPixels32();
            var rows = new List<(NormalMapGen.Method, Color32[])>();
            foreach (NormalMapGen.Method m in System.Enum.GetValues(typeof(NormalMapGen.Method)))
            {
                var s = new NormalMapGen.Settings { method = m, edgeIsAir = t.settings.edgeIsAir };
                var n = NormalMapGen.Make(px, tex.width, tex.height, s);
                var st = Stats(px, n, tex.width, tex.height);
                sb.AppendLine($"| {t.role} | {m} | {st.tilt:0}° | {st.bumpy:0}% | {(t.settings.edgeIsAir ? st.lr.ToString("+0;-0") : "—")} |");
                rows.Add((m, n));
            }
            string img = $"{CAPTURES}/method_{Path.GetFileNameWithoutExtension(t.png)}.png";
            WriteSheet(img, px, tex.width, tex.height, rows);
            shown.Add(img);
            Object.DestroyImmediate(tex);
        }
        AssetDatabase.Refresh();
        sb.AppendLine("        평균 기울기: 표면이 정면에서 평균 몇 도 기울었나 · 울퉁불퉁: 안쪽 픽셀 중 30° 넘게 기운 비율 · 왼쪽 − 오른쪽: 미리보기 명암 차(클수록 입체적)");
        sb.Append("        비교 그림: " + string.Join(", ", shown) + "  (줄 = 밝기 · 외곽 · 혼합, 칸 = 원본 · 노멀 맵 · 빛 ← · 빛 →)");
        Week7Common.Save("measure2", sb.ToString());
        Debug.Log(sb.ToString());
    }

    public struct MapStats { public float tilt, bumpy, lr; }

    public static MapStats Stats(Color32[] px, Color32[] n, int w, int h)
    {
        double tilt = 0, lsum = 0, rsum = 0; int cnt = 0, inner = 0, bumpy = 0, cl = 0, cr = 0;
        var lit = NormalMapGen.Shade(px, n, new Vector3(-1, 0, 1));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (px[i].a < 128) continue;
                var d = NormalMapGen.Decode(n[i]).normalized;
                float ang = Mathf.Acos(Mathf.Clamp(d.z, -1, 1)) * Mathf.Rad2Deg;
                tilt += ang; cnt++;
                if (!NearAir(px, w, h, x, y, 2)) { inner++; if (ang > 30) bumpy++; }
                float g = Week7Common.Gray(lit[i]);
                if (x < w / 2) { lsum += g; cl++; } else { rsum += g; cr++; }
            }
        return new MapStats
        {
            tilt = cnt > 0 ? (float)(tilt / cnt) : 0,
            bumpy = inner > 0 ? 100f * bumpy / inner : 0,
            lr = cl > 0 && cr > 0 ? (float)(lsum / cl - rsum / cr) : 0,
        };
    }

    static bool NearAir(Color32[] px, int w, int h, int x, int y, int r)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= w || yy >= h || px[yy * w + xx].a < 128) return true;
            }
        return false;
    }

    /// <summary>비교 그림: 줄 = 방법, 칸 = 원본 · 노멀 맵 · 빛 왼쪽 · 빛 오른쪽 (확대해서 픽셀이 보이게).</summary>
    public static void WriteSheet(string path, Color32[] px, int w, int h, List<(NormalMapGen.Method m, Color32[] n)> rows)
    {
        int S = Mathf.Max(1, 256 / Mathf.Max(w, h));
        var sheet = new Texture2D(w * 4 * S, h * rows.Count * S, TextureFormat.RGBA32, false);
        var fill = new Color32[sheet.width * sheet.height];
        for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(36, 38, 46, 255);
        sheet.SetPixels32(fill);
        for (int r = 0; r < rows.Count; r++)
        {
            var n = rows[r].n;
            var cols = new[] { px, n, NormalMapGen.Shade(px, n, new Vector3(-1, 0, 1)), NormalMapGen.Shade(px, n, new Vector3(1, 0, 1)) };
            for (int c = 0; c < 4; c++)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var v = cols[c][y * w + x];
                        if (c != 1 && v.a < 128) continue;
                        for (int sy = 0; sy < S; sy++)
                            for (int sx = 0; sx < S; sx++)
                                sheet.SetPixel(c * w * S + x * S + sx, (rows.Count - 1 - r) * h * S + y * S + sy, v);
                    }
        }
        sheet.Apply();
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
    }

    // ────────────────────────────────────────────────────────── 2-3 일괄

    [MenuItem(Week7Common.MENU + "2-3. 나머지 노멀 맵 한 번에 만들기 (적 · 투사체 · 벽 · 바닥)", priority = 23)]
    public static void MakeRest()
    {
        var targets = GameTargets();
        if (targets.Count == 0) { EditorUtility.DisplayDialog("노멀 맵 일괄", "게임 씬(SampleScene)에서 실행하세요.", "확인"); return; }
        var sb = new StringBuilder("[실습] 2-3 노멀 맵 일괄 만들기 — 그림과 같은 폴더에 '_n.png'\n");
        sb.AppendLine("| 그림 | 파일 | 크기 | 설정 | 결과 |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var t in targets)
        {
            string np = NormalMapGen.NormalPathFor(t.png);
            if (t.role == "플레이어" && File.Exists(np))
            {
                sb.AppendLine($"| {t.role} | {Path.GetFileName(np)} | — | 2-2에서 직접 만든 것 | 그대로 둠 |");
                continue;
            }
            var tex = NormalMapGen.LoadPng(t.png);
            NormalMapGen.Save(np, NormalMapGen.Make(tex.GetPixels32(), tex.width, tex.height, t.settings), tex.width, tex.height);
            sb.AppendLine($"| {t.role} | {Path.GetFileName(np)} | {tex.width}×{tex.height} | {t.settings} | 만듦 |");
            Object.DestroyImmediate(tex);
        }
        sb.Append("        모두 sRGB OFF · Point · 압축 없음 · 원본과 같은 크기. ── 다음 ── 3단계: Sprite Editor에서 연결");
        Debug.Log(sb.ToString());
    }
}

/// <summary>2-2 노멀 맵 스튜디오 — 그림 하나를 골라 방법과 숫자를 바꿔 가며 미리보기로 확인하고 저장한다.</summary>
public class NormalMapStudioWindow : EditorWindow
{
    Texture2D _sprite;
    NormalMapGen.Settings _s = new();
    float _lightAngle = 180f;
    Texture2D _albedo, _normal, _lit;
    Color32[] _px, _n;
    Week7Make.MapStats _stats;

    [MenuItem(Week7Common.MENU + "2-2. 노멀 맵 스튜디오 — 플레이어 노멀 맵 직접 만들기", priority = 22)]
    public static void Open()
    {
        var w = GetWindow<NormalMapStudioWindow>("노멀 맵 스튜디오");
        w.minSize = new Vector2(560, 520);
        var player = GameObject.FindWithTag("Player");
        var sr = player ? player.GetComponent<SpriteRenderer>() : null;
        if (sr != null && sr.sprite != null) w.SetSprite(sr.sprite.texture);
    }

    // 스크립트를 다시 컴파일하면 미리보기 텍스처가 사라진다 → 창이 다시 켜질 때 새로 만든다
    void OnEnable() { if (_sprite != null) Rebuild(); }

    void SetSprite(Texture2D t)
    {
        _sprite = t;
        _s.edgeIsAir = !AssetDatabase.GetAssetPath(t).Contains("tile");
        Rebuild();
    }

    void Rebuild()
    {
        if (_sprite == null) return;
        var src = NormalMapGen.LoadPng(AssetDatabase.GetAssetPath(_sprite));
        int w = src.width, h = src.height;
        _px = src.GetPixels32();
        DestroyImmediate(src);
        _n = NormalMapGen.Make(_px, w, h, _s);
        _stats = Week7Make.Stats(_px, _n, w, h);
        _albedo = Make(_albedo, w, h, _px);
        _normal = Make(_normal, w, h, _n);
        Relight();
    }

    void Relight()
    {
        if (_px == null) return;
        float a = _lightAngle * Mathf.Deg2Rad;
        _lit = Make(_lit, _albedo.width, _albedo.height, NormalMapGen.Shade(_px, _n, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f)));
    }

    static Texture2D Make(Texture2D t, int w, int h, Color32[] px)
    {
        if (t == null || t.width != w || t.height != h) t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("① 그림 고르기", EditorStyles.boldLabel);
        var t = (Texture2D)EditorGUILayout.ObjectField("스프라이트 텍스처", _sprite, typeof(Texture2D), false);
        if (t != _sprite) SetSprite(t);
        if (_sprite == null) { EditorGUILayout.HelpBox("게임 씬에서 열면 플레이어 그림이 자동으로 들어온다.", MessageType.Info); return; }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("② 방법과 숫자", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _s.method = (NormalMapGen.Method)EditorGUILayout.EnumPopup("방법", _s.method);
        _s.strength = EditorGUILayout.Slider("세기 (명암 강도)", _s.strength, 0.5f, 12f);
        if (_s.method != NormalMapGen.Method.밝기) _s.bevel = EditorGUILayout.IntSlider("경사 (가장자리 px)", _s.bevel, 1, 24);
        if (_s.method == NormalMapGen.Method.혼합) _s.detail = EditorGUILayout.Slider("디테일 (밝기 비율)", _s.detail, 0f, 1f);
        _s.edgeIsAir = EditorGUILayout.Toggle("그림 바깥 = 빈 곳 (캐릭터)", _s.edgeIsAir);
        if (EditorGUI.EndChangeCheck()) Rebuild();

        EditorGUI.BeginChangeCheck();
        _lightAngle = EditorGUILayout.Slider("미리보기 빛 방향 (°, 180 = 왼쪽)", _lightAngle, 0f, 360f);
        if (EditorGUI.EndChangeCheck()) Relight();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("③ 미리보기 — 원본 · 노멀 맵 · 빛을 받은 모습", EditorStyles.boldLabel);
        float size = Mathf.Min(170, (position.width - 40) / 3);
        var r = GUILayoutUtility.GetRect(position.width, size + 4);
        int i = 0;
        foreach (var tex in new[] { _albedo, _normal, _lit })
        {
            var cell = new Rect(10 + i++ * (size + 10), r.y, size, size);
            EditorGUI.DrawRect(cell, new Color(0.14f, 0.15f, 0.18f));
            if (tex != null) GUI.DrawTexture(cell, tex, ScaleMode.ScaleToFit);
        }
        EditorGUILayout.HelpBox($"평균 기울기 {_stats.tilt:0}° · 울퉁불퉁 {_stats.bumpy:0}% · 빛 ← 일 때 왼쪽 − 오른쪽 {_stats.lr:+0;-0}\n" +
                                "울퉁불퉁이 크면 선(외곽선 · 눈 · 무늬)마다 홈이 파인다. 캐릭터는 10~25%, 벽돌은 커도 된다.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("④ 저장", EditorStyles.boldLabel);
        string np = NormalMapGen.NormalPathFor(AssetDatabase.GetAssetPath(_sprite));
        EditorGUILayout.LabelField("저장 위치", np);
        if (GUILayout.Button("저장 — sRGB OFF · Point · 압축 없음", GUILayout.Height(28)))
        {
            NormalMapGen.Save(np, _n, _albedo.width, _albedo.height);
            var st = Week7Make.Stats(_px, _n, _albedo.width, _albedo.height);
            Debug.Log($"[실습] 2-2 노멀 맵 저장 — {np} ({_albedo.width}×{_albedo.height})\n" +
                      $"        설정 {_s} · 평균 기울기 {st.tilt:0}° · 울퉁불퉁 {st.bumpy:0}%\n" +
                      "        ── 다음 ── 2-3. 나머지 한 번에 만들기 → 3단계 Sprite Editor 연결");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(np));
        }
    }
}
