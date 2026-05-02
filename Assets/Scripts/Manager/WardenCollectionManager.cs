using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 供 Inspector 綁定的 (已收集, 總數) 事件。
/// </summary>
[Serializable]
public class CollectionCountUpdatedEvent : UnityEvent<int, int> { }

/// <summary>
/// 能量方塊收集進度：登記總數、計時、全部收齊時觸發完成事件。
/// </summary>
public class WardenCollectionManager : MonoBehaviour
{
    [Header("事件")]
    [Tooltip("已收集數、總數變更時觸發（HUD 綁定）")]
    [SerializeField] private CollectionCountUpdatedEvent onCountUpdated = new CollectionCountUpdatedEvent();

    [Tooltip("全部收集完畢時觸發，參數為本局經過秒數（Time.time - 起跑點）")]
    [SerializeField] private UnityEvent<float> onAllCollected = new UnityEvent<float>();

    private int _totalPickups;
    private int _collectedPickups;
    private int _registeredPickups;
    private float _runStartTime;
    private bool _allCollectedFired;

    /// <summary>本局目標方塊總數（由 <see cref="ResetForNewRun"/> 設定）。</summary>
    public int TotalPickups => _totalPickups;

    /// <summary>已收集數。</summary>
    public int CollectedPickups => _collectedPickups;

    /// <summary>
    /// 新一局：重置計時與計數，並通知 HUD。應在能量方塊生成完畢後呼叫（總數以參數為準）。
    /// </summary>
    public void ResetForNewRun(int newTotalCount)
    {
        _allCollectedFired = false;
        _registeredPickups = 0;
        _totalPickups = Mathf.Max(0, newTotalCount);
        _collectedPickups = 0;
        _runStartTime = Time.time;
        onCountUpdated?.Invoke(_collectedPickups, _totalPickups);
    }

    /// <summary>
    /// 累計已登記方塊數；總數以 <see cref="ResetForNewRun"/> 為準。執行時由 <see cref="WardenRoomGenerator"/> 在 Reset 後依生成數批次呼叫。
    /// </summary>
    public void RegisterPickup()
    {
        _registeredPickups++;
    }

    /// <summary>方塊被玩家收集時呼叫。</summary>
    public void OnPickupCollected()
    {
        if (_allCollectedFired)
            return;
        if (_totalPickups <= 0)
            return;
        if (_collectedPickups >= _totalPickups)
            return;

        _collectedPickups++;
        onCountUpdated?.Invoke(_collectedPickups, _totalPickups);

        if (_collectedPickups >= _totalPickups)
        {
            _allCollectedFired = true;
            float elapsed = Mathf.Max(0f, Time.time - _runStartTime);
            onAllCollected?.Invoke(elapsed);
        }
    }
}
