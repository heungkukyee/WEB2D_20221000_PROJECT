#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// Cinemachine 설치 상태를 확인하고, 필요하면 설치까지 해준다.
///
/// 이 파일은 Cinemachine을 전혀 참조하지 않는다. 그래서 패키지가 없어도 항상 컴파일된다.
/// 반대로 실제 카메라 리그 코드(CG2.Camera 어셈블리)는 패키지가 있을 때만 컴파일된다.
/// 즉 "안내는 항상 뜨고, 기능은 조건부"라는 구조다.
///
/// 이렇게 나눈 이유 — 미설치 상태에서 'using Unity.Cinemachine;' 한 줄이 있으면
/// 프로젝트 전체가 컴파일 에러로 멈춘다. 그러면 4주차 타일맵 수업까지 통째로 마비된다.
/// </summary>
public static class CG2CinemachineInstaller
{
    const string PACKAGE_ID = "com.unity.cinemachine";
    static AddRequest _request;

    [MenuItem("CG2 Lab/시네머신/설치 상태 확인", priority = 99)]
    public static void CheckStatus()
    {
        if (IsInstalled())
        {
            Debug.Log("[CG2 시네머신] 설치되어 있습니다.\n" +
                      "        다음 단계: 'CG2 Lab → 시네머신 → 카메라 리그 구성' 을 실행하세요.\n" +
                      "        메뉴가 안 보이면 Unity가 아직 컴파일 중이거나, 설치된 버전이 3.0 미만입니다.");
            return;
        }

        if (EditorUtility.DisplayDialog("Cinemachine 미설치",
                "Cinemachine이 설치되어 있지 않습니다.\n\n" +
                "설치하지 않아도 4주차 타일맵 수업은 정상 진행됩니다.\n" +
                "기존 CameraFollow.cs(Lerp 방식)가 그대로 카메라를 따라오게 합니다.\n\n" +
                "지금 설치할까요? (Package Manager로 자동 설치)",
                "설치하기", "나중에"))
            Install();
    }

    [MenuItem("CG2 Lab/시네머신/Cinemachine 설치", priority = 98)]
    public static void Install()
    {
        if (IsInstalled())
        {
            Debug.Log("[CG2 시네머신] 이미 설치되어 있습니다.");
            return;
        }

        Debug.Log($"[CG2 시네머신] {PACKAGE_ID} 설치를 시작합니다. 잠시 기다려 주세요…");
        _request = Client.Add(PACKAGE_ID);
        EditorApplication.update += Progress;
    }

    static void Progress()
    {
        if (_request == null || !_request.IsCompleted) return;
        EditorApplication.update -= Progress;

        if (_request.Status == StatusCode.Success)
            Debug.Log($"[CG2 시네머신] 설치 완료 — {_request.Result.displayName} {_request.Result.version}\n" +
                      "        컴파일이 끝나면 'CG2 Lab → 시네머신 → 카메라 리그 구성' 메뉴가 나타납니다.");
        else
            Debug.LogError($"[CG2 시네머신] 설치 실패 — {_request.Error?.message}\n" +
                           "        Window → Package Manager → Unity Registry 에서 Cinemachine을 직접 설치하세요.\n" +
                           "        설치하지 않아도 4주차 타일맵 수업은 정상 진행됩니다.");
        _request = null;
    }

    static bool IsInstalled()
    {
        // Client.List는 비동기라 메뉴에서 쓰기 불편하다. manifest.json을 직접 읽는다.
        const string manifest = "Packages/manifest.json";
        return System.IO.File.Exists(manifest) &&
               System.IO.File.ReadAllText(manifest).Contains($"\"{PACKAGE_ID}\"");
    }
}
#endif
