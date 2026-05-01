using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 任務完成面板：將 <see cref="WardenCollectionManager.onAllCollected"/> 綁定至 <see cref="ShowMissionComplete"/>，
/// 顯示時間、本地排行榜與個人最佳；再試一次可綁定 <see cref="WardenDeathManager.RestartRunAfterMissionComplete"/>。
/// </summary>
public class WardenMissionCompleteUI : MonoBehaviour
{
    [Header("資料來源")]
    [SerializeField] private WardenLeaderboard leaderboard;

    [Header("UI")]
    [SerializeField] private CanvasGroup missionCompletePanel;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text leaderboardText;
    [SerializeField] private TMP_Text personalBestText;

    [SerializeField] private Button tryAgainButton;

    [Header("行為")]
    [Tooltip("完成時暫停遊戲時間（方便點 UI）")]
    [SerializeField] private bool pauseTimeOnComplete = true;

    [Tooltip("再試一次：建議綁定 WardenDeathManager.RestartRunAfterMissionComplete")]
    [SerializeField] private UnityEvent onTryAgain = new UnityEvent();

    private void Start()
    {
        if (missionCompletePanel != null)
        {
            missionCompletePanel.alpha = 0f;
            missionCompletePanel.interactable = false;
            missionCompletePanel.blocksRaycasts = false;
        }

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(OnTryAgainClicked);
    }

    private void OnDestroy()
    {
        if (tryAgainButton != null)
            tryAgainButton.onClick.RemoveListener(OnTryAgainClicked);
    }

    /// <summary>綁定 <see cref="WardenCollectionManager.onAllCollected"/>（float 秒數）。</summary>
    public void ShowMissionComplete(float elapsedSeconds)
    {
        string formatted = WardenLeaderboard.FormatTimeMmSsFf(elapsedSeconds);
        Debug.Log(
            $"[MissionComplete] ShowMissionComplete elapsedSeconds={elapsedSeconds} (invoked on {name}), " +
            $"formatted={formatted}, timeTextAssigned={timeText != null}");

        if (timeText == null)
            Debug.LogWarning("[MissionComplete] timeText is not assigned; the TIME label will keep its scene default.");

        if (missionCompletePanel != null)
        {
            missionCompletePanel.alpha = 1f;
            missionCompletePanel.interactable = true;
            missionCompletePanel.blocksRaycasts = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (titleText != null)
            titleText.text = "MISSION COMPLETE";

        if (timeText != null)
            timeText.text = "TIME: " + formatted;

        bool isBest = leaderboard != null && leaderboard.IsPersonalBest(elapsedSeconds);
        if (personalBestText != null)
            personalBestText.text = isBest ? "NEW PERSONAL BEST!" : string.Empty;

        if (leaderboard != null)
        {
            leaderboard.SubmitTime(elapsedSeconds);
            if (leaderboardText != null)
                leaderboardText.text = BuildLeaderboardString(leaderboard.GetTopTimes());
        }

        if (pauseTimeOnComplete)
            Time.timeScale = 0f;
    }

    private static string BuildLeaderboardString(System.Collections.Generic.List<float> times)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TOP 5");
        for (int i = 0; i < times.Count; i++)
            sb.AppendLine($"{i + 1}. {WardenLeaderboard.FormatTimeMmSsFf(times[i])}");
        return sb.ToString().TrimEnd();
    }

    private void OnTryAgainClicked()
    {
        if (pauseTimeOnComplete)
            Time.timeScale = 1f;

        if (missionCompletePanel != null)
        {
            missionCompletePanel.alpha = 0f;
            missionCompletePanel.interactable = false;
            missionCompletePanel.blocksRaycasts = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        onTryAgain?.Invoke();
    }
}
