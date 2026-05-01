using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 拉霸 HUD：預設靜止；<see cref="StartSpinning"/> 後三滾輪向上循環滾動（21 格三聯無縫），
/// 依 <see cref="OnReelLocked"/> 停止並對齊符號；全停後等待再回靜止，等待下次 <see cref="StartSpinning"/>。
/// </summary>
public class WardenReelDisplay : MonoBehaviour
{
    private const int ReelCount = 3;
    /// <summary>邏輯符號種類數（0～6）。</summary>
    private const int LogicalSymbolCount = 7;

    /// <summary>同一條帶重複份數（7 → 21 格）。</summary>
    private const int StripRepeatCount = 3;

    private const int PhysicalSymbolCount = LogicalSymbolCount * StripRepeatCount;

    [Header("滾輪 ScrollRect（Reel_01～03）")]
    [SerializeField] private ScrollRect reel01;
    [SerializeField] private ScrollRect reel02;
    [SerializeField] private ScrollRect reel03;

    [Header("滾動規格")]
    [Tooltip("Content 向上移動速度（anchoredPosition.y 每秒變化量，單位 px）")]
    [SerializeField] private float scrollPixelsPerSecond = 300f;

    [Tooltip("單一符號佔位高度（圖示 + 間距，單位 px）；亦用於循環週期與 Content 總高計算")]
    [SerializeField] private float symbolHeightPixels = 70f;

    [Tooltip("停止時對齊到目標位置的 Lerp 時間（秒）")]
    [SerializeField] private float alignDurationSeconds = 0.2f;

    [Tooltip("三滾輪皆完成對齊後，延遲多久回到靜止（秒）")]
    [SerializeField] private float resumeScrollDelaySeconds = 1f;

    [Header("事件")]
    [Tooltip("三個滾輪都完成停止對齊後觸發（無參數）")]
    [SerializeField] private UnityEvent onAllReelsStopped = new UnityEvent();

    private ScrollRect[] _reels;
    private readonly bool[] _scrolling = new bool[ReelCount];
    private readonly bool[] _aligning = new bool[ReelCount];

    /// <summary>content.y = 0 時，「中間那一排」各邏輯符號中心在 Viewport 本地的 Y。</summary>
    private readonly float[] _symbolVpIntercept = new float[ReelCount * LogicalSymbolCount];

    /// <summary>d(符號中心 viewport Y) / d(content.y)，每邏輯符號一筆（取中間排取樣）。</summary>
    private readonly float[] _symbolVpSlope = new float[ReelCount * LogicalSymbolCount];

    private int _reelsFullyStoppedThisRound;
    private Coroutine _resumeRoutine;
    private Canvas _canvas;

    /// <summary>無縫循環週期：一段 7 格的高度；位移超過此值時扣回等長以消除跳躍。</summary>
    private float CycleStripHeight => symbolHeightPixels * LogicalSymbolCount;

    /// <summary>三聯後 Content 總高度（21 格）。</summary>
    private float FullStripHeight => symbolHeightPixels * PhysicalSymbolCount;

    private void Awake()
    {
        _reels = new[] { reel01, reel02, reel03 };
        for (int i = 0; i < ReelCount; i++)
        {
            if (_reels[i] != null)
            {
                _reels[i].inertia = false;
                _reels[i].movementType = ScrollRect.MovementType.Unrestricted;
                _canvas ??= _reels[i].GetComponentInParent<Canvas>();
            }
        }

        // 將各滾輪 Content 下 7 個符號複製成 21 個，供循環時視覺連續。
        for (int i = 0; i < ReelCount; i++)
        {
            if (_reels[i] != null && _reels[i].content != null)
                TripleReelContent(_reels[i].content as RectTransform);
        }
    }

    private void Start()
    {
        // 遊戲開始：不啟動滾動，維持 Content 初始 anchoredPosition。
        for (int i = 0; i < ReelCount; i++)
        {
            if (_reels[i] == null || _reels[i].content == null)
                continue;
            CalibrateReelViewportMapping(i);
            _scrolling[i] = false;
        }
    }

    /// <summary>
    /// 拉霸成功開始轉動時由 <see cref="WardenSlotSystem"/> 的 UnityEvent 綁定；三滾輪開始向上循環滾動。
    /// </summary>
    public void StartSpinning()
    {
        for (int i = 0; i < ReelCount; i++)
        {
            if (_reels[i] == null || _reels[i].content == null)
                continue;
            if (_aligning[i])
                continue;
            _scrolling[i] = true;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < ReelCount; i++)
        {
            if (_reels[i] == null || _reels[i].content == null)
                continue;
            if (!_scrolling[i] || _aligning[i])
                continue;

            RectTransform content = _reels[i].content;
            Vector2 ap = content.anchoredPosition;
            ap.y += scrollPixelsPerSecond * dt;
            while (ap.y >= CycleStripHeight)
                ap.y -= CycleStripHeight;
            content.anchoredPosition = ap;
            _reels[i].velocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 由 <see cref="WardenSlotSystem"/> 的 UnityEvent（int, int）綁定：停止指定滾輪並對齊符號到視窗中央。
    /// </summary>
    public void OnReelLocked(int reelIndex, int symbolIndex)
    {
        reelIndex = Mathf.Clamp(reelIndex, 0, ReelCount - 1);
        symbolIndex = Mathf.Clamp(symbolIndex, 0, LogicalSymbolCount - 1);

        if (_reels == null || reelIndex >= _reels.Length || _reels[reelIndex] == null)
            return;
        if (_aligning[reelIndex])
            return;

        // 若仍在「全停後延遲」Coroutine 中，新一輪鎖定先到：中斷等待並恢復可滾，避免計數錯亂。
        if (_resumeRoutine != null)
        {
            StopCoroutine(_resumeRoutine);
            _resumeRoutine = null;
            _reelsFullyStoppedThisRound = 0;
            for (int i = 0; i < ReelCount; i++)
            {
                if (_reels[i] != null)
                    _scrolling[i] = true;
            }
        }

        StartCoroutine(AlignReelRoutine(reelIndex, symbolIndex));
    }

    /// <summary>
    /// 將 Content 下恰好 7 個子物件複製成三排共 21 個，並擴大 Content 高度。
    /// </summary>
    private void TripleReelContent(RectTransform content)
    {
        if (content == null)
            return;
        if (content.childCount >= PhysicalSymbolCount)
            return;
        if (content.childCount != LogicalSymbolCount)
        {
            Debug.LogWarning(
                $"[WardenReelDisplay] Content 需恰好 {LogicalSymbolCount} 個子物件才會自動三聯化，目前為 {content.childCount}。",
                this);
            return;
        }

        var originals = new RectTransform[LogicalSymbolCount];
        for (int s = 0; s < LogicalSymbolCount; s++)
            originals[s] = content.GetChild(s) as RectTransform;

        float stepY = -Mathf.Abs(symbolHeightPixels);
        if (originals[0] != null && originals[1] != null)
        {
            float d = originals[1].anchoredPosition.y - originals[0].anchoredPosition.y;
            if (Mathf.Abs(d) > 0.01f)
                stepY = d;
        }

        for (int copy = 1; copy < StripRepeatCount; copy++)
        {
            float blockOffset = copy * LogicalSymbolCount * stepY;
            for (int s = 0; s < LogicalSymbolCount; s++)
            {
                RectTransform src = originals[s];
                if (src == null)
                    continue;
                GameObject clone = Instantiate(src.gameObject, content);
                RectTransform dst = clone.transform as RectTransform;
                if (dst != null)
                    dst.anchoredPosition = src.anchoredPosition + new Vector2(0f, blockOffset);
            }
        }

        Vector2 sizeDelta = content.sizeDelta;
        sizeDelta.y = Mathf.Max(sizeDelta.y, FullStripHeight);
        content.sizeDelta = sizeDelta;
    }

    /// <summary>
    /// 以兩個 Content.y 取樣，建立「中間排（索引 7～13）符號中心」在 Viewport 本地 Y 對 content.y 的線性關係。
    /// </summary>
    private void CalibrateReelViewportMapping(int reelIndex)
    {
        ScrollRect sr = _reels[reelIndex];
        RectTransform viewport = sr.viewport != null ? sr.viewport : sr.transform as RectTransform;
        RectTransform content = sr.content;
        if (viewport == null || content == null)
            return;

        Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        float ySaved = content.anchoredPosition.y;
        float xSaved = content.anchoredPosition.x;

        const float deltaY = 100f;
        float[] sample0 = new float[LogicalSymbolCount];
        float[] sample1 = new float[LogicalSymbolCount];

        // 有 21 格時取「中間排」符號取樣；否則退回第一排（未成功三聯化時）。
        int midRowOffset = content.childCount >= PhysicalSymbolCount ? LogicalSymbolCount : 0;

        content.anchoredPosition = new Vector2(xSaved, 0f);
        Canvas.ForceUpdateCanvases();
        for (int s = 0; s < LogicalSymbolCount; s++)
            sample0[s] = GetSymbolCenterViewportLocalY(viewport, content, s + midRowOffset, cam);

        content.anchoredPosition = new Vector2(xSaved, deltaY);
        Canvas.ForceUpdateCanvases();
        for (int s = 0; s < LogicalSymbolCount; s++)
            sample1[s] = GetSymbolCenterViewportLocalY(viewport, content, s + midRowOffset, cam);

        content.anchoredPosition = new Vector2(xSaved, ySaved);
        Canvas.ForceUpdateCanvases();

        int baseIdx = reelIndex * LogicalSymbolCount;
        for (int s = 0; s < LogicalSymbolCount; s++)
        {
            float b = (sample1[s] - sample0[s]) / deltaY;
            if (Mathf.Abs(b) < 1e-4f)
                b = 1f;
            _symbolVpSlope[baseIdx + s] = b;
            _symbolVpIntercept[baseIdx + s] = sample0[s];
        }
    }

    private static float GetSymbolCenterViewportLocalY(
        RectTransform viewport,
        RectTransform content,
        int childIndex,
        Camera cam)
    {
        if (childIndex < 0 || childIndex >= content.childCount)
            return 0f;
        var child = content.GetChild(childIndex) as RectTransform;
        if (child == null)
            return 0f;

        Vector3 world = child.TransformPoint(child.rect.center);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                RectTransformUtility.WorldToScreenPoint(cam, world),
                cam,
                out Vector2 local))
            return 0f;
        return local.y;
    }

    private IEnumerator AlignReelRoutine(int reelIndex, int symbolIndex)
    {
        ScrollRect sr = _reels[reelIndex];
        RectTransform viewport = sr.viewport != null ? sr.viewport : sr.transform as RectTransform;
        RectTransform content = sr.content;

        _aligning[reelIndex] = true;
        _scrolling[reelIndex] = false;
        sr.velocity = Vector2.zero;

        float vpCenterY = viewport.rect.center.y;
        int idx = reelIndex * LogicalSymbolCount + symbolIndex;
        float intercept = _symbolVpIntercept[idx];
        float slope = _symbolVpSlope[idx];
        float baseTarget = (vpCenterY - intercept) / slope;

        float startY = content.anchoredPosition.y;
        float endY = PickClosestWrappedY(startY, baseTarget, CycleStripHeight);

        float elapsed = 0f;
        while (elapsed < alignDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / alignDurationSeconds);
            float y = Mathf.Lerp(startY, endY, t);
            Vector2 ap = content.anchoredPosition;
            ap.y = y;
            content.anchoredPosition = ap;
            yield return null;
        }

        Vector2 finalAp = content.anchoredPosition;
        finalAp.y = endY;
        while (finalAp.y >= CycleStripHeight)
            finalAp.y -= CycleStripHeight;
        while (finalAp.y < 0f)
            finalAp.y += CycleStripHeight;
        content.anchoredPosition = finalAp;
        sr.velocity = Vector2.zero;

        _aligning[reelIndex] = false;

        _reelsFullyStoppedThisRound++;
        if (_reelsFullyStoppedThisRound >= ReelCount)
        {
            onAllReelsStopped?.Invoke();
            if (_resumeRoutine != null)
                StopCoroutine(_resumeRoutine);
            _resumeRoutine = StartCoroutine(ResumeStaticAfterDelay());
        }
    }

    /// <summary>在週期 period（7 格高）下，選與 start 最接近的等價目標。</summary>
    private static float PickClosestWrappedY(float startY, float baseTarget, float period)
    {
        float best = baseTarget;
        float bestDist = float.MaxValue;
        for (int k = -6; k <= 6; k++)
        {
            float cand = baseTarget + k * period;
            float d = Mathf.Abs(cand - startY);
            if (d < bestDist)
            {
                bestDist = d;
                best = cand;
            }
        }

        return best;
    }

    /// <summary>全滾輪對齊完成後等待，再全部回到靜止（不自動滾動）。</summary>
    private IEnumerator ResumeStaticAfterDelay()
    {
        yield return new WaitForSeconds(resumeScrollDelaySeconds);
        _reelsFullyStoppedThisRound = 0;
        for (int i = 0; i < ReelCount; i++)
        {
            if (_reels[i] != null)
                _scrolling[i] = false;
        }

        _resumeRoutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        scrollPixelsPerSecond = Mathf.Max(0f, scrollPixelsPerSecond);
        symbolHeightPixels = Mathf.Max(1f, symbolHeightPixels);
        alignDurationSeconds = Mathf.Max(0.01f, alignDurationSeconds);
        resumeScrollDelaySeconds = Mathf.Max(0f, resumeScrollDelaySeconds);
    }
#endif
}
