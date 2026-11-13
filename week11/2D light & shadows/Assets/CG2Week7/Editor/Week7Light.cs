using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 7주차 4단계 — 게임 씬에 테스트 조명을 넣고 마우스로 움직여 본다.
///   4-1 밤 장면     : 전역 조명을 낮춘다. 전역 조명이 1이면 이미 다 밝아서 테스트 조명의 명암이 안 보인다
///   (직접)          : GameObject → Light → Spot Light 2D (Unity 6.6에는 Point Light 2D 메뉴가 없다. 360°면 점 조명)
///   4-2 조명 검사   : 학생이 넣은 조명의 설정을 점검하고 마우스 추적을 붙인다 — Normal Maps가 Disabled면 경고
///   4-3 비교        : 노멀 맵 반응 끄기 ↔ 켜기 (Play 중에는 N 키)
/// 8주차(2D 라이트와 그림자)에서 이 조명을 횃불 · 가로등으로 발전시킨다.
/// </summary>
public static class Week7Light
{
    public const string TEST = "Test Light";
    const string BACKUP = "light_backup";

    [MenuItem(Week7Common.MENU + "4-1. 밤 장면 만들기 — 전역 조명 낮추기", priority = 41)]
    public static void Night()
    {
        if (Week7Common.Busy("밤 장면")) return;
        var sb = new StringBuilder("[실습] 4-1 밤 장면 — 전역 조명 세기\n| 조명 | 비추는 레이어 | 전 | 후 |\n|---|---|---|---|\n");
        var backup = new StringBuilder(Week7Common.Load(BACKUP));
        foreach (var l in Object.FindObjectsByType<Light2D>())
        {
            if (l.lightType != Light2D.LightType.Global) continue;
            if (!backup.ToString().Contains(l.name + "=")) backup.Append($"{l.name}={l.intensity}\n");
            bool entity = Week7Common.TargetNames(l).Contains("Entity");
            bool ground = Week7Common.TargetNames(l).Contains("Ground");
            float to = entity && !ground ? 0.2f : 0.25f;   // Linear에서 0.5는 화면 밝기 73%(6주차) — 이미 너무 밝아 명암이 묻힌다
            sb.AppendLine($"| {l.name} | {Week7Common.TargetNames(l)} | {l.intensity:0.00} | {to:0.00} |");
            Undo.RecordObject(l, "밤 장면");
            l.intensity = to;
        }
        Week7Common.Save(BACKUP, backup.ToString());
        sb.Append("        왜 낮추나: 전역 조명은 방향이 없다 → 노멀 맵과 무관하게 모든 면을 똑같이 밝힌다. 이것이 크면 명암이 묻힌다.\n" +
                  "        ── 다음 ── GameObject → Light → Spot Light 2D 를 직접 추가 → 이름 'Test Light' → 4-2");
        Debug.Log(sb.ToString());
    }

    static Light2D FindTest()
    {
        Light2D best = null;
        foreach (var l in Object.FindObjectsByType<Light2D>())
        {
            if (l.lightType == Light2D.LightType.Global) continue;
            if (l.name == TEST) return l;
            best ??= l;
        }
        return best;
    }

    [MenuItem(Week7Common.MENU + "4-2. 테스트 조명 검사 + 마우스 따라가기 붙이기", priority = 42)]
    public static void Inspect()
    {
        if (Week7Common.Busy("테스트 조명")) return;
        var l = FindTest();
        bool made = false;
        if (l == null)
        {
            // 직접 추가하지 못한 학생용 — 메뉴(Spot Light 2D)와 같은 기본값으로 만든다.
            l = new GameObject(TEST).AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            Week7Common.ApplyToAllLayers(l);
            var p = GameObject.FindWithTag("Player");
            l.transform.position = p ? p.transform.position + new Vector3(1.5f, 1f, 0) : Vector3.zero;
            l.pointLightOuterRadius = 4f;
            l.intensity = 1.5f;
            Undo.RegisterCreatedObjectUndo(l.gameObject, "테스트 조명");
            made = true;
        }
        if (l.GetComponent<CG2MouseLight>() == null) Undo.AddComponent<CG2MouseLight>(l.gameObject);
        Selection.activeGameObject = l.gameObject;

        bool off = l.normalMapQuality == Light2D.NormalMapQuality.Disabled;
        var sb = new StringBuilder($"[검증] 4-2 테스트 조명 '{l.name}'{(made ? " (없어서 새로 만듦)" : "")}\n| 항목 | 값 | 판정 |\n|---|---|---|\n");
        sb.AppendLine($"| 종류 | {l.lightType} (각도 {l.pointLightOuterAngle:0}°) | {(l.lightType == Light2D.LightType.Point ? "✓ 점 조명" : "⚠ Spot Light 2D로 만든다")} |");
        sb.AppendLine($"| 반경 Outer | {l.pointLightOuterRadius:0.0} | {(l.pointLightOuterRadius >= 2.5f ? "✓" : "⚠ 3~5로 키운다 (기본 1은 너무 작다)")} |");
        sb.AppendLine($"| 세기 Intensity | {l.intensity:0.0} | {(l.intensity >= 1f ? "✓" : "⚠ 1.5 정도로")} |");
        sb.AppendLine($"| 비추는 레이어 | {Week7Common.TargetNames(l)} | {(Week7Common.TargetNames(l).Contains("Entity") ? "✓" : "⚠ Entity 포함")} |");
        sb.AppendLine($"| **Normal Maps → Quality** | **{l.normalMapQuality}** | {(off ? "⚠ Disabled = 노멀 맵을 무시한다 → Accurate로" : "✓")} |");
        bool high = l.normalMapDistance > 1.5f;
        sb.AppendLine($"| Normal Maps → Distance | {l.normalMapDistance:0.0} | {(high ? "⚠ 조명 높이 — 캐릭터(1유닛)에 비해 너무 높아 위에서 내리쬔다 → 1로" : "✓ 조명 높이 (작을수록 비스듬히 · 명암 진하게)")} |");
        sb.Append(off
            ? "        ⚠ 지금은 조명을 움직여도 밝기만 바뀌고 명암은 그대로다. Inspector → Light 2D → Normal Maps → Quality = Accurate\n" +
              "          (Light2D.normalMapQuality는 코드에서 바꿀 수 없는 읽기 전용 속성이다 — Inspector에서 정하는 값)"
            : (high ? "        ⚠ Distance를 1로 낮추면 명암이 약 1.5배 진해진다 (테스트: 입체감 점수 13 → 20)\n" : "") +
              "        ✓ 씬 뷰에서 이동 도구(W)로 끌어 보거나, Play 중 마우스를 움직여 본다 (N 키 = 노멀 맵 반응 끄기/켜기 · 휠 = 조명 높이)");
        Debug.Log(sb.ToString());
    }

    [MenuItem(Week7Common.MENU + "4-3. (비교) 노멀 맵 반응 끄기 ↔ 켜기", priority = 43)]
    public static void Toggle()
    {
        var l = FindTest();
        if (l == null) { EditorUtility.DisplayDialog("비교", "먼저 테스트 조명을 추가하세요 (4-2).", "확인"); return; }
        var next = l.normalMapQuality == Light2D.NormalMapQuality.Disabled ? Light2D.NormalMapQuality.Accurate : Light2D.NormalMapQuality.Disabled;
        Undo.RecordObject(l, "노멀 맵 비교");
        Week7Common.SetNormalMap(l, next);
        Debug.Log($"[비교] 4-3 '{l.name}' Normal Maps → {next}  " +
                  (next == Light2D.NormalMapQuality.Disabled ? "(평면 — 밝기만 변한다)" : "(입체 — 밝은 면이 조명을 따라 움직인다)"));
    }

    [MenuItem(Week7Common.MENU + "되돌리기 — 4단계 조명 (전역 조명 복구 · 테스트 조명 삭제)", priority = 92)]
    public static void Revert()
    {
        if (Week7Common.Busy("되돌리기")) return;
        var map = Week7Common.Load(BACKUP);
        int n = 0;
        foreach (var l in Object.FindObjectsByType<Light2D>())
        {
            if (l.lightType == Light2D.LightType.Global)
            {
                foreach (var line in map.Split('\n'))
                {
                    var kv = line.Split('=');
                    if (kv.Length == 2 && kv[0] == l.name && float.TryParse(kv[1], out var v)) { Undo.RecordObject(l, "되돌리기"); l.intensity = v; n++; }
                }
            }
            else if (l.name == TEST) Undo.DestroyObjectImmediate(l.gameObject);
        }
        Week7Common.Save(BACKUP, "");
        Debug.Log($"[되돌리기] 전역 조명 세기 복구 {n}개 · 테스트 조명 삭제");
    }
}
