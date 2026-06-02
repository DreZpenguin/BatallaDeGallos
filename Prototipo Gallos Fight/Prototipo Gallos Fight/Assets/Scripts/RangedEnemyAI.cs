using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
public class RangedEnemyAI : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════

    [Header("── Referencias ──────────────────────────────────")]
    [Tooltip("Transform del jugador. Se busca por tag 'Player' si queda vacío.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("CircleCollider2D de la arena (IsTrigger = true).")]
    [SerializeField] private CircleCollider2D arenaCollider;

    [Tooltip("Punto de origen del disparo. Vacío = centro del enemigo.")]
    [SerializeField] private Transform firePoint;

    // ──────────────────────────────────────────────────────────
    [Header("── Proyectil ────────────────────────────────────")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletDamage   = 10f;
    [SerializeField] private float bulletSpeed    = 10f;
    [SerializeField] private float bulletLifetime = 4f;

    [Tooltip("Segundos entre disparos.")]
    [SerializeField] private float shootCooldown = 1.5f;

    // ──────────────────────────────────────────────────────────
    [Header("── Detección y huida ───────────────────────────")]
    [Tooltip("Distancia a la que detecta al jugador y comienza a actuar.")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Si el jugador entra a esta distancia, el enemigo huye.")]
    [SerializeField] private float fleeRange = 4f;

    [Tooltip("Velocidad de huida.")]
    [SerializeField] private float fleeSpeed = 4f;

    // ──────────────────────────────────────────────────────────
    [Header("── Rodeo de obstáculos ──────────────────────────")]
    [Tooltip("Layer de los obstáculos que bloquean la línea de visión. " +
             "Crea una Layer 'Obstacles' y asígnala aquí.")]
    [SerializeField] private LayerMask obstacleLayer;

    [Tooltip("Velocidad de desplazamiento lateral al rodear un obstáculo.")]
    [SerializeField] private float strafeSpeed = 3.5f;

    [Tooltip("Segundos máximos strafando en una dirección antes de invertirla. " +
             "Evita que el enemigo se quede rodeando indefinidamente por un lado.")]
    [SerializeField] private float strafeMaxTime = 2.5f;

    [Tooltip("Distancia del raycast de visión. Debe ser >= detectionRange.")]
    [SerializeField] private float visionRayLength = 12f;

    // ──────────────────────────────────────────────────────────
    [Header("── Rotación ─────────────────────────────────────")]
    [SerializeField] private float rotationSpeed = 8f;

    // ══════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ══════════════════════════════════════════════════════════

    private enum State { Idle, Active, Strafing, Dead }
    private State _state = State.Idle;

    private Rigidbody2D  _rb;
    private HealthSystem _healthSystem;

    private float   _shootTimer   = 0f;
    private Vector2 _arenaCenter;
    private float   _arenaRadius;

    // Rodeo
    private float _strafeDir      = 1f;   // +1 = derecha del enemigo, -1 = izquierda
    private float _strafeTimer    = 0f;   // tiempo acumulado strafando en esta dirección
    private bool  _hasLOS         = true; // ¿tiene línea de visión al jugador?

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

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
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else Debug.LogWarning("[RangedEnemyAI] No se encontró 'Player'.");
        }

        if (arenaCollider != null)
        {
            _arenaCenter = (Vector2)arenaCollider.transform.position + arenaCollider.offset;
            _arenaRadius = arenaCollider.radius * Mathf.Max(
                arenaCollider.transform.lossyScale.x,
                arenaCollider.transform.lossyScale.y);
        }
        else
        {
            Debug.LogWarning("[RangedEnemyAI] Sin arenaCollider.");
            _arenaRadius = float.MaxValue;
        }

        if (firePoint == null)
            firePoint = transform;

        _healthSystem.OnDeath.AddListener(OnDeath);
    }

    private void Update()
    {
        if (_state == State.Dead || playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        CheckLineOfSight();
        UpdateState(dist);
        HandleRotation();
        HandleShoot(dist);
    }

    private void FixedUpdate()
    {
        if (_state == State.Dead) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        switch (_state)
        {
            case State.Active:
                if (dist < fleeRange)
                    Flee();
                else
                    Brake();
                break;

            case State.Strafing:
                Strafe();
                break;

            default:
                Brake();
                break;
        }

        ClampToArena();
    }

    // ══════════════════════════════════════════════════════════
    //  LÍNEA DE VISIÓN
    // ══════════════════════════════════════════════════════════

    private void CheckLineOfSight()
    {
        if (playerTransform == null) return;

        Vector2 origin    = firePoint.position;
        Vector2 toPlayer  = (Vector2)playerTransform.position - origin;
        float   distance  = Mathf.Min(toPlayer.magnitude, visionRayLength);

        // Raycast solo contra la capa de obstáculos
        RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distance, obstacleLayer);
        _hasLOS = (hit.collider == null);
    }

    // ══════════════════════════════════════════════════════════
    //  MÁQUINA DE ESTADOS
    // ══════════════════════════════════════════════════════════

    private void UpdateState(float distance)
    {
        if (_state == State.Dead) return;

        bool inRange = distance <= detectionRange;

        if (!inRange)
        {
            // Fuera de rango → Idle
            TransitionTo(State.Idle);
            return;
        }

        if (!_hasLOS)
        {
            // En rango pero visión bloqueada → Strafing
            if (_state != State.Strafing)
                BeginStrafe();
            return;
        }

        // En rango y con visión → Active (cancela strafeo si lo había)
        if (_state == State.Strafing)
        {
            _strafeTimer = 0f;
            Debug.Log($"[RangedEnemyAI] {gameObject.name} → visión recuperada, volviendo a ACTIVE");
        }
        TransitionTo(State.Active);
    }

    private void TransitionTo(State next)
    {
        if (_state == next) return;
        _state = next;
        Debug.Log($"[RangedEnemyAI] {gameObject.name} → {_state}");
    }

    // ══════════════════════════════════════════════════════════
    //  RODEO (STRAFING)
    // ══════════════════════════════════════════════════════════

    private void BeginStrafe()
    {
        _state       = State.Strafing;
        _strafeTimer = 0f;

        // Elige la dirección que más aleje al enemigo del borde de la arena.
        // Calcula la posición que tendría en cada dirección y compara
        // cuál queda más cerca del centro.
        Vector2 toPlayer  = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Vector2 perpRight = new Vector2(toPlayer.y, -toPlayer.x);   // 90° derecha
        Vector2 perpLeft  = new Vector2(-toPlayer.y, toPlayer.x);   // 90° izquierda

        Vector2 posRight = (Vector2)transform.position + perpRight * strafeSpeed * 0.5f;
        Vector2 posLeft  = (Vector2)transform.position + perpLeft  * strafeSpeed * 0.5f;

        float distRight = Vector2.Distance(posRight, _arenaCenter);
        float distLeft  = Vector2.Distance(posLeft,  _arenaCenter);

        // Elige el lado que queda más hacia el centro (distancia menor al centro)
        _strafeDir = distRight <= distLeft ? 1f : -1f;

        Debug.Log($"[RangedEnemyAI] {gameObject.name} → STRAFING dir={(_strafeDir > 0 ? "derecha" : "izquierda")}");
    }

    private void Strafe()
    {
        // Acumula tiempo en esta dirección
        _strafeTimer += Time.fixedDeltaTime;

        // Si lleva demasiado tiempo sin recuperar visión, invierte dirección
        if (_strafeTimer >= strafeMaxTime)
        {
            _strafeDir   = -_strafeDir;
            _strafeTimer = 0f;
            Debug.Log($"[RangedEnemyAI] {gameObject.name} invirtió dirección de strafeo");
        }

        // Perpendicular a la dirección al jugador
        Vector2 toPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Vector2 strafeVec = new Vector2(toPlayer.y, -toPlayer.x) * _strafeDir;

        _rb.linearVelocity = strafeVec * strafeSpeed;
    }

    // ══════════════════════════════════════════════════════════
    //  MOVIMIENTO
    // ══════════════════════════════════════════════════════════

    private void Flee()
    {
        Vector2 away = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
        _rb.linearVelocity = away * fleeSpeed;
    }

    private void Brake()
    {
        _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 10f);
    }

    // ══════════════════════════════════════════════════════════
    //  CONTENCIÓN EN LA ARENA
    // ══════════════════════════════════════════════════════════

    private void ClampToArena()
    {
        if (_arenaRadius == float.MaxValue) return;

        Vector2 pos        = transform.position;
        Vector2 fromCenter = pos - _arenaCenter;
        float   dist       = fromCenter.magnitude;
        float   limit      = _arenaRadius - 0.3f;

        if (dist > limit)
        {
            _rb.MovePosition(_arenaCenter + fromCenter.normalized * limit);

            Vector2 outward    = fromCenter.normalized;
            float   outwardVel = Vector2.Dot(_rb.linearVelocity, outward);
            if (outwardVel > 0f)
                _rb.linearVelocity -= outward * outwardVel;

            // Si está intentando strafear hacia el borde, invierte dirección
            if (_state == State.Strafing)
            {
                _strafeDir   = -_strafeDir;
                _strafeTimer = 0f;
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ROTACIÓN
    // ══════════════════════════════════════════════════════════

    private void HandleRotation()
    {
        if (playerTransform == null) return;

        Vector2 dir        = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        float targetAngle  = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        float smoothAngle  = Mathf.LerpAngle(
            transform.rotation.eulerAngles.z,
            -targetAngle,
            rotationSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0f, 0f, smoothAngle);
    }

    // ══════════════════════════════════════════════════════════
    //  DISPARO
    // ══════════════════════════════════════════════════════════

    private void HandleShoot(float distance)
    {
        // Solo dispara si está activo Y tiene línea de visión limpia
        if (_state != State.Active && _state != State.Strafing) return;
        if (!_hasLOS) return;
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

    // ══════════════════════════════════════════════════════════
    //  MUERTE
    // ══════════════════════════════════════════════════════════

    private void OnDeath()
    {
        _state = State.Dead;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType       = RigidbodyType2D.Kinematic;
        Debug.Log($"[RangedEnemyAI] {gameObject.name} ha muerto.");
        gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════
    //  GIZMOS
    // ══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // Rango de detección
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.2f);
        Gizmos.DrawSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de huida
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.2f);
        Gizmos.DrawSphere(transform.position, fleeRange);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, fleeRange);

        // Línea de visión al jugador (solo en Play)
        if (Application.isPlaying && playerTransform != null)
        {
            Gizmos.color = _hasLOS ? new Color(0f, 1f, 0.3f, 0.9f)   // verde = despejado
                                   : new Color(1f, 0.1f, 0.1f, 0.9f); // rojo = bloqueado
            Gizmos.DrawLine(transform.position, playerTransform.position);

            // Muestra la dirección de strafeo actual
            if (_state == State.Strafing)
            {
                Vector2 toPlayer  = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
                Vector2 strafeVec = new Vector2(toPlayer.y, -toPlayer.x) * _strafeDir;
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, strafeVec * 2f);
            }
        }
    }
    public void ApplyScaling(float damageMult, float bulletSpeedMult,
                         float cooldownMult, float speedMult)
    {
        bulletDamage *= damageMult;
        bulletSpeed *= bulletSpeedMult;
        shootCooldown *= cooldownMult;   // cooldownMult < 1 → dispara más rápido
        fleeSpeed *= speedMult;
    }
    public void SetArena(CircleCollider2D arena)
    {
        arenaCollider = arena;
        if (arenaCollider != null)
        {
            // Recalcula centro y radio (mismo cálculo que en Start)
            _arenaCenter = (Vector2)arenaCollider.transform.position + arenaCollider.offset;
            _arenaRadius = arenaCollider.radius * Mathf.Max(
                arenaCollider.transform.lossyScale.x,
                arenaCollider.transform.lossyScale.y);
        }
    }
}
