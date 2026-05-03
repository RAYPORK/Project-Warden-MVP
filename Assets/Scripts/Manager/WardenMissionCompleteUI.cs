using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 任務完成面板：將 <see cref="WardenCollectionManager.onAllCollected"/> 綁定至 <see cref="ShowMissionComplete"/>，
/// 顯示距離、存活時間、本地距離排行榜與是否刷新最佳；再試一次可綁定 <see cref="WardenDeathManager.RestartRunAfterMissionComplete"/>。
/// </summary>
public class WardenMissionCompleteUI : MonoBehaviour
{
    [Header("資料來源")]
    [SerializeField] private WardenLeaderboard leaderboard;

    [Tooltip("本局最遠 Z；未指派則距離以 0 提交排行榜")]
    [SerializeField]
    private WardenDistanceTracker distanceTracker;

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

    /// <summary>任務完成面板是否正在顯示（阻擋暫停選單等）。</summary>
    public bool IsMissionCompletePanelVisible =>
        missionCompletePanel != null &&
        missionCompletePanel.alpha >= 0.99f &&
        missionCompletePanel.blocksRaycasts;

    private void Awake()
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

    /// <summary>綁定 <see cref="WardenCollectionManager.onAllCollected"/>（float 為本局經過秒數，僅供顯示存活時間）。</summary>
    public void ShowMissionComplete(float elapsedSeconds)
    {
        if (distanceTracker != null)
            distanceTracker.StopTracking();

        float runDistance = distanceTracker != null ? distanceTracker.GetCurrentDistance() : 0f;
        string distStr = WardenLeaderboard.FormatDistance(runDistance);
        string timeStr = FormatElapsedMmSs(elapsedSeconds);

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
            timeText.text = "DISTANCE: " + distStr + "\nTIME: " + timeStr;

        bool isBest = leaderboard != null && leaderboard.IsPersonalBest(runDistance);
        if (personalBestText != null)
            personalBestText.text = isBest ? "NEW PERSONAL BEST!" : string.Empty;

        if (leaderboard != null)
        {
            leaderboard.SubmitDistance(runDistance);
            if (leaderboardText != null)
                leaderboardText.text = BuildLeaderboardString(leaderboard.GetTopDistances());
        }

        if (pauseTimeOnComplete)
            Time.timeScale = 0f;
    }

    private static string BuildLeaderboardString(System.Collections.Generic.List<float> distances)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TOP 5");
        for (int i = 0; i < distances.Count; i++)
            sb.AppendLine($"{i + 1}. {WardenLeaderboard.FormatDistance(distances[i])}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>存活時間顯示為 mm:ss（整秒）。</summary>
    private static string FormatElapsedMmSs(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;
        int totalSec = Mathf.FloorToInt(seconds);
        int mm = totalSec / 60;
        int ss = totalSec % 60;
        return $"{mm:00}:{ss:00}";
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
