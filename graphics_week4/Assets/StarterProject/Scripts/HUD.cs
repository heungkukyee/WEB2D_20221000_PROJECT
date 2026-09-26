using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class HUD : MonoBehaviour
{
    [SerializeField] Slider hpBar,expBar;
    [SerializeField] TextMeshProUGUI scoreText,levelText,timeText;
    [SerializeField] GameObject gameOverPanel; [SerializeField] TextMeshProUGUI gameOverResultText;
    [SerializeField] Button restartButton;
    void Start()
    {
        var pl=GameObject.FindGameObjectWithTag("Player");
        if(pl!=null){var h=pl.GetComponent<Health>();if(h!=null){h.onHpChanged.AddListener((c,m)=>SL(hpBar,c/m));h.onDeath.AddListener(()=>GameManager.Instance?.TriggerGameOver());SL(hpBar,h.HpRatio);}}
        var gm=GameManager.Instance;
        if(gm!=null){gm.onScoreChanged.AddListener(s=>ST(scoreText,$"Score:{s}"));gm.onExpChanged.AddListener((c,r)=>SL(expBar,(float)c/r));gm.onLevelUp.AddListener(l=>ST(levelText,$"Lv.{l}"));gm.onGameOver.AddListener(Over);ST(levelText,$"Lv.{gm.Level}");}
        if(gameOverPanel)gameOverPanel.SetActive(false);
        restartButton?.onClick.AddListener(()=>GameManager.Instance?.RestartGame());
    }
    void Update(){if(GameManager.Instance!=null)ST(timeText,$"{GameManager.Instance.SurvivalTime:F0}s");}
    void Over(){if(gameOverPanel)gameOverPanel.SetActive(true);var gm=GameManager.Instance;if(gm!=null&&gameOverResultText!=null)gameOverResultText.text=$"Score:{gm.Score}\nTime:{gm.SurvivalTime:F1}s";}
    static void SL(Slider s,float v){if(s)s.value=Mathf.Clamp01(v);}
    static void ST(TextMeshProUGUI t,string m){if(t)t.text=m;}
}
