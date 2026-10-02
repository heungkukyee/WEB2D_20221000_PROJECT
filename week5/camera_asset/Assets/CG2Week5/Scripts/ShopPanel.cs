using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 5주차 — 상점 창. Tab 키(또는 화면의 SHOP 버튼)로 연다.
///
/// 화폐는 Score를 그대로 쓴다(Gold). 적을 잡을수록 쌓이고, 사면 줄어든다.
/// 상점이 열려 있는 동안은 게임이 멈춘다. 레벨업 창이 떠 있을 때는 열지 않는다.
/// </summary>
public class ShopPanel : MonoBehaviour
{
    enum Kind { Heal, Damage, Speed, FireRate }

    struct Item
    {
        public Kind kind; public string key, name; public int price;
        public Item(Kind k, string key, string n, int p) { kind = k; this.key = key; name = n; price = p; }
    }

    static readonly Item[] Items =
    {
        new(Kind.Heal,     CG2IconSet.HEAL,      "Potion  +30 HP",   30),
        new(Kind.Damage,   CG2IconSet.DAMAGE,    "Sharpen  +10% DMG", 60),
        new(Kind.Speed,    CG2IconSet.SPEED,     "Boots  +5% SPD",   40),
        new(Kind.FireRate, CG2IconSet.FIRE_RATE, "Trigger  +10% ROF", 60),
    };

    [SerializeField] CG2IconSet iconSet;
    [SerializeField] GameObject root;
    [SerializeField] TextMeshProUGUI goldText;
    [SerializeField] Button[] itemButtons = new Button[4];
    [SerializeField] Image[] itemIcons = new Image[4];
    [SerializeField] TextMeshProUGUI[] itemLabels = new TextMeshProUGUI[4];
    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    [SerializeField] LevelUpPanel levelUp;

    bool _open;

    void Start()
    {
        if (root) root.SetActive(false);
        for (int i = 0; i < itemButtons.Length && i < Items.Length; i++)
        {
            int idx = i;
            if (itemButtons[i]) itemButtons[i].onClick.AddListener(() => Buy(idx));
            if (itemIcons[i] && iconSet) itemIcons[i].sprite = iconSet.Get(Items[i].key);
            if (itemLabels[i]) itemLabels[i].text = $"{Items[i].name}\n<size=80%>{Items[i].price} G</size>";
        }
        if (openButton) openButton.onClick.AddListener(Toggle);
        if (closeButton) closeButton.onClick.AddListener(Toggle);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame) Toggle();
    }

    public void Toggle()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver) return;
        if (!_open && levelUp != null && levelUp.IsOpen) return;

        _open = !_open;
        if (root) root.SetActive(_open);
        if (_open) { CG2Pause.Push(); Refresh(); }
        else CG2Pause.Pop();
    }

    void Buy(int i)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Score < Items[i].price) return;
        gm.AddScore(-Items[i].price);

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            switch (Items[i].kind)
            {
                case Kind.Heal:     p.GetComponent<Health>()?.Heal(30f); break;
                case Kind.Damage:   p.GetComponent<WeaponController>()?.UpgradeDamage(1.1f); break;
                case Kind.FireRate: p.GetComponent<WeaponController>()?.UpgradeFireRate(1.1f); break;
                case Kind.Speed:
                    var pc = p.GetComponent<PlayerController>();
                    if (pc) pc.MoveSpeed *= 1.05f;
                    break;
            }
        }
        Debug.Log($"[상점] {Items[i].name} 구매 — 남은 Gold {gm.Score}");
        Refresh();
    }

    void Refresh()
    {
        int gold = GameManager.Instance ? GameManager.Instance.Score : 0;
        if (goldText) goldText.text = $"Gold  {gold}";
        for (int i = 0; i < itemButtons.Length && i < Items.Length; i++)
            if (itemButtons[i]) itemButtons[i].interactable = gold >= Items[i].price;
    }
}
