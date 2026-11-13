using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// 6주차 2~4단계 메뉴 — 색 감사 · 흑백/색약 미리보기 · UI 테마 · 씬 톤 · 테마 스왑 · 비교 · 되돌리기.
///
/// 실제로 칠하는 일은 씬의 CG2Theme 컴포넌트가 한다(Play 중 T 키 스왑, 팔레트 수정 즉시 반영).
/// 여기서는 그 컴포넌트를 놓고, 처음 적용하기 전의 5주차 색을 백업해 두었다가 비교·되돌리기에 쓴다.
/// </summary>
public static class Week6Theme
{
    const string THEME = "CG2 Theme";
    const string PRESET_DIR = PaletteStudioWindow.DIR + "/Presets";
    const string BACKUP = "Assets/CG2Week6/Backup/week6_before_theme.json";
    const string CAPTURE_DIR = "Assets/CG2Week6/Captures";

    // ────────────────────────────────────────────────────────── 2-0. 색 감사

    [MenuItem("CG2 Lab/6주차/2-0. 색 감사 — 지금 씬에 칠해진 색 세기", priority = 19)]
    public static void Audit()
    {
        var used = new Dictionary<string, List<string>>();
        void Add(Color c, string where)
        {
            if (c.a < 0.01f) return;
            string hex = CG2Palette.Hex(c);
            if (hex == "#FFFFFF") return;   // 흰색 = 곱셈 항등원(원본 그대로). "칠한 색"이 아니다
            if (!used.TryGetValue(hex, out var l)) used[hex] = l = new List<string>();
            l.Add(where);
        }

        foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include))
            if (g is not TMP_Text && g.enabled) Add(g.color, g.name);   // 꺼진 그래픽(평소의 포커스 링)은 화면에 없다
        foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include))
            if (s.transition == Selectable.Transition.ColorTint && s.colors.normalColor != Color.white)
                Add(s.colors.normalColor, s.name + "(Normal)");
        foreach (var t in Object.FindObjectsByType<Tilemap>()) Add(t.color, t.name);
        foreach (var l in Object.FindObjectsByType<Light2D>()) Add(l.color, l.name);
        if (Camera.main != null) Add(Camera.main.backgroundColor, "Camera 배경");

        // 출처 — 팔레트(또는 팔레트에서 계산한 상태색·톤)에서 나온 색인가, 누군가 손으로 적은 색인가
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        bool themed = theme != null && theme.enabled && theme.palette != null;
        var source = themed ? DerivedColors(theme) : new Dictionary<string, string>();
        int hard = used.Keys.Count(k => !source.ContainsKey(k));

        var rows = used.OrderByDescending(kv => kv.Value.Count)
                       .Select(kv => $"| {kv.Key} | {(source.TryGetValue(kv.Key, out var role) ? "팔레트 · " + role : "하드코딩")} | {kv.Value.Count} | " +
                                     $"{string.Join(", ", kv.Value.Distinct().Take(4))}{(kv.Value.Distinct().Count() > 4 ? " …" : "")} |");
        Debug.Log($"[측정] 색 감사 — 서로 다른 색 {used.Count}개 중 하드코딩 {hard}개 (흰색 제외)" +
                  (themed ? $" · CG2 Theme '{theme.palette.themeName}'" : " · 팔레트 없음") + "\n" +
                  "| 색 | 출처 | 쓰인 곳 수 | 어디 |\n|---|---|---|---|\n" + string.Join("\n", rows) +
                  (themed
                      ? "\n        모든 색이 팔레트 에셋 하나에서 나온다 → 팔레트를 바꾸면 전부 따라 바뀐다(4-2 테마 바꾸기)."
                      : "\n        누가 이 색들을 정했는가? 다 같은 게임처럼 보이는가? → 2. 팔레트 스튜디오에서 기준을 만든다."));
        Week6Common.Save(themed ? "audit_after" : "audit_before", $"{used.Count}|{hard}");
    }

    /// <summary>CG2Theme이 팔레트에서 계산해 칠하는 색 목록 (HEX → 역할 이름).</summary>
    static Dictionary<string, string> DerivedColors(CG2Theme t)
    {
        var p = t.palette;
        var d = new Dictionary<string, string>();
        void A(Color c, string role) { var h = CG2Palette.Hex(c); if (!d.ContainsKey(h)) d[h] = role; }
        A(p.main, "Main"); A(p.sub1, "Sub1"); A(p.sub2, "Sub2"); A(p.accent, "Accent"); A(p.danger, "Danger"); A(p.text, "Text");
        A(p.ButtonHighlighted, "Hover"); A(p.ButtonPressed, "Pressed"); A(p.ButtonDisabled, "Disabled");
        A(p.Ambient, "환경광"); A(CG2Palette.ShiftValue(p.main, -0.05f), "바 배경");
        A(Color.Lerp(Color.white, p.Ambient, t.characterTint), "캐릭터 조명");
        A(Color.Lerp(Color.white, CG2Palette.HSV(CG2Palette.Hue(p.main), 0.5f, 1f), t.groundTint), "Ground 틴트");
        A(Color.Lerp(Color.white, CG2Palette.HSV(CG2Palette.Hue(p.sub1), 0.4f, 1f), t.wallTint), "Wall 틴트");
        return d;
    }

    // ────────────────────────────────────────────────────────── 2-1. 흑백·색약 미리보기

    [MenuItem("CG2 Lab/6주차/2-1. 흑백·색약 미리보기 캡처 (Play 중)", priority = 21)]
    public static void CapturePreview()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("미리보기 캡처", "Play 중에 실행하세요. 적이 몇 마리 나온 뒤가 좋습니다.", "확인");
            return;
        }
        // Overlay UI까지 담으려면 프레임이 끝난 뒤 화면을 캡처해야 한다(카메라 렌더에는 Overlay UI가 없다).
        var go = new GameObject("CG2 PreviewCapture") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<CaptureRunner>().StartCoroutine(CaptureRoutine(go));
    }

    class CaptureRunner : MonoBehaviour { }

    static IEnumerator CaptureRoutine(GameObject runner)
    {
        yield return new WaitForEndOfFrame();
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        Object.Destroy(runner);   // 이 코루틴이 끝난 뒤(프레임 끝)에 지워진다

        // 원본 | 흑백 | 적록 색약 — 가로로 이어 붙인다. 너비 640으로 줄여 파일을 가볍게.
        int w = 640, h = Mathf.RoundToInt(640f * shot.height / shot.width);
        var outTex = new Texture2D(w * 3, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = shot.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                float g = CG2Palette.Luma(c);
                outTex.SetPixel(x, y, c);
                outTex.SetPixel(x + w, y, new Color(g, g, g));
                outTex.SetPixel(x + 2 * w, y, CG2Palette.Deuteranopia(c));
            }
        outTex.Apply();
        Directory.CreateDirectory(CAPTURE_DIR);
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        string tag = theme != null && theme.palette != null ? theme.palette.themeName.Replace(' ', '_') : "week5";
        string path = $"{CAPTURE_DIR}/preview_{tag}_{System.DateTime.Now:HHmmss}.png";
        File.WriteAllBytes(path, outTex.EncodeToPNG());
        Object.DestroyImmediate(shot);
        Object.DestroyImmediate(outTex);
        AssetDatabase.ImportAsset(path);
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        Debug.Log($"[검증] 미리보기 캡처 — {path}\n" +
                  "        왼쪽 원본 · 가운데 흑백 · 오른쪽 적록 색약(제2색맹 근사)\n" +
                  "        확인: ① 흑백에서 플레이어·적·투사체가 바닥과 구분되는가  ② 색약에서 HP바(위험)와 EXP바(보상)가 구분되는가");
    }

    // ────────────────────────────────────────────────────────── 3. UI 테마 · 4. 씬 톤

    [MenuItem("CG2 Lab/6주차/3. UI 테마 적용 — 팔레트 → 버튼 5상태·패널·바", priority = 30)]
    public static void ApplyUI() => Apply(ui: true, tone: null, "3단계 UI 테마");

    [MenuItem("CG2 Lab/6주차/4. 씬 톤 적용 — 팔레트 → 조명·타일·배경", priority = 40)]
    public static void ApplyTone() => Apply(ui: null, tone: true, "4단계 씬 톤");

    public static void ApplyAll() => Apply(ui: true, tone: true, "UI + 씬 톤");

    static void Apply(bool? ui, bool? tone, string title)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("테마", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        if (Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include) == null)
        {
            EditorUtility.DisplayDialog("테마", "게임 씬이 아닙니다. '1-3. 게임 씬으로 돌아가기'를 먼저 실행하세요.", "확인");
            return;
        }
        var palette = PaletteStudioWindow.EnsureMyPalette();
        var presets = EnsurePresets();
        if (!File.Exists(BACKUP)) Backup();

        var go = Week6Common.FindRoot(THEME);
        if (go == null)
        {
            go = new GameObject(THEME);
            Undo.RegisterCreatedObjectUndo(go, THEME);
        }
        var theme = go.GetComponent<CG2Theme>();
        if (theme == null)
        {
            theme = Undo.AddComponent<CG2Theme>(go);
            theme.applyUI = false;
            theme.applySceneTone = false;
        }
        Undo.RecordObject(theme, title);
        theme.enabled = true;
        theme.palette = palette;
        theme.presets = presets;
        theme.focusRingSprite = EnsureFocusRingSprite();
        if (ui.HasValue) theme.applyUI = ui.Value;
        if (tone.HasValue) theme.applySceneTone = tone.Value;
        string split = theme.applySceneTone ? SplitLights() : "";

        RecordSceneColors();
        theme.Apply();
        EditorSceneManager.MarkSceneDirty(go.scene);

        int buttons = 0;
        foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include))
            if (s is not Slider && s.targetGraphic != null && s.targetGraphic.color == Color.white) buttons++;
        var p = palette;
        Debug.Log($"[검증] {title} 적용 — 팔레트 '{p.themeName}'\n" +
                  (theme.applyUI
                      ? $"        UI : 버튼 {buttons}개 = Image 흰색 + ColorBlock 5상태 · 창 {CG2Palette.Hex(p.sub1)} · 제목 {CG2Palette.Hex(p.accent)} · HP {CG2Palette.Hex(p.danger)} · EXP {CG2Palette.Hex(p.accent)}\n" +
                        $"             Normal {CG2Palette.Hex(p.ButtonNormal)} · Hover {CG2Palette.Hex(p.ButtonHighlighted)} · Pressed {CG2Palette.Hex(p.ButtonPressed)} · Selected 테두리 {CG2Palette.Hex(p.FocusRing)} · Disabled {CG2Palette.Hex(p.ButtonDisabled)} α0.6 + 글자·아이콘 α{theme.disabledContentAlpha}\n"
                      : "        UI : (꺼짐 — 3단계 메뉴로 켠다)\n") +
                  (theme.applySceneTone
                      ? $"        씬 톤 : 배경 조명 {CG2Palette.Hex(p.Ambient)} · 캐릭터 조명 {CG2Palette.Hex(Color.Lerp(Color.white, p.Ambient, theme.characterTint))} · 배경색 {CG2Palette.Hex(p.main)} · Ground 틴트 {theme.groundTint:F2} · Wall 틴트 {theme.wallTint:F2}\n" +
                        $"        {split}\n"
                      : "        씬 톤 : (꺼짐 — 4단계 메뉴로 켠다)\n") +
                  "        ── Play 해서 확인 ── 상점(Tab)에서 Hover · 클릭 · Gold 부족(Disabled) / T 키 = 테마 바꾸기");
    }

    /// <summary>
    /// 전역 조명을 둘로 나눈다 — 배경(Ground · Wall)은 테마 톤, 나머지(캐릭터 · 투사체 · FX)는 흰색에 가깝게.
    /// 전역 조명 하나로 화면 전체에 톤을 곱하면 따뜻한 색 캐릭터가 푸른 조명에서 어두워져 바닥에 묻힌다(측정 #3-2).
    /// 4주차에서 본 Target Sorting Layers를 이용한다. 같은 레이어를 두 전역 조명이 비추면 더해지므로 겹치지 않게 나눈다.
    /// </summary>
    static string SplitLights()
    {
        Light2D main = null, entity = null;
        foreach (var l in Object.FindObjectsByType<Light2D>())
        {
            if (l.lightType != Light2D.LightType.Global) continue;
            if (l.name == CG2Theme.ENTITY_LIGHT) entity = l; else if (main == null) main = l;
        }
        if (main == null) return "⚠ 전역 조명(Global Light 2D)이 없어 조명 분리를 건너뛰었다";

        var bg = new List<int>(); var rest = new List<int>();
        foreach (var layer in SortingLayer.layers)
            (layer.name == "Ground" || layer.name == "Wall" ? bg : rest).Add(layer.id);
        if (bg.Count == 0) return "⚠ Ground·Wall Sorting Layer가 없어 조명 분리를 건너뛰었다 (4주차 '2. Sorting Layer 등록')";

        if (entity == null)
        {
            var go = new GameObject(CG2Theme.ENTITY_LIGHT);
            Undo.RegisterCreatedObjectUndo(go, "조명 분리");
            entity = go.AddComponent<Light2D>();
            entity.lightType = Light2D.LightType.Global;
        }
        Undo.RecordObject(main, "조명 분리");
        Undo.RecordObject(entity, "조명 분리");
        entity.intensity = main.intensity;
        Week6Common.SetLightTargets(main, bg);
        Week6Common.SetLightTargets(entity, rest);
        return $"조명 분리 : '{main.name}' → Ground·Wall / '{entity.name}' → {rest.Count}개 레이어 (캐릭터 · 투사체 · FX · Default)";
    }

    /// <summary>
    /// 포커스 링용 12×12 테두리 스프라이트(두께 3px · 가운데 투명). 9-슬라이스로 늘리면 어떤 크기 버튼에도 두께가 같다.
    /// PPU 100 = Canvas의 기준(Reference Pixels Per Unit)과 같아서 테두리 3px이 UI 3단위로 그대로 나온다.
    /// </summary>
    static Sprite EnsureFocusRingSprite()
    {
        const string path = "Assets/CG2Week6/Art/ui_focus_ring.png";
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var t = new Texture2D(12, 12, TextureFormat.RGBA32, false);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    t.SetPixel(x, y, x < 3 || y < 3 || x > 8 || y > 8 ? Color.white : Color.clear);
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.spriteBorder = new Vector4(3, 3, 3, 3);
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void RecordSceneColors()
    {
        foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include)) Undo.RecordObject(g, "theme");
        foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include)) Undo.RecordObject(s, "theme");
        foreach (var t in Object.FindObjectsByType<Tilemap>()) Undo.RecordObject(t, "theme");
        foreach (var l in Object.FindObjectsByType<Light2D>()) Undo.RecordObject(l, "theme");
        if (Camera.main != null) Undo.RecordObject(Camera.main, "theme");
    }

    // ────────────────────────────────────────────────────────── 3-3. 비교

    [MenuItem("CG2 Lab/6주차/3-3. (비교) 5주차 색 ↔ 테마", priority = 33)]
    public static void Compare()
    {
        var go = Week6Common.FindRoot(THEME);
        var theme = go != null ? go.GetComponent<CG2Theme>() : null;
        if (theme == null || !File.Exists(BACKUP))
        {
            EditorUtility.DisplayDialog("비교", "아직 테마를 적용하지 않았습니다. '3. UI 테마 적용'을 먼저 실행하세요.", "확인");
            return;
        }
        RecordSceneColors();
        Undo.RecordObject(theme, "compare");
        if (theme.enabled)
        {
            theme.enabled = false;
            int n = Restore();
            Debug.Log($"[검증] 비교 — 5주차 색으로 ({n}곳 복원). 한 번 더 누르면 테마로 돌아간다.");
        }
        else
        {
            theme.enabled = true;   // OnEnable에서 다시 칠한다
            theme.Apply();
            Debug.Log($"[검증] 비교 — 테마 '{theme.palette.themeName}'로");
        }
        if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // ────────────────────────────────────────────────────────── 4-2. 테마 바꾸기

    [MenuItem("CG2 Lab/6주차/4-2. 테마 바꾸기 (프리셋 순환 · Play 중엔 T 키)", priority = 42)]
    public static void CycleTheme()
    {
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        if (theme == null) { EditorUtility.DisplayDialog("테마 바꾸기", "'3. UI 테마 적용'을 먼저 실행하세요.", "확인"); return; }
        if (!EditorApplication.isPlaying) { RecordSceneColors(); Undo.RecordObject(theme, "theme swap"); }
        theme.NextPreset();
        if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(theme.gameObject.scene);
    }

    /// <summary>
    /// 비교용 프리셋 3종. 값은 "규칙으로 생성 → 검증 통과하도록 손으로 다듬기"를 거친 결과다.
    /// 이미 있으면 그대로 둔다(학생이 고친 프리셋을 덮지 않는다).
    /// </summary>
    public static CG2Palette[] EnsurePresets()
    {
        Directory.CreateDirectory(PRESET_DIR);
        return new[]
        {
            Preset("Dungeon", "던전 (분할 보색)", CG2Palette.Harmony.SplitComplementary, "1B1F2B", "2A3142", "3D5A80", "F2C14E", "E5484D", "F2F4F8"),
            Preset("NightForest", "밤의 숲 (유사색)", CG2Palette.Harmony.Analogous, "0F1D1B", "1B312D", "2D6A5C", "9CE37D", "E5484D", "EEF6F2"),
            Preset("Lava", "용암 (보색)", CG2Palette.Harmony.Complementary, "1F1210", "3A1F19", "8A3A24", "57D7F2", "FF4F5E", "FFF3EA"),
        };
    }

    static CG2Palette Preset(string file, string name, CG2Palette.Harmony h, string m, string s1, string s2, string a, string d, string t)
    {
        string path = $"{PRESET_DIR}/{file}.asset";
        var p = AssetDatabase.LoadAssetAtPath<CG2Palette>(path);
        if (p != null) return p;
        p = ScriptableObject.CreateInstance<CG2Palette>();
        p.themeName = name;
        p.harmony = h;
        p.main = Hex(m); p.sub1 = Hex(s1); p.sub2 = Hex(s2); p.accent = Hex(a); p.danger = Hex(d); p.text = Hex(t);
        AssetDatabase.CreateAsset(p, path);
        AssetDatabase.SaveAssets();
        return p;
    }

    static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;

    // ────────────────────────────────────────────────────────── 백업 · 복원

    [System.Serializable] class Entry { public string path, kind; public Color color; public int transition; public ColorBlock colors; }
    [System.Serializable] class Snapshot { public List<Entry> items = new(); }

    static void Backup()
    {
        var snap = new Snapshot();
        foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include))
            snap.items.Add(new Entry { path = Week6Common.PathOf(g.transform), kind = "graphic", color = g.color });
        foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include))
            snap.items.Add(new Entry { path = Week6Common.PathOf(s.transform), kind = "selectable", transition = (int)s.transition, colors = s.colors });
        foreach (var t in Object.FindObjectsByType<Tilemap>())
            snap.items.Add(new Entry { path = Week6Common.PathOf(t.transform), kind = "tilemap", color = t.color });
        foreach (var l in Object.FindObjectsByType<Light2D>())
            snap.items.Add(new Entry { path = Week6Common.PathOf(l.transform), kind = "light", color = l.color });
        if (Camera.main != null)
            snap.items.Add(new Entry { path = Week6Common.PathOf(Camera.main.transform), kind = "camera", color = Camera.main.backgroundColor });
        Directory.CreateDirectory(Path.GetDirectoryName(BACKUP));
        File.WriteAllText(BACKUP, JsonUtility.ToJson(snap, true));
        AssetDatabase.ImportAsset(BACKUP);
    }

    /// <summary>백업한 5주차 색을 되돌려 놓는다. 복원한 곳의 수를 돌려준다.</summary>
    public static int Restore()
    {
        if (!File.Exists(BACKUP)) return 0;
        var snap = JsonUtility.FromJson<Snapshot>(File.ReadAllText(BACKUP));
        int n = 0;
        foreach (var e in snap.items)
        {
            var t = Week6Common.Find(e.path);
            if (t == null) continue;
            switch (e.kind)
            {
                case "graphic" when t.TryGetComponent<Graphic>(out var g): g.color = e.color; n++; break;
                case "selectable" when t.TryGetComponent<Selectable>(out var s):
                    s.transition = (Selectable.Transition)e.transition; s.colors = e.colors; n++; break;
                case "tilemap" when t.TryGetComponent<Tilemap>(out var tm): tm.color = e.color; n++; break;
                case "light" when t.TryGetComponent<Light2D>(out var l): l.color = e.color; n++; break;
                case "camera" when t.TryGetComponent<Camera>(out var c): c.backgroundColor = e.color; n++; break;
            }
        }
        // 4단계에서 나눈 캐릭터 조명은 5주차에 없던 것 — 흰색으로 두면 5주차의 흰 전역 조명 하나와 같은 화면이 된다
        var entity = Week6Common.FindRoot(CG2Theme.ENTITY_LIGHT);
        if (entity != null && entity.TryGetComponent<Light2D>(out var el)) el.color = Color.white;
        return n;
    }

    [MenuItem("CG2 Lab/6주차/되돌리기 — 3·4단계 테마 제거 (5주차 색으로)", priority = 91)]
    static void RevertTheme()
    {
        if (EditorApplication.isPlaying) return;
        var go = Week6Common.FindRoot(THEME);
        RecordSceneColors();
        int n = Restore();
        if (go != null) Undo.DestroyObjectImmediate(go);
        // 나눈 조명을 하나로 되돌린다 — 캐릭터 조명을 지우고 원래 전역 조명이 모든 레이어를 비추게
        var entity = Week6Common.FindRoot(CG2Theme.ENTITY_LIGHT);
        if (entity != null)
        {
            Undo.DestroyObjectImmediate(entity);
            var all = new List<int>();
            foreach (var layer in SortingLayer.layers) all.Add(layer.id);
            foreach (var l in Object.FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global) { Undo.RecordObject(l, "조명 합치기"); Week6Common.SetLightTargets(l, all); }
        }
        // CG2Theme이 버튼마다 붙인 포커스 링(자식 Image)도 지운다
        int rings = 0;
        foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include))
        {
            var ring = s.transform.Find("FocusRing");
            if (ring != null) { Undo.DestroyObjectImmediate(ring.gameObject); rings++; }
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[검증] 테마 제거 — 5주차 색 {n}곳 복원 · 포커스 링 {rings}개 · CG2 Theme 삭제 (팔레트 에셋은 남긴다)");
    }
}
