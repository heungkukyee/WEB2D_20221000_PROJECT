using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 8주차 메뉴들이 같이 쓰는 도우미.
///   Save/Load — 측정 기록을 Library/CG2Week8에 남긴다. 체크포인트가 읽는다.
///   Shoot     — 임시 카메라로 한 번만 그려 화면 값을 읽는다(같은 프레임을 두 번 그리면 2D 조명이 꼬인다).
///   Stat      — 화면 밝기 분포. 숫자가 눈에 보이는 차이를 말하도록 화면 값(0~255)으로 잰다.
/// </summary>
public static class Week8Common
{
    public const string ROOT = "Assets/CG2Week8";
    public const string CAPTURES = ROOT + "/Captures";
    public const string BACKUP = ROOT + "/Backup";
    public const string MENU = "CG2 Lab/8주차/";
    public const string STORE = "Library/CG2Week8";
    public const string GROUND_PNG = "Assets/CG2Week4/Art/tile_ground_32.png";
    public const string WALL_PNG = "Assets/CG2Week4/Art/tile_wall_32.png";
    public const string TORCH = "Torch";
    public const string LANTERN = "Player Lantern";
    public const string LAMPS = "CG2 Street Lamps";
    public const string KEYS = "CG2 Light Keys";
    public const string ENTITY_LIGHT = "CG2 Entity Light";
    public const string GROUND_LIGHT = "Global Light 2D";
    public const int W = 1280, H = 720;

    public static void Save(string key, string value)
    {
        Directory.CreateDirectory(STORE);
        File.WriteAllText($"{STORE}/{key}.txt", value);
    }

    public static string Load(string key)
    {
        string p = $"{STORE}/{key}.txt";
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    public static bool Busy(string title)
    {
        if (!EditorApplication.isPlaying) return false;
        EditorUtility.DisplayDialog(title, "Play를 멈춘 뒤 실행하세요.", "확인");
        return true;
    }

    public static void ApplyToAllLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        var prop = so.FindProperty("m_ApplyToSortingLayers");
        var layers = SortingLayer.layers;
        prop.arraySize = layers.Length;
        for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Normal Maps · Shadows 칸. 일부는 코드에서 읽기 전용이라 직렬화 속성으로 바꾼다.</summary>
    public static void SetLightSerialized(Light2D l, Light2D.NormalMapQuality q, float distance, bool shadows, float strength, float softness)
    {
        var so = new SerializedObject(l);
        so.FindProperty("m_NormalMapQuality").intValue = (int)q;
        so.FindProperty("m_NormalMapDistance").floatValue = distance;
        so.FindProperty("m_ShadowsEnabled").boolValue = shadows;
        so.FindProperty("m_ShadowIntensity").floatValue = strength;
        so.FindProperty("m_ShadowSoftness").floatValue = softness;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static float ShadowSoftness(Light2D l) => new SerializedObject(l).FindProperty("m_ShadowSoftness").floatValue;

    public static Light2D Named(string name)
    {
        foreach (var l in Object.FindObjectsByType<Light2D>())
            if (l.name == name) return l;
        return null;
    }

    /// <summary>횃불 후보: 이름이 Torch인 조명, 없으면 7주차 Test Light.</summary>
    public static Light2D FindTorch() => Named(TORCH) ?? Named("Test Light");

    /// <summary>방향이 있는 조명 전부(측정용 임시 조명은 뺀다).</summary>
    public static List<Light2D> DirectionalLights()
    {
        var list = new List<Light2D>();
        foreach (var l in Object.FindObjectsByType<Light2D>())
            if (l.lightType != Light2D.LightType.Global && !l.name.StartsWith("CG2 Measure")) list.Add(l);
        return list;
    }

    // ────────────────────────────────────────────────────────── 화면 찍기와 재기

    /// <summary>항상 같은 곳(월드 0,0)을 같은 크기로 찍는 임시 카메라 — 플레이어가 어디 있든 같은 화면을 잰다.</summary>
    public static Camera FixedCamera()
    {
        var main = Camera.main;
        var go = new GameObject("CG2 Measure Camera") { hideFlags = HideFlags.DontSave };
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        go.AddComponent<UniversalAdditionalCameraData>();
        cam.enabled = false;
        cam.transform.position = new Vector3(0, 0, main.transform.position.z);
        return cam;
    }

    public static Texture2D Shoot(Camera cam, int w = W, int h = H)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.aspect = (float)w / h;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        cam.ResetAspect();
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        rt.Release();
        Object.DestroyImmediate(rt);
        if (Measure(tex).Range < 1f) Debug.LogWarning($"[측정] 빈 화면이 찍혔다 — 이 측정은 버리고 메뉴를 다시 누른다. (카메라 {cam.transform.position} · 크기 {cam.orthographicSize} · near {cam.nearClipPlane} · far {cam.farClipPlane} · 마스크 {cam.cullingMask})");
        return tex;
    }

    public static float Gray(Color32 c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    public struct Stat
    {
        public float dark, mid, bright, white;   // 어두운 쪽 5% · 가운데 · 밝은 쪽 5% · 하얗게 날아간 픽셀 %
        public float Range => bright - dark;     // 명암 폭
    }

    public static Stat Measure(Texture2D t)
    {
        var px = t.GetPixels32();
        var hist = new int[256];
        int white = 0;
        foreach (var c in px)
        {
            hist[Mathf.Clamp(Mathf.RoundToInt(Gray(c)), 0, 255)]++;
            if (c.r >= 250 && c.g >= 250 && c.b >= 250) white++;
        }
        return new Stat
        {
            dark = Percentile(hist, px.Length, 0.05f),
            mid = Percentile(hist, px.Length, 0.5f),
            bright = Percentile(hist, px.Length, 0.95f),
            white = 100f * white / px.Length
        };
    }

    public static int Percentile(int[] hist, int total, float q)
    {
        int need = Mathf.CeilToInt(total * q), sum = 0;
        for (int i = 0; i < 256; i++) { sum += hist[i]; if (sum >= need) return i; }
        return 255;
    }

    /// <summary>두 화면에서 밝기가 threshold 이상 달라진 픽셀 비율(%). 눈에 띄게 바뀐 면적.</summary>
    public static float ChangedPercent(Texture2D a, Texture2D b, float threshold = 30f)
    {
        var pa = a.GetPixels32(); var pb = b.GetPixels32();
        int n = 0;
        for (int i = 0; i < pa.Length; i++) if (Mathf.Abs(Gray(pa[i]) - Gray(pb[i])) >= threshold) n++;
        return 100f * n / pa.Length;
    }

    public static void SavePng(Texture2D t, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, t.EncodeToPNG());
    }

    public static Texture2D LoadPng(string path)
    {
        if (!File.Exists(path)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        t.LoadImage(File.ReadAllBytes(path));
        return t;
    }

    /// <summary>원본 PNG의 평균 반사율(선형 값). 받은 빛을 얼마나 되돌려 주는지.</summary>
    public static float Albedo(string png)
    {
        var t = LoadPng(png);
        if (t == null) return 0;
        double s = 0; int n = 0;
        foreach (var c in t.GetPixels32())
        {
            if (c.a < 128) continue;
            s += 0.2126 * Mathf.GammaToLinearSpace(c.r / 255f) + 0.7152 * Mathf.GammaToLinearSpace(c.g / 255f) + 0.0722 * Mathf.GammaToLinearSpace(c.b / 255f);
            n++;
        }
        Object.DestroyImmediate(t);
        return n > 0 ? (float)(s / n) : 0;
    }
}
