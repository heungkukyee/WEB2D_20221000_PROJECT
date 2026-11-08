using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 7주차 1단계 — 노멀 맵 실험 씬 (게임 씬과 분리된 실험대).
///   ① 공       — 수학으로 만든 "정답" 노멀 맵. 노멀 맵이 무엇인지 가장 잘 보인다
///   ② 기사 A   — 노멀 맵 없음 (대조군)
///   ③ 기사 B   — 노멀 맵 있음 (2단계 '혼합' 방법)
///   ④ 기사 C   — 같은 노멀 맵인데 sRGB ON (일부러 틀린 설정 — 6주차 D 실험의 반대)
///   ⑤ 벽돌     — 4주차 벽 타일 그림 + '밝기' 방법 노멀 맵
/// 전역 조명은 어둡게(0.15), 테스트 조명 하나(Point Light 2D)를 씬 뷰에서 끌고 다니며 본다.
/// </summary>
public static class Week7Lab
{
    public const string DIR = Week7Common.ROOT + "/Lab";
    public const string SCENE = DIR + "/NormalLab.unity";
    public const string LIGHT = "Test Light";

    // 실험대 대상 (이름 · 위치 · 크기) — 측정(5-1)도 같은 표를 쓴다.
    public static readonly (string name, string png, Vector2 pos, float scale)[] Items =
    {
        ("① 공 (정답 노멀)",     DIR + "/lab_sphere.png",       new(-6.5f, 0.6f), 2.5f),
        ("② 기사 A 노멀 없음",   DIR + "/lab_knight_flat.png",  new(-3.25f, 0.6f), 2.5f),
        ("③ 기사 B 노멀 있음",   DIR + "/lab_knight.png",       new( 0.0f, 0.6f), 2.5f),
        ("④ 기사 C sRGB ON",    DIR + "/lab_knight_srgb.png",  new( 3.25f, 0.6f), 2.5f),
        ("⑤ 벽돌 (밝기 방법)",   DIR + "/lab_bricks.png",       new( 6.5f, 0.6f), 1.25f),
    };

    public static string KnightSource()
    {
        const string ai = "Assets/CG2Week5/MyArt/Characters/ai_player_knight.png";
        return File.Exists(ai) ? ai : "Assets/StarterProject/Art/spr_player.png";
    }

    const string WALL = "Assets/CG2Week4/Art/tile_wall_32.png";

    [MenuItem(Week7Common.MENU + "1-1. 실험 씬 열기 — 노멀 맵 있는 것과 없는 것", priority = 11)]
    public static void Open()
    {
        if (Week7Common.Busy("실험 씬")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(SCENE)) Build();
        EditorSceneManager.OpenScene(SCENE);
        Week7Common.RefreshLights();
        var light = Week7Common.FindInScene(LIGHT);
        if (light != null) Selection.activeGameObject = light.gameObject;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0, 0.5f, 0), new Vector3(17, 6, 1)), false);
        Debug.Log("[실습] 1-1 노멀 맵 실험 씬\n" +
                  "        ① 공(정답) ② 기사 A 노멀 없음 ③ 기사 B 노멀 있음 ④ 기사 C sRGB ON ⑤ 벽돌\n" +
                  "        Test Light가 선택되어 있다 → 씬 뷰에서 이동 도구(W)로 끌어 좌우로 움직여 본다.\n" +
                  "        볼 것: 빛이 왼쪽 → 오른쪽으로 갈 때, ②는 밝기만 변하고 ③은 밝은 면이 따라 움직인다.");
    }

    [MenuItem(Week7Common.MENU + "1-5. 게임 씬으로 돌아가기", priority = 15)]
    public static void BackToGame()
    {
        if (Week7Common.Busy("게임 씬")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = null;   // 빌드 목록의 첫 씬 = 2주차부터 만든 게임 씬
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled && s.path != SCENE) { path = s.path; break; }
        path ??= "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(path);
        Debug.Log($"[실습] 1-5 게임 씬 — {path}\n        ── 다음 ── 2단계: 2-1 측정 #2 (노멀 맵 만드는 방법 비교)");
    }

    [MenuItem(Week7Common.MENU + "되돌리기 — 1단계 실험 씬 다시 만들기", priority = 90)]
    static void Rebuild()
    {
        if (Week7Common.Busy("실험 씬")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(DIR);
        Open();
    }

    // ────────────────────────────────────────────────────────── 실험 씬 만들기

    static void Build()
    {
        Directory.CreateDirectory(DIR);
        MakeAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x14, 0x16, 0x1C, 0xFF);
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<UniversalAdditionalCameraData>();

        // 전역 조명은 어둡게 — 테스트 조명의 효과가 보이게. (전역 조명은 방향이 없어서 노멀 맵과 무관하다)
        var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
        global.lightType = Light2D.LightType.Global;
        global.intensity = 0.15f;
        Week7Common.ApplyToAllLayers(global);

        var lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        foreach (var it in Items)
        {
            var go = new GameObject(it.name);
            go.transform.position = it.pos;
            go.transform.localScale = Vector3.one * it.scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(it.png);
            sr.sharedMaterial = lit;
        }

        var l = new GameObject(LIGHT).AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.transform.position = new Vector3(-1.6f, 1.6f, 0);
        l.pointLightInnerRadius = 0f;
        l.pointLightOuterRadius = 6f;
        l.falloffIntensity = 0.5f;
        l.intensity = 1.2f;
        Week7Common.ApplyToAllLayers(l);
        Week7Common.SetNormalMap(l, Light2D.NormalMapQuality.Accurate, 3f);   // 기본값은 Disabled — 4단계의 함정
        l.gameObject.AddComponent<CG2MouseLight>().followMouse = false;

        EditorSceneManager.SaveScene(scene, SCENE);
    }

    static void MakeAssets()
    {
        // ① 공 — 밝은 회색 원 + 수학으로 계산한 노멀 (반구의 표면 방향 = 중심에서 바깥으로)
        const int S = 64; const float R = 30f;
        var albedo = new Color32[S * S];
        var normal = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2) / R, dy = (y - S / 2) / R, d2 = dx * dx + dy * dy;   // 중심 = 픽셀 (32, 32)
                bool inside = d2 <= 1f;
                albedo[y * S + x] = inside ? new Color32(200, 200, 200, 255) : new Color32(0, 0, 0, 0);
                normal[y * S + x] = inside ? NormalMapGen.Encode(new Vector3(dx, dy, Mathf.Sqrt(1 - d2))) : NormalMapGen.Flat;
            }
        WriteSprite(DIR + "/lab_sphere.png", albedo, S, S);
        NormalMapGen.Save(DIR + "/lab_sphere_n.png", normal, S, S);
        Link(DIR + "/lab_sphere.png", DIR + "/lab_sphere_n.png");

        // ②③④ 기사 — 같은 그림 세 장 (보조 텍스처는 그림 파일마다 따로 붙기 때문에 파일을 나눈다)
        var knight = NormalMapGen.LoadPng(KnightSource());
        var kp = knight.GetPixels32();
        var kn = NormalMapGen.Make(kp, knight.width, knight.height, new NormalMapGen.Settings());
        foreach (var name in new[] { "lab_knight_flat", "lab_knight", "lab_knight_srgb" })
            WriteSprite($"{DIR}/{name}.png", kp, knight.width, knight.height);
        NormalMapGen.Save(DIR + "/lab_knight_n.png", kn, knight.width, knight.height);
        NormalMapGen.Save(DIR + "/lab_knight_srgb_n.png", kn, knight.width, knight.height, srgb: true);
        Link(DIR + "/lab_knight.png", DIR + "/lab_knight_n.png");
        Link(DIR + "/lab_knight_srgb.png", DIR + "/lab_knight_srgb_n.png");

        // ⑤ 벽돌 — 4주차 벽 타일 그림 전체(4×4칸) + 밝기 방법 (밝은 벽돌 = 높다 · 어두운 줄눈 = 낮다)
        if (File.Exists(WALL))
        {
            var wall = NormalMapGen.LoadPng(WALL);
            var wp = wall.GetPixels32();
            WriteSprite(DIR + "/lab_bricks.png", wp, wall.width, wall.height);
            NormalMapGen.Save(DIR + "/lab_bricks_n.png",
                NormalMapGen.Make(wp, wall.width, wall.height, new NormalMapGen.Settings { method = NormalMapGen.Method.밝기, strength = 4, edgeIsAir = false }),
                wall.width, wall.height);
            Link(DIR + "/lab_bricks.png", DIR + "/lab_bricks_n.png");
        }
    }

    static void WriteSprite(string path, Color32[] px, int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 64;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    /// <summary>Sprite Editor → Secondary Textures에서 하는 일을 코드로: 이름 "_NormalMap" + 텍스처.</summary>
    public static void Link(string spritePath, string normalPath)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        ti.secondarySpriteTextures = new[]
        {
            new SecondarySpriteTexture { name = "_NormalMap", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) },
        };
        ti.SaveAndReimport();
    }

    // ────────────────────────────────────────────────────────── 1-2 해부

    [MenuItem(Week7Common.MENU + "1-2. 스프라이트 해부 — 그림 한 장에 붙은 텍스처 보기", priority = 12)]
    public static void Dissect()
    {
        var sb = new StringBuilder("[분석] 1-2 스프라이트 해부 — 한 스프라이트가 셰이더에 넘기는 텍스처\n");
        sb.AppendLine("| 대상 | 메인 텍스처 (_MainTex) | 보조 텍스처 (Secondary) | 머티리얼 |");
        sb.AppendLine("|---|---|---|---|");
        int n = 0;
        var all = new System.Collections.Generic.List<SpriteRenderer>(Object.FindObjectsByType<SpriteRenderer>());
        all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var sr in all)
        {
            if (sr.sprite == null || n >= 8) continue;
            if (sr.name.StartsWith("Enemy(Clone)") && n > 0) continue;
            n++;
            string path = AssetDatabase.GetAssetPath(sr.sprite);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            string sec = "없음";
            if (ti != null && ti.secondarySpriteTextures != null && ti.secondarySpriteTextures.Length > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var s in ti.secondarySpriteTextures)
                    parts.Add($"{s.name} = {(s.texture ? s.texture.name : "(비어 있음)")}");
                sec = string.Join(", ", parts);
            }
            sb.AppendLine($"| {sr.name} | {Path.GetFileName(path)} ({sr.sprite.texture.width}×{sr.sprite.texture.height}) | {sec} | {sr.sharedMaterial?.shader.name} |");
        }
        sb.Append("        읽는 법: 같은 Sprite-Lit 셰이더가 '_NormalMap' 이름의 보조 텍스처가 있으면 그것으로 빛 방향을 계산한다.\n" +
                  "                 없으면 셰이더 기본값(평평한 보라)을 쓴다 → 빛이 어디서 오든 같은 명암 = 평면.");
        Debug.Log(sb.ToString());
    }

    // ────────────────────────────────────────────────────────── 1-3 색 읽기

    [MenuItem(Week7Common.MENU + "1-3. 노멀 맵 색 읽기 — 공의 다섯 지점", priority = 13)]
    public static void ReadColors()
    {
        string path = DIR + "/lab_sphere_n.png";
        if (!File.Exists(path)) { EditorUtility.DisplayDialog("노멀 맵 색 읽기", "먼저 1-1. 실험 씬 열기를 실행하세요.", "확인"); return; }
        var t = NormalMapGen.LoadPng(path);
        (string where, int x, int y, string mean)[] pts =
        {
            ("가운데",    32, 32, "화면 쪽(정면)을 본다"),
            ("왼쪽 끝",    4, 32, "왼쪽을 본다"),
            ("오른쪽 끝", 60, 32, "오른쪽을 본다"),
            ("위쪽 끝",   32, 60, "위를 본다"),
            ("아래쪽 끝", 32,  4, "아래를 본다"),
            ("바깥 (투명)", 0,  0, "그림 없음 → 평평한 값"),
        };
        var sb = new StringBuilder("[분석] 1-3 노멀 맵 색 읽기 — lab_sphere_n.png (64×64)\n");
        sb.AppendLine("| 지점 | 색 R, G, B | 방향 X, Y, Z | 뜻 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var p in pts)
        {
            Color32 c = t.GetPixel(p.x, p.y);
            var d = NormalMapGen.Decode(c);
            sb.AppendLine($"| {p.where} | {c.r}, {c.g}, {c.b} | {d.x:+0.00;-0.00}, {d.y:+0.00;-0.00}, {d.z:+0.00;-0.00} | {p.mean} |");
        }
        sb.Append("        읽는 법: 방향 = 색 ÷ 255 × 2 − 1.  R ↔ 좌우(X) · G ↔ 위아래(Y) · B ↔ 화면 쪽(Z)\n" +
                  "                 128은 0(기울지 않음), 255는 +1, 0은 −1. 그래서 평평한 곳은 (128, 128, 255) 보라색이다.");
        Object.DestroyImmediate(t);
        Debug.Log(sb.ToString());
    }
}
