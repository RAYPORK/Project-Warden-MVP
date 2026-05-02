using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 全遊戲音效單例：監聽各系統 UnityEvent 並播放對應音效。
/// 使用 DontDestroyOnLoad，場景重新載入時若已存在則銷毀重複物件。
/// </summary>
public class WardenAudioManager : MonoBehaviour
{
    public static WardenAudioManager Instance { get; private set; }

    [Header("一次性音效與 BGM 音檔")]
    [Tooltip("鋼索發射")]
    [SerializeField]
    private AudioClip grappleLaunchClip;

    [Tooltip("拉霸滾輪（循環）")]
    [SerializeField]
    private AudioClip slotSpinClip;

    [Tooltip("大獎")]
    [SerializeField]
    private AudioClip jackpotClip;

    [Tooltip("能量收集")]
    [SerializeField]
    private AudioClip energyCollectClip;

    [Tooltip("死亡")]
    [SerializeField]
    private AudioClip deathClip;

    [Tooltip("過載音效")]
    [SerializeField]
    private AudioClip overloadClip;

    [Tooltip("背景音樂（循環）")]
    [SerializeField]
    private AudioClip bgmClip;

    [Header("Mixer 路由")]
    [Tooltip("指派至 SFX Mixer Group")]
    [SerializeField]
    private AudioMixerGroup sfxMixerGroup;

    [Tooltip("指派至 BGM Mixer Group")]
    [SerializeField]
    private AudioMixerGroup bgmMixerGroup;

    // Awake 中自動建立
    private AudioSource _sfxSource;
    private AudioSource _slotSpinSource;
    private AudioSource _bgmSource;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _slotSpinSource = gameObject.AddComponent<AudioSource>();
        _bgmSource = gameObject.AddComponent<AudioSource>();

        ConfigureSource2D(_sfxSource, loop: false, sfxMixerGroup);
        ConfigureSource2D(_slotSpinSource, loop: true, sfxMixerGroup);
        ConfigureSource2D(_bgmSource, loop: true, bgmMixerGroup);

        if (bgmClip != null && _bgmSource != null)
        {
            _bgmSource.clip = bgmClip;
            _bgmSource.loop = true;
            _bgmSource.Play();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>將 AudioSource 設為純 2D、指定 Mixer Group 與循環。</summary>
    private static void ConfigureSource2D(AudioSource source, bool loop, AudioMixerGroup mixerGroup)
    {
        source.spatialBlend = 0f;
        source.loop = loop;
        source.playOnAwake = false;
        source.outputAudioMixerGroup = mixerGroup;
    }

    /// <summary>鋼索發射（UnityEvent 可綁定）。</summary>
    public void PlayGrappleLaunch()
    {
        PlayOneShotIfClip(grappleLaunchClip);
    }

    /// <summary>能量收集（UnityEvent 可綁定）。</summary>
    public void PlayEnergyCollect()
    {
        PlayOneShotIfClip(energyCollectClip);
    }

    /// <summary>開始循環播放拉霸滾輪聲（UnityEvent 可綁定）。</summary>
    public void StartSlotSpin()
    {
        Debug.Log("[Audio] StartSlotSpin called");
        if (slotSpinClip == null || _slotSpinSource == null)
            return;

        _slotSpinSource.clip = slotSpinClip;
        _slotSpinSource.loop = true;
        if (!_slotSpinSource.isPlaying)
            _slotSpinSource.Play();
    }

    /// <summary>停止拉霸滾輪聲並清除 clip（UnityEvent 可綁定）。</summary>
    public void StopSlotSpin()
    {
        if (_slotSpinSource == null)
            return;

        _slotSpinSource.Stop();
        _slotSpinSource.clip = null;
    }

    /// <summary>
    /// 停止拉霸滾輪聲（無參數）；供 <c>UnityEvent&lt;int,int,int&gt;</c>（如 ThreeIntsEvent）以 Static Parameters 綁定，
    /// 與帶事件參數的簽章區隔時使用。
    /// </summary>
    public void StopSlotSpinNoParam()
    {
        Debug.Log("[Audio] StopSlotSpinNoParam called");
        if (_slotSpinSource == null)
            return;

        _slotSpinSource.Stop();
    }

    /// <summary>大獎（UnityEvent 可綁定）。</summary>
    public void PlayJackpot()
    {
        PlayOneShotIfClip(jackpotClip);
    }

    /// <summary>死亡（UnityEvent 可綁定）。</summary>
    public void PlayDeath()
    {
        PlayOneShotIfClip(deathClip);
    }

    /// <summary>過載（UnityEvent 可綁定）。</summary>
    public void PlayOverload()
    {
        PlayOneShotIfClip(overloadClip);
    }

    // --- Editor 右鍵選單：快速驗證音檔與路由（須在 Play 模式下、且已指派對應 Clip）---

    /// <summary>Editor 選單：測試鋼索發射音效。</summary>
    [ContextMenu("Test Grapple Sound")]
    public void TestGrappleSound() => PlayGrappleLaunch();

    /// <summary>Editor 選單：測試大獎音效。</summary>
    [ContextMenu("Test Jackpot Sound")]
    public void TestJackpotSound() => PlayJackpot();

    /// <summary>Editor 選單：測試過載音效。</summary>
    [ContextMenu("Test Overload Sound")]
    public void TestOverloadSound() => PlayOverload();

    /// <summary>Editor 選單：測試死亡音效。</summary>
    [ContextMenu("Test Death Sound")]
    public void TestDeathSound() => PlayDeath();

    /// <summary>Editor 選單：測試能量收集音效。</summary>
    [ContextMenu("Test Collect Sound")]
    public void TestCollectSound() => PlayEnergyCollect();

    /// <summary>Editor 選單：測試拉霸滾輪循環聲（開始）。</summary>
    [ContextMenu("Test Slot Spin")]
    public void TestSlotSpin() => StartSlotSpin();

    /// <summary>Editor 選單：測試停止拉霸滾輪聲。</summary>
    [ContextMenu("Test Stop Slot Spin")]
    public void TestStopSlotSpin() => StopSlotSpinNoParam();

    private void PlayOneShotIfClip(AudioClip clip)
    {
        if (clip == null || _sfxSource == null)
            return;

        _sfxSource.PlayOneShot(clip);
    }
}
