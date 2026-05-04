using UnityEngine;

/// <summary>
/// 砲台：玩家進入偵測半徑時水平轉向玩家並依間隔發射飛彈（飛彈可追蹤玩家）；離開範圍則不發射並重置發射計時。
/// </summary>
[DisallowMultipleComponent]
public class Turret : MonoBehaviour
{
    private const float UnarmedNextFire = -1f;

    [Header("目標")]
    [Tooltip("玩家 Transform；未指派時會以 Tag「Player」自動尋找")]
    [SerializeField]
    private Transform playerTransform;

    [Header("偵測")]
    [Tooltip("與玩家距離小於等於此值時才轉向並允許發射（世界空間半徑）")]
    [Range(5f, 50f)]
    [SerializeField]
    private float detectionRadius = 20f;

    [Tooltip("紅色半透明偵測球；執行時自動建立")]
    [SerializeField]
    private GameObject detectionSphereVisual;

    [Header("發射")]
    [Tooltip("飛彈 Prefab（須掛 Missile 腳本）")]
    [SerializeField]
    private GameObject missilePrefab;

    [Tooltip("發射間隔（秒）")]
    [Range(1f, 10f)]
    [SerializeField]
    private float fireInterval = 3f;

    [Tooltip("每次進入偵測範圍後，第一發發射前的延遲（秒）")]
    [SerializeField]
    private float firstShotDelay = 1f;

    [Tooltip("發射點；未指派則使用本物件位置")]
    [SerializeField]
    private Transform firePoint;

    private Material _detectionSphereMaterial;

    /// <summary>
    /// 下一發允許發射的時間（<see cref="Time.time"/>）；為 <see cref="UnarmedNextFire"/> 表示未排程（玩家不在範圍內）。
    /// </summary>
    private float _nextFireTime = UnarmedNextFire;

    private void Awake()
    {
        BuildDetectionSphereVisual();
    }

    private void OnDestroy()
    {
        if (_detectionSphereMaterial != null)
            Destroy(_detectionSphereMaterial);
    }

    private void Start()
    {
        EnsurePlayerReference();
    }

    private void Update()
    {
        EnsurePlayerReference();
        RefreshDetectionSphereScale();

        if (playerTransform == null)
        {
            _nextFireTime = UnarmedNextFire;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= detectionRadius)
        {
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (_nextFireTime < 0f)
                _nextFireTime = Time.time + Mathf.Max(0f, firstShotDelay);

            if (Time.time >= _nextFireTime)
            {
                TryFire();
                _nextFireTime = Time.time + Mathf.Max(0.01f, fireInterval);
            }
        }
        else
        {
            // 玩家不在範圍內：不發射，發射計時重置（再次進入需重新經過 firstShotDelay）
            _nextFireTime = UnarmedNextFire;
        }
    }

    /// <summary>若未指派玩家，以 Tag「Player」尋找場景中的玩家根物件。</summary>
    private void EnsurePlayerReference()
    {
        if (playerTransform != null)
            return;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            playerTransform = go.transform;
    }

    private void TryFire()
    {
        if (missilePrefab == null)
            return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Quaternion spawnRot = transform.rotation;

        GameObject instance = Instantiate(missilePrefab, spawnPos, spawnRot);
        Missile missile = instance.GetComponent<Missile>();
        if (missile == null)
        {
            Destroy(instance);
            return;
        }

        missile.Launch(transform.forward, playerTransform);
    }

    /// <summary>建立紅色半透明偵測球（無 Collider），邏輯與舊版 Missile 偵測視覺相同。</summary>
    private void BuildDetectionSphereVisual()
    {
        if (detectionSphereVisual != null)
            return;

        detectionSphereVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        detectionSphereVisual.name = "DetectionSphereVisual";
        Destroy(detectionSphereVisual.GetComponent<Collider>());
        detectionSphereVisual.transform.SetParent(transform, false);
        detectionSphereVisual.transform.localPosition = Vector3.zero;

        Renderer renderer = detectionSphereVisual.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default")
                        ?? Shader.Find("Unlit/Color");

        _detectionSphereMaterial = new Material(shader);
        Color c = new Color(1f, 0f, 0f, 0.25f);
        if (_detectionSphereMaterial.HasProperty("_BaseColor"))
            _detectionSphereMaterial.SetColor("_BaseColor", c);
        if (_detectionSphereMaterial.HasProperty("_Color"))
            _detectionSphereMaterial.color = c;

        if (_detectionSphereMaterial.HasProperty("_Surface"))
        {
            _detectionSphereMaterial.SetFloat("_Surface", 1f);
            _detectionSphereMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _detectionSphereMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _detectionSphereMaterial.SetInt("_ZWrite", 0);
            _detectionSphereMaterial.renderQueue = 3000;
            _detectionSphereMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _detectionSphereMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        }

        renderer.sharedMaterial = _detectionSphereMaterial;
        RefreshDetectionSphereScale();
    }

    private void RefreshDetectionSphereScale()
    {
        if (detectionSphereVisual == null)
            return;

        float r = Mathf.Max(0.01f, detectionRadius);
        float uniformScale = r / 0.5f;
        detectionSphereVisual.transform.localScale = Vector3.one * uniformScale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fireInterval = Mathf.Clamp(fireInterval, 1f, 10f);
        firstShotDelay = Mathf.Max(0f, firstShotDelay);
        detectionRadius = Mathf.Clamp(detectionRadius, 5f, 50f);
        RefreshDetectionSphereScale();
    }
#endif
}
