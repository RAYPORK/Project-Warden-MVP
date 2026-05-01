using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 在固定空間內以自由座標生成隨機大小平台，並保證起點到終點可達。
/// </summary>
[DisallowMultipleComponent]
public class WardenRoomGenerator : MonoBehaviour
{
    [Header("隨機種子")]
    [SerializeField] private int seed = 42001;

    [Header("Prefab")]
    [SerializeField] private GameObject platformPrefab;

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

    [Header("事件")]
    [SerializeField] private UnityEvent onMapGenerated;

    private const float SpaceX = 80f;
    private const float SpaceY = 40f;
    private const float SpaceZ = 50f;
    private const float MinSize = 1f;
    private const float MaxSize = 3f;
    private const float PlatformThickness = 0.5f;
    private const float MaxHorizontalGap = 10f;
    private const float MaxVerticalGap = 6f;
    private const int MinPlatforms = 80;
    private const int MaxPlatforms = 120;

    private static readonly Vector3 StartPos = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 EndPos = new Vector3(75f, 35f, 45f);
    private static readonly Vector3 FixedStartEndSize = new Vector3(3f, PlatformThickness, 3f);

    private System.Random _rng;

    private struct PlatformData
    {
        public Vector3 Position;
        public Vector3 Size;
        public bool IsStart;
        public bool IsEnd;
        public MaterialType MaterialType;
    }

    private void Start()
    {
        GenerateMap();
    }

    private void Update()
    {
        if (IsRegeneratePressed())
        {
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            GenerateMap();
        }
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        if (platformPrefab == null)
        {
            Debug.LogError("WardenRoomGenerator: 請先指定 platformPrefab。");
            return;
        }

        _rng = new System.Random(seed);
        EnsureRoot();
        ClearGenerated();

        List<PlatformData> platforms = BuildPlatformLayout();
        SpawnPlatforms(platforms);
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

    private List<PlatformData> BuildPlatformLayout()
    {
        int totalCount = _rng.Next(MinPlatforms, MaxPlatforms + 1);
        List<PlatformData> platforms = new List<PlatformData>(totalCount);

        PlatformData start = new PlatformData
        {
            Position = StartPos,
            Size = FixedStartEndSize,
            IsStart = true,
            MaterialType = MaterialType.Concrete
        };
        PlatformData end = new PlatformData
        {
            Position = EndPos,
            Size = FixedStartEndSize,
            IsEnd = true,
            MaterialType = MaterialType.Concrete
        };
        platforms.Add(start);

        platforms.Add(end);

        int safety = 0;
        while (platforms.Count < totalCount && safety++ < 4000)
            TryAddRandomPlatform(platforms);

        if (!HasPathStartToEnd(platforms))
        {
            // 若填充後意外破壞可達性，重新補主路徑。
            List<PlatformData> repairPath = BuildMainPath(start, end, platforms);
            platforms.AddRange(repairPath);
        }

        return platforms;
    }

    /// <summary>建立由起點到終點的骨幹路徑，確保每步都符合可達距離。</summary>
    private List<PlatformData> BuildMainPath(PlatformData start, PlatformData end, List<PlatformData> existing)
    {
        List<PlatformData> path = new List<PlatformData>();
        PlatformData current = start;

        for (int i = 0; i < 128; i++)
        {
            if (IsReachable(current.Position, end.Position))
                break;

            if (!TryCreateTowardTarget(current.Position, end.Position, existing, path, out PlatformData next))
                continue;

            path.Add(next);
            current = next;
        }

        return path;
    }

    private bool TryCreateTowardTarget(
        Vector3 from,
        Vector3 target,
        List<PlatformData> existing,
        List<PlatformData> staged,
        out PlatformData result)
    {
        for (int retry = 0; retry < 80; retry++)
        {
            Vector2 fromXZ = new Vector2(from.x, from.z);
            Vector2 targetXZ = new Vector2(target.x, target.z);
            Vector2 dir = (targetXZ - fromXZ).normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                float a = RandomRange(0f, Mathf.PI * 2f);
                dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }

            float stepH = Mathf.Lerp(4f, MaxHorizontalGap, (float)_rng.NextDouble());
            float nx = from.x + dir.x * stepH + RandomRange(-1.2f, 1.2f);
            float nz = from.z + dir.y * stepH + RandomRange(-1.2f, 1.2f);
            float ny = Mathf.MoveTowards(from.y, target.y, MaxVerticalGap) + RandomRange(-1f, 1f);

            Vector3 size = RandomSize();
            Vector3 pos = ClampCenterInsideBounds(new Vector3(nx, ny, nz), size);
            PlatformData candidate = new PlatformData
            {
                Position = pos,
                Size = size,
                MaterialType = PickRandomMaterialType()
            };

            if (!IsReachable(from, pos)) continue;
            if (OverlapsAny(candidate, existing) || OverlapsAny(candidate, staged)) continue;

            result = candidate;
            return true;
        }

        result = default;
        return false;
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

    private bool HasPathStartToEnd(List<PlatformData> platforms)
    {
        int startIndex = -1;
        int endIndex = -1;
        for (int i = 0; i < platforms.Count; i++)
        {
            if (platforms[i].IsStart) startIndex = i;
            if (platforms[i].IsEnd) endIndex = i;
        }
        if (startIndex < 0 || endIndex < 0) return false;

        Queue<int> q = new Queue<int>();
        bool[] visited = new bool[platforms.Count];
        q.Enqueue(startIndex);
        visited[startIndex] = true;

        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            if (cur == endIndex) return true;

            for (int i = 0; i < platforms.Count; i++)
            {
                if (visited[i] || i == cur) continue;
                if (!IsReachable(platforms[cur].Position, platforms[i].Position)) continue;
                visited[i] = true;
                q.Enqueue(i);
            }
        }

        return false;
    }

    private static bool IsReachable(Vector3 a, Vector3 b)
    {
        Vector2 aXZ = new Vector2(a.x, a.z);
        Vector2 bXZ = new Vector2(b.x, b.z);
        float h = Vector2.Distance(aXZ, bXZ);
        float v = Mathf.Abs(a.y - b.y);
        return h <= MaxHorizontalGap + 0.001f && v <= MaxVerticalGap + 0.001f;
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
            else if (p.IsEnd) go.name = "Platform_End";
            else go.name = $"Platform_{i:000}";

            // 起點與終點固定為 Concrete，其他平台使用抽樣材質類型。
            MaterialType type = (p.IsStart || p.IsEnd) ? MaterialType.Concrete : p.MaterialType;
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
