using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 7주차 2단계 — 그림(스프라이트 PNG)에서 노멀 맵을 만든다.
///
/// 노멀 맵 = 픽셀마다 "표면이 어느 쪽을 보는지(방향 X·Y·Z)"를 색(R·G·B)으로 적어 둔 그림.
///   R = 좌(0) ↔ 우(255) · G = 아래(0) ↔ 위(255) · B = 화면 쪽(255). 평평한 곳 = (128, 128, 255) 보라색.
///
/// 방향은 "높이 지도"의 기울기로 구한다. 높이를 어디서 얻느냐가 방법의 차이다.
///   밝기 — 밝은 곳이 높다고 가정한다. 벽돌(밝은 벽돌 · 어두운 줄눈)에 잘 맞는다.
///   외곽 — 그림 안쪽일수록 높다고 가정한다(베개처럼 볼록). 캐릭터 실루엣에 잘 맞는다.
///   혼합 — 외곽으로 큰 모양을 만들고 밝기로 디테일을 조금 얹는다.
///
/// 결과는 원본과 같은 크기 · Point 필터 · 압축 없음 · <b>sRGB OFF</b>(색이 아니라 방향 데이터)로 저장한다.
/// 알파는 255로 채운다 — PC의 URP 2D는 노멀 맵을 읽을 때 R에 알파를 곱한다(UnpackNormal).
/// </summary>
public static class NormalMapGen
{
    public enum Method { 밝기, 외곽, 혼합 }

    [System.Serializable]
    public class Settings
    {
        public Method method = Method.혼합;
        public float strength = 6f;     // 기울기 배율 — 클수록 명암이 강하다
        public int bevel = 12;          // 외곽 방법: 가장자리에서 몇 px 안쪽까지 경사를 줄지
        public float detail = 0.2f;     // 혼합 방법: 밝기 디테일 비율
        public bool edgeIsAir = true;   // 그림 바깥(텍스처 경계)을 빈 곳으로 볼지 — 캐릭터는 true, 이어지는 타일은 false

        public Settings Clone() => (Settings)MemberwiseClone();
        public override string ToString() =>
            method switch
            {
                Method.밝기 => $"밝기 · 세기 {strength:0.#}",
                Method.외곽 => $"외곽 · 세기 {strength:0.#} · 경사 {bevel}px",
                _ => $"혼합 · 세기 {strength:0.#} · 경사 {bevel}px · 디테일 {detail:0.##}",
            };
    }

    /// <summary>PNG 파일을 그대로 읽는다 — 임포트 설정(Read/Write)과 무관하게 원본 픽셀을 얻는다.</summary>
    public static Texture2D LoadPng(string assetPath)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        t.LoadImage(File.ReadAllBytes(assetPath));
        t.filterMode = FilterMode.Point;
        return t;
    }

    public static float[] Height(Color32[] px, int w, int h, Settings s)
    {
        var lum = new float[w * h];
        for (int i = 0; i < px.Length; i++)
            lum[i] = px[i].a < 128 ? 0f : (0.2126f * px[i].r + 0.7152f * px[i].g + 0.0722f * px[i].b) / 255f;
        if (s.method == Method.밝기) return lum;

        var shape = Shape(px, w, h, Mathf.Max(1, s.bevel), s.edgeIsAir);
        if (s.method == Method.외곽) return shape;
        var mix = new float[w * h];
        for (int i = 0; i < mix.Length; i++) mix[i] = shape[i] + s.detail * lum[i];
        return mix;
    }

    /// <summary>가장자리(투명)까지의 거리 → 둥근 높이 0~1. 베개처럼 가장자리는 낮고 안쪽은 평평하게 높다.</summary>
    static float[] Shape(Color32[] px, int w, int h, int bevel, bool edgeIsAir)
    {
        var o = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a < 128) continue;
                float best = bevel + 1;
                for (int dy = -bevel - 1; dy <= bevel + 1; dy++)
                    for (int dx = -bevel - 1; dx <= bevel + 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        bool air = xx < 0 || yy < 0 || xx >= w || yy >= h ? edgeIsAir : px[yy * w + xx].a < 128;
                        if (!air) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - 0.5f;
                        if (d < best) best = d;
                    }
                float t = Mathf.Clamp01(best / bevel);
                o[y * w + x] = Mathf.Sqrt(1f - (1f - t) * (1f - t));   // 원의 4분의 1 — 둥근 어깨
            }
        return o;
    }

    /// <summary>높이 지도 → 노멀(소벨 필터로 기울기). 투명 픽셀은 평평한 색으로 채운다.</summary>
    public static Color32[] Normals(Color32[] px, float[] hgt, int w, int h, Settings s)
    {
        var o = new Color32[w * h];
        float H(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                if (s.edgeIsAir) return 0f;
                x = Mathf.Clamp(x, 0, w - 1); y = Mathf.Clamp(y, 0, h - 1);
            }
            return hgt[y * w + x];
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a < 128) { o[y * w + x] = Flat; continue; }
                float gx = (H(x + 1, y - 1) + 2 * H(x + 1, y) + H(x + 1, y + 1) - H(x - 1, y - 1) - 2 * H(x - 1, y) - H(x - 1, y + 1)) / 8f;
                float gy = (H(x - 1, y + 1) + 2 * H(x, y + 1) + H(x + 1, y + 1) - H(x - 1, y - 1) - 2 * H(x, y - 1) - H(x + 1, y - 1)) / 8f;
                o[y * w + x] = Encode(new Vector3(-gx * s.strength, -gy * s.strength, 1f).normalized);
            }
        return o;
    }

    public static readonly Color32 Flat = new(128, 128, 255, 255);

    public static Color32 Encode(Vector3 n) =>
        new((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255),
            (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255), 255);

    public static Vector3 Decode(Color32 c) => new Vector3(c.r / 255f * 2 - 1, c.g / 255f * 2 - 1, c.b / 255f * 2 - 1);

    public static Color32[] Make(Color32[] px, int w, int h, Settings s) => Normals(px, Height(px, w, h, s), w, h, s);

    /// <summary>
    /// 미리보기용 명암 — 빛 방향 L과 노멀의 내적(N·L, 램버트). 0.25는 환경광(빛이 안 닿는 면도 완전히 검지 않게).
    /// URP 2D의 실제 계산과 같은 원리다(빛 방향 = 라이트 위치 - 픽셀 위치, 높이 = Normal Map Distance).
    /// </summary>
    public static Color32[] Shade(Color32[] albedo, Color32[] normals, Vector3 light, float ambient = 0.25f)
    {
        light.Normalize();
        var o = new Color32[albedo.Length];
        for (int i = 0; i < o.Length; i++)
        {
            float k = ambient + (1 - ambient) * Mathf.Max(0, Vector3.Dot(Decode(normals[i]).normalized, light));
            var a = albedo[i];
            o[i] = new Color32((byte)(a.r * k), (byte)(a.g * k), (byte)(a.b * k), a.a);
        }
        return o;
    }

    /// <summary>노멀 맵 PNG를 쓰고 "방향 데이터" 규칙으로 임포트한다. srgb=true는 일부러 틀린 설정(실험 씬 비교용).</summary>
    public static void Save(string path, Color32[] n, int w, int h, bool srgb = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        t.SetPixels32(n);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ApplyImport(path, srgb);
        ReimportUsers(path);
    }

    /// <summary>
    /// 테스트에서 발견: 노멀 맵 파일만 덮어쓰면 그 노멀 맵을 쓰는 <b>스프라이트는 옛 노멀 맵을 계속 쓴다.</b>
    /// 스프라이트 쪽을 다시 임포트해야 새 노멀 맵이 반영된다 → 연결된 스프라이트를 찾아 함께 다시 임포트한다.
    /// </summary>
    public static int ReimportUsers(string normalPath)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (tex == null) return 0;
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p == normalPath || !(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
            var sec = ti.secondarySpriteTextures;
            if (sec == null) continue;
            foreach (var s in sec)
                if (s.texture == tex) { AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); n++; break; }
        }
        return n;
    }

    public static void ApplyImport(string path, bool srgb = false)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return;
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.alphaSource = TextureImporterAlphaSource.None;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    /// <summary>스프라이트 PNG 경로 → 노멀 맵 경로 (같은 폴더에 _n 붙임 — 짝이 한눈에 보이게).</summary>
    public static string NormalPathFor(string spritePath) =>
        Path.Combine(Path.GetDirectoryName(spritePath), Path.GetFileNameWithoutExtension(spritePath) + "_n.png").Replace('\\', '/');
}
