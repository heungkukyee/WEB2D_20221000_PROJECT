using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5주차 2단계 — 카메라 Size를 바꿔 가며 Stats를 자동 기록한다.
///
/// 왜 도구가 필요한가 —
///   1) Cinemachine이 붙은 뒤에는 Main Camera의 Size를 손으로 바꿔도 다음 프레임에 덮어써진다.
///      CinemachineCamera의 Lens를 바꾸고 Confiner 캐시도 비워야 한다(CG2CameraRig.SetOrthoSize).
///   2) Size를 바꾼 직후 몇 프레임은 청크 컬링과 배칭이 갱신되지 않은 값이 찍힌다.
///      30프레임을 기다린 뒤에 기록한다.
///   3) 게임 씬에서는 적이 계속 생겨 Tris가 흔들린다. 측정하는 동안 Time.timeScale을 0으로 멈춘다.
///
/// 이 파일은 CG2.Camera 어셈블리를 참조하지 않는다. 리그가 있으면 SendMessage로 부르고,
/// 없으면 Camera.orthographicSize를 직접 바꾼다. Cinemachine이 없어도 컴파일된다.
/// </summary>
public static class CameraSizeProbe
{
    const string RIG_NAME = "CG2 CameraRig";
    const int TEXEL_PPU = 64;
    const int SETTLE_FRAMES = 30;

    static readonly float[] Sizes = { 5f, 7f, 8.4375f, 10f, 12f };

    static int _index = -1, _waitUntil;
    static float _originalSize, _originalTimeScale;
    static readonly List<string> _rows = new();

    [MenuItem("CG2 Lab/5주차/2. Size 측정 — 5단계 자동 기록 (Play 중)", priority = 20)]
    static void Start()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Size 측정", "Play 상태에서 실행하세요.\n(Game 뷰 1920×1080 고정 · Stats 켜기)", "확인");
            return;
        }
        if (_index >= 0) { Debug.Log("[Size 측정] 이미 진행 중입니다."); return; }

        var cam = Camera.main;
        if (cam == null || !cam.orthographic) { Debug.LogError("[Size 측정] 직교 MainCamera가 없습니다."); return; }

        _originalSize = cam.orthographicSize;
        _originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;      // 적 스폰·이동을 멈춰 측정 대상을 고정한다
        _rows.Clear();
        _index = 0;
        SetSize(Sizes[0]);
        _waitUntil = Time.frameCount + SETTLE_FRAMES;
        EditorApplication.update += Tick;
        Debug.Log($"[Size 측정] 시작 — Size {string.Join(" → ", Sizes)} 순서로 각 {SETTLE_FRAMES}프레임 뒤 기록합니다. " +
                  "측정 중에는 게임이 멈춥니다.");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(false); return; }
        if (Time.frameCount < _waitUntil) return;

        var cam = Camera.main;
        float s = cam.orthographicSize;
        float screenPxPerUnit = Screen.height / (2f * s);
        _rows.Add(
            $"| {s,7:0.####} | {2f * s * cam.aspect,5:F1} × {2f * s,4:F1} | {screenPxPerUnit,6:F2} | " +
            $"{screenPxPerUnit / TEXEL_PPU,5:F3} | {UnityStats.drawCalls,5} | {UnityStats.setPassCalls,4} | " +
            $"{UnityStats.triangles,7:N0} |");

        _index++;
        if (_index >= Sizes.Length) { Finish(true); return; }
        SetSize(Sizes[_index]);
        _waitUntil = Time.frameCount + SETTLE_FRAMES;
    }

    static void Finish(bool ok)
    {
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying)
        {
            SetSize(_originalSize);
            Time.timeScale = _originalTimeScale;
        }
        _index = -1;
        if (!ok) { Debug.LogWarning("[Size 측정] Play가 끝나 측정을 중단했습니다."); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"[측정] 카메라 Size 5단계 · Game 뷰 {Screen.width}×{Screen.height} · 텍스처 PPU {TEXEL_PPU}");
        sb.AppendLine("| Size | 보이는 영역(유닛) | 화면px/유닛 | 화면px/텍셀 | DrawCalls | SetPass | Tris |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in _rows) sb.AppendLine(r);
        sb.Append("        ↑ 이 표를 그대로 README에 붙여 넣으세요. 화면px/텍셀이 정수(1.000, 2.000)인 행만 픽셀이 균일합니다.");
        Debug.Log(sb.ToString());
    }

    static void SetSize(float size)
    {
        var rig = GameObject.Find(RIG_NAME);
        if (rig != null && rig.activeInHierarchy)
            rig.SendMessage("SetOrthoSize", size, SendMessageOptions.DontRequireReceiver);
        else if (Camera.main != null)
            Camera.main.orthographicSize = size;
    }
}
