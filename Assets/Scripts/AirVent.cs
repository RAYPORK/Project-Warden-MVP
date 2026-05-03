using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 氣流噴射口：開局隨機擇一軸向為噴射方向，週期性在噴射／停止之間切換；
/// 噴射中玩家位於 Trigger 內時，沿該方向以加速度模式推動 Rigidbody，不造成傷害。
/// 可掛在根物件：若 Trigger Collider 在子物件上，執行時會自動加上 <see cref="AirVentTriggerRelay"/> 轉發事件（Unity 只對「帶 Collider 的那顆 GameObject」派送 Trigger）。
/// </summary>
[DisallowMultipleComponent]
public class AirVent : MonoBehaviour
{
    [Header("噴射")]
    [Tooltip("噴射方向（單位向量）；執行時於 Awake 由六向隨機覆寫")]
    [SerializeField]
    private Vector3 pushDirection = Vector3.forward;

    [Tooltip("推力大小（與 pushDirection 相乘；ForceMode.Acceleration）")]
    [SerializeField]
    private float pushForce = 20f;

    [Tooltip("每次噴射持續秒數")]
    [SerializeField]
    private float activeDuration = 2f;

    [Tooltip("每次停止持續秒數")]
    [SerializeField]
    private float inactiveDuration = 2f;

    [Tooltip("進場時是否先處於噴射狀態")]
    [SerializeField]
    private bool startActive = true;

    [Header("視覺")]
    [Tooltip("箭頭物件；未指派則略過旋轉")]
    [SerializeField]
    private Transform arrowVisual;

    [Tooltip("噴射中顯示、停止時隱藏；未指派則略過 SetActive")]
    [SerializeField]
    private GameObject activeEffect;

    /// <summary>目前是否處於噴射相位。</summary>
    private bool _isActive;

    /// <summary>當前相位剩餘秒數（噴射或停止）。</summary>
    private float _phaseRemain;

    private void Awake()
    {
        Vector3[] directions =
        {
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            Vector3.forward,
            Vector3.back
        };

        pushDirection = directions[Random.Range(0, directions.Length)];

        if (arrowVisual != null)
            arrowVisual.rotation = QuaternionForArrowFacing(pushDirection);

        _isActive = startActive;
        if (activeEffect != null)
            activeEffect.SetActive(_isActive);

        _phaseRemain = _isActive ? activeDuration : inactiveDuration;

        EnsureChildTriggerRelays();
    }

    /// <summary>
    /// 在子階層上帶 Trigger 的 GameObject 掛載轉發器，使父物件上的本腳本仍能收到 OnTriggerStay。
    /// </summary>
    private void EnsureChildTriggerRelays()
    {
        var seen = new HashSet<GameObject>();
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col == null || !col.isTrigger)
                continue;
            GameObject triggerGo = col.gameObject;
            if (triggerGo == gameObject)
                continue;
            if (!seen.Add(triggerGo))
                continue;

            AirVentTriggerRelay relay = triggerGo.GetComponent<AirVentTriggerRelay>();
            if (relay == null)
                relay = triggerGo.AddComponent<AirVentTriggerRelay>();
            relay.Bind(this);
        }
    }

    private void Update()
    {
        _phaseRemain -= Time.deltaTime;
        if (_phaseRemain > 0f)
            return;

        _isActive = !_isActive;
        if (activeEffect != null)
            activeEffect.SetActive(_isActive);

        _phaseRemain = _isActive ? activeDuration : inactiveDuration;
    }

    private void OnTriggerStay(Collider other)
    {
        ApplyTriggerStay(other);
    }

    /// <summary>由本物件或 <see cref="AirVentTriggerRelay"/> 呼叫的 Trigger 推力邏輯。</summary>
    internal void ApplyTriggerStay(Collider other)
    {
        if (!_isActive || !IsUnderPlayerHierarchy(other.transform))
            return;

        Rigidbody rb = other.attachedRigidbody != null
            ? other.attachedRigidbody
            : other.GetComponentInParent<Rigidbody>();
        if (rb == null)
            return;

        rb.AddForce(pushDirection * pushForce, ForceMode.Acceleration);
    }

    /// <summary>
    /// 讓箭頭的 +Z 軸對準噴射方向；若方向與世界 up 幾乎平行，改用 world forward 作為 LookRotation 的 up 參考，避免退化。
    /// </summary>
    private static Quaternion QuaternionForArrowFacing(Vector3 dir)
    {
        Vector3 d = dir.normalized;
        if (Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.99f)
            return Quaternion.LookRotation(d, Vector3.forward);
        return Quaternion.LookRotation(d);
    }

    private static bool IsUnderPlayerHierarchy(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Player"))
                return true;
            t = t.parent;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pushForce = Mathf.Max(0f, pushForce);
        activeDuration = Mathf.Max(0.01f, activeDuration);
        inactiveDuration = Mathf.Max(0.01f, inactiveDuration);
    }
#endif
}

/// <summary>
/// 掛在「子物件 Trigger Collider」上，將 Trigger 事件轉給父階的 <see cref="AirVent"/>（執行時由 AirVent 自動建立）。
/// </summary>
[DisallowMultipleComponent]
public sealed class AirVentTriggerRelay : MonoBehaviour
{
    private AirVent _owner;

    public void Bind(AirVent owner)
    {
        _owner = owner;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_owner != null)
            _owner.ApplyTriggerStay(other);
    }
}
