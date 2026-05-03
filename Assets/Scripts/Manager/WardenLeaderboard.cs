using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本地最遠距離排行榜（前 5 名），距離越大越好；使用 <see cref="PlayerPrefs"/> 鍵名 Warden_Dist_0～Warden_Dist_4。
/// </summary>
public class WardenLeaderboard : MonoBehaviour
{
    private const string KeyPrefix = "Warden_Dist_";
    private const int SlotCount = 5;

    /// <summary>空槽位寫入值（讀取時會略過）。</summary>
    private const float EmptySlotValue = -1f;

    /// <summary>提交距離（公尺）；負值、NaN、無限大不寫入。保留距離最大的 5 筆（降序）。</summary>
    public void SubmitDistance(float distance)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
            return;

        List<float> list = GetTopDistances();
        list.Add(distance);
        list.Sort((a, b) => b.CompareTo(a));
        while (list.Count > SlotCount)
            list.RemoveAt(list.Count - 1);

        SaveDistances(list);
    }

    /// <summary>回傳目前前 5 名距離（由大到小；無紀錄則為空清單）。</summary>
    public List<float> GetTopDistances()
    {
        var list = new List<float>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            float v = PlayerPrefs.GetFloat(KeyPrefix + i, EmptySlotValue);
            if (v >= 0f && !float.IsNaN(v) && !float.IsInfinity(v))
                list.Add(v);
        }

        list.Sort((a, b) => b.CompareTo(a));
        return list;
    }

    /// <summary>是否優於榜上最佳（無紀錄時視為可寫入新紀錄）。</summary>
    public bool IsPersonalBest(float distance)
    {
        if (distance < 0f || float.IsNaN(distance) || float.IsInfinity(distance))
            return false;

        List<float> top = GetTopDistances();
        if (top.Count == 0)
            return true;
        return distance > top[0];
    }

    /// <summary>格式化為整數公尺字串，例如「999m」。</summary>
    public static string FormatDistance(float distance)
    {
        int m = Mathf.FloorToInt(Mathf.Max(0f, distance));
        return m + "m";
    }

    /// <summary>清除 Warden_Dist_0～4 全部紀錄。</summary>
    public void ClearLeaderboard()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            PlayerPrefs.SetFloat(KeyPrefix + i, EmptySlotValue);
        }

        PlayerPrefs.Save();
    }

    private static void SaveDistances(List<float> sortedDescending)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            float v = i < sortedDescending.Count ? sortedDescending[i] : EmptySlotValue;
            PlayerPrefs.SetFloat(KeyPrefix + i, v);
        }

        PlayerPrefs.Save();
    }
}
