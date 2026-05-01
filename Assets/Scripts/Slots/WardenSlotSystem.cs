using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 雙參數 int 事件（滾輪索引、符號 0–6），供 Inspector 綁定。
/// </summary>
[Serializable]
public class ReelIndexSymbolEvent : UnityEvent<int, int> { }

/// <summary>
/// 三個 int 事件（最終三滾輪符號），供 UI 或特效綁定。
/// </summary>
[Serializable]
public class ThreeIntsEvent : UnityEvent<int, int, int> { }

/// <summary>
/// 拉霸博弈：F 鍵觸發、消耗能量、三滾輪依序停止後判定結果並觸發對應 UnityEvent 與角色／捲揚數值。
/// </summary>
public class WardenSlotSystem : MonoBehaviour
{
    private const int SymbolCount = 7;
    private const int OverloadSymbolIndex = 6;

    [Header("能量")]
    [Tooltip("能量管理器（查詢／扣除）；亦可額外用事件處理回復／清空")]
    [SerializeField] private WardenEnergyManager energyManager;

    [Tooltip("每次拉霸消耗能量")]
    [SerializeField] private float spinEnergyCost = 20f;

    [Header("中獎：能量回復（UnityEvent）")]
    [Tooltip("兩個符號一致時觸發，參數為回復量（預設 30）")]
    [SerializeField] private UnityEvent<float> onWinEnergyRestore = new UnityEvent<float>();

    [SerializeField] private float winEnergyRestoreAmount = 30f;

    [Header("過載：能量與表現（UnityEvent）")]
    [Tooltip("7-7-7 時清空能量，可綁定 WardenEnergyManager.ClearEnergy")]
    [SerializeField] private UnityEvent onOverloadEnergyClear = new UnityEvent();

    [Tooltip("過載：Glitch 開始（通知 Post Processing 等）")]
    [SerializeField] private UnityEvent onOverloadGlitchStart = new UnityEvent();

    [Tooltip("過載：Glitch 結束（5 秒後自動觸發）")]
    [SerializeField] private UnityEvent onOverloadGlitchEnd = new UnityEvent();

    [Tooltip("過載：無敵開始（通知角色）")]
    [SerializeField] private UnityEvent onOverloadInvincibleStart = new UnityEvent();

    [Tooltip("過載：無敵結束（5 秒後自動觸發）")]
    [SerializeField] private UnityEvent onOverloadInvincibleEnd = new UnityEvent();

    [Header("結果事件（判定後）")]
    [SerializeField] private UnityEvent onJackpot = new UnityEvent();
    [SerializeField] private UnityEvent onWin = new UnityEvent();
    [SerializeField] private UnityEvent onOverload = new UnityEvent();
    [SerializeField] private UnityEvent onNormal = new UnityEvent();

    [Header("滾輪時序與動畫事件")]
    [Tooltip("第 1 個滾輪停止並鎖定符號的延遲（秒）")]
    [SerializeField] private float reel1StopDelay = 1f;

    [Tooltip("第 2 個滾輪停止並鎖定符號的延遲（秒）")]
    [SerializeField] private float reel2StopDelay = 1.5f;

    [Tooltip("第 3 個滾輪停止並鎖定符號的延遲（秒）")]
    [SerializeField] private float reel3StopDelay = 2f;

    [Tooltip("每個滾輪鎖定時：(滾輪索引 0–2, 符號 0–6)")]
    [SerializeField] private ReelIndexSymbolEvent onReelLocked = new ReelIndexSymbolEvent();

    [Tooltip("三個滾輪皆停止後，最終符號 (a,b,c)")]
    [SerializeField] private ThreeIntsEvent onAllReelsStopped = new ThreeIntsEvent();

    [Tooltip("拉霸成功扣能量後立即呼叫（可綁定 WardenReelDisplay.StartSpinning）")]
    [SerializeField] private UnityEvent onReelDisplaySpinStart = new UnityEvent();

    [Header("大獎：直接修改的元件")]
    [SerializeField] private WardenController playerController;

    [SerializeField] private WardenWinchSystem winchSystem;

    [Tooltip("大獎期間繩長上限（公尺）")]
    [SerializeField] private float jackpotMaxRopeLength = 40f;

    [Tooltip("大獎移動速度為基準的倍率")]
    [SerializeField] private float jackpotMoveSpeedMultiplier = 1.5f;

    [Tooltip("大獎增益持續時間（秒）")]
    [SerializeField] private float jackpotBuffDurationSeconds = 30f;

    [Header("過載持續時間")]
    [SerializeField] private float overloadEffectDurationSeconds = 5f;

    [Header("隨機")]
    [Tooltip("0 表示於 Start 用時間戳建立非確定性種子")]
    [SerializeField] private int randomSeed;

    [Header("保底")]
    [Tooltip("連續幾次「普通」後，下一次強制中獎（兩個符號相同）")]
    [SerializeField] private int pityThresholdBeforeForceWin = 8;

    [Header("音效")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("能量不足時播放")]
    [SerializeField] private AudioClip insufficientEnergyClip;

    private System.Random _rng;
    private bool _isSpinning;
    private int _pityLossStreak;
    private Coroutine _spinRoutine;
    private Coroutine _jackpotRestoreRoutine;

    /// <summary>大獎還原用：快取於 Start 時的移動速度與繩長上限。</summary>
    private float _baselineMoveSpeed;

    private float _baselineMaxRopeLength;
    private readonly int[] _finalSymbols = new int[3];

    /// <summary>最後一次完成拉霸的三個符號（0–6）。</summary>
    public int FinalSymbol0 => _finalSymbols[0];
    public int FinalSymbol1 => _finalSymbols[1];
    public int FinalSymbol2 => _finalSymbols[2];

    /// <summary>是否正在轉動（含等待判定）。</summary>
    public bool IsSpinning => _isSpinning;

    private void Start()
    {
        if (randomSeed != 0)
            _rng = new System.Random(randomSeed);
        else
            _rng = new System.Random(Environment.TickCount ^ (GetInstanceID() << 16));

        // 於 Start 快取，確保 WardenController／Winch 已完成 Awake 與 Inspector 數值載入。
        CacheBaselineStats();
    }

    private void OnEnable()
    {
        _pityLossStreak = 0;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
            TryStartSlotSpin();
    }

    /// <summary>場景中元件數值變更後若要重抓基準，可呼叫（通常不必）。</summary>
    public void CacheBaselineStats()
    {
        if (playerController != null)
            _baselineMoveSpeed = playerController.MoveSpeed;
        if (winchSystem != null)
            _baselineMaxRopeLength = winchSystem.MaxRopeLength;
    }

    private void TryStartSlotSpin()
    {
        if (_isSpinning)
            return;

        if (energyManager == null)
        {
            Debug.LogWarning("[WardenSlotSystem] 未指派 WardenEnergyManager。", this);
            return;
        }

        if (energyManager.CurrentEnergy < spinEnergyCost)
        {
            PlayFailSound();
            return;
        }

        if (!energyManager.TrySpend(spinEnergyCost))
        {
            PlayFailSound();
            return;
        }

        onReelDisplaySpinStart?.Invoke();

        if (_spinRoutine != null)
            StopCoroutine(_spinRoutine);
        _spinRoutine = StartCoroutine(SlotSpinSequence());
    }

    private void PlayFailSound()
    {
        if (audioSource != null && insufficientEnergyClip != null)
            audioSource.PlayOneShot(insufficientEnergyClip);
    }

    private IEnumerator SlotSpinSequence()
    {
        _isSpinning = true;

        RollFinalSymbols(_finalSymbols);

        yield return new WaitForSeconds(reel1StopDelay);
        onReelLocked?.Invoke(0, _finalSymbols[0]);

        yield return new WaitForSeconds(reel2StopDelay - reel1StopDelay);
        onReelLocked?.Invoke(1, _finalSymbols[1]);

        yield return new WaitForSeconds(reel3StopDelay - reel2StopDelay);
        onReelLocked?.Invoke(2, _finalSymbols[2]);

        onAllReelsStopped?.Invoke(_finalSymbols[0], _finalSymbols[1], _finalSymbols[2]);

        SlotOutcome outcome = ClassifyOutcome(_finalSymbols[0], _finalSymbols[1], _finalSymbols[2]);
        ApplyPityCounter(outcome);
        ApplyOutcome(outcome);

        _isSpinning = false;
        _spinRoutine = null;
    }

    private void RollFinalSymbols(int[] dst)
    {
        bool forceWin = _pityLossStreak >= pityThresholdBeforeForceWin;
        if (forceWin)
            RollForcedPairWin(dst);
        else
        {
            dst[0] = _rng.Next(0, SymbolCount);
            dst[1] = _rng.Next(0, SymbolCount);
            dst[2] = _rng.Next(0, SymbolCount);
        }
    }

    /// <summary>強制「兩個相同、一個不同」，避免形成三連大獎／過載。</summary>
    private void RollForcedPairWin(int[] dst)
    {
        int pairSymbol = _rng.Next(0, SymbolCount);
        int other;
        do
        {
            other = _rng.Next(0, SymbolCount);
        } while (other == pairSymbol);

        int loneReel = _rng.Next(0, 3);
        dst[0] = dst[1] = dst[2] = pairSymbol;
        dst[loneReel] = other;
    }

    private enum SlotOutcome
    {
        Normal,
        Win,
        Jackpot,
        Overload
    }

    private static SlotOutcome ClassifyOutcome(int a, int b, int c)
    {
        bool allMatch = a == b && b == c;
        if (allMatch)
        {
            if (a == OverloadSymbolIndex)
                return SlotOutcome.Overload;
            return SlotOutcome.Jackpot;
        }

        if (a == b || b == c || a == c)
            return SlotOutcome.Win;

        return SlotOutcome.Normal;
    }

    private void ApplyPityCounter(SlotOutcome outcome)
    {
        if (outcome == SlotOutcome.Normal)
            _pityLossStreak++;
        else
            _pityLossStreak = 0;
    }

    private void ApplyOutcome(SlotOutcome outcome)
    {
        Debug.Log($"[Slot] 結果：{outcome}，符號：{_finalSymbols[0]}-{_finalSymbols[1]}-{_finalSymbols[2]}");
        switch (outcome)
        {
            case SlotOutcome.Jackpot:
                onJackpot?.Invoke();
                ApplyJackpotBuff();
                break;
            case SlotOutcome.Win:
                onWin?.Invoke();
                onWinEnergyRestore?.Invoke(winEnergyRestoreAmount);
                break;
            case SlotOutcome.Overload:
                onOverload?.Invoke();
                onOverloadEnergyClear?.Invoke();
                StartCoroutine(OverloadTimedEvents());
                break;
            default:
                onNormal?.Invoke();
                break;
        }
    }

    private void ApplyJackpotBuff()
    {
        if (playerController != null)
        {
            if (_jackpotRestoreRoutine != null)
                StopCoroutine(_jackpotRestoreRoutine);
            playerController.MoveSpeed = _baselineMoveSpeed * jackpotMoveSpeedMultiplier;
        }

        if (winchSystem != null)
            winchSystem.MaxRopeLength = jackpotMaxRopeLength;

        if (playerController != null || winchSystem != null)
            _jackpotRestoreRoutine = StartCoroutine(RestoreJackpotAfterDelay());
    }

    private IEnumerator RestoreJackpotAfterDelay()
    {
        yield return new WaitForSeconds(jackpotBuffDurationSeconds);

        if (playerController != null)
            playerController.MoveSpeed = _baselineMoveSpeed;
        if (winchSystem != null)
            winchSystem.MaxRopeLength = _baselineMaxRopeLength;

        _jackpotRestoreRoutine = null;
    }

    private IEnumerator OverloadTimedEvents()
    {
        onOverloadGlitchStart?.Invoke();
        onOverloadInvincibleStart?.Invoke();

        yield return new WaitForSeconds(overloadEffectDurationSeconds);

        onOverloadGlitchEnd?.Invoke();
        onOverloadInvincibleEnd?.Invoke();
    }

    private void OnDestroy()
    {
        // 物件銷毀或換場景時還原大獎修改，避免殘留倍率／繩長。
        if (_jackpotRestoreRoutine != null)
            StopCoroutine(_jackpotRestoreRoutine);
        if (playerController != null)
            playerController.MoveSpeed = _baselineMoveSpeed;
        if (winchSystem != null)
            winchSystem.MaxRopeLength = _baselineMaxRopeLength;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spinEnergyCost = Mathf.Max(0f, spinEnergyCost);
        winEnergyRestoreAmount = Mathf.Max(0f, winEnergyRestoreAmount);
        reel1StopDelay = Mathf.Max(0f, reel1StopDelay);
        reel2StopDelay = Mathf.Max(reel1StopDelay, reel2StopDelay);
        reel3StopDelay = Mathf.Max(reel2StopDelay, reel3StopDelay);
        jackpotMaxRopeLength = Mathf.Max(0.01f, jackpotMaxRopeLength);
        jackpotMoveSpeedMultiplier = Mathf.Max(0.01f, jackpotMoveSpeedMultiplier);
        jackpotBuffDurationSeconds = Mathf.Max(0f, jackpotBuffDurationSeconds);
        overloadEffectDurationSeconds = Mathf.Max(0f, overloadEffectDurationSeconds);
        pityThresholdBeforeForceWin = Mathf.Max(1, pityThresholdBeforeForceWin);
    }
#endif
}
