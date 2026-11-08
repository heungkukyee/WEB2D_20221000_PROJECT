using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 6주차 2단계 — 팔레트 스튜디오.
///
/// 기준색 하나 + 조화 규칙 → 1·2·1 팔레트를 만들고, 그 자리에서 수치로 검증한다.
///   · 60 : 30 : 10 면적 막대
///   · 색마다 HEX · HSV · 흑백 밝기
///   · 명도 대비(WCAG) — 글자 4.5:1, 그래픽 3:1
///   · 흑백 / 적록 색약으로 봤을 때의 줄
/// 손으로 고친 색도 같은 검사를 받는다. "예쁘다"가 아니라 "기준을 통과했다"로 결정한다.
/// </summary>
public class PaletteStudioWindow : EditorWindow
{
    public const string DIR = "Assets/CG2Week6/Palette";
    public const string MY = DIR + "/MyPalette.asset";
    const string GROUND = "Assets/CG2Week4/Art/tile_ground_32.png";

    CG2Palette _p;
    Color _base = new Color32(0x2B, 0x2F, 0x3A, 0xFF);
    CG2Palette.Harmony _harmony = CG2Palette.Harmony.Complementary;
    Vector2 _scroll;

    [MenuItem("CG2 Lab/6주차/2. 팔레트 스튜디오 — 1·2·1 설계", priority = 20)]
    public static void Open()
    {
        var w = GetWindow<PaletteStudioWindow>("CG2 Palette Studio");
        w.minSize = new Vector2(520, 640);
        w._p = EnsureMyPalette();
        w._harmony = w._p.harmony;
    }

    public static CG2Palette EnsureMyPalette()
    {
        var p = AssetDatabase.LoadAssetAtPath<CG2Palette>(MY);
        if (p != null) return p;
        Directory.CreateDirectory(DIR);
        p = CreateInstance<CG2Palette>();
        p.themeName = "My Palette";
        // 처음에는 4주차 바닥 타일의 평균색에서 출발한다 — 이미 화면의 60%를 차지하는 색이기 때문이다.
        // 규칙은 보색(남색 바닥 → 금색 강조)으로 시작한다. 분할 보색은 이 바닥색에서 녹슨 주황 버튼 + 라임 강조가 나온다
        // — 수학적으로 맞아도 톤이 안 맞으면 규칙을 바꿔 본다(스튜디오에서 4가지를 비교).
        p.Generate(GroundColor(), CG2Palette.Harmony.Complementary);
        AssetDatabase.CreateAsset(p, MY);
        AssetDatabase.SaveAssets();
        return p;
    }

    public static Color GroundColor()
    {
        if (!File.Exists(GROUND)) return new Color32(0x2B, 0x2F, 0x3A, 0xFF);
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(GROUND));
        Color sum = Color.clear; int n = 0;
        foreach (var c in t.GetPixels()) if (c.a > 0.5f) { sum += c; n++; }
        DestroyImmediate(t);
        return n > 0 ? sum / n : Color.gray;
    }

    void OnGUI()
    {
        if (_p == null) _p = EnsureMyPalette();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("① 규칙으로 만들기", EditorStyles.boldLabel);
        _p = (CG2Palette)EditorGUILayout.ObjectField("팔레트 에셋", _p, typeof(CG2Palette), false);
        using (new EditorGUILayout.HorizontalScope())
        {
            _base = EditorGUILayout.ColorField("기준색 (주조)", _base);
            if (GUILayout.Button("바닥 타일에서 추출", GUILayout.Width(130))) _base = GroundColor();
        }
        _harmony = (CG2Palette.Harmony)EditorGUILayout.EnumPopup("조화 규칙", _harmony);
        EditorGUILayout.HelpBox(HarmonyHelp(_harmony), MessageType.None);
        if (GUILayout.Button("규칙으로 생성 (손으로 고친 색은 덮어쓴다)", GUILayout.Height(26)))
        {
            Undo.RecordObject(_p, "팔레트 생성");
            _p.Generate(_base, _harmony);
            EditorUtility.SetDirty(_p);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("② 손으로 다듬기", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var so = new SerializedObject(_p);
        foreach (var f in new[] { "themeName", "main", "sub1", "sub2", "accent", "danger", "text" })
            EditorGUILayout.PropertyField(so.FindProperty(f));
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            _p.NotifyChanged();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("③ 면적 60 : 30 : 10", EditorStyles.boldLabel);
        DrawAreaBar(_p);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("④ 색 정보 — 위: 원본 · 가운데: 흑백 · 아래: 적록 색약", EditorStyles.boldLabel);
        DrawSwatches(_p);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("⑤ 검증", EditorStyles.boldLabel);
        foreach (var (ok, line) in Checks(_p))
            EditorGUILayout.LabelField((ok ? "✓ " : "⚠ ") + line, ok ? EditorStyles.label : EditorStyles.boldLabel);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("저장", GUILayout.Height(28)))
            {
                EditorUtility.SetDirty(_p);
                AssetDatabase.SaveAssets();
                LogPalette(_p);
            }
            if (GUILayout.Button("씬에 적용 (3·4단계)", GUILayout.Height(28)))
                Week6Theme.ApplyAll();
        }
        EditorGUILayout.EndScrollView();
    }

    static string HarmonyHelp(CG2Palette.Harmony h) => h switch
    {
        CG2Palette.Harmony.Analogous => "유사색 — 주조 H, 보조 H+30°, 강조 H−30°. 차분하고 통일감. 강조가 덜 튀므로 명도·채도로 띄운다.",
        CG2Palette.Harmony.Complementary => "보색 — 보조 = 주조와 같은 색상, 강조 H+180°. 가장 강한 대비. 강조를 아껴 쓴다.",
        CG2Palette.Harmony.Triadic => "삼각 — 보조 H+120°, 강조 H+240°. 색이 다양하고 경쾌하다. 채도를 낮춰 정리한다.",
        _ => "분할 보색 — 보조 H+150°, 강조 H+210°. 보색의 대비 + 덜 거친 조합. 게임 UI에서 가장 무난하다.",
    };

    static void DrawAreaBar(CG2Palette p)
    {
        var r = GUILayoutUtility.GetRect(10, 36, GUILayout.ExpandWidth(true));
        float x = r.x;
        void Seg(Color c, float w, string label)
        {
            var s = new Rect(x, r.y, r.width * w, r.height);
            EditorGUI.DrawRect(s, c);
            var st = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = CG2Palette.Luminance(c) > 0.3f ? Color.black : Color.white;
            GUI.Label(s, label, st);
            x += s.width;
        }
        Seg(p.main, 0.60f, "Main 60%");
        Seg(p.sub1, 0.15f, "Sub1 15%");
        Seg(p.sub2, 0.15f, "Sub2 15%");
        Seg(p.accent, 0.10f, "Acc 10%");
    }

    static void DrawSwatches(CG2Palette p)
    {
        var roles = Roles(p);
        var r = GUILayoutUtility.GetRect(10, 150, GUILayout.ExpandWidth(true));
        float w = r.width / roles.Count;
        var st = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, wordWrap = true };
        for (int i = 0; i < roles.Count; i++)
        {
            var (name, c) = roles[i];
            float x = r.x + i * w;
            EditorGUI.DrawRect(new Rect(x + 2, r.y, w - 4, 34), c);
            float g = CG2Palette.Luma(c);
            EditorGUI.DrawRect(new Rect(x + 2, r.y + 36, w - 4, 20), new Color(g, g, g));
            EditorGUI.DrawRect(new Rect(x + 2, r.y + 58, w - 4, 20), CG2Palette.Deuteranopia(c));
            Color.RGBToHSV(c, out float h, out float s, out float v);
            GUI.Label(new Rect(x, r.y + 80, w, 70),
                $"{name}\n{CG2Palette.Hex(c)}\nH{h * 360:0}° S{s * 100:0} V{v * 100:0}\n흑백 {g * 100:0}%", st);
        }
    }

    public static List<(string, Color)> Roles(CG2Palette p) => new()
    {
        ("Main", p.main), ("Sub1", p.sub1), ("Sub2", p.sub2), ("Accent", p.accent), ("Danger", p.danger), ("Text", p.text),
    };

    /// <summary>창과 아트 가이드 문서가 같은 검사를 쓴다.</summary>
    public static List<(bool, string)> Checks(CG2Palette p)
    {
        var list = new List<(bool, string)>();
        void C(Color a, Color b, float need, string what)
        {
            float r = CG2Palette.Contrast(a, b);
            list.Add((r >= need, $"{what} {r:F2}:1 (기준 {need}:1)"));
        }
        C(p.text, p.sub1, 4.5f, "글자 / 창(Sub1)");
        C(p.text, p.ButtonNormal, 4.5f, "글자 / 버튼(Sub2)");
        C(p.accent, p.main, 3f, "강조 / 바닥(Main)");
        C(p.accent, p.sub1, 3f, "강조 제목 / 창(Sub1)");
        C(p.danger, p.main, 3f, "체력바(Danger) / 바닥");

        // 명도 위계 — 흑백으로 봐도 바닥 < 창 < 버튼 < 강조 순서가 유지되는가
        float m = CG2Palette.Luma(p.main), s1 = CG2Palette.Luma(p.sub1), s2 = CG2Palette.Luma(p.sub2), a = CG2Palette.Luma(p.accent);
        list.Add((m < s1 && s1 < s2 && s2 < a, $"명도 위계 Main {m * 100:0} < Sub1 {s1 * 100:0} < Sub2 {s2 * 100:0} < Accent {a * 100:0} (흑백 %)"));

        // 강조(보상·경험치)와 위험(체력)이 색약에게 같은 색으로 보이면 둘이 섞인다.
        // 빨강–노랑–초록은 적록 색약에게 모두 노란 계열로 보인다. 색상이 같아지면 명도 차와 모양(아이콘)만 남는다.
        var da = CG2Palette.Deuteranopia(p.accent);
        var dd = CG2Palette.Deuteranopia(p.danger);
        float hueDiff = Mathf.Abs(Mathf.DeltaAngle(CG2Palette.Hue(da) * 360f, CG2Palette.Hue(dd) * 360f));
        float lumaDiff = Mathf.Abs(CG2Palette.Luma(da) - CG2Palette.Luma(dd));
        list.Add((hueDiff > 30f || lumaDiff > 0.25f,
            hueDiff > 30f
                ? $"적록 색약에서 강조↔위험 색상 차 {hueDiff:0}° (기준 30° 초과)"
                : $"적록 색약에서 강조↔위험 색상 차 {hueDiff:0}° — 색으로는 구분 안 됨, 명도 차 {lumaDiff * 100:0}%로만 구분 (기준 25%) → 아이콘(♥/◆) 같은 모양 단서를 함께 쓴다"));

        // 상태 규칙 — Hover와 Pressed가 Normal과 구분되는가
        float hv = CG2Palette.Luma(p.ButtonHighlighted) - CG2Palette.Luma(p.ButtonNormal);
        float pr = CG2Palette.Luma(p.ButtonNormal) - CG2Palette.Luma(p.ButtonPressed);
        list.Add((hv > 0.05f && pr > 0.05f, $"버튼 상태 명도 차 Hover +{hv * 100:0}% · Pressed −{pr * 100:0}% (각 5% 초과)"));
        return list;
    }

    public static void LogPalette(CG2Palette p)
    {
        var rows = new List<string>();
        foreach (var (name, c) in Roles(p))
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            rows.Add($"| {name} | {CG2Palette.Hex(c)} | {h * 360:0}° | {s * 100:0} | {v * 100:0} | {CG2Palette.Luma(c) * 100:0}% |");
        }
        int warn = 0;
        var checks = new List<string>();
        foreach (var (ok, line) in Checks(p)) { checks.Add((ok ? "✓ " : "⚠ ") + line); if (!ok) warn++; }
        Debug.Log($"[검증] 팔레트 '{p.themeName}' ({p.harmony}) — 경고 {warn}개\n" +
                  "| 역할 | HEX | H | S | V | 흑백 |\n|---|---|---|---|---|---|\n" + string.Join("\n", rows) +
                  "\n        " + string.Join("\n        ", checks));
    }
}
