using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 6주차 1단계 — Gamma / Linear 색 공간 실험대.
///
/// 같은 씬을 두 색 공간에서 렌더링하고, 정해진 지점의 픽셀 값을 0~255로 읽어 표로 남긴다.
///   A. 회색 단색 칸 3개      — 덧셈·섞기가 없으면 두 색 공간의 결과가 같다 (대조군)
///   B. Point Light 2D 2개 겹침 — 빛의 덧셈은 선형 공간에서 해야 맞다
///   C. 반투명 빨강 위 반투명 초록 — 알파 블렌딩도 섞기(덧셈)다
///   D. 같은 PNG 두 장 · sRGB 체크 ON/OFF — 색 텍스처에 sRGB를 끄면 Linear에서만 색이 바랜다 (7주차 노멀 맵 복선)
///
/// 실험 씬은 게임 씬과 분리한다. 게임 씬의 라이트·UI가 측정값을 오염시키지 않게 하기 위함이다.
/// </summary>
public static class Week6ColorSpaceLab
{
    const string DIR = "Assets/CG2Week6/Lab";
    const string SCENE = DIR + "/ColorSpaceLab.unity";
    const string TEX_ON = DIR + "/lab_swatch_srgb_on.png";
    const string TEX_OFF = DIR + "/lab_swatch_srgb_off.png";
    const string WHITE = DIR + "/lab_white.png";
    const string LIT_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    const string UNLIT_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // 측정 지점 (월드 좌표) — 씬을 만들 때와 읽을 때 같은 표를 쓴다.
    static readonly (string label, Vector2 pos)[] Probes =
    {
        ("A 회색 25% (64)",        new(-6.0f,  3.0f)),
        ("A 회색 50% (128)",       new(-4.5f,  3.0f)),
        ("A 회색 75% (191)",       new(-3.0f,  3.0f)),
        ("B 라이트 1개 (중심)",            new( 1.0f,  1.0f)),
        ("B 라이트 1개 (중심에서 1.5)",     new(-0.5f,  1.0f)),
        ("B 라이트 2개 겹침 (각각 1.5)",    new( 2.5f,  1.0f)),
        ("C 빨강 α0.5 (흰 바탕)",   new(-5.6f, -1.6f)),
        ("C 빨강+초록 겹침",        new(-4.5f, -1.6f)),
        ("D sRGB ON  주황",        new( 2.0f, -3.0f)),
        ("D sRGB OFF 주황",        new( 4.5f, -3.0f)),
    };

    [MenuItem("CG2 Lab/6주차/1. 색 공간 실험 씬 열기 (없으면 만든다)", priority = 10)]
    public static void OpenLab()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("색 공간 실험", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(SCENE)) BuildLab();
        EditorSceneManager.OpenScene(SCENE);
        RefreshLights();
        Debug.Log($"[검증] 색 공간 실험 씬 — {SCENE}\n" +
                  $"        현재 Color Space : {PlayerSettings.colorSpace}\n" +
                  "        A 회색 칸 · B 라이트 겹침 · C 반투명 겹침 · D sRGB 체크 ON/OFF\n" +
                  "        ── 다음 ── 1-1. 측정 → 1-2. 전환 → 1-1. 측정 → 비교 → 1-2로 Linear 복귀");
    }

    [MenuItem("CG2 Lab/6주차/1-3. 게임 씬으로 돌아가기", priority = 13)]
    public static void BackToGame()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("게임 씬", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // 빌드 목록의 첫 씬 = 2주차부터 만든 게임 씬
        string path = null;
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled && s.path != SCENE) { path = s.path; break; }
        path ??= "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(path);
        Debug.Log($"[검증] 게임 씬 열기 — {path} · Color Space {PlayerSettings.colorSpace}" +
                  (PlayerSettings.colorSpace == ColorSpace.Linear ? " ✓" : " ⚠ Gamma 상태 — 1-2로 Linear 복귀"));
    }

    [MenuItem("CG2 Lab/6주차/되돌리기 — 1단계 실험 씬 다시 만들기", priority = 90)]
    static void RebuildLab()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(SCENE);
        OpenLab();
    }

    static void BuildLab()
    {
        Directory.CreateDirectory(DIR);
        MakeTextures();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<UniversalAdditionalCameraData>();

        // 전역 조명은 끈다 — B 영역에서 Point Light 두 개의 기여만 보이게.
        var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
        global.lightType = Light2D.LightType.Global;
        global.intensity = 0f;
        ApplyToAllLayers(global);

        var white = AssetDatabase.LoadAssetAtPath<Sprite>(WHITE);
        var lit = AssetDatabase.LoadAssetAtPath<Material>(LIT_MAT);
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UNLIT_MAT);

        // A — 회색 칸 3개 (Unlit: 조명과 무관한 순수한 색)
        float[] grays = { 64 / 255f, 128 / 255f, 191 / 255f };
        for (int i = 0; i < 3; i++)
            Quad($"A Gray {Mathf.RoundToInt(grays[i] * 255)}", white, unlit, new Vector2(-6 + i * 1.5f, 3), new Vector2(1.4f, 1.4f),
                 new Color(grays[i], grays[i], grays[i]), 0);
        Label("A 단색", new Vector2(-4.5f, 4.3f));

        // B — 흰 바닥(Lit) + Point Light 2개(반경 3, 세기 0.5, 중심 거리 3).
        //     같은 거리(1.5)에서 '라이트 1개만 닿는 곳'과 '2개가 겹치는 곳'을 비교한다 → 빛이 2배일 때 화면 값은?
        Quad("B Lit Floor", white, lit, new Vector2(2.5f, 1), new Vector2(8f, 3.6f), Color.white, 0);
        PointLight("B Light L", new Vector2(1, 1));
        PointLight("B Light R", new Vector2(4, 1));
        Label("B 라이트 겹침", new Vector2(2.5f, 3.3f));

        // C — 흰 바탕 위 반투명 빨강 · 초록 겹침 (Unlit)
        Quad("C White Base", white, unlit, new Vector2(-4.5f, -2.2f), new Vector2(4f, 2.6f), Color.white, 0);
        Quad("C Red a0.5", white, unlit, new Vector2(-5.0f, -1.6f), new Vector2(1.8f, 1.2f), new Color(1, 0, 0, 0.5f), 1);
        Quad("C Green a0.5", white, unlit, new Vector2(-4.0f, -1.6f), new Vector2(1.8f, 1.2f), new Color(0, 1, 0, 0.5f), 2);
        Label("C 반투명 겹침", new Vector2(-4.5f, -0.5f));

        // D — 같은 PNG, sRGB 체크만 다르다 (Unlit)
        Quad("D sRGB ON", AssetDatabase.LoadAssetAtPath<Sprite>(TEX_ON), unlit, new Vector2(2.0f, -3), new Vector2(2f, 2f), Color.white, 0);
        Quad("D sRGB OFF", AssetDatabase.LoadAssetAtPath<Sprite>(TEX_OFF), unlit, new Vector2(4.5f, -3), new Vector2(2f, 2f), Color.white, 0);
        Label("D sRGB ON | OFF", new Vector2(3.25f, -1.6f));

        EditorSceneManager.SaveScene(scene, SCENE);
    }

    static void Quad(string name, Sprite s, Material m, Vector2 pos, Vector2 size, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sharedMaterial = m;
        sr.color = c;
        sr.sortingOrder = order;
        sr.drawMode = SpriteDrawMode.Simple;
        // 스프라이트는 PPU 64 · 64px = 1유닛. 크기는 스케일로 맞춘다.
        go.transform.localScale = new Vector3(size.x, size.y, 1);
    }

    static void PointLight(string name, Vector2 pos)
    {
        var l = new GameObject(name).AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.transform.position = pos;
        l.pointLightInnerRadius = 0f;
        l.pointLightOuterRadius = 3f;
        l.falloffIntensity = 0.5f;
        l.intensity = 0.5f;   // 겹쳐도 1(흰색)을 넘지 않게 — 포화(255)로 잘리면 차이를 읽을 수 없다
        l.color = Color.white;
        ApplyToAllLayers(l);
    }

    /// <summary>
    /// 코드로 추가한 Light2D는 대상 Sorting Layer에서 Default가 빠진 채 만들어질 수 있다(4주차 진단 메뉴와 같은 문제).
    /// 실험대 스프라이트는 Default 레이어에 있으므로 모든 레이어를 대상으로 넣는다.
    /// </summary>
    static void ApplyToAllLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        var prop = so.FindProperty("m_ApplyToSortingLayers");
        var layers = SortingLayer.layers;
        prop.arraySize = layers.Length;
        for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Label(string text, Vector2 pos)
    {
        // 씬 뷰에서만 보이는 이름표 — 측정 픽셀을 덮지 않게 빈 오브젝트 이름으로만 남긴다.
        var go = new GameObject($"── {text}");
        go.transform.position = pos;
    }

    static void MakeTextures()
    {
        // 64×64 흰색 (A·B·C용)
        WritePng(WHITE, 64, _ => Color.white, srgb: true);
        // 주황 #E07A30 + 아래 1/4은 50% 회색 — 색 텍스처를 sRGB OFF로 들이면 어떻게 되는지 본다.
        Color orange = new Color32(0xE0, 0x7A, 0x30, 0xFF);
        System.Func<int, Color> swatch = y => y < 16 ? new Color(0.5f, 0.5f, 0.5f) : orange;
        WritePng(TEX_ON, 64, swatch, srgb: true);
        WritePng(TEX_OFF, 64, swatch, srgb: false);
    }

    static void WritePng(string path, int size, System.Func<int, Color> rowColor, bool srgb)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                t.SetPixel(x, y, rowColor(y));
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 64;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.sRGBTexture = srgb;
        ti.SaveAndReimport();
    }

    // ────────────────────────────────────────────────────────── 측정

    [MenuItem("CG2 Lab/6주차/1-1. 측정 — 지점별 픽셀 값 기록", priority = 11)]
    public static void Measure()
    {
        var cam = Camera.main;
        if (cam == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != SCENE)
        {
            EditorUtility.DisplayDialog("색 공간 측정", "먼저 '1. 색 공간 실험 씬 열기'를 실행하세요.", "확인");
            return;
        }

        RefreshLights();
        var values = Capture(cam);
        string space = PlayerSettings.colorSpace.ToString();
        Week6Common.Save("colorspace_" + space, string.Join("|", values));

        // 두 색 공간을 모두 쟀으면 나란히 비교한다.
        string lin = Week6Common.Load("colorspace_Linear"), gam = Week6Common.Load("colorspace_Gamma");
        var rows = new List<string>();
        for (int i = 0; i < Probes.Length; i++)
        {
            string l = Pick(lin, i), g = Pick(gam, i);
            rows.Add($"| {Probes[i].label} | {l} | {g} |");
        }
        string table = "| 지점 | Linear | Gamma |\n|---|---|---|\n" + string.Join("\n", rows);
        Week6Common.Save("colorspace_table", table);
        Debug.Log($"[측정] 색 공간 실험 — 현재 {space} · 값은 화면 출력 sRGB 0~255 (R, G, B)\n" + table +
                  (lin == "" || gam == "" ? "\n        ── 다음 ── 1-2. 색 공간 전환 → 1-1. 측정을 다시 실행하면 빈 칸이 채워진다."
                                          : "\n        A는 같고 B·C·D가 다르면 정상 — 덧셈·섞기·디코드가 들어간 곳만 달라진다."));
    }

    /// <summary>
    /// 편집 모드의 Light2D는 LateUpdate에서 빛 메시와 경계(bounds)를 만든다. 씬을 막 열었거나 에디터가 쉬고 있으면
    /// 경계가 0인 채로 남아 컬링되어 빛이 하나도 안 그려진다(측정값 0). 측정 직전에 메시를 강제로 갱신한다.
    /// UpdateMesh는 URP 내부 함수라 버전에 따라 없을 수 있다 — 없으면 건너뛴다(그때는 씬 뷰를 한 번 클릭하면 된다).
    /// </summary>
    static void RefreshLights()
    {
        var m = typeof(Light2D).GetMethod("UpdateMesh",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null, new[] { typeof(bool) }, null);
        if (m == null) return;
        foreach (var l in Object.FindObjectsByType<Light2D>())
            if (l.lightType != Light2D.LightType.Global) m.Invoke(l, new object[] { true });
    }

    static string Pick(string joined, int i)
    {
        if (string.IsNullOrEmpty(joined)) return "—";
        var parts = joined.Split('|');
        return i < parts.Length ? parts[i] : "—";
    }

    static List<string> Capture(Camera cam)
    {
        const int W = 1600, H = 900;
        // 화면에 나가는 것과 같은 sRGB 인코딩 값을 읽기 위해 sRGB 렌더 텍스처를 쓴다.
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture;
        cam.aspect = (float)W / H;
        cam.targetTexture = rt;
        // 한 번만 그린다 — 같은 프레임에 두 번 그리면 2D 라이트·스프라이트 배치 상태가 꼬여 값이 틀어진다(테스트에서 확인).
        cam.Render();
        cam.targetTexture = prev;
        cam.ResetAspect();

        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = active;

        var result = new List<string>();
        float halfH = cam.orthographicSize, halfW = halfH * W / H;
        Vector2 c = cam.transform.position;
        foreach (var p in Probes)
        {
            int x = Mathf.RoundToInt((p.pos.x - c.x + halfW) / (2 * halfW) * W);
            int y = Mathf.RoundToInt((p.pos.y - c.y + halfH) / (2 * halfH) * H);
            Color32 px = tex.GetPixel(x, y);
            result.Add($"{px.r}, {px.g}, {px.b}");
        }
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return result;
    }

    // ────────────────────────────────────────────────────────── 전환

    [MenuItem("CG2 Lab/6주차/1-2. 색 공간 전환 (Gamma ↔ Linear)", priority = 12)]
    public static void Toggle()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("색 공간 전환", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        var next = PlayerSettings.colorSpace == ColorSpace.Linear ? ColorSpace.Gamma : ColorSpace.Linear;
        if (!EditorUtility.DisplayDialog("색 공간 전환",
                $"{PlayerSettings.colorSpace} → {next}\n\n모든 텍스처를 다시 임포트합니다. 프로젝트가 클수록 오래 걸립니다.\n" +
                "(실제 프로젝트에서 중간에 바꾸기 어려운 이유)", "전환", "취소"))
            return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        PlayerSettings.colorSpace = next;
        AssetDatabase.Refresh();
        sw.Stop();
        Debug.Log($"[검증] Color Space → {PlayerSettings.colorSpace} (전환·재임포트 {sw.Elapsed.TotalSeconds:F1}초)\n" +
                  (next == ColorSpace.Gamma
                      ? "        ── 다음 ── 1-1. 측정 → 비교가 끝나면 이 메뉴를 다시 눌러 Linear로 되돌린다."
                      : "        Linear 복귀 ✓ — URP · WebGL 2 권장 설정"));
    }
}
