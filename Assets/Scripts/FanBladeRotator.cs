using UnityEngine;

/// <summary>
/// 風扇葉片持續自轉；玩家以 Trigger 或實體碰撞進入時扣血並擊飛。
/// 葉片 Collider 為 <b>非 Trigger</b> 時必須靠 <see cref="OnCollisionEnter"/>；為 Trigger 時則靠 <see cref="OnTriggerEnter"/>。
/// 請將 Player Tag 掛在玩家根（PlayerRig）即可，子物件上的碰撞器不必重複設 Tag。
/// </summary>
[DisallowMultipleComponent]
public class FanBladeRotator : MonoBehaviour
{
    [Header("旋轉")]
    [Tooltip("每秒旋轉角度（度）")]
    [SerializeField]
    private float rotationSpeed = 60f;

    [Tooltip("本機空間下的旋轉軸（未正規化時仍可用；常見為 forward / up）")]
    [SerializeField]
    private Vector3 rotationAxis = Vector3.forward;

    [Tooltip("true：順時針（實際視覺依 rotationAxis 與模型而定）")]
    [SerializeField]
    private bool clockwise = true;

    [Header("傷害與擊飛")]
    [Tooltip("每次觸發有效時扣除的血量")]
    [SerializeField]
    private float damageAmount = 25f;

    [Tooltip("擊飛力道（Impulse）")]
    [SerializeField]
    private float knockbackForce = 15f;

    [Tooltip("兩次有效傷害之間的最短間隔（秒）")]
    [SerializeField]
    private float damageCooldown = 0.5f;

    /// <summary>下一次允許造成傷害／擊飛的時間（<see cref="Time.time"/>）。</summary>
    private float _nextDamageTime;

    private void Update()
    {
        float dir = clockwise ? 1f : -1f;
        transform.Rotate(rotationAxis * rotationSpeed * dir * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyFanHit(other);
    }

    /// <summary>葉片為非 Trigger 的 MeshCollider 時，與玩家 Rigidbody 產生的是碰撞事件而非觸發。</summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            TryApplyFanHit(collision.collider);
    }

    /// <summary>自本物件往上尋找是否為玩家階層（根上帶 Player Tag 即可）。</summary>
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

    private void TryApplyFanHit(Collider other)
    {
        if (!IsUnderPlayerHierarchy(other.transform))
            return;

        if (Time.time < _nextDamageTime)
            return;

        // 血量管理器常與 PlayerRig 分開掛在場景上；僅 GetComponentInParent 會找不到而只擊飛不扣血。
        WardenHealthManager health = other.GetComponentInParent<WardenHealthManager>();
        if (health == null)
            health = UnityEngine.Object.FindFirstObjectByType<WardenHealthManager>();

        if (health != null && damageAmount > 0f && !health.IsInvincible)
            health.TakeDamage(damageAmount);

        Rigidbody body = other.attachedRigidbody != null
            ? other.attachedRigidbody
            : other.GetComponentInParent<Rigidbody>();
        if (body != null)
        {
            Vector3 origin = transform.position;
            Vector3 toPlayer = other.transform.position - origin;
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = transform.forward;
            else
                toPlayer.Normalize();

            body.AddForce(toPlayer * knockbackForce, ForceMode.Impulse);
        }

        _nextDamageTime = Time.time + damageCooldown;
    }
}
