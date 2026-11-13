using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using C = Week8Common;
using Object = UnityEngine.Object;

/// <summary>
/// 8주차 측정 — 모두 Play 중에 실행한다.
///   같은 값이 나오도록 측정 동안 게임을 멈추고, 플레이어를 (0, 0)에 두고, 캐릭터 그림을 숨기고, 깜빡임을 끈다.
///   화면은 항상 월드 (0, 0)을 찍는 임시 카메라로 찍는다. 적 수나 게임 오버와 상관없이 같은 화면을 잰다.
///   7주차 평가: 점수는 올랐는데 눈으로는 차이가 약했다 → 8주차 지표는 전부 화면 값(0~255)이다.
///   1-1 #1 7주차 화면 · 1-4 #2 환경광 · 2-4 #3 조명 전후 · 3-3 #4 그림자 전후 · 4-2 #5 그림자 방향 · 5-1 #6 비용 · 5-2 캡처
/// </summary>
public static class Week8Measure
{
    const float VISIBLE = 30f;   // 화면 값이 이만큼 달라지면 '눈에 띄게 바뀐' 픽셀로 센다

    // ────────────────────────────────────────────────────────── 프레임 단위 실행기

    class Step { public Action act; public int wait; }
    static readonly Queue<Step> _steps = new();
    static int _until;
    static Action _onAbort;

    static void Run(Action onAbort)
    {
        _onAbort = onAbort;
        // 예방: Play 직후 프레임은 불안정하다 → 60프레임 기다린 뒤 시작한다
        _until = Time.frameCount + 60;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Then(Action a, int wait = 6) => _steps.Enqueue(new Step { act = a, wait = wait });

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Halt(); _onAbort?.Invoke(); Debug.LogWarning("[측정] Play가 끝나 중단했다."); return; }
        if (Time.frameCount < _until) return;
        if (_steps.Count == 0) { Halt(); return; }
        var s = _steps.Dequeue();
        try { s.act(); }
        catch (Exception e) { Halt(); _onAbort?.Invoke(); Debug.LogException(e); return; }
        _until = Time.frameCount + s.wait;
    }

    static void Halt() { EditorApplication.update -= Tick; _steps.Clear(); }

    static bool NeedPlay(string title)
    {
        if (EditorApplication.isPlaying && _steps.Count == 0) return false;
        EditorUtility.DisplayDialog(title, _steps.Count > 0 ? "측정이 이미 진행 중입니다." : "Play 중에 실행하세요. (Game 뷰 1920×1080)", "확인");
        return true;
    }

    // ────────────────────────────────────────────────────────── 측정 준비와 복구

    /// <summary>게임을 멈추고 조명 · 캐릭터 상태를 기억한다. End()가 그대로 되돌린다.</summary>
    class Stage
    {
        float _ts; bool _async;
        Transform _player, _moved; Vector3 _playerPos, _movedPos;
        readonly List<SpriteRenderer> _hidden = new();
        readonly List<Behaviour> _off = new();
        readonly Dictionary<Light2D, (bool on, bool shadow, float intensity)> _lights = new();
        Camera _cam;
        // 메뉴를 누른 순간 카메라를 복사하면 Play 직후에 빈 화면이 찍혔다(테스트에서 확인) → 대기가 끝난 뒤 처음 찍을 때 복사한다
        public Camera cam => _cam != null ? _cam : (_cam = C.FixedCamera());

        public Stage(bool hideCharacters)
        {
            // 예방: 처음 쓰는 셰이더 조합을 에디터가 뒤에서 컴파일하는 동안 빠진 채로 찍히지 않게 측정 중에는 기다리게 한다
            _async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            _ts = Time.timeScale;
            Time.timeScale = 0f;
            var p = GameObject.FindWithTag("Player");
            if (p != null) { _player = p.transform; _playerPos = _player.position; _player.position = Vector3.zero; }
            if (hideCharacters)
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
                    if (sr.enabled && (sr.CompareTag("Player") || sr.CompareTag("Enemy") || sr.CompareTag("Projectile"))) { sr.enabled = false; _hidden.Add(sr); }
            foreach (var t in Object.FindObjectsByType<CG2Torch>()) if (t.enabled) { t.enabled = false; _off.Add(t); }
            foreach (var l in Object.FindObjectsByType<Light2D>())
            {
                _lights[l] = (l.enabled, l.shadowsEnabled, l.intensity);
                if (l.name == "Projectile Glow") l.enabled = false;   // 날아가던 투사체 불빛은 재지 않는다
                var mouse = l.GetComponent("CG2MouseLight") as Behaviour;
                if (mouse != null && mouse.enabled)
                {
                    // 7주차 테스트 조명은 마우스를 따라가 위치가 매번 다르다 → 측정 동안 플레이어 왼쪽 위에 둔다
                    mouse.enabled = false; _off.Add(mouse);
                    _moved = l.transform; _movedPos = l.transform.position;
                    l.transform.position = new Vector3(-1.5f, 0.5f, 0);
                }
            }
        }

        public void End()
        {
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            foreach (var kv in _lights)
                if (kv.Key != null) { kv.Key.enabled = kv.Value.on; kv.Key.shadowsEnabled = kv.Value.shadow; kv.Key.intensity = kv.Value.intensity; }
            foreach (var sr in _hidden) if (sr != null) sr.enabled = true;
            foreach (var b in _off) if (b != null) b.enabled = true;
            if (_player != null) _player.position = _playerPos;
            if (_moved != null) _moved.position = _movedPos;
            ShaderUtil.allowAsyncCompilation = _async;
            if (EditorApplication.isPlaying) Time.timeScale = _ts;
        }
    }

    static void SetDirectional(bool on, bool? shadows = null)
    {
        foreach (var l in C.DirectionalLights())
        {
            if (l.name == "Projectile Glow") continue;
            l.enabled = on;
            if (shadows.HasValue) l.shadowsEnabled = shadows.Value;
        }
    }

    static string ScreenRow(string label, C.Stat s, string tail = "") =>
        $"| {label} | {s.dark:0} | {s.mid:0} | {s.bright:0} | **{s.Range:0}** | {s.white:0.0}% |{tail}";

    const string SCREEN_HEAD = "| 화면 | 어두운 쪽 5% | 가운데 | 밝은 쪽 5% | 명암 폭 | 하얗게 날아감 |";
    const string SCREEN_LINE = "|---|---|---|---|---|---|";

    static string RawPath(string name) => $"{C.STORE}/{name}.png";

    // ────────────────────────────────────────────────────────── 1-1 측정 #1

    [MenuItem(C.MENU + "1-1. 측정 #1 — 7주차 화면 기록하기 (Play 중)", priority = 11)]
    public static void Measure1()
    {
        if (NeedPlay("측정 #1")) return;
        var st = new Stage(true);
        Then(() =>
        {
            var t = C.Shoot(st.cam);
            var s = C.Measure(t);
            C.SavePng(t, RawPath("shot_week7"));
            Object.DestroyImmediate(t);
            float g = C.Albedo(C.GROUND_PNG), w = C.Albedo(C.WALL_PNG);
            var sb = new StringBuilder("[측정] #1 7주차 화면 — 고정 카메라 · 캐릭터 숨김 · 게임 정지\n");
            sb.AppendLine(SCREEN_HEAD); sb.AppendLine(SCREEN_LINE);
            sb.AppendLine(ScreenRow("7주차 밤 장면", s));
            sb.AppendLine("| 그림 | 평균 반사율 |\n|---|---|");
            sb.AppendLine($"| 바닥 tile_ground_32 | **{g * 100f:0.0}%** |");
            sb.AppendLine($"| 벽 tile_wall_32 | **{w * 100f:0.0}%** |");
            sb.Append("        읽는 법: 화면 값 = 그림 × 빛. 바닥이 받은 빛의 몇 %만 되돌려 주는지가 반사율이다\n        ── 다음 ── Stop → 1-2");
            C.Save("m1", sb.ToString());
            C.Save("m1_range", s.Range.ToString("0"));
            st.End();
            Debug.Log(sb.ToString());
        });
        Run(st.End);
    }

    // ────────────────────────────────────────────────────────── 1-4 측정 #2

    [MenuItem(C.MENU + "1-4. 측정 #2 — 환경광 세기와 화면 값 (Play 중)", priority = 14)]
    public static void Measure2()
    {
        if (NeedPlay("측정 #2")) return;
        var st = new Stage(true);
        var g = C.Named(C.GROUND_LIGHT);
        if (g == null) { st.End(); Debug.LogWarning("[측정] #2 전역 조명을 찾지 못했다."); return; }
        SetDirectional(false);
        float mine = g.intensity;
        var rows = new List<string>();
        foreach (var v in new[] { 1f, 0.5f, 0.25f, 0.1f, mine })
        {
            float k = v; bool last = v == mine;
            Then(() => g.intensity = k);
            Then(() =>
            {
                var t = C.Shoot(st.cam);
                var s = C.Measure(t);
                Object.DestroyImmediate(t);
                rows.Add($"| {k:0.00}{(last ? " (지금 값)" : "")} | {s.mid:0} | **{255 - s.mid:0}** |");
            }, 1);
        }
        Then(() =>
        {
            g.intensity = mine;
            var sb = new StringBuilder("[측정] #2 환경광 세기 — 방향 있는 조명 끔 · 바닥만 · 화면 값 0~255\n| Global Light 2D Intensity | 바닥 화면 값 | 조명이 쓸 수 있는 폭 (255 − 바닥) |\n|---|---|---|\n");
            foreach (var r in rows) sb.AppendLine(r);
            sb.Append("        읽는 법: 환경광이 낮을수록 횃불이 밝힐 수 있는 폭이 넓다. 폭이 곧 명암이다\n        ── 다음 ── Stop → (직접) Test Light를 횃불로 → 2-1");
            C.Save("m2", sb.ToString());
            st.End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(st.End);
    }

    // ────────────────────────────────────────────────────────── 2-4 측정 #3

    [MenuItem(C.MENU + "2-4. 측정 #3 — 조명 전후 화면 비교 (Play 중)", priority = 24)]
    public static void Measure3()
    {
        if (NeedPlay("측정 #3")) return;
        var st = new Stage(true);
        Texture2D ambient = null, lit = null;
        Then(() => SetDirectional(false, false));
        Then(() => ambient = C.Shoot(st.cam), 1);
        Then(() => SetDirectional(true, false));
        Then(() => lit = C.Shoot(st.cam), 1);
        Then(() =>
        {
            var week7 = C.LoadPng(RawPath("shot_week7"));
            var sb = new StringBuilder("[측정] #3 조명 전후 — 고정 카메라 · 캐릭터 숨김 · 그림자 끔\n");
            sb.AppendLine(SCREEN_HEAD); sb.AppendLine(SCREEN_LINE);
            if (week7 != null) sb.AppendLine(ScreenRow("ⓐ 7주차 화면", C.Measure(week7)));
            var sAmb = C.Measure(ambient); var sLit = C.Measure(lit);
            sb.AppendLine(ScreenRow("ⓑ 환경광만", sAmb));
            sb.AppendLine(ScreenRow("ⓒ 조명 켬", sLit));
            float vsAmb = C.ChangedPercent(ambient, lit, VISIBLE);
            float vs7 = week7 != null ? C.ChangedPercent(week7, lit, VISIBLE) : 0;
            sb.AppendLine("| 비교 | 눈에 띄게 바뀐 면적 (화면 값 30 이상 차이) |\n|---|---|");
            sb.AppendLine($"| ⓑ → ⓒ 조명을 켰을 때 | **{vsAmb:0.0}%** |");
            if (week7 != null) sb.AppendLine($"| ⓐ → ⓒ 7주차 화면과 비교 | **{vs7:0.0}%** |");
            var warm = WarmCool(lit);
            sb.AppendLine($"| ⓒ 색 — 따뜻한 픽셀 / 푸른 픽셀 | {warm.x:0.0}% / {warm.y:0.0}% |");
            sb.Append("        ── 다음 ── Stop → 3-1");
            C.SavePng(lit, RawPath("shot_lights"));
            C.Save("m3", sb.ToString());
            C.Save("m3_range", sLit.Range.ToString("0"));
            Object.DestroyImmediate(ambient); Object.DestroyImmediate(lit);
            if (week7 != null) Object.DestroyImmediate(week7);
            st.End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(() => st.End());
    }

    static Vector2 WarmCool(Texture2D t)
    {
        var px = t.GetPixels32();
        int warm = 0, cool = 0;
        foreach (var c in px)
        {
            if (c.r > c.b + 20) warm++;
            else if (c.b > c.r + 8) cool++;
        }
        return new Vector2(100f * warm / px.Length, 100f * cool / px.Length);
    }

    // ────────────────────────────────────────────────────────── 3-3 측정 #4

    [MenuItem(C.MENU + "3-3. 측정 #4 — 그림자 전후 화면 비교 (Play 중)", priority = 33)]
    public static void Measure4()
    {
        if (NeedPlay("측정 #4")) return;
        var st = new Stage(true);
        Texture2D off = null, on = null;
        var saved = new Dictionary<Light2D, bool>();
        foreach (var l in C.DirectionalLights()) saved[l] = l.shadowsEnabled;
        Then(() => SetDirectional(true, false));
        Then(() => off = C.Shoot(st.cam), 1);
        Then(() => { foreach (var kv in saved) if (kv.Key != null) kv.Key.shadowsEnabled = kv.Value; });
        Then(() => on = C.Shoot(st.cam), 1);
        Then(() =>
        {
            var pa = off.GetPixels32(); var pb = on.GetPixels32();
            double sumOff = 0, sumOn = 0; int n = 0;
            for (int i = 0; i < pa.Length; i++)
            {
                float a = C.Gray(pa[i]), b = C.Gray(pb[i]);
                if (a - b >= VISIBLE) { sumOff += a; sumOn += b; n++; }
            }
            float area = 100f * n / pa.Length;
            float avgOff = n > 0 ? (float)(sumOff / n) : 0, avgOn = n > 0 ? (float)(sumOn / n) : 0;
            float depth = avgOff > 0 ? (1f - avgOn / avgOff) * 100f : 0;
            var sb = new StringBuilder("[측정] #4 그림자 전후 — 같은 조명 · 고정 카메라 · 캐릭터 숨김\n");
            sb.AppendLine(SCREEN_HEAD); sb.AppendLine(SCREEN_LINE);
            sb.AppendLine(ScreenRow("ⓒ 그림자 끔", C.Measure(off)));
            var sOn = C.Measure(on);
            sb.AppendLine(ScreenRow("ⓓ 그림자 켬", sOn));
            sb.AppendLine("| 그늘 (ⓒ → ⓓ에서 화면 값이 30 이상 낮아진 곳) | 값 |\n|---|---|");
            sb.AppendLine($"| 그늘 면적 | **{area:0.0}%** |");
            sb.AppendLine($"| 그늘 자리 밝기 끔 → 켬 | {avgOff:0} → {avgOn:0} |");
            sb.AppendLine($"| 그늘 깊이 | **{depth:0}%** |");
            sb.Append(area < 1f
                ? "        ⚠ 그늘이 거의 없다 → 3-2 그림자 검사로 돌아간다\n        ── 다음 ── Stop → 3-2"
                : "        ── 다음 ── Stop → 4-1");
            C.SavePng(on, RawPath("shot_shadows"));
            C.Save("m4", sb.ToString());
            C.Save("m4_area", area.ToString("0.0"));
            C.Save("m4_depth", depth.ToString("0"));
            Object.DestroyImmediate(off); Object.DestroyImmediate(on);
            st.End();
            Debug.Log(sb.ToString());
        }, 1);
        Run(st.End);
    }

    // ────────────────────────────────────────────────────────── 4-2 측정 #5

    [MenuItem(C.MENU + "4-2. 측정 #5 — 빛을 옮기면 그림자는 어디로 (Play 중)", priority = 42)]
    public static void Measure5()
    {
        if (NeedPlay("측정 #5")) return;
        var torch = C.FindTorch();
        if (torch == null) { Debug.LogWarning("[측정] #5 횃불이 없다 (2-1)."); return; }
        var st = new Stage(true);
        var torchPos = torch.transform.position;
        var torchShadow = (torch.shadowsEnabled, torch.shadowIntensity);

        // 다른 벽의 그림자가 섞이지 않게 벽 캐스터를 끄고 임시 기둥 하나만 둔다
        Vector2 center = new Vector2(-4f, -1f);
        var wall = GameObject.Find("Tilemap_Wall")?.GetComponent<ShadowCaster2D>();
        bool wallOn = wall != null && wall.enabled;
        if (wall != null) wall.enabled = false;
        var box = new GameObject("CG2 Measure Pillar") { hideFlags = HideFlags.DontSave };
        box.transform.position = center;
        box.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        box.AddComponent<BoxCollider2D>();
        box.AddComponent<ShadowCaster2D>();

        void Cleanup()
        {
            if (box != null) Object.DestroyImmediate(box);
            if (wall != null) wall.enabled = wallOn;
            if (torch != null) { torch.transform.position = torchPos; torch.shadowsEnabled = torchShadow.Item1; }
            st.End();
        }

        SetDirectional(false, false);
        torch.enabled = true;
        var rows = new List<string>();
        var cases = new (string label, Vector2 offset)[]
        {
            ("왼쪽 3유닛", new Vector2(-3, 0)), ("오른쪽 3유닛", new Vector2(3, 0)),
            ("위쪽 3유닛", new Vector2(0, 3)), ("아래쪽 3유닛", new Vector2(0, -3)),
            ("왼쪽 1.5유닛", new Vector2(-1.5f, 0)), ("왼쪽 5유닛", new Vector2(-5, 0))
        };
        foreach (var c in cases)
        {
            var cc = c;
            Texture2D off = null, on = null;
            Then(() => { torch.transform.position = center + cc.offset; torch.shadowsEnabled = false; }, 8);
            Then(() => off = C.Shoot(st.cam), 1);
            Then(() => { torch.shadowsEnabled = true; torch.shadowIntensity = 1f; }, 8);
            Then(() => on = C.Shoot(st.cam), 1);
            Then(() =>
            {
                rows.Add(ShadowRow(cc.label, cc.offset, center, st.cam, off, on));
                Object.DestroyImmediate(off); Object.DestroyImmediate(on);
            }, 1);
        }
        Then(() =>
        {
            var sb = new StringBuilder("[측정] #5 그림자 방향 — 횃불 하나 · 임시 기둥 하나(1.5유닛) · 기둥 중심에서 그림자 무게중심까지\n| 횃불 위치 (기둥 기준) | 빛이 오는 방향 | 그림자가 뻗은 방향 | 차이 | 그림자 넓이 |\n|---|---|---|---|---|\n");
            foreach (var r in rows) sb.AppendLine(r);
            sb.Append("        읽는 법: 그림자는 빛의 정반대쪽(차이 180°)으로 뻗고, 빛이 가까울수록 넓어진다\n        ── 다음 ── Stop → 4-3");
            C.Save("m5", sb.ToString());
            Cleanup();
            Debug.Log(sb.ToString());
        }, 1);
        Run(Cleanup);
    }

    static string ShadowRow(string label, Vector2 offset, Vector2 center, Camera cam, Texture2D off, Texture2D on)
    {
        var pa = off.GetPixels32(); var pb = on.GetPixels32();
        float perUnit = C.H / (cam.orthographicSize * 2f);
        double sx = 0, sy = 0; int n = 0;
        for (int y = 0; y < C.H; y++)
            for (int x = 0; x < C.W; x++)
            {
                int i = y * C.W + x;
                if (C.Gray(pa[i]) - C.Gray(pb[i]) < VISIBLE) continue;
                sx += (x - C.W / 2f) / perUnit; sy += (y - C.H / 2f) / perUnit; n++;
            }
        if (n == 0) return $"| {label} | — | 그림자 없음 | — | 0.0% |";
        var dir = new Vector2((float)(sx / n), (float)(sy / n)) - center;
        float from = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        float to = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return $"| {label} | {from:0}° | {to:0}° | **{Mathf.Abs(Mathf.DeltaAngle(from, to)):0}°** | {100f * n / pa.Length:0.0}% |";
    }

    // ────────────────────────────────────────────────────────── 5-1 측정 #6

    [MenuItem(C.MENU + "5-1. 측정 #6 — 조명과 그림자의 비용 (Play 중)", priority = 51)]
    public static void Measure6()
    {
        if (NeedPlay("측정 #6")) return;
        var st = new Stage(true);
        var saved = new Dictionary<Light2D, bool>();
        foreach (var l in C.DirectionalLights()) saved[l] = l.shadowsEnabled;
        int ours = 0, oursShadow = 0, mainShadow = 0;
        foreach (var kv in saved)
            if (kv.Key.name != "Projectile Glow")
            {
                ours++;
                if (kv.Value) oursShadow++;
                if (kv.Value && (kv.Key.name == C.TORCH || kv.Key.name == C.LANTERN)) mainShadow++;
            }

        var extra = new List<Light2D>();
        var main = Camera.main.transform.position;
        for (int i = 0; i < 16; i++)
        {
            var l = new GameObject($"CG2 Measure Light {i}") { hideFlags = HideFlags.DontSave }.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            C.ApplyToAllLayers(l);
            l.pointLightOuterRadius = 5f;
            l.intensity = 0.3f;
            l.transform.position = new Vector3(main.x - 9f + (i % 4) * 6f, main.y - 4.5f + (i / 4) * 3f, 0);
            l.enabled = false;
            extra.Add(l);
        }
        void Cleanup() { foreach (var l in extra) if (l != null) Object.Destroy(l.gameObject); st.End(); }
        void Ours(bool on, bool shadows, bool mainOnly)
        {
            foreach (var kv in saved)
            {
                if (kv.Key == null || kv.Key.name == "Projectile Glow") continue;
                kv.Key.enabled = on;
                bool main = kv.Key.name == C.TORCH || kv.Key.name == C.LANTERN;
                kv.Key.shadowsEnabled = shadows && kv.Value && (!mainOnly || main);
            }
        }
        void Extra(int count, bool shadows)
        {
            for (int i = 0; i < extra.Count; i++) { extra[i].enabled = i < count; extra[i].shadowsEnabled = shadows; }
        }

        var cases = new (string label, bool on, bool shadow, int add, bool addShadow, bool mainOnly)[]
        {
            ("ⓐ 환경광만", false, false, 0, false, false),
            ("ⓑ 우리 조명 · 그림자 끔", true, false, 0, false, false),
            ("ⓒ 우리 조명 · 횃불과 랜턴만 그림자", true, true, 0, false, true),
            ("ⓓ 우리 조명 · 전부 그림자", true, true, 0, false, false),
            ("ⓔ ⓓ + 조명 8개 · 그림자 끔", true, true, 8, false, false),
            ("ⓕ ⓓ + 조명 8개 · 그림자 켬", true, true, 8, true, false),
            ("ⓖ ⓓ + 조명 16개 · 그림자 켬", true, true, 16, true, false),
        };
        var rows = new string[cases.Length];
        int sw = 0, sh = 0;
        for (int i = 0; i < cases.Length; i++)
        {
            int k = i;
            // 30프레임 대기 — 다 쓴 렌더 텍스처는 잠시 풀에 남는다(7주차에서 확인)
            Then(() => { Ours(cases[k].on, cases[k].shadow, cases[k].mainOnly); Extra(cases[k].add, cases[k].addShadow); }, 30);
            Then(() =>
            {
                sw = Screen.width; sh = Screen.height;
                int total = (cases[k].on ? ours : 0) + cases[k].add;
                int shadowLights = (cases[k].shadow ? (cases[k].mainOnly ? mainShadow : oursShadow) : 0) + (cases[k].addShadow ? cases[k].add : 0);
                rows[k] = $"| {cases[k].label} | {total} | {shadowLights} | {UnityStats.drawCalls} | {UnityStats.setPassCalls} | {UnityStats.renderTextureCount} | {UnityStats.renderTextureBytes / 1048576f:0.0} MB |";
            }, 1);
        }
        Then(() =>
        {
            var sb = new StringBuilder($"[측정] #6 조명과 그림자의 비용 — Game 뷰 {sw}×{sh} · 게임 정지 · 캐릭터 숨김\n| 조건 | 조명 수 | 그림자 조명 수 | DrawCalls | SetPass | 렌더 텍스처 수 | 렌더 텍스처 메모리 |\n|---|---|---|---|---|---|---|\n");
            foreach (var r in rows) sb.AppendLine(r);
            sb.Append("        읽는 법: 조명 수보다 그림자 조명 수가 비용을 더 키운다. 그림자는 꼭 필요한 조명에만 켠다\n        ── 다음 ── 5-2");
            C.Save("m6", sb.ToString());
            Cleanup();
            Debug.Log(sb.ToString());
        }, 1);
        Run(Cleanup);
        Debug.Log("[측정] #6 시작 — 약 5초. 게임이 잠시 멈춘다. Unity 창을 앞에 둔다.");
    }

    // ────────────────────────────────────────────────────────── 5-2 캡처

    [MenuItem(C.MENU + "5-2. README 캡처 — 7주차 · 조명 · 그림자 나란히 (Play 중)", priority = 52)]
    public static void Readme()
    {
        if (NeedPlay("README 캡처")) return;
        var st = new Stage(false);
        var saved = new Dictionary<Light2D, bool>();
        foreach (var l in C.DirectionalLights()) saved[l] = l.shadowsEnabled;
        Texture2D lit = null, full = null;
        Then(() => SetDirectional(true, false));
        Then(() => lit = C.Shoot(st.cam), 1);
        Then(() => { foreach (var kv in saved) if (kv.Key != null) kv.Key.shadowsEnabled = kv.Value; });
        Then(() => full = C.Shoot(st.cam), 1);
        Then(() =>
        {
            var week7 = C.LoadPng(RawPath("shot_week7"));
            C.SavePng(lit, $"{C.CAPTURES}/week8_b_lights.png");
            C.SavePng(full, $"{C.CAPTURES}/week8_c_shadows.png");
            if (week7 != null) C.SavePng(week7, $"{C.CAPTURES}/week8_a_week7.png");
            const int PW = 640, PH = 360;
            var sheet = new Texture2D(PW * 3, PH, TextureFormat.RGBA32, false);
            var panels = new[] { week7, lit, full };
            for (int i = 0; i < 3; i++)
            {
                if (panels[i] == null) continue;
                for (int y = 0; y < PH; y++)
                    for (int x = 0; x < PW; x++)
                        sheet.SetPixel(i * PW + x, y, panels[i].GetPixel(x * 2, y * 2));
            }
            sheet.Apply();
            string path = $"{C.CAPTURES}/week8_before_after.png";
            C.SavePng(sheet, path);
            Object.DestroyImmediate(sheet); Object.DestroyImmediate(lit); Object.DestroyImmediate(full);
            if (week7 != null) Object.DestroyImmediate(week7);
            st.End();
            AssetDatabase.Refresh();
            Debug.Log($"[실습] 5-2 README 캡처 — {path}\n        왼쪽: 7주차 화면 · 가운데: 조명 · 오른쪽: 조명 + 그림자\n        ── 다음 ── 5-3");
        }, 1);
        Run(st.End);
    }
}
