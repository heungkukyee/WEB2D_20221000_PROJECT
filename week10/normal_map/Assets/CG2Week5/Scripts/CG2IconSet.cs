using UnityEngine;

/// <summary>
/// 5주차 — UI 아이콘을 한 곳에서 관리한다.
///
/// 아이콘을 Image마다 직접 꽂아 두면, 생성형 AI로 새 아이콘을 만들었을 때
/// 씬과 프리팹을 돌아다니며 하나씩 바꿔야 한다. 이 에셋 하나만 바꾸면
/// HUD·레벨업·상점의 아이콘이 모두 따라 바뀐다(CG2UIIcon, LevelUpPanel, ShopPanel이 여기서 읽는다).
/// </summary>
[CreateAssetMenu(menuName = "CG2/Icon Set", fileName = "CG2IconSet")]
public class CG2IconSet : ScriptableObject
{
    public const string HP = "hp", EXP = "exp", COIN = "coin",
                        DAMAGE = "damage", FIRE_RATE = "firerate", RANGE = "range",
                        HEAL = "heal", SPEED = "speed";

    public static readonly string[] Keys = { HP, EXP, COIN, DAMAGE, FIRE_RATE, RANGE, HEAL, SPEED };

    [System.Serializable]
    public struct Entry
    {
        public string key;
        public Sprite sprite;
    }

    public Entry[] entries = new Entry[0];

    public Sprite Get(string key)
    {
        foreach (var e in entries) if (e.key == key) return e.sprite;
        return null;
    }

    public void Set(string key, Sprite sprite)
    {
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].key == key) { entries[i].sprite = sprite; return; }
        System.Array.Resize(ref entries, entries.Length + 1);
        entries[^1] = new Entry { key = key, sprite = sprite };
    }
}
