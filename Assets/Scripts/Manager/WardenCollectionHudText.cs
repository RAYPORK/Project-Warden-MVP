using TMPro;
using UnityEngine;

/// <summary>
/// 右上角等 HUD：綁定 <see cref="WardenCollectionManager.onCountUpdated"/>，顯示「已收 / 總數」。
/// </summary>
public class WardenCollectionHudText : MonoBehaviour
{
    [SerializeField] private TMP_Text collectionText;

    /// <summary>給 UnityEvent(int,int) 動態綁定。</summary>
    public void OnCountUpdated(int collected, int total)
    {
        if (collectionText != null)
            collectionText.text = $"{collected} / {total}";
    }
}
