using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 追蹤玩家本局最遠 Z 座標（僅增加不減少），每幀通知 HUD；死亡時停止、重開時由 <see cref="StartTracking"/> 重置。
/// 進場於 <see cref="Awake"/> 自動開始追蹤（<see cref="DefaultExecutionOrder"/> -100，早於多數 Manager）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class WardenDistanceTracker : MonoBehaviour
{
    [Header("參照")]
    [Tooltip("玩家世界座標來源（例如 PlayerRig）")]
    [SerializeField]
    private Transform playerTransform;

    [Header("事件")]
    [Tooltip("每幀帶入「目前最遠距離」整數公尺（float 形式，已 Floor）")]
    [SerializeField]
    private UnityEvent<float> onDistanceUpdated;

    [Tooltip("刷新最遠紀錄時觸發，參數為新的最遠距離（整數公尺，已 Floor）")]
    [SerializeField]
    private UnityEvent<float> onNewRecord;

    private float _maxZ;
    private bool _tracking;

    private void Awake()
    {
        StartTracking();
    }

    private void Update()
    {
        if (!_tracking || playerTransform == null)
            return;

        float z = playerTransform.position.z;
        int prevFloor = Mathf.FloorToInt(_maxZ);
        if (z > _maxZ)
            _maxZ = z;

        int newFloor = Mathf.FloorToInt(_maxZ);
        if (newFloor > prevFloor)
            onNewRecord?.Invoke(newFloor);

        float display = Mathf.FloorToInt(Mathf.Max(0f, _maxZ));
        onDistanceUpdated?.Invoke(display);
    }

    /// <summary>開始追蹤並將距離歸零（重開／新一局時呼叫）。</summary>
    public void StartTracking()
    {
        // 先清數值但不觸發事件，再設旗標，最後再通知 HUD，避免 Inspector 誤綁在同步回呼內改狀態。
        ResetDistance(notifyHud: false);
        _tracking = true;
        onDistanceUpdated?.Invoke(Mathf.FloorToInt(Mathf.Max(0f, _maxZ)));
    }

    /// <summary>停止追蹤（死亡或結算時呼叫，數值保留至下次 StartTracking 重置）。</summary>
    public void StopTracking()
    {
        _tracking = false;
    }

    /// <summary>目前本局最遠 Z（公尺，未 Floor；供排行榜提交等）。</summary>
    public float GetCurrentDistance()
    {
        return _maxZ;
    }

    /// <summary>強制將最遠距離歸零；預設會通知 HUD。</summary>
    /// <param name="notifyHud">false 時僅清內部數值（供 <see cref="StartTracking"/>）。</param>
    public void ResetDistance(bool notifyHud = true)
    {
        _maxZ = 0f;
        if (notifyHud)
            onDistanceUpdated?.Invoke(0f);
    }
}
