using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 狀態視覺 HUD：大獎與過載共用同一 TMP 倒數文字（金色／暗紅）；大獎秒數見 <see cref="jackpotDuration"/>，過載見 <see cref="overloadDuration"/>，再次觸發會重置計時。
/// 由 <see cref="StartJackpotTimer"/>／<see cref="StartOverloadTimer"/> 搭配 UnityEvent 或程式呼叫。
/// </summary>
public class WardenBuffTimerHUD : MonoBehaviour
{
    /// <summary>大獎倒數文字顏色 RGB(255, 200, 0)。</summary>
    private static readonly Color JackpotTextColor = new Color(255f / 255f, 200f / 255f, 0f, 1f);

    /// <summary>過載倒數文字顏色 RGB(200, 0, 0)。</summary>
    private static readonly Color OverloadTextColor = new Color(200f / 255f, 0f, 0f, 1f);

    [Header("共用倒數文字（大獎／過載）")]
    [Tooltip("大獎與過載共用之剩餘時間 TMP（為空則兩者皆跳過）")]
    [SerializeField]
    private TMP_Text jackpotTimerText;

    [Tooltip("大獎倒數總秒數（StartJackpotTimer 專用，預設 30）")]
    [SerializeField]
    private float jackpotDuration = 30f;

    [Tooltip("過載倒數總秒數（StartOverloadTimer 專用，預設 15）")]
    [SerializeField]
    private float overloadDuration = 15f;

    // 場景載入時 TMP 的原始顏色（倒數結束後還原）
    private Color _textColorDefault;
    private Coroutine _countdownRoutine;

    private void Awake()
    {
        if (jackpotTimerText != null)
            _textColorDefault = jackpotTimerText.color;
    }

    private void OnDisable()
    {
        StopAllTimers();
    }

    /// <summary>開始大獎倒數（金色「JACKPOT! Xs」）；使用 <see cref="jackpotDuration"/>（預設 30 秒），再次呼叫會重置計時。</summary>
    public void StartJackpotTimer()
    {
        Debug.Log($"[BuffHUD] StartJackpotTimer called, textAssigned: {jackpotTimerText != null}");
        if (jackpotTimerText == null)
            return;

        StopCountdownRoutine();
        _countdownRoutine = StartCoroutine(CountdownRoutine(JackpotTextColor, "JACKPOT! ", jackpotDuration));
    }

    /// <summary>開始過載倒數（暗紅「OVERLOAD! Xs」）；與大獎共用同一 TMP，使用 <see cref="overloadDuration"/>，再次呼叫會重置計時。</summary>
    public void StartOverloadTimer()
    {
        if (jackpotTimerText == null)
            return;

        StopCountdownRoutine();
        _countdownRoutine = StartCoroutine(CountdownRoutine(OverloadTextColor, "OVERLOAD! ", overloadDuration));
    }

    private void StopCountdownRoutine()
    {
        if (_countdownRoutine == null)
            return;

        StopCoroutine(_countdownRoutine);
        _countdownRoutine = null;
    }

    /// <summary>
    /// 停止共用倒數協程、清空 TMP 文字並還原字色；重置內部計時狀態（再開倒數會從頭計）。
    /// </summary>
    public void StopAllTimers()
    {
        StopCountdownRoutine();

        if (jackpotTimerText != null)
        {
            jackpotTimerText.text = string.Empty;
            jackpotTimerText.color = _textColorDefault;
        }
    }

    /// <summary>與 <see cref="StopAllTimers"/> 相同，供重開／切場景時強制還原 HUD，確保不殘留倒數字樣。</summary>
    public void ResetHUD()
    {
        StopAllTimers();
    }

    /// <summary>共用倒數協程：最後 5 秒文字 alpha 閃爍，結束後清空並還原字色（秒數由參數決定）。</summary>
    private IEnumerator CountdownRoutine(Color baseColor, string labelPrefix, float durationSeconds)
    {
        jackpotTimerText.color = baseColor;
        float endTime = Time.time + Mathf.Max(0.01f, durationSeconds);

        while (Time.time < endTime)
        {
            float remaining = endTime - Time.time;
            int sec = Mathf.Max(0, Mathf.CeilToInt(remaining));
            jackpotTimerText.text = $"{labelPrefix}{sec}s";

            Color c = baseColor;
            if (remaining < 5f)
            {
                float pulse = (Mathf.Sin(Time.time * Mathf.PI * 6f) + 1f) * 0.5f;
                c.a = Mathf.Lerp(0.35f, 1f, pulse);
            }

            jackpotTimerText.color = c;
            yield return null;
        }

        jackpotTimerText.text = string.Empty;
        jackpotTimerText.color = _textColorDefault;
        _countdownRoutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        jackpotDuration = Mathf.Max(0.01f, jackpotDuration);
        overloadDuration = Mathf.Max(0.01f, overloadDuration);
    }
#endif
}
