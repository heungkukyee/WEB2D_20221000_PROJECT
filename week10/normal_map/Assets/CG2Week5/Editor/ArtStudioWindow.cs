using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 5주차 4단계 — 생성형 AI 이미지를 게임 규격 스프라이트로 가공하고, 게임에 교체 적용한다.
///
/// 생성형 AI 이미지가 그대로 쓸 수 없는 이유 네 가지와 이 도구의 대응 —
///   1) 알파가 없다     → 배경 제거: 이미지 테두리에서 시작해 배경색과 비슷한 픽셀만 지운다(flood fill).
///                          캐릭터 안쪽의 흰 눈동자처럼 테두리와 이어지지 않은 흰색은 지워지지 않는다.
///                          체크무늬 "가짜 투명" 배경도 테두리의 대표색 2개를 뽑아 함께 지운다.
///   2) 너무 크다       → 1024px 그대로 PPU 64로 쓰면 16유닛(화면 높이보다 크다). 목표 픽셀 크기로 줄인다.
///   3) 여백이 제멋대로 → 내용물 경계로 잘라낸 뒤 정사각형 캔버스 가운데에 다시 앉힌다.
///   4) 가장자리 반투명 → 알파를 0/1로 이진화한다. 반투명 테두리는 Point 필터에서 지저분한 띠(halo)가 된다.
///
/// 원본은 건드리지 않는다. 결과는 Assets/CG2Week5/MyArt/{Characters|Icons}/ 에 새 PNG로 저장된다.
/// </summary>
public class ArtStudioWindow : EditorWindow
{
    const string OUT_ROOT = "Assets/CG2Week5/MyArt";
    const string ICON_SET = "Assets/CG2Week5/Art/CG2IconSet.asset";

    enum Target { Player = 64, Enemy = 48, Projectile = 24, Icon = 32 }

    // ── 가공 ──
    Texture2D _source;
    Target _target = Target.Player;
    int _gridCols = 1, _gridRows = 1, _cell;
    float _tolerance = 0.12f;
    bool _global;
    bool _outline = true;
    Color _outlineColor = new(0.08f, 0.07f, 0.1f, 1f);
    string _outName = "";
    string _report = "";

    // ── 교체 ──
    Sprite _player, _enemy, _projectile;
    readonly Dictionary<string, Sprite> _icons = new();
    Vector2 _scroll;

    // Sprite 칸은 기본으로 64px 미리보기가 그려진다. 한 줄 높이로 줄여 11칸이 한 화면에 들어오게 한다.
    static readonly GUILayoutOption OneLine = GUILayout.Height(18);

    [MenuItem("CG2 Lab/5주차/4. 아트 스튜디오 — AI 이미지 가공·교체", priority = 40)]
    static void Open() => GetWindow<ArtStudioWindow>("CG2 Art Studio").minSize = new Vector2(420, 560);

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("① 가공 — AI 이미지 → 게임 규격 스프라이트", EditorStyles.boldLabel);
        _source = (Texture2D)EditorGUILayout.ObjectField("원본 이미지", _source, typeof(Texture2D), false);
        _target = (Target)EditorGUILayout.EnumPopup(new GUIContent("용도 (목표 픽셀)", "Player 64 · Enemy 48 · Projectile 24 · Icon 32"), _target);
        using (new EditorGUILayout.HorizontalScope())
        {
            _gridCols = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("시트 분할 열×행", "AI가 한 장에 아이콘 여러 개를 그렸을 때"), _gridCols), 1, 8);
            _gridRows = Mathf.Clamp(EditorGUILayout.IntField(_gridRows, GUILayout.Width(40)), 1, 8);
        }
        if (_gridCols * _gridRows > 1)
            _cell = EditorGUILayout.IntSlider("가공할 칸 (0부터)", _cell, 0, _gridCols * _gridRows - 1);
        _tolerance = EditorGUILayout.Slider(new GUIContent("배경 허용 오차", "배경이 덜 지워지면 올리고, 캐릭터가 깎이면 내린다"), _tolerance, 0.02f, 0.4f);
        _global = EditorGUILayout.Toggle(new GUIContent("전역 제거 (크로마키 배경)",
            "끔: 테두리와 이어진 배경만 지운다 — 눈 흰자처럼 안쪽의 배경색은 남는다.\n" +
            "켬: 배경색과 같은 픽셀을 모두 지운다 — 다리 사이처럼 갇힌 배경도 지워진다.\n" +
            "배경을 #FF00FF 같은 캐릭터에 없는 색으로 생성했을 때만 켠다."), _global);
        _outline = EditorGUILayout.Toggle(new GUIContent("1px 외곽선", "어두운 타일맵 위에서 실루엣이 묻히지 않게 한다"), _outline);
        if (_outline) _outlineColor = EditorGUILayout.ColorField("외곽선 색", _outlineColor);
        _outName = EditorGUILayout.TextField(new GUIContent("저장 이름", "비우면 원본 이름 + 용도"), _outName);

        using (new EditorGUI.DisabledScope(_source == null))
            if (GUILayout.Button("가공해서 저장", GUILayout.Height(28))) Process();

        if (!string.IsNullOrEmpty(_report)) EditorGUILayout.HelpBox(_report, MessageType.Info);

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("② 교체 — 게임에 적용", EditorStyles.boldLabel);
        _player = (Sprite)EditorGUILayout.ObjectField("Player", _player, typeof(Sprite), false, OneLine);
        _enemy = (Sprite)EditorGUILayout.ObjectField("Enemy (프리팹)", _enemy, typeof(Sprite), false, OneLine);
        _projectile = (Sprite)EditorGUILayout.ObjectField("Projectile (프리팹)", _projectile, typeof(Sprite), false, OneLine);
        EditorGUILayout.LabelField("UI 아이콘 (비워 두면 기존 아이콘 유지)", EditorStyles.miniBoldLabel);
        foreach (var key in CG2IconSet.Keys)
        {
            _icons.TryGetValue(key, out var s);
            _icons[key] = (Sprite)EditorGUILayout.ObjectField(key, s, typeof(Sprite), false, OneLine);
        }
        if (GUILayout.Button("폴더에서 자동 채우기 (MyArt 파일명 규칙)")) AutoFill();
        if (GUILayout.Button("교체 적용", GUILayout.Height(28))) Apply();

        EditorGUILayout.EndScrollView();
    }

    // ────────────────────────────────────────────────────────── 가공

    void Process()
    {
        string srcPath = AssetDatabase.GetAssetPath(_source);
        string name = string.IsNullOrWhiteSpace(_outName)
            ? $"{Path.GetFileNameWithoutExtension(srcPath)}_{_target.ToString().ToLower()}{(_gridCols * _gridRows > 1 ? "_" + _cell : "")}"
            : _outName.Trim();
        var sprite = ProcessToAsset(srcPath, (int)_target, _target == Target.Icon, _gridCols, _gridRows, _cell,
                                    _tolerance, _global, _outline ? _outlineColor : (Color?)null, name, out _report);
        EditorGUIUtility.PingObject(sprite);
        if (_target == Target.Player) _player = sprite;
        else if (_target == Target.Enemy) _enemy = sprite;
        else if (_target == Target.Projectile) _projectile = sprite;
    }

    /// <summary>원본 파일을 읽어 가공하고 MyArt 폴더에 스프라이트로 저장한다. 창 없이도 호출할 수 있다.</summary>
    public static Sprite ProcessToAsset(string srcPath, int targetPx, bool isIcon, int cols, int rows, int cell,
                                        float tolerance, bool global, Color? outline, string outName, out string report)
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        // 임포트 설정(Read/Write, 압축, 최대 크기)과 무관하게 원본 파일을 직접 읽는다.
        src.LoadImage(File.ReadAllBytes(srcPath));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = Pipeline(src, targetPx, cols, rows, cell, tolerance, global, outline, out var info);
        Object.DestroyImmediate(src);

        string folder = $"{OUT_ROOT}/{(isIcon ? "Icons" : "Characters")}";
        Directory.CreateDirectory(folder);
        string outPath = $"{folder}/{outName}.png";
        File.WriteAllBytes(outPath, result.EncodeToPNG());
        Object.DestroyImmediate(result);

        AssetDatabase.ImportAsset(outPath);
        ArtImport.ApplyPixelSprite(outPath, isIcon ? ArtImport.Kind.Icon : ArtImport.Kind.Character);

        report = $"저장: {outPath}\n{info}\n처리 {sw.ElapsedMilliseconds}ms";
        Debug.Log($"[검증] 아트 가공 완료 — {outPath}\n        {info.Replace("\n", "\n        ")}");
        return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
    }

    /// <summary>가공 파이프라인 본체. 메뉴 없이 테스트할 수 있게 static으로 둔다.</summary>
    public static Texture2D Pipeline(Texture2D src, int targetPx, int cols, int rows, int cell,
                                     float tolerance, bool global, Color? outline, out string info)
    {
        // 0) 시트 분할 — 해당 칸만 잘라낸다
        int cw = src.width / cols, ch = src.height / rows;
        int cx = cell % cols, cy = rows - 1 - cell / cols;           // 0번 칸 = 왼쪽 위
        var px = src.GetPixels(cx * cw, cy * ch, cw, ch);
        int w = cw, h = ch;

        // 1) 배경 제거 — 테두리 대표색 최대 2개 + flood fill
        var bgColors = BorderColors(px, w, h);
        int removed = global ? GlobalRemove(px, bgColors, tolerance) : FloodRemove(px, w, h, bgColors, tolerance);
        bool hadAlpha = bgColors.Count == 0;

        // 1-2) 디프린지 — AI 이미지의 경계는 배경색과 섞인 반투명 색이다(마젠타 배경이면 보라색 띠).
        //      원본 해상도에서 경계를 몇 px 깎아 낸다. 1024→64 축소에서 3px은 0.2px도 안 되므로 형태는 그대로다.
        int fringe = hadAlpha ? 0 : Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(w, h) / (float)targetPx * 0.2f), 1, 4);
        for (int i = 0; i < fringe; i++) Erode(px, w, h);

        // 2) 내용물 경계로 트림
        if (!OpaqueBounds(px, w, h, out var r))
        {
            info = "⚠ 남은 픽셀이 없습니다. 배경 허용 오차를 내리세요.";
            return new Texture2D(targetPx, targetPx, TextureFormat.RGBA32, false);
        }

        // 3) 목표 크기로 축소 — 긴 변을 (목표 - 외곽선 여유)에 맞춘다. 영역 평균(Box) 필터.
        int margin = outline.HasValue ? 1 : 0;
        int inner = targetPx - margin * 2;
        float scale = Mathf.Min((float)inner / r.width, (float)inner / r.height);
        int dw = Mathf.Max(1, Mathf.RoundToInt(r.width * scale)), dh = Mathf.Max(1, Mathf.RoundToInt(r.height * scale));
        var small = BoxDownscale(px, w, r, dw, dh);

        // 4) 정사각형 캔버스에 배치 — 가로 가운데, 세로는 바닥에 붙인다(발이 캔버스 바닥에 닿게)
        var outPx = new Color[targetPx * targetPx];
        int ox = (targetPx - dw) / 2, oy = margin;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                var c = small[y * dw + x];
                if (c.a < 0.5f) continue;                              // 알파 이진화 — 반투명 테두리(halo) 제거
                // 축소 결과는 알파를 곱한 상태라 알파로 나눠 원래 색을 되살린다.
                outPx[(oy + y) * targetPx + ox + x] = Clamp01(new Color(c.r / c.a, c.g / c.a, c.b / c.a, 1f));
            }

        // 5) 1px 외곽선
        int outlineCount = 0;
        if (outline.HasValue) outlineCount = AddOutline(outPx, targetPx, outline.Value);

        var tex = new Texture2D(targetPx, targetPx, TextureFormat.RGBA32, false);
        tex.SetPixels(outPx);
        tex.Apply();

        info = $"원본 {src.width}×{src.height}" + (cols * rows > 1 ? $" → {cols}×{rows} 시트 중 {cell}번 칸 {w}×{h}" : "") +
               (hadAlpha ? " · 원본에 이미 알파 있음" : $" · 배경 {removed:N0}px 제거 ({(global ? "전역" : "테두리 연결")} · 대표색 {bgColors.Count}개 · 경계 {fringe}px 디프린지)") +
               $"\n내용물 {r.width}×{r.height} → {dw}×{dh}px (×{scale:F3}) · 캔버스 {targetPx}×{targetPx}" +
               (outline.HasValue ? $" · 외곽선 {outlineCount}px" : "") +
               $"\n월드 크기 {targetPx / (float)ArtImport.PPU:F2}유닛 · Size 7·1080p에서 화면 {targetPx * 1080f / 14f / ArtImport.PPU:F0}px";
        return tex;
    }

    static Color Clamp01(Color c) => new(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), c.a);

    /// <summary>
    /// 테두리 픽셀에서 배경 대표색을 뽑는다. 이미 투명한 테두리면(알파 있는 PNG) 빈 목록을 돌려준다.
    /// 체크무늬 가짜 투명 배경은 두 색이 번갈아 나오므로 최대 2개까지 뽑는다.
    /// </summary>
    static List<Color> BorderColors(Color[] px, int w, int h)
    {
        var buckets = new Dictionary<int, (Color sum, int n)>();
        int transparent = 0, total = 0;
        void Add(Color c)
        {
            total++;
            if (c.a < 0.5f) { transparent++; return; }
            int key = ((int)(c.r * 15) << 8) | ((int)(c.g * 15) << 4) | (int)(c.b * 15);   // 4bit 양자화
            buckets.TryGetValue(key, out var b);
            buckets[key] = (b.sum + c, b.n + 1);
        }
        for (int x = 0; x < w; x++) { Add(px[x]); Add(px[(h - 1) * w + x]); }
        for (int y = 0; y < h; y++) { Add(px[y * w]); Add(px[y * w + w - 1]); }

        var list = new List<Color>();
        if (transparent > total / 2) return list;

        var sorted = new List<(Color sum, int n)>(buckets.Values);
        sorted.Sort((a, b) => b.n.CompareTo(a.n));
        for (int i = 0; i < sorted.Count && i < 2; i++)
            if (i == 0 || sorted[i].n > total * 0.15f)                     // 두 번째 색은 충분히 많을 때만(체크무늬)
                list.Add(sorted[i].sum / sorted[i].n);
        return list;
    }

    static bool IsBg(Color c, List<Color> bg, float tol)
    {
        if (c.a < 0.5f) return true;
        foreach (var b in bg)
            if (Mathf.Abs(c.r - b.r) + Mathf.Abs(c.g - b.g) + Mathf.Abs(c.b - b.b) < tol * 3f) return true;
        return false;
    }

    /// <summary>크로마키 방식 — 연결 여부와 상관없이 배경색과 비슷한 픽셀을 모두 지운다.</summary>
    static int GlobalRemove(Color[] px, List<Color> bg, float tol)
    {
        if (bg.Count == 0) return 0;
        int removed = 0;
        for (int i = 0; i < px.Length; i++)
            if (IsBg(px[i], bg, tol)) { px[i] = Color.clear; removed++; }
        return removed;
    }

    static int FloodRemove(Color[] px, int w, int h, List<Color> bg, float tol)
    {
        if (bg.Count == 0) return 0;
        var seen = new bool[px.Length];
        var q = new Queue<int>();
        void Seed(int i) { if (!seen[i] && IsBg(px[i], bg, tol)) { seen[i] = true; q.Enqueue(i); } }
        for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }

        int removed = 0;
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            px[i] = Color.clear;
            removed++;
            int x = i % w, y = i / w;
            if (x > 0) Seed(i - 1);
            if (x < w - 1) Seed(i + 1);
            if (y > 0) Seed(i - w);
            if (y < h - 1) Seed(i + w);
        }
        return removed;
    }

    /// <summary>투명 픽셀과 맞닿은 불투명 픽셀을 한 겹 지운다.</summary>
    static void Erode(Color[] px, int w, int h)
    {
        var kill = new List<int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (px[i].a < 0.5f) continue;
                if ((x > 0 && px[i - 1].a < 0.5f) || (x < w - 1 && px[i + 1].a < 0.5f) ||
                    (y > 0 && px[i - w].a < 0.5f) || (y < h - 1 && px[i + w].a < 0.5f))
                    kill.Add(i);
            }
        foreach (var i in kill) px[i] = Color.clear;
    }

    static bool OpaqueBounds(Color[] px, int w, int h, out RectInt r)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a >= 0.5f)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        r = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return maxX >= 0;
    }

    /// <summary>
    /// 영역 평균 축소. 최근접(Nearest)으로 1024→64를 하면 16픽셀 중 1개만 골라 선이 끊기고 점이 튄다.
    /// 알파 가중 평균이라 투명 픽셀의 색(보통 검정)이 가장자리로 번지지 않는다.
    /// </summary>
    static Color[] BoxDownscale(Color[] px, int w, RectInt r, int dw, int dh)
    {
        var o = new Color[dw * dh];
        float sx = (float)r.width / dw, sy = (float)r.height / dh;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int x0 = r.x + Mathf.FloorToInt(x * sx), x1 = r.x + Mathf.Max(Mathf.FloorToInt((x + 1) * sx), Mathf.FloorToInt(x * sx) + 1);
                int y0 = r.y + Mathf.FloorToInt(y * sy), y1 = r.y + Mathf.Max(Mathf.FloorToInt((y + 1) * sy), Mathf.FloorToInt(y * sy) + 1);
                float R = 0, G = 0, B = 0, A = 0; int n = 0;
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++)
                    {
                        var c = px[yy * w + xx];
                        R += c.r * c.a; G += c.g * c.a; B += c.b * c.a; A += c.a; n++;
                    }
                o[y * dw + x] = n == 0 ? Color.clear : new Color(R / n, G / n, B / n, A / n);   // 프리멀티플라이 상태
            }
        return o;
    }

    static int AddOutline(Color[] px, int s, Color c)
    {
        var add = new List<int>();
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (px[y * s + x].a > 0f) continue;
                bool near = (x > 0 && px[y * s + x - 1].a > 0f) || (x < s - 1 && px[y * s + x + 1].a > 0f) ||
                            (y > 0 && px[(y - 1) * s + x].a > 0f) || (y < s - 1 && px[(y + 1) * s + x].a > 0f);
                if (near) add.Add(y * s + x);
            }
        foreach (var i in add) px[i] = c;
        return add.Count;
    }

    // ────────────────────────────────────────────────────────── 교체

    /// <summary>
    /// MyArt 폴더의 파일명 규칙으로 칸을 채운다.
    ///   Characters/…player…  Characters/…enemy… (또는 slime·monster)  Characters/…projectile… (또는 bullet)
    ///   Icons/…{key}…        예: ai_icons_hp.png, heal_icon.png
    /// </summary>
    void AutoFill()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { OUT_ROOT }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(path).ToLower();
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (path.Contains("/Characters/"))
            {
                if (n.Contains("player")) _player = s;
                else if (n.Contains("enemy") || n.Contains("monster") || n.Contains("slime")) _enemy = s;
                else if (n.Contains("projectile") || n.Contains("bullet")) _projectile = s;
            }
            else if (path.Contains("/Icons/"))
                foreach (var key in CG2IconSet.Keys)
                    if (n.Contains(key)) _icons[key] = s;
        }
        Repaint();
    }

    void Apply()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("교체 적용", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        var log = new List<string>();

        if (_player != null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null && p.TryGetComponent<SpriteRenderer>(out var sr))
            {
                Undo.RecordObject(sr, "Player sprite");
                sr.sprite = _player;
                log.Add(Line("Player", _player, p.GetComponent<CircleCollider2D>()));
            }
        }
        if (_enemy != null) log.Add(SwapPrefab("Assets/StarterProject/Prefabs/Enemy.prefab", "Enemy", _enemy));
        if (_projectile != null) log.Add(SwapPrefab("Assets/StarterProject/Prefabs/Projectile.prefab", "Projectile", _projectile));

        var set = AssetDatabase.LoadAssetAtPath<CG2IconSet>(ICON_SET);
        int iconCount = 0;
        if (set != null)
        {
            Undo.RecordObject(set, "Icon set");
            foreach (var kv in _icons)
                if (kv.Value != null) { set.Set(kv.Key, kv.Value); iconCount++; }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            foreach (var ic in Object.FindObjectsByType<CG2UIIcon>(FindObjectsInactive.Include)) ic.Refresh();
            // 레벨업·상점 카드의 아이콘은 Play 중에 세트에서 다시 읽는다. 에디터 화면도 맞춰 둔다.
            foreach (var shop in Object.FindObjectsByType<ShopPanel>(FindObjectsInactive.Include)) EditorUtility.SetDirty(shop);
        }
        log.Add($"UI 아이콘 {iconCount}종 교체 → CG2IconSet (HUD·레벨업·상점 공통)");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[검증] 에셋 교체 완료\n        " + string.Join("\n        ", log) +
                  "\n        ── Play 해서 확인 ── ① 캐릭터가 콜라이더와 맞는 크기인가 ② 가장자리에 흰 띠(halo)가 없는가");
    }

    static string SwapPrefab(string path, string label, Sprite s)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return $"{label} : ⚠ 프리팹 없음 ({path})";
        var sr = root.GetComponent<SpriteRenderer>();
        sr.sprite = s;
        string line = Line(label, s, root.GetComponent<CircleCollider2D>());
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return line;
    }

    /// <summary>스프라이트 크기와 콜라이더 지름을 나란히 보여 준다. 차이가 크면 히트박스가 어긋난다.</summary>
    static string Line(string label, Sprite s, CircleCollider2D col)
    {
        var size = s.bounds.size;
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s)) as TextureImporter;
        string spec = ti == null ? "" :
            $" · PPU {ti.spritePixelsPerUnit} · {ti.filterMode}" + (ti.filterMode != FilterMode.Point || ti.spritePixelsPerUnit != ArtImport.PPU ? " ⚠ 규격 불일치" : "");
        string colTxt = col == null ? "" :
            $" · 콜라이더 지름 {col.radius * 2f * col.transform.lossyScale.x:F2}" +
            (Mathf.Abs(size.x - col.radius * 2f) > 0.35f ? " ⚠ 크기 차이 큼" : " ✓");
        return $"{label,-10} : {s.name} {s.rect.width}×{s.rect.height}px = {size.x:F2}×{size.y:F2}유닛{spec}{colTxt}";
    }
}
