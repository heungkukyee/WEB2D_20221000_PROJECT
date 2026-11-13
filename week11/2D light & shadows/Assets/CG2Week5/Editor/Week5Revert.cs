using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 5주차 되돌리기 메뉴 — 4주차와 같이, 단계를 틀려도 그 단계 직전 상태로 돌아가 다시 해 볼 수 있게 한다.
///
///   되돌리기 — 1단계 카메라 리그     리그·Brain 제거, 2주차 CameraFollow 다시 켬
///   되돌리기 — 3단계 UI 구성         Canvas 3개 → Starter v8의 Canvas 1개 배치로 복원
///   되돌리기 — 4단계 에셋 교체       플레이어·적·투사체·아이콘을 플레이스홀더로 복원 (MyArt 파일은 남긴다)
///   되돌리기 — 5단계 Sprite Atlas    아틀라스 2종 삭제
///
/// 어느 것도 학생이 만든 파일(MyArt)을 지우지 않는다.
/// </summary>
public static class Week5Revert
{
    const string STATIC = "Canvas_HUD_Static";
    const string DYNAMIC = "Canvas_HUD_Dynamic";
    const string POPUP = "Canvas_Popup";
    const string ART = "Assets/StarterProject/Art";
    const string ICON_SET = "Assets/CG2Week5/Art/CG2IconSet.asset";
    const string ICON_DIR = "Assets/CG2Week5/Art/Placeholder";
    const string ATLAS_DIR = "Assets/CG2Week5/Atlas";

    static bool Blocked()
    {
        if (!EditorApplication.isPlaying) return false;
        EditorUtility.DisplayDialog("되돌리기", "Play를 멈춘 뒤 실행하세요.", "확인");
        return true;
    }

    [MenuItem("CG2 Lab/5주차/되돌리기 — 1단계 카메라 리그", priority = 90)]
    static void RevertRig()
    {
        if (Blocked()) return;
        // 리그 코드는 Cinemachine 전용 어셈블리에 있다. 메뉴 이름으로 부른다.
        if (!EditorApplication.ExecuteMenuItem("CG2 Lab/시네머신/되돌리기 — 카메라 리그 제거"))
            Debug.Log("[CG2] 카메라 리그 메뉴가 없습니다(Cinemachine 미설치). 되돌릴 리그가 없습니다.");
    }

    [MenuItem("CG2 Lab/5주차/되돌리기 — 3단계 UI 구성", priority = 91)]
    static void RevertUI()
    {
        if (Blocked()) return;
        var hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud == null || hud.gameObject.name != DYNAMIC)
        {
            Debug.Log("[CG2] 5주차 UI 구성이 적용되어 있지 않습니다. 되돌릴 것이 없습니다.");
            return;
        }

        Undo.SetCurrentGroupName("CG2 UI 되돌리기");
        int group = Undo.GetCurrentGroup();
        var canvas = hud.gameObject;

        // 3-5 비교 메뉴로 합쳐 둔 상태여도 정적 요소는 아래에서 함께 지운다.
        foreach (var name in new[] { "TopBar", "Icon_HP", "Icon_EXP", "Icon_Coin", "ShopButton" })
        {
            var t = canvas.transform.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // 게임오버 창은 Starter의 것이다. 팝업 Canvas를 지우기 전에 원래 자리로 옮긴다.
        var so = new SerializedObject(hud);
        var gameOver = (GameObject)so.FindProperty("gameOverPanel").objectReferenceValue;
        if (gameOver != null) Undo.SetTransformParent(gameOver.transform, canvas.transform, "GameOver 복귀");

        foreach (var name in new[] { STATIC, POPUP })
        {
            var go = FindRoot(name);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }

        // ── Starter v8 StarterSceneBuilder.BuildUI와 같은 값으로 되돌린다 ──
        Undo.RecordObject(canvas, "rename");
        canvas.name = "Canvas";
        var c = canvas.GetComponent<Canvas>();
        Undo.RecordObject(c, "canvas");
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 0;
        c.sortingLayerName = "Default";
        var scaler = canvas.GetComponent<CanvasScaler>();
        Undo.RecordObject(scaler, "scaler");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 0f;

        var hp = (Slider)so.FindProperty("hpBar").objectReferenceValue;
        var exp = (Slider)so.FindProperty("expBar").objectReferenceValue;
        Starter(hp, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(260, 20));
        Starter(exp, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -36), new Vector2(200, 14));
        if (exp) { Undo.RecordObject(exp, "exp"); exp.value = 1f; }

        var center = new Vector2(0.5f, 0.5f);
        StarterText((TextMeshProUGUI)so.FindProperty("scoreText").objectReferenceValue, center, new Vector2(-10, -10), TextAlignmentOptions.TopRight);
        StarterText((TextMeshProUGUI)so.FindProperty("levelText").objectReferenceValue, center, new Vector2(-10, -40), TextAlignmentOptions.TopRight);
        StarterText((TextMeshProUGUI)so.FindProperty("timeText").objectReferenceValue, center, new Vector2(0, -10), TextAlignmentOptions.Top);

        foreach (var g in canvas.GetComponentsInChildren<Graphic>(true))
        {
            Undo.RecordObject(g, "raycast");
            g.raycastTarget = true;   // Starter는 모든 Graphic이 기본값(켬)이었다
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Debug.Log("[검증] 3단계 UI 되돌리기 완료 — Starter v8 HUD (Canvas 1개)\n" +
                  "        Scaler 800×600 · 텍스트 앵커 중앙 · EXP바 1 · Raycast Target 전부 켬 (Starter 원래 값)\n" +
                  "        아이콘 세트(CG2IconSet)와 아이콘 파일은 남겨 두었습니다. '3. UI 구성'을 다시 실행할 수 있습니다.");
    }

    [MenuItem("CG2 Lab/5주차/되돌리기 — 4단계 에셋 교체", priority = 92)]
    static void RevertArt()
    {
        if (Blocked()) return;
        var lines = new System.Collections.Generic.List<string>();

        var player = GameObject.FindGameObjectWithTag("Player");
        var ps = AssetDatabase.LoadAssetAtPath<Sprite>($"{ART}/spr_player.png");
        if (player != null && ps != null && player.TryGetComponent<SpriteRenderer>(out var sr))
        {
            Undo.RecordObject(sr, "Player sprite");
            sr.sprite = ps;
            lines.Add("Player → spr_player");
        }
        lines.Add(SetPrefab("Assets/StarterProject/Prefabs/Enemy.prefab", "Enemy", $"{ART}/spr_enemy.png"));
        lines.Add(SetPrefab("Assets/StarterProject/Prefabs/Projectile.prefab", "Projectile", $"{ART}/spr_projectile.png"));

        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        int icons = 0;
        if (set != null)
        {
            Undo.RecordObject(set, "Icon set");
            foreach (var key in CG2IconSet.Keys)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ICON_DIR}/icon_{key}.png");
                if (s != null) { set.Set(key, s); icons++; }
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            foreach (var ic in Object.FindObjectsByType<CG2UIIcon>(FindObjectsInactive.Include)) ic.Refresh();
        }
        lines.Add($"UI 아이콘 {icons}종 → 플레이스홀더");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[검증] 4단계 에셋 교체 되돌리기 완료\n        " + string.Join("\n        ", lines) +
                  "\n        가공한 파일(Assets/CG2Week5/MyArt)은 그대로 있습니다. 아트 스튜디오에서 다시 교체할 수 있습니다.");
    }

    [MenuItem("CG2 Lab/5주차/되돌리기 — 5단계 Sprite Atlas", priority = 93)]
    static void RevertAtlas()
    {
        if (Blocked()) return;
        var removed = new System.Collections.Generic.List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { ATLAS_DIR }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.DeleteAsset(p)) removed.Add(System.IO.Path.GetFileName(p));
        }
        Debug.Log(removed.Count == 0
            ? "[CG2] 삭제할 5주차 아틀라스가 없습니다."
            : $"[검증] 5단계 Sprite Atlas 되돌리기 완료 — {string.Join(", ", removed)} 삭제\n" +
              "        스프라이트가 원본 텍스처로 그려집니다. '5. Sprite Atlas 재적용'으로 다시 만들 수 있습니다.");
    }

    // ────────────────────────────────────────────────────────── 헬퍼

    static string SetPrefab(string path, string label, string spritePath)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (s == null) return $"{label} : ⚠ 플레이스홀더 없음 ({spritePath})";
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return $"{label} : ⚠ 프리팹 없음";
        root.GetComponent<SpriteRenderer>().sprite = s;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return $"{label} → {s.name}";
    }

    static void Starter(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (c == null) return;
        var rt = (RectTransform)c.transform;
        Undo.RecordObject(rt, "layout");
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void StarterText(TextMeshProUGUI t, Vector2 anchor, Vector2 pos, TextAlignmentOptions align)
    {
        if (t == null) return;
        Starter(t, anchor, anchor, pos, new Vector2(200, 30));
        Undo.RecordObject(t, "text");
        t.fontSize = 18; t.alignment = align;
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
