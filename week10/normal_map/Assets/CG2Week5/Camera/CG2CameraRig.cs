using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 5주차 카메라 리그 — Cinemachine 3 기반.
///
/// 리그는 네 가지 기능을 갖고 있지만, 5주차에는 Follow와 Confiner만 켠다.
///   Follow   (5주차) : 플레이어를 따라간다.
///   Confiner (5주차) : 맵 경계(CG2 CameraBounds) 밖을 비추지 않는다.
///   Shake    (13주차): 피격·폭발 시 화면 흔들림 — 지금은 꺼 둔다.
///   Zoom     (13주차): 위기 연출용 줌 — 지금은 꺼 둔다.
///
/// 이 어셈블리(CG2.Camera)는 Cinemachine이 설치됐을 때만 컴파일된다(versionDefines).
/// 다른 스크립트는 이 클래스를 직접 참조하지 말고 SendMessage("SetOrthoSize", 값)로 부른다.
/// 그래야 Cinemachine이 없는 학생 프로젝트에서도 컴파일 에러가 나지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class CG2CameraRig : MonoBehaviour
{
    [Header("5주차 — 켜는 기능")]
    [Tooltip("플레이어를 따라간다. 끄면 카메라가 제자리에 멈춘다.")]
    [SerializeField] bool follow = true;
    [Tooltip("맵 경계 밖을 비추지 않는다. 끄고 맵 끝으로 가 보면 차이를 바로 볼 수 있다.")]
    [SerializeField] bool confiner = true;

    [Header("13주차 — 아직 끔")]
    [SerializeField] bool shake = false;
    [SerializeField] bool zoom = false;

    [Header("연결 (리그 구성 메뉴가 자동으로 채운다)")]
    [SerializeField] CinemachineCamera vcam;
    [SerializeField] CinemachineConfiner2D confiner2D;
    [SerializeField] CinemachineImpulseListener impulseListener;
    [SerializeField] CinemachineImpulseSource impulseSource;
    [SerializeField] Transform target;

    Coroutine _zoomCo;

    public float OrthoSize => vcam != null ? vcam.Lens.OrthographicSize : 0f;

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        Apply();
    }

    void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    /// <summary>토글 값을 실제 컴포넌트에 반영한다.</summary>
    public void Apply()
    {
        if (vcam != null) vcam.Follow = follow ? target : null;
        if (confiner2D != null) confiner2D.enabled = confiner;
        if (impulseListener != null) impulseListener.enabled = shake;
    }

    /// <summary>
    /// 직교 카메라 Size를 바꾼다.
    ///
    /// Cinemachine이 붙은 뒤에는 Main Camera의 Size를 직접 바꿔도 다음 프레임에 덮어써진다.
    /// 반드시 CinemachineCamera의 Lens를 바꿔야 한다.
    /// 또 Confiner2D는 "이 Size에서 카메라가 움직일 수 있는 영역"을 캐시해 두므로,
    /// Size를 바꾼 뒤 캐시를 비우지 않으면 경계 계산이 옛 Size 기준으로 남는다.
    /// </summary>
    public void SetOrthoSize(float size)
    {
        if (vcam == null) return;
        vcam.Lens.OrthographicSize = Mathf.Max(0.5f, size);
        if (confiner2D != null) confiner2D.InvalidateLensCache();
    }

    /// <summary>13주차 — 화면 흔들림. 5주차에는 꺼져 있어 아무 일도 하지 않는다.</summary>
    public void Shake(float force = 0.3f)
    {
        if (!shake || impulseSource == null) return;
        impulseSource.GenerateImpulseWithForce(force);
    }

    /// <summary>13주차 — 부드러운 줌. 5주차에는 꺼져 있어 아무 일도 하지 않는다.</summary>
    public void ZoomTo(float size, float duration = 0.4f)
    {
        if (!zoom || vcam == null) return;
        if (_zoomCo != null) StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(ZoomCo(size, duration));
    }

    IEnumerator ZoomCo(float to, float duration)
    {
        float from = OrthoSize, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetOrthoSize(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetOrthoSize(to);
        _zoomCo = null;
    }
}
