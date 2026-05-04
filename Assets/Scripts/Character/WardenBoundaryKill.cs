using UnityEngine;

/// <summary>
/// 掛在玩家根物件（PlayerRig）：當世界座標 Y 超過上限時直接觸發死亡流程。
/// </summary>
[DisallowMultipleComponent]
public class WardenBoundaryKill : MonoBehaviour
{
    [Tooltip("Y 軸上限；關卡空間高度約 40m（SpaceY）時建議留約 5m 緩衝")]
    [SerializeField]
    private float maxHeight = 45f;

    [Tooltip("未指派時於需要時自動尋找場景內 WardenDeathManager")]
    [SerializeField]
    private WardenDeathManager deathManager;

    private void Update()
    {
        if (transform.position.y > maxHeight)
        {
            EnsureDeathManagerReference();
            if (deathManager != null && !deathManager.isDead)
                deathManager.BeginDeathSequence();
        }
    }

    /// <summary>若未在 Inspector 指派，於執行時尋找 <see cref="WardenDeathManager"/>。</summary>
    private void EnsureDeathManagerReference()
    {
        if (deathManager != null)
            return;
        deathManager = Object.FindFirstObjectByType<WardenDeathManager>();
    }
}
