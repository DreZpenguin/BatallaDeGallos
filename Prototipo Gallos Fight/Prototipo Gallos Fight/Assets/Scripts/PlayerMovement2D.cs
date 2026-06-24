// ============================================================
//  PlayerMovement2D.cs  — v2
//
//  CAMBIO respecto a v1:
//   · La rotación del personaje ya NO depende del stick izquierdo
//     ni de la dirección de movimiento (WASD).
//   · Con teclado/ratón: rota hacia el cursor del mouse (igual que antes).
//   · Con mando: rota SOLO si el stick derecho supera el deadzone.
//     Si el stick derecho está en reposo, el personaje mantiene
//     su última dirección de apuntado sin girar hacia el movimiento.
//   · El movimiento (WASD / stick izquierdo) nunca afecta la rotación.
// ============================================================
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
    [Tooltip("Número de eje joystick para stick derecho HORIZONTAL. Xbox XInput Windows: 4.")]
    [SerializeField, Range(1, 10)] private int aimAxisX = 4;

    [Tooltip("Número de eje joystick para stick derecho VERTICAL. Xbox XInput Windows: 5.")]
    [SerializeField, Range(1, 10)] private int aimAxisY = 5;

    [Tooltip("Activa si al empujar el stick ARRIBA el personaje mira ABAJO.")]
    [SerializeField] private bool invertAimY = false;

    [Tooltip("Activa si al empujar el stick DERECHA el personaje mira IZQUIERDA.")]
    [SerializeField] private bool invertAimX = false;

    [Header("Control — Mando Xbox — General")]
    [SerializeField, Range(0.05f, 0.9f)] private float gamepadDeadzone = 0.25f;

    // ── Estado interno ─────────────────────────────────────────
    private Rigidbody2D      _rb;
    private AttackController _attackController;
    private Camera           _cam;
    private Animator         _animator;          // Animator del jugador

    // Hashes de parámetros (más eficiente que strings en Update)
    private static readonly int _hashIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int _hashIsDashing = Animator.StringToHash("IsDashing");

    private Vector2 _lastInputDir      = Vector2.up;   // para el dash
    private Vector2 _lastAimDir        = Vector2.up;   // última dirección de apuntado válida
    private bool    _usingGamepad      = false;
    private float   _dashCooldownTimer = 0f;
    private float   _dashActiveTimer   = 0f;
    private bool    _isDashing         = false;
    private float   _speedBonus        = 0f;

    public float BaseSpeed       => maxSpeed;
    public float CurrentMaxSpeed => maxSpeed * (1f + _speedBonus);

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        _rb               = GetComponent<Rigidbody2D>();
        _attackController = GetComponent<AttackController>();
        _cam              = Camera.main;
        _animator         = GetComponentInChildren<Animator>();

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
        UpdateAnimator();
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

    // ══════════════════════════════════════════════════════════
    //  API PÚBLICA
    // ══════════════════════════════════════════════════════════

    public void AddSpeedBonus(float percent)
    {
        _speedBonus += percent;
        Debug.Log($"[PlayerMovement2D] Velocidad +{percent * 100f:F0}%. Max: {CurrentMaxSpeed:F1}");
    }

    // ══════════════════════════════════════════════════════════
    //  DETECCIÓN DE DISPOSITIVO
    // ══════════════════════════════════════════════════════════

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

        // Cualquier movimiento de ratón devuelve al modo teclado/ratón
        if (Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f)
            _usingGamepad = false;
    }

    // ══════════════════════════════════════════════════════════
    //  APUNTADO / ROTACIÓN
    //  Regla: la rotación NUNCA depende del stick izquierdo ni
    //  de las teclas de movimiento.
    // ══════════════════════════════════════════════════════════

    private void HandleAim()
    {
        Vector2 newAimDir = Vector2.zero;

        if (_usingGamepad)
        {
            // ── Mando: solo stick DERECHO ──────────────────────
            float rx = ReadJoystickAxis(aimAxisX) * (invertAimX ? -1f : 1f);
            float ry = ReadJoystickAxis(aimAxisY) * (invertAimY ? -1f : 1f);

            if (Mathf.Abs(rx) > gamepadDeadzone || Mathf.Abs(ry) > gamepadDeadzone)
            {
                // Stick derecho activo → actualiza dirección
                newAimDir  = new Vector2(rx, ry).normalized;
                _lastAimDir = newAimDir;
            }
            // Si el stick derecho está en reposo → NO hacer nada.
            // El personaje mantiene _lastAimDir (la última dirección válida).
            // NO hay fallback al stick izquierdo.
        }
        else
        {
            // ── Teclado/Ratón: siempre hacia el cursor ─────────
            if (_cam != null)
            {
                Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                newAimDir  = ((Vector2)(mouseWorld - transform.position)).normalized;
                _lastAimDir = newAimDir;
            }
        }

        // Aplica la rotación usando _lastAimDir (que nunca es Vector2.zero)
        float angle = Mathf.Atan2(_lastAimDir.x, _lastAimDir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, -angle);
        _attackController?.SetFacingDirection(_lastAimDir);
    }

    // ══════════════════════════════════════════════════════════
    //  MOVIMIENTO — no afecta la rotación
    // ══════════════════════════════════════════════════════════

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

        // Guarda la dirección de movimiento solo para el dash
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

    // ══════════════════════════════════════════════════════════
    //  DASH
    // ══════════════════════════════════════════════════════════

    private void HandleDashInput()
    {
        bool dashPressed = Input.GetKeyDown(dashKey) || Input.GetKeyDown(gamepadDashKey);
        if (dashPressed && _dashCooldownTimer <= 0f)
        {
            _isDashing         = true;
            _dashActiveTimer   = dashDuration;
            _dashCooldownTimer = dashCooldown;
            _rb.linearVelocity = Vector2.zero;
            // El dash va en la dirección de MOVIMIENTO, no de apuntado
            _rb.AddForce(_lastInputDir * dashForce, ForceMode2D.Impulse);
            AudioManager.Instance?.PlayPlayerDash();
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

    // ── Animator ───────────────────────────────────────────────

    private void UpdateAnimator()
    {
        if (_animator == null) return;

        // IsWalking: hay input de movimiento Y no está dasheando
        float lx      = Input.GetAxisRaw(horizontalAxis);
        float ly      = Input.GetAxisRaw(verticalAxis);
        float dz      = _usingGamepad ? gamepadDeadzone : 0.01f;
        bool  isMoving = (Mathf.Abs(lx) > dz || Mathf.Abs(ly) > dz) && !_isDashing;

        _animator.SetBool(_hashIsWalking, isMoving);
        _animator.SetBool(_hashIsDashing, _isDashing);
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private float ReadJoystickAxis(int axisNumber)
    {
        try   { return Input.GetAxisRaw("joystick axis " + axisNumber); }
        catch { return 0f; }
    }
}
