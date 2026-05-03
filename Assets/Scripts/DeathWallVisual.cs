using UnityEngine;

/// <summary>
/// 死亡牆視覺同步：每幀將 <see cref="WardenRoomGenerator.DeathWallZ"/> 寫入世界 Z（X／Y 不變）。
/// 若邏輯初值為負（起點後方），即使 Z 不大於 0 也會顯示並同步；初值為 0 時維持「僅在 Z 大於 0 時顯示」以配合緩衝期。
/// 執行順序晚於 <see cref="WardenRoomGenerator"/>，避免同一幀讀到尚未初始化的 DeathWallZ。
/// </summary>
[DefaultExecutionOrder(100)]
public class DeathWallVisual : MonoBehaviour
{
    [Tooltip("提供死亡牆邏輯 Z 的房間產生器")]
    [SerializeField]
    private WardenRoomGenerator roomGenerator;

    [Tooltip("場景中要同步 Z 的死亡牆 Transform（可為空物件＋視覺子物件）")]
    [SerializeField]
    private Transform deathWallTransform;

    /// <summary>
    /// 初值 ≥0 時先隱藏，避免緩衝期間牆卡在 Z=0 被看見；初值 &lt;0（起點後方）則不在此關閉，否則 Play 後會有一瞬或整段「牆不見」。
    /// </summary>
    private void Start()
    {
        if (deathWallTransform == null || roomGenerator == null)
            return;

        if (roomGenerator.DeathWallInitialOffset < 0f)
            return;

        deathWallTransform.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (roomGenerator == null || deathWallTransform == null)
            return;

        if (!roomGenerator.EnableDeathWall)
        {
            if (deathWallTransform.gameObject.activeSelf)
                deathWallTransform.gameObject.SetActive(false);
            return;
        }

        // 必須先同步 Z：邏輯初值為 -10 時 DeathWallZ≤0，舊寫法會 return 導致永遠留在場景預設位置。
        Vector3 pos = deathWallTransform.position;
        pos.z = roomGenerator.DeathWallZ;
        deathWallTransform.position = pos;

        // 負向初值：牆在玩家後方（Z 為負）也應顯示；初值 ≥0 時維持「Z>0 才顯示」避免緩衝期間牆在 Z=0。
        if (roomGenerator.DeathWallInitialOffset < 0f)
        {
            if (!deathWallTransform.gameObject.activeSelf)
                deathWallTransform.gameObject.SetActive(true);
            return;
        }

        if (roomGenerator.DeathWallZ <= 0f)
        {
            if (deathWallTransform.gameObject.activeSelf)
                deathWallTransform.gameObject.SetActive(false);
        }
        else
        {
            if (!deathWallTransform.gameObject.activeSelf)
                deathWallTransform.gameObject.SetActive(true);
        }
    }
}
