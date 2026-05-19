// ============================================================
//  ShootingController.cs  — v5
//  Cambios respecto a v4:
//   · Soporte para mando Xbox: disparo con gatillo derecho (RT)
//     o botón configurable desde el Inspector.
// ============================================================
using UnityEngine;

public class ShootingController : MonoBehaviour
{
    // ── Control — Teclado/Ratón ────────────────────────────────
    [Header("Control — Teclado / Ratón")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse1;

    // ── Control — Mando Xbox ───────────────────────────────────
    [Header("Control — Mando Xbox")]
    [Tooltip("Botón del mando para disparar (RB = joystick button 5 | RT como botón = joystick button 9 en algunos mapeos).")]
    [SerializeField] private KeyCode gamepadShootKey = KeyCode.JoystickButton5;

    [Tooltip("Eje del gatillo derecho (RT) para disparar. Configura en Input Manager como 'RT'. " +
             "Deja vacío si prefieres usar solo el botón gamepadShootKey.")]
    [SerializeField] private string gamepadTriggerAxis = "RT";

    [Tooltip("Umbral del gatillo a partir del cual se dispara (0 = cualquier presión, 0.5 = mitad del recorrido).")]
    [SerializeField, Range(0f, 0.99f)] private float triggerThreshold = 0.3f;

    [Header("Prefab de Bala")]
    [Tooltip("Prefab raíz con BulletController + Rigidbody2D + Collider2D en el mismo objeto.")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Punto de origen del disparo. Vacío = centro del jugador.")]
    [SerializeField] private Transform firePoint;

    [Header("Estadísticas base de la bala")]
    [SerializeField] private float baseDamage   = 10f;
    [SerializeField] private float baseSpeed    = 12f;
    [SerializeField] private float baseLifetime = 3f;

    [Header("Cooldown")]
    [SerializeField] private float shootCooldown = 0.5f;

    private float _cooldownTimer  = 0f;
    private int   _upgradeStacks  = 0;
    private bool  _triggerWasDown = false;   // evita disparo continuo con el gatillo

    private Camera _cam;

    private const float DamagePerStack   = 5f;
    private const float SpeedPerStack    = 2f;
    private const float LifetimePerStack = 0.5f;

    public float CurrentDamage   => baseDamage   + _upgradeStacks * DamagePerStack;
    public float CurrentSpeed    => baseSpeed    + _upgradeStacks * SpeedPerStack;
    public float CurrentLifetime => baseLifetime + _upgradeStacks * LifetimePerStack;

    private void Awake()
    {
        _cam = Camera.main;
        if (firePoint == null)
            firePoint = transform;

        if (bulletPrefab == null)
        {
            Debug.LogError("[ShootingController] ¡bulletPrefab no está asignado en el Inspector!");
            return;
        }
        if (bulletPrefab.GetComponent<BulletController>() == null)
            Debug.LogError("[ShootingController] El prefab NO tiene BulletController en el objeto raíz.");
        if (bulletPrefab.GetComponent<Rigidbody2D>() == null)
            Debug.LogError("[ShootingController] El prefab NO tiene Rigidbody2D en el objeto raíz.");
        if (bulletPrefab.GetComponent<Collider2D>() == null)
            Debug.LogError("[ShootingController] El prefab NO tiene Collider2D en el objeto raíz.");
    }

    private void Start()
    {
        Debug.Log($"[ShootingController] Listo. Tecla: {shootKey} | Mando: {gamepadShootKey} / eje '{gamepadTriggerAxis}' | " +
                  $"Prefab: {(bulletPrefab != null ? bulletPrefab.name : "NULL")} | " +
                  $"FirePoint: {firePoint.name}");
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        // ── Input de teclado/ratón ────────────────────────────
        if (Input.GetKeyDown(shootKey))
        {
            TryShoot();
            return;
        }

        // ── Input de mando: botón ─────────────────────────────
        if (Input.GetKeyDown(gamepadShootKey))
        {
            TryShoot();
            return;
        }

        // ── Input de mando: gatillo (eje analógico) ───────────
        float triggerValue = GetTriggerAxis();
        bool  triggerDown  = triggerValue >= triggerThreshold;

        // Dispara solo en el flanco de bajada del gatillo (no continuo)
        if (triggerDown && !_triggerWasDown)
            TryShoot();

        _triggerWasDown = triggerDown;
    }

    // ── Helpers ────────────────────────────────────────────────

    private void TryShoot()
    {
        if (_cooldownTimer > 0f)
        {
            Debug.Log($"[ShootingController] Cooldown activo ({_cooldownTimer:F2}s restantes).");
            return;
        }
        Shoot();
    }

    private float GetTriggerAxis()
    {
        if (string.IsNullOrEmpty(gamepadTriggerAxis)) return 0f;
        try   { return Input.GetAxis(gamepadTriggerAxis); }
        catch { return 0f; }
    }

    // ── Disparo ───────────────────────────────────────────────

    private void Shoot()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("[ShootingController] No hay prefab de bala asignado.");
            return;
        }

        _cooldownTimer = shootCooldown;
        AudioManager.Instance?.PlayPlayerShoot();

        // Dirección: stick derecho (mando) o ratón (teclado)
        Vector2 direction = GetShootDirection();

        Debug.Log($"[ShootingController] Disparando hacia {direction}");

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        BulletController bc = bullet.GetComponent<BulletController>();

        if (bc != null)
            bc.Init(CurrentDamage, CurrentSpeed, CurrentLifetime, direction, gameObject);
        else
        {
            Debug.LogError("[ShootingController] La instancia no tiene BulletController.");
            Destroy(bullet);
        }
    }

    /// Obtiene la dirección de disparo desde la rotación actual del jugador
    /// (que ya fue calculada por PlayerMovement2D usando ratón o stick derecho).
    private Vector2 GetShootDirection()
    {
        // La rotación del jugador siempre apunta en la dirección correcta
        // (PlayerMovement2D la actualiza tanto para ratón como para stick derecho)
        return transform.up;   // "arriba" local = dirección de la cara del sprite
    }

    // ── API Pública ───────────────────────────────────────────

    public void SetBulletUpgradeStacks(int totalStacks)
    {
        _upgradeStacks = totalStacks;
        Debug.Log($"[ShootingController] Stacks de bala → {_upgradeStacks}. " +
                  $"Dmg:{CurrentDamage:F1} Spd:{CurrentSpeed:F1} Life:{CurrentLifetime:F1}s");
    }

    public void AddBulletStack()
    {
        _upgradeStacks++;
        Debug.Log($"[ShootingController] +1 stack de bala. Total: {_upgradeStacks}. " +
                  $"Dmg:{CurrentDamage:F1} Spd:{CurrentSpeed:F1} Life:{CurrentLifetime:F1}s");
    }

    public void SetShootKey(KeyCode key)
    {
        shootKey = key;
        Debug.Log($"[ShootingController] Tecla de disparo → {key}");
    }

    //private void OnGUI()
    //{
    //    if (!gameObject.CompareTag("Player")) return;
    //    GUI.Label(new Rect(10, 165, 300, 60),
    //        $"Disparo [KB:{shootKey} | PAD:{gamepadShootKey}/RT] — CD: {Mathf.Max(0f, _cooldownTimer):F1}s\n" +
    //        $"Bala: Dmg {CurrentDamage:F1} | Spd {CurrentSpeed:F1} | Life {CurrentLifetime:F1}s");
    //}
}
