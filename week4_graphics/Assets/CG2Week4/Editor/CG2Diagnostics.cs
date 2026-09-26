#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// "캐릭터가 안 보인다" 같은 증상의 원인을 한 번에 찾기 위한 진단 도구.
///
/// 2D에서 무언가 안 보이는 원인은 대개 다음 중 하나다.
///   ① Sorting Layer 순서 — 뒤에 있는 레이어가 위에 그려진다
///   ② 오브젝트가 화면 밖 — 카메라가 다른 곳을 보고 있다
///   ③ 렌더러/오브젝트 비활성
///   ④ 스프라이트 미할당
///   ⑤ 카메라 Culling Mask에서 제외
/// 하나씩 추측해서 고치면 시간이 오래 걸리므로, 다섯 가지를 한꺼번에 찍는다.
/// </summary>
public static class CG2Diagnostics
{
    [MenuItem("CG2 Lab/진단 — 왜 안 보이는지 찍어보기", priority = 200)]
    public static void Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══ CG2 진단 ═══");

        // ── ① Sorting Layer 순서 ───────────────────────────────────
        sb.AppendLine("\n[Sorting Layer] 아래로 갈수록 위에 그려진다");
        var layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
            sb.AppendLine($"    {i}  {layers[i].name,-10} (id {layers[i].id})");

        // ── ② 카메라 ───────────────────────────────────────────────
        var cam = Camera.main;
        if (cam == null) sb.AppendLine("\n[카메라] MainCamera 없음 ⚠");
        else
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            sb.AppendLine($"\n[카메라] {cam.name}  pos {cam.transform.position}  " +
                          $"ortho {cam.orthographic}  size {cam.orthographicSize}");
            sb.AppendLine($"    보이는 범위  X [{cam.transform.position.x - halfW:F1} … {cam.transform.position.x + halfW:F1}]  " +
                          $"Y [{cam.transform.position.y - halfH:F1} … {cam.transform.position.y + halfH:F1}]");
            sb.AppendLine($"    Culling Mask {cam.cullingMask}  ({MaskNames(cam.cullingMask)})");
            // 클립 평면 — 여기가 무너지면 배경색만 칠하고 아무것도 안 그린다.
            // 스프라이트는 z=0, 카메라는 z=-10이므로 깊이 10이 [near, far] 안에 들어와야 한다.
            sb.AppendLine($"    Clip  near {cam.nearClipPlane}  far {cam.farClipPlane}  " +
                          $"→ 스프라이트까지 깊이 {Mathf.Abs(cam.transform.position.z):F1} " +
                          $"{(cam.farClipPlane > Mathf.Abs(cam.transform.position.z) && cam.nearClipPlane < Mathf.Abs(cam.transform.position.z) ? "✓ 범위 안" : "⚠ 범위 밖 — 아무것도 안 그려진다")}");
            sb.AppendLine($"    Clear {cam.clearFlags}  배경색 {cam.backgroundColor}  depth {cam.depth}");
        }

        // ── ③ 플레이어 집중 진단 ───────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        sb.AppendLine("\n[플레이어]");
        if (player == null) sb.AppendLine("    씬에 'Player' 태그 오브젝트가 없다 ⚠");
        else
        {
            var sr = player.GetComponent<SpriteRenderer>();
            sb.AppendLine($"    {player.name}  pos {player.transform.position}  " +
                          $"scale {player.transform.localScale}  activeInHierarchy {player.activeInHierarchy}");
            sb.AppendLine($"    GameObject Layer : {LayerMask.LayerToName(player.layer)} ({player.layer})");
            if (sr == null) sb.AppendLine("    SpriteRenderer 없음 ⚠");
            else
            {
                sb.AppendLine($"    SpriteRenderer  enabled {sr.enabled}  " +
                              $"sprite {(sr.sprite != null ? sr.sprite.name : "없음 ⚠")}  " +
                              $"color {sr.color}");
                sb.AppendLine($"    Sorting  layer '{sr.sortingLayerName}' (index {IndexOf(sr.sortingLayerName)})  " +
                              $"order {sr.sortingOrder}");
                sb.AppendLine($"    Material {MatInfo(sr.sharedMaterial)}");
                sb.AppendLine($"    화면상 크기  {sr.bounds.size}  월드 bounds {sr.bounds}");
                if (cam != null) sb.AppendLine($"    카메라 시야 안인가 : {InView(cam, sr.bounds)}");
            }
        }

        // ── ④ 타일맵들 ─────────────────────────────────────────────
        sb.AppendLine("\n[Tilemap]");
        var maps = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include);
        if (maps.Length == 0) sb.AppendLine("    씬에 타일맵 없음");
        foreach (var r in maps)
        {
            var tm = r.GetComponent<Tilemap>();
            sb.AppendLine($"    {Path(r.transform)}");
            sb.AppendLine($"        layer '{r.sortingLayerName}' (index {IndexOf(r.sortingLayerName)})  " +
                          $"order {r.sortingOrder}  enabled {r.enabled}  mode {r.mode}");
            sb.AppendLine($"        Material {MatInfo(r.sharedMaterial)}");
            if (tm != null)
            {
                var b = tm.localBounds;
                var o = r.transform.position;
                sb.AppendLine($"        cellSize {tm.layoutGrid.cellSize}  " +
                              $"셀 {tm.cellBounds.min}~{tm.cellBounds.max}  타일 {tm.GetUsedTilesCount()}종");
                sb.AppendLine($"        월드 범위  X [{o.x + b.min.x:F1} … {o.x + b.max.x:F1}]  " +
                              $"Y [{o.y + b.min.y:F1} … {o.y + b.max.y:F1}]");
            }
        }

        // ── 2D 조명 ────────────────────────────────────────────────
        // URP 2D Renderer의 기본 머티리얼이 Lit이면 Light2D 없이는 스프라이트가 검게 나온다.
        var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        sb.AppendLine($"\n[Light2D] {lights.Length}개");
        foreach (var l in lights)
        {
            sb.AppendLine($"    {Path(l.transform)}  type {l.lightType}  " +
                          $"intensity {l.intensity}  color {l.color}  enabled {l.enabled}");
            // 조명이 있어도 대상 Sorting Layer가 비어 있으면 아무것도 안 비춘다.
            sb.AppendLine($"        비추는 Sorting Layer : {TargetLayers(l)}");
        }
        if (lights.Length == 0)
            sb.AppendLine("    없음 — 머티리얼이 Sprite-Lit 계열이면 모든 스프라이트가 검게 나온다 ⚠");

        // ── ⑤ 판정 ─────────────────────────────────────────────────
        sb.AppendLine("\n[판정]");
        sb.AppendLine(Verdict(player, maps, cam));

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// URP 2D 전역 조명을 씬에 넣는다.
    ///
    /// URP 2D Renderer의 기본 머티리얼이 Lit이면 스프라이트는 빛을 받아야 보인다.
    /// Light2D가 하나도 없으면 전부 검게 그려진다 — 화면이 꺼진 것처럼 보인다.
    /// Global 타입 Light2D 하나면 모든 Sorting Layer가 균일하게 밝아진다.
    /// </summary>
    [MenuItem("CG2 Lab/진단 — 2D 전역 조명 추가 · 복구", priority = 201)]
    public static void AddGlobalLight2D()
    {
        // 이미 있으면 새로 만들지 않고 고친다.
        // 조명이 있는데도 화면이 검은 경우가 대부분 "대상 레이어 비어 있음"이기 때문이다.
        Light2D light = null;
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            if (l.lightType == Light2D.LightType.Global) { light = l; break; }

        bool created = false;
        if (light == null)
        {
            var go = new GameObject("Global Light 2D");
            Undo.RegisterCreatedObjectUndo(go, "2D 전역 조명 추가");
            light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            created = true;
        }

        Undo.RecordObject(light, "2D 전역 조명 설정");
        light.intensity = 1f;
        light.color = Color.white;
        light.enabled = true;

        string before = TargetLayers(light);
        AddMissingSortingLayers(light);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = light.gameObject;

        Debug.Log($"[검증] Global Light 2D {(created ? "생성" : "복구")}\n" +
                  $"        비추는 Sorting Layer  이전 : {before}\n" +
                  $"                              이후 : {TargetLayers(light)}\n" +
                  "        URP 2D에서 Lit 스프라이트는 빛이 없으면 검게 그려집니다.\n" +
                  "        어둡게 하려면 이 조명의 intensity나 color를 낮추세요.");
    }

    /// <summary>
    /// 씬의 모든 Light2D가 모든 Sorting Layer를 비추도록 맞춘다.
    ///
    /// Sorting Layer를 새로 추가하면 기존 조명의 대상 목록에 자동으로 들어가지 않는다.
    /// 그 상태에서는 새 레이어의 스프라이트가 통째로 검게 그려진다 —
    /// 조명이 아예 없으면 멀쩡한데, 있는데 안 비추면 검어지는 것이 함정이다.
    /// 그래서 Sorting Layer를 건드리는 쪽에서 이 함수를 반드시 같이 호출한다.
    /// </summary>
    internal static int RepairAllLight2DTargetLayers()
    {
        int n = 0;
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            if (AddMissingSortingLayers(l)) n++;
        return n;
    }

    /// <summary>
    /// 조명의 대상 목록에 **빠져 있는 레이어만 더한다.**
    ///
    /// 전체 목록으로 덮어쓰면 "이 조명은 배경만 비춘다" 같은 의도적인 제외 설정이 날아간다.
    /// 새로 만든 레이어가 반드시 비춰지는 것만 보장하면 되므로, 없는 것만 추가하는 편이
    /// 목적을 똑같이 달성하면서 기존 설정을 건드리지 않는다.
    ///
    /// URP가 이 목록을 public으로 노출하지 않아 SerializedObject로 다룬다.
    /// </summary>
    /// <returns>실제로 무언가 추가했으면 true</returns>
    static bool AddMissingSortingLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        var prop = so.FindProperty("m_ApplyToSortingLayers");
        if (prop == null || !prop.isArray) return false;

        var have = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < prop.arraySize; i++)
            have.Add(prop.GetArrayElementAtIndex(i).intValue);

        var missing = new System.Collections.Generic.List<int>();
        foreach (var l in SortingLayer.layers)
            if (!have.Contains(l.id)) missing.Add(l.id);

        if (missing.Count == 0) return false;

        int at = prop.arraySize;
        prop.arraySize = at + missing.Count;
        for (int i = 0; i < missing.Count; i++)
            prop.GetArrayElementAtIndex(at + i).intValue = missing[i];

        so.ApplyModifiedProperties();
        return true;
    }

    // ────────────────────────────────────────────────────────────── 판정

    static string Verdict(GameObject player, TilemapRenderer[] maps, Camera cam)
    {
        // 클립 평면부터 본다. 여기가 무너지면 정렬이 아무리 맞아도 화면에 아무것도 안 나온다.
        if (cam != null)
        {
            float depth = Mathf.Abs(cam.transform.position.z);
            if (cam.farClipPlane <= cam.nearClipPlane || cam.farClipPlane < depth)
                return $"    카메라 클립 평면이 무너졌다 — near {cam.nearClipPlane} / far {cam.farClipPlane}.\n" +
                       $"        스프라이트까지 깊이 {depth:F1}이 이 구간에 없어 월드가 통째로 안 그려진다.\n" +
                       "        Cinemachine을 쓴다면 가상 카메라 Lens의 Near/Far Clip Plane을 확인하라.\n" +
                       "        → 'CG2 Lab → 시네머신 → 카메라 리그 구성'을 다시 실행하면 복구된다.";
        }

        // 스프라이트가 "검게" 나오는 대표 원인 — Lit 머티리얼인데 빛이 닿지 않는다.
        {
            var sr0 = player != null ? player.GetComponent<SpriteRenderer>() : null;
            string sh = sr0 != null && sr0.sharedMaterial != null && sr0.sharedMaterial.shader != null
                ? sr0.sharedMaterial.shader.name : "";
            bool lit = sh.Contains("Lit") && !sh.Contains("Unlit");

            if (lit)
            {
                var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include);
                if (lights.Length == 0)
                    return $"    스프라이트가 Lit 머티리얼('{sh}')인데 씬에 Light2D가 하나도 없다.\n" +
                           "        URP 2D에서 이 조합은 모든 스프라이트를 검게 그린다.\n" +
                           "        → 'CG2 Lab → 진단 — 2D 전역 조명 추가 · 복구' 실행.";

                // 조명이 있어도 플레이어의 Sorting Layer를 비추지 않으면 검게 남는다.
                string playerLayer = sr0.sortingLayerName;
                bool covered = false;
                foreach (var l in lights)
                    if (l.enabled && l.intensity > 0f && TargetLayers(l).Contains(playerLayer)) covered = true;

                if (!covered)
                    return $"    Light2D는 있지만 '{playerLayer}' 레이어를 비추지 않는다.\n" +
                           "        Sorting Layer를 나중에 추가하면 기존 조명의 대상 목록에 자동으로 들어가지 않는다.\n" +
                           "        → 'CG2 Lab → 진단 — 2D 전역 조명 추가 · 복구' 실행.";
            }
        }

        if (player == null) return "    플레이어가 씬에 없다. 'CG2 Starter → Step 3'로 씬을 다시 빌드하라.";

        var sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) return "    플레이어에 SpriteRenderer가 없다.";
        if (!player.activeInHierarchy) return "    플레이어 GameObject가 비활성이다.";
        if (!sr.enabled) return "    플레이어 SpriteRenderer가 꺼져 있다.";
        if (sr.sprite == null) return "    플레이어에 스프라이트가 할당되지 않았다.";
        if (sr.color.a < 0.05f) return "    플레이어 색의 알파가 0에 가깝다.";

        if (cam != null && !InView(cam, sr.bounds))
            return "    플레이어가 카메라 시야 밖이다. 카메라 위치와 Confiner 경계를 확인하라.";

        if (cam != null && (cam.cullingMask & (1 << player.layer)) == 0)
            return $"    카메라 Culling Mask가 '{LayerMask.LayerToName(player.layer)}' 레이어를 제외하고 있다.";

        int playerIdx = IndexOf(sr.sortingLayerName);
        foreach (var r in maps)
        {
            if (!r.enabled) continue;
            int mapIdx = IndexOf(r.sortingLayerName);
            if (mapIdx > playerIdx || (mapIdx == playerIdx && r.sortingOrder > sr.sortingOrder))
                return $"    '{Path(r.transform)}' 타일맵이 플레이어보다 위에 그려진다.\n" +
                       $"        타일맵 '{r.sortingLayerName}'(index {mapIdx}, order {r.sortingOrder}) > " +
                       $"플레이어 '{sr.sortingLayerName}'(index {playerIdx}, order {sr.sortingOrder})\n" +
                       "        → 'CG2 Lab → 4주차 준비 → 2. Sorting Layer 등록'을 실행해 순서를 바로잡아라.";
        }

        return "    렌더링 조건상 플레이어는 보여야 한다. 위 수치를 함께 확인하라.";
    }

    // ────────────────────────────────────────────────────────────── 유틸

    /// <summary>
    /// 머티리얼과 셰이더를 함께 찍는다.
    ///
    /// URP 2D에서 스프라이트가 검게 나오는 대표 원인이 "Sprite-Lit 머티리얼 + Light2D 없음"이다.
    /// 셰이더 이름에 Lit이 들어 있는지가 판별 기준이므로 반드시 셰이더까지 봐야 한다.
    /// </summary>
    static string MatInfo(Material m)
    {
        if (m == null) return "없음 ⚠";
        string shader = m.shader != null ? m.shader.name : "셰이더 없음 ⚠";
        bool lit = shader.Contains("Lit") && !shader.Contains("Unlit");
        return $"'{m.name}'  셰이더 '{shader}'{(lit ? "  ← Lit 계열: Light2D 필요" : "")}";
    }

    /// <summary>
    /// Light2D가 실제로 비추는 Sorting Layer 목록.
    ///
    /// URP는 이 값을 public으로 노출하지 않아 SerializedObject로 읽어야 한다.
    /// 코드로 AddComponent한 Light2D는 이 목록이 비어 있을 수 있는데,
    /// 그러면 조명이 켜져 있어도 아무것도 비추지 않아 스프라이트가 검게 남는다.
    /// </summary>
    internal static string TargetLayers(Light2D light)
    {
        var prop = new SerializedObject(light).FindProperty("m_ApplyToSortingLayers");
        if (prop == null || !prop.isArray) return "읽을 수 없음";
        if (prop.arraySize == 0) return "없음 ⚠ — 이 조명은 아무것도 비추지 않는다";

        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < prop.arraySize; i++)
        {
            int id = prop.GetArrayElementAtIndex(i).intValue;
            names.Add(SortingLayer.IDToName(id) is { Length: > 0 } n ? n : $"id{id}");
        }
        return string.Join(", ", names);
    }

    static int IndexOf(string layerName)
    {
        var layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++) if (layers[i].name == layerName) return i;
        return -1;
    }

    static bool InView(Camera cam, Bounds b)
    {
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        var c = cam.transform.position;
        return b.max.x >= c.x - halfW && b.min.x <= c.x + halfW &&
               b.max.y >= c.y - halfH && b.min.y <= c.y + halfH;
    }

    static string MaskNames(int mask)
    {
        if (mask == ~0) return "Everything";
        var sb = new StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            var n = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(n)) { if (sb.Length > 0) sb.Append(", "); sb.Append(n); }
        }
        return sb.ToString();
    }

    static string Path(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
#endif
