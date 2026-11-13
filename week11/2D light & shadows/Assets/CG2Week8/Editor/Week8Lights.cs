using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using C = Week8Common;

/// <summary>
/// 8주차 2단계와 4단계의 조명.
///   1-3 환경광 검사 · 2-1 횃불 검사 · 2-2 가로등 · 2-3 투사체 불빛 · 4-1 플레이어 랜턴
/// 값을 정한 이유는 8주차 수업 설계의 변경 이력에 적었다.
/// </summary>
public static class Week8Lights
{
    public const float GROUND_AMBIENT = 0.06f, ENTITY_AMBIENT = 0.15f;
    public static readonly Color AMBIENT_COLOR = new Color(0.45f, 0.55f, 1f);
    public static readonly Color TORCH_COLOR = new Color(1f, 0.67f, 0.35f);
    public static readonly Color LAMP_COLOR = new Color(0.65f, 0.82f, 1f);
    public static readonly Vector3[] LAMP_POS = { new Vector3(-9f, 4.5f, 0), new Vector3(9f, -4.5f, 0) };

    static string Row(string item, string value, bool ok, string fix) => $"| {item} | {value} | {(ok ? "✓" : "⚠ " + fix)} |\n";

    [MenuItem(C.MENU + "1-3. 환경광 검사 — 어두운 필드가 되었나", priority = 13)]
    public static void CheckAmbient()
    {
        if (C.Busy("환경광 검사")) return;
        var g = C.Named(C.GROUND_LIGHT); var e = C.Named(C.ENTITY_LIGHT);
        if (g == null || e == null) { Debug.LogWarning("[검증] 1-3 전역 조명을 찾지 못했다. 게임 씬에서 실행한다."); return; }
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        bool blue = g.color.b > g.color.r + 0.2f;
        var sb = new StringBuilder("[검증] 1-3 환경광\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        sb.Append(Row("테마 Apply Scene Tone", theme != null && theme.applySceneTone ? "켜짐" : "꺼짐", theme == null || !theme.applySceneTone, "켜져 있으면 Play 때 색이 덮어써진다 → 1-2"));
        sb.Append(Row("바닥 조명 Intensity", $"{g.intensity:0.00}", g.intensity <= 0.1f, "0.06으로 낮춘다"));
        sb.Append(Row("바닥 조명 Color", $"R {g.color.r:0.00} · G {g.color.g:0.00} · B {g.color.b:0.00}", blue, "푸른 쪽으로 (0.45, 0.55, 1.0)"));
        sb.Append(Row("캐릭터 조명 Intensity", $"{e.intensity:0.00}", e.intensity >= 0.1f && e.intensity <= 0.25f, "0.15 — 어둠 속에서도 적이 보여야 한다"));
        sb.Append("        ── 다음 ── Play → 1-4");
        Debug.Log(sb.ToString());
    }

    [MenuItem(C.MENU + "2-1. 횃불 검사 — 깜빡임 붙이기", priority = 21)]
    public static void CheckTorch()
    {
        if (C.Busy("횃불 검사")) return;
        var t = C.FindTorch();
        if (t == null)
        {
            t = new GameObject(C.TORCH).AddComponent<Light2D>();
            t.lightType = Light2D.LightType.Point;
            C.ApplyToAllLayers(t);
            t.transform.position = new Vector3(-1.5f, 0.5f, 0);
            Undo.RegisterCreatedObjectUndo(t.gameObject, "횃불");
        }
        var mouse = t.GetComponent("CG2MouseLight");
        if (mouse != null) Undo.DestroyObjectImmediate(mouse);   // 횃불은 제자리에 있다. 움직이는 빛은 4단계에서 만든다
        if (t.GetComponent<CG2Torch>() == null) Undo.AddComponent<CG2Torch>(t.gameObject);
        if (Object.FindAnyObjectByType<CG2LightKeys>() == null)
        {
            var k = new GameObject(C.KEYS).AddComponent<CG2LightKeys>();
            Undo.RegisterCreatedObjectUndo(k.gameObject, "비교 키");
        }
        AddGlow(t.transform, new Color(1f, 0.8f, 0.5f), 0.6f);
        Selection.activeGameObject = t.gameObject;

        bool warm = t.color.r > t.color.b + 0.3f;
        var sb = new StringBuilder($"[검증] 2-1 횃불 '{t.name}'\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        sb.Append(Row("이름", t.name, t.name == C.TORCH, "Torch로 바꾼다"));
        sb.Append(Row("Color", $"R {t.color.r:0.00} · G {t.color.g:0.00} · B {t.color.b:0.00}", warm, "주황으로 (1.0, 0.67, 0.35) — 푸른 어둠과 대비된다"));
        sb.Append(Row("Intensity", $"{t.intensity:0.0}", t.intensity >= 2f, "2.5로 올린다"));
        sb.Append(Row("Radius Outer", $"{t.pointLightOuterRadius:0.0}", t.pointLightOuterRadius >= 7f, "9로 넓힌다 (화면 절반쯤)"));
        sb.Append(Row("Falloff Strength", $"{t.falloffIntensity:0.00}", t.falloffIntensity <= 0.25f, "0.15로 낮춘다 — 값이 크면 빛이 금방 약해져 그림자가 묻힌다"));
        sb.Append(Row("위치", $"({t.transform.position.x:0.0}, {t.transform.position.y:0.0})", Mathf.Abs(t.transform.position.x + 5f) < 1.5f, "(-5, 0) — 플레이어 왼쪽. 랜턴과 겹치면 그림자가 서로 지워진다"));
        sb.Append(Row("Normal Maps", $"{t.normalMapQuality} · Distance {t.normalMapDistance:0.0}", t.normalMapQuality != Light2D.NormalMapQuality.Disabled, "Accurate"));
        sb.Append("        깜빡임(CG2Torch)과 비교 키(L · K)를 붙였다. 마우스 따라가기는 뗐다\n        ── 다음 ── 2-2");
        Debug.Log(sb.ToString());
    }

    [MenuItem(C.MENU + "2-2. 가로등 세우기 — Spot 두 개", priority = 22)]
    public static void MakeLamps()
    {
        if (C.Busy("가로등")) return;
        var old = GameObject.Find(C.LAMPS);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(C.LAMPS);
        Undo.RegisterCreatedObjectUndo(root, "가로등");
        var sb = new StringBuilder("[실습] 2-2 가로등 — 부채꼴 빛 두 개\n| 이름 | 위치 | 각도 Inner / Outer | Radius | Intensity |\n|---|---|---|---|---|\n");
        for (int i = 0; i < LAMP_POS.Length; i++)
        {
            var go = new GameObject($"Street Lamp {i + 1}");
            go.transform.SetParent(root.transform);
            go.transform.position = LAMP_POS[i];
            Vector2 dir = -((Vector2)LAMP_POS[i]).normalized;              // 화면 가운데를 향한다
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            C.ApplyToAllLayers(l);
            l.color = LAMP_COLOR;
            l.intensity = 1.8f;
            l.falloffIntensity = 0.2f;
            l.pointLightInnerRadius = 0f;
            l.pointLightOuterRadius = 9f;
            l.pointLightInnerAngle = 20f;
            l.pointLightOuterAngle = 100f;
            C.SetLightSerialized(l, Light2D.NormalMapQuality.Accurate, 1f, false, 0.75f, 0.3f);
            AddGlow(go.transform, LAMP_COLOR, 0.5f);
            sb.AppendLine($"| {go.name} | ({LAMP_POS[i].x:0.0}, {LAMP_POS[i].y:0.0}) | {l.pointLightInnerAngle:0}° / {l.pointLightOuterAngle:0}° | {l.pointLightOuterRadius:0} | {l.intensity:0.0} |");
        }
        sb.Append("        ── 다음 ── 2-3");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log(sb.ToString());
    }

    const string GLOW_PNG = C.ROOT + "/Art/light_source.png";
    const string UNLIT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    /// <summary>빛이 어디서 나오는지 보이게 하는 작은 불씨 그림. 조명을 받지 않고 스스로 빛나는 재질(Unlit)을 쓴다.</summary>
    public static void AddGlow(Transform parent, Color color, float size)
    {
        if (!System.IO.File.Exists(GLOW_PNG))
        {
            const int S = 32;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    t.SetPixel(x, y, new Color(1, 1, 1, d < 0.55f ? 1f : Mathf.Clamp01((1f - d) / 0.45f) * 0.6f));
                }
            t.Apply();
            System.IO.Directory.CreateDirectory(C.ROOT + "/Art");
            System.IO.File.WriteAllBytes(GLOW_PNG, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(GLOW_PNG);
            var ti = (TextureImporter)AssetImporter.GetAtPath(GLOW_PNG);
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = 32;
            ti.filterMode = FilterMode.Point;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        var old = parent.Find("Light Source");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Light Source");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_PNG);
        sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UNLIT);
        sr.color = color;
        sr.sortingLayerName = "FX";
    }

    const string PROJECTILE = "Assets/StarterProject/Prefabs/Projectile.prefab";
    const string GLOW = "Projectile Glow";

    [MenuItem(C.MENU + "2-3. 투사체 불빛 달기 — 프리팹에 작은 빛", priority = 23)]
    public static void ProjectileGlow()
    {
        if (C.Busy("투사체 불빛")) return;
        var root = PrefabUtility.LoadPrefabContents(PROJECTILE);
        var t = root.transform.Find(GLOW);
        string result = "이미 있음";
        if (t == null)
        {
            var go = new GameObject(GLOW);
            go.transform.SetParent(root.transform, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            C.ApplyToAllLayers(l);
            l.color = new Color(0.45f, 0.9f, 1f);
            l.intensity = 1.5f;
            l.pointLightOuterRadius = 2.5f;
            C.SetLightSerialized(l, Light2D.NormalMapQuality.Disabled, 1f, false, 0.75f, 0.3f);
            PrefabUtility.SaveAsPrefabAsset(root, PROJECTILE);
            result = "만듦";
        }
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[실습] 2-3 투사체 불빛 — {PROJECTILE} 안 '{GLOW}' {result}\n" +
                  "        Radius 2.5 · Intensity 1.5 · Normal Maps Disabled · Shadows 끔 — 많이 생기는 빛이라 가장 싼 설정으로 둔다\n" +
                  "        ── 다음 ── Play → 2-4");
    }

    [MenuItem(C.MENU + "4-1. 플레이어 랜턴 달기 — 함께 움직이는 빛", priority = 41)]
    public static void Lantern()
    {
        if (C.Busy("랜턴")) return;
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogWarning("[실습] 4-1 플레이어를 찾지 못했다. 게임 씬에서 실행한다."); return; }
        var t = player.transform.Find(C.LANTERN);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        var go = new GameObject(C.LANTERN);
        Undo.RegisterCreatedObjectUndo(go, "랜턴");
        go.transform.SetParent(player.transform, false);
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        C.ApplyToAllLayers(l);
        l.color = TORCH_COLOR;
        l.intensity = 1.8f;
        l.falloffIntensity = 0.15f;
        l.pointLightInnerRadius = 0.5f;
        l.pointLightOuterRadius = 7f;
        C.SetLightSerialized(l, Light2D.NormalMapQuality.Accurate, 1f, true, 1f, 0.3f);
        go.AddComponent<CG2Torch>().flicker = 0.08f;
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[실습] 4-1 플레이어 랜턴 — Radius 7 · Intensity 1.8 · Falloff Strength 0.15 · Shadows 켬 · Strength 1 · Softness 0.3\n" +
                  "        Play 중 WASD로 움직이면 기둥 그림자가 빛 반대쪽으로 돌아간다. K 키 = 그림자 끄기/켜기\n" +
                  "        ── 다음 ── Play → 4-2");
    }

    [MenuItem(C.MENU + "되돌리기 — 2·4단계 조명 (가로등 · 랜턴 · 투사체 불빛 삭제)", priority = 92)]
    public static void Revert()
    {
        if (C.Busy("되돌리기")) return;
        var lamps = GameObject.Find(C.LAMPS);
        if (lamps != null) Undo.DestroyObjectImmediate(lamps);
        var lantern = C.Named(C.LANTERN);
        if (lantern != null) Undo.DestroyObjectImmediate(lantern.gameObject);
        var keys = Object.FindAnyObjectByType<CG2LightKeys>();
        if (keys != null) Undo.DestroyObjectImmediate(keys.gameObject);
        var root = PrefabUtility.LoadPrefabContents(PROJECTILE);
        var t = root.transform.Find(GLOW);
        if (t != null) { Object.DestroyImmediate(t.gameObject); PrefabUtility.SaveAsPrefabAsset(root, PROJECTILE); }
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[되돌리기] 가로등 · 플레이어 랜턴 · 비교 키 · 투사체 불빛 삭제");
    }
}
