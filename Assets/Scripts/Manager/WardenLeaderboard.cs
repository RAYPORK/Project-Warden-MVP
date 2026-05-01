using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 本地完成時間排行榜（最快 5 筆），使用 <see cref="PlayerPrefs"/> 儲存。
/// </summary>
public class WardenLeaderboard : MonoBehaviour
{
    private const string KeyPrefix = "Warden_LB_";
    private const int SlotCount = 5;

    /// <summary>提交新時間（秒）；≤0 不寫入。會排序後保留最快 5 筆。</summary>
    public void SubmitTime(float seconds)
    {
        if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            return;

        List<float> times = GetTopTimes();
        times.Add(seconds);
        times.Sort();
        while (times.Count > SlotCount)
            times.RemoveAt(times.Count - 1);

        SaveTimes(times);
    }

    /// <summary>回傳目前最快時間列表（由快到慢，最多 5 筆；無紀錄則為空）。</summary>
    public List<float> GetTopTimes()
    {
        var list = new List<float>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            float t = PlayerPrefs.GetFloat(KeyPrefix + i, float.MaxValue);
            if (t < float.MaxValue && !float.IsInfinity(t))
                list.Add(t);
        }

        list.Sort();
        return list;
    }

    /// <summary>是否優於目前榜單最佳（無紀錄時視為個人最佳）。</summary>
    public bool IsPersonalBest(float seconds)
    {
        if (seconds <= 0f)
            return false;
        List<float> top = GetTopTimes();
        if (top.Count == 0)
            return true;
        return seconds < top[0];
    }

    /// <summary>清除全部排行榜槽位（測試用）。</summary>
    public void ClearLeaderboard()
    {
        for (int i = 0; i < SlotCount; i++)
            PlayerPrefs.SetFloat(KeyPrefix + i, float.MaxValue);
        PlayerPrefs.Save();
    }

    /// <summary>格式化為 mm:ss.ff（分:秒.百分秒）。</summary>
    public static string FormatTimeMmSsFf(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;
        int totalCs = Mathf.Clamp(Mathf.RoundToInt(seconds * 100f), 0, int.MaxValue);
        int cs = totalCs % 100;
        int s = (totalCs / 100) % 60;
        int m = totalCs / 6000;
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:00}", m, s, cs);
    }

    private static void SaveTimes(List<float> sortedAscending)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            float v = i < sortedAscending.Count ? sortedAscending[i] : float.MaxValue;
            PlayerPrefs.SetFloat(KeyPrefix + i, v);
        }

        PlayerPrefs.Save();
    }
}
