using UnityEngine;

public class PlayerMovement2D_DualStick : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeed     = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField, Range(0f, 1f)] private float friction = 0.85f;

    [Header("Dash")]
    [SerializeField] private float dashForce    = 18f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashDuration = 0.15f;

    [Header("Control — Teclado (fallback)")]
    [SerializeField] private string  horizontalAxis = "Horizontal";
    [SerializeField] private string  verticalAxis   = "Vertical";
    [SerializeField] private KeyCode dashKey        = KeyCode.LeftShift;

    [Header("Control — Mando — Dash")]
    [SerializeField] private KeyCode gamepadDashKey = KeyCode.JoystickButton0;

    [Header("Control — Mando — Stick Derecho (Apuntado)")]
    [Tooltip("joystick axis 4 = horizontal stick derecho en Xbox XInput")]
    [SerializeField, Range(1, 28)] private int  aimAxisX  = 4;
    [Tooltip("joystick axis 5 = vertical stick derecho en Xbox XInput")]
    [SerializeField, Range(1, 28)] private int  aimAxisY  = 5;
    [SerializeField] private bool               invertAimX = false;
    [SerializeField] private bool               invertAimY = false;

    [Header("Control — General")]
    [SerializeField, Range(0.05f, 0.9f)] private float gamepadDeadzone = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool showGamepadDebug = true;

    private Rigidbody2D      _rb;
    private AttackController _attackController;

    private Vector2 _lastMoveDir       = Vector2.up;
    private Vector2 _lastAimDir        = Vector2.up;
    private float   _dashCooldownTimer = 0f;
    private float   _dashActiveTimer   = 0f;
    private bool    _isDashing         = false;
    private float   _speedBonus        = 0f;

    public float BaseSpeed       => maxSpeed;
    public float CurrentMaxSpeed => maxSpeed * (1f + _speedBonus);

    private void Awake()
    {
        _rb               = GetComponent<Rigidbody2D>();
        _attackController = GetComponent<AttackController>();
        _rb.gravityScale  = 0f;
        _rb.linearDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        HandleAim();
        HandleDashInput();
        HandleDashTimer();
    }

    private void FixedUpdate()
    {
        if (!_isDashing)
        {
            ApplyMovement();
            ApplyFriction();
            ClampVelocity();
        }
    }

    public void AddSpeedBonus(float percent) => _speedBonus += percent;

    private float RawAxis(int n)
    {
        try   { return Input.GetAxisRaw("joystick axis " + n); }
        catch { return 0f; }
    }

    private void HandleAim()
    {
        float ax = RawAxis(aimAxisX) * (invertAimX ? -1f : 1f);
        float ay = RawAxis(aimAxisY) * (invertAimY ? -1f : 1f);

        if (Mathf.Abs(ax) > gamepadDeadzone || Mathf.Abs(ay) > gamepadDeadzone)
            _lastAimDir = new Vector2(ax, ay).normalized;

        float angle = Mathf.Atan2(_lastAimDir.x, _lastAimDir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, -angle);
        _attackController?.SetFacingDirection(_lastAimDir);
    }

    private void ApplyMovement()
    {
        float lx = Input.GetAxisRaw(horizontalAxis);
        float ly = Input.GetAxisRaw(verticalAxis);

        Vector2 input = new Vector2(
            Mathf.Abs(lx) > gamepadDeadzone ? lx : 0f,
            Mathf.Abs(ly) > gamepadDeadzone ? ly : 0f
        );

        if (input == Vector2.zero)
            input = new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis));

        input = Vector2.ClampMagnitude(input, 1f);
        if (input != Vector2.zero) _lastMoveDir = input.normalized;
        _rb.AddForce(input * acceleration, ForceMode2D.Force);
    }

    private void ApplyFriction()
    {
        bool moving = Mathf.Abs(Input.GetAxisRaw(horizontalAxis)) > gamepadDeadzone
                   || Mathf.Abs(Input.GetAxisRaw(verticalAxis))   > gamepadDeadzone;
        if (!moving) _rb.linearVelocity *= friction;
    }

    private void ClampVelocity()
    {
        if (_rb.linearVelocity.magnitude > CurrentMaxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * CurrentMaxSpeed;
    }

    private void HandleDashInput()
    {
        if ((Input.GetKeyDown(dashKey) || Input.GetKeyDown(gamepadDashKey)) && _dashCooldownTimer <= 0f)
        {
            _isDashing         = true;
            _dashActiveTimer   = dashDuration;
            _dashCooldownTimer = dashCooldown;
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(_lastMoveDir * dashForce, ForceMode2D.Impulse);
        }
    }

    private void HandleDashTimer()
    {
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
        if (_isDashing)
        {
            _dashActiveTimer -= Time.deltaTime;
            if (_dashActiveTimer <= 0f) _isDashing = false;
        }
    }

    private void OnGUI()
    {
        if (!showGamepadDebug) return;

        GUI.Box(new Rect(8, 8, 300, 120), "");
        GUI.Label(new Rect(12, 12, 292, 114),
            "=== DEBUG DUAL STICK ===\n" +
            $"Eje {aimAxisX} (AimX raw):  {RawAxis(aimAxisX):F3}\n" +
            $"Eje {aimAxisY} (AimY raw):  {RawAxis(aimAxisY):F3}\n" +
            $"AimX (con invert): {RawAxis(aimAxisX) * (invertAimX ? -1f : 1f):F3}\n" +
            $"AimY (con invert): {RawAxis(aimAxisY) * (invertAimY ? -1f : 1f):F3}\n" +
            $"_lastAimDir: {_lastAimDir}\n" +
            $"Vel: {_rb.linearVelocity.magnitude:F2} / {CurrentMaxSpeed:F1}");
    }
}
