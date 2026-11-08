using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// 6주차 4단계 측정 #3 — 실제 게임에서 5주차 상태와 6주차 상태를 같은 조건으로 비교한다 (5주차 측정 #4와 같은 방식).
///
/// 같은 Play · 같은 프레임(시간 정지) · 같은 Game 뷰에서 두 상태를 번갈아 끼우고 잰다.
///   5주차 상태 = 백업해 둔 5주차 색 + 기본 영문 폰트 (CG2 Theme 꺼짐)
///   6주차 상태 = 팔레트 테마 + 갈무리11
/// 기록
///   #3-1 렌더링 비용   DrawCalls · SetPass · Tris — 전투 HUD / 상점 열림
///   #3-2 가독성        플레이어·적·투사체 vs 바닥의 명도 대비 (5주차 5-2와 같은 식, 조명·타일 틴트까지 반영)
///   #3-3 화면 값       실제 출력 화면(Overlay UI 포함)에서 바닥 · HUD 띠 · 팝업 뒤 막의 밝기
///   #3-4 색 공간       Linear와 Gamma에서 한 번씩 실행하면 같은 게임 화면의 값을 나란히 비교
/// </summary>
public static class Week6GameCompare
{
    const int SETTLE = 15;

    [MenuItem("CG2 Lab/6주차/4-3. 측정 #3 — 게임 전·후 비교 (Play 중)", priority = 43)]
    static void Start()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("게임 전·후 비교", "Play 중에 실행하세요. (Game 뷰 1920×1080 고정 · 적이 몇 마리 나온 뒤)", "확인");
            return;
        }
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        if (theme == null || theme.palette == null || !File.Exists("Assets/CG2Week6/Backup/week6_before_theme.json"))
        {
            EditorUtility.DisplayDialog("게임 전·후 비교", "'3. UI 테마 적용'과 '4. 씬 톤 적용'을 먼저 실행하세요.", "확인");
            return;
        }
        var go = new GameObject("CG2 GameCompare") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<Runner>().StartCoroutine(Run(go, theme));
    }

    class Runner : MonoBehaviour { }

    struct Sample { public int dc, sp, tris; public float screen, ground, bar, dim; }

    static IEnumerator Run(GameObject runner, CG2Theme theme)
    {
        float timeScale = Time.timeScale;
        Time.timeScale = 0f;   // 적·투사체를 멈춰 두 상태가 같은 장면을 그리게 한다
        var shop = Object.FindAnyObjectByType<ShopPanel>();
        Debug.Log("[측정 #3] 시작 — 5주차 상태 → 6주차 상태 순서로 전투 HUD · 상점 열림을 잰다. 게임이 잠시 멈춘다.");

        // 폰트를 오갈 수 있게 지금(6주차) 폰트를 기억한다
        var texts = new List<(TMP_Text t, TMP_FontAsset f, Material m)>();
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            texts.Add((t, t.font, t.fontSharedMaterial));

        var result = new Sample[STATES, 2];     // [상태, 장면 0=HUD 1=상점]
        var contrast = new List<string>[STATES + 1];   // 마지막 칸 = 6주차지만 조명을 나누기 전(전역 조명 하나)
        for (int state = 0; state < STATES; state++)
        {
            SetState(state, theme, texts);
            contrast[state] = Contrasts();
            if (state == STATES - 1) contrast[STATES] = Contrasts(singleLight: true);
            for (int scene = 0; scene < 2; scene++)
            {
                if (scene == 1 && shop != null) { shop.Toggle(); Time.timeScale = 0f; }
                for (int i = 0; i < SETTLE; i++) yield return null;
                yield return new WaitForEndOfFrame();
                result[state, scene] = Measure();
                if (scene == 1 && shop != null) { shop.Toggle(); Time.timeScale = 0f; }
            }
        }
        // 마지막 상태(6주차)를 그대로 두고 시간만 되돌린다
        Time.timeScale = timeScale;
        Object.Destroy(runner);
        Report(result, contrast);
    }

    const int STATES = 3;
    static readonly string[] StateNames = { "5주차", "6주차 색 + 영문 폰트", "6주차" };

    /// <summary>
    /// 0 = 5주차 (5주차 색 · 영문 폰트 · 영문 문구)
    /// 1 = 6주차 색 + 영문 폰트 + 한글 문구 — 한글 글리프는 기본 폰트의 Fallback(갈무리)에서 온다
    /// 2 = 6주차 (테마 · 갈무리11 하나로 통일)
    /// </summary>
    static void SetState(int state, CG2Theme theme, List<(TMP_Text t, TMP_FontAsset f, Material m)> texts)
    {
        var def = TMP_Settings.defaultFontAsset;
        if (state == 0)
        {
            theme.enabled = false;
            Week6Theme.Restore();
            Week6Font.SetLabels(korean: false);
        }
        else
        {
            theme.enabled = true;   // OnEnable에서 다시 칠한다
            theme.Apply();
            Week6Font.SetLabels(korean: true);
        }
        foreach (var (t, f, m) in texts)
        {
            bool mine = state == 2;
            t.font = mine ? f : def;
            t.fontSharedMaterial = mine ? m : def.material;
        }
    }

    static Sample Measure()
    {
        var s = new Sample { dc = UnityStats.drawCalls, sp = UnityStats.setPassCalls, tris = UnityStats.triangles };
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        int w = shot.width, h = shot.height;
        float L(float x, float y) => CG2Palette.Luma(shot.GetPixel(Mathf.RoundToInt(x * (w - 1)), Mathf.RoundToInt((1 - y) * (h - 1))));
        float Median(params float[] v) { System.Array.Sort(v); return v[v.Length / 2]; }

        // 화면 평균 (16×9 격자)
        float sum = 0; int n = 0;
        for (int gy = 0; gy < 9; gy++) for (int gx = 0; gx < 16; gx++) { sum += L((gx + 0.5f) / 16, (gy + 0.5f) / 9); n++; }
        s.screen = sum / n;
        // 바닥 — 플레이어(가운데)를 피한 9곳의 중앙값 (적·벽이 몇 곳 걸려도 중앙값은 바닥이다)
        s.ground = Median(L(.15f, .3f), L(.3f, .7f), L(.45f, .85f), L(.6f, .3f), L(.75f, .65f), L(.9f, .5f), L(.2f, .55f), L(.65f, .85f), L(.85f, .25f));
        // HUD 띠 — 상단 4% 높이, 글자·바를 피한 구간
        s.bar = Median(L(.30f, .02f), L(.35f, .02f), L(.62f, .02f), L(.66f, .02f), L(.70f, .02f));
        // 팝업 뒤 막 — 창 바깥 왼쪽
        s.dim = Median(L(.08f, .45f), L(.1f, .6f), L(.12f, .75f));
        Object.Destroy(shot);
        return s;
    }

    /// <summary>
    /// 5주차 5-2와 같은 식 — 스프라이트 평균색 × 전역 조명 색 vs 바닥 타일 평균색 × 타일맵 색 × 전역 조명 색.
    /// (Sprite-Lit은 조명 색을 곱해 그린다. 강도는 1로 같아서 비율에서 지워진다.)
    /// </summary>
    static List<string> Contrasts() => Contrasts(false);

    /// <param name="singleLight">true면 캐릭터도 바닥과 같은 조명을 받는다고 계산한다(4단계 조명 분리 전).</param>
    static List<string> Contrasts(bool singleLight)
    {
        Color tint = Color.white;
        int groundLayer = 0;
        var g = GameObject.Find("Tilemap_Ground");
        if (g != null && g.TryGetComponent<Tilemap>(out var tm))
        {
            tint = tm.color;
            groundLayer = g.GetComponent<TilemapRenderer>().sortingLayerID;
        }
        // 레이어마다 비추는 전역 조명이 다를 수 있다(4단계 조명 분리) — 각자 자기 레이어의 조명 색을 곱한다
        Color ground = Avg("Assets/CG2Week4/Art/tile_ground_32.png") * tint * LightColor(groundLayer);
        var rows = new List<string>();
        void Row(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) { rows.Add("—"); return; }
            Color c = Avg(AssetDatabase.GetAssetPath(sr.sprite)) * LightColor(singleLight ? groundLayer : sr.sortingLayerID);
            float r = CG2Palette.Contrast(c, ground);
            rows.Add($"{r:F2}{(r < 3f ? " ⚠" : "")}");
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        Row(player != null ? player.GetComponent<SpriteRenderer>() : null);
        Row(PrefabRenderer("Assets/StarterProject/Prefabs/Enemy.prefab"));
        Row(PrefabRenderer("Assets/StarterProject/Prefabs/Projectile.prefab"));
        rows.Add(CG2Palette.Hex(ground));
        return rows;
    }

    static Color LightColor(int sortingLayerId)
    {
        var l = Week6Common.GlobalLightFor(sortingLayerId);
        if (l == null) return Color.black;   // 이 레이어를 비추는 전역 조명이 없으면 Sprite-Lit은 검게 나온다
        var c = l.color * l.intensity;
        c.a = 1;
        return c;
    }

    static SpriteRenderer PrefabRenderer(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return go != null && go.TryGetComponent<SpriteRenderer>(out var sr) ? sr : null;
    }

    static Color Avg(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Color.gray;
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(path));
        Color sum = Color.clear; int n = 0;
        foreach (var p in t.GetPixels()) if (p.a > 0.5f) { sum += p; n++; }
        Object.DestroyImmediate(t);
        var c = n > 0 ? sum / n : Color.gray;
        c.a = 1;
        return c;
    }

    static void Report(Sample[,] r, List<string>[] c)
    {
        string space = PlayerSettings.colorSpace.ToString();
        string P(float v) => $"{v * 100:0}%";
        var sb = new StringBuilder();
        sb.AppendLine($"[측정] #3 게임 전·후 비교 — Game 뷰 {Screen.width}×{Screen.height} · Color Space {space} · 시간 정지 · 같은 프레임");
        const int W6 = STATES - 1;
        sb.AppendLine("#3-1 렌더링 비용 — DrawCalls / SetPass / Tris");
        sb.AppendLine($"| 장면 | {StateNames[0]} | {StateNames[1]} | {StateNames[2]} |");
        sb.AppendLine("|---|---|---|---|");
        string[] scenes = { "전투 HUD", "상점 열림" };
        for (int s = 0; s < 2; s++)
        {
            sb.Append($"| {scenes[s]} |");
            for (int st = 0; st < STATES; st++) sb.Append($" {r[st, s].dc} / {r[st, s].sp} / {r[st, s].tris:N0} |");
            sb.AppendLine();
        }
        sb.AppendLine("#3-2 가독성 — 바닥과의 명도 대비 (3:1 이상 권장)");
        sb.AppendLine("| 대상 | 5주차 | 6주차 · 전역 조명 하나 | 6주차 · 조명 분리 |");
        sb.AppendLine("|---|---|---|---|");
        string[] who = { "Player", "Enemy", "Projectile", "바닥 실제 색(타일×틴트×조명)" };
        for (int i = 0; i < who.Length; i++) sb.AppendLine($"| {who[i]} | {c[0][i]} | {c[STATES][i]} | {c[W6][i]} |");
        sb.AppendLine("#3-3 화면 값 — 출력 화면의 흑백 밝기");
        sb.AppendLine("| 지점 | 5주차 | 6주차 |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| 화면 평균 | {P(r[0, 0].screen)} | {P(r[W6, 0].screen)} |");
        sb.AppendLine($"| 바닥 | {P(r[0, 0].ground)} | {P(r[W6, 0].ground)} |");
        sb.AppendLine($"| HUD 띠 (반투명) | {P(r[0, 0].bar)} | {P(r[W6, 0].bar)} |");
        sb.AppendLine($"| 상점 뒤 막 (반투명) | {P(r[0, 1].dim)} | {P(r[W6, 1].dim)} |");
        string table = sb.ToString();

        // 이번 색 공간의 화면 값을 남기고, 다른 색 공간 기록이 있으면 나란히 비교한다
        Week6Common.Save("game_" + space, $"{r[W6, 0].screen}|{r[W6, 0].ground}|{r[W6, 0].bar}|{r[W6, 1].dim}|{r[0, 0].ground}|{r[0, 0].bar}|{r[0, 1].dim}");
        Week6Common.Save("game_compare", table);
        string lin = Week6Common.Load("game_Linear"), gam = Week6Common.Load("game_Gamma");
        string cs;
        if (lin != "" && gam != "")
        {
            var a = lin.Split('|'); var b = gam.Split('|');
            string F(string v) => $"{float.Parse(v) * 100:0}%";
            cs = "#3-4 색 공간 — 같은 게임 화면 (6주차 상태 / 바닥·HUD 띠는 5주차 상태도)\n" +
                 "| 지점 | Linear | Gamma |\n|---|---|---|\n" +
                 $"| 화면 평균 | {F(a[0])} | {F(b[0])} |\n| 바닥 | {F(a[1])} | {F(b[1])} |\n" +
                 $"| HUD 띠 (반투명) | {F(a[2])} | {F(b[2])} |\n| 상점 뒤 막 (반투명) | {F(a[3])} | {F(b[3])} |\n" +
                 $"| 5주차 바닥 | {F(a[4])} | {F(b[4])} |\n| 5주차 HUD 띠 (검정 α0.35) | {F(a[5])} | {F(b[5])} |" +
                 (a.Length > 6 && b.Length > 6 ? $"\n| 5주차 상점 뒤 막 (검정 α0.6) | {F(a[6])} | {F(b[6])} |" : "");
            Week6Common.Save("game_colorspace", cs);
        }
        else cs = $"#3-4 색 공간 — {space}만 기록됨. Stop → 1-2. 색 공간 전환 → Play → 4-3을 한 번 더 실행하면 비교표가 나온다 (끝나면 Linear 복귀).";

        Debug.Log(table + cs + "\n        읽는 법: #3-1 5주차 ↔ 6주차가 거의 같으면 '색을 바꾸는 것은 공짜' · 가운데 열이 늘면 그것이 폰트를 섞은(Fallback) 비용\n" +
                  "                 #3-2 전역 조명 하나로 톤을 입히면 캐릭터까지 물든다 → 조명 분리로 3:1 회복 · #3-4는 반투명(블렌딩)이 있는 곳만 달라진다.");
    }
}
