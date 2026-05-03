using UnityEngine;

/// <summary>
/// 電擊球：開局於 <see cref="minSize"/>～<see cref="maxSize"/> 間隨機決定外球均勻縮放一次；
/// 玩家進入 Trigger 時依每秒傷害累積，並以 <see cref="damageCooldown"/> 間隔批次扣血。
/// 請將本腳本掛在具 <b>Is Trigger</b> 的 Collider 上，並指派 <see cref="outerSphere"/>（可含 <see cref="SphereCollider"/>）。
/// </summary>
[DisallowMultipleComponent]
public class ElectricOrb : MonoBehaviour
{
    [Header("外球縮放")]
    [Tooltip("外層球體 Transform；未指派則略過縮放與 Collider 半徑設定")]
    [SerializeField]
    private Transform outerSphere;

    [Tooltip("外球均勻縮放隨機下限")]
    [Range(1f, 30f)]
    [SerializeField]
    private float minSize = 3f;

    [Tooltip("外球均勻縮放隨機上限；不可小於 minSize")]
    [Range(1f, 30f)]
    [SerializeField]
    private float maxSize = 8f;

    [Header("傷害")]
    [Tooltip("玩家在 Trigger 內時每秒造成的傷害量")]
    [SerializeField]
    private float damagePerSecond = 15f;

    [Tooltip("傷害節流：累積時間達此秒數時批次扣血一次")]
    [SerializeField]
    private float damageCooldown = 0.1f;

    [Header("血量")]
    [Tooltip("可空；未指派時於執行時自動尋找 WardenHealthManager")]
    [SerializeField]
    private WardenHealthManager healthManager;

    /// <summary>玩家在 Trigger 內尚未結算為批次扣血的時間累積（秒）。</summary>
    private float _damageAccum;

    private void Start()
    {
        float randomSize = Random.Range(minSize, maxSize);
        if (outerSphere != null)
        {
            outerSphere.localScale = Vector3.one * randomSize;
            // 半徑維持 Unity 預設 0.5（單位球），實際世界大小由 localScale 決定。
            SphereCollider col = outerSphere.GetComponent<SphereCollider>();
            if (col != null)
                col.radius = 0.5f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        EnsureHealthManagerReference();
        if (healthManager != null && healthManager.IsInvincible)
            return;

        _damageAccum += Time.deltaTime;
        float cd = Mathf.Max(0.01f, damageCooldown);
        while (_damageAccum >= cd)
        {
            if (healthManager != null)
                healthManager.TakeDamage(damagePerSecond * cd);
            _damageAccum -= cd;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
            _damageAccum = 0f;
    }

    private void EnsureHealthManagerReference()
    {
        if (healthManager != null)
            return;
        healthManager = Object.FindFirstObjectByType<WardenHealthManager>();
    }

    /// <summary>碰撞體本身或任一父階層是否帶有 Tag「Player」（子物件 Trigger 亦成立）。</summary>
    private static bool IsPlayerCollider(Collider other)
    {
        return IsUnderPlayerHierarchy(other.transform);
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
        minSize = Mathf.Clamp(minSize, 1f, 30f);
        maxSize = Mathf.Clamp(maxSize, 1f, 30f);
        if (minSize > maxSize)
            maxSize = minSize;

        damagePerSecond = Mathf.Max(0f, damagePerSecond);
        damageCooldown = Mathf.Max(0.01f, damageCooldown);
    }
#endif
}
