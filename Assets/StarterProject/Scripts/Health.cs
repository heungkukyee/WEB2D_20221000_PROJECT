using UnityEngine;
using UnityEngine.Events;
public class Health : MonoBehaviour
{
    [SerializeField] float maxHp=100f;
    float _hp; bool _dead;
    public UnityEvent<float,float> onHpChanged=new(); public UnityEvent onDeath=new();
    public float CurrentHp=>_hp; public float MaxHp=>maxHp; public bool IsDead=>_dead; public float HpRatio=>_hp/maxHp;
    void Awake(){_hp=maxHp;}
    public void TakeDamage(float a){if(_dead||a<=0f)return;_hp=Mathf.Max(0f,_hp-a);onHpChanged.Invoke(_hp,maxHp);GetComponent<HitReceiver>()?.OnHit();if(_hp<=0f){_dead=true;onDeath.Invoke();}}
    public void Heal(float a){if(_dead)return;_hp=Mathf.Min(maxHp,_hp+a);onHpChanged.Invoke(_hp,maxHp);}
    public void SetMaxHp(float m,bool full=false){maxHp=m;if(full)_hp=m;_hp=Mathf.Min(_hp,m);onHpChanged.Invoke(_hp,m);}
}
