using UnityEngine;
using UnityEngine.Events;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance{get;private set;}
    [SerializeField] int[] expTable={30,60,120,200,300,450,600};
    public int Score{get;private set;} public int Level{get;private set;}=1; public int CurrentExp{get;private set;}
    public float SurvivalTime{get;private set;} public bool IsGameOver{get;private set;}
    public UnityEvent<int> onScoreChanged=new(); public UnityEvent<int,int> onExpChanged=new();
    public UnityEvent<int> onLevelUp=new(); public UnityEvent onGameOver=new();
    void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
    void Update(){if(!IsGameOver)SurvivalTime+=Time.deltaTime;}
    public void AddExp(int a){if(IsGameOver)return;CurrentExp+=a;AddScore(a);onExpChanged.Invoke(CurrentExp,Req());while(CurrentExp>=Req()){CurrentExp-=Req();Level++;onLevelUp.Invoke(Level);onExpChanged.Invoke(CurrentExp,Req());Debug.Log($"[GM] Level {Level}");}}
    public void AddScore(int a){Score+=a;onScoreChanged.Invoke(Score);}
    public int Req()=>expTable[Mathf.Clamp(Level-1,0,expTable.Length-1)];
    public void TriggerGameOver(){if(IsGameOver)return;IsGameOver=true;Time.timeScale=0f;onGameOver.Invoke();}
    public void RestartGame(){Time.timeScale=1f;var s=UnityEngine.SceneManagement.SceneManager.GetActiveScene();UnityEngine.SceneManagement.SceneManager.LoadScene(s.buildIndex);}
}
