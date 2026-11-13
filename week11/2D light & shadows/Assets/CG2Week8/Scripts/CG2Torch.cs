using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 8주차 — 횃불 깜빡임. 세기와 반경을 느린 잡음으로 조금씩 흔든다.
/// 측정 메뉴는 이 컴포넌트를 잠시 꺼서 값을 고정한다(꺼지면 원래 값으로 돌아간다).
/// </summary>
[RequireComponent(typeof(Light2D))]
public class CG2Torch : MonoBehaviour
{
    [Range(0, 0.5f)] public float flicker = 0.15f;
    public float speed = 6f;

    Light2D _light;
    float _intensity, _radius, _seed;

    void OnEnable()
    {
        _light = GetComponent<Light2D>();
        _intensity = _light.intensity;
        _radius = _light.pointLightOuterRadius;
        _seed = Random.value * 100f;
    }

    void OnDisable()
    {
        if (_light == null) return;
        _light.intensity = _intensity;
        _light.pointLightOuterRadius = _radius;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f;
        _light.intensity = _intensity * (1f + n * flicker);
        _light.pointLightOuterRadius = _radius * (1f + n * flicker * 0.3f);
    }
}
