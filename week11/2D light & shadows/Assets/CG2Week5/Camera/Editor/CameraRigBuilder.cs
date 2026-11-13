using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 5주차 1단계 — 카메라 리그를 씬에 구성한다.
///
/// 4주차 CG2CinemachineInstaller가 안내하던 "카메라 리그 구성" 메뉴의 실체가 이 파일이다.
/// Cinemachine이 설치된 뒤에만 컴파일되므로(CG2.Camera.Editor 어셈블리), 미설치 상태에서는
/// 메뉴 자체가 보이지 않는다. 5주차 메뉴(CG2 Lab → 5주차 → 1)가 설치 여부를 먼저 확인해 준다.
///
/// 구성 결과
///   Main Camera           CinemachineBrain 추가 · 기존 CameraFollow는 끈다(지우지 않는다)
///   CG2 CameraBounds      PolygonCollider2D(Trigger) — 4주차 게임 맵 크기에 맞춘 경계
///   CG2 CameraRig         CG2CameraRig · CinemachineImpulseSource
///     └ CM FollowCam      CinemachineCamera · PositionComposer · Confiner2D · ImpulseListener(끔)
/// </summary>
public static class CameraRigBuilder
{
    const string RIG_NAME = "CG2 CameraRig";
    const string VCAM_NAME = "CM FollowCam";
    const string BOUNDS_NAME = "CG2 CameraBounds";   // 4주차 GameMapBuilder가 이 이름으로 찾는다
    const string MAP_NAME = "CG2 GameMap";
    const float DEFAULT_SIZE = 7f;

    [MenuItem("CG2 Lab/시네머신/카메라 리그 구성", priority = 100)]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("카메라 리그", "Play를 멈춘 뒤 실행하세요. Play 중에 만든 리그는 Stop과 함께 사라집니다.", "확인");
            return;
        }
        var cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("카메라 리그", "MainCamera 태그가 붙은 카메라가 없습니다.", "확인");
            return;
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("카메라 리그",
                "Player 태그 오브젝트가 없습니다.\n2주차 'CG2 Starter → Step 3'으로 씬을 먼저 구성하세요.", "확인");
            return;
        }

        Undo.SetCurrentGroupName("CG2 카메라 리그 구성");
        int group = Undo.GetCurrentGroup();

        var old = GameObject.Find(RIG_NAME);
        if (old != null) Undo.DestroyObjectImmediate(old);

        // ── Main Camera ────────────────────────────────────────────
        if (!cam.TryGetComponent<CinemachineBrain>(out _)) Undo.AddComponent<CinemachineBrain>(cam.gameObject);

        // 2주차 CameraFollow는 Lerp로 카메라를 직접 움직인다. Brain과 함께 켜 두면
        // 두 스크립트가 매 프레임 서로의 위치를 덮어써 화면이 떨린다. 지우지 않고 끄기만 한다.
        var legacy = cam.GetComponent("CameraFollow") as Behaviour;
        if (legacy != null)
        {
            Undo.RecordObject(legacy, "CameraFollow 끄기");
            legacy.enabled = false;
        }

        float size = cam.orthographic ? cam.orthographicSize : DEFAULT_SIZE;

        // ── 경계 ───────────────────────────────────────────────────
        var bounds = EnsureBounds(cam, size, out string boundsNote);

        // ── 리그 ───────────────────────────────────────────────────
        var rigGo = new GameObject(RIG_NAME);
        Undo.RegisterCreatedObjectUndo(rigGo, "리그 생성");
        var impulseSource = rigGo.AddComponent<CinemachineImpulseSource>();

        var vcamGo = new GameObject(VCAM_NAME);
        Undo.RegisterCreatedObjectUndo(vcamGo, "가상 카메라 생성");
        vcamGo.transform.SetParent(rigGo.transform, false);
        vcamGo.transform.position = player.transform.position + new Vector3(0f, 0f, -10f);

        var vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Lens = LensSettings.Default;
        vcam.Lens.OrthographicSize = size;
        vcam.Lens.NearClipPlane = 0.3f;
        vcam.Lens.FarClipPlane = 1000f;
        vcam.Follow = player.transform;

        var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
        composer.CameraDistance = 10f;
        // 2주차 CameraFollow(smoothSpeed 8)와 비슷한 체감이 나도록 약간의 지연만 둔다.
        composer.Damping = new Vector3(0.3f, 0.3f, 0f);

        var confiner = vcamGo.AddComponent<CinemachineConfiner2D>();
        confiner.BoundingShape2D = bounds;
        confiner.Damping = 0f;

        var listener = vcamGo.AddComponent<CinemachineImpulseListener>();
        listener.enabled = false;   // 13주차에 켠다

        var rig = rigGo.AddComponent<CG2CameraRig>();
        var so = new SerializedObject(rig);
        so.FindProperty("vcam").objectReferenceValue = vcam;
        so.FindProperty("confiner2D").objectReferenceValue = confiner;
        so.FindProperty("impulseListener").objectReferenceValue = listener;
        so.FindProperty("impulseSource").objectReferenceValue = impulseSource;
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Selection.activeGameObject = rigGo;

        Debug.Log(
            "[검증] 카메라 리그 구성 완료\n" +
            $"        Main Camera : CinemachineBrain ✓ · CameraFollow {(legacy != null ? "꺼짐 ✓" : "없음")}\n" +
            $"        {VCAM_NAME} : Follow = {player.name} · Lens Size = {size} · PositionComposer Damping 0.3\n" +
            $"        Confiner2D  : {BOUNDS_NAME} · {boundsNote}\n" +
            "        켜진 기능 : Follow ✓ · Confiner ✓  /  꺼진 기능 : Shake · Zoom (13주차)\n" +
            "        ── Play 해서 확인 ──\n" +
            "        ① 플레이어를 따라가는가   ② 맵 끝에서 카메라가 멈추고 벽 바깥(검은 배경)이 안 보이는가");
    }

    [MenuItem("CG2 Lab/시네머신/되돌리기 — 카메라 리그 제거", priority = 101)]
    public static void Remove()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[CG2] Play를 멈춘 뒤 실행하세요."); return; }
        var rig = GameObject.Find(RIG_NAME);
        if (rig != null) Undo.DestroyObjectImmediate(rig);

        var cam = Camera.main;
        if (cam != null)
        {
            if (cam.TryGetComponent<CinemachineBrain>(out var brain)) Undo.DestroyObjectImmediate(brain);
            var legacy = cam.GetComponent("CameraFollow") as Behaviour;
            if (legacy != null) { Undo.RecordObject(legacy, "CameraFollow 켜기"); legacy.enabled = true; }
        }
        Debug.Log("[검증] 1단계 카메라 리그 되돌리기 완료 — CG2 CameraRig · CinemachineBrain 제거\n" +
                  "        2주차 CameraFollow를 다시 켰습니다. 경계(CG2 CameraBounds)는 남겨 두었습니다.\n" +
                  "        '1. 카메라 리그 적용'으로 다시 구성할 수 있습니다.");
    }

    /// <summary>
    /// 맵 크기에 맞는 경계 폴리곤을 만든다(있으면 갱신한다).
    ///
    /// 경계가 카메라 시야보다 작으면 Confiner2D가 풀 수 없는 문제가 된다 —
    /// 카메라를 경계 안에 넣을 방법이 없으니 위치가 보장되지 않는다.
    /// 4주차 GameMapBuilder와 같은 규칙으로, 그때는 경계를 시야만큼 넓힌다.
    /// </summary>
    static PolygonCollider2D EnsureBounds(Camera cam, float size, out string note)
    {
        var go = GameObject.Find(BOUNDS_NAME);
        if (go == null)
        {
            go = new GameObject(BOUNDS_NAME);
            Undo.RegisterCreatedObjectUndo(go, "카메라 경계 생성");
        }
        // 물리 레이캐스트에 걸리지 않게 한다. 트리거라 충돌은 없지만 쿼리 결과를 어지럽히지 않도록.
        go.layer = 2; // Ignore Raycast

        var poly = go.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = Undo.AddComponent<PolygonCollider2D>(go);
        Undo.RecordObject(poly, "카메라 경계 갱신");
        poly.isTrigger = true;

        Rect r;
        var map = GameObject.Find(MAP_NAME);
        var ground = map != null ? map.GetComponentInChildren<Tilemap>() : null;
        if (ground != null)
        {
            ground.CompressBounds();
            var cb = ground.cellBounds;
            Vector3 min = ground.CellToWorld(cb.min);
            Vector3 max = ground.CellToWorld(cb.max);
            r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            note = $"게임 맵 {cb.size.x}×{cb.size.y}타일 기준 {r.width:F1}×{r.height:F1}유닛";
        }
        else
        {
            r = Rect.MinMaxRect(-15f, -15f, 15f, 15f);
            note = "⚠ 게임 맵이 없어 ±15유닛 기본 경계 사용 (4주차 7. 게임 맵을 먼저 까세요)";
        }

        float needW = size * cam.aspect, needH = size;
        if (r.width < needW * 2f || r.height < needH * 2f)
        {
            float w = Mathf.Max(r.width, needW * 2f + 1f), h = Mathf.Max(r.height, needH * 2f + 1f);
            r = new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
            note += $" → ⚠ 시야보다 작아 {w:F1}×{h:F1}로 확장";
        }

        poly.points = new[]
        {
            new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
            new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
        };
        EditorUtility.SetDirty(poly);
        return poly;
    }
}
