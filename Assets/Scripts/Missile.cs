using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飛彈：可選追蹤目標時以 <see cref="trackingStrength"/> 平滑轉向，否則沿初始方向直線飛行；
/// 壽命結束或直接命中玩家時爆炸並造成範圍／直接傷害（無敵時略過扣血）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class Missile : MonoBehaviour
{
    [Header("移動")]
    [Tooltip("飛行速度（單位／秒）")]
    [SerializeField]
    private float moveSpeed = 15f;

    [Tooltip("追蹤時每秒最大轉向角度（度）")]
    [SerializeField]
    private float trackingStrength = 90f;

    [Header("生命週期")]
    [Tooltip("飛彈存活時間（秒），結束時觸發爆炸")]
    [Range(1f, 20f)]
    [SerializeField]
    private float lifetime = 5f;

    [Header("爆炸與命中")]
    [Tooltip("爆炸傷害判定半徑")]
    [SerializeField]
    private float explosionRadius = 5f;

    [Tooltip("爆炸對範圍內玩家造成的傷害")]
    [SerializeField]
    private float explosionDamage = 30f;

    [Tooltip("飛彈本體直接命中玩家時的傷害")]
    [SerializeField]
    private float directHitDamage = 20f;

    [Header("特效")]
    [Tooltip("爆炸時生成的特效 Prefab；可留空")]
    [SerializeField]
    private GameObject explosionEffectPrefab;

    private Rigidbody _rigidbody;
    private Vector3 _initialDirection;

    /// <summary>追蹤目標；為 null 時僅直線飛行。</summary>
    private Transform _target;

    /// <summary>為 true 且 <see cref="_target"/> 有效時，每幀轉向並朝目標前進。</summary>
    private bool _isTracking;

    /// <summary>追蹤時目前的飛行方向（由 <see cref="_initialDirection"/> 漸轉至目標）。</summary>
    private Vector3 _currentMoveDir;

    /// <summary>剩餘壽命（秒）；僅在 <see cref="_launched"/> 為 true 時有意義。</summary>
    private float _lifeRemaining;

    private bool _hasExploded;

    /// <summary>是否已由發射源呼叫過 <see cref="Launch"/>；避免用負的 <see cref="_lifeRemaining"/> 與「未發射」混淆。</summary>
    private bool _launched;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;

        EnsureTriggerCollider();
    }

    /// <summary>若 Prefab 未掛 Collider，補上一個 Trigger 球體以利直接命中判定。</summary>
    private void EnsureTriggerCollider()
    {
        Collider existing = GetComponent<Collider>();
        if (existing != null)
        {
            existing.isTrigger = true;
            return;
        }

        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.5f;
    }

    /// <summary>
    /// 由砲台等發射源呼叫：設定初速方向（世界空間）；若傳入 <paramref name="target"/> 則啟用追蹤。
    /// </summary>
    public void Launch(Vector3 initialDirection, Transform target = null)
    {
        _initialDirection = initialDirection.normalized;
        _target = target;
        _isTracking = target != null;
        _currentMoveDir = _initialDirection;
        transform.forward = _initialDirection;
        _lifeRemaining = lifetime;
        _launched = true;
    }

    private void Update()
    {
        if (_hasExploded) return;
        if (!_launched)
            return;

        if (_lifeRemaining > 0f)
            _lifeRemaining -= Time.deltaTime;

        if (_lifeRemaining <= 0f)
        {
            Explode();
            return;
        }

        if (_isTracking && _target != null)
        {
            Vector3 toTarget = (_target.position - transform.position).normalized;
            float maxRadians = trackingStrength * Mathf.Deg2Rad * Time.deltaTime;
            _currentMoveDir = Vector3.RotateTowards(_currentMoveDir, toTarget, maxRadians, 0f);
            transform.forward = _currentMoveDir;
            transform.position += _currentMoveDir * (moveSpeed * Time.deltaTime);
        }
        else
        {
            transform.position += _initialDirection * (moveSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsUnderPlayerHierarchy(other.transform))
            return;

        WardenHealthManager health = other.GetComponentInParent<WardenHealthManager>();
        if (health == null)
            health = FindHealthManager();

        if (health != null && !health.IsInvincible)
            health.TakeDamage(directHitDamage);

        // 無敵時略過扣血，仍爆炸並銷毀飛彈
        Explode();
    }

    /// <summary>
    /// 爆炸並銷毀本物件。
    /// 常見誤解排查（繁中說明）：
    /// (1) DontDestroyOnLoad 不會阻止 Destroy；仍可正常銷毀。
    /// (2) 一般父階層不會阻止銷毀；但若 Missile 掛在子物件上，Destroy(gameObject) 只刪除該子節點，
    /// 帶有 Mesh 的父物件仍留在場景，看起來像飛彈沒消失。本專案 Prefab 根節點為「Missile」則無此問題。
    /// (3) _hasExploded 僅阻擋第二次進入；第一次仍應跑完。若第一次在中途拋出例外，Destroy 可能從未執行。
    /// (4) C# 參照不會阻止 Unity 銷毀 GameObject。
    /// 最可能原因：ApplyExplosionDamage、TakeDamage、Inspector 綁定的 onDeath 等事件鏈拋出例外，
    /// 導致 Destroy 永遠執行不到；_hasExploded 已為 true，Update 不再移動，視覺上像凍結。
    /// 故以 try/finally 保證無論前段是否例外，皆會 Destroy。
    /// </summary>
    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        try
        {
            ApplyExplosionDamage();
            if (explosionEffectPrefab != null)
                Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }
        finally
        {
            Destroy(gameObject);
        }
    }

    private void ApplyExplosionDamage()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            ~0,
            QueryTriggerInteraction.Collide);

        var applied = new HashSet<WardenHealthManager>();
        foreach (Collider col in hits)
        {
            if (col == null)
                continue;

            WardenHealthManager health = col.GetComponentInParent<WardenHealthManager>();
            if (health == null)
                continue;

            if (health.IsInvincible)
                continue;

            if (!applied.Add(health))
                continue;

            health.TakeDamage(explosionDamage);
        }
    }

    private static WardenHealthManager FindHealthManager()
    {
        return UnityEngine.Object.FindFirstObjectByType<WardenHealthManager>();
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
        lifetime = Mathf.Clamp(lifetime, 1f, 20f);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        trackingStrength = Mathf.Max(0f, trackingStrength);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);
        directHitDamage = Mathf.Max(0f, directHitDamage);
    }
#endif
}
