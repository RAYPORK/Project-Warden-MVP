using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 在固定空間內以自由座標生成隨機大小平台，並保證起點到終點可達。
/// 預設 <see cref="DefaultExecutionOrder"/> 為 40，於 Controller／Winch 之後 Awake，減少與其他系統的競態。
/// </summary>
[DefaultExecutionOrder(40)]
[DisallowMultipleComponent]
public class WardenRoomGenerator : MonoBehaviour
{
    [Header("隨機種子")]
    [SerializeField] private int seed = 42001;

    [Header("Prefab")]
    [SerializeField] private GameObject platformPrefab;

    [Header("能量方塊")]
    [Tooltip("須含 WardenEnergyPickup；可選擇命名 EnergyPickup_xxx 或 Tag「EnergyPickup」以便清除")]
    [SerializeField] private GameObject energyPickupPrefab;

    [Tooltip("每次生成地圖時在空間內撒落的數量")]
    [Range(0, 50)]
    [SerializeField] private int energyPickupCount = 20;

    [Header("材質類型設定")]
    [SerializeField] private Material matCementGrey;
    [SerializeField] private Material matLava;
    [SerializeField] private Material matIce;
    [SerializeField] private bool enableLava = false;
    [SerializeField] private bool enableIce = false;
    [Range(0f, 10f)]
    [SerializeField] private float lavaRatio = 0.1f;
    [Range(0f, 10f)]
    [SerializeField] private float iceRatio = 0.1f;

    [Header("生成容器")]
    [SerializeField] private Transform generatedRoot;

    [Header("收集模式")]
    [Tooltip("能量方塊生成完畢後重置進度與計時")]
    [SerializeField] private WardenCollectionManager collectionManager;

    [Header("事件")]
    [SerializeField] private UnityEvent onMapGenerated;

    private const float SpaceX = 80f;
    private const float SpaceY = 40f;
    private const float SpaceZ = 50f;
    private const float MinSize = 1f;
    private const float MaxSize = 3f;
    private const float PlatformThickness = 0.5f;
    private const int MinPlatforms = 80;
    private const int MaxPlatforms = 120;

    /// <summary>能量方塊與任一平台中心的最小距離（公尺）。</summary>
    private const float EnergyPickupMinDistFromPlatformCenter = 2f;

    /// <summary>能量方塊彼此中心的最小距離（公尺）。</summary>
    private const float EnergyPickupMinDistBetween = 2f;

    /// <summary>能量方塊世界 Y 範圍（避開貼地與貼頂）。</summary>
    private const float EnergyPickupYMin = 1f;
    private const float EnergyPickupYMax = 39f;

    /// <summary>單一方塊隨機位置最大嘗試次數。</summary>
    private const int EnergyPickupMaxAttemptsPerItem = 200;

    private static readonly Vector3 StartPos = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 FixedStartSize = new Vector3(3f, PlatformThickness, 3f);

    private System.Random _rng;
    private bool _initialGenerateFromAwake;

    private struct PlatformData
    {
        public Vector3 Position;
        public Vector3 Size;
        public bool IsStart;
        public MaterialType MaterialType;
    }

    /// <summary>首次生成於 Awake（執行順序 40，晚於多數系統）。</summary>
    private void Awake()
    {
        GenerateMap();
        _initialGenerateFromAwake = true;
    }

    /// <summary>若 Awake 未跑完，由 Start 補一次生成。</summary>
    private void Start()
    {
        if (_initialGenerateFromAwake)
            return;
        GenerateMap();
    }

    private void Update()
    {
        if (IsRegeneratePressed())
            GenerateMap();
    }

    /// <summary>
    /// 使用<strong>新隨機種子</strong>重新執行場景生成（起點／終點、平台列表與材質抽樣）。
    /// 首次於 <see cref="Awake"/> 呼叫時亦會換新種子；若需固定首局種子請在生成後於 Inspector 調整流程。
    /// </summary>
    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        if (platformPrefab == null)
        {
            Debug.LogError(
                "[WardenRoomGenerator] 未指派 platformPrefab，無法生成地圖。請在場景的 SceneGenerator 上指定平台 Prefab（Build 後若遺失參照也會如此）。");
            return;
        }

        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        _rng = new System.Random(seed);
        EnsureRoot();
        ClearGenerated();

        List<PlatformData> platforms = BuildPlatformLayout();
        SpawnPlatforms(platforms);
        int spawnedPickups = SpawnEnergyPickups(platforms);
        NotifyCollectionManagerReset(spawnedPickups);
        onMapGenerated?.Invoke();
    }

    private static bool IsRegeneratePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private void EnsureRoot()
    {
        if (generatedRoot != null) return;
        GameObject go = new GameObject("GeneratedMap");
        go.transform.SetParent(transform, false);
        generatedRoot = go.transform;
    }

    private void ClearGenerated()
    {
        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            Destroy(generatedRoot.GetChild(i).gameObject);
    }

    /// <summary>
    /// 清除上一局留在 <see cref="generatedRoot"/> 下的能量方塊（名稱前綴或 Tag）。
    /// 註：<see cref="ClearGenerated"/> 已會清空整個容器，此方法供單獨呼叫時保險用。
    /// </summary>
    private void ClearExistingEnergyPickups()
    {
        if (generatedRoot == null)
            return;

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            GameObject go = generatedRoot.GetChild(i).gameObject;
            if (go.name.StartsWith("EnergyPickup_", StringComparison.Ordinal))
            {
                Destroy(go);
                continue;
            }

            if (go.CompareTag("EnergyPickup"))
                Destroy(go);
        }
    }

    /// <summary>
    /// 在 80×40×50 空間內撒落能量方塊：遠離平台中心與彼此，Y 介於 1～39，使用與地圖相同的 <see cref="_rng"/>。
    /// </summary>
    /// <returns>實際生成數量（可能小於 <see cref="energyPickupCount"/>）。</returns>
    private int SpawnEnergyPickups(List<PlatformData> platforms)
    {
        if (energyPickupPrefab == null)
            return 0;

        if (energyPickupCount <= 0)
            return 0;

        EnsureRoot();
        ClearExistingEnergyPickups();

        var spawnedCenters = new List<Vector3>(energyPickupCount);

        for (int i = 0; i < energyPickupCount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < EnergyPickupMaxAttemptsPerItem; attempt++)
            {
                float x = RandomRange(0f, SpaceX);
                float y = RandomRange(EnergyPickupYMin, EnergyPickupYMax);
                float z = RandomRange(0f, SpaceZ);
                Vector3 candidate = new Vector3(x, y, z);

                if (!IsValidEnergyPickupPosition(candidate, platforms, spawnedCenters))
                    continue;

                GameObject go = Instantiate(energyPickupPrefab, candidate, Quaternion.identity, generatedRoot);
                go.name = $"EnergyPickup_{i:000}";
                spawnedCenters.Add(candidate);
                placed = true;
                break;
            }

            if (!placed)
                break;
        }

        return spawnedCenters.Count;
    }

    /// <summary>通知收集管理器本局能量方塊總數（含 0）。</summary>
    private void NotifyCollectionManagerReset(int spawnedPickups)
    {
        if (collectionManager == null)
            collectionManager = UnityEngine.Object.FindFirstObjectByType<WardenCollectionManager>();
        if (collectionManager == null)
            return;
        collectionManager.ResetForNewRun(spawnedPickups);
        // 能量方塊改於 Awake 初始化時不再各自 Register（會早於 Reset）；改由生成端在 Reset 後補登記。
        for (int i = 0; i < spawnedPickups; i++)
            collectionManager.RegisterPickup();
    }

    /// <summary>與所有平台中心距離皆 &gt; 2m，且與已放方塊中心距離皆 ≥ 2m。</summary>
    private bool IsValidEnergyPickupPosition(Vector3 p, List<PlatformData> platforms, List<Vector3> existingPickups)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            if (Vector3.Distance(p, platforms[i].Position) <= EnergyPickupMinDistFromPlatformCenter)
                return false;
        }

        for (int i = 0; i < existingPickups.Count; i++)
        {
            if (Vector3.Distance(p, existingPickups[i]) < EnergyPickupMinDistBetween)
                return false;
        }

        return true;
    }

    private List<PlatformData> BuildPlatformLayout()
    {
        int totalCount = _rng.Next(MinPlatforms, MaxPlatforms + 1);
        List<PlatformData> platforms = new List<PlatformData>(totalCount);

        PlatformData start = new PlatformData
        {
            Position = StartPos,
            Size = FixedStartSize,
            IsStart = true,
            MaterialType = MaterialType.Concrete
        };
        platforms.Add(start);

        int safety = 0;
        while (platforms.Count < totalCount && safety++ < 4000)
            TryAddRandomPlatform(platforms);

        return platforms;
    }

    /// <summary>
    /// 在整個空間均勻隨機撒點：不依附既有平台，只檢查邊界與不重疊。
    /// </summary>
    private bool TryAddRandomPlatform(List<PlatformData> platforms)
    {
        for (int retry = 0; retry < 120; retry++)
        {
            Vector3 size = RandomSize();
            Vector3 pos = ClampCenterInsideBounds(
                new Vector3(
                    RandomRange(0f, SpaceX),
                    RandomRange(0f, SpaceY),
                    RandomRange(0f, SpaceZ)),
                size);
            // 隨機撒點平台也必須在生成當下決定材質類型，避免只剩補路徑平台才有 Lava/Ice。
            PlatformData candidate = new PlatformData
            {
                Position = pos,
                Size = size,
                MaterialType = PickRandomMaterialType()
            };

            if (OverlapsAny(candidate, platforms)) continue;

            platforms.Add(candidate);
            return true;
        }

        return false;
    }

    private static bool OverlapsAny(PlatformData candidate, List<PlatformData> list)
    {
        Bounds c = new Bounds(candidate.Position, candidate.Size);
        for (int i = 0; i < list.Count; i++)
        {
            Bounds o = new Bounds(list[i].Position, list[i].Size);
            if (c.Intersects(o)) return true;
        }
        return false;
    }

    private Vector3 RandomSize()
    {
        return new Vector3(
            RandomRange(MinSize, MaxSize),
            PlatformThickness,
            RandomRange(MinSize, MaxSize));
    }

    private Vector3 ClampCenterInsideBounds(Vector3 pos, Vector3 size)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        pos.x = Mathf.Clamp(pos.x, hx, SpaceX - hx);
        pos.y = Mathf.Clamp(pos.y, hy, SpaceY - hy);
        pos.z = Mathf.Clamp(pos.z, hz, SpaceZ - hz);
        return pos;
    }

    private float RandomRange(float min, float max)
    {
        return (float)(min + (_rng.NextDouble() * (max - min)));
    }

    private void SpawnPlatforms(List<PlatformData> platforms)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformData p = platforms[i];
            GameObject go = Instantiate(platformPrefab, p.Position, Quaternion.identity, generatedRoot);
            go.transform.localScale = p.Size;

            if (p.IsStart) go.name = "Platform_Start";
            else go.name = $"Platform_{i:000}";

            // 起點固定為 Concrete，其他平台使用抽樣材質類型。
            MaterialType type = p.IsStart ? MaterialType.Concrete : p.MaterialType;
            ApplyMaterial(go, type);

            // 供鋼索 Raycast 判定平台材質（水泥／岩漿／冰皆可勾）。
            PlatformType platformType = go.GetComponent<PlatformType>();
            if (platformType == null)
                platformType = go.AddComponent<PlatformType>();
            platformType.SetMaterialType(type);
        }
    }

    /// <summary>
    /// 依平台材質類型套用對應 Material。
    /// 日後新增材質時，請在 MaterialType 與此方法補上對應分支。
    /// </summary>
    private void ApplyMaterial(GameObject root, MaterialType type)
    {
        Material mat = GetMaterialByType(type);
        if (mat == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] arr = renderers[i].sharedMaterials;
            for (int j = 0; j < arr.Length; j++) arr[j] = mat;
            renderers[i].sharedMaterials = arr;
        }
    }

    /// <summary>
    /// 根據啟用開關與權重抽取材質：
    /// - enableLava / enableIce 為 false 時，該權重視為 0
    /// - Concrete 權重固定為 1，三者加總可為任意正值
    /// - 以總權重做區間抽樣，等同自動正規化
    /// </summary>
    private MaterialType PickRandomMaterialType()
    {
        float activeLava = enableLava ? Mathf.Max(0f, lavaRatio) : 0f;
        float activeIce = enableIce ? Mathf.Max(0f, iceRatio) : 0f;
        float concreteRatio = 1f;

        float total = concreteRatio + activeLava + activeIce;
        if (total <= 0.0001f)
            return MaterialType.Concrete;

        float roll = (float)_rng.NextDouble() * total;
        if (roll < concreteRatio)
            return MaterialType.Concrete;

        roll -= concreteRatio;
        if (roll < activeLava)
            return MaterialType.Lava;

        return MaterialType.Ice;
    }

    private Material GetMaterialByType(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Lava:
                return matLava != null ? matLava : matCementGrey;
            case MaterialType.Ice:
                return matIce != null ? matIce : matCementGrey;
            case MaterialType.Concrete:
            default:
                return matCementGrey;
        }
    }
}
