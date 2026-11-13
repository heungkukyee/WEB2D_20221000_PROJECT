using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 7주차 메뉴들이 같이 쓰는 도우미.
///   Save/Load — 측정 결과를 Library/CG2Week7에 남긴다(깃에 안 올라가는 로컬 폴더). 체크포인트가 읽는다.
///   Capture   — 카메라 한 장을 sRGB 렌더 텍스처로 그려 화면 출력 값(0~255)을 읽는다.
/// </summary>
public static class Week7Common
{
    const string STORE = "Library/CG2Week7";
    public const string ROOT = "Assets/CG2Week7";
    public const string MENU = "CG2 Lab/7주차/";

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

    /// <summary>코드로 만든 Light2D는 대상 Sorting Layer가 비어 있거나 Default만 있을 수 있다 → 모든 레이어를 넣는다.</summary>
    public static void ApplyToAllLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        var prop = so.FindProperty("m_ApplyToSortingLayers");
        var layers = SortingLayer.layers;
        prop.arraySize = layers.Length;
        for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 조명의 Normal Maps 설정(Inspector: Normal Maps → Quality · Distance).
    /// Light2D.normalMapQuality는 코드에서 읽기 전용이라 직렬화 속성으로 바꾼다(테스트에서 확인 — 컴파일 오류 CS0200).
    /// </summary>
    public static void SetNormalMap(Light2D l, Light2D.NormalMapQuality q, float distance = -1)
    {
        var so = new SerializedObject(l);
        so.FindProperty("m_NormalMapQuality").intValue = (int)q;
        if (distance > 0) so.FindProperty("m_NormalMapDistance").floatValue = distance;
        so.ApplyModifiedProperties();
    }

    public static string TargetNames(Light2D l)
    {
        var p = new SerializedObject(l).FindProperty("m_ApplyToSortingLayers");
        var names = new string[p.arraySize];
        for (int i = 0; i < names.Length; i++) names[i] = SortingLayer.IDToName(p.GetArrayElementAtIndex(i).intValue);
        return string.Join("·", names);
    }

    /// <summary>편집 모드의 Light2D는 빛 메시가 늦게 만들어져 컬링될 수 있다(6주차에서 확인) → 측정 직전에 강제 갱신.</summary>
    public static void RefreshLights()
    {
        var m = typeof(Light2D).GetMethod("UpdateMesh",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null, new[] { typeof(bool) }, null);
        if (m == null) return;
        foreach (var l in Object.FindObjectsByType<Light2D>())
            if (l.lightType != Light2D.LightType.Global) m.Invoke(l, new object[] { true });
    }

    /// <summary>카메라를 한 번만 그려(같은 프레임 두 번 그리면 2D 라이트 상태가 꼬인다 — 6주차) 화면 값 텍스처를 돌려준다.</summary>
    public static Texture2D Capture(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture;
        cam.aspect = (float)w / h;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        cam.ResetAspect();
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        rt.Release();
        Object.DestroyImmediate(rt);
        return tex;
    }

    public static Vector2Int WorldToPixel(Camera cam, Vector2 world, int w, int h)
    {
        float halfH = cam.orthographicSize, halfW = halfH * w / h;
        Vector2 c = cam.transform.position;
        return new Vector2Int(Mathf.FloorToInt((world.x - c.x + halfW) / (2 * halfW) * w),
                              Mathf.FloorToInt((world.y - c.y + halfH) / (2 * halfH) * h));
    }

    public static float Gray(Color32 c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);

    public static Transform FindInScene(string name)
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == name) return go.transform;
            var t = go.transform.Find(name);
            if (t != null) return t;
        }
        return null;
    }
}
