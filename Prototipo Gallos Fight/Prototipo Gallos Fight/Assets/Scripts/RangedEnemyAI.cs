// ============================================================
//  RangedEnemyAI.cs  — NUEVO
//
//  IA de enemigo a distancia. Comportamiento:
//   · Dispara proyectiles al jugador con un cooldown configurable.
//   · Si el jugador entra al rango de huida, se aleja de él.
//   · Si el jugador está fuera del rango de huida pero dentro de
//     detección, el enemigo se mantiene quieto (puede disparar igualmente).
//   · Se mantiene dentro del CircleCollider2D de la arena usando
//     el trigger del collider de la arena como límite de posición.
//
//  SETUP EN UNITY:
//   1. Crea un prefab de enemigo con: Rigidbody2D, Collider2D, SpriteRenderer,
//      HealthSystem, y este script.
//   2. Asigna el prefab de bala (mismo que usa el jugador sirve).
//   3. Arrastra el GameObject de la arena (el que tiene el CircleCollider2D
//      con IsTrigger = true) al campo "Arena Collider".
//   4. Si quieres un firePoint personalizado, crea un hijo vacío y asígnalo;
//      si lo dejas vacío se usa el centro del enemigo.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
public class RangedEnemyAI : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────

    [Header("Referencias")]
    [Tooltip("Transform del jugador. Se busca por tag 'Player' si queda vacío.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("CircleCollider2D de la arena (IsTrigger = true). Limita el movimiento del enemigo.")]
    [SerializeField] private CircleCollider2D arenaCollider;

    [Tooltip("Punto de origen del disparo. Deja vacío para usar el centro del enemigo.")]
    [SerializeField] private Transform firePoint;

    [Header("Prefab de proyectil")]
    [Tooltip("Prefab de bala con BulletController.")]
    [SerializeField] private GameObject bulletPrefab;

    [Header("Estadísticas del proyectil")]
    [SerializeField] private float bulletDamage   = 10f;
    [SerializeField] private float bulletSpeed    = 10f;
    [SerializeField] private float bulletLifetime = 4f;

    [Header("Cooldown de disparo")]
    [Tooltip("Segundos entre cada disparo.")]
    [SerializeField] private float shootCooldown = 1.5f;

    [Header("Detección y huida")]
    [Tooltip("Distancia a la que el enemigo detecta al jugador y empieza a disparar.")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Si el jugador entra a esta distancia, el enemigo huye.")]
    [SerializeField] private float fleeRange = 4f;

    [Tooltip("Velocidad de movimiento al huir del jugador.")]
    [SerializeField] private float fleeSpeed = 4f;

    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 8f;

    // ── Estado interno ─────────────────────────────────────────
    private enum State { Idle, Active, Dead }
    private State _state = State.Idle;

    private Rigidbody2D  _rb;
    private HealthSystem _healthSystem;
    private float        _shootTimer = 0f;

    // Centro y radio de la arena (calculados en Start)
    private Vector2 _arenaCenter;
    private float   _arenaRadius;

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        _rb           = GetComponent<Rigidbody2D>();
        _healthSystem = GetComponent<HealthSystem>();

        _rb.gravityScale   = 0f;
        _rb.linearDamping  = 6f;
        _rb.angularDamping = 0f;
        _rb.constraints    = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        // Busca al jugador por tag si no está asignado
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("[RangedEnemyAI] No se encontró GameObject con tag 'Player'.");
        }

        // Lee el centro y radio de la arena
        if (arenaCollider != null)
        {
            _arenaCenter = (Vector2)arenaCollider.transform.position + arenaCollider.offset;
            _arenaRadius = arenaCollider.radius * Mathf.Max(
                arenaCollider.transform.lossyScale.x,
                arenaCollider.transform.lossyScale.y);
        }
        else
        {
            Debug.LogWarning("[RangedEnemyAI] No se asignó arenaCollider. El enemigo no tendrá límites de movimiento.");
            _arenaRadius = float.MaxValue;
        }

        // FirePoint por defecto
        if (firePoint == null)
            firePoint = transform;

        // Suscribirse a la muerte
        _healthSystem.OnDeath.AddListener(OnDeath);
    }

    private void Update()
    {
        if (_state == State.Dead) return;
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        UpdateState(dist);
        HandleRotation();
        HandleShoot(dist);
    }

    private void FixedUpdate()
    {
        if (_state != State.Active) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist < fleeRange)
            Flee();
        else
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 10f);

        ClampToArena();
    }

    // ── Máquina de estados ─────────────────────────────────────

    private void UpdateState(float distance)
    {
        State next = distance <= detectionRange ? State.Active : State.Idle;

        if (next != _state)
        {
            _state = next;
            Debug.Log($"[RangedEnemyAI] {gameObject.name} → {_state}");
        }
    }

    // ── Movimiento de huida ────────────────────────────────────

    private void Flee()
    {
        // Dirección OPUESTA al jugador
        Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
        _rb.linearVelocity = awayFromPlayer * fleeSpeed;
    }

    // ── Contención dentro de la arena ─────────────────────────

    private void ClampToArena()
    {
        if (_arenaRadius == float.MaxValue) return;

        Vector2 pos     = transform.position;
        Vector2 fromCenter = pos - _arenaCenter;
        float   dist    = fromCenter.magnitude;

        // Margen para que el enemigo no se quede pegado al borde
        float margin = 0.3f;
        float limit  = _arenaRadius - margin;

        if (dist > limit)
        {
            // Empuja al enemigo de vuelta hacia el centro
            Vector2 clampedPos = _arenaCenter + fromCenter.normalized * limit;
            _rb.MovePosition(clampedPos);

            // Cancela la componente de velocidad que apunta hacia afuera
            Vector2 outward = fromCenter.normalized;
            float   outwardVel = Vector2.Dot(_rb.linearVelocity, outward);
            if (outwardVel > 0f)
                _rb.linearVelocity -= outward * outwardVel;
        }
    }

    // ── Rotación ───────────────────────────────────────────────

    private void HandleRotation()
    {
        if (playerTransform == null) return;

        Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        float targetAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        float smoothAngle = Mathf.LerpAngle(
            transform.rotation.eulerAngles.z,
            -targetAngle,
            rotationSpeed * Time.deltaTime
        );
        transform.rotation = Quaternion.Euler(0f, 0f, smoothAngle);
    }

    // ── Disparo ────────────────────────────────────────────────

    private void HandleShoot(float distance)
    {
        if (_state != State.Active) return;
        if (bulletPrefab == null) return;

        _shootTimer -= Time.deltaTime;
        if (_shootTimer > 0f) return;

        _shootTimer = shootCooldown;
        FireBullet();
    }

    private void FireBullet()
    {
        Vector2 direction = ((Vector2)playerTransform.position - (Vector2)firePoint.position).normalized;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        BulletController bc = bullet.GetComponent<BulletController>();

        if (bc != null)
            bc.Init(bulletDamage, bulletSpeed, bulletLifetime, direction, gameObject);
        else
            Debug.LogError("[RangedEnemyAI] El prefab de bala no tiene BulletController.");
    }

    // ── Muerte ─────────────────────────────────────────────────

    private void OnDeath()
    {
        _state = State.Dead;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType       = RigidbodyType2D.Kinematic;
        Debug.Log($"[RangedEnemyAI] {gameObject.name} ha muerto.");
        gameObject.SetActive(false);
    }

    // ── Debug Visual ───────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Rango de detección (amarillo)
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Gizmos.DrawSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de huida (naranja)
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.25f);
        Gizmos.DrawSphere(transform.position, fleeRange);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, fleeRange);
    }
}
