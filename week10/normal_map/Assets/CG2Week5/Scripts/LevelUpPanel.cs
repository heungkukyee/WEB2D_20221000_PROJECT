using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 5주차 — 레벨업 선택 창.
///
/// GameManager.onLevelUp이 불리면 게임을 멈추고 강화 3개 중 하나를 고르게 한다.
/// 경험치를 한 번에 많이 얻어 여러 레벨이 오르면(GameManager의 while 루프) 그 횟수만큼 차례로 연다.
///
/// 이 창은 평소에 꺼져 있다가 가끔 켜지는 "팝업"이다. HP바처럼 매 프레임 바뀌는
/// 동적 UI와 같은 Canvas에 두지 않는다 — Canvas_Popup에 분리해 둔다.
/// </summary>
public class LevelUpPanel : MonoBehaviour
{
    enum Kind { Damage, FireRate, Range, Heal, Speed }

    struct Option
    {
        public Kind kind; public string key, title, desc;
        public Option(Kind k, string key, string t, string d) { kind = k; this.key = key; title = t; desc = d; }
    }

    static readonly Option[] All =
    {
        new(Kind.Damage,   CG2IconSet.DAMAGE,    "Power",  "Damage +20%"),
        new(Kind.FireRate, CG2IconSet.FIRE_RATE, "Rapid",  "Fire rate +15%"),
        new(Kind.Range,    CG2IconSet.RANGE,     "Reach",  "Range +1"),
        new(Kind.Heal,     CG2IconSet.HEAL,      "Heal",   "Restore 40 HP"),
        new(Kind.Speed,    CG2IconSet.SPEED,     "Swift",  "Move speed +10%"),
    };

    [SerializeField] CG2IconSet iconSet;
    [SerializeField] GameObject root;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button[] cards = new Button[3];
    [SerializeField] Image[] cardIcons = new Image[3];
    [SerializeField] TextMeshProUGUI[] cardTitles = new TextMeshProUGUI[3];
    [SerializeField] TextMeshProUGUI[] cardDescs = new TextMeshProUGUI[3];

    readonly Option[] _current = new Option[3];
    int _pending;
    bool _open;

    public bool IsOpen => _open;

    void Start()
    {
        if (root) root.SetActive(false);
        for (int i = 0; i < cards.Length; i++)
        {
            int idx = i;
            if (cards[i]) cards[i].onClick.AddListener(() => Choose(idx));
        }
        GameManager.Instance?.onLevelUp.AddListener(OnLevelUp);
    }

    void OnLevelUp(int level)
    {
        _pending++;
        if (!_open) Open(level);
    }

    void Open(int level)
    {
        // 5개 중 서로 다른 3개를 뽑는다.
        var pool = new System.Collections.Generic.List<Option>(All);
        for (int i = 0; i < 3; i++)
        {
            int r = Random.Range(0, pool.Count);
            _current[i] = pool[r];
            pool.RemoveAt(r);

            if (cardIcons[i] && iconSet) cardIcons[i].sprite = iconSet.Get(_current[i].key);
            if (cardTitles[i]) cardTitles[i].text = _current[i].title;
            if (cardDescs[i]) cardDescs[i].text = _current[i].desc;
        }
        if (titleText) titleText.text = $"LEVEL UP!  Lv.{(GameManager.Instance ? GameManager.Instance.Level : level)}";

        _open = true;
        if (root) root.SetActive(true);
        CG2Pause.Push();
    }

    void Choose(int i)
    {
        if (!_open) return;
        Apply(_current[i]);
        _pending = Mathf.Max(0, _pending - 1);
        _open = false;
        if (root) root.SetActive(false);
        CG2Pause.Pop();
        if (_pending > 0) Open(GameManager.Instance ? GameManager.Instance.Level : 0);
    }

    static void Apply(Option o)
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        switch (o.kind)
        {
            case Kind.Damage:   p.GetComponent<WeaponController>()?.UpgradeDamage(1.2f); break;
            case Kind.FireRate: p.GetComponent<WeaponController>()?.UpgradeFireRate(1.15f); break;
            case Kind.Range:    p.GetComponent<WeaponController>()?.UpgradeRange(1f); break;
            case Kind.Heal:     p.GetComponent<Health>()?.Heal(40f); break;
            case Kind.Speed:
                var pc = p.GetComponent<PlayerController>();
                if (pc) pc.MoveSpeed *= 1.1f;
                break;
        }
        Debug.Log($"[레벨업] {o.title} 선택 — {o.desc}");
    }
}
