using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 플래시
/// [v6 fix] MaterialPropertyBlock + SRP Batcher 경고 제거
///          → color fallback 방식으로 단순화
///          9주차 Shader Graph _FlashAmount 교체 예정
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitReceiver : MonoBehaviour
{
    [SerializeField] float flashDuration = 0.12f;
    [SerializeField] Color flashColor = Color.white;
    SpriteRenderer _sr; Coroutine _co;

    // Shader Graph 연동용 (9주차)
    static readonly int AID = Shader.PropertyToID("_FlashAmount");
    static readonly int CID = Shader.PropertyToID("_FlashColor");

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    public void OnHit()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co());
    }

    IEnumerator Co()
    {
        var original = _sr.color;
        _sr.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        _sr.color = original;
    }

    /// <summary>
    /// Shader Graph FlashAmount 프로퍼티 직접 제어 (9주차용)
    /// SRP Batcher 비호환 → 인스턴스 머티리얼 사용
    /// </summary>
    public void SetFlashAmount(float v)
    {
        // SRP Batcher 우회: 인스턴스 머티리얼로 변경
        var mat = _sr.material; // 자동 인스턴스화
        mat.SetFloat(AID, Mathf.Clamp01(v));
        mat.SetColor(CID, flashColor);
    }
}
