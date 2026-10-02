using UnityEditor;
using UnityEngine;

/// <summary>
/// 5주차 에셋 임포트 규격 — 2~4주차와 같은 기준을 한 곳에 모았다.
///
///   공통      Sprite · PPU 64 · Point · 밉맵 끔 · 압축 없음 · Clamp · Alpha Is Transparency
///   캐릭터    Tight 메시 (투명 영역을 덜 그려 오버드로우가 줄어든다) · Pivot Center
///   아이콘    Full Rect · Pivot Center (UI Image는 메시 타입을 쓰지 않지만 아틀라스 패킹이 단순해진다)
///
/// Pivot을 발바닥이 아닌 중심으로 두는 이유 —
///   Starter의 Player·Enemy 콜라이더는 transform 중심에 있다(Offset 0). Pivot을 발바닥으로 바꾸면
///   그림은 위로 반 칸 올라가고 콜라이더는 발밑에 남아 히트박스가 어긋난다.
///   3주차처럼 발바닥 Pivot을 쓰려면 콜라이더 Offset Y도 함께 올려야 한다.
/// </summary>
public static class ArtImport
{
    public enum Kind { Character, Icon }

    public const int PPU = 64;

    public static void ApplyPixelSprite(string path, Kind kind)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.isReadable = false;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = kind == Kind.Character ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteExtrude = 1;
        ti.SetTextureSettings(s);

        // 웹 플랫폼 오버라이드가 있으면 거기서 압축이 다시 걸린다. 같이 풀어 준다.
        var web = ti.GetPlatformTextureSettings("WebGL");
        if (web.overridden)
        {
            web.textureCompression = TextureImporterCompression.Uncompressed;
            web.format = TextureImporterFormat.RGBA32;
            ti.SetPlatformTextureSettings(web);
        }
        ti.SaveAndReimport();
    }
}

/// <summary>
/// 32px 플레이스홀더 아이콘. 생성형 AI 아이콘으로 바꾸기 전까지 쓰는 임시 그림이다.
/// 도형 마스크를 칠한 뒤 1px 어두운 외곽선을 둘러 픽셀아트처럼 보이게 한다.
/// </summary>
public static class PlaceholderIcons
{
    const int S = 32;

    public static Texture2D Make(string key)
    {
        System.Func<float, float, bool> mask;
        Color fill;
        switch (key)
        {
            case CG2IconSet.HP:        fill = new Color(0.90f, 0.20f, 0.25f); mask = Heart; break;
            case CG2IconSet.EXP:       fill = new Color(0.30f, 0.65f, 1.00f); mask = (x, y) => Mathf.Abs(x) + Mathf.Abs(y) < 0.8f; break;
            case CG2IconSet.COIN:      fill = new Color(1.00f, 0.80f, 0.20f); mask = (x, y) => x * x + y * y < 0.62f; break;
            case CG2IconSet.DAMAGE:    fill = new Color(0.85f, 0.85f, 0.90f); mask = Sword; break;
            case CG2IconSet.FIRE_RATE: fill = new Color(1.00f, 0.90f, 0.30f); mask = Bolt; break;
            case CG2IconSet.RANGE:     fill = new Color(0.40f, 0.85f, 0.45f); mask = Target; break;
            case CG2IconSet.HEAL:      fill = new Color(0.95f, 0.45f, 0.70f); mask = (x, y) => (Mathf.Abs(x) < 0.25f && Mathf.Abs(y) < 0.8f) || (Mathf.Abs(y) < 0.25f && Mathf.Abs(x) < 0.8f); break;
            default:                   fill = new Color(0.55f, 0.90f, 1.00f); mask = Chevrons; break;
        }

        var on = new bool[S, S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                on[x, y] = mask((x + 0.5f) / S * 2f - 1f, (y + 0.5f) / S * 2f - 1f);

        var outline = new Color(0.08f, 0.08f, 0.12f);
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = Color.clear;
                if (on[x, y])
                    c = y > S * 0.6f ? Color.Lerp(fill, Color.white, 0.25f) : fill;   // 윗부분 하이라이트
                else if (Near(on, x, y))
                    c = outline;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    static bool Near(bool[,] on, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < S && ny < S && on[nx, ny]) return true;
            }
        return false;
    }

    static bool Heart(float x, float y)
    {
        y = -y * 1.1f + 0.15f;
        float a = x * x + y * y - 0.45f;
        return a * a * a - x * x * y * y * y < 0f;
    }

    static bool Sword(float x, float y)
    {
        float u = (x + y) * 0.7071f, v = (y - x) * 0.7071f;   // 45° 회전
        bool blade = Mathf.Abs(v) < 0.12f && u > -0.35f && u < 0.85f;
        bool guard = Mathf.Abs(u + 0.4f) < 0.08f && Mathf.Abs(v) < 0.35f;
        bool grip = Mathf.Abs(v) < 0.08f && u < -0.4f && u > -0.8f;
        return blade || guard || grip;
    }

    static bool Bolt(float x, float y)
    {
        bool upper = y > -0.05f && y < 0.85f && x > -0.35f + (y - 0.85f) * -0.35f && x < 0.25f + (y - 0.85f) * -0.35f;
        bool lower = y < 0.05f && y > -0.85f && x > -0.25f + (y + 0.85f) * -0.35f && x < 0.35f + (y + 0.85f) * -0.35f;
        return upper || lower;
    }

    static bool Target(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        return r < 0.2f || (r > 0.4f && r < 0.55f) || (r > 0.72f && r < 0.86f);
    }

    static bool Chevrons(float x, float y)
    {
        float a = Mathf.Abs(y);
        return (x + a > -0.35f && x + a < 0.0f) || (x + a > 0.3f && x + a < 0.65f);   // ">>" 오른쪽 방향
    }
}
