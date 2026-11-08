using UnityEngine;

/// <summary>
/// 6주차 — 게임의 색을 한곳에 모은 팔레트 에셋.
///
/// 색을 코드·씬 여기저기에 직접 적어 두면(5주차 UI에는 서로 다른 색 7개가 흩어져 있다)
/// 테마를 바꾸거나 톤을 맞출 때 전부 찾아다녀야 한다. 색은 이 에셋 하나에만 두고,
/// UI·조명·타일은 CG2Theme이 이 값을 읽어 칠한다 — "색은 데이터다".
///
/// 역할 1·2·1
///   Main   (주조, 화면의 60%)  바닥·배경 톤. 가장 어둡고 채도가 낮다.
///   Sub1   (보조, 30%)        창·패널 바탕. 주조와 같은 색상, 한 단계 밝게.
///   Sub2   (보조, 30%)        버튼·상호작용 요소. 조화 규칙으로 고른 두 번째 색상.
///   Accent (강조, 10%)        반드시 봐야 하는 것 — 선택·보상·경험치·레벨업. 가장 밝고 채도가 높다.
/// 기능색
///   Danger 체력·위험, Text 글자. 팔레트 규칙과 별개로 "의미"를 지키는 색이다.
/// </summary>
[CreateAssetMenu(menuName = "CG2/Palette", fileName = "CG2Palette")]
public class CG2Palette : ScriptableObject
{
    public enum Harmony { Analogous, Complementary, SplitComplementary, Triadic }

    public string themeName = "My Palette";
    public Harmony harmony = Harmony.SplitComplementary;

    [Header("1 · 2 · 1")]
    public Color main = new Color32(0x1B, 0x1F, 0x2B, 0xFF);
    public Color sub1 = new Color32(0x2A, 0x31, 0x42, 0xFF);
    public Color sub2 = new Color32(0x3D, 0x5A, 0x80, 0xFF);
    public Color accent = new Color32(0xF2, 0xC1, 0x4E, 0xFF);

    [Header("기능색")]
    public Color danger = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    public Color text = new Color32(0xF2, 0xF4, 0xF8, 0xFF);

    /// <summary>에디터에서 값을 바꾸면 Play 중인 CG2Theme이 바로 다시 칠한다.</summary>
    public static event System.Action<CG2Palette> Changed;
    void OnValidate() => Changed?.Invoke(this);
    public void NotifyChanged() => Changed?.Invoke(this);

    // ────────────────────────────────────────────────────────── 파생 규칙 (UI 상태 · 조명)
    // 상태색은 따로 고르지 않는다. 팔레트에서 "계산"한다 — 팔레트가 바뀌면 상태색도 따라 바뀐다.

    public Color ButtonNormal => sub2;
    public Color ButtonHighlighted => ShiftValue(sub2, +0.12f);
    public Color ButtonPressed => ShiftValue(sub2, -0.15f);
    /// <summary>
    /// Selected(키보드·패드 포커스)는 배경을 바꾸지 않는다. 마우스로 누른 버튼도 계속 Selected로 남기 때문에
    /// 배경을 강조색으로 칠하면 "흰 글자 + 노란 배경"(대비 1.5:1)이 오래 보인다. 대신 강조색 테두리(FocusRing)를 두른다.
    /// </summary>
    public Color ButtonSelected => sub2;
    public Color FocusRing => accent;
    public Color ButtonDisabled => Disabled(sub2);

    /// <summary>씬 전체에 곱해지는 환경광 — 주조색의 색상만 옅게 남긴 흰색.</summary>
    public Color Ambient => HSV(Hue(main), 0.25f, 1f);

    public static Color Disabled(Color c)
    {
        float v = Mathf.Lerp(Luma(c), 0.4f, 0.5f);
        return new Color(v, v, v, 0.6f);   // 채도 0 · 명도 40% 쪽으로 · 반투명 — 색을 못 봐도 "꺼짐"이 보인다
    }

    // ────────────────────────────────────────────────────────── 생성 (수학적 색체계)

    /// <summary>
    /// 기준색 하나와 조화 규칙으로 1·2·1 팔레트를 만든다. 색상(H)은 색상환 각도로, 명도(V)는 역할로 정한다.
    ///   유사      : Sub2 = H+30°,  Accent = H−30°
    ///   보색      : Sub2 = H,      Accent = H+180°
    ///   분할 보색 : Sub2 = H+150°, Accent = H+210°
    ///   삼각      : Sub2 = H+120°, Accent = H+240°
    /// </summary>
    public void Generate(Color baseColor, Harmony h)
    {
        harmony = h;
        Color.RGBToHSV(baseColor, out float H, out float S, out _);
        float d2, dA;
        switch (h)
        {
            case Harmony.Analogous:     d2 = 30f;  dA = -30f; break;
            case Harmony.Complementary: d2 = 0f;   dA = 180f; break;
            case Harmony.Triadic:       d2 = 120f; dA = 240f; break;
            default:                    d2 = 150f; dA = 210f; break;
        }
        float s = Mathf.Clamp(S, 0.15f, 0.45f);
        main = HSV(H, s, 0.17f);                      // 60% — 어둡게 깔린다
        sub1 = HSV(H, s * 0.9f, 0.27f);               // 30% — 같은 색상, 한 단계 밝게
        sub2 = HSV(H + d2 / 360f, 0.50f, 0.50f);      // 30% — 버튼 (흰 글자 4.5:1을 지키는 명도)
        accent = HSV(H + dA / 360f, 0.70f, 0.98f);    // 10% — 가장 밝고 진하게
        text = HSV(H, 0.05f, 0.97f);
        danger = new Color32(0xE5, 0x48, 0x4D, 0xFF);
        NotifyChanged();
    }

    // ────────────────────────────────────────────────────────── 색 수학

    public static float Hue(Color c) { Color.RGBToHSV(c, out float h, out _, out _); return h; }

    public static Color HSV(float h, float s, float v) => Color.HSVToRGB(Mathf.Repeat(h, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));

    public static Color ShiftValue(Color c, float dv)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var r = Color.HSVToRGB(h, s, Mathf.Clamp01(v + dv));
        r.a = c.a;
        return r;
    }

    /// <summary>WCAG 상대 휘도 — sRGB 값을 선형으로 편 뒤 사람 눈의 민감도(녹 > 적 > 청)로 가중합한다.</summary>
    public static float Luminance(Color c)
    {
        static float L(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
    }

    /// <summary>흑백 보기에서의 밝기(0~1, sRGB) — 상대 휘도를 다시 화면 값으로 감는다.</summary>
    public static float Luma(Color c)
    {
        float y = Luminance(c);
        return y <= 0.0031308f ? y * 12.92f : 1.055f * Mathf.Pow(y, 1f / 2.4f) - 0.055f;
    }

    public static float Contrast(Color a, Color b)
    {
        float la = Luminance(a), lb = Luminance(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    /// <summary>적록 색약(제2색맹, Machado 2009) 근사 — 선형 RGB에서 3×3 행렬을 곱한다.</summary>
    public static Color Deuteranopia(Color c)
    {
        var l = c.linear;
        var r = new Color(
            0.367322f * l.r + 0.860646f * l.g - 0.227968f * l.b,
            0.280085f * l.r + 0.672501f * l.g + 0.047413f * l.b,
            -0.011820f * l.r + 0.042940f * l.g + 0.968881f * l.b, c.a);
        r.r = Mathf.Clamp01(r.r); r.g = Mathf.Clamp01(r.g); r.b = Mathf.Clamp01(r.b);
        return r.gamma;
    }

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
