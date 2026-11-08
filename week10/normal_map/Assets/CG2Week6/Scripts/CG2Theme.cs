using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// 6주차 — 팔레트 에셋(CG2Palette) 하나로 씬 전체를 칠한다.
///
/// 칠하는 곳 (색의 세 층 + UI)
///   ① 씬 톤     Global Light 2D 색 · 카메라 배경         — 화면 전체에 곱해진다
///   ② 레이어 톤  Tilemap_Ground · Tilemap_Wall 의 color  — 타일 그림은 그대로, 정점 색만 바뀐다
///   ③ UI        패널·버튼 5상태·바·글자                  — 5주차에 흩어져 있던 하드코딩 색 7개를 대체
/// 개체 틴트(SpriteRenderer.color)는 4단계 측정 도구(CG2TintBench)에서 따로 다룬다.
///
/// Play 중 T 키 — presets를 차례로 바꿔 끼운다(테마 스왑). 팔레트 에셋을 Inspector에서 고치면 즉시 다시 칠한다.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class CG2Theme : MonoBehaviour
{
    public CG2Palette palette;
    [Tooltip("Play 중 T 키로 바꿔 끼울 테마들. 비어 있으면 스왑하지 않는다.")]
    public CG2Palette[] presets;

    [Header("단계별 적용 — 3단계 UI · 4단계 씬 톤")]
    public bool applyUI = true;
    public bool applySceneTone = true;

    [Header("레이어 톤 — 타일에 섞을 정도 (0 = 원본 그대로)")]
    [Range(0, 1)] public float groundTint = 0.7f;
    [Range(0, 1)] public float wallTint = 0.45f;
    [Tooltip("캐릭터 조명(CG2 Entity Light)에 환경광을 섞는 정도. 0 = 흰색(가독성 최우선), 1 = 배경과 같은 톤")]
    [Range(0, 1)] public float characterTint = 0.25f;

    public const string ENTITY_LIGHT = "CG2 Entity Light";

    [Tooltip("버튼이 비활성일 때 글자·아이콘 알파 — 색만으로 상태를 전달하지 않는다")]
    [Range(0, 1)] public float disabledContentAlpha = 0.4f;

    [Tooltip("포커스 링(키보드·패드 선택) — 9-슬라이스 테두리 스프라이트")]
    public Sprite focusRingSprite;
    public float ringOutset = 4f;

    readonly List<(Selectable sel, Graphic[] content, Image ring)> _buttons = new();
    CG2Palette _original;
    int _presetIndex = -1;

    public CG2Palette Current => palette;

    void OnEnable()
    {
        CG2Palette.Changed += OnPaletteChanged;
        Apply();
    }

    void OnDisable() => CG2Palette.Changed -= OnPaletteChanged;

    void OnPaletteChanged(CG2Palette p)
    {
        if (p == palette) Apply();
    }

    void Update()
    {
        if (!Application.isPlaying || presets == null || presets.Length == 0) return;
        var kb = Keyboard.current;
        if (kb != null && kb.tKey.wasPressedThisFrame) NextPreset();
    }

    /// <summary>프리셋을 차례로 끼우고, 한 바퀴 돌면 원래 팔레트로 돌아온다.</summary>
    public void NextPreset()
    {
        if (_original == null) _original = palette;
        _presetIndex++;
        palette = _presetIndex < presets.Length ? presets[_presetIndex] : _original;
        if (_presetIndex >= presets.Length) _presetIndex = -1;
        Apply();
        Debug.Log($"[테마] {palette.themeName} — 주조 {CG2Palette.Hex(palette.main)} · 강조 {CG2Palette.Hex(palette.accent)}");
    }

    void LateUpdate()
    {
        var selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        foreach (var (sel, content, ring) in _buttons)
        {
            if (sel == null) continue;
            bool on = sel.IsInteractable();
            // 비활성 버튼은 배경(ColorBlock)만 회색이 된다. 글자·아이콘도 함께 낮춰 "못 누른다"를 색 없이도 읽히게 한다.
            float a = on ? 1f : disabledContentAlpha;
            foreach (var g in content)
            {
                if (g == null) continue;
                var c = g.color;
                if (!Mathf.Approximately(c.a, a)) { c.a = a; g.color = c; }
            }
            // 선택(포커스)은 배경색이 아니라 테두리로 — 배경을 강조색으로 칠하면 흰 글자가 안 읽힌다.
            if (ring != null) ring.enabled = focusRingSprite != null && on && selected == sel.gameObject;
        }
    }

    // ────────────────────────────────────────────────────────── 칠하기

    public void Apply()
    {
        if (palette == null) return;
        var p = palette;

        if (applySceneTone)
        {
            // ① 씬 톤
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = p.main;
            // 배경 조명(Ground·Wall)은 테마 톤, 캐릭터 조명(CG2 Entity Light)은 흰색 쪽으로 — 톤 때문에 주인공이 묻히지 않게
            foreach (var l in FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global)
                    l.color = l.name == ENTITY_LIGHT ? Color.Lerp(Color.white, p.Ambient, characterTint) : p.Ambient;

            // ② 레이어 톤
            Tint("Tilemap_Ground", Color.Lerp(Color.white, CG2Palette.HSV(CG2Palette.Hue(p.main), 0.5f, 1f), groundTint));
            Tint("Tilemap_Wall", Color.Lerp(Color.white, CG2Palette.HSV(CG2Palette.Hue(p.sub1), 0.4f, 1f), wallTint));
        }

        // ③ UI
        _buttons.Clear();
        if (!applyUI) return;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (!canvas.isRootCanvas) continue;
            foreach (var g in canvas.GetComponentsInChildren<Graphic>(true)) PaintGraphic(g, p);
            foreach (var s in canvas.GetComponentsInChildren<Selectable>(true)) PaintSelectable(s, p);
        }
    }

    static void Tint(string name, Color c)
    {
        var go = GameObject.Find(name);
        if (go != null && go.TryGetComponent<Tilemap>(out var tm)) tm.color = c;
    }

    static void PaintGraphic(Graphic g, CG2Palette p)
    {
        string n = g.name, parent = g.transform.parent != null ? g.transform.parent.name : "";

        if (g is TMP_Text t)
        {
            // 제목은 강조색, 나머지 글자는 글자색
            t.color = n == "Title" ? p.accent : p.text;
            return;
        }
        if (g is not Image img) return;

        if (n == "TopBar") img.color = WithAlpha(p.main, 0.85f);
        else if (n == "Window") img.color = WithAlpha(p.sub1, 0.97f);
        else if (n.EndsWith("Panel")) img.color = WithAlpha(p.main, 0.7f);          // 팝업 뒤 어둡게 깔리는 막
        else if (n == "BG" && (parent == "HpBar" || parent == "ExpBar")) img.color = WithAlpha(CG2Palette.ShiftValue(p.main, -0.05f), 0.9f);
        else if (n == "F" && Ancestor(g.transform, "HpBar")) img.color = p.danger;   // 체력 = 위험(기능색)
        else if (n == "F" && Ancestor(g.transform, "ExpBar")) img.color = p.accent;  // 경험치 = 보상(강조)
    }

    void PaintSelectable(Selectable s, CG2Palette p)
    {
        if (s is Slider)
        {
            // 표시용 바 — 상태 전환이 채움 색에 곱해지지 않게 끈다.
            s.transition = Selectable.Transition.None;
            return;
        }
        if (s.targetGraphic == null) return;

        // Color Tint는 Image.color × 상태색을 곱한다. Image에 색이 들어 있으면 상태색이 "어둡게"밖에 못 만든다.
        // Image는 흰색(곱셈의 항등원)으로 두고, 색은 전부 상태 규칙(ColorBlock)이 맡는다.
        s.targetGraphic.color = Color.white;
        s.transition = Selectable.Transition.ColorTint;
        s.colors = new ColorBlock
        {
            normalColor = p.ButtonNormal,
            highlightedColor = p.ButtonHighlighted,
            pressedColor = p.ButtonPressed,
            selectedColor = p.ButtonSelected,
            disabledColor = p.ButtonDisabled,
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        var ring = FocusRing(s, p);
        var content = new List<Graphic>();
        foreach (var g in s.GetComponentsInChildren<Graphic>(true))
            if (g != s.targetGraphic && g != ring) content.Add(g);
        _buttons.Add((s, content.ToArray(), ring));
    }

    /// <summary>
    /// 포커스 링 — 버튼의 자식 Image(9-슬라이스 테두리 스프라이트)로 둔다.
    /// 버튼 자신에 Outline 효과를 붙이면 Color Tint가 렌더러 전체에 곱해져 테두리까지 어두워진다(남색 × 금색 = 올리브).
    /// 자식은 자기 CanvasRenderer를 따로 가지므로 Tint의 영향을 받지 않는다. 가운데가 투명해 글자를 가리지 않는다.
    /// </summary>
    Image FocusRing(Selectable s, CG2Palette p)
    {
        var t = s.transform.Find("FocusRing");
        Image ring;
        if (t == null)
        {
            var go = new GameObject("FocusRing", typeof(RectTransform), typeof(Image)) { layer = s.gameObject.layer };
            go.transform.SetParent(s.transform, false);
            ring = go.GetComponent<Image>();
        }
        else ring = t.GetComponent<Image>();
        var rt = (RectTransform)ring.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-ringOutset, -ringOutset); rt.offsetMax = new Vector2(ringOutset, ringOutset);
        ring.sprite = focusRingSprite;
        ring.type = Image.Type.Sliced;
        ring.color = p.FocusRing;
        ring.raycastTarget = false;
        ring.enabled = false;
        return ring;
    }

    static bool Ancestor(Transform t, string name)
    {
        for (var x = t.parent; x != null; x = x.parent) if (x.name == name) return true;
        return false;
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
