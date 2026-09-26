using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 3주차 드로우콜 측정 실습 씬을 생성한다.
/// 씬 파일을 배포하는 대신 코드로 만들어, Unity 버전이나 URP 설정이 달라도
/// 항상 올바른 카메라 구성으로 씬이 만들어지도록 한다.
/// </summary>
public static class DrawCallLabSceneBuilder
{
    const string DIR = "Assets/DrawCallLab";
    const string SCENE_PATH = DIR + "/DrawCallLab.unity";

    [MenuItem("CG2 Lab/드로우콜 실습 씬 만들기", priority = 0)]
    public static void BuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (System.IO.File.Exists(SCENE_PATH) &&
            !EditorUtility.DisplayDialog("드로우콜 실습 씬",
                "이미 실습 씬이 있습니다. 새로 만들면 기존 씬을 덮어씁니다.\n계속할까요?",
                "덮어쓰기", "취소"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 2D 실습이므로 기본 생성되는 Directional Light는 제거한다.
        var light = Object.FindAnyObjectByType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 10f;          // DrawCallLab이 Play 시 다시 맞춘다
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        var go = new GameObject("DrawCallLab");
        go.AddComponent<DrawCallLab>();
        Selection.activeGameObject = go;

        if (!AssetDatabase.IsValidFolder(DIR)) AssetDatabase.CreateFolder("Assets", "DrawCallLab");
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();

        Debug.Log($"[DrawCallLab] 실습 씬 생성 완료 → {SCENE_PATH}\n" +
                  "Inspector에서 개체 수와 모드를 정한 뒤 Play → Game View 우상단 Stats 를 확인하세요.");
    }

    [MenuItem("CG2 Lab/드로우콜 실습 씬 열기", priority = 1)]
    public static void OpenScene()
    {
        if (!System.IO.File.Exists(SCENE_PATH))
        {
            EditorUtility.DisplayDialog("드로우콜 실습 씬",
                "실습 씬이 없습니다. 먼저 'CG2 Lab → 드로우콜 실습 씬 만들기' 를 실행하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SCENE_PATH);
    }

    [MenuItem("CG2 Lab/Sprite Atlas 설정 열기", priority = 20)]
    public static void OpenAtlasSettings()
    {
        SettingsService.OpenProjectSettings("Project/Editor");
        Debug.Log("[DrawCallLab] Editor 설정의 'Sprite Atlas' 항목에서 Atlas 활성/비활성을 전환하세요. " +
                  "같은 씬에서 적용 전/후를 비교할 수 있습니다.");
    }
}
