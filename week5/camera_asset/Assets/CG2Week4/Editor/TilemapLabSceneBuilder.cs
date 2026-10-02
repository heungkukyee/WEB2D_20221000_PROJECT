#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 4주차 타일맵 실습 씬을 생성한다.
///
/// 3주차 DrawCallLabSceneBuilder와 같은 방침이다 — 씬 파일을 배포하는 대신 코드로 만들어,
/// Unity 버전이나 URP 설정이 달라도 항상 올바른 카메라 구성으로 씬이 만들어지도록 한다.
/// </summary>
public static class TilemapLabSceneBuilder
{
    const string DIR = "Assets/CG2Week4";
    const string SCENE_PATH = DIR + "/TilemapLab.unity";

    [MenuItem("CG2 Lab/4주차 준비/5. 타일맵 실습 씬 만들기", priority = 4)]
    public static void BuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (System.IO.File.Exists(SCENE_PATH) &&
            !EditorUtility.DisplayDialog("타일맵 실습 씬",
                "이미 실습 씬이 있습니다. 새로 만들면 기존 씬을 덮어씁니다.\n계속할까요?",
                "덮어쓰기", "취소"))
            return;

        // 타일 에셋이 없으면 씬을 만들어도 Play에서 에러가 난다. 먼저 막는다.
        if (!AssetDatabase.IsValidFolder(DIR + "/Tiles") ||
            AssetDatabase.FindAssets("t:TileBase", new[] { DIR + "/Tiles" }).Length == 0)
        {
            if (EditorUtility.DisplayDialog("타일 에셋 없음",
                    "타일 에셋이 아직 없습니다. 먼저 생성해야 실습 씬이 동작합니다.\n\n" +
                    "지금 생성할까요?", "생성하고 계속", "취소"))
                TilemapAssetBuilder.BuildAll();
            else
                return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 2D 실습이므로 기본 생성되는 Directional Light는 제거한다.
        var light = Object.FindAnyObjectByType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 7f;            // 2주차 게임과 같은 화각
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        var go = new GameObject("TilemapLab");
        go.AddComponent<TilemapLab>();
        Selection.activeGameObject = go;

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();

        Debug.Log($"[TilemapLab] 실습 씬 생성 완료 → {SCENE_PATH}\n" +
                  "Inspector에서 맵 크기와 모드를 정한 뒤 Play → Game View 우상단 Stats 를 확인하세요.\n" +
                  "권장 순서: Chunk/Single → Individual/Single → Chunk/Mixed → Atlas 적용");
    }

    [MenuItem("CG2 Lab/4주차 준비/5. 타일맵 실습 씬 열기", priority = 5)]
    public static void OpenScene()
    {
        if (!System.IO.File.Exists(SCENE_PATH))
        {
            EditorUtility.DisplayDialog("타일맵 실습 씬",
                "실습 씬이 없습니다. 먼저 'CG2 Lab → 4주차 준비 → 5. 타일맵 실습 씬 만들기' 를 실행하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SCENE_PATH);
    }
}
#endif
