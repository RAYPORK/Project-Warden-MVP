using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 碎裂平台：鋼索勾住後倒數，變色警示並透過 <see cref="CurrentShakeIntensity"/> 驅動畫面震動；
/// 時間到後銷毀本物件。斷索或碎裂時會將震動強度歸零。
/// 可將 <see cref="OnGrappleAttached"/> 綁至 <c>WardenWinchSystem</c> 的 <c>onGrappleLaunched</c>，
/// <see cref="OnGrappleDetached"/> 綁至鋼索斷開事件（依專案實際事件名稱綁定）。
/// </summary>
public class CollapsiblePlatform : MonoBehaviour
{
    /// <summary>供攝影機腳本（如 <see cref="WardenController"/>）於 <c>LateUpdate</c> 讀取；由本元件在倒數中更新、斷索／碎裂時歸零。</summary>
    public static float CurrentShakeIntensity { get; private set; }

    [Header("倒數")]
    [Tooltip("鋼索勾住後幾秒碎裂")]
    [SerializeField]
    private float timeBeforeCollapse = 3f;

    [Header("畫面震動")]
    [Tooltip("倒數結束前震動強度由 0 線性漸增至本值；實際位移由攝影機腳本乘上 Time.deltaTime")]
    [SerializeField]
    private float cameraShakeIntensity = 0.1f;

    [Header("變色警示")]
    [Tooltip("開始警示顏色")]
    [SerializeField]
    private Color warningColorStart = Color.yellow;

    [Tooltip("即將碎裂顏色")]
    [SerializeField]
    private Color warningColorEnd = Color.red;

    private Vector3 _baseWorldPosition;
    private readonly List<RendererColorSnapshot> _snapshots = new List<RendererColorSnapshot>();
    private Coroutine _collapseRoutine;

    private struct RendererColorSnapshot
    {
        public Renderer Renderer;
        public bool IsSprite;
        public Color SpriteOriginal;
        public Color[] MaterialOriginals;
    }

    private void Awake()
    {
        _baseWorldPosition = transform.position;
        CacheRendererColors();
    }

    private void OnDestroy()
    {
        // 物件被場景卸載或碎裂銷毀時，避免殘留震動強度。
        CurrentShakeIntensity = 0f;
    }

    /// <summary>
    /// 於 Awake 記錄自身與所有子物件之 Renderer 原始顏色。
    /// Mesh／Skinned 等使用 <see cref="Renderer.materials"/> 取得實例材質，避免讀到共享材質導致無法正確還原。
    /// </summary>
    private void CacheRendererColors()
    {
        _snapshots.Clear();
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r is SpriteRenderer sr)
            {
                _snapshots.Add(new RendererColorSnapshot
                {
                    Renderer = r,
                    IsSprite = true,
                    SpriteOriginal = sr.color,
                    MaterialOriginals = null
                });
                continue;
            }

            // 使用 materials（實例）拷貝初始色，與 ApplyWarningColors 寫回同一套實例一致。
            var mats = r.materials;
            var cols = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                cols[i] = mats[i].color;

            _snapshots.Add(new RendererColorSnapshot
            {
                Renderer = r,
                IsSprite = false,
                SpriteOriginal = default,
                MaterialOriginals = cols
            });
        }
    }

    /// <summary>
    /// 鋼索勾住平台時呼叫（例如綁定 <c>onGrappleLaunched</c>）。
    /// 已開始倒數時再次呼叫不會重置計時。
    /// </summary>
    public void OnGrappleAttached()
    {
        if (_collapseRoutine != null)
            return;

        if (timeBeforeCollapse <= 0f)
        {
            CurrentShakeIntensity = 0f;
            StopAllCoroutinesBeforeDestroy();
            Destroy(gameObject);
            return;
        }

        _collapseRoutine = StartCoroutine(CollapseCountdownRoutine());
    }

    /// <summary>鋼索斷開時呼叫：停止倒數、還原外觀，並取消畫面震動貢獻。</summary>
    public void OnGrappleDetached()
    {
        if (_collapseRoutine != null)
        {
            StopCoroutine(_collapseRoutine);
            _collapseRoutine = null;
        }

        CurrentShakeIntensity = 0f;
        RestoreOriginalVisuals();
    }

    private IEnumerator CollapseCountdownRoutine()
    {
        float elapsed = 0f;

        while (elapsed < timeBeforeCollapse)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / timeBeforeCollapse);

            // 畫面震動：0 → Inspector 的 cameraShakeIntensity（例如 Prefab 設 2）隨 progress 線性增加；WardenController.LateUpdate 讀取 CurrentShakeIntensity。
            CurrentShakeIntensity = Mathf.Lerp(0f, cameraShakeIntensity, progress);

            ApplyWarningColors(progress);
            yield return null;
        }

        CurrentShakeIntensity = 0f;
        StopAllCoroutinesBeforeDestroy();
        Destroy(gameObject);
    }

    /// <summary>依倒數進度在原始色與警示色之間插值，並套用到所有已快取之 Renderer。</summary>
    private void ApplyWarningColors(float progress)
    {
        Color warningMix = Color.Lerp(warningColorStart, warningColorEnd, progress);

        foreach (var snap in _snapshots)
        {
            if (snap.Renderer == null)
                continue;

            if (snap.IsSprite && snap.Renderer is SpriteRenderer sr)
            {
                sr.color = Color.Lerp(snap.SpriteOriginal, warningMix, progress);
                continue;
            }

            if (snap.MaterialOriginals == null)
                continue;

            // 每次取 materials 為實例陣列，修改後回寫，確保顏色確實套用到 Renderer。
            var mats = snap.Renderer.materials;
            for (int i = 0; i < mats.Length && i < snap.MaterialOriginals.Length; i++)
                mats[i].color = Color.Lerp(snap.MaterialOriginals[i], warningMix, progress);

            snap.Renderer.materials = mats;
        }
    }

    /// <summary>還原 Awake 時記錄之世界座標與各 Renderer 原始顏色。</summary>
    private void RestoreOriginalVisuals()
    {
        transform.position = _baseWorldPosition;

        foreach (var snap in _snapshots)
        {
            if (snap.Renderer == null)
                continue;

            if (snap.IsSprite && snap.Renderer is SpriteRenderer sr)
            {
                sr.color = snap.SpriteOriginal;
                continue;
            }

            if (snap.MaterialOriginals == null)
                continue;

            var mats = snap.Renderer.materials;
            for (int i = 0; i < mats.Length && i < snap.MaterialOriginals.Length; i++)
                mats[i].color = snap.MaterialOriginals[i];

            snap.Renderer.materials = mats;
        }
    }

    private void StopAllCoroutinesBeforeDestroy()
    {
        CurrentShakeIntensity = 0f;
        StopAllCoroutines();
        _collapseRoutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        timeBeforeCollapse = Mathf.Max(0f, timeBeforeCollapse);
        cameraShakeIntensity = Mathf.Max(0f, cameraShakeIntensity);
    }
#endif
}
