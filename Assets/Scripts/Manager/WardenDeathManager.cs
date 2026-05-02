using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 死亡與重開：玩家 Y 低於門檻（掉落虛空）時凍結輸入、淡入結算面板；
/// 再試一次時重產地圖（新種子）、補滿能量並重置統計與拉霸狀態。
/// 統計建議綁定：<see cref="WardenSlotSystem"/> 的 onReelDisplaySpinStart → <see cref="RegisterSlotSpin"/>；
/// 各 <see cref="WardenEnergyPickup"/> 的 onCollected → <see cref="RegisterEnergyCollected"/>。
/// </summary>
public class WardenDeathManager : MonoBehaviour
{
    [Header("核心參照")]
    [SerializeField] private WardenEnergyManager energyManager;
    [SerializeField] private WardenController playerController;
    [SerializeField] private WardenWinchSystem winchSystem;
    [SerializeField] private WardenRoomGenerator roomGenerator;
    [SerializeField] private WardenSlotSystem slotSystem;

    [Tooltip("大獎／過載倒數 HUD；死亡時停止倒數，重開時清空顯示")]
    [SerializeField]
    private WardenBuffTimerHUD buffTimerHUD;

    [Header("掉落虛空")]
    [Tooltip("玩家此 Transform 的世界 Y 低於此值時判定死亡（例如 -20）")]
    [SerializeField] private float fallDeathY = -20f;

    [Header("結算 UI（Canvas Group，勿用 SetActive）")]
    [SerializeField] private CanvasGroup gameOverPanel;

    [SerializeField] private TMP_Text survivalTimeText;
    [SerializeField] private TMP_Text slotSpinCountText;
    [SerializeField] private TMP_Text energyCollectedText;
    [SerializeField] private UnityEngine.UI.Button tryAgainButton;

    [Header("結算 TMP 字型（選填）")]
    [Tooltip("LiberationSans SDF 不含中文。若場景中這三個 TMP 曾填中文預設字，請改為純數字顯示或在此指派含 CJK 的 Font Asset（例如 Noto／思源黑體 SDF）。")]
    [SerializeField] private TMP_FontAsset statsFontWithCjkSupport;

    [Header("重開：玩家位置")]
    [Tooltip("用於掉落判定與再試一次時重置位置的 Transform（例如 PlayerRig）")]
    [SerializeField] private Transform playerRespawnRoot;

    [SerializeField] private Vector3 respawnWorldPosition = new Vector3(0f, 2f, 0f);

    [Header("淡入")]
    [SerializeField] private float gameOverFadeInSeconds = 0.5f;

    private bool _isDead;
    private float _sessionStartTime;
    private int _slotSpinCount;
    private float _energyCollectedTotal;
    private Coroutine _fadeRoutine;

    private void Start()
    {
        _sessionStartTime = Time.time;

        ApplyStatsFontAndAsciiPlaceholders();

        if (gameOverPanel != null)
        {
            gameOverPanel.alpha = 0f;
            gameOverPanel.interactable = false;
            gameOverPanel.blocksRaycasts = false;
        }

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(OnDeathTryAgainClicked);
    }

    /// <summary>
    /// 避免場景預設中文與 LiberationSans 衝突：可選套用 CJK 字型，並將三個數值 TMP 改為純 ASCII 佔位（僅數字與冒號）。
    /// </summary>
    private void ApplyStatsFontAndAsciiPlaceholders()
    {
        if (statsFontWithCjkSupport != null)
        {
            if (survivalTimeText != null)
                survivalTimeText.font = statsFontWithCjkSupport;
            if (slotSpinCountText != null)
                slotSpinCountText.font = statsFontWithCjkSupport;
            if (energyCollectedText != null)
                energyCollectedText.font = statsFontWithCjkSupport;
        }

        ResetHudStatTextsToAsciiPlaceholders();
    }

    private void ResetHudStatTextsToAsciiPlaceholders()
    {
        if (survivalTimeText != null)
            survivalTimeText.text = "TIME: 00:00";
        if (slotSpinCountText != null)
            slotSpinCountText.text = "SLOTS: 0";
        if (energyCollectedText != null)
            energyCollectedText.text = "ENERGY: 0";
    }

    private void Update()
    {
        if (_isDead || playerRespawnRoot == null)
            return;

        if (playerRespawnRoot.position.y < fallDeathY)
            BeginDeathSequence();
    }

    private void OnDestroy()
    {
        if (tryAgainButton != null)
            tryAgainButton.onClick.RemoveListener(OnDeathTryAgainClicked);
    }

    /// <summary>綁定 <see cref="WardenSlotSystem.onReelDisplaySpinStart"/>（無參數 UnityEvent）。</summary>
    public void RegisterSlotSpin()
    {
        if (_isDead)
            return;
        _slotSpinCount++;
    }

    /// <summary>綁定 <see cref="WardenEnergyPickup.onCollected"/>（float 能量量）。</summary>
    public void RegisterEnergyCollected(float amount)
    {
        if (_isDead)
            return;
        if (amount <= 0f)
            return;
        _energyCollectedTotal += amount;
    }

    public void BeginDeathSequence()
    {
        _isDead = true;

        // 停止大獎／過載共用倒數協程並清空文字，避免結算期間仍閃爍或疊字。
        if (buffTimerHUD != null)
            buffTimerHUD.StopAllTimers();

        // 死亡時解鎖游標讓玩家能點擊 UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float survivedSeconds = Mathf.Max(0f, Time.time - _sessionStartTime);
        PopulateResultTexts(survivedSeconds);

        if (slotSystem != null)
            slotSystem.HaltActiveSlotCoroutines();

        if (playerController != null)
            playerController.enabled = false;
        if (winchSystem != null)
        {
            winchSystem.ForceDisconnectIfConnected();
            winchSystem.enabled = false;
        }

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeInGameOverPanel());
    }

    private void PopulateResultTexts(float survivedSeconds)
    {
        int totalSec = Mathf.FloorToInt(survivedSeconds);
        int mm = totalSec / 60;
        int ss = totalSec % 60;

        if (survivalTimeText != null)
            survivalTimeText.text = $"TIME: {mm:00}:{ss:00}";
        if (slotSpinCountText != null)
            slotSpinCountText.text = $"SLOTS: {_slotSpinCount}";
        if (energyCollectedText != null)
            energyCollectedText.text = $"ENERGY: {Mathf.FloorToInt(_energyCollectedTotal)}";
    }

    private IEnumerator FadeInGameOverPanel()
    {
        if (gameOverPanel == null)
            yield break;

        float duration = Mathf.Max(0.01f, gameOverFadeInSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            gameOverPanel.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }

        gameOverPanel.alpha = 1f;
        gameOverPanel.interactable = true;
        gameOverPanel.blocksRaycasts = true;
        _fadeRoutine = null;
    }

    private void OnDeathTryAgainClicked()
    {
        if (!_isDead)
            return;
        PerformRunRestart(hideDeathPanel: true);
    }

    /// <summary>
    /// 任務完成面板「再試一次」等外部呼叫：重產地圖、補滿能量、重置玩家與統計（不要求處於死亡狀態）。
    /// </summary>
    public void RestartRunAfterMissionComplete()
    {
        PerformRunRestart(hideDeathPanel: false);
    }

    private void PerformRunRestart(bool hideDeathPanel)
    {
        if (hideDeathPanel)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.alpha = 0f;
                gameOverPanel.interactable = false;
                gameOverPanel.blocksRaycasts = false;
            }
        }

        if (roomGenerator != null)
            roomGenerator.GenerateMap();

        if (energyManager != null)
            energyManager.RestoreFullEnergy();

        // 重開時補滿血量（與能量補滿同一步驟；Unity 6 使用 FindFirstObjectByType）
        WardenHealthManager healthManager = Object.FindFirstObjectByType<WardenHealthManager>();
        if (healthManager != null)
            healthManager.RestoreFullHealth();

        if (slotSystem != null)
            slotSystem.ResetForNewRun();

        // 與 StopAllTimers 等價：確保 buff 倒數 TMP 清空、協程已停（新一局乾淨狀態）。
        if (buffTimerHUD != null)
            buffTimerHUD.ResetHUD();

        if (winchSystem != null)
            winchSystem.ForceDisconnectIfConnected();

        if (playerRespawnRoot != null)
        {
            playerRespawnRoot.position = respawnWorldPosition;
            if (playerRespawnRoot.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (playerController != null)
            playerController.enabled = true;
        if (winchSystem != null)
            winchSystem.enabled = true;

        _slotSpinCount = 0;
        _energyCollectedTotal = 0f;
        _sessionStartTime = Time.time;
        _isDead = false;

        ResetHudStatTextsToAsciiPlaceholders();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gameOverFadeInSeconds = Mathf.Max(0.01f, gameOverFadeInSeconds);
    }
#endif
}
