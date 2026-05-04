// ============================================================
//  ShootingController.cs  — v4
//  Cambios respecto a v3:
//   · Llama a AudioManager.Instance.PlayPlayerShoot() al disparar.
// ============================================================
using UnityEngine;

public class ShootingController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse1;

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

    private float _cooldownTimer = 0f;
    private int   _upgradeStacks = 0;

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
        Debug.Log($"[ShootingController] Listo. Tecla: {shootKey} | " +
                  $"Prefab: {(bulletPrefab != null ? bulletPrefab.name : "NULL")} | " +
                  $"FirePoint: {firePoint.name}");
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(shootKey))
        {
            if (_cooldownTimer > 0f)
                Debug.Log($"[ShootingController] Cooldown activo ({_cooldownTimer:F2}s restantes).");
            else
                Shoot();
        }
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

        // Sonido de disparo
        AudioManager.Instance?.PlayPlayerShoot();

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction  = ((Vector2)mouseWorld - (Vector2)firePoint.position).normalized;

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

    private void OnGUI()
    {
        if (!gameObject.CompareTag("Player")) return;
        GUI.Label(new Rect(10, 165, 280, 60),
            $"Disparo [{shootKey}] — CD: {Mathf.Max(0f, _cooldownTimer):F1}s\n" +
            $"Bala: Dmg {CurrentDamage:F1} | Spd {CurrentSpeed:F1} | Life {CurrentLifetime:F1}s");
    }
}
