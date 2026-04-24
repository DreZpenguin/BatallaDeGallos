using UnityEngine;

public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeed = 8f;        // <- Velocidad BASE (no modificar en runtime)
    [SerializeField] private float acceleration = 60f;
    [SerializeField, Range(0f, 1f)] private float friction = 0.85f;

    [Header("Dash")]
    [SerializeField] private float dashForce = 18f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashDuration = 0.15f;

    private Rigidbody2D _rb;
    private AttackController _attackController;
    private Camera _cam;

    private Vector2 _lastInputDir = Vector2.right;
    private float _dashCooldownTimer = 0f;
    private float _dashActiveTimer = 0f;
    private bool _isDashing = false;

    // Bonus acumulado de velocidad (sumatorio de porcentajes, ej: 0.3 = +30%)
    private float _speedBonus = 0f;

    // Propiedades públicas
    public float BaseSpeed => maxSpeed;
    public float CurrentMaxSpeed => maxSpeed * (1f + _speedBonus);

    // Unity Lifecycle 

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _attackController = GetComponent<AttackController>();
        _cam = Camera.main;

        _rb.gravityScale = 0f;
        _rb.linearDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        RotateTowardsMouse();
        HandleDashInput();
        HandleDashTimer();
        if (Input.GetKeyDown(KeyCode.R)) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        if (Input.GetKeyDown(KeyCode.F1)) UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        if (Input.GetKeyDown(KeyCode.F2)) UnityEngine.SceneManagement.SceneManager.LoadScene(1);
        if (Input.GetKeyDown(KeyCode.F3)) UnityEngine.SceneManagement.SceneManager.LoadScene(2);
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

    // API Pública


    /// Añade un porcentaje de bonus a la velocidad máxima base.
    /// Ejemplo: AddSpeedBonus(0.30f) → +30% de la velocidad base.

    public void AddSpeedBonus(float percent)
    {
        _speedBonus += percent;
        Debug.Log($"[PlayerMovement2D] Velocidad aumentada. Bonus total: {_speedBonus * 100f:F0}% | Velocidad actual: {CurrentMaxSpeed:F1}");
    }

    // Rotación

    private void RotateTowardsMouse()
    {
        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorld - transform.position).normalized;

        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, -angle);

        _attackController?.SetFacingDirection(direction);
    }

    //  Movimiento

    private void ApplyMovement()
    {
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (input != Vector2.zero)
            _lastInputDir = input;

        _rb.AddForce(input * acceleration, ForceMode2D.Force);
    }

    private void ApplyFriction()
    {
        Vector2 rawInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        if (rawInput == Vector2.zero)
            _rb.linearVelocity *= friction;
    }

    private void ClampVelocity()
    {
        // Usa CurrentMaxSpeed (base + bonus) para el clamp
        if (_rb.linearVelocity.magnitude > CurrentMaxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * CurrentMaxSpeed;
    }

    // Dash 

    private void HandleDashInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && _dashCooldownTimer <= 0f)
        {
            _isDashing = true;
            _dashActiveTimer = dashDuration;
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

    //Debug GUI 

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 260, 100),
            $"Velocidad: {_rb.linearVelocity.magnitude:F2} / {CurrentMaxSpeed:F1}\n" +
            $"Bonus velocidad: +{_speedBonus * 100f:F0}%\n" +
            $"Dash activo: {_isDashing}\n" +
            $"Cooldown dash: {Mathf.Max(0f, _dashCooldownTimer):F1}s");
    }
}
