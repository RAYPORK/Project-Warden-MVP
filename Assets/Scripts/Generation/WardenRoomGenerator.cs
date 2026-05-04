using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 沿 Z 軸向前無限串流生成隨機平台區段；啟用 <see cref="enableDeathWall"/> 時以持續前推的死亡牆 Z 回收後方平台，
/// 玩家落於牆後則呼叫 <see cref="WardenDeathManager.BeginDeathSequence"/>；死亡牆計時自「玩家開始前進」起算，
/// 邏輯 Z 可由 <see cref="deathWallInitialOffset"/> 自起點後方開始，關閉時改回依玩家距離回收。
/// 預設 <see cref="DefaultExecutionOrder"/> 為 40，於 Controller／Winch 之後 Awake，減少與其他系統的競態。
/// </summary>
[DefaultExecutionOrder(40)]
[DisallowMultipleComponent]
public class WardenRoomGenerator : MonoBehaviour
{
    [Header("隨機種子")]
    [SerializeField] private int seed = 42001;

    [Header("串流生成 — 玩家與區段")]
    [Tooltip("玩家 Transform；未指派時於 Start 以 Tag「Player」尋找")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("每個區段沿 Z 軸的深度（公尺）")]
    [Range(20f, 100f)]
    [SerializeField] private float chunkDepth = 50f;

    [Tooltip("玩家前方多遠（沿 Z）開始生成下一段")]
    [Range(30f, 150f)]
    [SerializeField] private float generateAheadDistance = 80f;

    [Tooltip("僅在關閉死亡牆（enableDeathWall）時：玩家後方多遠（沿 Z）以外的平台回收進 Pool")]
    [Range(30f, 150f)]
    [SerializeField] private float destroyBehindDistance = 60f;

    [Tooltip("區段在 X 軸的總寬度（公尺），以世界 X=0 為中心")]
    [Range(20f, 200f)]
    [SerializeField] private float chunkWidth = 80f;

    [Tooltip("每個區段隨機平台數量（不含起點；第一區段會額外生成一塊起點平台）")]
    [Range(10, 200)]
    [SerializeField] private int platformsPerChunk = 30;

    [Header("Prefab")]
    [SerializeField] private GameObject platformPrefab;

    [Header("能量方塊")]
    [Tooltip("須含 WardenEnergyPickup；可選擇命名 EnergyPickup_xxx 或 Tag「EnergyPickup」以便清除")]
    [SerializeField] private GameObject energyPickupPrefab;

    [Tooltip("每次重置／首局在第一區段空間內撒落的數量")]
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

    [Header("物件池根節點（可空，將自動建立）")]
    [SerializeField] private Transform platformPoolRoot;

    [Header("風扇障礙")]
    [Tooltip("可選；未指派則不生成風扇")]
    [SerializeField] private GameObject fanObstaclePrefab;

    [Tooltip("沿 Z 超過此距離後才開始累加風扇出現機率（公尺）")]
    [SerializeField] private float fanStartDistance = 200f;

    [Tooltip("機率漸進至滿時的上限（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float fanMaxChance = 0.2f;

    [Tooltip("從起點距離到機率達上限所經過的 Z 距離（公尺）；過小時避免除零")]
    [SerializeField] private float fanRampDistance = 200f;

    [Header("電擊球")]
    [Tooltip("須含 ElectricOrb（及 Trigger 等）；未指派則不生成")]
    [SerializeField]
    private GameObject electricOrbPrefab;

    [Tooltip("沿 Z（區段起點世界 Z）超過此距離後才開始累加電擊球出現機率")]
    [SerializeField]
    private float orbStartDistance = 50f;

    [Tooltip("最大出現機率（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float orbMaxChance = 0.5f;

    [Tooltip("機率爬升距離（公尺）；過小時避免除零")]
    [SerializeField]
    private float orbRampDistance = 100f;

    [Tooltip("每個區段最多生成幾個電擊球（密集度）")]
    [Range(1, 10)]
    [SerializeField]
    private int orbMaxPerChunk = 3;

    [Header("砲台")]
    [Tooltip("須含 Turret（及飛彈 Prefab 等）；未指派則不生成")]
    [SerializeField]
    private GameObject turretPrefab;

    [Tooltip("沿 Z（區段起點世界 Z）超過此距離後才開始累加砲台出現機率")]
    [SerializeField]
    private float turretStartDistance = 100f;

    [Tooltip("最大出現機率（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float turretMaxChance = 0.3f;

    [Tooltip("機率爬升距離（公尺）；過小時避免除零")]
    [SerializeField]
    private float turretRampDistance = 150f;

    [Tooltip("每個區段最多嘗試生成幾個砲台")]
    [Range(1, 5)]
    [SerializeField]
    private int turretMaxPerChunk = 2;

    [Header("雷射柱")]
    [Tooltip("須含 LaserPillar；未指派則不生成")]
    [SerializeField]
    private GameObject laserPillarPrefab;

    [Tooltip("沿 Z（區段起點世界 Z）超過此距離後才開始累加雷射柱出現機率")]
    [SerializeField]
    private float laserStartDistance = 100f;

    [Tooltip("最大出現機率（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float laserMaxChance = 0.4f;

    [Tooltip("機率爬升距離（公尺）；過小時避免除零")]
    [SerializeField]
    private float laserRampDistance = 150f;

    [Tooltip("每個區段最多嘗試生成幾根雷射柱")]
    [Range(1, 10)]
    [SerializeField]
    private int laserMaxPerChunk = 3;

    [Header("氣流噴射口")]
    [Tooltip("須含 AirVent（及 Trigger 等）；未指派則不生成")]
    [SerializeField]
    private GameObject airVentPrefab;

    [Tooltip("沿 Z（區段起點世界 Z）超過此距離後才開始累加噴射口出現機率")]
    [SerializeField]
    private float ventStartDistance = 0f;

    [Tooltip("最大出現機率（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float ventMaxChance = 0.4f;

    [Tooltip("機率爬升距離（公尺）；過小時避免除零")]
    [SerializeField]
    private float ventRampDistance = 100f;

    [Tooltip("每個區段最多生成幾個氣流噴射口")]
    [Range(1, 10)]
    [SerializeField]
    private int ventMaxPerChunk = 2;

    [Header("碎裂平台")]
    [Tooltip("須含 CollapsiblePlatform（及可勾表面如 PlatformType）；未指派則不生成")]
    [SerializeField]
    private GameObject collapsiblePlatformPrefab;

    [Tooltip("沿 Z（區段起點世界 Z）超過此距離後才開始累加碎裂平台出現機率（公尺）")]
    [SerializeField]
    private float collapsibleStartDistance = 100f;

    [Tooltip("機率漸進至滿時的上限（0～1）；計算方式與風扇相同，見 GetObstacleChance")]
    [Range(0f, 1f)]
    [SerializeField]
    private float collapsibleMaxChance = 0.3f;

    [Tooltip("從起點距離到機率達上限所經過的 Z 距離（公尺）；過小時避免除零")]
    [SerializeField]
    private float collapsibleRampDistance = 150f;

    [Tooltip("每個區段最多嘗試生成幾個碎裂平台")]
    [Range(1, 10)]
    [SerializeField]
    private int collapsibleMaxPerChunk = 3;

    [Header("死亡牆")]
    [Tooltip("啟用後以固定速度沿 +Z 推進死亡線；平台中心落於牆後回收，玩家落於牆後觸發死亡")]
    [SerializeField]
    private bool enableDeathWall = true;

    [Tooltip("緩衝結束後，死亡牆每秒沿世界 +Z 推進的公尺數")]
    [Range(1f, 20f)]
    [SerializeField]
    private float deathWallSpeed = 5f;

    [Tooltip("本局／重置後經過幾秒才開始推進死亡牆（秒）")]
    [SerializeField]
    private float deathWallStartDelay = 3f;

    [Tooltip("死亡牆邏輯 Z 的初始值（公尺）；負值表示在玩家起點（約 Z=0）後方，例如 -10 即牆先位於起點後方 10m，倒數結束後再向 +Z 追趕")]
    [SerializeField]
    private float deathWallInitialOffset = -10f;

    [Tooltip("玩家落於牆後時呼叫；未指派則於執行時尋找 WardenDeathManager")]
    [SerializeField]
    private WardenDeathManager deathManager;

    [Header("死亡牆加速")]
    [Tooltip("啟用後死亡牆速度會隨時間增加")]
    [SerializeField]
    private bool enableDeathWallAcceleration = true;

    [Tooltip("每秒增加的速度（公尺／秒²）")]
    [Range(0f, 2f)]
    [SerializeField]
    private float deathWallAcceleration = 0.1f;

    [Tooltip("死亡牆速度上限（公尺／秒）")]
    [Range(1f, 50f)]
    [SerializeField]
    private float deathWallMaxSpeed = 20f;

    [Header("收集模式")]
    [Tooltip("能量方塊生成完畢後重置進度與計時")]
    [SerializeField] private WardenCollectionManager collectionManager;

    [Header("事件")]
    [SerializeField] private UnityEvent onMapGenerated;

    private const float SpaceY = 40f;
    private const float MinSize = 2f;
    private const float MaxSize = 6f;
    private const float PlatformThickness = 0.5f;

    /// <summary>能量方塊與任一平台中心的最小距離（公尺）。</summary>
    private const float EnergyPickupMinDistFromPlatformCenter = 2f;

    /// <summary>能量方塊彼此中心的最小距離（公尺）。</summary>
    private const float EnergyPickupMinDistBetween = 2f;

    /// <summary>能量方塊世界 Y 範圍（避開貼地與貼頂）。</summary>
    private const float EnergyPickupYMin = 1f;
    private const float EnergyPickupYMax = 39f;

    /// <summary>單一方塊隨機位置最大嘗試次數。</summary>
    private const int EnergyPickupMaxAttemptsPerItem = 200;

    // 平台中心往後移，讓平台從 Z=0 往後延伸到 Z=-15
    private static readonly Vector3 StartPos = new Vector3(0f, 0f, -7f);
    private static readonly Vector3 FixedStartSize = new Vector3(80f, PlatformThickness, 15f);

    private System.Random _rng;

    /// <summary>已生成區段數（第一區段生成完畢後為 1）。</summary>
    private int chunksGenerated;

    private readonly Stack<GameObject> platformPool = new Stack<GameObject>();

    private struct PlatformData
    {
        public Vector3 Position;
        public Vector3 Size;
        public bool IsStart;
        public MaterialType MaterialType;
    }

    /// <summary>目前場上活躍平台（與 <see cref="_activeLayout"/> 索引對齊）。</summary>
    private readonly List<GameObject> _activePlatforms = new List<GameObject>();

    /// <summary>與活躍平台對應的版面資料（重疊檢查、能量方塊避讓）。</summary>
    private readonly List<PlatformData> _activeLayout = new List<PlatformData>();

    /// <summary>本局已生成之風扇（供重置與後方銷毀）。</summary>
    private readonly List<GameObject> _activeFanObstacles = new List<GameObject>();

    /// <summary>本局已生成之電擊球（供重置與死亡牆／玩家後方銷毀）。</summary>
    private readonly List<GameObject> _activeElectricOrbs = new List<GameObject>();

    /// <summary>本局已生成之砲台（供重置與死亡牆／玩家後方銷毀）。</summary>
    private readonly List<GameObject> _activeTurrets = new List<GameObject>();

    /// <summary>本局已生成之雷射柱（供重置與死亡牆／玩家後方銷毀）。</summary>
    private readonly List<GameObject> _activeLaserPillars = new List<GameObject>();

    /// <summary>本局已生成之氣流噴射口（供重置與死亡牆／玩家後方銷毀）。</summary>
    private readonly List<GameObject> _activeAirVents = new List<GameObject>();

    /// <summary>本局已生成之碎裂平台實例（與 <see cref="_collapsibleOverlapEntries"/> 索引對齊）。</summary>
    private readonly List<GameObject> _activeCollapsiblePlatforms = new List<GameObject>();

    /// <summary>碎裂平台用於與後續區段隨機平台重疊檢查的邊界資料（無對應 Pool 物件）。</summary>
    private readonly List<PlatformData> _collapsibleOverlapEntries = new List<PlatformData>();

    /// <summary>死亡牆目前世界 Z（+Z 為前進方向）；開局為 <see cref="deathWallInitialOffset"/>，推進後遞增。可供 HUD 顯示。</summary>
    private float _deathWallZ;

    /// <summary>玩家已開始前進後，累計用於 <see cref="deathWallStartDelay"/> 的秒數（未移動前不加）。</summary>
    private float _deathWallRunTime;

    /// <summary>玩家世界 Z 是否已超過門檻，通過後才開始累計死亡牆延遲與推進時間。</summary>
    private bool _playerHasStartedMoving;

    /// <summary>死亡牆目前沿 +Z 的推進速度（公尺／秒）；開局與重置時等於 <see cref="deathWallSpeed"/>，啟用加速時每幀遞增至上限。</summary>
    private float _currentDeathWallSpeed;

    /// <summary>死亡牆目前世界 Z 位置（供 HUD 等讀取）。</summary>
    public float DeathWallZ => _deathWallZ;

    /// <summary>死亡牆邏輯 Z 初值（與 Inspector 同步）；供 <see cref="DeathWallVisual"/> 判斷是否為負向起點。</summary>
    public float DeathWallInitialOffset => deathWallInitialOffset;

    /// <summary>是否啟用死亡牆邏輯。</summary>
    public bool EnableDeathWall => enableDeathWall;

    private void Awake()
    {
        EnsureRoot();
        EnsurePoolRoot();
    }

    private void Start()
    {
        if (platformPrefab == null)
        {
            Debug.LogError(
                "[WardenRoomGenerator] 未指派 platformPrefab，無法生成地圖。請在場景的 SceneGenerator 上指定平台 Prefab（Build 後若遺失參照也會如此）。");
            return;
        }

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                playerTransform = p.transform;
        }

        EnsureDeathManagerReference();

        // 首局使用 Inspector 種子建立決定性 RNG
        _rng = new System.Random(seed);
        chunksGenerated = 0;
        GenerateNextChunk();
        chunksGenerated++;

        int spawnedPickups = SpawnEnergyPickupsInFirstChunk();
        NotifyCollectionManagerReset(spawnedPickups);
        onMapGenerated?.Invoke();

        // 死亡牆自起點後方偏移開始（預設 -10m）；玩家開始移動並經 deathWallStartDelay 後才向 +Z 推進。
        _deathWallZ = deathWallInitialOffset;
        _currentDeathWallSpeed = deathWallSpeed;
    }

    private void Update()
    {
        if (IsRegeneratePressed())
        {
            ResetStreamingWorld();
            return;
        }

        if (playerTransform == null)
            return;

        float pz = playerTransform.position.z;

        // 玩家越過閾值時持續補齊前方區段（單幀可補多段以防落後）
        while (pz > chunksGenerated * chunkDepth - generateAheadDistance)
        {
            GenerateNextChunk();
            chunksGenerated++;
        }

        if (enableDeathWall)
        {
            UpdateDeathWall();
            TryKillPlayerWithDeathWall(pz);
            RecyclePlatformsBehindDeathWall();
            DestroyFanObstaclesBehindDeathWall();
            DestroyElectricOrbsBehindDeathWall();
            DestroyTurretsBehindDeathWall();
            DestroyLaserPillarsBehindDeathWall();
            DestroyAirVentsBehindDeathWall();
            DestroyCollapsiblePlatformsBehindDeathWall();
        }
        else
        {
            RecyclePlatformsBehindPlayer(pz);
            DestroyFanObstaclesBehindPlayer(pz);
            DestroyElectricOrbsBehindPlayer(pz);
            DestroyTurretsBehindPlayer(pz);
            DestroyLaserPillarsBehindPlayer(pz);
            DestroyAirVentsBehindPlayer(pz);
            DestroyCollapsiblePlatformsBehindPlayer(pz);
        }

        CompactDestroyedCollapsiblePlatforms();
    }

    /// <summary>若未在 Inspector 指派，於執行時尋找 <see cref="WardenDeathManager"/>。</summary>
    private void EnsureDeathManagerReference()
    {
        if (deathManager != null)
            return;
        deathManager = UnityEngine.Object.FindFirstObjectByType<WardenDeathManager>();
    }

    /// <summary>
    /// 偵測玩家是否已開始前進；通過後才累計 <see cref="_deathWallRunTime"/>，超過 <see cref="deathWallStartDelay"/> 再推進 <see cref="_deathWallZ"/>。
    /// 推進量使用 <see cref="_currentDeathWallSpeed"/>；若 <see cref="enableDeathWallAcceleration"/> 為 true，速度每幀依加速度遞增至 <see cref="deathWallMaxSpeed"/>。
    /// </summary>
    private void UpdateDeathWall()
    {
        // 偵測玩家是否開始移動（世界 Z 超過 1m 視為已離開起點附近）。
        if (!_playerHasStartedMoving && playerTransform != null)
        {
            float pz = playerTransform.position.z;
            if (pz > 1f)
                _playerHasStartedMoving = true;
        }

        // 只有玩家開始移動後才累計延遲與牆面前進。
        if (!_playerHasStartedMoving)
            return;

        _deathWallRunTime += Time.deltaTime;
        if (_deathWallRunTime < deathWallStartDelay)
            return;

        if (enableDeathWallAcceleration)
        {
            _currentDeathWallSpeed += deathWallAcceleration * Time.deltaTime;
            _currentDeathWallSpeed = Mathf.Min(_currentDeathWallSpeed, deathWallMaxSpeed);
        }

        _deathWallZ += _currentDeathWallSpeed * Time.deltaTime;
    }

    /// <summary>玩家世界 Z 落於死亡牆之後（較小）時觸發結算死亡流程。</summary>
    private void TryKillPlayerWithDeathWall(float playerZ)
    {
        if (!_playerHasStartedMoving || _deathWallRunTime < deathWallStartDelay)
            return;

        EnsureDeathManagerReference();
        if (deathManager == null || deathManager.isDead)
            return;

        if (playerZ < _deathWallZ)
            deathManager.BeginDeathSequence();
    }

    /// <summary>平台版面中心 Z 已落於死亡牆之後者，回收到 Pool。</summary>
    private void RecyclePlatformsBehindDeathWall()
    {
        for (int i = _activePlatforms.Count - 1; i >= 0; i--)
        {
            if (_activeLayout[i].Position.z < _deathWallZ)
            {
                ReturnPlatformToPool(_activePlatforms[i]);
                _activePlatforms.RemoveAt(i);
                _activeLayout.RemoveAt(i);
            }
        }
    }

    /// <summary>風扇中心 Z 落於死亡牆之後者銷毀。</summary>
    private void DestroyFanObstaclesBehindDeathWall()
    {
        for (int i = _activeFanObstacles.Count - 1; i >= 0; i--)
        {
            GameObject fan = _activeFanObstacles[i];
            if (fan == null)
            {
                _activeFanObstacles.RemoveAt(i);
                continue;
            }

            if (fan.transform.position.z < _deathWallZ)
            {
                Destroy(fan);
                _activeFanObstacles.RemoveAt(i);
            }
        }
    }

    /// <summary>電擊球中心 Z 落於死亡牆之後者銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindDeathWall"/>）。</summary>
    private void DestroyElectricOrbsBehindDeathWall()
    {
        for (int i = _activeElectricOrbs.Count - 1; i >= 0; i--)
        {
            GameObject orb = _activeElectricOrbs[i];
            if (orb == null)
            {
                _activeElectricOrbs.RemoveAt(i);
                continue;
            }

            if (orb.transform.position.z < _deathWallZ)
            {
                Destroy(orb);
                _activeElectricOrbs.RemoveAt(i);
            }
        }
    }

    /// <summary>砲台中心 Z 落於死亡牆之後者銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindDeathWall"/>）。</summary>
    private void DestroyTurretsBehindDeathWall()
    {
        for (int i = _activeTurrets.Count - 1; i >= 0; i--)
        {
            GameObject turret = _activeTurrets[i];
            if (turret == null)
            {
                _activeTurrets.RemoveAt(i);
                continue;
            }

            if (turret.transform.position.z < _deathWallZ)
            {
                Destroy(turret);
                _activeTurrets.RemoveAt(i);
            }
        }
    }

    /// <summary>雷射柱中心 Z 落於死亡牆之後者銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindDeathWall"/>）。</summary>
    private void DestroyLaserPillarsBehindDeathWall()
    {
        for (int i = _activeLaserPillars.Count - 1; i >= 0; i--)
        {
            GameObject pillar = _activeLaserPillars[i];
            if (pillar == null)
            {
                _activeLaserPillars.RemoveAt(i);
                continue;
            }

            if (pillar.transform.position.z < _deathWallZ)
            {
                Destroy(pillar);
                _activeLaserPillars.RemoveAt(i);
            }
        }
    }

    /// <summary>氣流噴射口中心 Z 落於死亡牆之後者銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindDeathWall"/>）。</summary>
    private void DestroyAirVentsBehindDeathWall()
    {
        for (int i = _activeAirVents.Count - 1; i >= 0; i--)
        {
            GameObject vent = _activeAirVents[i];
            if (vent == null)
            {
                _activeAirVents.RemoveAt(i);
                continue;
            }

            if (vent.transform.position.z < _deathWallZ)
            {
                Destroy(vent);
                _activeAirVents.RemoveAt(i);
            }
        }
    }

    /// <summary>碎裂平台中心 Z 落於死亡牆之後者銷毀並同步移除重疊快取。</summary>
    private void DestroyCollapsiblePlatformsBehindDeathWall()
    {
        for (int i = _activeCollapsiblePlatforms.Count - 1; i >= 0; i--)
        {
            GameObject go = _activeCollapsiblePlatforms[i];
            if (go == null)
            {
                _activeCollapsiblePlatforms.RemoveAt(i);
                if (i < _collapsibleOverlapEntries.Count)
                    _collapsibleOverlapEntries.RemoveAt(i);
                continue;
            }

            if (go.transform.position.z < _deathWallZ)
            {
                Destroy(go);
                _activeCollapsiblePlatforms.RemoveAt(i);
                if (i < _collapsibleOverlapEntries.Count)
                    _collapsibleOverlapEntries.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 與按 R 相同：清空場上平台（回收到 Pool）、換新種子、只生成第一區段並重撒能量方塊。
    /// 供死亡重生等流程呼叫。
    /// </summary>
    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ResetStreamingWorld();
    }

    /// <summary>
    /// 按 R 或 <see cref="GenerateMap"/>：回收所有平台、換種子、重生第一區段。
    /// </summary>
    private void ResetStreamingWorld()
    {
        if (platformPrefab == null)
        {
            Debug.LogError("[WardenRoomGenerator] 未指派 platformPrefab，無法重置。");
            return;
        }

        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        _rng = new System.Random(seed);

        EnsureRoot();
        EnsurePoolRoot();
        RecycleAllActivePlatforms();
        DestroyEnergyPickupsUnderRoot();
        DestroyAllFanObstacles();
        DestroyAllElectricOrbs();
        DestroyAllTurrets();
        DestroyAllLaserPillars();
        DestroyAllAirVents();
        DestroyAllCollapsiblePlatforms();

        // 與首局相同：牆先置於起點後方，再依玩家移動與延遲推進。
        _deathWallZ = deathWallInitialOffset;
        _deathWallRunTime = 0f;
        _playerHasStartedMoving = false;
        _currentDeathWallSpeed = deathWallSpeed;

        chunksGenerated = 0;
        GenerateNextChunk();
        chunksGenerated++;

        int spawnedPickups = SpawnEnergyPickupsInFirstChunk();
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

    private void EnsurePoolRoot()
    {
        if (platformPoolRoot != null)
            return;
        GameObject poolGo = new GameObject("PlatformPool");
        poolGo.transform.SetParent(transform, false);
        poolGo.SetActive(false);
        platformPoolRoot = poolGo.transform;
    }

    /// <summary>
    /// 生成索引為 <see cref="chunksGenerated"/> 的區段（Z 由 chunksGenerated * chunkDepth 至下一邊界）。
    /// </summary>
    private void GenerateNextChunk()
    {
        int chunkIndex = chunksGenerated;
        List<PlatformData> chunkList = BuildChunkPlatformLayout(chunkIndex);
        for (int i = 0; i < chunkList.Count; i++)
        {
            PlatformData p = chunkList[i];
            GameObject go = RentPlatformFromPool();
            go.transform.SetParent(generatedRoot, false);
            go.transform.SetPositionAndRotation(p.Position, Quaternion.identity);
            go.transform.localScale = p.Size;

            if (p.IsStart) go.name = "Platform_Start";
            else go.name = $"Platform_c{chunkIndex}_{i:000}";

            // 起點平台設為 StartPlatform Layer
            if (p.IsStart)
            {
                int startLayer = LayerMask.NameToLayer("StartPlatform");
                go.layer = startLayer;
            }

            MaterialType type = p.IsStart ? MaterialType.Concrete : p.MaterialType;
            ApplyMaterial(go, type);

            PlatformType platformType = go.GetComponent<PlatformType>();
            if (platformType == null)
                platformType = go.AddComponent<PlatformType>();
            platformType.SetMaterialType(type);

            _activePlatforms.Add(go);
            _activeLayout.Add(p);
        }

        // 風扇：依目前區段起點 Z（等同 chunksGenerated * chunkDepth）漸進機率，於區段中央擇一生成
        TrySpawnFanObstacleForChunk(chunkIndex);

        // 碎裂平台：機率模型與風扇相同（GetObstacleChance），通過時於區段內隨機擇一空位生成一塊
        TrySpawnCollapsiblePlatformForChunk(chunkIndex);

        // 電擊球：每區段多次擲骰，通過者於區段盒狀範圍內隨機座標生成
        TrySpawnElectricOrbsForChunk(chunkIndex);

        // 砲台：邏輯與電擊球相同，垂直範圍為 Y 5～35
        TrySpawnTurretsForChunk(chunkIndex);

        // 雷射柱：邏輯與電擊球相同，區段盒內隨機 X／Y／Z（Y 為 5～35）
        TrySpawnLaserPillarsForChunk(chunkIndex);

        // 氣流噴射口：邏輯與電擊球相同，垂直範圍為 Y 2～35
        TrySpawnAirVentsForChunk(chunkIndex);
    }

    /// <summary>
    /// 依玩家／區段沿 Z 的進度計算障礙出現機率（起點前為 0，超過 ramp 後達 maxChance）。
    /// </summary>
    private float GetObstacleChance(float playerZ, float startDistance, float maxChance, float rampDistance)
    {
        if (playerZ < startDistance)
            return 0f;
        float progress = (playerZ - startDistance) / rampDistance;
        return Mathf.Clamp01(progress) * maxChance;
    }

    /// <summary>區段平台生成完畢後，依機率於區段中央（X=0、Y=20、Z 中央）生成風扇。</summary>
    private void TrySpawnFanObstacleForChunk(int chunkIndex)
    {
        if (fanObstaclePrefab == null)
            return;

        float zDistance = chunkIndex * chunkDepth;
        float chance = GetObstacleChance(zDistance, fanStartDistance, fanMaxChance, fanRampDistance);
        if ((float)_rng.NextDouble() >= chance)
            return;

        float zCenter = zDistance + chunkDepth * 0.5f;
        Vector3 pos = new Vector3(0f, 20f, zCenter);
        GameObject fan = Instantiate(fanObstaclePrefab, pos, Quaternion.identity, generatedRoot);
        fan.name = $"FanObstacle_{chunkIndex}";
        _activeFanObstacles.Add(fan);
    }

    /// <summary>
    /// 區段生成完畢後，最多嘗試 <see cref="collapsibleMaxPerChunk"/> 次；
    /// 每次皆須通過 <see cref="GetObstacleChance"/> 門檻與位置重疊檢查，通過後才實例化一塊碎裂平台。
    /// </summary>
    private void TrySpawnCollapsiblePlatformForChunk(int chunkIndex)
    {
        if (collapsiblePlatformPrefab == null)
            return;

        float zDistance = chunkIndex * chunkDepth;
        float ramp = Mathf.Max(1f, collapsibleRampDistance);
        float chance = GetObstacleChance(zDistance, collapsibleStartDistance, collapsibleMaxChance, ramp);

        float zMin = zDistance;
        float zMax = zDistance + chunkDepth;

        int maxAttempts = Mathf.Clamp(collapsibleMaxPerChunk, 1, 10);
        for (int count = 0; count < maxAttempts; count++)
        {
            float roll = (float)_rng.NextDouble();
            if (roll >= chance)
                continue;

            var overlapWith = new List<PlatformData>(_activeLayout.Count + _collapsibleOverlapEntries.Count);
            overlapWith.AddRange(_activeLayout);
            overlapWith.AddRange(_collapsibleOverlapEntries);

            for (int retry = 0; retry < 120; retry++)
            {
                Vector3 size = RandomSize();
                float halfW = chunkWidth * 0.5f;
                float hx = size.x * 0.5f;
                float hy = size.y * 0.5f;
                float hz = size.z * 0.5f;

                Vector3 pos = new Vector3(
                    RandomRange(-halfW + hx, halfW - hx),
                    RandomRange(0f + hy, SpaceY - hy),
                    RandomRange(zMin + hz, zMax - hz));

                PlatformData candidate = new PlatformData
                {
                    Position = pos,
                    Size = size,
                    IsStart = false,
                    MaterialType = MaterialType.Concrete
                };

                if (OverlapsAny(candidate, overlapWith))
                    continue;

                GameObject go = Instantiate(collapsiblePlatformPrefab, pos, Quaternion.identity, generatedRoot);
                go.transform.localScale = size;
                go.name = $"CollapsiblePlatform_c{chunkIndex}_{count}_{retry:000}";
                _activeCollapsiblePlatforms.Add(go);
                _collapsibleOverlapEntries.Add(candidate);
                overlapWith.Add(candidate);
                break;
            }
        }
    }

    /// <summary>
    /// 區段生成完畢後，最多嘗試 <see cref="orbMaxPerChunk"/> 次；
    /// 每次以 <see cref="GetObstacleChance"/> 擲骰，通過則於區段內隨機 X／Y／Z 生成一顆電擊球。
    /// </summary>
    private void TrySpawnElectricOrbsForChunk(int chunkIndex)
    {
        if (electricOrbPrefab == null)
            return;

        float zDistance = chunkIndex * chunkDepth;
        float ramp = Mathf.Max(1f, orbRampDistance);
        float chance = GetObstacleChance(zDistance, orbStartDistance, orbMaxChance, ramp);

        float zMin = zDistance;
        float zMax = zDistance + chunkDepth;
        float halfW = chunkWidth * 0.5f;

        int maxSpawns = Mathf.Clamp(orbMaxPerChunk, 1, 10);
        for (int n = 0; n < maxSpawns; n++)
        {
            float roll = (float)_rng.NextDouble();
            if (roll >= chance)
                continue;

            Vector3 pos = new Vector3(
                RandomRange(-halfW, halfW),
                RandomRange(5f, 35f),
                RandomRange(zMin, zMax));

            GameObject orb = Instantiate(electricOrbPrefab, pos, Quaternion.identity, generatedRoot);
            orb.name = $"ElectricOrb_c{chunkIndex}_{n:000}";
            _activeElectricOrbs.Add(orb);
        }
    }

    /// <summary>
    /// 區段生成完畢後，最多嘗試 <see cref="turretMaxPerChunk"/> 次；
    /// 每次以 <see cref="GetObstacleChance"/> 擲骰，通過則於區段內隨機 X／Y／Z 生成一座砲台（Y 為 5～35）。
    /// 砲台不套用平台用的隨機 <c>localScale</c>，實例化後維持 Prefab 預設縮放（通常為 (1,1,1)）。
    /// </summary>
    private void TrySpawnTurretsForChunk(int chunkIndex)
    {
        if (turretPrefab == null)
            return;

        float zDistance = chunkIndex * chunkDepth;
        float ramp = Mathf.Max(1f, turretRampDistance);
        float chance = GetObstacleChance(zDistance, turretStartDistance, turretMaxChance, ramp);

        float zMin = zDistance;
        float zMax = zDistance + chunkDepth;
        float halfW = chunkWidth * 0.5f;

        int maxSpawns = Mathf.Clamp(turretMaxPerChunk, 1, 5);
        for (int n = 0; n < maxSpawns; n++)
        {
            float roll = (float)_rng.NextDouble();
            if (roll >= chance)
                continue;

            Vector3 pos = new Vector3(
                RandomRange(-halfW, halfW),
                RandomRange(5f, 35f),
                RandomRange(zMin, zMax));

            // 僅設定位置與父節點；不設定 localScale，砲台維持 Prefab 的 (1,1,1)（隨機縮放僅用於平台）
            GameObject turret = Instantiate(turretPrefab, pos, Quaternion.identity, generatedRoot);
            turret.name = $"Turret_c{chunkIndex}_{n:000}";
            _activeTurrets.Add(turret);
        }
    }

    /// <summary>
    /// 區段生成完畢後，最多嘗試 <see cref="laserMaxPerChunk"/> 次；
    /// 每次以 <see cref="GetObstacleChance"/> 擲骰，通過則隨機擇上／下／左／右之一為發射軸，
    /// 於對應邊界或頂／底面生成，並呼叫 <see cref="LaserPillar.SetDirectionAndLength"/> 設定方向與長度。
    /// </summary>
    private void TrySpawnLaserPillarsForChunk(int chunkIndex)
    {
        if (laserPillarPrefab == null)
            return;

        // 第一個區段為出生／教學區，不生成雷射柱，避免開局即被擊落
        if (chunkIndex == 0)
            return;

        float zDistance = chunkIndex * chunkDepth;
        float ramp = Mathf.Max(1f, laserRampDistance);
        float chance = GetObstacleChance(zDistance, laserStartDistance, laserMaxChance, ramp);

        float zMin = zDistance;
        float zMax = zDistance + chunkDepth;
        float halfW = chunkWidth * 0.5f;

        int maxSpawns = Mathf.Clamp(laserMaxPerChunk, 1, 10);
        for (int n = 0; n < maxSpawns; n++)
        {
            float roll = (float)_rng.NextDouble();
            if (roll >= chance)
                continue;

            // 僅四向（上／下／左／右），不選前後；生成點貼邊或頂／底，長度與空間尺寸一致
            int axisKind = _rng.Next(0, 4);
            Vector3 pos;
            Vector3 fireDir;
            float laserLen;
            switch (axisKind)
            {
                case 0: // 頂部，向下發射
                    pos = new Vector3(RandomRange(-halfW, halfW), SpaceY, RandomRange(zMin, zMax));
                    fireDir = Vector3.down;
                    laserLen = SpaceY;
                    break;
                case 1: // 底部，向上發射
                    pos = new Vector3(RandomRange(-halfW, halfW), 0f, RandomRange(zMin, zMax));
                    fireDir = Vector3.up;
                    laserLen = SpaceY;
                    break;
                case 2: // 左側邊界，向右發射
                    pos = new Vector3(-halfW, RandomRange(5f, 35f), RandomRange(zMin, zMax));
                    fireDir = Vector3.right;
                    laserLen = chunkWidth;
                    break;
                default: // 右側邊界，向左發射
                    pos = new Vector3(halfW, RandomRange(5f, 35f), RandomRange(zMin, zMax));
                    fireDir = Vector3.left;
                    laserLen = chunkWidth;
                    break;
            }

            GameObject pillar = Instantiate(laserPillarPrefab, pos, Quaternion.identity, generatedRoot);
            pillar.name = $"LaserPillar_c{chunkIndex}_{n:000}";

            LaserPillar laserPillar = pillar.GetComponent<LaserPillar>();
            if (laserPillar == null)
            {
                Debug.LogWarning("[WardenRoomGenerator] laserPillarPrefab 缺少 LaserPillar 元件，已銷毀實例。");
                Destroy(pillar);
                continue;
            }

            laserPillar.SetDirectionAndLength(fireDir, laserLen);
            _activeLaserPillars.Add(pillar);
        }
    }

    /// <summary>
    /// 區段生成完畢後，最多嘗試 <see cref="ventMaxPerChunk"/> 次；
    /// 每次以 <see cref="GetObstacleChance"/> 擲骰，通過則於區段內隨機 X／Y／Z 生成一個氣流噴射口（Y 為 2～35）。
    /// </summary>
    private void TrySpawnAirVentsForChunk(int chunkIndex)
    {
        if (airVentPrefab == null)
            return;

        float zDistance = chunkIndex * chunkDepth;
        float ramp = Mathf.Max(1f, ventRampDistance);
        float chance = GetObstacleChance(zDistance, ventStartDistance, ventMaxChance, ramp);

        float zMin = zDistance;
        float zMax = zDistance + chunkDepth;
        float halfW = chunkWidth * 0.5f;

        int maxSpawns = Mathf.Clamp(ventMaxPerChunk, 1, 10);
        for (int n = 0; n < maxSpawns; n++)
        {
            float roll = (float)_rng.NextDouble();
            if (roll >= chance)
                continue;

            Vector3 pos = new Vector3(
                RandomRange(-halfW, halfW),
                RandomRange(2f, 35f),
                RandomRange(zMin, zMax));

            GameObject vent = Instantiate(airVentPrefab, pos, Quaternion.identity, generatedRoot);
            vent.name = $"AirVent_c{chunkIndex}_{n:000}";
            _activeAirVents.Add(vent);
        }
    }

    /// <summary>碎裂平台自行 Destroy 後清掉列表與重疊快取，避免索引殘留。</summary>
    private void CompactDestroyedCollapsiblePlatforms()
    {
        for (int i = _activeCollapsiblePlatforms.Count - 1; i >= 0; i--)
        {
            if (_activeCollapsiblePlatforms[i] != null)
                continue;
            _activeCollapsiblePlatforms.RemoveAt(i);
            if (i < _collapsibleOverlapEntries.Count)
                _collapsibleOverlapEntries.RemoveAt(i);
        }
    }

    private void DestroyAllCollapsiblePlatforms()
    {
        for (int i = 0; i < _activeCollapsiblePlatforms.Count; i++)
        {
            if (_activeCollapsiblePlatforms[i] != null)
                Destroy(_activeCollapsiblePlatforms[i]);
        }

        _activeCollapsiblePlatforms.Clear();
        _collapsibleOverlapEntries.Clear();
    }

    /// <summary>玩家後方過遠的碎裂平台銷毀，避免串流無限累積。</summary>
    private void DestroyCollapsiblePlatformsBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activeCollapsiblePlatforms.Count - 1; i >= 0; i--)
        {
            GameObject go = _activeCollapsiblePlatforms[i];
            if (go == null)
                continue;

            if (threshold > go.transform.position.z)
            {
                Destroy(go);
                _activeCollapsiblePlatforms.RemoveAt(i);
                if (i < _collapsibleOverlapEntries.Count)
                    _collapsibleOverlapEntries.RemoveAt(i);
            }
        }
    }

    private void DestroyAllFanObstacles()
    {
        for (int i = 0; i < _activeFanObstacles.Count; i++)
        {
            if (_activeFanObstacles[i] != null)
                Destroy(_activeFanObstacles[i]);
        }

        _activeFanObstacles.Clear();
    }

    /// <summary>重置或換局時銷毀場上所有電擊球。</summary>
    private void DestroyAllElectricOrbs()
    {
        for (int i = 0; i < _activeElectricOrbs.Count; i++)
        {
            if (_activeElectricOrbs[i] != null)
                Destroy(_activeElectricOrbs[i]);
        }

        _activeElectricOrbs.Clear();
    }

    /// <summary>重置或換局時銷毀場上所有砲台。</summary>
    private void DestroyAllTurrets()
    {
        for (int i = 0; i < _activeTurrets.Count; i++)
        {
            if (_activeTurrets[i] != null)
                Destroy(_activeTurrets[i]);
        }

        _activeTurrets.Clear();
    }

    /// <summary>重置或換局時銷毀場上所有雷射柱。</summary>
    private void DestroyAllLaserPillars()
    {
        for (int i = 0; i < _activeLaserPillars.Count; i++)
        {
            if (_activeLaserPillars[i] != null)
                Destroy(_activeLaserPillars[i]);
        }

        _activeLaserPillars.Clear();
    }

    /// <summary>重置或換局時銷毀場上所有氣流噴射口。</summary>
    private void DestroyAllAirVents()
    {
        for (int i = 0; i < _activeAirVents.Count; i++)
        {
            if (_activeAirVents[i] != null)
                Destroy(_activeAirVents[i]);
        }

        _activeAirVents.Clear();
    }

    /// <summary>玩家後方過遠的風扇直接銷毀，避免無限串流累積。</summary>
    private void DestroyFanObstaclesBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activeFanObstacles.Count - 1; i >= 0; i--)
        {
            GameObject fan = _activeFanObstacles[i];
            if (fan == null)
            {
                _activeFanObstacles.RemoveAt(i);
                continue;
            }

            if (threshold > fan.transform.position.z)
            {
                Destroy(fan);
                _activeFanObstacles.RemoveAt(i);
            }
        }
    }

    /// <summary>玩家後方過遠的電擊球直接銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindPlayer"/>）。</summary>
    private void DestroyElectricOrbsBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activeElectricOrbs.Count - 1; i >= 0; i--)
        {
            GameObject orb = _activeElectricOrbs[i];
            if (orb == null)
            {
                _activeElectricOrbs.RemoveAt(i);
                continue;
            }

            if (threshold > orb.transform.position.z)
            {
                Destroy(orb);
                _activeElectricOrbs.RemoveAt(i);
            }
        }
    }

    /// <summary>玩家後方過遠的砲台直接銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindPlayer"/>）。</summary>
    private void DestroyTurretsBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activeTurrets.Count - 1; i >= 0; i--)
        {
            GameObject turret = _activeTurrets[i];
            if (turret == null)
            {
                _activeTurrets.RemoveAt(i);
                continue;
            }

            if (threshold > turret.transform.position.z)
            {
                Destroy(turret);
                _activeTurrets.RemoveAt(i);
            }
        }
    }

    /// <summary>玩家後方過遠的雷射柱直接銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindPlayer"/>）。</summary>
    private void DestroyLaserPillarsBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activeLaserPillars.Count - 1; i >= 0; i--)
        {
            GameObject pillar = _activeLaserPillars[i];
            if (pillar == null)
            {
                _activeLaserPillars.RemoveAt(i);
                continue;
            }

            if (threshold > pillar.transform.position.z)
            {
                Destroy(pillar);
                _activeLaserPillars.RemoveAt(i);
            }
        }
    }

    /// <summary>玩家後方過遠的氣流噴射口直接銷毀（邏輯同 <see cref="DestroyFanObstaclesBehindPlayer"/>）。</summary>
    private void DestroyAirVentsBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activeAirVents.Count - 1; i >= 0; i--)
        {
            GameObject vent = _activeAirVents[i];
            if (vent == null)
            {
                _activeAirVents.RemoveAt(i);
                continue;
            }

            if (threshold > vent.transform.position.z)
            {
                Destroy(vent);
                _activeAirVents.RemoveAt(i);
            }
        }
    }

    private GameObject RentPlatformFromPool()
    {
        if (platformPool.Count > 0)
        {
            GameObject go = platformPool.Pop();
            go.SetActive(true);
            return go;
        }

        GameObject created = Instantiate(platformPrefab, generatedRoot);
        return created;
    }

    /// <summary>回收到 Pool：重設位置／縮放／材質並關閉。</summary>
    private void ReturnPlatformToPool(GameObject go)
    {
        if (go == null)
            return;

        go.transform.SetParent(platformPoolRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        ApplyMaterial(go, MaterialType.Concrete);

        PlatformType pt = go.GetComponent<PlatformType>();
        if (pt != null)
            pt.SetMaterialType(MaterialType.Concrete);

        go.SetActive(false);
        platformPool.Push(go);
    }

    private void RecycleAllActivePlatforms()
    {
        for (int i = 0; i < _activePlatforms.Count; i++)
            ReturnPlatformToPool(_activePlatforms[i]);
        _activePlatforms.Clear();
        _activeLayout.Clear();
    }

    private void RecyclePlatformsBehindPlayer(float playerZ)
    {
        float threshold = playerZ - destroyBehindDistance;
        for (int i = _activePlatforms.Count - 1; i >= 0; i--)
        {
            if (threshold > _activeLayout[i].Position.z)
            {
                ReturnPlatformToPool(_activePlatforms[i]);
                _activePlatforms.RemoveAt(i);
                _activeLayout.RemoveAt(i);
            }
        }
    }

    private void DestroyEnergyPickupsUnderRoot()
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
    /// 建立單一區段的版面：第 0 段含起點，其餘段僅隨機平台；材質抽樣邏輯與舊版相同。
    /// </summary>
    private List<PlatformData> BuildChunkPlatformLayout(int chunkIndex)
    {
        float zMin = chunkIndex * chunkDepth;
        float zMax = (chunkIndex + 1) * chunkDepth;
        var combinedForOverlap = new List<PlatformData>(_activeLayout.Count + platformsPerChunk + 1 + _collapsibleOverlapEntries.Count);
        combinedForOverlap.AddRange(_activeLayout);
        combinedForOverlap.AddRange(_collapsibleOverlapEntries);

        var chunk = new List<PlatformData>(platformsPerChunk + 1);

        if (chunkIndex == 0)
        {
            PlatformData start = new PlatformData
            {
                Position = StartPos,
                Size = FixedStartSize,
                IsStart = true,
                MaterialType = MaterialType.Concrete
            };
            chunk.Add(start);
            combinedForOverlap.Add(start);
        }

        int targetRandom = platformsPerChunk;
        int safety = 0;
        while (chunk.Count < (chunkIndex == 0 ? 1 + targetRandom : targetRandom) && safety++ < 8000)
            TryAddRandomPlatformInChunk(chunk, combinedForOverlap, zMin, zMax);

        return chunk;
    }

    /// <summary>在區段 Z 範圍與以 X=0 為中心的寬度內嘗試加入一塊隨機平台。</summary>
    private bool TryAddRandomPlatformInChunk(
        List<PlatformData> chunk,
        List<PlatformData> overlapList,
        float zMin,
        float zMax)
    {
        for (int retry = 0; retry < 120; retry++)
        {
            Vector3 size = RandomSize();
            float halfW = chunkWidth * 0.5f;
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            Vector3 pos = new Vector3(
                RandomRange(-halfW + hx, halfW - hx),
                RandomRange(0f + hy, SpaceY - hy),
                RandomRange(zMin + hz, zMax - hz));

            PlatformData candidate = new PlatformData
            {
                Position = pos,
                Size = size,
                IsStart = false,
                MaterialType = PickRandomMaterialType()
            };

            if (OverlapsAny(candidate, overlapList))
                continue;

            chunk.Add(candidate);
            overlapList.Add(candidate);
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

    private float RandomRange(float min, float max)
    {
        return (float)(min + (_rng.NextDouble() * (max - min)));
    }

    /// <summary>
    /// 於第一區段對應的 X／Z 範圍內撒落能量方塊（Y 仍為 1～39），使用同一 <see cref="_rng"/>。
    /// </summary>
    private int SpawnEnergyPickupsInFirstChunk()
    {
        if (energyPickupPrefab == null)
            return 0;

        if (energyPickupCount <= 0)
            return 0;

        EnsureRoot();

        var spawnedCenters = new List<Vector3>(energyPickupCount);
        float halfW = chunkWidth * 0.5f;

        for (int i = 0; i < energyPickupCount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < EnergyPickupMaxAttemptsPerItem; attempt++)
            {
                float x = RandomRange(-halfW, halfW);
                float y = RandomRange(EnergyPickupYMin, EnergyPickupYMax);
                float z = RandomRange(0f, chunkDepth);
                Vector3 candidate = new Vector3(x, y, z);

                if (!IsValidEnergyPickupPosition(candidate, _activeLayout, spawnedCenters))
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

    private void NotifyCollectionManagerReset(int spawnedPickups)
    {
        if (collectionManager == null)
            collectionManager = UnityEngine.Object.FindFirstObjectByType<WardenCollectionManager>();
        if (collectionManager == null)
            return;
        collectionManager.ResetForNewRun(spawnedPickups);
        for (int i = 0; i < spawnedPickups; i++)
            collectionManager.RegisterPickup();
    }

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
