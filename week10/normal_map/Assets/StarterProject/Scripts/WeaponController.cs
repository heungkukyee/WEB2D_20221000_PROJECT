using UnityEngine;
public class WeaponController : MonoBehaviour
{
    [SerializeField] GameObject projectilePrefab; [SerializeField] float fireRate=1f,projSpeed=9f,damage=25f,range=9f;
    ObjectPool _pool; float _timer;
    void Start(){_pool=gameObject.AddComponent<ObjectPool>();_pool.Initialize(projectilePrefab,20);}
    void Update(){_timer+=Time.deltaTime;if(_timer>=1f/fireRate){_timer=0f;Fire();}}
    void Fire(){var t=Nearest();if(t==null)return;var go=_pool.Get();if(go==null)return;go.transform.position=transform.position;go.GetComponent<Projectile>()?.Init(((Vector2)t.position-(Vector2)transform.position).normalized,projSpeed,damage,range,_pool);}
    Transform Nearest(){Transform n=null;float d=range;foreach(var e in GameObject.FindGameObjectsWithTag("Enemy")){if(!e.activeInHierarchy)continue;var h=e.GetComponent<Health>();if(h!=null&&h.IsDead)continue;float dd=Vector2.Distance(transform.position,e.transform.position);if(dd<d){d=dd;n=e.transform;}}return n;}
    public void UpgradeDamage(float m)=>damage*=m; public void UpgradeFireRate(float m)=>fireRate*=m; public void UpgradeRange(float a)=>range+=a;
}
