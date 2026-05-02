using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 岩漿等傷害來源可透過 Inspector 綁定 <see cref="TakeDamage"/>（float）；
/// 血量變化綁定 <see cref="onHealthChanged"/> 供 HUD Slider；歸零時觸發 <see cref="onDeath"/>（僅一次），
/// <see cref="RestoreFullHealth"/> 用於重開補滿並重置死亡標記。
/// </summary>
public class WardenHealthManager : MonoBehaviour
{
    [Header("血量設定")]
    [Tooltip("血量上限（Inspector 可調）")]
    [SerializeField]
    private float maxHealth = 100f;

    [Tooltip("回合開始時的初始血量（會夾在 0 與 maxHealth 之間）")]
    [SerializeField]
    private float startingHealth = 100f;

    [Header("事件")]
    [Tooltip("血量變更時帶入「目前血量」，可綁定 HUD Slider")]
    [SerializeField]
    private WardenHealthFloatUnityEvent onHealthChanged = new WardenHealthFloatUnityEvent();

    [Tooltip("血量歸零時觸發（同一輪僅觸發一次）")]
    [SerializeField]
    private UnityEvent onDeath = new UnityEvent();

    // 目前血量（不低於 0）
    private float _currentHealth;
    // 本輪是否已觸發過 onDeath（歸零僅觸發一次；RestoreFullHealth 會清除）
    private bool _deathTriggered;

    private void Awake()
    {
        ClampSettings();
        _currentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
    }

    private void Start()
    {
        RaiseHealthChanged();
    }

    /// <summary>造成傷害（例如岩漿 UnityEvent 綁定此方法）。</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f || _deathTriggered)
            return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        RaiseHealthChanged();

        if (_currentHealth <= 0f)
        {
            _deathTriggered = true;
            onDeath?.Invoke();
        }
    }

    /// <summary>補滿血量並重置死亡標記（重開／再試一次時呼叫）。</summary>
    public void RestoreFullHealth()
    {
        _deathTriggered = false;
        _currentHealth = Mathf.Max(0f, maxHealth);
        RaiseHealthChanged();
    }

    private void RaiseHealthChanged()
    {
        onHealthChanged?.Invoke(_currentHealth);
    }

    private void ClampSettings()
    {
        maxHealth = Mathf.Max(0f, maxHealth);
        startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHealth = Mathf.Max(0f, maxHealth);
        startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
    }
#endif
}

/// <summary>Inspector 可序列化的 float UnityEvent。</summary>
[Serializable]
public class WardenHealthFloatUnityEvent : UnityEvent<float> { }
