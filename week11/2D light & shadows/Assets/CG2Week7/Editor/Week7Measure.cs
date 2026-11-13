using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// 7주차 측정 — 모두 Play 중에 실행한다.
///   편집 모드에서 스크립트로 조명을 멀리 옮기면 조명의 화면 범위(컬링 경계)가 늦게 갱신되어 빛이 아예 안 그려질 수 있다(테스트에서 확인).
///   Play 중에는 매 프레임 갱신되므로, 조명을 옮기고 몇 프레임 기다린 뒤 잰다.
///
///   1-4 측정 #1 — 실험 씬: 공 · 기사 A(없음) · B(있음) · C(sRGB ON) · 벽돌을 빛 ← / → 에서 잰다
///   5-1 측정 #3 — 우리 게임: ① 입체감 (Normal Maps Disabled vs Accurate) ② 비용 (조명 없음 · Disabled · Fast · Accurate) ③ 메모리
///   5-2 캡처    — README용 전·후 그림
/// 측정 방법: 대상만 크게 찍는 임시 카메라 + 거리와 상관없이 세기가 같은 '측정 조명'(반경 안쪽 = 바깥쪽)
///            → 밝기 차이가 거리(감쇠)가 아니라 오직 표면 방향(노멀)에서만 나오게 한다.
/// </summary>
public static class Week7Measure
{
    // ────────────────────────────────────────────────────────── 프레임 단위 실행기

    class Step { public Action act; public int wait; }
    static readonly Queue<Step> _steps = new();
    static int _until;
    static Action _onAbort;

    static void Run(Action onAbort)
    {
        _onAbort = onAbort;
        _until = Time.frameCount + 2;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Then(Action a, int wait = 4) => _steps.Enqueue(new Step { act = a, wait = wait });

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop(); _onAbort?.Invoke(); Debug.LogWarning("[측정] Play가 끝나 중단했습니다."); return; }
        if (Time.frameCount < _until) return;
        if (_steps.Count == 0) { Stop(); return; }
        var s = _steps.Dequeue();
        try { s.act(); }
        catch (Exception e) { Stop(); _onAbort?.Invoke(); Debug.LogException(e); return; }
        _until = Time.frameCount + s.wait;
    }

    static void Stop() { EditorApplication.update -= Tick; _steps.Clear(); }
    static bool Running => _steps.Count > 0;

    static bool NeedPlay(string title)
    {
        if (EditorApplication.isPlaying && !Running) return false;
        EditorUtility.DisplayDialog(title, Running ? "측정이 이미 진행 중입니다." : "Play 중에 실행하세요. (Game 뷰 1920×1080)", "확인");
        return true;
    }

    // ────────────────────────────────────────────────────────── 공통 도구

    static Camera ZoomCamera(Bounds b)
    {
        var main = Camera.main;
        var go = new GameObject("CG2 Measure Camera") { hideFlags = HideFlags.DontSave };
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        go.AddComponent<UniversalAdditionalCameraData>();
        cam.enabled = false;
        cam.orthographicSize = Mathf.Max(b.size.x, b.size.y) * 0.75f;
        cam.transform.position = new Vector3(b.center.x, b.center.y, main.transform.position.z);
        return cam;
    }

    static Light2D MeasureLight(float distance)
    {
        var l = new GameObject("CG2 Measure Light") { hideFlags = HideFlags.DontSave }.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        Week7Common.ApplyToAllLayers(l);
        l.pointLightInnerRadius = 9.9f;     // 안쪽 반경 = 바깥 반경 → 거리 감쇠 없음
        l.pointLightOuterRadius = 10f;
        l.intensity = 1f;
        Week7Common.SetNormalMap(l, Light2D.NormalMapQuality.Accurate, distance);
        return l;
    }

    /// <summary>스프라이트 원본 PNG의 알파 — 화면 픽셀이 그림 안인지 판단 (flipX면 좌우 반전).</summary>
    class Mask
    {
        Texture2D _t; bool _flip;
        public Mask(SpriteRenderer sr) { _t = NormalMapGen.LoadPng(AssetDatabase.GetAssetPath(sr.sprite)); _flip = sr.flipX; }
        public bool Inside(float u, float v)
        {
            int x = Mathf.Clamp((int)(u * _t.width), 0, _t.width - 1), y = Mathf.Clamp((int)(v * _t.height), 0, _t.height - 1);
            if (_flip) x = _t.width - 1 - x;
            return _t.GetPixel(x, y).a >= 0.5f;
        }
        public void Free() => Object.DestroyImmediate(_t);
    }

    /// <summary>그림 안 픽셀만 골라 왼쪽 절반 · 오른쪽 절반의 평균 밝기(흑백 0~255).</summary>
    static Vector2 Halves(Camera cam, SpriteRenderer sr, Mask m)
    {
        const int W = 640, H = 640;
        var shot = Week7Common.Capture(cam, W, H);
        var b = sr.bounds;
        var p0 = Week7Common.WorldToPixel(cam, b.min, W, H);
        var p1 = Week7Common.WorldToPixel(cam, b.max, W, H);
        double l = 0, r = 0; int cl = 0, cr = 0;
        for (int y = Mathf.Max(0, p0.y); y < Mathf.Min(H, p1.y); y++)
            for (int x = Mathf.Max(0, p0.x); x < Mathf.Min(W, p1.x); x++)
            {
                if (!m.Inside((x - p0.x + 0.5f) / (p1.x - p0.x), (y - p0.y + 0.5f) / (p1.y - p0.y))) continue;
                float g = Week7Common.Gray(shot.GetPixel(x, y));
                if (x < (p0.x + p1.x) / 2) { l += g; cl++; } else { r += g; cr++; }
            }
        Object.DestroyImmediate(shot);
        return new Vector2(cl > 0 ? (float)(l / cl) : 0, cr > 0 ? (float)(r / cr) : 0);
    }

    /// <summary>입체감 점수 = 빛이 ← 에서 → 로 갈 때 '왼쪽 − 오른쪽' 밝기 차가 얼마나 뒤집히는가 (÷2).</summary>
    public static float Score(Vector2 left, Vector2 right) => ((left.x - left.y) - (right.x - right.y)) / 2f;

    static string Cell(Vector2 v) => $"{v.x:0} / {v.y:0}";

    /// <summary>대상 하나를 빛 ← · → 에서 잰다. 결과는 done(왼쪽 빛, 오른쪽 빛)으로.</summary>
    static void SweepLR(SpriteRenderer sr, Light2D light, float offset, Action<Vector2, Vector2> done)
    {
        Camera cam = null; Mask m = null; Vector2 L = default;
        Then(() =>
        {
            cam = ZoomCamera(sr.bounds); m = new Mask(sr);
            light.transform.position = (Vector3)((Vector2)sr.bounds.center + Vector2.left * offset);
        });
        Then(() =>
        {
            L = Halves(cam, sr, m);
            light.transform.position = (Vector3)((Vector2)sr.bounds.center + Vector2.right * offset);
        });
        Then(() =>
        {
            var R = Halves(cam, sr, m);
            Object.DestroyImmediate(cam.gameObject); m.Free();
            done(L, R);
        }, 1);
    }

    // ────────────────────────────────────────────────────────── 1-4 측정 #1 (실험 씬)

    [MenuItem(Week7Common.MENU + "1-4. 측정 #1 — 실험 씬: 빛 ← → 에서 밝기 재기 (Play 중)", priority = 14)]
    public static void MeasureLab()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != Week7Lab.SCENE)
        { EditorUtility.DisplayDialog("측정 #1", "1-1. 실험 씬을 연 뒤 Play 하고 실행하세요.", "확인"); return; }
        if (NeedPlay("측정 #1")) return;

        var test = Week7Common.FindInScene(Week7Lab.LIGHT)?.GetComponent<Light2D>();
        bool testWas = test != null && test.enabled;
        if (test != null) test.enabled = false;
        var light = MeasureLight(2.5f);   // 대상 크기 2.5유닛 — 조명 높이도 같은 비율로
        var rows = new List<string>();
        void Cleanup() { if (light) Object.Destroy(light.gameObject); if (test) test.enabled = testWas; }

        foreach (var it in Week7Lab.Items)
        {
            var sr = GameObject.Find(it.name)?.GetComponent<SpriteRenderer>();
            if (sr == null || it.name.Contains("벽돌")) continue;   // 벽돌은 좌우가 같은 반복 무늬 → 절반 비교가 의미 없다(눈으로 본다)
            string name = it.name;
            SweepLR(sr, light, sr.bounds.size.x * 1.3f, (L, R) =>
                rows.Add($"| {name} | {Cell(L)} | {Cell(R)} | **{Score(L, R):0.0}** | {(L.x + L.y) / 2:0} / {(R.x + R.y) / 2:0} |"));
        }
        // 비교 그림 — 줄 = 빛 ← · 빛 →, 칸 = 대상 (README · 슬라이드용)
        var srs = new List<SpriteRenderer>();
        foreach (var it in Week7Lab.Items)
        {
            var sr = GameObject.Find(it.name)?.GetComponent<SpriteRenderer>();
            if (sr != null) srs.Add(sr);
        }
        const int T = 320;
        var tiles = new Texture2D[srs.Count, 2];
        for (int i = 0; i < srs.Count; i++)
        {
            int k = i; Camera cam = null;
            Then(() => { cam = ZoomCamera(srs[k].bounds); light.transform.position = srs[k].bounds.center + Vector3.left * srs[k].bounds.size.x * 1.3f; });
            Then(() => { tiles[k, 0] = Week7Common.Capture(cam, T, T); light.transform.position = srs[k].bounds.center + Vector3.right * srs[k].bounds.size.x * 1.3f; });
            Then(() => { tiles[k, 1] = Week7Common.Capture(cam, T, T); Object.DestroyImmediate(cam.gameObject); }, 1);
        }
        string sheetPath = Week7Make.CAPTURES + "/measure1_lab_left_right.png";
        Then(() =>
        {
            var sheet = new Texture2D(T * srs.Count, T * 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < srs.Count; i++)
                for (int r = 0; r < 2; r++)
                {
                    sheet.SetPixels32(i * T, (1 - r) * T, T, T, tiles[i, r].GetPixels32());
                    Object.DestroyImmediate(tiles[i, r]);
                }
            sheet.Apply();
            Directory.CreateDirectory(Week7Make.CAPTURES);
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            AssetDatabase.ImportAsset(sheetPath);
        }, 1);

        Then(() =>
        {
            Cleanup();
            var sb = new StringBuilder("[측정] #1 실험 씬 — 빛을 대상의 왼쪽(←) · 오른쪽(→)에 두고 잰 밝기 (흑백 0~255)\n");
            sb.AppendLine("| 대상 | 빛 ← : 왼쪽 / 오른쪽 | 빛 → : 왼쪽 / 오른쪽 | 입체감 점수 | 전체 평균 ← / → |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in rows) sb.AppendLine(r);
            sb.Append("        입체감 점수 = (빛 ← 일 때 왼−오 − 빛 → 일 때 왼−오) ÷ 2 → 빛을 따라 밝은 면이 얼마나 옮겨 가나\n" +
                      "        ② 노멀 맵 없음도 0이 아닌 이유: 모든 픽셀이 '정면'을 보는 평평한 노멀을 받는다 → 조명에 가까운 픽셀이 조금 더 정면으로 빛을 받는다(기준값)\n" +
                      "        ⑤ 벽돌은 좌우가 같은 반복 무늬라 이 점수로 재지 않는다 → 조명을 끌며 줄눈 모서리가 빛 쪽으로 밝아지는지 눈으로 본다\n" +
                      "        전체 평균 ← / → : 같아야 정상. 한쪽만 어두우면 노멀 맵이 한쪽으로 기울어 있다는 뜻(sRGB 실수)\n" +
                      "        측정 조명: 거리 감쇠 없음(반경 안쪽 = 바깥쪽) · 세기 1 · Normal Map Distance 2.5 · 대상 폭의 1.3배 거리\n" +
                      $"        비교 그림: {sheetPath} (위 줄 = 빛 ← · 아래 줄 = 빛 →)");
            Week7Common.Save("measure1_lab", sb.ToString());
            Debug.Log(sb.ToString());
        }, 1);
        Run(Cleanup);
        Debug.Log("[측정] #1 시작 — 약 3초. 측정 중에는 화면을 건드리지 않는다.");
    }

    // ────────────────────────────────────────────────────────── 5-1 측정 #3 (우리 게임)

    static readonly (string label, Light2D.NormalMapQuality q, bool on)[] CostRows =
    {
        ("ⓐ 테스트 조명 없음", Light2D.NormalMapQuality.Disabled, false),
        ("ⓑ 조명 · Normal Maps Disabled", Light2D.NormalMapQuality.Disabled, true),
        ("ⓒ 조명 · Fast", Light2D.NormalMapQuality.Fast, true),
        ("ⓓ 조명 · Accurate", Light2D.NormalMapQuality.Accurate, true),
    };

    [MenuItem(Week7Common.MENU + "5-1. 측정 #3 — 우리 게임: 입체감 · 비용 (Play 중)", priority = 51)]
    public static void MeasureGame()
    {
        if (NeedPlay("측정 #3")) return;
        var player = GameObject.FindWithTag("Player")?.GetComponent<SpriteRenderer>();
        if (player == null) { EditorUtility.DisplayDialog("측정 #3", "게임 씬(SampleScene)에서 실행하세요.", "확인"); return; }
        SpriteRenderer enemy = null;
        float best = float.MaxValue;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
            if (sr.CompareTag("Enemy") && sr.isVisible)
            {
                float d = (sr.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; enemy = sr; }
            }

        // 준비 — 게임 정지 · 학생의 테스트 조명 상태 기억
        float ts = Time.timeScale;
        Time.timeScale = 0f;
        var test = FindTestLight();
        bool made = test == null;
        if (made)
        {
            test = new GameObject(Week7Light.TEST) { hideFlags = HideFlags.DontSave }.AddComponent<Light2D>();
            test.lightType = Light2D.LightType.Point;
            Week7Common.ApplyToAllLayers(test);
            test.pointLightOuterRadius = 4f; test.intensity = 1.5f;
        }
        var testPos = test.transform.position;
        bool testOn = test.enabled;
        var testQ = test.normalMapQuality;
        var follow = test.GetComponent<CG2MouseLight>();
        bool followWas = follow != null && follow.followMouse;
        if (follow) follow.followMouse = false;
        var light = MeasureLight(1f);
        light.enabled = false;   // 비용 측정이 끝날 때까지 꺼 둔다

        void Cleanup()
        {
            if (light) Object.Destroy(light.gameObject);
            if (made) { if (test) Object.Destroy(test.gameObject); }
            else if (test)
            {
                test.transform.position = testPos; test.enabled = testOn;
                Week7Common.SetNormalMap(test, testQ);
                if (follow) follow.followMouse = followWas;
            }
            if (EditorApplication.isPlaying) Time.timeScale = ts;
        }

        // ② 비용 먼저 — 측정용 임시 카메라 · 조명이 없는 상태의 '게임 화면' 그대로
        var cost = new string[CostRows.Length];
        int screenW = 0, screenH = 0;
        test.transform.position = player.transform.position + new Vector3(-1.3f, 0.3f, 0);
        for (int i = 0; i < CostRows.Length; i++)
        {
            int k = i;
            // 30프레임 대기 — 다 쓴 렌더 텍스처는 바로 지워지지 않고 잠시 풀(pool)에 남는다(10프레임으로는 이전 상태가 섞였다)
            Then(() => { test.enabled = CostRows[k].on; Week7Common.SetNormalMap(test, CostRows[k].q); }, 30);
            Then(() =>
            {
                screenW = Screen.width; screenH = Screen.height;
                cost[k] = $"| {CostRows[k].label} | {UnityStats.drawCalls} | {UnityStats.setPassCalls} | {UnityStats.renderTextureCount} | {UnityStats.renderTextureBytes / 1048576f:0.0} MB |";
            }, 1);
        }

        // ① 입체감 — 학생 조명은 끄고 측정 조명으로
        var feel = new List<string>();
        Then(() => { test.enabled = false; light.enabled = true; });
        foreach (var (label, sr) in new[] { ("플레이어", player), ("적 (가장 가까운)", enemy) })
        {
            if (sr == null) continue;
            foreach (var q in new[] { Light2D.NormalMapQuality.Disabled, Light2D.NormalMapQuality.Accurate })
            {
                var qq = q;
                Then(() => Week7Common.SetNormalMap(light, qq, 1f), 1);
                SweepLR(sr, light, 1.3f, (L, R) =>
                    feel.Add($"| {label} | {(qq == Light2D.NormalMapQuality.Disabled ? "Disabled (노멀 맵 무시)" : "Accurate (노멀 맵 사용)")} | {Cell(L)} | {Cell(R)} | **{Score(L, R):0.0}** |"));
            }
        }

        Then(() =>
        {
            Cleanup();
            var sb = new StringBuilder($"[측정] #3 우리 게임 — 같은 Play · 같은 프레임(게임 정지) · Game 뷰 {screenW}×{screenH}\n");
            sb.AppendLine("#3-1 입체감 — 측정 조명(거리 감쇠 없음 · 세기 1 · Distance 1)을 대상 왼쪽/오른쪽 1.3유닛에 두고 잰 밝기");
            sb.AppendLine("| 대상 | 조명 설정 | 빛 ← : 왼쪽 / 오른쪽 | 빛 → : 왼쪽 / 오른쪽 | 입체감 점수 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in feel) sb.AppendLine(r);
            sb.AppendLine("#3-2 비용 — 테스트 조명 하나 (Stats 창과 같은 값)");
            sb.AppendLine("| 조건 | DrawCalls | SetPass | 렌더 텍스처 수 | 렌더 텍스처 메모리 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var c in cost) sb.AppendLine(c);
            sb.AppendLine("#3-3 텍스처 메모리 — 노멀 맵 파일");
            sb.Append(MemoryTable());
            sb.Append("        읽는 법: ⓑ→ⓒ 노멀 맵을 켜는 순간 '노멀 버퍼'(화면 크기 텍스처 1장)와 스프라이트를 한 번 더 그리는 패스가 생긴다. ⓒ와 ⓓ는 개수가 같다(차이는 픽셀 계산 정밀도).");
            Week7Common.Save("measure3_game", sb.ToString());
            Debug.Log(sb.ToString());
        }, 1);
        Run(Cleanup);
        Debug.Log("[측정] #3 시작 — 약 3초. 게임이 잠시 멈춘다.");
    }

    static Light2D FindTestLight()
    {
        Light2D any = null;
        foreach (var l in Object.FindObjectsByType<Light2D>())
        {
            if (l.lightType == Light2D.LightType.Global || l.name == "CG2 Measure Light") continue;
            if (l.name == Week7Light.TEST) return l;
            any ??= l;
        }
        return any;
    }

    /// <summary>GPU에 올라가는 크기 = 가로 × 세로 × 픽셀당 바이트 (압축 없음 · 밉맵 없음).</summary>
    static long GpuBytes(Texture t) =>
        t == null ? 0 : (long)t.width * t.height * UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockSize(t.graphicsFormat);

    static string MemoryTable()
    {
        var sb = new StringBuilder("| 그림 | 원본 (크기 · 포맷 · 메모리) | 노멀 맵 (포맷 · 메모리) |\n|---|---|---|\n");
        long total = 0, main = 0;
        foreach (var t in Week7Make.GameTargets())
        {
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(t.png);
            var ti = AssetImporter.GetAtPath(t.png) as TextureImporter;
            Texture2D n = null;
            if (ti?.secondarySpriteTextures != null)
                foreach (var s in ti.secondarySpriteTextures) if (s.name == Week7Link.NAME) n = s.texture;
            long nb = GpuBytes(n), mb = GpuBytes(src);
            total += nb; main += mb;
            sb.AppendLine($"| {t.role} | {(src ? $"{src.width}×{src.height} · {src.format} · {mb / 1024f:0.#} KB" : "—")} | {(n ? $"{n.format} · {nb / 1024f:0.#} KB" : "없음")} |");
        }
        sb.AppendLine($"| 합계 | {main / 1024f:0.#} KB | **+{total / 1024f:0.#} KB** |");

        // Sprite Atlas — 노멀 맵이 연결된 스프라이트는 아틀라스에 노멀 맵 페이지가 한 장 더 생긴다
        var player = GameObject.FindWithTag("Player")?.GetComponent<SpriteRenderer>();
        if (player != null && player.sprite != null && player.sprite.packed)
        {
            var sec = new SecondarySpriteTexture[4];
            int c = player.sprite.GetSecondaryTextures(sec);
            sb.AppendLine($"        Sprite Atlas: 플레이어 그림은 '{player.sprite.texture.name}'({player.sprite.texture.width}×{player.sprite.texture.height})에 들어 있고, " +
                          (c > 0 ? $"노멀 맵도 같은 배치의 페이지 '{sec[0].name}'({sec[0].texture.width}×{sec[0].texture.height})로 함께 묶였다 → 배칭은 그대로 · 아틀라스 메모리 2배"
                                 : "노멀 맵 페이지가 없다 ⚠ — 아틀라스를 다시 묶는다(Sprite Atlas → Pack Preview)"));
        }
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────── 5-2 README 캡처

    [MenuItem(Week7Common.MENU + "5-2. README 캡처 — 노멀 맵 끄기 · 켜기 나란히 (Play 중)", priority = 52)]
    public static void CaptureReadme()
    {
        if (NeedPlay("README 캡처")) return;
        var player = GameObject.FindWithTag("Player")?.GetComponent<SpriteRenderer>();
        var test = FindTestLight();
        if (player == null || test == null) { EditorUtility.DisplayDialog("README 캡처", "게임 씬에서 테스트 조명(4-2)을 만든 뒤 실행하세요.", "확인"); return; }

        float ts = Time.timeScale; Time.timeScale = 0f;
        var pos = test.transform.position; var q0 = test.normalMapQuality;
        var follow = test.GetComponent<CG2MouseLight>(); bool fw = follow && follow.followMouse;
        if (follow) follow.followMouse = false;
        void Cleanup()
        {
            if (test) { test.transform.position = pos; Week7Common.SetNormalMap(test, q0); }
            if (follow) follow.followMouse = fw;
            if (EditorApplication.isPlaying) Time.timeScale = ts;
        }

        var shots = new Texture2D[4];
        var b = new Bounds(player.bounds.center, Vector3.one * 1.4f);   // 플레이어만 크게
        Camera cam = null;
        int idx = 0;
        foreach (var q in new[] { Light2D.NormalMapQuality.Disabled, Light2D.NormalMapQuality.Accurate })
            foreach (var dx in new[] { -1.3f, 1.3f })
            {
                int k = idx++; var qq = q; float x = dx;
                Then(() =>
                {
                    cam ??= ZoomCamera(b);
                    Week7Common.SetNormalMap(test, qq);
                    test.transform.position = player.bounds.center + new Vector3(x, 0.3f, 0);
                });
                Then(() => shots[k] = Week7Common.Capture(cam, 480, 480), 1);
            }
        Then(() =>
        {
            Object.DestroyImmediate(cam.gameObject);
            var sheet = new Texture2D(960, 960, TextureFormat.RGBA32, false);
            for (int i = 0; i < 4; i++) { sheet.SetPixels32((i % 2) * 480, (1 - i / 2) * 480, 480, 480, shots[i].GetPixels32()); Object.DestroyImmediate(shots[i]); }
            sheet.Apply();
            Directory.CreateDirectory(Week7Make.CAPTURES);
            string path = Week7Make.CAPTURES + "/week7_normalmap_before_after.png";
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Cleanup();
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[실습] 5-2 README 캡처 — {path}\n        위: Normal Maps Disabled (빛 ← · 빛 →) · 아래: Accurate (빛 ← · 빛 →) — 아래 줄만 밝은 면이 빛을 따라 바뀐다");
        }, 1);
        Run(Cleanup);
    }
}
