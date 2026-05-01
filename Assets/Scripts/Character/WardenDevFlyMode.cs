using UnityEngine;

/// <summary>
/// 開發用「空中飛人」模式：切換後以 Kinematic 方式自由飛行，方便飛到生成平台測試。
/// 掛在 PlayerRig 上；可改按鍵，或由 UI Button 呼叫 <see cref="ToggleFly"/>。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WardenDevFlyMode : MonoBehaviour
{
    /// <summary>其他腳本（移動／鋼索）據此略過一般物理。</summary>
    public static bool IsFlying { get; private set; }

    [Header("切換")]
    [Tooltip("舊版輸入：按下此鍵切換飛人模式開／關")]
    [SerializeField] private KeyCode toggleFlyKey = KeyCode.F9;

    [Tooltip("飛行時是否顯示游標（方便切回編輯器或點 UI）")]
    [SerializeField] private bool showCursorWhileFlying = true;

    [Header("飛行速度")]
    [Tooltip("WASD 水平飛行速度（公尺／秒，依主攝影機水平方向）")]
    [SerializeField] private float flyHorizontalSpeed = 22f;

    [Tooltip("Space 上升／LeftControl 下降 速度（公尺／秒）")]
    [SerializeField] private float flyVerticalSpeed = 18f;

    [Tooltip("若留空則使用 Camera.main")]
    [SerializeField] private Camera flyCamera;

    private Rigidbody _rb;
    private bool _flyActive;
    private bool _savedUseGravity;
    private bool _savedIsKinematic;
    private RigidbodyInterpolation _savedInterpolation;
    private WardenWinchSystem _winch;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _winch = GetComponent<WardenWinchSystem>();
        if (flyCamera == null)
            flyCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (_flyActive)
            ExitFly();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleFlyKey))
            ToggleFly();
    }

    private void FixedUpdate()
    {
        if (!_flyActive)
            return;

        ApplyFlyMovement();
    }

    /// <summary>供 UI Button 等呼叫：在飛行／一般模式之間切換。</summary>
    public void ToggleFly()
    {
        if (_flyActive)
            ExitFly();
        else
            EnterFly();
    }

    private void EnterFly()
    {
        _winch?.ForceDisconnectIfConnected();

        _savedUseGravity = _rb.useGravity;
        _savedIsKinematic = _rb.isKinematic;
        _savedInterpolation = _rb.interpolation;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _flyActive = true;
        IsFlying = true;

        if (showCursorWhileFlying)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Debug.Log("[WardenDevFlyMode] 空中飛人：開（再按 " + toggleFlyKey + " 或呼叫 ToggleFly 關閉）");
    }

    private void ExitFly()
    {
        _rb.isKinematic = _savedIsKinematic;
        _rb.useGravity = _savedUseGravity;
        _rb.interpolation = _savedInterpolation;

        _flyActive = false;
        IsFlying = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[WardenDevFlyMode] 空中飛人：關");
    }

    private void ApplyFlyMovement()
    {
        Camera c = flyCamera != null ? flyCamera : Camera.main;
        if (c == null)
            return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float y = 0f;
        if (Input.GetKey(KeyCode.Space))
            y += 1f;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftShift))
            y -= 1f;

        Vector3 forward = c.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 1e-6f)
            forward.Normalize();
        else
            forward = Vector3.forward;

        Vector3 right = c.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude > 1e-6f)
            right.Normalize();
        else
            right = Vector3.right;

        Vector3 delta = (forward * v + right * h) * flyHorizontalSpeed + Vector3.up * y * flyVerticalSpeed;
        delta *= Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + delta);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        flyHorizontalSpeed = Mathf.Max(0f, flyHorizontalSpeed);
        flyVerticalSpeed = Mathf.Max(0f, flyVerticalSpeed);
    }
#endif
}
