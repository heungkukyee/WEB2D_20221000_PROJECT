using UnityEngine;
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target; [SerializeField] float smoothSpeed=8f;
    [SerializeField] Vector3 offset=new Vector3(0,0,-10f);
    void LateUpdate(){if(target==null)return;transform.position=Vector3.Lerp(transform.position,target.position+offset,smoothSpeed*Time.deltaTime);}
    public void SetTarget(Transform t)=>target=t;
}
