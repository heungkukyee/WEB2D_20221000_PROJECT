#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 새로 추가되는 스프라이트에 CG2 규격을 자동으로 적용한다.
///
/// 왜 필요한가 —
/// Unity의 PPU 기본값은 100이다. 이 프로젝트의 규격은 64다(Docs/Week4/00_규격확정서.md §1).
/// 그래서 아트를 추가할 때마다 누군가 기억하고 손으로 고쳐야 하는데, 2주차에 바로 그걸
/// 놓쳐서 스프라이트가 콜라이더보다 1.4~1.7배 작아지는 문제가 생겼다.
/// 사람이 기억해야 하는 절차는 언젠가 빠진다. 임포트 시점에 자동으로 박아넣는다.
///
/// 적용 범위 — "처음 임포트되는" 텍스처만 건드린다.
///   importSettingsMissing == true 는 .meta 파일이 아직 없다는 뜻이다.
///   즉 폴더에 막 떨어뜨린 새 파일에만 적용되고,
///   이미 프로젝트에 있는 에셋이나 사용자가 손으로 바꾼 설정은 절대 덮어쓰지 않는다.
///   3주차 sprites_assert(이미 PPU 64)를 임포트해도 그 설정이 그대로 유지된다.
///
/// 끄는 법: CG2 Lab → 4주차 준비 → 스프라이트 자동 규격 (토글)
/// </summary>
public class CG2SpriteImportDefaults : AssetPostprocessor
{
    const string PREF_KEY = "CG2.SpriteImportDefaults.Enabled";
    const string MENU = "CG2 Lab/4주차 준비/스프라이트 자동 규격 적용";

    const int TARGET_PPU = 64;

    // 이 접두사로 시작하는 파일은 타일로 간주해 더 엄격한 규격을 적용한다.
    const string TILE_PREFIX = "tile_";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PREF_KEY, true);
        set => EditorPrefs.SetBool(PREF_KEY, value);
    }

    [MenuItem(MENU, priority = 20)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Debug.Log($"[CG2] 스프라이트 자동 규격 적용 — {(Enabled ? "켬" : "끔")}\n" +
                  (Enabled
                      ? $"        앞으로 추가되는 스프라이트에 PPU {TARGET_PPU} · Point 필터가 자동 적용됩니다."
                      : "        Unity 기본값(PPU 100)이 적용됩니다. 나중에 '1. 스프라이트 PPU 64로 통일'을 실행하세요."));
    }

    [MenuItem(MENU, validate = true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MENU, Enabled);
        return true;
    }

    void OnPreprocessTexture()
    {
        if (!Enabled) return;

        var ti = (TextureImporter)assetImporter;

        // 핵심 조건 — 이미 .meta가 있는 에셋은 손대지 않는다.
        // 이게 없으면 사용자가 의도적으로 바꾼 설정을 재임포트마다 덮어쓰게 된다.
        if (!ti.importSettingsMissing) return;

        // 폰트 아틀라스 등은 대상이 아니다.
        if (assetPath.Contains("/TextMesh Pro/")) return;
        if (ti.textureType != TextureImporterType.Sprite &&
            ti.textureType != TextureImporterType.Default) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = TARGET_PPU;
        ti.filterMode = FilterMode.Point;      // 픽셀아트 공통
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;              // 2D 직교 카메라에서 불필요

        if (!System.IO.Path.GetFileName(assetPath).StartsWith(TILE_PREFIX)) return;

        // 타일만 추가 규격. 캐릭터는 Tight 메시가 오버드로우 측면에서 유리하므로 그대로 둔다.
        // 타일은 Full Rect여야 정점 수가 고정되고 seam이 생기지 않는다.
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteExtrude = 0;
        ti.SetTextureSettings(s);

        var ps = ti.GetDefaultPlatformTextureSettings();
        ps.format = TextureImporterFormat.RGBA32;
        ps.textureCompression = TextureImporterCompression.Uncompressed;
        ps.overridden = true;
        ti.SetPlatformTextureSettings(ps);
    }
}
#endif
