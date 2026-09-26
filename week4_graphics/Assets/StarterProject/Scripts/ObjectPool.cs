using System.Collections.Generic;
using UnityEngine;
/// <summary>범용 오브젝트 풀 — 11주차 GC 비교 실습</summary>
public class ObjectPool : MonoBehaviour
{
    readonly Queue<GameObject> _idle=new(); GameObject _prefab; int _active;
    public int ActiveCount=>_active; public int IdleCount=>_idle.Count;
    public void Initialize(GameObject pf,int n=10){_prefab=pf;for(int i=0;i<n;i++){var g=Make();g.SetActive(false);_idle.Enqueue(g);}}
    public GameObject Get(){if(_prefab==null)return null;var g=_idle.Count>0?_idle.Dequeue():Make();g.SetActive(true);_active++;return g;}
    public void Return(GameObject g){g.SetActive(false);_idle.Enqueue(g);_active=Mathf.Max(0,_active-1);}
    public void ReturnAll(){foreach(Transform t in transform)if(t.gameObject.activeSelf)Return(t.gameObject);}
    GameObject Make(){var g=Instantiate(_prefab,transform);g.SetActive(false);return g;}
}
