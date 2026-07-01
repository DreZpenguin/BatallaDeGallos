
using UnityEngine;

public class ShootingController : MonoBehaviour
{
    // ── Control — Teclado/Ratón ────────────────────────────────
    [Header("Control — Teclado / Ratón")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse1;

    // ── Control — Mando Xbox ───────────────────────────────────
    [Header("Control — Mando Xbox")]
    [Tooltip("Botón del mando para disparar.")]
    [SerializeField] private KeyCode gamepadShootKey = KeyCode.JoystickButton5;

    [Tooltip("Eje del gatillo derecho (RT). Deja vacío para usar solo botón.")]
    [SerializeField] private string gamepadTriggerAxis = "RT";

    [Tooltip("Umbral del gatillo para disparar.")]
    [SerializeField, Range(0f, 0.99f)] private float triggerThreshold = 0.3f;

    [Header("Prefab de Bala")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Punto de origen del disparo. Vacío = centro del jugador.")]
    [SerializeField] private Transform firePoint;

    [Header("Estadísticas base de la bala")]
    [SerializeField] private float baseDamage   = 10f;
    [SerializeField] private float baseSpeed    = 12f;
    [SerializeField] private float baseLifetime = 3f;

    [Header("Bonus por stack de daño (PowerUp Disparo)")]
    [Tooltip("Daño adicional por cada stack de +Disparo.")]
    [SerializeField] private float damagePerStack = 5f;

    [Header("Bonus por stack de velocidad/knockback (PowerUp Rango)")]
    [Tooltip("Velocidad adicional por cada stack de +Rango.")]
    [SerializeField] private float speedPerStack     = 2f;
    [Tooltip("Multiplicador de knockback adicional por cada stack de +Rango. " +
             "Se suma al multiplicador base (1.0). Ej: 0.5 → cada stack añade ×0.5 de knockback extra.")]
    [SerializeField] private float knockbackPerStack = 0.5f;

    [Header("Cooldown")]
    [SerializeField] private float shootCooldown = 0.5f;

    // ── Bonuses acumulados ─────────────────────────────────────
    private float _damageBonus    = 0f;   // daño extra plano
    private float _speedBonus     = 0f;   // velocidad extra plana
    private float _knockbackMult  = 1f;   // multiplicador de knockback (empieza en 1)

    private float _cooldownTimer  = 0f;
    private bool  _triggerWasDown = false;

    // ── Propiedades públicas ───────────────────────────────────
    public float CurrentDamage    => baseDamage   + _damageBonus;
    public float CurrentSpeed     => baseSpeed    + _speedBonus;
    public float CurrentLifetime  => baseLifetime;
    public float CurrentKnockback => _knockbackMult;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (firePoint == null) firePoint = transform;

        if (bulletPrefab == null)
        {
            Debug.LogError("[ShootingController] bulletPrefab no asignado.");
            return;
        }
        if (bulletPrefab.GetComponent<BulletController>() == null)
            Debug.LogError("[ShootingController] El prefab NO tiene BulletController.");
        if (bulletPrefab.GetComponent<Rigidbody2D>() == null)
            Debug.LogError("[ShootingController] El prefab NO tiene Rigidbody2D.");
        if (bulletPrefab.GetComponent<Collider2D>() == null)
            Debug.LogError("[ShootingController] El prefab NO tiene Collider2D.");
    }

    private void Start()
    {
        Debug.Log($"[ShootingController] Listo. Tecla:{shootKey} | Mando:{gamepadShootKey}/RT | " +
                  $"Prefab:{(bulletPrefab != null ? bulletPrefab.name : "NULL")}");
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(shootKey))      { TryShoot(); return; }
        if (Input.GetKeyDown(gamepadShootKey)){ TryShoot(); return; }

        float triggerValue = GetTriggerAxis();
        bool  triggerDown  = triggerValue >= triggerThreshold;
        if (triggerDown && !_triggerWasDown) TryShoot();
        _triggerWasDown = triggerDown;
    }

    // ══════════════════════════════════════════════════════════
    //  DISPARO
    // ══════════════════════════════════════════════════════════

    private void TryShoot()
    {
        if (_cooldownTimer > 0f) return;
        Shoot();
    }

    private void Shoot()
    {
        if (bulletPrefab == null) return;

        _cooldownTimer = shootCooldown;
        AudioManager.Instance?.PlayPlayerShoot();

        Vector2    dir    = transform.up;
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        BulletController bc = bullet.GetComponent<BulletController>();

        if (bc != null)
            bc.Init(CurrentDamage, CurrentSpeed, CurrentLifetime,
                    dir, gameObject, _knockbackMult);
        else
        {
            Debug.LogError("[ShootingController] La instancia no tiene BulletController.");
            Destroy(bullet);
        }
    }

    private float GetTriggerAxis()
    {
        if (string.IsNullOrEmpty(gamepadTriggerAxis)) return 0f;
        try   { return Input.GetAxis(gamepadTriggerAxis); }
        catch { return 0f; }
    }

    // ══════════════════════════════════════════════════════════
    //  API PÚBLICA — bonuses independientes
    // ══════════════════════════════════════════════════════════

    /// PowerUpType.Shoot → solo aumenta el daño de la bala.
    public void AddBulletDamageBonus(float flatAmount)
    {
        _damageBonus += flatAmount;
        Debug.Log($"[ShootingController] +Daño bala: {flatAmount:F1}. Total: {CurrentDamage:F1}");
    }

    /// PowerUpType.Range → aumenta velocidad de la bala.
    public void AddBulletSpeedBonus(float flatAmount)
    {
        _speedBonus += flatAmount;
        Debug.Log($"[ShootingController] +Velocidad bala: {flatAmount:F1}. Total: {CurrentSpeed:F1}");
    }

    /// PowerUpType.Range → aumenta el multiplicador de knockback.
    public void AddBulletKnockbackBonus(float multiplierAmount)
    {
        _knockbackMult += multiplierAmount;
        Debug.Log($"[ShootingController] +Knockback bala: ×{_knockbackMult:F2}");
    }

    /// Aplica N stacks de velocidad+knockback de una vez (usado al cargar PlayerData).
    public void SetBulletRangeStacks(int totalStacks)
    {
        _speedBonus    = speedPerStack    * totalStacks;
        _knockbackMult = 1f + knockbackPerStack * totalStacks;
        Debug.Log($"[ShootingController] Rango restaurado. " +
                  $"Spd:{CurrentSpeed:F1} Knock:×{_knockbackMult:F2}");
    }

    /// Aplica N stacks de daño de una vez (usado al cargar PlayerData).
    public void SetBulletDamageStacks(int totalStacks)
    {
        _damageBonus = damagePerStack * totalStacks;
        Debug.Log($"[ShootingController] Daño restaurado. Dmg:{CurrentDamage:F1}");
    }

    /// Retrocompatibilidad con código que usaba SetBulletUpgradeStacks.
    /// Convierte los stacks antiguos a bonus de daño.
    public void SetBulletUpgradeStacks(int totalStacks)
    {
        SetBulletDamageStacks(totalStacks);
    }

    /// Retrocompatibilidad con código que usaba AddBulletStack.
    public void AddBulletStack()
    {
        AddBulletDamageBonus(damagePerStack);
    }

    public void SetShootKey(KeyCode key) => shootKey = key;
}
