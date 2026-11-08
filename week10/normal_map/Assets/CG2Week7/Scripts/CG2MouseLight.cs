using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 7주차 4단계 — 마우스를 따라다니는 테스트 조명 (횃불을 들고 비춰 보기).
///   Play 중 마우스 위치로 이동 · N 키 = 노멀 맵 반응 켜기/끄기(Normal Maps 설정) · 휠 = 조명 높이(Normal Map Distance)
/// 편집 모드에서는 아무것도 하지 않는다 → 씬 뷰에서 이동 도구(W)로 직접 끌어 본다.
/// 조명이 시간에 따라 바뀌는 연출(횃불 깜빡임 · 그림자)은 8주차에 다룬다.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class CG2MouseLight : MonoBehaviour
{
    public bool followMouse = true;
    [Tooltip("N 키로 끌 때 되돌아갈 품질")]
    public Light2D.NormalMapQuality onQuality = Light2D.NormalMapQuality.Accurate;

    Light2D _light;

    // Light2D.normalMapQuality · normalMapDistance는 코드에서 읽기 전용이다(Inspector에서 정하라는 뜻).
    // 수업용 키 조작을 위해 직렬화 필드를 직접 바꾼다.
    static readonly System.Reflection.FieldInfo QualityField =
        typeof(Light2D).GetField("m_NormalMapQuality", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    static readonly System.Reflection.FieldInfo DistanceField =
        typeof(Light2D).GetField("m_NormalMapDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    void Awake() => _light = GetComponent<Light2D>();

    void Update()
    {
        var mouse = Mouse.current;
        var cam = Camera.main;
        if (followMouse && mouse != null && cam != null)
        {
            Vector3 p = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && DistanceField != null)
            {
                float d = Mathf.Clamp(_light.normalMapDistance + Mathf.Sign(wheel) * 0.25f, 0.25f, 10f);
                DistanceField.SetValue(_light, d);
                Debug.Log($"[테스트 조명] 조명 높이(Normal Map Distance) → {d:0.00}");
            }
        }
        var kb = Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame && QualityField != null)
        {
            var next = _light.normalMapQuality == Light2D.NormalMapQuality.Disabled ? onQuality : Light2D.NormalMapQuality.Disabled;
            QualityField.SetValue(_light, next);
            Debug.Log($"[테스트 조명] Normal Maps → {next}");
        }
    }
}
