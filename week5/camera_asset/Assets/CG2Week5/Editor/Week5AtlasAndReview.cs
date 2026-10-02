using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 5주차 5단계 — 교체한 에셋에 Sprite Atlas를 다시 적용하고, 화면 가독성을 수치로 점검한다.
///
/// 아틀라스를 두 개로 나누는 이유 —
///   월드 스프라이트(플레이어·적·투사체)와 UI 아이콘은 서로 다른 패스에서 그려진다.
///   Overlay UI는 카메라 렌더링이 끝난 뒤 따로 그려지므로, 한 아틀라스에 섞어도 둘이 한 배치로 묶이지 않는다.
///   오히려 UI 아틀라스가 캐릭터 텍스처까지 메모리에 끌고 다니게 된다. 쓰이는 곳끼리 묶는다.
///
/// 설정은 3주차와 같다 — 회전 끔 · Tight Packing 끔 · Padding 4 · Point · 밉맵 끔 · 압축 없음.
/// </summary>
public static class Week5AtlasAndReview
{
    const string DIR = "Assets/CG2Week5/Atlas";
    const string WORLD = DIR + "/Atlas_World.spriteatlasv2";
    const string UI = DIR + "/Atlas_UI.spriteatlasv2";
    const string ICON_SET = "Assets/CG2Week5/Art/CG2IconSet.asset";

    [MenuItem("CG2 Lab/5주차/5. Sprite Atlas 재적용 — 월드·UI 2종", priority = 50)]
    public static void BuildAtlases()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Sprite Atlas", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        Directory.CreateDirectory(DIR);

        var world = new List<Object>();
        var player = GameObject.FindGameObjectWithTag("Player");
        AddSprite(world, player != null ? player.GetComponent<SpriteRenderer>()?.sprite : null);
        AddSprite(world, PrefabSprite("Assets/StarterProject/Prefabs/Enemy.prefab"));
        AddSprite(world, PrefabSprite("Assets/StarterProject/Prefabs/Projectile.prefab"));

        var ui = new List<Object>();
        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        if (set != null) foreach (var e in set.entries) AddSprite(ui, e.sprite);

        // 원본이 압축돼 있으면 아틀라스에 뭉개진 픽셀이 그대로 들어간다(Unity도 경고를 낸다).
        // Starter v8의 플레이스홀더(spr_*.png)는 기본 압축으로 임포트돼 있다.
        var fixedList = new List<string>();
        foreach (var o in world) if (Uncompress((Sprite)o)) fixedList.Add(o.name);
        foreach (var o in ui) if (Uncompress((Sprite)o)) fixedList.Add(o.name);

        string a = Create(WORLD, world);
        string b = Create(UI, ui);

        // 에디터에서 바로 측정할 수 있게 지금 패킹한다(원래는 Play/빌드 때 패킹된다).
        SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget, false);

        Debug.Log("[검증] Sprite Atlas 재적용 완료\n" +
                  $"        {a}\n        {b}\n" +
                  (fixedList.Count > 0 ? $"        원본 압축 해제 : {string.Join(", ", fixedList)} (압축된 원본은 아틀라스에서도 뭉개진다)\n" : "") +
                  $"        Sprite Packer Mode : {EditorSettings.spritePackerMode}" +
                  (EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2 ? " ✓" : " ⚠ Project Settings → Editor → Sprite Packer Mode를 'Sprite Atlas V2 - Enabled'로") +
                  "\n        ── Play 해서 확인 ── Stats의 SetPass / Frame Debugger의 UI 이벤트 수를 적용 전과 비교하세요.");
    }

    static void AddSprite(List<Object> list, Sprite s)
    {
        if (s != null && !list.Contains(s)) list.Add(s);
    }

    static bool Uncompress(Sprite s)
    {
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s)) as TextureImporter;
        if (ti == null || ti.textureCompression == TextureImporterCompression.Uncompressed) return false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        return true;
    }

    static Sprite PrefabSprite(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return go != null && go.TryGetComponent<SpriteRenderer>(out var sr) ? sr.sprite : null;
    }

    static string Create(string path, List<Object> sprites)
    {
        // 새 스프라이트가 들어올 수 있으므로 매번 새로 만든다. GUID는 유지되지 않아도 참조하는 곳이 없다.
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        var atlas = new SpriteAtlasAsset();
        atlas.Add(sprites.ToArray());
        SpriteAtlasAsset.Save(atlas, path);
        AssetDatabase.ImportAsset(path);

        var imp = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
        imp.includeInBuild = true;
        imp.packingSettings = new SpriteAtlasPackingSettings
        {
            blockOffset = 1, padding = 4, enableRotation = false, enableTightPacking = false, enableAlphaDilation = false,
        };
        imp.textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Point, anisoLevel = 0,
        };
        var ps = imp.GetPlatformSettings("DefaultTexturePlatform");
        ps.maxTextureSize = 2048;
        ps.format = TextureImporterFormat.Automatic;
        ps.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SetPlatformSettings(ps);
        imp.SaveAndReimport();

        var names = new List<string>();
        foreach (var s in sprites) names.Add(s.name);
        return $"{Path.GetFileName(path),-26} 스프라이트 {sprites.Count}개 — {string.Join(", ", names)}";
    }

    // ────────────────────────────────────────────────────────── 가독성 점검

    [MenuItem("CG2 Lab/5주차/5-2. 가독성 점검 — 명도 대비·화면 크기", priority = 52)]
    public static void Review()
    {
        var ground = AverageColor("Assets/CG2Week4/Art/tile_ground_32.png", out _);
        var cam = Camera.main;
        float size = cam != null ? cam.orthographicSize : 7f;
        float screenPxPerUnit = 1080f / (2f * size);

        var rows = new List<string>();
        void Row(string label, Sprite s)
        {
            if (s == null) return;
            // 아틀라스로 패킹된 뒤에는 s.texture가 아틀라스 텍스처를 가리킨다. 원본 PNG는 스프라이트 경로로 찾는다.
            var c = AverageColor(AssetDatabase.GetAssetPath(s), out float coverage);
            float ratio = Contrast(c, ground);
            float onScreen = s.rect.height / s.pixelsPerUnit * screenPxPerUnit;
            string verdict = ratio >= 3f ? "✓" : ratio >= 2f ? "△ 외곽선·채도로 보강" : "⚠ 배경에 묻힘";
            rows.Add($"| {label} | {s.name} | {Hex(c)} | {ratio:F2} : 1 {verdict} | {onScreen:F0}px ({s.rect.height}px × {screenPxPerUnit / s.pixelsPerUnit:F2}) | {coverage:P0} |");
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        Row("Player", player != null ? player.GetComponent<SpriteRenderer>()?.sprite : null);
        Row("Enemy", PrefabSprite("Assets/StarterProject/Prefabs/Enemy.prefab"));
        Row("Projectile", PrefabSprite("Assets/StarterProject/Prefabs/Projectile.prefab"));

        Debug.Log($"[측정] 가독성 점검 — 바닥 타일 평균색 {Hex(ground)} · 카메라 Size {size} · 1080p 기준\n" +
                  "| 대상 | 스프라이트 | 평균색 | 바닥과 명도 대비 | 화면 크기 | 불투명 비율 |\n|---|---|---|---|---|---|\n" +
                  string.Join("\n", rows) +
                  "\n        명도 대비 3:1 이상 권장(WCAG 비텍스트 기준). 화면 크기 배율이 정수가 아니면 픽셀 폭이 들쭉날쭉해진다.");
    }

    static Color AverageColor(string path, out float coverage)
    {
        coverage = 0f;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Color.gray;
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(path));
        var px = t.GetPixels();
        Object.DestroyImmediate(t);
        Color sum = Color.clear; int n = 0;
        foreach (var p in px) if (p.a > 0.5f) { sum += p; n++; }
        coverage = px.Length > 0 ? (float)n / px.Length : 0f;
        return n > 0 ? sum / n : Color.gray;
    }

    static float Luminance(Color c)
    {
        float L(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
    }

    static float Contrast(Color a, Color b)
    {
        float la = Luminance(a), lb = Luminance(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
