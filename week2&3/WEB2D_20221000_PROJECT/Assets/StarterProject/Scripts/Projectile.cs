using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    Rigidbody2D _rb; ObjectPool _pool; Vector2 _start; float _dmg, _range;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
    }

    public void Init(Vector2 dir, float spd, float dmg, float rng, ObjectPool pool = null)
    {
        _dmg = dmg; _range = rng; _pool = pool; _start = transform.position;
        // Unity 6000.5 대응: linearVelocity 유지
        _rb.linearVelocity = dir * spd;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        if (Vector2.Distance(_start, transform.position) >= _range) Retire();
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (!c.CompareTag("Enemy")) return;
        c.GetComponent<Health>()?.TakeDamage(_dmg);
        Retire();
    }

    void Retire()
    {
        _rb.linearVelocity = Vector2.zero;
        if (_pool != null) _pool.Return(gameObject);
        else gameObject.SetActive(false);
    }
}
