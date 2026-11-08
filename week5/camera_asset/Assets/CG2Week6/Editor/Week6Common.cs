using System.IO;
using UnityEngine;

/// <summary>
/// 6주차 메뉴들이 같이 쓰는 도우미.
///   Store — 측정 결과를 Library/CG2Week6에 남긴다. 에디터를 껐다 켜도 5단계 아트 가이드가 읽을 수 있다.
///           (Library는 깃에 올라가지 않는 로컬 폴더다)
///   Find  — 꺼져 있는 오브젝트(팝업 창)까지 경로로 찾는다. GameObject.Find는 켜진 것만 찾는다.
/// </summary>
public static class Week6Common
{
    const string STORE = "Library/CG2Week6";

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

    public static string PathOf(Transform t)
    {
        string p = t.name;
        for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
        return p;
    }

    public static Transform Find(string path)
    {
        int slash = path.IndexOf('/');
        string root = slash < 0 ? path : path.Substring(0, slash);
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name != root) continue;
            if (slash < 0) return go.transform;
            var t = go.transform.Find(path.Substring(slash + 1));
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>Light2D가 비추는 Sorting Layer ID 목록 (Inspector의 Target Sorting Layers).</summary>
    public static int[] LightTargets(UnityEngine.Rendering.Universal.Light2D l)
    {
        var p = new UnityEditor.SerializedObject(l).FindProperty("m_ApplyToSortingLayers");
        var ids = new int[p.arraySize];
        for (int i = 0; i < ids.Length; i++) ids[i] = p.GetArrayElementAtIndex(i).intValue;
        return ids;
    }

    public static void SetLightTargets(UnityEngine.Rendering.Universal.Light2D l, System.Collections.Generic.IList<int> ids)
    {
        var so = new UnityEditor.SerializedObject(l);
        var p = so.FindProperty("m_ApplyToSortingLayers");
        p.arraySize = ids.Count;
        for (int i = 0; i < ids.Count; i++) p.GetArrayElementAtIndex(i).intValue = ids[i];
        so.ApplyModifiedProperties();
    }

    /// <summary>이 Sorting Layer를 비추는 전역 조명 — 레이어마다 다른 전역 조명을 둘 수 있다.</summary>
    public static UnityEngine.Rendering.Universal.Light2D GlobalLightFor(int layerId)
    {
        foreach (var l in Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>())
            if (l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global &&
                System.Array.IndexOf(LightTargets(l), layerId) >= 0) return l;
        return null;
    }

    public static GameObject FindRoot(string name)
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
