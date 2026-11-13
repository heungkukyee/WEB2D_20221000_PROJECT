#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// CG2 Starter Scene 자동 빌더 — v7 (Unity 6000.5+ 대응)
/// 수정 이력:
///   v5: GraphicRaycaster 네임스페이스 수정 + InputSystemUIInputModule
///   v6: FindFirstObjectByType API 변경 대응
///       TextAlignmentOptions.TopCenter 제거 → Top 사용
///       SRP Batcher 경고 제거 (HitReceiver)
///       linearVelocity 경고 주석 추가
///   v7: FindFirstObjectByType → FindAnyObjectByType (Unity 6에서 deprecated)
///       Save()에 PPU 64 명시 — 미지정 시 Unity 기본값 100이 박혀
///       스프라이트가 콜라이더보다 1.4~1.7배 작아지던 문제 수정
/// 메뉴: CG2 Starter -> Step 1 -> Step 2 -> Step 3
/// </summary>
public static class StarterSceneBuilder
{
    const string ART = "Assets/StarterProject/Art";

    [MenuItem("CG2 Starter/Step 1 - Add Tags (Player, Enemy, Projectile)")]
    public static void AddTags()
    {
        Tag("Player"); Tag("Enemy"); Tag("Projectile");
        Debug.Log("[CG2] Tags 추가 완료: Player / Enemy / Projectile");
    }

    [MenuItem("CG2 Starter/Step 2 - Create Placeholder Sprites")]
    public static void CreateSprites()
    {
        if (!AssetDatabase.IsValidFolder(ART))
        { AssetDatabase.CreateFolder("Assets/StarterProject", "Art"); AssetDatabase.Refresh(); }

        Circle(new Color(0.3f, 0.6f, 1f),  64, $"{ART}/spr_player.png");
        Circle(new Color(1f, 0.3f, 0.3f),  48, $"{ART}/spr_enemy.png");
        Circle(new Color(1f, 0.9f, 0.2f),  20, $"{ART}/spr_projectile.png");
        Square(new Color(0.4f, 0.4f, 0.4f), 8, $"{ART}/spr_tile.png");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[CG2] Placeholder Sprites 생성 완료");
    }

    [MenuItem("CG2 Starter/Step 3 - Build Starter Scene")]
    public static void BuildScene()
    {
        foreach (var n in new[] { "Player", "GameManager", "EnemySpawner", "Canvas", "EventSystem" })
        { var g = GameObject.Find(n); if (g) Object.DestroyImmediate(g); }

        string pd = "Assets/StarterProject/Prefabs";
        if (!AssetDatabase.IsValidFolder(pd))
        { AssetDatabase.CreateFolder("Assets/StarterProject", "Prefabs"); AssetDatabase.Refresh(); }

        var ep = MakeEnemyPrefab(Spr("spr_enemy"),      pd);
        var pp = MakeProjPrefab (Spr("spr_projectile"), pd);

        new GameObject("GameManager").AddComponent<GameManager>();

        var pl  = new GameObject("Player"); pl.tag = "Player";
        var psr = pl.AddComponent<SpriteRenderer>(); psr.sprite = Spr("spr_player"); psr.sortingOrder = 2;
        var prb = pl.AddComponent<Rigidbody2D>(); prb.gravityScale = 0f; prb.constraints = RigidbodyConstraints2D.FreezeRotation;
        pl.AddComponent<CircleCollider2D>().radius = 0.45f;
        pl.AddComponent<PlayerController>(); pl.AddComponent<HitReceiver>(); pl.AddComponent<Health>();
        SF(pl.AddComponent<WeaponController>(), "projectilePrefab", pp);

        SF(new GameObject("EnemySpawner").AddComponent<EnemySpawner>(), "enemyPrefab", ep);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = 7f; cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            SF(cam.gameObject.AddComponent<CameraFollow>(), "target", pl.transform);
        }

        BuildUI(pl);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[CG2] Starter Scene 빌드 완료! ▶ Play 버튼으로 테스트하세요.");
    }

    static GameObject MakeEnemyPrefab(Sprite s, string dir)
    {
        var g  = new GameObject("Enemy_Prefab"); g.tag = "Enemy";
        var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = 1;
        var rb = g.AddComponent<Rigidbody2D>(); rb.gravityScale = 0f; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        g.AddComponent<CircleCollider2D>().radius = 0.4f;
        g.AddComponent<EnemyAI>(); g.AddComponent<HitReceiver>(); g.AddComponent<Health>();
        var pf = PrefabUtility.SaveAsPrefabAsset(g, $"{dir}/Enemy.prefab");
        Object.DestroyImmediate(g); return pf;
    }

    static GameObject MakeProjPrefab(Sprite s, string dir)
    {
        var g   = new GameObject("Proj_Prefab"); g.tag = "Projectile";
        var sr  = g.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = 3;
        var rb  = g.AddComponent<Rigidbody2D>(); rb.gravityScale = 0f; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var col = g.AddComponent<CircleCollider2D>(); col.radius = 0.12f; col.isTrigger = true;
        g.AddComponent<Projectile>();
        var pf = PrefabUtility.SaveAsPrefabAsset(g, $"{dir}/Projectile.prefab");
        Object.DestroyImmediate(g); return pf;
    }

    static void BuildUI(GameObject pl)
    {
        var cgo = new GameObject("Canvas");
        var cv  = cgo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cgo.AddComponent<GraphicRaycaster>(); // UnityEngine.UI 소속

        // Unity 6000.5 대응: FindFirstObjectByType는 인스턴스 ID 순서에 의존해 deprecated 되었다.
        // 여기서는 "EventSystem이 하나라도 있는가"만 보므로 순서가 무의미하고,
        // 순서를 따지지 않는 FindAnyObjectByType가 더 빠르고 의미도 정확하다.
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // New Input System 전용 모듈 (InputSystem 패키지 필수)
            es.AddComponent<InputSystemUIInputModule>();
        }

        var hpB = MkSlider(cgo.transform, "HpBar",  new Vector2(10, -10), new Vector2(260, 20), new Color(0.8f, 0.15f, 0.15f));
        var exB = MkSlider(cgo.transform, "ExpBar", new Vector2(10, -36), new Vector2(200, 14), new Color(0.2f, 0.6f,  1f));
        var scT = MkTMP(cgo.transform, "ScoreText", new Vector2(-10, -10), new Vector2(200, 30), TextAlignmentOptions.TopRight);
        var lvT = MkTMP(cgo.transform, "LevelText", new Vector2(-10, -40), new Vector2(200, 30), TextAlignmentOptions.TopRight);
        // [v6 fix] TopCenter → Top (TMPro에 TopCenter 없음)
        var tmT = MkTMP(cgo.transform, "TimeText",  new Vector2(0,   -10), new Vector2(200, 30), TextAlignmentOptions.Top);
        scT.text = "Score:0"; lvT.text = "Lv.1"; tmT.text = "0s";

        var gop = MkPanel(cgo.transform, "GameOverPanel", new Color(0, 0, 0, 0.75f));
        var got = MkTMP(gop.transform, "ResultText", Vector2.zero, new Vector2(400, 200), TextAlignmentOptions.Center);
        got.fontSize = 32; got.text = "GAME OVER";
        var btn = MkBtn(gop.transform, "RestartBtn", "Restart", new Vector2(0, -80), new Vector2(160, 50));

        var hud = cgo.AddComponent<HUD>();
        SF(hud, "hpBar",             hpB.GetComponent<Slider>());
        SF(hud, "expBar",            exB.GetComponent<Slider>());
        SF(hud, "scoreText",         scT);
        SF(hud, "levelText",         lvT);
        SF(hud, "timeText",          tmT);
        SF(hud, "gameOverPanel",     gop);
        SF(hud, "gameOverResultText",got);
        SF(hud, "restartButton",     btn.GetComponent<Button>());
    }

    static GameObject MkSlider(Transform p, string n, Vector2 pos, Vector2 sz, Color fc)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var bg = new GameObject("BG"); bg.transform.SetParent(go.transform, false);
        bg.AddComponent<Image>().color = new Color(.1f, .1f, .1f, .8f);
        var br = bg.GetComponent<RectTransform>(); br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = Vector2.zero;
        var fa = new GameObject("FA"); fa.transform.SetParent(go.transform, false);
        var fr = fa.AddComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.sizeDelta = Vector2.zero;
        var f  = new GameObject("F"); f.transform.SetParent(fa.transform, false); f.AddComponent<Image>().color = fc;
        var ffr = f.GetComponent<RectTransform>(); ffr.anchorMin = Vector2.zero; ffr.anchorMax = Vector2.one; ffr.sizeDelta = Vector2.zero;
        var sl = go.AddComponent<Slider>(); sl.fillRect = ffr; sl.value = 1f; sl.interactable = false;
        return go;
    }

    static TextMeshProUGUI MkTMP(Transform p, string n, Vector2 pos, Vector2 sz, TextAlignmentOptions a)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var t  = go.AddComponent<TextMeshProUGUI>(); t.alignment = a; t.fontSize = 18; t.color = Color.white;
        return t;
    }

    static GameObject MkPanel(Transform p, string n, Color c)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        go.AddComponent<Image>().color = c; return go;
    }

    static GameObject MkBtn(Transform p, string n, string lbl, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = sz;
        go.AddComponent<Image>().color = new Color(.3f, .7f, .3f); go.AddComponent<Button>();
        var tgo = new GameObject("T"); tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>(); tmp.text = lbl;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.fontSize = 20;
        var tr = tgo.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
        return go;
    }

    static void Circle(Color c, int s, string path)
    {
        var t  = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float cx = s * .5f, cy = s * .5f, r = s * .5f;
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            { float dx = x - cx, dy = y - cy; px[y * s + x] = dx * dx + dy * dy <= r * r ? c : Color.clear; }
        t.SetPixels(px); t.Apply(); Save(t, path);
    }

    static void Square(Color c, int s, string path)
    {
        var t  = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color[s * s]; for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels(px); t.Apply(); Save(t, path);
    }

    static void Save(Texture2D t, string path)
    {
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.filterMode          = FilterMode.Point;
            // [v7] PPU를 명시하지 않으면 Unity 기본값 100이 박힌다. 프로젝트 규격은 64다.
            // 100일 때 Player 스프라이트는 0.64유닛인데 콜라이더 지름은 0.90유닛이라
            // 히트박스가 1.4배 어긋난다. 64로 맞추면 1.00 대 0.90으로 들어맞는다.
            ti.spritePixelsPerUnit = 64f;
            ti.SaveAndReimport();
        }
    }

    static Sprite Spr(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ART}/{n}.png");

    static void SF(object o, string f, object v)
    {
        var fi = o.GetType().GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fi?.SetValue(o, v);
    }

    static void Tag(string t)
    {
        var tm   = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == t) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = t;
        tm.ApplyModifiedProperties();
    }
}
#endif
