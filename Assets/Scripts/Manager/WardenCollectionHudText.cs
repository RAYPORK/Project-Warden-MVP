using TMPro;
using UnityEngine;

/// <summary>
/// 右上角 HUD：可同時綁定收集進度與距離；若兩者共用同一 <see cref="TMP_Text"/>，會合併為一行避免互相覆寫。
/// </summary>
public class WardenCollectionHudText : MonoBehaviour
{
    [Header("暫時選項")]
    [Tooltip("關閉時不顯示能量方塊「已收／總數」；僅顯示距離（分兩顆字時會清空收集那顆）。")]
    [SerializeField]
    private bool showEnergyPickupCounter = true;

    [Tooltip("能量「已收／總數」顯示；若與距離分開可只綁此欄並另設 distanceText")]
    [SerializeField]
    private TMP_Text collectionText;

    [Tooltip("距離專用文字；留空則與 collectionText 合併顯示")]
    [SerializeField]
    private TMP_Text distanceText;

    private int _lastCollected;
    private int _lastTotal;
    private bool _hasCount;
    private int _lastDistanceMeters;

    /// <summary>給 UnityEvent(int,int) 動態綁定。</summary>
    public void OnCountUpdated(int collected, int total)
    {
        if (!showEnergyPickupCounter)
            return;

        _hasCount = true;
        _lastCollected = collected;
        _lastTotal = total;
        Redraw();
    }

    /// <summary>給 <see cref="WardenDistanceTracker.onDistanceUpdated"/> 等 UnityEvent(float) 綁定。</summary>
    public void OnDistanceUpdated(float distance)
    {
        _lastDistanceMeters = Mathf.FloorToInt(distance);
        Redraw();
    }

    private void Redraw()
    {
        if (distanceText != null)
            distanceText.text = $"{_lastDistanceMeters}m";

        if (collectionText == null)
            return;

        if (!showEnergyPickupCounter)
        {
            if (distanceText != null)
                collectionText.text = string.Empty;
            else
                collectionText.text = $"{_lastDistanceMeters}m";
            return;
        }

        if (distanceText != null)
        {
            collectionText.text = $"{_lastCollected} / {_lastTotal}";
            return;
        }

        // 與距離共用同一 TMP：合併一行，避免 OnCountUpdated 蓋掉距離。
        if (_hasCount)
            collectionText.text = $"{_lastDistanceMeters}m  ·  {_lastCollected} / {_lastTotal}";
        else
            collectionText.text = $"{_lastDistanceMeters}m";
    }
}
