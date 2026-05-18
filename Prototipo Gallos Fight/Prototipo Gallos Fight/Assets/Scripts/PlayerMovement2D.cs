using UnityEngine;

public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeed     = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField, Range(0f, 1f)] private float friction = 0.85f;

    [Header("Dash")]
    [SerializeField] private float dashForce    = 18f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashDuration = 0.15f;

    [Header("Control — Teclado")]
    [SerializeField] private string  horizontalAxis = "Horizontal";
    [SerializeField] private string  verticalAxis   = "Vertical";
    [SerializeField] private KeyCode dashKey        = KeyCode.LeftShift;

    [Header("Control — Mando Xbox — Movimiento")]
    [SerializeField] private KeyCode gamepadDashKey = KeyCode.JoystickButton0;

    [Header("Control — Mando Xbox — Apuntado Stick Derecho")]
    [Tooltip("Numero de eje joystick para stick derecho HORIZONTAL.\nXbox XInput Windows: prueba 4. Si no funciona prueba 3.")]
    [SerializeField, Range(1, 10)] private int aimAxisX = 4;

    [Tooltip("Numero de eje joystick para stick derecho VERTICAL.\nXbox XInput Windows: prueba 5. Si no funciona prueba 4 o 6.")]
    [SerializeField, Range(1, 10)] private int aimAxisY = 5;

    [Tooltip("Activa si al empujar el stick ARRIBA el personaje mira ABAJO.")]
    [SerializeField] private bool invertAimY = false;

    [Tooltip("Activa si al empujar el stick DERECHA el personaje mira IZQUIERDA.")]
    [SerializeField] private bool invertAimX = false;

    [Header("Control — Mando Xbox — General")]
    [SerializeField, Range(0.05f, 0.9f)] private float gamepadDeadzone = 0.25f;

    [Header("Debug (desactiva en build final)")]
    [SerializeField] private bool showGamepadDebug = true;

    private Rigidbody2D      _rb;
    private AttackController _attackController;
    private Camera           _cam;

    private Vector2 _lastInputDir      = Vector2.up;
    private Vector2 _gamepadAimDir     = Vector2.zero;
    private bool    _usingGamepad      = false;
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
        _cam              = Camera.main;

        _rb.gravityScale  = 0f;
        _rb.linearDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        DetectInputDevice();
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

    public void AddSpeedBonus(float percent)
    {
        _speedBonus += percent;
        Debug.Log($"[PlayerMovement2D] Velocidad +{percent*100f:F0}%. Max: {CurrentMaxSpeed:F1}");
    }

    // Lee un eje de joystick directamente por numero (1-based).
    private float ReadJoystickAxis(int axisNumber)
    {
        string axisName = "joystick axis " + axisNumber;
        try   { return Input.GetAxisRaw(axisName); }
        catch { return 0f; }
    }

    private void DetectInputDevice()
    {
        float lx = Input.GetAxisRaw(horizontalAxis);
        float ly = Input.GetAxisRaw(verticalAxis);
        float rx = ReadJoystickAxis(aimAxisX);
        float ry = ReadJoystickAxis(aimAxisY);

        bool gamepadActive = Mathf.Abs(lx) > gamepadDeadzone
                          || Mathf.Abs(ly) > gamepadDeadzone
                          || Mathf.Abs(rx) > gamepadDeadzone
                          || Mathf.Abs(ry) > gamepadDeadzone
                          || Input.GetKey(gamepadDashKey);

        if (gamepadActive)
            _usingGamepad = true;

        if (Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f)
            _usingGamepad = false;
    }

    private void HandleAim()
    {
        Vector2 aimDir = Vector2.zero;

        if (_usingGamepad)
        {
            float rx = ReadJoystickAxis(aimAxisX) * (invertAimX ? -1f : 1f);
            float ry = ReadJoystickAxis(aimAxisY) * (invertAimY ? -1f : 1f);

            if (Mathf.Abs(rx) > gamepadDeadzone || Mathf.Abs(ry) > gamepadDeadzone)
            {
                aimDir         = new Vector2(rx, ry).normalized;
                _gamepadAimDir = aimDir;
            }
            else if (_gamepadAimDir != Vector2.zero)
            {
                aimDir = _gamepadAimDir;
            }
            else
            {
                float lx = Input.GetAxisRaw(horizontalAxis);
                float ly = Input.GetAxisRaw(verticalAxis);
                if (Mathf.Abs(lx) > gamepadDeadzone || Mathf.Abs(ly) > gamepadDeadzone)
                    aimDir = new Vector2(lx, ly).normalized;
            }
        }
        else
        {
            if (_cam != null)
            {
                Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                aimDir = ((Vector2)(mouseWorld - transform.position)).normalized;
            }
        }

        if (aimDir == Vector2.zero) return;

        float angle = Mathf.Atan2(aimDir.x, aimDir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, -angle);
        _attackController?.SetFacingDirection(aimDir);
    }

    private void ApplyMovement()
    {
        float lx = Input.GetAxisRaw(horizontalAxis);
        float ly = Input.GetAxisRaw(verticalAxis);
        float dz = _usingGamepad ? gamepadDeadzone : 0.01f;

        Vector2 input = new Vector2(
            Mathf.Abs(lx) > dz ? lx : 0f,
            Mathf.Abs(ly) > dz ? ly : 0f
        );
        input = Vector2.ClampMagnitude(input, 1f);

        if (input != Vector2.zero)
            _lastInputDir = input.normalized;

        _rb.AddForce(input * acceleration, ForceMode2D.Force);
    }

    private void ApplyFriction()
    {
        float lx = Input.GetAxisRaw(horizontalAxis);
        float ly = Input.GetAxisRaw(verticalAxis);
        float dz = _usingGamepad ? gamepadDeadzone : 0.01f;

        bool hasInput = Mathf.Abs(lx) > dz || Mathf.Abs(ly) > dz;
        if (!hasInput)
            _rb.linearVelocity *= friction;
    }

    private void ClampVelocity()
    {
        if (_rb.linearVelocity.magnitude > CurrentMaxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * CurrentMaxSpeed;
    }

    private void HandleDashInput()
    {
        bool dashPressed = Input.GetKeyDown(dashKey) || Input.GetKeyDown(gamepadDashKey);
        if (dashPressed && _dashCooldownTimer <= 0f)
        {
            _isDashing         = true;
            _dashActiveTimer   = dashDuration;
            _dashCooldownTimer = dashCooldown;
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(_lastInputDir * dashForce, ForceMode2D.Impulse);
        }
    }

    private void HandleDashTimer()
    {
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        if (_isDashing)
        {
            _dashActiveTimer -= Time.deltaTime;
            if (_dashActiveTimer <= 0f)
                _isDashing = false;
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 90),
            $"Vel: {_rb.linearVelocity.magnitude:F2} / {CurrentMaxSpeed:F1}\n" +
            $"Dash CD: {Mathf.Max(0f, _dashCooldownTimer):F1}s\n" +
            $"Input: {(_usingGamepad ? "MANDO" : "Teclado/Raton")}");

        if (!showGamepadDebug) return;

        GUI.Box(new Rect(8, 108, 255, 205), "");
        GUI.Label(new Rect(12, 112, 248, 198),
            "[DEBUG STICK DERECHO]\n" +
            $"Eje{aimAxisX}(AimX): {ReadJoystickAxis(aimAxisX):F3}\n" +
            $"Eje{aimAxisY}(AimY): {ReadJoystickAxis(aimAxisY):F3}\n" +
            $"Dir apuntado: {_gamepadAimDir}\n" +
            "--- Todos los ejes ---\n" +
            $"Eje1:{ReadJoystickAxis(1):F2}  Eje2:{ReadJoystickAxis(2):F2}\n" +
            $"Eje3:{ReadJoystickAxis(3):F2}  Eje4:{ReadJoystickAxis(4):F2}\n" +
            $"Eje5:{ReadJoystickAxis(5):F2}  Eje6:{ReadJoystickAxis(6):F2}\n" +
            $"Eje7:{ReadJoystickAxis(7):F2}  Eje8:{ReadJoystickAxis(8):F2}");
    }
}
