using UnityEngine;

/// <summary>
/// 레벨업 창과 상점 창이 동시에 게임을 멈출 수 있으므로, 멈춤 요청을 개수로 센다.
/// 마지막 창이 닫힐 때만 시간을 다시 흐르게 한다. 게임 오버 상태면 풀지 않는다.
/// </summary>
public static class CG2Pause
{
    static int _count;

    public static bool IsPaused => _count > 0;

    public static void Push()
    {
        _count++;
        Time.timeScale = 0f;
    }

    public static void Pop()
    {
        _count = Mathf.Max(0, _count - 1);
        if (_count == 0 && !(GameManager.Instance != null && GameManager.Instance.IsGameOver))
            Time.timeScale = 1f;
    }

    // Enter Play Mode 옵션에서 도메인 리로드를 꺼도, Restart로 씬을 다시 불러도 카운트가 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad()
    {
        _count = 0;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => _count = 0;
    }
}
