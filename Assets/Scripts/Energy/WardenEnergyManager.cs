using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 玩家能量：供拉霸等系統查詢／消耗／回復／清空；數值變更時可透過事件通知 UI。
/// </summary>
public class WardenEnergyManager : MonoBehaviour
{
    [Header("數值")]
    [Tooltip("能量上限")]
    [SerializeField] private float maxEnergy = 100f;

    [Tooltip("場景載入時的初始能量")]
    [SerializeField] private float startingEnergy = 100f;

    [Header("事件")]
    [Tooltip("當前能量變更時觸發（新數值）")]
    [SerializeField] private UnityEvent<float> onEnergyChanged = new UnityEvent<float>();

    private float _current;

    /// <summary>目前能量（唯讀給外部查詢）。</summary>
    public float CurrentEnergy => _current;

    /// <summary>能量上限。</summary>
    public float MaxEnergy => maxEnergy;

    private void Awake()
    {
        _current = Mathf.Clamp(startingEnergy, 0f, maxEnergy);
    }

    private void Start()
    {
        onEnergyChanged?.Invoke(_current);
    }

    /// <summary>若足夠則扣除並回傳 true。</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f)
            return true;
        if (_current < amount)
            return false;
        SetCurrent(_current - amount);
        return true;
    }

    /// <summary>增加能量（不超過上限）。</summary>
    public void AddEnergy(float amount)
    {
        if (amount <= 0f)
            return;
        SetCurrent(_current + amount);
    }

    /// <summary>將能量歸零（過載等效果）。</summary>
    public void ClearEnergy()
    {
        SetCurrent(0f);
    }

    private void SetCurrent(float value)
    {
        _current = Mathf.Clamp(value, 0f, maxEnergy);
        onEnergyChanged?.Invoke(_current);
        Debug.Log($"[Energy] 當前能量：{_current} / {maxEnergy}");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxEnergy = Mathf.Max(1f, maxEnergy);
        startingEnergy = Mathf.Clamp(startingEnergy, 0f, maxEnergy);
    }
#endif
}
