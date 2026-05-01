using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 能量方塊：玩家進入吸附半徑後以 <see cref="Vector3.MoveTowards"/> 飛向玩家，接觸後增加能量並銷毀。
/// 玩家可留空並依賴 Tag「Player」；能量由場景中的 <see cref="WardenEnergyManager"/> 自動尋找並呼叫 <see cref="WardenEnergyManager.AddEnergy"/>。
/// </summary>
public class WardenEnergyPickup : MonoBehaviour
{
    private static readonly Color PickupColor = new Color(0f, 200f / 255f, 180f / 255f, 1f);

    [Header("參照")]
    [Tooltip("玩家根物件或能量判定用錨點（例如 PlayerRig）")]
    [SerializeField] private Transform player;

    [Header("行為")]
    [Tooltip("與玩家距離小於等於此值（公尺）時開始飛向玩家")]
    [SerializeField] private float attractDistance = 4f;

    [Tooltip("飛向玩家的速度（公尺／秒）")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("與玩家距離小於等於此值（公尺）視為接觸並收集")]
    [SerializeField] private float contactDistance = 0.5f;

    [Tooltip("收集時傳給 onCollected 的能量量")]
    [SerializeField] private float energyAmount = 10f;

    [Header("事件（擴充）")]
    [Tooltip("接觸玩家後額外觸發（參數為能量量）；主要加能量已由程式呼叫 WardenEnergyManager")]
    [SerializeField] private UnityEvent<float> onCollected = new UnityEvent<float>();

    [Header("視覺")]
    [Tooltip("是否在 Start 時將 MeshRenderer 材質主色設為藍綠色")]
    [SerializeField] private bool applyPickupMaterialColor = true;

    [Tooltip("每秒繞世界 Y 軸旋轉角度")]
    [SerializeField] private float rotationDegreesPerSecond = 90f;

    private bool _chasing;
    private WardenEnergyManager _energyManager;
    private WardenCollectionManager _collectionManager;

    private void Start()
    {
        // 若 Inspector 未指派，自動尋找標記為 Player 的物件
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null)
                player = found.transform;
        }

        // 自動尋找能量管理器（Unity 6 建議使用 FindFirstObjectByType）
        _energyManager = Object.FindFirstObjectByType<WardenEnergyManager>();
        _collectionManager = Object.FindFirstObjectByType<WardenCollectionManager>();
        _collectionManager?.RegisterPickup();

        if (applyPickupMaterialColor && TryGetComponent<MeshRenderer>(out var renderer))
            renderer.material.color = PickupColor;
    }

    private void Update()
    {
        if (player == null)
            return;

        // 簡單自轉（與是否追逐無關，方便辨識場景中的方塊）
        if (rotationDegreesPerSecond != 0f)
            transform.Rotate(Vector3.up, rotationDegreesPerSecond * Time.deltaTime, Space.World);

        Vector3 selfPos = transform.position;
        Vector3 playerPos = player.position;
        float distance = Vector3.Distance(selfPos, playerPos);

        if (!_chasing)
        {
            if (distance > attractDistance)
                return;
            _chasing = true;
        }

        // 已開始飛行則持續追向玩家，不因再次超出 attractDistance 而停止
        float step = moveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(selfPos, playerPos, step);

        if (Vector3.Distance(transform.position, playerPos) <= contactDistance)
        {
            if (_energyManager != null)
                _energyManager.AddEnergy(energyAmount);

            _collectionManager?.OnPickupCollected();
            onCollected?.Invoke(energyAmount);
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        attractDistance = Mathf.Max(0.01f, attractDistance);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        contactDistance = Mathf.Max(0.01f, contactDistance);
        energyAmount = Mathf.Max(0f, energyAmount);
    }
#endif
}
