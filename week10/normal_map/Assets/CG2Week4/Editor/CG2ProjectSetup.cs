#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;   // Light2D

/// <summary>
/// 4주차 준비 — 프로젝트 전역 설정을 4주차 규격에 맞춘다.
///
/// 하는 일 두 가지.
///   1) 스프라이트 PPU를 64로 통일한다.
///   2) Sorting Layer(Ground / Wall / Entity / FX)를 등록한다.
///
/// PPU 통일이 필요한 이유 — 2주차 StarterSceneBuilder.Save()가 spritePixelsPerUnit을
/// 지정하지 않아 Unity 기본값 100이 박혔다. 그 결과 스프라이트가 콜라이더보다
/// 1.4~1.7배 작다(히트박스 불일치). PPU 64로 맞추면 오차 10% 이내로 들어온다.
/// 즉 이것은 4주차 편의를 위한 변경이 아니라 2주차 버그 수정이다.
///
///   Player 64px : PPU100 → 0.64유닛  /  PPU64 → 1.00유닛  (콜라이더 지름 0.90)
///   Enemy  48px : PPU100 → 0.48유닛  /  PPU64 → 0.75유닛  (콜라이더 지름 0.80)
///
/// 되돌리기를 대비해 변경 전 PPU를 JSON으로 백업하므로, 언제든 원복할 수 있다.
/// </summary>
public static class CG2ProjectSetup
{
    const int TARGET_PPU = 64;
    const string BACKUP_PATH = "Assets/CG2Week4/ppu_backup.json";

    // 스프라이트를 찾을 폴더. 없는 폴더는 조용히 건너뛴다.
    static readonly string[] SEARCH_FOLDERS =
    {
        "Assets/StarterProject/Art",
        "Assets/Sprites",
        "Assets/CG2Week4/Art",
    };

    // 순서가 곧 렌더링 순서다. 뒤에 있을수록 위에 그려진다.
    static readonly string[] SORTING_LAYERS = { "Ground", "Wall", "Entity", "FX" };

    // ────────────────────────────────────────────────────────────── 메뉴

    [MenuItem("CG2 Lab/4주차 준비/1. 스프라이트 PPU 64로 통일", priority = 0)]
    public static void UnifyPixelsPerUnit()
    {
        var targets = CollectSpriteImporters();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("PPU 통일",
                "스프라이트를 찾지 못했습니다.\n\n" + string.Join("\n", SEARCH_FOLDERS), "확인");
            return;
        }

        int needChange = 0;
        foreach (var ti in targets) if (!Mathf.Approximately(ti.spritePixelsPerUnit, TARGET_PPU)) needChange++;

        if (needChange == 0)
        {
            Debug.Log($"[CG2 4주차] 스프라이트 {targets.Count}장 모두 이미 PPU {TARGET_PPU}입니다. 변경 없음.");
            return;
        }

        if (!EditorUtility.DisplayDialog("PPU 64로 통일",
                $"스프라이트 {targets.Count}장 중 {needChange}장의 PPU를 {TARGET_PPU}로 바꿉니다.\n\n" +
                "게임 속 캐릭터가 약 1.56배 커지지만, 콜라이더와 크기가 맞아떨어져\n" +
                "히트박스가 오히려 정확해집니다.\n\n" +
                "변경 전 값은 백업되며 '되돌리기' 메뉴로 원복할 수 있습니다.",
                "통일하기", "취소"))
            return;

        var backup = new PpuBackup();
        int changed = 0;

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var ti = targets[i];
                EditorUtility.DisplayProgressBar("PPU 통일", ti.assetPath, (i + 1f) / targets.Count);
                if (Mathf.Approximately(ti.spritePixelsPerUnit, TARGET_PPU)) continue;

                backup.entries.Add(new PpuEntry { path = ti.assetPath, ppu = ti.spritePixelsPerUnit });
                ti.spritePixelsPerUnit = TARGET_PPU;
                ti.SaveAndReimport();
                changed++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        WriteBackup(backup);
        AssetDatabase.Refresh();

        // 요청값이 아니라 되읽은 값을 찍는다. 여기가 다르면 적용 실패다.
        Debug.Log($"[검증] PPU 통일 완료 — {changed}장 변경 / 전체 {targets.Count}장\n" +
                  $"        {VerifyPpuLine()}\n" +
                  $"        백업 → {BACKUP_PATH}");
    }

    [MenuItem("CG2 Lab/4주차 준비/2. Sorting Layer 등록", priority = 1)]
    public static void EnsureSortingLayers()
    {
        // 유니티는 목록에서 "뒤에 있는" 레이어를 위에 그린다.
        //
        // 따라서 Ground·Wall은 Default보다 반드시 앞에 와야 한다.
        // 뒤에 붙이면 타일맵이 Default에 있는 오브젝트를 덮어버리는데,
        // 새로 만든 오브젝트와 프리팹은 전부 Default가 기본값이다.
        // 즉 "레이어 이동 메뉴를 눌러야만 안 깨지는" 상태가 되어 언젠가 사고가 난다.
        //
        // 최종 순서:  Ground → Wall → Default → Entity → FX
        //             (바닥·벽)   (안전망)    (게임 오브젝트)
        // 이러면 마이그레이션을 깜빡해도 캐릭터가 타일맵에 묻히지 않는다.
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("m_SortingLayers");

        // 기존 레이어를 이름·ID 쌍으로 읽어둔다. uniqueID를 보존해야
        // 이미 배치된 렌더러의 참조가 끊어지지 않는다(참조는 인덱스가 아니라 ID로 저장된다).
        var existing = new List<(string name, int id)>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            var e = layers.GetArrayElementAtIndex(i);
            existing.Add((e.FindPropertyRelative("name").stringValue,
                          e.FindPropertyRelative("uniqueID").intValue));
        }
        string before = string.Join(" → ", existing.ConvertAll(e => e.name));

        var ours = new HashSet<string>(SORTING_LAYERS);
        var desired = new List<(string name, int id)>();

        // ① 바닥·벽을 맨 앞에
        foreach (var n in new[] { "Ground", "Wall" }) desired.Add(Resolve(existing, n));
        // ② Default를 비롯해 우리가 만들지 않은 레이어는 원래 순서대로 가운데
        foreach (var e in existing) if (!ours.Contains(e.name)) desired.Add(e);
        // ③ 엔티티·이펙트를 맨 뒤에 (가장 위에 그려진다)
        foreach (var n in new[] { "Entity", "FX" }) desired.Add(Resolve(existing, n));

        layers.arraySize = desired.Count;
        for (int i = 0; i < desired.Count; i++)
        {
            var e = layers.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("name").stringValue = desired[i].name;
            e.FindPropertyRelative("uniqueID").intValue = desired[i].id;
        }

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        // ★ 조명 갱신은 선택이 아니라 필수다.
        // 새로 추가한 Sorting Layer는 기존 Light2D의 대상 목록에 들어가지 않는다.
        // 그 상태로 두면 새 레이어의 스프라이트가 통째로 검게 그려져 화면이 꺼진 것처럼 보인다.
        // (조명이 아예 없으면 멀쩡한데, 있는데 안 비추면 검어지는 것이 함정이다)
        // 목록을 덮어쓰지 않고 빠진 레이어만 더한다. 조명별 의도적인 제외 설정을 보존하기 위함이다.
        int total = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include).Length;
        int fixedCount = CG2Diagnostics.RepairAllLight2DTargetLayers();

        string lightLine = total == 0
            ? "이 씬에는 2D 조명이 없습니다 — 정상 (조명이 없으면 스프라이트가 그냥 보입니다)"
            : fixedCount == 0
                ? $"Light2D {total}개 — 이미 새 레이어를 비추고 있어 변경 없음"
                : $"Light2D {total}개 중 {fixedCount}개에 빠진 레이어를 추가했습니다";

        Debug.Log($"[검증] Sorting Layer 정렬 완료\n" +
                  $"        이전 : {before}\n" +
                  $"        이후 : {string.Join(" → ", CurrentSortingLayerNames())}\n" +
                  "        뒤에 있을수록 위에 그려집니다. Ground·Wall이 Default보다 앞이면 정상입니다.\n" +
                  "        (이 순서면 레이어 이동을 깜빡해도 캐릭터가 타일맵에 묻히지 않습니다)\n" +
                  $"        {lightLine}\n" +
                  "        ※ 다른 씬은 열었을 때 이 메뉴를 다시 실행하세요. 열려 있는 씬만 갱신됩니다.");
    }

    /// <summary>기존 레이어면 uniqueID를 살려 쓰고, 없으면 새로 만든다.</summary>
    static (string, int) Resolve(List<(string name, int id)> existing, string name)
    {
        foreach (var e in existing) if (e.name == name) return e;
        return (name, StableId(name));
    }

    [MenuItem("CG2 Lab/4주차 준비/3. 기존 오브젝트를 Entity·FX 레이어로 이동", priority = 2)]
    public static void MigrateExistingRenderers()
    {
        // 주의: sortingOrder 숫자는 절대 건드리지 않는다. sortingLayerName만 옮긴다.
        // Sorting Layer가 다르면 Order 값과 무관하게 레이어 순서가 우선하므로
        // 2주차의 Player(2) > Enemy(1) 상대 순서가 그대로 유지된다.
        int scene = 0;
        // FindObjectsSortMode 오버로드는 Unity 6.3에서 deprecated 되었다(인스턴스 ID 순서 의존).
        // 여기서는 전부 순회하며 레이어만 바꾸므로 순서가 무의미하다.
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (sr.sortingLayerName != "Default") continue;
            Undo.RecordObject(sr, "CG2 Sorting Layer 이동");
            sr.sortingLayerName = LayerFor(sr.gameObject.tag);
            EditorUtility.SetDirty(sr);
            scene++;
        }

        // 프리팹도 같이 옮긴다. 적과 투사체는 런타임에 생성되므로
        // 씬 오브젝트만 고치면 Play하는 순간 타일맵 아래로 들어간다.
        int prefabs = MigratePrefabs();
        if (prefabs > 0) AssetDatabase.SaveAssets();

        Debug.Log($"[검증] Sorting Layer 이동 — 씬 오브젝트 {scene}개 · 프리팹 {prefabs}개\n" +
                  "        sortingOrder 값은 변경하지 않았습니다 (Player 2 > Enemy 1 순서 유지).");
    }

    /// <summary>태그로 레이어를 정한다. CompareTag는 미등록 태그에서 예외를 던지므로 문자열 비교.</summary>
    static string LayerFor(string tag) => tag == "Projectile" ? "FX" : "Entity";

    static int MigratePrefabs()
    {
        const string dir = "Assets/StarterProject/Prefabs";
        if (!AssetDatabase.IsValidFolder(dir)) return 0;

        int moved = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            bool dirty = false;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sortingLayerName != "Default") continue;
                sr.sortingLayerName = LayerFor(go.tag);
                dirty = true;
            }
            if (!dirty) continue;

            EditorUtility.SetDirty(go);
            moved++;
        }
        return moved;
    }

    [MenuItem("CG2 Lab/4주차 준비/되돌리기 — PPU 원래대로", priority = 40)]
    public static void RestorePixelsPerUnit()
    {
        var backup = ReadBackup();
        if (backup == null || backup.entries.Count == 0)
        {
            EditorUtility.DisplayDialog("PPU 되돌리기", $"백업이 없습니다.\n{BACKUP_PATH}", "확인");
            return;
        }

        int restored = 0;
        try
        {
            for (int i = 0; i < backup.entries.Count; i++)
            {
                var e = backup.entries[i];
                EditorUtility.DisplayProgressBar("PPU 되돌리기", e.path, (i + 1f) / backup.entries.Count);
                if (AssetImporter.GetAtPath(e.path) is not TextureImporter ti) continue;
                ti.spritePixelsPerUnit = e.ppu;
                ti.SaveAndReimport();
                restored++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.DeleteAsset(BACKUP_PATH);
        AssetDatabase.Refresh();
        Debug.Log($"[검증] PPU 되돌리기 완료 — {restored}장 복원, 백업 파일 삭제.");
    }

    [MenuItem("CG2 Lab/4주차 준비/현재 상태 점검", priority = 41)]
    public static void Diagnose()
    {
        Debug.Log("[CG2 4주차] 상태 점검\n" +
                  $"        PPU        : {VerifyPpuLine()}\n" +
                  $"        Sorting    : {string.Join(" → ", CurrentSortingLayerNames())}\n" +
                  $"        Cinemachine: {(HasCinemachine() ? "설치됨" : "미설치 — Package Manager에서 Cinemachine 설치 필요")}\n" +
                  $"        Tilemap Extras: {(HasTilemapExtras() ? "설치됨 (Rule Tile 사용 가능)" : "미설치")}");
    }

    // ────────────────────────────────────────────────────────────── 내부

    static List<TextureImporter> CollectSpriteImporters()
    {
        var result = new List<TextureImporter>();
        var folders = new List<string>();
        foreach (var f in SEARCH_FOLDERS) if (AssetDatabase.IsValidFolder(f)) folders.Add(f);
        if (folders.Count == 0) return result;

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", folders.ToArray()))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                ti.textureType == TextureImporterType.Sprite)
                result.Add(ti);
        }
        return result;
    }

    /// <summary>변경 후 실제 값을 되읽어 요약한다. 요청과 다르면 여기서 드러난다.</summary>
    static string VerifyPpuLine()
    {
        var counts = new Dictionary<int, int>();
        foreach (var ti in CollectSpriteImporters())
        {
            int ppu = Mathf.RoundToInt(ti.spritePixelsPerUnit);
            counts[ppu] = counts.TryGetValue(ppu, out var c) ? c + 1 : 1;
        }
        if (counts.Count == 0) return "스프라이트 없음";

        var parts = new List<string>();
        foreach (var kv in counts) parts.Add($"PPU {kv.Key} × {kv.Value}장");
        return string.Join(" · ", parts) + (counts.Count == 1 && counts.ContainsKey(TARGET_PPU) ? "  ✓ 통일됨" : "  ← 통일 필요");
    }

    static List<string> CurrentSortingLayerNames()
    {
        var names = new List<string>();
        foreach (var l in SortingLayer.layers) names.Add(l.name);
        return names;
    }

    static int StableId(string s)
    {
        // 결정적 해시. string.GetHashCode()는 실행마다 값이 달라질 수 있어 쓰지 않는다.
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h == 0 ? 1 : h & 0x7FFFFFFF;
        }
    }

    static bool HasCinemachine() => PackageInstalled("com.unity.cinemachine");
    static bool HasTilemapExtras() => PackageInstalled("com.unity.2d.tilemap.extras");

    static bool PackageInstalled(string id)
    {
        // manifest.json을 직접 읽는다. PackageManager.Client는 비동기라 메뉴에서 쓰기 불편하다.
        const string manifest = "Packages/manifest.json";
        return System.IO.File.Exists(manifest) && System.IO.File.ReadAllText(manifest).Contains($"\"{id}\"");
    }

    // ────────────────────────────────────────────────────────────── 백업 직렬화

    [System.Serializable] class PpuEntry { public string path; public float ppu; }
    [System.Serializable] class PpuBackup { public List<PpuEntry> entries = new(); }

    static void WriteBackup(PpuBackup backup)
    {
        var dir = System.IO.Path.GetDirectoryName(BACKUP_PATH);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(BACKUP_PATH, JsonUtility.ToJson(backup, true));
        AssetDatabase.ImportAsset(BACKUP_PATH);
    }

    static PpuBackup ReadBackup()
    {
        if (!System.IO.File.Exists(BACKUP_PATH)) return null;
        return JsonUtility.FromJson<PpuBackup>(System.IO.File.ReadAllText(BACKUP_PATH));
    }
}
#endif
