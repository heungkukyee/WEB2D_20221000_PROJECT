using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 6주차 4단계 측정 #2 — "색 바꾸는 코드 한 줄"의 렌더링 비용.
///
/// 적 스프라이트 100개를 화면에 깔고 네 가지 방법으로 색을 입힌 뒤 Stats를 기록한다.
///   ① 틴트 없음                 — 흰색(원본)
///   ② SpriteRenderer.color      — 정점 색. 메시 데이터에 실려 가므로 머티리얼은 그대로다
///   ③ renderer.material.color   — 머티리얼을 개체마다 복제(인스턴스)한다 → 서로 다른 머티리얼 100개
///   ④ MaterialPropertyBlock     — 복제는 안 하지만 렌더러마다 상수가 다르다
/// 3주차처럼 SRP Batcher를 켠 상태와 끈 상태를 모두 잰다. 둘의 결론이 다르다는 것이 핵심이다.
///   ON  : 같은 셰이더면 머티리얼이 달라도 SetPass가 늘지 않는다(③ 괜찮음). 대신 MPB를 쓴 렌더러는 SRP Batcher에서 빠진다(④ 나쁨).
///   OFF : 머티리얼이 다르면 묶을 수 없다(③ 나쁨). 인터넷의 "MPB를 써라"는 이 시절의 조언이다.
/// 측정 중에는 Time.timeScale = 0으로 게임을 멈추고, 끝나면 SRP Batcher 설정을 원래대로 돌린다.
/// </summary>
public static class Week6TintBench
{
    const int COUNT = 100;
    const int SETTLE = 20;
    const string ROOT = "CG2 TintBench";

    static readonly string[] Modes =
    {
        "기준 (벤치 없음)", "① 틴트 없음 (흰색 100개)", "② SpriteRenderer.color", "③ material.color (복제 100개)", "④ MaterialPropertyBlock",
    };

    static int _step = -1, _waitUntil;
    static float _timeScale;
    static bool _srpOriginal;
    static GameObject _root;
    static Material _shared;
    static readonly List<SpriteRenderer> _srs = new();
    static readonly List<Material> _instances = new();
    static readonly int[,] _dc = new int[2, 5], _sp = new int[2, 5], _srp = new int[2, 5];
    static Color[] _colors;

    static int Pass => _step / Modes.Length;     // 0 = SRP Batcher ON, 1 = OFF
    static int Mode => _step % Modes.Length;

    [MenuItem("CG2 Lab/6주차/4-1. 측정 — 틴트 방법별 배칭 (Play 중)", priority = 41)]
    static void Start()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("틴트 측정", "Play 중에 실행하세요. (Game 뷰 1920×1080 · Stats 켜기)", "확인");
            return;
        }
        if (_step >= 0) { Debug.Log("[틴트 측정] 이미 진행 중입니다."); return; }

        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterProject/Prefabs/Enemy.prefab");
        var src = enemy != null ? enemy.GetComponent<SpriteRenderer>() : null;
        if (src == null || Camera.main == null) { Debug.LogError("[틴트 측정] Enemy 프리팹 또는 MainCamera가 없습니다."); return; }

        var theme = Object.FindAnyObjectByType<CG2Theme>();
        var p = theme != null && theme.palette != null ? theme.palette : PaletteStudioWindow.EnsureMyPalette();
        _colors = new[] { p.accent, p.sub2, p.danger, p.text, CG2Palette.ShiftValue(p.accent, -0.2f) };

        _timeScale = Time.timeScale;
        Time.timeScale = 0f;
        _srpOriginal = GraphicsSettings.useScriptableRenderPipelineBatching;
        Build(src);
        _step = 0;
        Enter();
        EditorApplication.update += Tick;
        Debug.Log($"[틴트 측정] 시작 — SRP Batcher ON/OFF × {Modes.Length}단계 × {SETTLE}프레임. 측정 중에는 게임이 멈춘다.");
    }

    static void Build(SpriteRenderer src)
    {
        _shared = src.sharedMaterial;
        _root = new GameObject(ROOT);
        var cam = Camera.main.transform.position;
        for (int i = 0; i < COUNT; i++)
        {
            var go = new GameObject($"Tint {i}");
            go.transform.SetParent(_root.transform);
            // 10×10 격자, 화면 안쪽(가로 ±9 · 세로 ±5 유닛)
            go.transform.position = new Vector3(cam.x - 9 + (i % 10) * 2f, cam.y - 5 + (i / 10) * 1.1f, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.sharedMaterial = _shared;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder;
            _srs.Add(sr);
        }
    }

    /// <summary>현재 단계의 조건을 만든다 — SRP Batcher 켬/끔 + 색 입히는 방법.</summary>
    static void Enter()
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = Pass == 0;
        _root.SetActive(Mode != 0);
        DestroyInstances();

        var mpb = new MaterialPropertyBlock();
        for (int i = 0; i < _srs.Count; i++)
        {
            var sr = _srs[i];
            var c = _colors[i % _colors.Length];
            sr.sharedMaterial = _shared;
            sr.color = Color.white;
            sr.SetPropertyBlock(null);
            switch (Mode)
            {
                case 2: sr.color = c; break;
                case 3:
                    var inst = sr.material;          // 이 순간 머티리얼이 복제된다 (이름에 "(Instance)")
                    inst.color = c;
                    _instances.Add(inst);
                    break;
                case 4:
                    mpb.SetColor("_Color", c);
                    sr.SetPropertyBlock(mpb);
                    break;
            }
        }
        _waitUntil = Time.frameCount + SETTLE;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(false); return; }
        if (Time.frameCount < _waitUntil) return;

        _dc[Pass, Mode] = UnityStats.drawCalls;
        _sp[Pass, Mode] = UnityStats.setPassCalls;
        _srp[Pass, Mode] = UnityStats.srpBatcherDrawCalls;

        _step++;
        if (_step >= Modes.Length * 2) { Finish(true); return; }
        Enter();
    }

    static void DestroyInstances()
    {
        // 복제된 머티리얼은 렌더러를 지워도 남는다(메모리 누수). 직접 지워야 한다.
        foreach (var m in _instances) Object.Destroy(m);
        _instances.Clear();
    }

    static void Finish(bool ok)
    {
        EditorApplication.update -= Tick;
        DestroyInstances();
        if (_root != null) Object.Destroy(_root);
        _srs.Clear();
        GraphicsSettings.useScriptableRenderPipelineBatching = _srpOriginal;
        if (EditorApplication.isPlaying) Time.timeScale = _timeScale;
        _step = -1;
        if (!ok) { Debug.LogWarning("[틴트 측정] Play가 끝나 측정을 중단했습니다. SRP Batcher 설정은 원래대로 돌렸습니다."); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"[측정] 틴트 방법별 렌더링 비용 — 적 스프라이트 {COUNT}개 · Game 뷰 {Screen.width}×{Screen.height}");
        sb.AppendLine("| 방법 | ON DrawCalls | ON SetPass | ON SRP Batch 경유 | OFF DrawCalls | OFF SetPass |");
        sb.AppendLine("|---|---|---|---|---|---|");
        for (int m = 0; m < Modes.Length; m++)
            sb.AppendLine($"| {Modes[m]} | {_dc[0, m]} | {_sp[0, m]} | {_srp[0, m]} | {_dc[1, m]} | {_sp[1, m]} |");
        sb.Append("        ON : ③은 ②와 같고 ④에서 SetPass가 튄다 — SRP Batcher는 '같은 셰이더'면 머티리얼이 달라도 묶는다. MPB는 그 호환을 깬다.\n" +
                  "        OFF: ②는 배칭되고 ③은 머티리얼 수만큼 늘어난다. → 결론: 개체 틴트는 SpriteRenderer.color(정점 색)가 어느 쪽에서도 안전하다.\n" +
                  $"        SRP Batcher 설정 복구 → {(_srpOriginal ? "ON" : "OFF")}");
        Week6Common.Save("tint_bench", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
