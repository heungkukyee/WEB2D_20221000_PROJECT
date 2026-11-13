using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 6주차 3단계 — 한글 폰트 에셋과 톤앤매너(타이포그래피).
///
/// TMP 기본 폰트(LiberationSans SDF)에는 한글 글리프가 없어서 한글을 넣으면 □(두부)로 나온다(5주차 예고).
/// 픽셀 아트 게임이므로 픽셀 한글 폰트 갈무리11(Galmuri11, SIL OFL 1.1)을 쓴다 — 무료 · 게임 동봉 · 수정 허용.
///
/// Dynamic 폰트 에셋 — 처음 쓰는 글자가 나올 때 그 글자만 아틀라스에 굽는다. 한글 11,172자를 미리 굽지 않아도 된다.
///   대신 원본 TTF(약 5.1MB)가 빌드에 함께 들어간다 → 14주차(용량 최적화)에서 쓰는 글자만 Static으로 굽는다.
/// </summary>
public static class Week6Font
{
    const string DIR = "Assets/CG2Week6/Fonts";
    const string TTF = DIR + "/Galmuri11.ttf";
    const string ASSET = DIR + "/Galmuri11 SDF.asset";

    // 4단계와 같은 이유로 경로로 찾는다(팝업 창은 꺼져 있다). 런타임에 코드가 덮어쓰는 글자는 바꿔도 소용없다.
    static readonly Dictionary<string, (string ko, string en)> Labels = new()
    {
        { "Canvas_HUD_Dynamic/ShopButton/Label",                  ("상점 [Tab]", "SHOP [Tab]") },
        { "Canvas_Popup/ShopPanel/Window/Title",                  ("상점", "SHOP") },
        { "Canvas_Popup/ShopPanel/Window/CloseButton/Label",      ("닫기 [Tab]", "CLOSE [Tab]") },
        { "Canvas_Popup/GameOverPanel/RestartBtn/T",              ("다시 시작", "Restart") },
    };

    [MenuItem("CG2 Lab/6주차/3-1. 한글 폰트 에셋 만들기 (갈무리11 · OFL)", priority = 31)]
    public static TMP_FontAsset Create()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ASSET);
        if (existing != null) { AddFallback(existing); Report(existing, "이미 있음"); return existing; }

        var font = AssetDatabase.LoadAssetAtPath<Font>(TTF);
        if (font == null)
        {
            EditorUtility.DisplayDialog("한글 폰트", $"{TTF}가 없습니다. 6주차 패키지를 다시 임포트하세요.", "확인");
            return null;
        }
        // 샘플링 44pt = 갈무리11 픽셀 격자(11px)의 4배. SDF라 화면 크기가 달라도 외곽이 뭉개지지 않는다.
        var fa = TMP_FontAsset.CreateFontAsset(font, 44, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = "Galmuri11 SDF";
        AssetDatabase.CreateAsset(fa, ASSET);
        // 아틀라스 텍스처와 머티리얼을 에셋 안에 함께 저장해야 다시 열어도 깨지지 않는다.
        fa.atlasTextures[0].name = "Galmuri11 SDF Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = "Galmuri11 SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);

        // 고정 문구에 쓰는 글자는 미리 굽는다 — 첫 프레임에 아틀라스를 갱신하는 끊김을 피한다.
        var chars = "";
        foreach (var kv in Labels.Values) chars += kv.ko;
        fa.TryAddCharacters(chars + "0123456789 []:.%+-");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();

        AddFallback(fa);
        Report(fa, "생성");
        return fa;
    }

    /// <summary>기본 폰트의 Fallback에 넣어 두면, 영문 폰트를 쓰는 글자에서도 한글이 □ 대신 나온다.</summary>
    static void AddFallback(TMP_FontAsset fa)
    {
        var def = TMP_Settings.defaultFontAsset;
        if (def == null) return;
        def.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        if (!def.fallbackFontAssetTable.Contains(fa))
        {
            def.fallbackFontAssetTable.Add(fa);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
        }
    }

    static void Report(TMP_FontAsset fa, string what)
    {
        var ttf = new FileInfo(TTF);
        Debug.Log($"[검증] 한글 폰트 에셋 {what} — {ASSET}\n" +
                  $"        원본 {Path.GetFileName(TTF)} {ttf.Length / 1024f / 1024f:F2} MB · 샘플링 {fa.faceInfo.pointSize}pt · 아틀라스 {fa.atlasWidth}×{fa.atlasHeight} · {fa.atlasPopulationMode}\n" +
                  $"        기본 폰트 '{TMP_Settings.defaultFontAsset?.name}'의 Fallback에 등록 ✓ (영문 폰트에서도 한글이 나온다)\n" +
                  "        ── 다음 ── 3-2. 픽셀 폰트로 통일 · 고정 문구 한글화");
    }

    [MenuItem("CG2 Lab/6주차/3-2. 픽셀 폰트로 통일 · 고정 문구 한글화", priority = 32)]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("폰트", "Play를 멈춘 뒤 실행하세요.", "확인"); return; }
        var fa = Create();
        if (fa == null) return;

        int n = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(t, "폰트");
            t.font = fa;
            t.fontSharedMaterial = fa.material;
            n++;
        }
        int ko = SetLabels(korean: true);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[검증] 픽셀 폰트 통일 — TMP 글자 {n}개 → {fa.name} · 고정 문구 {ko}개 한글화\n" +
                  "        ── Play 해서 확인 ── 상점 버튼·창 제목이 한글로, 숫자·영문도 같은 픽셀 서체로 보이는가\n" +
                  "        ※ 레벨업 카드·상점 품목·점수 문구는 스크립트가 실행 중에 채운다 → 과제: LevelUpPanel.cs · ShopPanel.cs · HUD.cs의 문자열을 한글로");
    }

    public static int SetLabels(bool korean)
    {
        int n = 0;
        foreach (var kv in Labels)
        {
            var t = Week6Common.Find(kv.Key);
            if (t == null || !t.TryGetComponent<TMP_Text>(out var text)) continue;
            Undo.RecordObject(text, "문구");
            text.text = korean ? kv.Value.ko : kv.Value.en;
            n++;
        }
        return n;
    }

    [MenuItem("CG2 Lab/6주차/되돌리기 — 3단계 폰트 (기본 영문 폰트로)", priority = 92)]
    static void Revert()
    {
        if (EditorApplication.isPlaying) return;
        var def = TMP_Settings.defaultFontAsset;
        int n = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(t, "폰트 되돌리기");
            t.font = def;
            t.fontSharedMaterial = def.material;
            n++;
        }
        SetLabels(korean: false);
        // 기본 폰트의 Fallback에서 갈무리를 뺀다 — 패키지를 지운 뒤 빈 참조가 남지 않게
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ASSET);
        if (fa != null && def.fallbackFontAssetTable != null && def.fallbackFontAssetTable.Remove(fa))
        {
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[검증] 폰트 되돌리기 — TMP 글자 {n}개 → {def.name} · 고정 문구 영문 복귀 · Fallback 등록 해제 (폰트 에셋 파일은 남긴다)");
    }
}
