using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 5주차 메뉴 진입점과 체크포인트 진단.
///
///   1단계  카메라 리그 적용 — Cinemachine이 없으면 설치부터 안내한다.
///   6단계  웹 빌드 전 점검 — 체크포인트 3가지(카메라·UI·에셋)를 한 번에 확인하고 결과를 표로 남긴다.
/// </summary>
public static class Week5Checkpoint
{
    const string RIG_MENU = "CG2 Lab/시네머신/카메라 리그 구성";

    [MenuItem("CG2 Lab/5주차/1. 카메라 리그 적용 (Follow + Confiner)", priority = 10)]
    static void ApplyRig()
    {
        if (!PackageInstalled("com.unity.cinemachine"))
        {
            if (EditorUtility.DisplayDialog("카메라 리그",
                    "Cinemachine이 설치되어 있지 않습니다.\n지금 설치할까요? 설치·컴파일이 끝난 뒤 이 메뉴를 한 번 더 누르세요.",
                    "설치하기", "취소"))
                EditorApplication.ExecuteMenuItem("CG2 Lab/시네머신/Cinemachine 설치");
            return;
        }
        // 리그 코드는 Cinemachine이 있을 때만 컴파일되는 별도 어셈블리에 있다. 메뉴 이름으로 부른다.
        if (!EditorApplication.ExecuteMenuItem(RIG_MENU))
            EditorUtility.DisplayDialog("카메라 리그",
                "리그 메뉴를 찾지 못했습니다. Unity가 아직 컴파일 중이면 잠시 뒤 다시 누르세요.\n" +
                "계속 안 되면 Console의 컴파일 에러를 확인하세요.", "확인");
    }

    const int CAM = 1, UI = 2, ASSET = 4, WEB = 8;

    [MenuItem("CG2 Lab/5주차/진단 — 1단계 카메라", priority = 80)]
    static void DiagCamera() => Run(CAM, "1단계 카메라");

    [MenuItem("CG2 Lab/5주차/진단 — 3단계 UI", priority = 81)]
    static void DiagUI() => Run(UI, "3단계 UI");

    [MenuItem("CG2 Lab/5주차/진단 — 4·5단계 에셋·아틀라스", priority = 82)]
    static void DiagAsset() => Run(ASSET, "4·5단계 에셋·아틀라스");

    [MenuItem("CG2 Lab/5주차/6. 웹 빌드 전 점검 (체크포인트)", priority = 60)]
    public static void Check() => Run(CAM | UI | ASSET | WEB, "체크포인트");

    /// <summary>
    /// 단계별 진단과 최종 체크포인트가 같은 검사를 쓴다. 막힌 단계만 골라 부를 수 있게 영역을 나눴다.
    /// </summary>
    static void Run(int mask, string title)
    {
        var ok = new List<string>();
        int warn = 0;
        void Item(bool pass, string text, string fix = null)
        {
            ok.Add($"{(pass ? "✓" : "⚠")} {text}" + (!pass && fix != null ? $"\n            → {fix}" : ""));
            if (!pass) warn++;
        }

        // ── ① 카메라 ──
        if ((mask & CAM) != 0)
        {
            ok.Add("[카메라]");
            var cam = Camera.main;
            var brain = cam != null ? cam.GetComponent("CinemachineBrain") : null;
            var legacy = cam != null ? cam.GetComponent("CameraFollow") as Behaviour : null;
            var rig = GameObject.Find("CG2 CameraRig");
            var bounds = GameObject.Find("CG2 CameraBounds");
            Item(PackageInstalled("com.unity.cinemachine"), "Cinemachine 설치", "CG2 Lab → 5주차 → 1. 카메라 리그 적용 (설치 안내가 뜬다)");
            Item(brain != null, "Main Camera에 CinemachineBrain", "CG2 Lab → 5주차 → 1. 카메라 리그 적용");
            Item(legacy == null || !legacy.enabled, "2주차 CameraFollow 꺼짐 (Brain과 동시에 켜면 화면이 떨린다)");
            Item(rig != null, "CG2 CameraRig (Follow + Confiner)");
            var poly = bounds != null ? bounds.GetComponent<PolygonCollider2D>() : null;
            if (poly != null && cam != null)
            {
                var b = poly.bounds;
                float needW = cam.orthographicSize * cam.aspect * 2f, needH = cam.orthographicSize * 2f;
                Item(b.size.x >= needW && b.size.y >= needH,
                     $"경계 {b.size.x:F1}×{b.size.y:F1}유닛 ≥ 시야 {needW:F1}×{needH:F1}유닛 (Size {cam.orthographicSize})",
                     "Size를 줄이거나 더 큰 맵을 까세요. 시야가 경계보다 크면 Confiner가 경계를 지킬 수 없습니다.");
            }
            else Item(false, "CG2 CameraBounds 경계 폴리곤", "카메라 리그 구성을 다시 실행하세요.");
        }

        // ── ② UI ──
        if ((mask & UI) != 0)
        {
            ok.Add("[UI]");
            foreach (var n in new[] { "Canvas_HUD_Static", "Canvas_HUD_Dynamic", "Canvas_Popup" })
            {
                var go = FindRoot(n);
                if (go == null) { Item(false, n, "CG2 Lab → 5주차 → 3. UI 구성"); continue; }
                if (!go.activeSelf)
                {
                    Item(false, $"{n} 꺼짐 — 3-5 비교용 '합침' 상태", "CG2 Lab → 5주차 → 3-5를 한 번 더 눌러 분리 상태로");
                    continue;
                }
                var c = go.GetComponent<Canvas>();
                var s = go.GetComponent<CanvasScaler>();
                bool scaleOk = s != null && s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                               s.referenceResolution == new Vector2(1920, 1080);
                Item(scaleOk, $"{n} · {c.renderMode} · Scaler {(s != null ? s.referenceResolution.ToString() : "없음")}",
                     "Scale With Screen Size 1920×1080 — 브라우저 창 크기가 바뀌어도 비율이 유지된다");
                if (c.renderMode != RenderMode.ScreenSpaceOverlay)
                    Item(false, $"{n}가 {c.renderMode} — 비교가 끝났으면 Overlay로", "CG2 Lab → 5주차 → 3-1. Render Mode — Overlay");
            }
            int raycastTargets = 0;
            foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include))
                if (g.raycastTarget && g.GetComponent<Selectable>() == null && !g.name.EndsWith("Panel")) raycastTargets++;
            Item(raycastTargets == 0, $"클릭 대상이 아닌 Graphic의 Raycast Target {raycastTargets}개",
                 "텍스트·아이콘은 Raycast Target을 끄면 GraphicRaycaster의 검사 대상이 준다");
        }

        // ── ③ 에셋 · 아틀라스 ──
        if ((mask & ASSET) != 0)
        {
            ok.Add("[에셋]");
            var player = GameObject.FindGameObjectWithTag("Player");
            CheckSprite(Item, "Player", player != null ? player.GetComponent<SpriteRenderer>().sprite : null);
            CheckSprite(Item, "Enemy", PrefabSprite("Assets/StarterProject/Prefabs/Enemy.prefab"));
            Item(AssetDatabase.LoadAssetAtPath<Object>("Assets/CG2Week5/Atlas/Atlas_World.spriteatlasv2") != null &&
                 AssetDatabase.LoadAssetAtPath<Object>("Assets/CG2Week5/Atlas/Atlas_UI.spriteatlasv2") != null,
                 "Sprite Atlas 2종 (World · UI)", "CG2 Lab → 5주차 → 5. Sprite Atlas 재적용");
            bool packer = EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2 ||
                          EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2Build;
            Item(packer, $"Sprite Packer Mode = {EditorSettings.spritePackerMode}",
                 "5-1 비교용 '끔' 상태입니다. CG2 Lab → 5주차 → 5-1을 한 번 더 눌러 켜세요 (끈 채로 빌드하면 아틀라스가 빠진다)");
        }

        // ── ④ 웹 빌드 ──
        if ((mask & WEB) != 0)
        {
            ok.Add("[웹 빌드]");
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            bool inBuild = false;
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled && s.path == active) inBuild = true;
            Item(inBuild, $"현재 씬이 빌드 목록에 포함 ({active})", "Build Profiles → Scene List에 추가");
            Item(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL), "Web 빌드 모듈 설치",
                 "Unity Hub → 설치 → 모듈 추가 → Web Build Support");
            var comp = PlayerSettings.WebGL.compressionFormat;
            Item(true, $"압축 {comp} · Decompression Fallback {(PlayerSettings.WebGL.decompressionFallback ? "켬" : "끔")}" +
                       (comp != WebGLCompressionFormat.Disabled && !PlayerSettings.WebGL.decompressionFallback
                           ? "  (index.html을 더블클릭하거나 일반 서버로 열면 로딩 에러 — Build And Run을 쓴다)" : ""));
        }

        Debug.Log($"[검증] 5주차 {title} — 경고 {warn}개\n        " + string.Join("\n        ", ok) +
                  ((mask & WEB) != 0
                      ? "\n        ── 브라우저에서 직접 확인 ──\n" +
                        "        ① 맵 끝에서 카메라가 멈추는가  ② 창 크기를 바꿔도 HUD가 모서리에 붙어 있는가\n" +
                        "        ③ 캐릭터 픽셀이 에디터와 같은 선명도인가 (브라우저 확대 100% · Windows 배율 확인)"
                      : ""));
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static void CheckSprite(System.Action<bool, string, string> item, string label, Sprite s)
    {
        if (s == null) { item(false, $"{label} 스프라이트 없음", null); return; }
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s)) as TextureImporter;
        bool spec = ti != null && ti.spritePixelsPerUnit == 64 && ti.filterMode == FilterMode.Point &&
                    ti.textureCompression == TextureImporterCompression.Uncompressed;
        bool mine = AssetDatabase.GetAssetPath(s).Contains("/MyArt/");
        item(spec, $"{label} = {s.name} · PPU {ti?.spritePixelsPerUnit} · {ti?.filterMode} · {ti?.textureCompression}",
             "CG2 Art Studio로 가공하면 규격이 자동 적용된다");
        item(mine, $"{label}가 고유 아트(MyArt)로 교체됨", "CG2 Lab → 5주차 → 4. 아트 스튜디오 → 교체 적용");
    }

    static Sprite PrefabSprite(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return go != null && go.TryGetComponent<SpriteRenderer>(out var sr) ? sr.sprite : null;
    }

    static bool PackageInstalled(string id) =>
        System.IO.File.Exists("Packages/manifest.json") &&
        System.IO.File.ReadAllText("Packages/manifest.json").Contains($"\"{id}\"");
}
