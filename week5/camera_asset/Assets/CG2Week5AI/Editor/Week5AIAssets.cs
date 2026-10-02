using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 5주차 AI 에셋 패키지 — docs/asset_prompt.md 프롬프트로 만든 샘플 이미지 5장(캐릭터 3 + 아이콘 시트 2)을
/// 아트 스튜디오와 같은 파이프라인으로 가공해 게임에 교체 적용하고, 적용 전 상태로 되돌린다.
///
///   AI 에셋 추가     Raw/ 원본 5장 → MyArt/{Characters|Icons}/ai_*.png 11종 가공 → Player·Enemy·Projectile·아이콘 세트 교체
///   기존 에셋으로 복구  추가 직전에 기록해 둔 스프라이트로 되돌린다. 기록이 없으면 4단계 되돌리기(플레이스홀더)로 대신한다.
///
/// 원본 이미지에서 미리 정리한 것 (docs/SAMPLE의 생성 원본과 다른 점) —
///   · 오른쪽 아래 생성 도구 워터마크(✦)를 배경색으로 덮었다. 남겨 두면 내용물 경계가 넓어져 캐릭터가 작아진다.
///   · player.png 아래쪽 130px의 어두운 바닥띠를 배경색으로 덮었다. 남겨 두면 바닥색이 배경 대표색으로 뽑혀
///     같은 계열인 캐릭터의 검은 외곽선까지 지워진다(프롬프트 규칙 1 "no floor" 위반 사례).
/// </summary>
public static class Week5AIAssets
{
    const string ROOT = "Assets/CG2Week5AI";
    const string RAW = ROOT + "/Raw";
    const string BACKUP_DIR = ROOT + "/Backup";
    const string BACKUP = BACKUP_DIR + "/week5_before_ai.json";
    const string MY_ART = "Assets/CG2Week5/MyArt";
    const string ICON_SET = "Assets/CG2Week5/Art/CG2IconSet.asset";
    const string ICON_DIR = "Assets/CG2Week5/Art/Placeholder";
    const string ATLAS_DIR = "Assets/CG2Week5/Atlas";
    const string ENEMY = "Assets/StarterProject/Prefabs/Enemy.prefab";
    const string PROJECTILE = "Assets/StarterProject/Prefabs/Projectile.prefab";
    const string PLAYER = "player", ENEMY_SLOT = "enemy", PROJECTILE_SLOT = "projectile";

    // 아트 스튜디오 기본값과 같다. 배경이 #FF00FF 단색이라 전역 제거(크로마키)를 켠다.
    const float TOLERANCE = 0.12f;
    const bool GLOBAL = true;
    static readonly Color OUTLINE = new(0.08f, 0.07f, 0.1f, 1f);

    readonly struct Job
    {
        public readonly string raw, outName, slot;
        public readonly int px, cols, rows, cell;
        public readonly bool icon;
        public Job(string raw, string outName, string slot, int px, bool icon, int cols = 1, int rows = 1, int cell = 0)
        {
            this.raw = raw; this.outName = outName; this.slot = slot; this.px = px; this.icon = icon;
            this.cols = cols; this.rows = rows; this.cell = cell;
        }
    }

    // 칸 번호 0 = 왼쪽 위, 1 = 오른쪽 위, 2 = 왼쪽 아래, 3 = 오른쪽 아래 (asset_prompt.md §6)
    static readonly Job[] Jobs =
    {
        new("ai_player_knight", "ai_player_knight", PLAYER, 64, false),
        new("ai_enemy_slime", "ai_enemy_slime", ENEMY_SLOT, 48, false),
        new("ai_projectile_orb", "ai_projectile_orb", PROJECTILE_SLOT, 24, false),
        new("ai_icon_sheet_a", "ai_icon_hp", CG2IconSet.HP, 32, true, 2, 2, 0),
        new("ai_icon_sheet_a", "ai_icon_exp", CG2IconSet.EXP, 32, true, 2, 2, 1),
        new("ai_icon_sheet_a", "ai_icon_coin", CG2IconSet.COIN, 32, true, 2, 2, 2),
        new("ai_icon_sheet_a", "ai_icon_damage", CG2IconSet.DAMAGE, 32, true, 2, 2, 3),
        new("ai_icon_sheet_b", "ai_icon_firerate", CG2IconSet.FIRE_RATE, 32, true, 2, 2, 0),
        new("ai_icon_sheet_b", "ai_icon_range", CG2IconSet.RANGE, 32, true, 2, 2, 1),
        new("ai_icon_sheet_b", "ai_icon_heal", CG2IconSet.HEAL, 32, true, 2, 2, 2),
        new("ai_icon_sheet_b", "ai_icon_speed", CG2IconSet.SPEED, 32, true, 2, 2, 3),
    };

    [System.Serializable]
    class Slot { public string key; public string sprite; }

    [System.Serializable]
    class Backup
    {
        public string savedAt;
        public string scene;
        public List<Slot> slots = new();

        public string Get(string key)
        {
            foreach (var s in slots) if (s.key == key) return s.sprite;
            return null;
        }
    }

    static bool Blocked(string title)
    {
        if (!EditorApplication.isPlaying) return false;
        EditorUtility.DisplayDialog(title, "Play를 멈춘 뒤 실행하세요.", "확인");
        return true;
    }

    // ────────────────────────────────────────────────────────── 추가

    [MenuItem("CG2 Lab/5주차/AI 에셋 추가 — 샘플 11종 가공·교체", priority = 45)]
    static void AddMenu()
    {
        const string title = "AI 에셋 추가";
        if (Blocked(title)) return;

        var missing = MissingRaws();
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog(title, $"원본 이미지가 없습니다.\n{RAW}/\n  " + string.Join(".png\n  ", missing) + ".png", "확인");
            return;
        }

        bool hadBackup = File.Exists(BACKUP);
        if (!EditorUtility.DisplayDialog(title,
                "Player · Enemy · Projectile 스프라이트와 UI 아이콘 8종을 AI 샘플 에셋으로 교체합니다.\n\n" +
                (hadBackup
                    ? "이미 한 번 추가한 적이 있어 처음 기록한 '기존 에셋'을 그대로 유지합니다."
                    : "지금 상태를 '기존 에셋'으로 기록해 두며, '기존 에셋으로 복구'로 되돌릴 수 있습니다.") +
                $"\n\n가공 결과는 {MY_ART}/ 에 ai_*.png로 저장됩니다.",
                "추가", "취소"))
            return;

        Add();
    }

    /// <summary>대화상자 없이 추가한다. 메뉴와 자동화(MCP · 테스트)가 같이 쓴다.</summary>
    public static bool Add()
    {
        const string title = "AI 에셋 추가";
        if (EditorApplication.isPlaying) { Debug.LogWarning("[CG2] Play를 멈춘 뒤 실행하세요."); return false; }
        var missing = MissingRaws();
        if (missing.Count > 0) { Debug.LogError($"[CG2] 원본 이미지가 없습니다: {RAW}/{string.Join(", ", missing)}"); return false; }
        bool hadBackup = File.Exists(BACKUP);

        // 1) 기존 상태 기록 — 아이콘 세트를 만들기 전에 찍어야 '세트가 없던 상태'도 기록된다.
        if (!hadBackup) SaveBackup();

        // 2) 아이콘 세트가 없으면(3단계 UI 구성 전) 플레이스홀더 세트를 먼저 만든다. 3단계를 나중에 실행해도 교체한 아이콘은 유지된다.
        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        bool createdSet = set == null;
        if (createdSet) set = UIBuilder.EnsureIconSet();

        // 3) 가공 — 아트 스튜디오와 같은 파이프라인
        var made = new Dictionary<string, Sprite>();
        var reports = new List<string>();
        try
        {
            for (int i = 0; i < Jobs.Length; i++)
            {
                var j = Jobs[i];
                EditorUtility.DisplayProgressBar(title, $"{j.outName} ({j.px}px) 가공 중…", i / (float)Jobs.Length);
                var s = ArtStudioWindow.ProcessToAsset(RawPath(j.raw), j.px, j.icon, j.cols, j.rows, j.cell,
                                                       TOLERANCE, GLOBAL, OUTLINE, j.outName, out _);
                if (s == null) { reports.Add($"⚠ {j.outName} 가공 실패"); continue; }
                made[j.slot] = s;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // 4) 교체
        reports.Add(SetPlayer(made.GetValueOrDefault(PLAYER)));
        reports.Add(SetPrefab(ENEMY, "Enemy", made.GetValueOrDefault(ENEMY_SLOT)));
        reports.Add(SetPrefab(PROJECTILE, "Projectile", made.GetValueOrDefault(PROJECTILE_SLOT)));
        var icons = new Dictionary<string, Sprite>();
        foreach (var key in CG2IconSet.Keys) icons[key] = made.GetValueOrDefault(key);
        reports.Add(SetIcons(set, icons) + (createdSet ? " (3단계 전이라 아이콘 세트를 새로 만듦)" : ""));
        reports.Add(RebuildAtlasIfAny());

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[검증] 5주차 AI 에셋 추가 완료\n        " + string.Join("\n        ", reports.FindAll(r => r != null)) +
                  $"\n        기존 에셋 기록 : {BACKUP}{(hadBackup ? " (처음 기록 유지)" : " (새로 기록)")}" +
                  "\n        ── 씬을 저장(Ctrl+S)하고 Play 해서 확인 ── ① 캐릭터가 콜라이더와 맞는가 ② 바닥 위에서 잘 보이는가 ③ 가장자리에 보라 띠가 없는가");
        return true;
    }

    static List<string> MissingRaws()
    {
        var missing = new List<string>();
        foreach (var j in Jobs)
            if (!File.Exists(RawPath(j.raw)) && !missing.Contains(j.raw)) missing.Add(j.raw);
        return missing;
    }

    // ────────────────────────────────────────────────────────── 복구

    [MenuItem("CG2 Lab/5주차/AI 에셋 → 기존 에셋으로 복구", priority = 46)]
    static void RestoreMenu()
    {
        const string title = "기존 에셋으로 복구";
        if (Blocked(title)) return;

        if (!File.Exists(BACKUP))
        {
            if (EditorUtility.DisplayDialog(title,
                    "AI 에셋을 추가하기 전 기록이 없습니다.\nStarter 플레이스홀더로 되돌리는 '되돌리기 — 4단계 에셋 교체'를 실행할까요?",
                    "실행", "취소"))
                EditorApplication.ExecuteMenuItem("CG2 Lab/5주차/되돌리기 — 4단계 에셋 교체");
            return;
        }

        var b = JsonUtility.FromJson<Backup>(File.ReadAllText(BACKUP));
        int choice = EditorUtility.DisplayDialogComplex(title,
            $"AI 에셋을 추가하기 전({b.savedAt}) 스프라이트로 되돌립니다.\n\n" +
            $"가공한 ai_*.png 파일({MY_ART})도 지울까요?",
            "복구 (파일 유지)", "취소", "복구 + ai 파일 삭제");
        if (choice == 1) return;

        Restore(choice == 2);
    }

    /// <summary>대화상자 없이 복구한다. 기록이 없으면 false.</summary>
    public static bool Restore(bool deleteFiles)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[CG2] Play를 멈춘 뒤 실행하세요."); return false; }
        if (!File.Exists(BACKUP)) { Debug.LogWarning($"[CG2] 복구 기록이 없습니다 ({BACKUP})."); return false; }
        var b = JsonUtility.FromJson<Backup>(File.ReadAllText(BACKUP));

        var lines = new List<string>();
        if (!string.IsNullOrEmpty(b.scene) && b.scene != EditorSceneManager.GetActiveScene().path)
            lines.Add($"⚠ 기록한 씬({b.scene})과 지금 씬이 다릅니다. Player는 지금 씬 기준으로 복구합니다.");

        lines.Add(SetPlayer(LoadRef(b.Get(PLAYER))));
        lines.Add(SetPrefab(ENEMY, "Enemy", LoadRef(b.Get(ENEMY_SLOT))));
        lines.Add(SetPrefab(PROJECTILE, "Projectile", LoadRef(b.Get(PROJECTILE_SLOT))));

        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        if (set != null)
        {
            // 추가 전에 아이콘 세트가 없었으면 기록이 비어 있다 → 플레이스홀더로 채운다.
            var icons = new Dictionary<string, Sprite>();
            foreach (var key in CG2IconSet.Keys)
                icons[key] = LoadRef(b.Get(key)) ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ICON_DIR}/icon_{key}.png");
            lines.Add(SetIcons(set, icons));
        }

        if (deleteFiles)
        {
            int deleted = 0;
            foreach (var j in Jobs)
                if (AssetDatabase.DeleteAsset($"{MY_ART}/{(j.icon ? "Icons" : "Characters")}/{j.outName}.png")) deleted++;
            lines.Add($"가공 파일 {deleted}개 삭제 (MyArt/ai_*.png)");
        }

        // 기록은 복구에 한 번 쓰고 지운다. 다음 'AI 에셋 추가' 때 그 시점 상태를 새로 기록한다.
        AssetDatabase.DeleteAsset(BACKUP);
        lines.Add(RebuildAtlasIfAny());

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[검증] 기존 에셋으로 복구 완료\n        " + string.Join("\n        ", lines.FindAll(l => l != null)) +
                  "\n        씬을 저장(Ctrl+S)하면 복구 상태가 유지됩니다.");
        return true;
    }

    // ────────────────────────────────────────────────────────── 기록

    static void SaveBackup()
    {
        var b = new Backup
        {
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            scene = EditorSceneManager.GetActiveScene().path,
        };
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent<SpriteRenderer>(out var sr)) Add(b, PLAYER, sr.sprite);
        Add(b, ENEMY_SLOT, PrefabSprite(ENEMY));
        Add(b, PROJECTILE_SLOT, PrefabSprite(PROJECTILE));
        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        if (set != null) foreach (var key in CG2IconSet.Keys) Add(b, key, set.Get(key));

        Directory.CreateDirectory(BACKUP_DIR);
        File.WriteAllText(BACKUP, JsonUtility.ToJson(b, true));
        AssetDatabase.ImportAsset(BACKUP);
    }

    static void Add(Backup b, string key, Sprite s)
    {
        string r = Ref(s);
        if (r != null) b.slots.Add(new Slot { key = key, sprite = r });
    }

    /// <summary>스프라이트를 "GUID:로컬ID"로 기록한다. 시트를 잘라 만든 스프라이트(서브 에셋)도 정확히 되찾는다.</summary>
    static string Ref(Sprite s) =>
        s != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id) ? $"{guid}:{id}" : null;

    static Sprite LoadRef(string r)
    {
        if (string.IsNullOrEmpty(r)) return null;
        var parts = r.Split(':');
        string path = AssetDatabase.GUIDToAssetPath(parts[0]);
        if (string.IsNullOrEmpty(path) || parts.Length < 2 || !long.TryParse(parts[1], out long id)) return null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out _, out long sid) && sid == id) return s;
        return null;
    }

    // ────────────────────────────────────────────────────────── 교체

    static string SetPlayer(Sprite s)
    {
        if (s == null) return "Player     : ⚠ 스프라이트 없음 — 건너뜀";
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null || !p.TryGetComponent<SpriteRenderer>(out var sr)) return "Player     : ⚠ 씬에 Player 태그 오브젝트(SpriteRenderer)가 없음";
        Undo.RecordObject(sr, "Player sprite");
        sr.sprite = s;
        return Line("Player", s, p.GetComponent<CircleCollider2D>());
    }

    static string SetPrefab(string path, string label, Sprite s)
    {
        if (s == null) return $"{label,-10} : ⚠ 스프라이트 없음 — 건너뜀";
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return $"{label,-10} : ⚠ 프리팹 없음 ({path})";
        string line;
        if (root.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.sprite = s;
            line = Line(label, s, root.GetComponent<CircleCollider2D>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        else line = $"{label,-10} : ⚠ SpriteRenderer 없음";
        PrefabUtility.UnloadPrefabContents(root);
        return line;
    }

    static string SetIcons(CG2IconSet set, Dictionary<string, Sprite> icons)
    {
        if (set == null) return "UI 아이콘  : ⚠ 아이콘 세트 없음";
        Undo.RecordObject(set, "Icon set");
        var names = new List<string>();
        foreach (var kv in icons)
            if (kv.Value != null) { set.Set(kv.Key, kv.Value); names.Add(kv.Value.name); }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        foreach (var ic in Object.FindObjectsByType<CG2UIIcon>(FindObjectsInactive.Include)) ic.Refresh();
        foreach (var shop in Object.FindObjectsByType<ShopPanel>(FindObjectsInactive.Include)) EditorUtility.SetDirty(shop);
        return $"UI 아이콘  : {names.Count}종 → {string.Join(", ", names)}";
    }

    /// <summary>아틀라스는 '그 시점의' 스프라이트로 만들어진다. 이미 있으면 새 스프라이트로 다시 묶는다.</summary>
    static string RebuildAtlasIfAny()
    {
        if (!AssetDatabase.IsValidFolder(ATLAS_DIR) || AssetDatabase.FindAssets("t:SpriteAtlas", new[] { ATLAS_DIR }).Length == 0) return null;
        Week5AtlasAndReview.BuildAtlases();
        return "Sprite Atlas : 기존 아틀라스가 있어 새 스프라이트로 다시 묶음";
    }

    // ────────────────────────────────────────────────────────── 헬퍼

    static string RawPath(string name) => $"{RAW}/{name}.png";

    static Sprite PrefabSprite(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return go != null && go.TryGetComponent<SpriteRenderer>(out var sr) ? sr.sprite : null;
    }

    static string Line(string label, Sprite s, CircleCollider2D col)
    {
        var size = s.bounds.size;
        string colTxt = col == null ? "" :
            $" · 콜라이더 지름 {col.radius * 2f:F2}" + (Mathf.Abs(size.x - col.radius * 2f) > 0.35f ? " ⚠ 크기 차이 큼" : " ✓");
        return $"{label,-10} : {s.name} {s.rect.width}×{s.rect.height}px = {size.x:F2}×{size.y:F2}유닛{colTxt}";
    }
}

/// <summary>
/// Raw/ 원본은 가공 도구가 파일을 직접 읽기 때문에 게임에서 쓰이지 않는다.
/// 4주차 자동 규격(Sprite · PPU 64)이 붙지 않도록 일반 텍스처 · 작은 미리보기 크기로 임포트한다.
/// </summary>
public class Week5AIRawImport : AssetPostprocessor
{
    public override int GetPostprocessOrder() => 100;   // 4주차 CG2SpriteImportDefaults 뒤에 실행

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/CG2Week5AI/Raw/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 256;
    }
}
