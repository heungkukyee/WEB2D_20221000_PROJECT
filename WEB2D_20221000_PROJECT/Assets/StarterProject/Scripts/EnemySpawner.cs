using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 적 스폰 — 플레이어 주변 원둘레에 일정 간격으로 생성한다.
///
/// [v8] 스폰 위치 검사 추가
///   기존에는 플레이어 주변 반경 spawnRadius 원 위에 무조건 배치했다.
///   맵에 벽이 생기면서 두 가지 문제가 드러났다.
///     · 플레이어가 가장자리에 있으면 원의 일부가 바깥 벽 너머로 나가 적이 갇힌다
///     · 안쪽 기둥 위에 스폰되면 벽에 박힌 채로 밀려나온다
///   이제 후보 위치를 여러 번 뽑아 "맵 안이고 벽이 아닌" 자리에만 배치한다.
///   타일맵이 없는 씬(2주차 기본 상태)에서는 범위 제한이 걸리지 않아 기존 동작과 같다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] float spawnInterval=2f,minSpawnInterval=0.3f,spawnRadius=13f; [SerializeField] int maxEnemies=50;

    [Header("스폰 위치 검사 (v8)")]
    [Tooltip("유효한 자리를 찾기 위해 시도할 횟수. 시도할수록 반경을 조금씩 줄여 안쪽을 노린다.")]
    [SerializeField] int spawnAttempts = 12;
    [Tooltip("이 반경 안에 벽이 있으면 그 자리는 쓰지 않는다. 적 콜라이더 크기에 맞춘다.")]
    [SerializeField] float clearRadius = 0.45f;
    [Tooltip("맵 가장자리에서 이만큼 안쪽에만 스폰한다. 바깥 벽 두께보다 커야 한다.")]
    [SerializeField] float edgeMargin = 2f;

    ObjectPool _pool; Transform _player;
    Bounds _area; bool _hasArea;

    void Start(){var p=GameObject.FindGameObjectWithTag("Player");if(p)_player=p.transform;_pool=gameObject.AddComponent<ObjectPool>();_pool.Initialize(enemyPrefab,25);CacheSpawnArea();StartCoroutine(SpawnLoop());StartCoroutine(DiffLoop());}
    IEnumerator SpawnLoop(){while(true){yield return new WaitForSeconds(spawnInterval);if(_player&&_pool.ActiveCount<maxEnemies)Spawn();}}
    IEnumerator DiffLoop(){while(true){yield return new WaitForSeconds(60f);spawnInterval=Mathf.Max(minSpawnInterval,spawnInterval-0.05f);}}

    void Spawn()
    {
        if (!TryFindSpawnPos(out var pos)) return;   // 자리를 못 찾으면 이번 회차는 거른다
        var go = _pool.Get(); if (go == null) return;
        go.transform.position = pos;
        go.GetComponent<Health>()?.Heal(99999f);
    }

    /// <summary>
    /// 씬의 타일맵 전체를 감싸는 범위를 스폰 가능 영역으로 잡는다.
    /// 타일맵이 없으면 범위 제한을 걸지 않는다 — 2주차 기본 씬에서 기존 동작을 유지하기 위함이다.
    /// </summary>
    void CacheSpawnArea()
    {
        foreach (var map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            var b = map.localBounds;
            b.center += map.transform.position;
            if (!_hasArea) { _area = b; _hasArea = true; }
            else _area.Encapsulate(b);
        }
        // 바깥 벽 안쪽으로 물린다. Expand는 size에 더하므로 각 변에서 절반씩 줄어든다.
        if (_hasArea) _area.Expand(new Vector3(-edgeMargin * 2f, -edgeMargin * 2f, 0f));
    }

    /// <summary>
    /// 각도를 무작위로 바꿔가며 유효한 자리를 찾는다.
    /// 시도가 거듭될수록 반경을 줄여 플레이어 쪽으로 당긴다 —
    /// 맵이 좁거나 플레이어가 구석에 몰려도 결국 자리를 찾게 하기 위함이다.
    /// </summary>
    bool TryFindSpawnPos(out Vector2 pos)
    {
        for (int i = 0; i < spawnAttempts; i++)
        {
            float t = spawnAttempts > 1 ? i / (float)(spawnAttempts - 1) : 0f;
            float r = spawnRadius * Mathf.Lerp(1f, 0.45f, t);
            float a = Random.Range(0f, Mathf.PI * 2f);
            pos = (Vector2)_player.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (IsValid(pos)) return true;
        }
        pos = default; return false;
    }

    bool IsValid(Vector2 pos)
    {
        if (_hasArea && (pos.x < _area.min.x || pos.x > _area.max.x ||
                         pos.y < _area.min.y || pos.y > _area.max.y)) return false;

        // 벽은 Static Rigidbody2D를 쓴다. 플레이어와 적은 Dynamic이므로
        // bodyType만 보면 "지형인가"를 태그나 레이어 설정 없이 구분할 수 있다.
        foreach (var hit in Physics2D.OverlapCircleAll(pos, clearRadius))
        {
            var rb = hit.attachedRigidbody;
            if (rb != null && rb.bodyType == RigidbodyType2D.Static) return false;
        }
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!_hasArea) return;
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireCube(_area.center, new Vector3(_area.size.x, _area.size.y, 0f));
    }
#endif
}
