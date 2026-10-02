using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5주차 비교 측정용 전환 메뉴 — 같은 씬에서 조건 하나만 바꿔 Frame Debugger·Stats를 다시 잰다.
///
///   3-5. Canvas 합치기 ↔ 분리   정적 Canvas의 요소를 동적 Canvas로 옮겼다가 되돌린다. Play 중에도 된다.
///   5-1. Sprite Atlas 끄기 ↔ 켜기  Sprite Packer Mode를 Disabled ↔ Sprite Atlas V2로 바꾼다. Play 전에 누른다.
///
/// 두 메뉴 모두 "측정용 임시 상태"다. 측정이 끝나면 한 번 더 눌러 원래(분리 · 켬)로 돌려 놓는다.
/// 체크포인트 진단이 원래 상태가 아니면 ⚠로 알려 준다.
/// </summary>
public static class Week5Compare
{
    const string STATIC = "Canvas_HUD_Static";
    const string DYNAMIC = "Canvas_HUD_Dynamic";

    // 정적 Canvas에 들어가는 요소 이름(UIBuilder와 같다). 합친 뒤에도 이 이름으로 찾아 되돌린다.
    static readonly string[] StaticChildren = { "TopBar", "Icon_HP", "Icon_EXP", "Icon_Coin" };

    [MenuItem("CG2 Lab/5주차/3-5. (비교) Canvas 합치기 ↔ 분리", priority = 35)]
    static void ToggleMerge()
    {
        var dyn = GameObject.Find(DYNAMIC);
        var stat = FindIncludingInactive(STATIC);
        if (dyn == null || stat == null)
        {
            EditorUtility.DisplayDialog("Canvas 비교", "먼저 'CG2 Lab → 5주차 → 3. UI 구성'을 실행하세요.", "확인");
            return;
        }

        bool merged = !stat.activeSelf;
        int moved = 0;
        if (!merged)
        {
            // 분리 → 합침 : 정적 요소를 동적 Canvas 맨 앞(= 맨 뒤에 그려지는 배경 쪽)으로 옮긴다.
            var kids = new List<Transform>();
            foreach (Transform t in stat.transform) kids.Add(t);
            foreach (var t in kids)
            {
                Undo.SetTransformParent(t, dyn.transform, "Canvas 합치기");
                t.SetSiblingIndex(moved++);
            }
            Undo.RecordObject(stat, "Canvas 합치기");
            stat.SetActive(false);
        }
        else
        {
            foreach (var name in StaticChildren)
            {
                var t = dyn.transform.Find(name);
                if (t == null) continue;
                Undo.SetTransformParent(t, stat.transform, "Canvas 분리");
                t.SetSiblingIndex(moved++);
            }
            Undo.RecordObject(stat, "Canvas 분리");
            stat.SetActive(true);
        }
        MarkDirty();

        Debug.Log($"[검증] Canvas {(merged ? "분리 (원래 상태)" : "합침 (비교용)")} — 요소 {moved}개 이동\n" +
                  (merged
                      ? "        Canvas_HUD_Static 다시 켬 → Canvas 3개 구조로 복귀했습니다."
                      : "        Canvas_HUD_Static 끔 → 배경·아이콘이 Canvas_HUD_Dynamic 안에 들어갔습니다.\n" +
                        "        이제 HP바·시간이 바뀔 때마다 배경·아이콘까지 함께 다시 계산된다.") +
                  "\n        ── 측정 ── 일시정지 → Frame Debugger의 Canvas.RenderOverlays 수 · Stats DrawCalls/SetPass 기록" +
                  (EditorApplication.isPlaying ? "\n        ※ Play 중 변경은 Stop하면 원래대로 돌아갑니다." :
                   merged ? "" : "\n        ※ 측정 후 이 메뉴를 한 번 더 눌러 분리 상태로 되돌리세요."));
    }

    [MenuItem("CG2 Lab/5주차/5-1. (비교) Sprite Atlas 끄기 ↔ 켜기", priority = 51)]
    static void ToggleAtlas()
    {
        if (EditorApplication.isPlaying)
        {
            // 대화상자 대신 로그로 안내한다 — 측정 중 흐름을 끊지 않기 위함이다.
            Debug.LogWarning("[CG2] Sprite Atlas 끄기/켜기는 Stop한 뒤 누르세요. 아틀라스 사용 여부는 Play를 시작할 때 정해집니다.");
            return;
        }
        bool on = EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2 ||
                  EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2Build;
        EditorSettings.spritePackerMode = on ? SpritePackerMode.Disabled : SpritePackerMode.SpriteAtlasV2;
        bool exists = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets/CG2Week5" }).Length > 0;

        Debug.Log($"[검증] Sprite Atlas {(on ? "끔 (비교용)" : "켬 (원래 상태)")} — Sprite Packer Mode = {EditorSettings.spritePackerMode}\n" +
                  (on
                      ? "        스프라이트가 각자의 원본 텍스처로 그려집니다. Play → 일시정지 → 측정하세요.\n" +
                        "        ⚠ 이 상태로 빌드하면 아틀라스가 빠집니다. 측정 후 반드시 한 번 더 눌러 켜세요."
                      : exists
                          ? "        아틀라스(Atlas_World · Atlas_UI)로 그려집니다. Play → 일시정지 → 측정하세요."
                          : "        아직 아틀라스가 없습니다. 'CG2 Lab → 5주차 → 5. Sprite Atlas 재적용'을 실행하세요."));
    }

    static GameObject FindIncludingInactive(string name)
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static void MarkDirty()
    {
        if (!EditorApplication.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
