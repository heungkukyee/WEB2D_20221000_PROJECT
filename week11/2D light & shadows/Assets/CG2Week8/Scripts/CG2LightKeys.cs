using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 8주차 — Play 중 전·후 비교 키.
///   L = 방향 있는 조명 전부 끄기/켜기 (환경광만 남는다)
///   K = 그림자 전부 끄기/켜기
/// 같은 장면을 키 하나로 번갈아 보면 차이가 가장 잘 보인다.
/// </summary>
public class CG2LightKeys : MonoBehaviour
{
    bool _lightsOff, _shadowsOff;
    readonly Dictionary<Light2D, bool> _shadowWas = new();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.lKey.wasPressedThisFrame)
        {
            _lightsOff = !_lightsOff;
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include))
                if (l.lightType != Light2D.LightType.Global) l.enabled = !_lightsOff;
            Debug.Log($"[비교] L 키 — 방향 있는 조명 {(_lightsOff ? "끔 (환경광만)" : "켬")}");
        }
        if (kb.kKey.wasPressedThisFrame)
        {
            _shadowsOff = !_shadowsOff;
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            {
                if (l.lightType == Light2D.LightType.Global) continue;
                if (_shadowsOff) { _shadowWas[l] = l.shadowsEnabled; l.shadowsEnabled = false; }
                else if (_shadowWas.TryGetValue(l, out var was)) l.shadowsEnabled = was;
            }
            Debug.Log($"[비교] K 키 — 그림자 {(_shadowsOff ? "끔" : "켬")}");
        }
    }
}
