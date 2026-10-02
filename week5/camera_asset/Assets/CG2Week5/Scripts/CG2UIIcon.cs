using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Image에 붙여 두면 CG2IconSet에서 key에 해당하는 스프라이트를 가져와 표시한다.
/// 에디터에서도 즉시 반영되므로(OnValidate) 아이콘 세트를 바꾸면 Scene 뷰에서 바로 확인할 수 있다.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class CG2UIIcon : MonoBehaviour
{
    public CG2IconSet iconSet;
    public string key = CG2IconSet.HP;

    void OnEnable() => Refresh();
    void OnValidate() => Refresh();

    public void Refresh()
    {
        if (iconSet == null) return;
        var s = iconSet.Get(key);
        var img = GetComponent<Image>();
        if (s != null && img.sprite != s) img.sprite = s;
    }
}
