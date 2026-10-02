using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 5주차 3단계 — Starter의 HUD를 정적·동적·팝업 Canvas 3개로 재구성하고 레벨업·상점 창을 붙인다.
///
/// 왜 나누는가 —
///   Canvas 안의 UI 요소 하나라도 바뀌면(HP바 길이, 시간 텍스트) Unity는 그 Canvas 전체의
///   메시를 다시 만든다(Canvas.BuildBatch). 아이콘·배경처럼 절대 안 바뀌는 요소가 같은 Canvas에
///   있으면 매번 함께 다시 계산된다. 자주 바뀌는 것과 안 바뀌는 것을 다른 Canvas로 떼어 놓는다.
///   대신 Canvas끼리는 배칭되지 않으므로 드로우콜은 조금 늘 수 있다 — 이 트레이드오프를 측정한다.
///
/// Starter v8 HUD의 버그도 여기서 고친다 —
///   ScoreText·LevelText·TimeText는 앵커가 화면 중앙(기본값)이라 창 크기가 바뀌면 가운데로 몰린다.
///   CanvasScaler 기준 해상도도 기본값(800×600)이라 1920×1080 화면에서 UI가 2배 가까이 커진다.
/// </summary>
public static class UIBuilder
{
    const string DIR = "Assets/CG2Week5";
    const string ICON_DIR = DIR + "/Art/Placeholder";
    const string ICON_SET = DIR + "/Art/CG2IconSet.asset";

    const string STATIC = "Canvas_HUD_Static";
    const string DYNAMIC = "Canvas_HUD_Dynamic";
    const string POPUP = "Canvas_Popup";
    static readonly Vector2 REF = new(1920, 1080);

    [MenuItem("CG2 Lab/5주차/3. UI 구성 — 정적·동적·팝업 Canvas 분리", priority = 30)]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("UI 구성", "Play를 멈춘 뒤 실행하세요. Play 중에 만든 UI는 Stop과 함께 사라집니다.", "확인");
            return;
        }
        var hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            EditorUtility.DisplayDialog("UI 구성", "HUD가 없습니다. 2주차 'CG2 Starter → Step 3'을 먼저 실행하세요.", "확인");
            return;
        }

        var iconSet = EnsureIconSet();

        Undo.SetCurrentGroupName("CG2 UI 구성");
        int group = Undo.GetCurrentGroup();

        // ── 동적 Canvas: Starter의 Canvas를 그대로 살려 이름과 규격만 고친다 ──
        var dyn = hud.GetComponent<Canvas>();
        Undo.RecordObject(dyn.gameObject, "rename");
        dyn.gameObject.name = DYNAMIC;
        SetupCanvas(dyn, 1, raycast: true);

        var so = new SerializedObject(hud);
        var hpBar = (Slider)so.FindProperty("hpBar").objectReferenceValue;
        var expBar = (Slider)so.FindProperty("expBar").objectReferenceValue;
        var scoreT = (TextMeshProUGUI)so.FindProperty("scoreText").objectReferenceValue;
        var levelT = (TextMeshProUGUI)so.FindProperty("levelText").objectReferenceValue;
        var timeT = (TextMeshProUGUI)so.FindProperty("timeText").objectReferenceValue;
        var gameOver = (GameObject)so.FindProperty("gameOverPanel").objectReferenceValue;

        Place(hpBar, TL, new Vector2(64, -20), new Vector2(360, 28));
        Place(expBar, TL, new Vector2(64, -60), new Vector2(300, 16));
        // Starter는 Slider 초기값이 1이라 경험치 0인데도 EXP바가 가득 찬 채로 시작한다.
        if (expBar) { Undo.RecordObject(expBar, "exp"); expBar.value = 0f; }
        PlaceText(scoreT, TR, new Vector2(-24, -16), new Vector2(220, 40), 30, TextAlignmentOptions.TopLeft);   // 코인 아이콘 바로 오른쪽에서 시작
        PlaceText(levelT, TR, new Vector2(-24, -58), new Vector2(260, 36), 26, TextAlignmentOptions.TopRight);
        PlaceText(timeT, TC, new Vector2(0, -14), new Vector2(240, 52), 40, TextAlignmentOptions.Top);

        var shopBtn = FindOrMake(dyn.transform, "ShopButton");
        Place(shopBtn, TR, new Vector2(-24, -104), new Vector2(160, 44));
        MakeButtonVisual(shopBtn, "SHOP [Tab]", new Color(0.18f, 0.45f, 0.75f));

        // ── 정적 Canvas: 배경 띠 + HUD 아이콘. 한 번 그려지면 바뀌지 않는다 ──
        var stat = FindOrMakeCanvas(STATIC);
        SetupCanvas(stat, 0, raycast: false);
        var bar = FindOrMake(stat.transform, "TopBar");
        Place(bar, new Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1)), Vector2.zero, new Vector2(0, 96));
        Img(bar, null, new Color(0f, 0f, 0f, 0.35f));
        Icon(stat.transform, "Icon_HP", CG2IconSet.HP, iconSet, TL, new Vector2(20, -18), 32);
        Icon(stat.transform, "Icon_EXP", CG2IconSet.EXP, iconSet, TL, new Vector2(20, -52), 32);
        Icon(stat.transform, "Icon_Coin", CG2IconSet.COIN, iconSet, TR, new Vector2(-252, -18), 32);

        // ── 팝업 Canvas: 레벨업·상점·게임오버. 평소엔 꺼져 있다 ──
        var pop = FindOrMakeCanvas(POPUP);
        SetupCanvas(pop, 10, raycast: true);
        if (gameOver != null && gameOver.transform.parent != pop.transform)
            Undo.SetTransformParent(gameOver.transform, pop.transform, "GameOver 이동");

        var levelUp = BuildLevelUp(pop, iconSet);
        BuildShop(pop, iconSet, shopBtn.GetComponent<Button>(), levelUp);

        // Starter가 만든 슬라이더 배경·채움, 게임오버 글자까지 Raycast Target이 켜져 있다.
        // 클릭을 받는 것(버튼)과 뒤를 막는 것(…Panel 배경)만 남기고 끈다.
        int trimmed = 0;
        foreach (var c in new[] { stat, dyn, pop })
            foreach (var g in c.GetComponentsInChildren<Graphic>(true))
                if (g.raycastTarget && g.GetComponent<Selectable>() == null && !g.name.EndsWith("Panel"))
                {
                    Undo.RecordObject(g, "raycast");
                    g.raycastTarget = false;
                    trimmed++;
                }

        Undo.CollapseUndoOperations(group);
        MarkDirty();
        Selection.activeGameObject = pop.gameObject;

        Debug.Log(
            "[검증] UI 구성 완료 — Canvas 3개로 분리\n" +
            $"        {STATIC,-20} order 0 · Raycaster 없음 · {Count(stat)}개 요소 (배경·아이콘 — 바뀌지 않음)\n" +
            $"        {DYNAMIC,-20} order 1 · Raycaster 있음 · {Count(dyn)}개 요소 (HP·EXP·점수·시간 — 자주 바뀜)\n" +
            $"        {POPUP,-20} order 10 · Raycaster 있음 · {Count(pop)}개 요소 (레벨업·상점·게임오버 — 평소 꺼짐)\n" +
            $"        CanvasScaler : Scale With Screen Size {REF.x}×{REF.y} · Match 0.5  (Starter 기본값 800×600 수정)\n" +
            "        앵커 수정 : Score·Level → 우상단 / Time → 상단 중앙 / HP·EXP → 좌상단\n" +
            $"        Raycast Target 정리 : 클릭 대상이 아닌 Graphic {trimmed}개 끔 (버튼·패널만 남김)\n" +
            "        ── Play 해서 확인 ──  ① 레벨업 시 3택 창이 뜨고 게임이 멈추는가  ② Tab으로 상점이 열리는가");
    }

    // ────────────────────────────────────────────────────────── Render Mode 비교

    [MenuItem("CG2 Lab/5주차/3-1. Render Mode — Screen Space Overlay (기본)", priority = 31)]
    static void ToOverlay() => SetMode(RenderMode.ScreenSpaceOverlay);

    [MenuItem("CG2 Lab/5주차/3-2. Render Mode — Screen Space Camera", priority = 32)]
    static void ToCamera() => SetMode(RenderMode.ScreenSpaceCamera);

    [MenuItem("CG2 Lab/5주차/3-3. Render Mode — World Space", priority = 33)]
    static void ToWorld() => SetMode(RenderMode.WorldSpace);

    static void SetMode(RenderMode mode)
    {
        var cam = Camera.main;
        var lines = new List<string>();
        foreach (var name in new[] { STATIC, DYNAMIC, POPUP })
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var c = go.GetComponent<Canvas>();
            Undo.RecordObject(c, "Render Mode");
            Undo.RecordObject(c.transform, "Render Mode");
            c.renderMode = mode;
            if (mode == RenderMode.ScreenSpaceCamera)
            {
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            else if (mode == RenderMode.WorldSpace)
            {
                c.worldCamera = cam;
                // 월드 공간: 1920×1080 캔버스를 월드 유닛으로 줄여 플레이어 주변(원점)에 둔다.
                var rt = (RectTransform)c.transform;
                rt.sizeDelta = REF;
                rt.position = Vector3.zero;
                rt.localScale = Vector3.one * (14f / REF.y);   // 세로 14유닛 = Size 7 화면 높이
            }
            lines.Add($"{name,-20} sortingLayer '{c.sortingLayerName}' · order {c.sortingOrder}");
        }
        MarkDirty();

        string warn = mode == RenderMode.ScreenSpaceOverlay
            ? "Overlay : 카메라와 무관하게 모든 렌더링이 끝난 뒤 화면 맨 위에 그린다. Sorting Layer 영향 없음."
            : UiLayerIsTop()
                ? "Canvas가 'UI' Sorting Layer(최상단)에 있어 캐릭터에 가려지지 않습니다."
                : "⚠ Canvas가 'Default' Sorting Layer에 있습니다. Entity·FX(캐릭터·투사체)가 UI를 덮습니다.\n" +
                  "          → 'CG2 Lab → 5주차 → 3-4. UI Sorting Layer 등록'으로 해결하세요.";
        Debug.Log($"[검증] Canvas Render Mode → {mode}\n        {string.Join("\n        ", lines)}\n        {warn}\n" +
                  "        Frame Debugger에서 UI가 그려지는 위치(카메라 패스 안 / 밖)를 비교하세요.");
    }

    [MenuItem("CG2 Lab/5주차/3-4. UI Sorting Layer 등록 (최상단)", priority = 34)]
    static void EnsureUiSortingLayer()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("m_SortingLayers");
        int idx = -1;
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "UI") idx = i;

        if (idx < 0)
        {
            layers.arraySize++;
            idx = layers.arraySize - 1;
            var e = layers.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("name").stringValue = "UI";
            e.FindPropertyRelative("uniqueID").intValue = 0x55490000 | 0x4C;   // 고정 ID ("UI")
        }
        else if (idx != layers.arraySize - 1)
        {
            layers.MoveArrayElement(idx, layers.arraySize - 1);   // 맨 뒤 = 가장 위에 그려진다
        }
        tm.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        foreach (var name in new[] { STATIC, DYNAMIC, POPUP })
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var c = go.GetComponent<Canvas>();
            Undo.RecordObject(c, "UI layer");
            c.sortingLayerName = "UI";
        }
        MarkDirty();

        var names = new List<string>();
        foreach (var l in SortingLayer.layers) names.Add(l.name);
        Debug.Log($"[검증] UI Sorting Layer 등록 — {string.Join(" → ", names)}\n" +
                  "        CG2 Canvas 3개를 'UI' 레이어로 옮겼습니다. Screen Space Camera에서도 캐릭터 위에 그려집니다.\n" +
                  "        ※ 4주차 '2. Sorting Layer 등록'을 다시 실행하면 UI가 가운데로 밀리니 이 메뉴를 다시 실행하세요.");
    }

    static bool UiLayerIsTop()
    {
        var layers = SortingLayer.layers;
        if (layers.Length == 0 || layers[^1].name != "UI") return false;
        var go = GameObject.Find(DYNAMIC);
        return go != null && go.GetComponent<Canvas>().sortingLayerName == "UI";
    }

    // ────────────────────────────────────────────────────────── 팝업 구성

    static LevelUpPanel BuildLevelUp(Canvas pop, CG2IconSet set)
    {
        var root = FindOrMake(pop.transform, "LevelUpPanel");
        Place(root, Stretch, Vector2.zero, Vector2.zero);
        Img(root, null, new Color(0, 0, 0, 0.6f));

        var win = FindOrMake(root.transform, "Window");
        Place(win, MC, Vector2.zero, new Vector2(1000, 520));
        Img(win, null, new Color(0.12f, 0.12f, 0.18f, 0.95f));

        var title = Text(win.transform, "Title", "LEVEL UP!", 44, TC, new Vector2(0, -24), new Vector2(900, 60), TextAlignmentOptions.Top);

        var cards = new Button[3]; var icons = new Image[3];
        var titles = new TextMeshProUGUI[3]; var descs = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            var card = FindOrMake(win.transform, $"Card{i}");
            Place(card, MC, new Vector2((i - 1) * 320, -30), new Vector2(290, 320));
            MakeButtonVisual(card, null, new Color(0.22f, 0.24f, 0.32f));
            cards[i] = card.GetComponent<Button>();
            icons[i] = Icon(card.transform, "Icon", CG2IconSet.DAMAGE, set, TC, new Vector2(0, -36), 96, uiIcon: false);
            titles[i] = Text(card.transform, "Name", "Power", 34, TC, new Vector2(0, -150), new Vector2(260, 48), TextAlignmentOptions.Top);
            descs[i] = Text(card.transform, "Desc", "Damage +20%", 24, TC, new Vector2(0, -210), new Vector2(260, 100), TextAlignmentOptions.Top);
        }
        root.SetActive(false);

        var panel = GetOrAdd<LevelUpPanel>(pop.gameObject);
        var so = new SerializedObject(panel);
        so.FindProperty("iconSet").objectReferenceValue = set;
        so.FindProperty("root").objectReferenceValue = root;
        so.FindProperty("titleText").objectReferenceValue = title;
        SetArray(so, "cards", cards);
        SetArray(so, "cardIcons", icons);
        SetArray(so, "cardTitles", titles);
        SetArray(so, "cardDescs", descs);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    static void BuildShop(Canvas pop, CG2IconSet set, Button openBtn, LevelUpPanel levelUp)
    {
        var root = FindOrMake(pop.transform, "ShopPanel");
        Place(root, Stretch, Vector2.zero, Vector2.zero);
        Img(root, null, new Color(0, 0, 0, 0.6f));

        var win = FindOrMake(root.transform, "Window");
        Place(win, MC, Vector2.zero, new Vector2(760, 640));
        Img(win, null, new Color(0.12f, 0.12f, 0.18f, 0.95f));

        Text(win.transform, "Title", "SHOP", 44, TC, new Vector2(0, -20), new Vector2(700, 60), TextAlignmentOptions.Top);
        var gold = Text(win.transform, "Gold", "Gold 0", 28, TC, new Vector2(0, -80), new Vector2(700, 40), TextAlignmentOptions.Top);

        var btns = new Button[4]; var icons = new Image[4]; var labels = new TextMeshProUGUI[4];
        string[] keys = { CG2IconSet.HEAL, CG2IconSet.DAMAGE, CG2IconSet.SPEED, CG2IconSet.FIRE_RATE };
        for (int i = 0; i < 4; i++)
        {
            var row = FindOrMake(win.transform, $"Item{i}");
            Place(row, TC, new Vector2(0, -136 - i * 100), new Vector2(680, 88));
            MakeButtonVisual(row, null, new Color(0.22f, 0.24f, 0.32f));
            btns[i] = row.GetComponent<Button>();
            icons[i] = Icon(row.transform, "Icon", keys[i], set, ML, new Vector2(16, 0), 64, uiIcon: false);
            labels[i] = Text(row.transform, "Label", "Item", 28, ML, new Vector2(100, 0), new Vector2(560, 80), TextAlignmentOptions.Left);
        }

        var close = FindOrMake(win.transform, "CloseButton");
        Place(close, BC, new Vector2(0, 20), new Vector2(200, 50));
        MakeButtonVisual(close, "CLOSE [Tab]", new Color(0.5f, 0.25f, 0.25f));
        root.SetActive(false);

        var shop = GetOrAdd<ShopPanel>(pop.gameObject);
        var so = new SerializedObject(shop);
        so.FindProperty("iconSet").objectReferenceValue = set;
        so.FindProperty("root").objectReferenceValue = root;
        so.FindProperty("goldText").objectReferenceValue = gold;
        so.FindProperty("openButton").objectReferenceValue = openBtn;
        so.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>();
        so.FindProperty("levelUp").objectReferenceValue = levelUp;
        SetArray(so, "itemButtons", btns);
        SetArray(so, "itemIcons", icons);
        SetArray(so, "itemLabels", labels);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────────────────────────────────── 아이콘 세트

    /// <summary>
    /// 플레이스홀더 아이콘 8종(32px 픽셀아트)과 아이콘 세트 에셋을 만든다.
    /// 4단계에서 생성형 AI 아이콘으로 교체하기 전까지 쓰는 임시 그림이다.
    /// 이미 있으면 건드리지 않는다 — 학생이 교체한 아이콘을 덮어쓰지 않기 위함이다.
    /// </summary>
    public static CG2IconSet EnsureIconSet()
    {
        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<CG2IconSet>();

        System.IO.Directory.CreateDirectory(ICON_DIR);
        foreach (var key in CG2IconSet.Keys)
        {
            if (set.Get(key) != null) continue;   // 이미 있는(또는 교체한) 아이콘은 그대로 둔다
            string path = $"{ICON_DIR}/icon_{key}.png";
            if (!System.IO.File.Exists(path))
            {
                System.IO.File.WriteAllBytes(path, PlaceholderIcons.Make(key).EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                ArtImport.ApplyPixelSprite(path, ArtImport.Kind.Icon);
            }
            set.Set(key, AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }

        if (created) AssetDatabase.CreateAsset(set, ICON_SET);
        else EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return set;
    }

    // ────────────────────────────────────────────────────────── 헬퍼

    readonly struct Anchor
    {
        public readonly Vector2 min, max, pivot;
        public Anchor(Vector2 min, Vector2 max, Vector2 pivot) { this.min = min; this.max = max; this.pivot = pivot; }
    }

    static readonly Anchor TL = new(new(0, 1), new(0, 1), new(0, 1));
    static readonly Anchor TR = new(new(1, 1), new(1, 1), new(1, 1));
    static readonly Anchor TC = new(new(0.5f, 1), new(0.5f, 1), new(0.5f, 1));
    static readonly Anchor MC = new(new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f));
    static readonly Anchor ML = new(new(0, 0.5f), new(0, 0.5f), new(0, 0.5f));
    static readonly Anchor BC = new(new(0.5f, 0), new(0.5f, 0), new(0.5f, 0));
    static readonly Anchor Stretch = new(Vector2.zero, Vector2.one, new(0.5f, 0.5f));

    static void SetupCanvas(Canvas c, int order, bool raycast)
    {
        Undo.RecordObject(c, "canvas");
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;

        var scaler = GetOrAdd<CanvasScaler>(c.gameObject);
        Undo.RecordObject(scaler, "scaler");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = REF;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var ray = c.GetComponent<GraphicRaycaster>();
        if (raycast && ray == null) Undo.AddComponent<GraphicRaycaster>(c.gameObject);
        if (!raycast && ray != null) Undo.DestroyObjectImmediate(ray);
    }

    static Canvas FindOrMakeCanvas(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.layer = 5; // UI
        }
        return GetOrAdd<Canvas>(go);
    }

    static GameObject FindOrMake(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Place(Component c, Anchor a, Vector2 pos, Vector2 size) { if (c) Place(c.gameObject, a, pos, size); }

    static void Place(GameObject go, Anchor a, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        Undo.RecordObject(rt, "place");
        rt.anchorMin = a.min; rt.anchorMax = a.max; rt.pivot = a.pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void PlaceText(TextMeshProUGUI t, Anchor a, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        if (t == null) return;
        Place(t.gameObject, a, pos, size);
        Undo.RecordObject(t, "text");
        t.fontSize = fontSize; t.alignment = align;
        t.raycastTarget = false;   // 글자는 클릭 대상이 아니다 — Raycaster가 검사할 대상을 줄인다
    }

    static Image Img(GameObject go, Sprite s, Color c)
    {
        var img = GetOrAdd<Image>(go);
        img.sprite = s; img.color = c;
        img.raycastTarget = go.GetComponent<Button>() != null || go.name.EndsWith("Panel");
        return img;
    }

    static Image Icon(Transform parent, string name, string key, CG2IconSet set, Anchor a, Vector2 pos, int px, bool uiIcon = true)
    {
        var go = FindOrMake(parent, name);
        Place(go, a, pos, new Vector2(px, px));
        var img = Img(go, set.Get(key), Color.white);
        img.raycastTarget = false;
        img.preserveAspect = true;
        if (uiIcon)
        {
            var ic = GetOrAdd<CG2UIIcon>(go);
            ic.iconSet = set; ic.key = key;
        }
        return img;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Anchor a, Vector2 pos, Vector2 box, TextAlignmentOptions align)
    {
        var go = FindOrMake(parent, name);
        Place(go, a, pos, box);
        var t = GetOrAdd<TextMeshProUGUI>(go);
        t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static void MakeButtonVisual(GameObject go, string label, Color c)
    {
        Img(go, null, c);
        var b = GetOrAdd<Button>(go);
        go.GetComponent<Image>().raycastTarget = true;
        b.targetGraphic = go.GetComponent<Image>();
        if (label != null)
            Text(go.transform, "Label", label, 22, Stretch, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
    }

    static void SetArray<T>(SerializedObject so, string prop, T[] values) where T : Object
    {
        var p = so.FindProperty(prop);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    /// <summary>Play 중에는 씬을 dirty로 표시할 수 없다(예외). Render Mode 비교는 Play 중에도 해 볼 수 있게 허용한다.</summary>
    static void MarkDirty()
    {
        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // 에디터의 GetComponent는 컴포넌트가 없을 때 "가짜 null" 객체를 돌려줄 수 있어 ?? 연산자를 쓰면 안 된다.
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static int Count(Canvas c) => c.GetComponentsInChildren<Graphic>(true).Length;
}
