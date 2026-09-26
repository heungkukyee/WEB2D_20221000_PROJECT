using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 3주차 드로우콜 측정 실습 — Sprite Atlas 적용 전/후 비교용.
///
/// 같은 개체 수에서 "텍스처 종류"만 바꿔가며 Batches 변화를 관찰한다.
///   Single : 모든 개체가 같은 스프라이트 1장 → 텍스처 1개
///   Random : 개체마다 다른 스프라이트 배정    → 텍스처 여러 개
/// 개체 수는 그대로인데 Batches가 폭증하는 것을 보는 것이 이 실습의 핵심이다.
///
/// 배치와 스프라이트 배정에 고정 시드를 쓰므로 누가 실행해도 같은 수치가 나온다.
/// 사용법: Inspector에서 값을 정하고 Play → Game View 우상단 Stats 확인 → Stop → 값 변경.
/// </summary>
public class DrawCallLab : MonoBehaviour
{
    public enum TextureMode { Single, Random }

    [Header("측정 조건")]
    [Tooltip("화면에 배치할 스프라이트 개수")]
    [SerializeField] int count = 500;

    [Tooltip("Single = 전부 같은 텍스처 / Random = 개체마다 다른 텍스처")]
    [SerializeField] TextureMode mode = TextureMode.Single;

    [Tooltip("Sorting Order를 개체마다 다르게 부여해 Unity가 텍스처끼리 묶어 정렬하지 " +
             "못하게 막는다. 배칭이 더 크게 깨진다. (PDF 17p 배칭 성립 조건 참고)")]
    [SerializeField] bool forceInterleave = false;

    [Header("렌더링")]
    [Tooltip("SRP Batcher — 셰이더 상태 설정 비용을 줄인다. 드로우콜 수 자체는 줄이지 않는다. (PDF 20p)")]
    [SerializeField] bool srpBatcher = true;

    [Tooltip("URP Asset의 Dynamic Batching. URP 기본값은 꺼짐이다. " +
             "다만 이 기법은 작은 '메시'를 합치는 것이라 SpriteRenderer는 애초에 대상이 아니다. " +
             "켜도 스프라이트에는 변화가 없는 것이 정상이다.")]
    [SerializeField] bool dynamicBatching = false;

    [Tooltip("스프라이트마다 같은 셰이더의 별도 Material 인스턴스를 부여한다. " +
             "머티리얼이 하나뿐이면 SRP Batcher가 줄일 상태 전환 자체가 없다. " +
             "이 옵션을 켜야 SRP Batcher의 효과(SetPass 감소)가 숫자로 드러난다.")]
    [SerializeField] bool materialVariants = false;

    [Header("배치")]
    [SerializeField] float spacing = 0.8f;
    [SerializeField] int randomSeed = 20260301;

    [Header("스프라이트 목록")]
    [Tooltip("비어 있으면 Play 시 아래 폴더에서 자동으로 수집한다.")]
    [SerializeField] string spriteFolder = "Assets/Sprites";
    [SerializeField] Sprite[] palette = new Sprite[0];

    [Header("화면 표시")]
    [Tooltip("켜면 조건이 화면에 표시되지만, 오버레이 자체가 드로우콜과 삼각형 수를 더한다. " +
             "정밀 측정 시에는 꺼둘 것. 조건과 측정값은 Console에도 한 줄로 출력된다.")]
    [SerializeField] bool showOverlay = false;

    int _distinctTextures;
    int _materialCount;
    long _textureBytes;
    float _fps;

    // materialVariants로 생성한 머티리얼은 직접 정리해야 한다.
    readonly List<Material> _madeMaterials = new();
#if UNITY_EDITOR
    int _frame;
#endif

    // 원복용 — URP 에셋을 영구히 바꾸지 않기 위해 원래 값을 기억한다.
    object _rpAsset;
    readonly List<KeyValuePair<PropertyInfo, bool>> _rpOverrides = new();

    // 렌더링 설정은 첫 프레임이 그려지기 전에 적용해야 한다.
    void Awake() { ApplyPipelineToggles(); }

    void Start() { Build(); }

    void ApplyPipelineToggles()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null)
        {
            Debug.LogWarning("[DrawCallLab] 렌더 파이프라인 에셋을 찾을 수 없습니다.");
            return;
        }
        _rpAsset = rp;

        // URP는 파이프라인을 생성할 때 GraphicsSettings 값을 에셋 값으로 덮어쓴다.
        // 따라서 GraphicsSettings가 아니라 에셋 속성을 바꿔야 실제로 적용된다.
        SetPipelineBool(rp, "useSRPBatcher", srpBatcher);
        SetPipelineBool(rp, "supportsDynamicBatching", dynamicBatching);
        GraphicsSettings.useScriptableRenderPipelineBatching = srpBatcher;

        // 요청값이 아니라 에셋에서 되읽은 값을 찍는다. 여기가 요청과 다르면 적용 실패다.
        Debug.Log($"[검증] 파이프라인 설정 적용 결과 — " +
                  $"useSRPBatcher = {ReadPipelineBool(rp, "useSRPBatcher")} (요청 {srpBatcher}) · " +
                  $"supportsDynamicBatching = {ReadPipelineBool(rp, "supportsDynamicBatching")} (요청 {dynamicBatching})");
    }

    static string ReadPipelineBool(object target, string propertyName)
    {
        var prop = target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return prop == null ? "읽기 실패" : prop.GetValue(target).ToString();
    }

    void SetPipelineBool(object target, string propertyName, bool value)
    {
        // URP 어셈블리를 직접 참조하지 않기 위해 리플렉션으로 접근한다.
        var prop = target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop == null || prop.PropertyType != typeof(bool) || !prop.CanRead || !prop.CanWrite)
        {
            Debug.LogWarning($"[DrawCallLab] '{propertyName}' 을(를) 코드에서 바꿀 수 없습니다. " +
                             "URP Asset(Assets/Settings/UniversalRP.asset)에서 직접 설정하세요.");
            return;
        }
        _rpOverrides.Add(new KeyValuePair<PropertyInfo, bool>(prop, (bool)prop.GetValue(target)));
        prop.SetValue(target, value);
    }

    void OnDestroy()
    {
        if (_rpAsset != null)
            foreach (var kv in _rpOverrides) kv.Key.SetValue(_rpAsset, kv.Value);
        _rpOverrides.Clear();

        foreach (var m in _madeMaterials) if (m != null) Destroy(m);
        _madeMaterials.Clear();
    }

    void Update()
    {
        _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        // 수치가 안정된 뒤 한 번만 기록한다. 이 줄을 그대로 보고서에 옮겨 적으면 된다.
        if (_frame++ != 120) return;
        Debug.Log(
            $"[측정] 개체 {count} · {mode}{(forceInterleave ? "+Interleave" : "")} · " +
            $"SRP Batcher {(srpBatcher ? "ON" : "OFF")} · " +
            $"Dynamic Batching {(dynamicBatching ? "ON" : "OFF")} · " +
            $"머티리얼 {_materialCount}개\n" +
            $"        DrawCalls {UnityEditor.UnityStats.drawCalls} · " +
            $"SetPass {UnityEditor.UnityStats.setPassCalls} · " +
            $"Tris {UnityEditor.UnityStats.triangles}\n" +
            $"        스프라이트가 참조하는 텍스처 {_distinctTextures}종 · " +
            $"약 {_textureBytes / 1024f:F0} KB   ← Atlas가 적용되면 1종으로 떨어진다");
#endif
    }

    void Build()
    {
        if (palette == null || palette.Length == 0) CollectFromFolder();
        if (palette == null || palette.Length == 0)
        {
            Debug.LogError($"[DrawCallLab] 스프라이트를 찾지 못했습니다. " +
                           $"'{spriteFolder}' 에 스프라이트가 임포트되어 있는지 확인하세요.");
            return;
        }

        var rng = new System.Random(randomSeed);
        var used = new HashSet<Texture>();
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt(count / (float)cols);
        float originX = -(cols - 1) * spacing * 0.5f;
        float originY = (rows - 1) * spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            // Single 모드는 목록의 첫 번째 스프라이트 하나만 사용한다.
            var sprite = mode == TextureMode.Single ? palette[0] : palette[rng.Next(palette.Length)];

            var go = new GameObject($"Sprite_{i:D4}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                originX + (i % cols) * spacing,
                originY - (i / cols) * spacing, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (forceInterleave) sr.sortingOrder = i;

            if (materialVariants)
            {
                // 셰이더는 그대로 두고 머티리얼 인스턴스만 개체마다 분리한다.
                // SRP Batcher가 없으면 머티리얼마다 SetPass가 발생하고, 켜면 한 배치로 묶인다.
                var mat = new Material(sr.sharedMaterial) { name = $"SpriteMat_{i:D4}" };
                if (mat.HasProperty("_Color"))
                {
                    float t = i / Mathf.Max(1f, count - 1f);
                    mat.SetColor("_Color", Color.Lerp(Color.white, new Color(1f, 0.8f, 0.8f), t));
                }
                sr.sharedMaterial = mat;
                _madeMaterials.Add(mat);
            }

            if (sprite != null && sprite.texture != null) used.Add(sprite.texture);
        }

        // 머티리얼이 실제로 붙었는지 확인한다. 생성 개수가 아니라 렌더러에 붙은 것을 센다.
        var appliedMaterials = new HashSet<Material>();
        foreach (Transform child in transform)
        {
            var csr = child.GetComponent<SpriteRenderer>();
            if (csr != null && csr.sharedMaterial != null) appliedMaterials.Add(csr.sharedMaterial);
        }
        _materialCount = appliedMaterials.Count;

        if (transform.childCount > 0)
        {
            var firstMat = transform.GetChild(0).GetComponent<SpriteRenderer>().sharedMaterial;
            Debug.Log($"[검증] 첫 스프라이트 머티리얼 = '{firstMat.name}' · 셰이더 '{firstMat.shader.name}' · " +
                      $"렌더러에 실제로 붙은 머티리얼 {_materialCount}종 " +
                      $"(materialVariants {(materialVariants ? "ON" : "OFF")})");
        }

        _distinctTextures = used.Count;
        _textureBytes = 0;
        foreach (var t in used) _textureBytes += (long)t.width * t.height * 4; // RGBA32 기준 추정

        FitCamera(cols, rows);
    }

    void FitCamera(int cols, int rows)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float halfH = rows * spacing * 0.5f + spacing;
        float halfW = (cols * spacing * 0.5f + spacing) / Mathf.Max(cam.aspect, 0.0001f);
        cam.orthographicSize = Mathf.Max(halfH, halfW);
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    void OnValidate()
    {
        count = Mathf.Clamp(count, 1, 5000);
        spacing = Mathf.Max(0.05f, spacing);
    }

    void CollectFromFolder()
    {
#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(spriteFolder))
        {
            Debug.LogError($"[DrawCallLab] 폴더가 없습니다: {spriteFolder}");
            return;
        }
        var found = new List<Sprite>();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) found.Add(sprite);
        }
        // 이름순 정렬 — 누가 실행해도 palette[0]과 랜덤 배정 결과가 같아야 한다.
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        palette = found.ToArray();
        Debug.Log($"[DrawCallLab] 스프라이트 {palette.Length}장 수집 완료 ({spriteFolder})");
#endif
    }

    void OnGUI()
    {
        if (!showOverlay) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
        const int w = 330, h = 140;
        GUI.Box(new Rect(10, 10, w, h), GUIContent.none);
        GUI.Label(new Rect(24, 20, w - 24, h), string.Join("\n", new[]
        {
            $"개체 수  :  {count}",
            $"모드  :  {mode}" + (forceInterleave ? "  +Interleave" : ""),
            $"SRP Batcher  :  {(srpBatcher ? "ON" : "OFF")}   Dynamic  :  {(dynamicBatching ? "ON" : "OFF")}",
            $"머티리얼  :  {_materialCount}개",
            $"서로 다른 텍스처  :  {_distinctTextures}",
            $"FPS  :  {_fps:F1}",
            "",
            "Game View 우상단 Stats 에서",
            "Batches / SetPass Calls 확인",
        }), style);
    }
}
