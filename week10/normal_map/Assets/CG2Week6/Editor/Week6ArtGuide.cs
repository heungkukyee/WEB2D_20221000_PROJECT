using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 6주차 5단계 — 색상 가이드·톤앤매너 문서화와 체크포인트.
///
/// 아트 가이드는 "지금 프로젝트에 실제로 적용된 값"에서 만든다. 손으로 쓴 문서는 코드와 어긋나기 쉽다.
/// 팔레트 에셋 · CG2 Theme 설정 · 폰트 · 측정 기록(Library/CG2Week6)을 읽어 ArtGuide.md와 스와치 PNG로 내보낸다.
/// README에 링크하면 팀원(또는 다음 주의 나)이 같은 기준으로 에셋을 만든다.
/// </summary>
public static class Week6ArtGuide
{
    const string DIR = "Assets/CG2Week6/ArtGuide";
    const string MD = DIR + "/ArtGuide.md";
    const string SWATCH = DIR + "/palette_swatch.png";

    [MenuItem("CG2 Lab/6주차/5. 아트 가이드 내보내기 — ArtGuide.md + 스와치", priority = 50)]
    public static void Export()
    {
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        var p = theme != null && theme.palette != null ? theme.palette : PaletteStudioWindow.EnsureMyPalette();
        Directory.CreateDirectory(DIR);
        WriteSwatch(p);

        var sb = new StringBuilder();
        string project = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
        sb.AppendLine($"# 아트 가이드 — {project}");
        sb.AppendLine();
        sb.AppendLine($"> 테마 **{p.themeName}** · 조화 규칙 {HarmonyKo(p.harmony)} · Color Space **{PlayerSettings.colorSpace}** · 생성 {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("> 이 문서는 `CG2 Lab → 6주차 → 5`가 프로젝트에 실제로 적용된 값에서 자동으로 만든다. 값을 바꾸면 다시 내보낸다.");
        sb.AppendLine();
        sb.AppendLine("![palette](palette_swatch.png)");
        sb.AppendLine();

        sb.AppendLine("## 1. 색 공간");
        sb.AppendLine();
        sb.AppendLine($"- Project Settings → Player → Color Space = **{PlayerSettings.colorSpace}**" +
                      (PlayerSettings.colorSpace == ColorSpace.Linear ? " (URP · WebGL 2 권장)" : " ⚠ Linear로 바꿀 것"));
        sb.AppendLine("- 색 텍스처(스프라이트·UI 아이콘)는 `sRGB (Color Texture)` **켬**, 데이터 텍스처(노멀 맵·마스크)는 **끔** — 7주차 노멀 맵부터 적용");
        string cs = Week6Common.Load("colorspace_table");
        if (cs != "") { sb.AppendLine(); sb.AppendLine("측정 #1 — 같은 씬, 두 색 공간 (화면 출력 0~255)"); sb.AppendLine(); sb.AppendLine(cs); }
        sb.AppendLine();

        sb.AppendLine("## 2. 팔레트 1 · 2 · 1");
        sb.AppendLine();
        sb.AppendLine("| 역할 | HEX | H | S | V | 흑백 밝기 | 면적 | 쓰는 곳 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        string[] use = { "바닥 톤 · 배경 · HUD 띠", "창·패널 바탕", "버튼 · 상호작용", "제목 · 경험치 · 포커스 링 · 보상", "체력 · 위험 (기능색)", "글자 (기능색)" };
        string[] area = { "60%", "15%", "15%", "10%", "—", "—" };
        var roles = PaletteStudioWindow.Roles(p);
        for (int i = 0; i < roles.Count; i++)
        {
            var (name, c) = roles[i];
            Color.RGBToHSV(c, out float h, out float s, out float v);
            sb.AppendLine($"| {name} | `{CG2Palette.Hex(c)}` | {h * 360:0}° | {s * 100:0} | {v * 100:0} | {CG2Palette.Luma(c) * 100:0}% | {area[i]} | {use[i]} |");
        }
        sb.AppendLine();
        sb.AppendLine("### 검증");
        sb.AppendLine();
        foreach (var (ok, line) in PaletteStudioWindow.Checks(p)) sb.AppendLine($"- {(ok ? "✅" : "⚠️")} {line}");
        sb.AppendLine();

        sb.AppendLine("## 3. UI 상태 규칙");
        sb.AppendLine();
        sb.AppendLine("버튼 Image는 **흰색**, 색은 Button의 ColorBlock(Color Tint)이 맡는다. 상태색은 팔레트에서 계산한다.");
        sb.AppendLine();
        sb.AppendLine("| 상태 | 색 | 규칙 |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| Normal | `{CG2Palette.Hex(p.ButtonNormal)}` | Sub2 |");
        sb.AppendLine($"| Highlighted (호버) | `{CG2Palette.Hex(p.ButtonHighlighted)}` | Sub2 명도 +12% |");
        sb.AppendLine($"| Pressed | `{CG2Palette.Hex(p.ButtonPressed)}` | Sub2 명도 −15% |");
        sb.AppendLine($"| Selected (포커스) | `{CG2Palette.Hex(p.ButtonSelected)}` + 테두리 `{CG2Palette.Hex(p.FocusRing)}` | 배경은 Normal, 강조색 9-슬라이스 테두리 3px |");
        sb.AppendLine($"| Disabled | `{CG2Palette.Hex(p.ButtonDisabled)}` α0.6 | 채도 0 · 글자·아이콘 α{(theme != null ? theme.disabledContentAlpha : 0.4f)} |");
        sb.AppendLine("| Fade Duration | 0.08 s | 즉각 반응 |");
        sb.AppendLine();

        sb.AppendLine("## 4. 씬 톤 · 개체 틴트");
        sb.AppendLine();
        float ct = theme != null ? theme.characterTint : 0.25f;
        sb.AppendLine($"- 배경 조명 (Global Light 2D → Ground·Wall) `{CG2Palette.Hex(p.Ambient)}` — Main 색상 · 채도 25% · 명도 100%");
        sb.AppendLine($"- 캐릭터 조명 ({CG2Theme.ENTITY_LIGHT} → Default·Entity·FX) `{CG2Palette.Hex(Color.Lerp(Color.white, p.Ambient, ct))}` — 흰색에 환경광 {ct * 100:0}%");
        sb.AppendLine($"- 카메라 배경 `{CG2Palette.Hex(p.main)}`");
        if (theme != null) sb.AppendLine($"- Tilemap_Ground 틴트 {theme.groundTint:F2} · Tilemap_Wall 틴트 {theme.wallTint:F2} (흰색 ↔ 팔레트 색상 보간)");
        sb.AppendLine("- 개체 틴트는 **`SpriteRenderer.color`(정점 색)** 만 쓴다. `material.color`(머티리얼 복제) · `MaterialPropertyBlock`은 금지");
        string bench = Week6Common.Load("tint_bench");
        if (bench != "")
        {
            sb.AppendLine(); sb.AppendLine("측정 #2 — 틴트 방법별 렌더링 비용"); sb.AppendLine();
            foreach (var line in bench.Split('\n')) if (line.StartsWith("|")) sb.AppendLine(line.TrimEnd());
        }
        sb.AppendLine();

        string game = Week6Common.Load("game_compare");
        if (game != "")
        {
            sb.AppendLine("### 측정 #3 — 실제 게임에서 5주차 ↔ 6주차");
            sb.AppendLine();
            foreach (var line in game.Split('\n'))
            {
                var l = line.TrimEnd();
                if (l.StartsWith("|")) sb.AppendLine(l);
                else if (l.StartsWith("#3")) { sb.AppendLine(); sb.AppendLine($"**{l}**"); sb.AppendLine(); }
            }
            string cs2 = Week6Common.Load("game_colorspace");
            if (cs2 != "")
            {
                sb.AppendLine();
                foreach (var line in cs2.Split('\n'))
                    sb.AppendLine(line.StartsWith("#3") ? $"**{line.TrimEnd()}**\n" : line.TrimEnd());
            }
            sb.AppendLine();
        }
        sb.AppendLine("- 조명을 나누는 이유: 전역 조명 하나로 톤을 입히면 캐릭터까지 물들어 바닥에 묻힌다 (측정 #3-2의 '전역 조명 하나' 열)");
        sb.AppendLine();

        sb.AppendLine("## 5. 타이포그래피");
        sb.AppendLine();
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CG2Week6/Fonts/Galmuri11 SDF.asset");
        sb.AppendLine(fa != null
            ? $"- 본문·UI: **갈무리11 (Galmuri11)** · SIL OFL 1.1 · TMP {fa.atlasPopulationMode} SDF · 샘플링 {fa.faceInfo.pointSize}pt — 픽셀 아트와 같은 격자감"
            : "- ⚠ 한글 폰트 에셋 없음 — 3-1 실행");
        sb.AppendLine("- 제목 = Accent, 본문 = Text. 숫자·영문도 같은 서체로 통일");
        sb.AppendLine("- 라이선스 전문: `Assets/CG2Week6/Fonts/Galmuri_OFL.txt` (게임과 함께 배포 가능)");
        sb.AppendLine();

        sb.AppendLine("## 6. 접근성");
        sb.AppendLine();
        sb.AppendLine("- 흑백으로 봐도 바닥 < 창 < 버튼 < 강조 명도 순서가 유지된다 (2-1 미리보기 캡처로 확인)");
        sb.AppendLine("- 색만으로 정보를 전달하지 않는다 — HP ♥ / EXP ◆ 아이콘, Disabled 글자 흐림, 포커스는 테두리");
        var caps = Directory.Exists("Assets/CG2Week6/Captures") ? Directory.GetFiles("Assets/CG2Week6/Captures", "*.png") : new string[0];
        if (caps.Length > 0) sb.AppendLine($"- 최근 미리보기: `{caps[caps.Length - 1].Replace('\\', '/')}`");
        sb.AppendLine();

        sb.AppendLine("## 7. 하지 말 것");
        sb.AppendLine();
        sb.AppendLine("- 코드·Inspector에 색을 직접 적기 (`new Color(...)`) → 팔레트 에셋의 역할 이름으로 쓴다");
        sb.AppendLine("- 버튼 Image에 색 넣기 → Color Tint와 곱해져 상태색이 어두워진다");
        sb.AppendLine("- 강조색 배경 위 흰 글자 → 대비 부족 (포커스는 테두리로)");
        sb.AppendLine("- 색 텍스처의 sRGB 끄기 / 노멀 맵의 sRGB 켜기");
        sb.AppendLine("- 개체 틴트에 `renderer.material` 접근 → 머티리얼 복제 · 누수");
        string before = Week6Common.Load("audit_before"), after = Week6Common.Load("audit_after");
        string Audit(string v) { var a = v.Split('|'); return a.Length == 2 ? $"{a[0]}색 중 하드코딩 {a[1]}개" : v; }
        if (before != "")
        {
            sb.AppendLine();
            sb.AppendLine($"색 감사 (2-0): 5주차 {Audit(before)} → 팔레트 적용 후 {(after != "" ? Audit(after) : "(2-0 다시 실행)")}");
        }

        File.WriteAllText(MD, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(MD);
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(MD));
        Debug.Log($"[검증] 아트 가이드 내보내기 — {MD} · {SWATCH}\n" +
                  "        README.md에 링크하고 스와치 이미지를 붙인다. 측정 #1·#2 표가 비어 있으면 1-1 · 4-1을 먼저 실행한다.");
    }

    static string HarmonyKo(CG2Palette.Harmony h) => h switch
    {
        CG2Palette.Harmony.Analogous => "유사색",
        CG2Palette.Harmony.Complementary => "보색",
        CG2Palette.Harmony.Triadic => "삼각",
        _ => "분할 보색",
    };

    /// <summary>위: 60·30·10 면적 막대 / 아래: 역할 6칸 (원본 · 흑백 · 적록 색약 3줄).</summary>
    static void WriteSwatch(CG2Palette p)
    {
        const int W = 720, H = 300;
        var t = new Texture2D(W, H, TextureFormat.RGB24, false);
        var bg = new Color(0.12f, 0.12f, 0.14f);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) t.SetPixel(x, y, bg);

        void Rect(int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) t.SetPixel(x, H - 1 - y, c);
        }
        // 면적 막대 (60 · 15 · 15 · 10)
        int bx = 0;
        foreach (var (c, w) in new[] { (p.main, 0.60f), (p.sub1, 0.15f), (p.sub2, 0.15f), (p.accent, 0.10f) })
        {
            int px = Mathf.RoundToInt(W * w);
            Rect(bx, 0, Mathf.Min(px, W - bx), 80, c);
            bx += px;
        }
        // 역할 6칸 × 3줄
        var roles = PaletteStudioWindow.Roles(p);
        int cw = W / roles.Count;
        for (int i = 0; i < roles.Count; i++)
        {
            var c = roles[i].Item2;
            float g = CG2Palette.Luma(c);
            Rect(i * cw + 6, 96, cw - 12, 100, c);
            Rect(i * cw + 6, 200, cw - 12, 46, new Color(g, g, g));
            Rect(i * cw + 6, 250, cw - 12, 46, CG2Palette.Deuteranopia(c));
        }
        t.Apply();
        File.WriteAllBytes(SWATCH, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(SWATCH);
    }

    // ────────────────────────────────────────────────────────── 체크포인트

    [MenuItem("CG2 Lab/6주차/6. 체크포인트 — 색 공간 · 팔레트 · UI · 톤 · 문서", priority = 60)]
    public static void Check()
    {
        var lines = new List<string>();
        int warn = 0;
        void Item(bool pass, string text, string fix = null)
        {
            lines.Add($"{(pass ? "✓" : "⚠")} {text}" + (!pass && fix != null ? $"\n            → {fix}" : ""));
            if (!pass) warn++;
        }

        lines.Add("[색 공간]");
        Item(PlayerSettings.colorSpace == ColorSpace.Linear, $"Color Space = {PlayerSettings.colorSpace}", "1-2. 색 공간 전환으로 Linear 복귀");
        Item(Week6Common.Load("colorspace_Linear") != "" && Week6Common.Load("colorspace_Gamma") != "", "측정 #1 Linear · Gamma 둘 다 기록", "1-1 → 1-2 → 1-1");
        int srgbOff = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/CG2Week5", "Assets/StarterProject", "Assets/CG2Week4/Art" }))
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is TextureImporter ti &&
                ti.textureType == TextureImporterType.Sprite && !ti.sRGBTexture) srgbOff++;
        Item(srgbOff == 0, $"색 텍스처(스프라이트) 중 sRGB 꺼진 것 {srgbOff}개", "스프라이트 Inspector → sRGB (Color Texture) 켜기");

        lines.Add("[팔레트]");
        var theme = Object.FindAnyObjectByType<CG2Theme>();
        var p = theme != null && theme.palette != null ? theme.palette : AssetDatabase.LoadAssetAtPath<CG2Palette>(PaletteStudioWindow.MY);
        Item(p != null, $"팔레트 에셋 {(p != null ? $"'{p.themeName}'" : "없음")}", "2. 팔레트 스튜디오 → 저장");
        if (p != null)
        {
            int fails = 0;
            foreach (var (ok, _) in PaletteStudioWindow.Checks(p)) if (!ok) fails++;
            Item(fails == 0, $"팔레트 검증 경고 {fails}개", "팔레트 스튜디오 ⑤ 검증의 ⚠ 항목을 고친다");
        }

        lines.Add("[UI]");
        Item(theme != null && theme.enabled && theme.applyUI, "CG2 Theme · UI 적용", "3. UI 테마 적용 (3-3 비교 중이면 한 번 더 눌러 테마로)");
        if (theme != null && p != null)
        {
            int bad = 0, total = 0;
            foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include))
            {
                if (s is Slider || s.targetGraphic == null) continue;
                total++;
                if (s.targetGraphic.color != Color.white || s.colors.normalColor != p.ButtonNormal) bad++;
            }
            Item(bad == 0, $"버튼 {total}개 중 규칙 밖 {bad}개 (Image 흰색 + Normal = Sub2)", "3. UI 테마 적용 다시 실행");
            Item(theme.focusRingSprite != null, "포커스 링 스프라이트 (9-슬라이스)");
        }
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CG2Week6/Fonts/Galmuri11 SDF.asset");
        int texts = 0, pixel = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)) { texts++; if (fa != null && t.font == fa) pixel++; }
        Item(fa != null && pixel == texts, $"한글 픽셀 폰트 {pixel}/{texts}", "3-1 → 3-2");

        lines.Add("[씬 톤]");
        Item(theme != null && theme.applySceneTone, "CG2 Theme · 씬 톤 적용", "4. 씬 톤 적용");
        Light2D global = null;
        foreach (var l in Object.FindObjectsByType<Light2D>()) if (l.lightType == Light2D.LightType.Global) global = l;
        if (global != null && p != null)
            Item(global.color == p.Ambient, $"Global Light 2D {CG2Palette.Hex(global.color)} = 팔레트 환경광 {CG2Palette.Hex(p.Ambient)}", "4. 씬 톤 적용 다시 실행");
        Item(Week6Common.Load("tint_bench") != "", "측정 #2 틴트 방법별 비용 기록", "Play → 4-1");
        Item(Week6Common.FindRoot(CG2Theme.ENTITY_LIGHT) != null, "조명 분리 — 배경 조명 / 캐릭터 조명", "4. 씬 톤 적용 다시 실행");
        Item(Week6Common.Load("game_compare") != "", "측정 #3 게임 전·후 비교 기록", "Play → 4-3");
        // #3-4(게임 화면 Gamma 재측정)는 수업에서 교수자 시연으로 한다 — 기록이 없어도 경고로 세지 않는다
        lines.Add(Week6Common.Load("game_colorspace") != ""
            ? "✓ 측정 #3-4 게임 화면 Linear · Gamma 기록 (선택)"
            : "ℹ 측정 #3-4 게임 화면 Gamma 비교 — 교수자 시연 항목 (선택: Stop → 1-2 → Play → 4-3 → Stop → 1-2)");

        lines.Add("[문서]");
        Item(File.Exists(MD), $"아트 가이드 {MD}", "5. 아트 가이드 내보내기");

        Debug.Log($"[검증] 6주차 체크포인트 — 경고 {warn}개\n        " + string.Join("\n        ", lines) +
                  "\n        ── 마무리 ── Ctrl+S 저장 → README.md에 ArtGuide 링크 · 스와치 · 측정 #1·#2 · 테마 비교 스크린샷");
    }
}
