// ============================================================
//  BullEnemyAI.cs
//  IA de enemigo tipo TORO con audio integrado.
//
//  ESTADOS:
//   ESCANEO     → Rota lentamente buscando al jugador con cono
//                 de visión frontal. Al detectarlo, pasa a CARGA.
//   EMBESTIDA   → Acelera en línea recta fija (no persigue).
//                 Al llegar al borde de la arena, pasa a ATURDIDO.
//   ATURDIDO    → Pausa de recuperación con flash de color,
//                 luego vuelve a ESCANEO.
//
//  AUDIO:
//   · PlayBullCharge() al iniciar embestida.
//   · PlayBullStun()   al chocar con el borde.
//
//  SETUP EN UNITY:
//   1. Prefab con: Rigidbody2D, Collider2D, SpriteRenderer,
//      HealthSystem y este script.
//   2. Asigna el CircleCollider2D de la arena al campo
//      "Arena Collider" (isTrigger = true).
//   3. Ajusta los parámetros en el Inspector.
// ============================================================
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
public class BullEnemyAI : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════

    [Header("── Referencias ──────────────────────────────────")]
    [Tooltip("Transform del jugador. Se busca por tag 'Player' si queda vacío.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("CircleCollider2D de la arena (isTrigger = true). Define el límite de la embestida.")]
    [SerializeField] private CircleCollider2D arenaCollider;

    // ──────────────────────────────────────────────────────────
    [Header("── Escaneo ──────────────────────────────────────")]

    [Tooltip("Velocidad de rotación durante el escaneo (grados/segundo).")]
    [SerializeField] private float scanRotationSpeed = 60f;

    [Tooltip("Dirección inicial: +1 = horario, -1 = antihorario.")]
    [SerializeField] private float scanDirection = 1f;

    [Tooltip("Cada cuántos segundos invierte la dirección de escaneo. 0 = nunca.")]
    [SerializeField] private float scanReverseInterval = 3f;

    [Tooltip("Ángulo de medio-cono frontal (grados). Ej: 25 → detecta en ±25° al frente.")]
    [SerializeField] private float detectionHalfAngle = 25f;

    [Tooltip("Distancia máxima de detección durante el escaneo.")]
    [SerializeField] private float detectionRange = 12f;

    [Tooltip("Tiempo mínimo en ESCANEO antes de poder detectar (evita embestidas inmediatas).")]
    [SerializeField] private float minScanTime = 0.5f;

    // ──────────────────────────────────────────────────────────
    [Header("── Embestida ────────────────────────────────────")]

    [Tooltip("Velocidad máxima de la embestida (unidades/segundo).")]
    [SerializeField] private float chargeMaxSpeed = 18f;

    [Tooltip("Aceleración de la embestida (unidades/segundo²).")]
    [SerializeField] private float chargeAcceleration = 30f;

    [Tooltip("Margen antes del borde donde empieza a frenar (unidades).")]
    [SerializeField] private float brakingMargin = 1.5f;

    // ──────────────────────────────────────────────────────────
    [Header("── Recuperación / Aturdimiento ───────────────────")]

    [Tooltip("Segundos de aturdimiento al chocar con el borde.")]
    [SerializeField] private float stunDuration = 1.2f;

    [Tooltip("Color del flash durante el aturdimiento.")]
    [SerializeField] private Color stunColor = new Color(1f, 0.6f, 0f);

    [Tooltip("Segundos que tarda el giro de 180° al salir del aturdimiento.")]
[SerializeField] private float turnDuration = 0.4f;

    // ──────────────────────────────────────────────────────────
    [Header("── Daño de contacto ──────────────────────────────")]

    [Tooltip("Daño al jugador si lo toca durante la embestida.")]
    [SerializeField] private float chargeDamage = 30f;

    [Tooltip("Radio de contacto para detectar al jugador.")]
    [SerializeField] private float damageRadius = 0.8f;

    // ══════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ══════════════════════════════════════════════════════════

    private enum State { Scanning, Charging, Stunned, Dead }
    private State _state = State.Scanning;

    private Rigidbody2D    _rb;
    private HealthSystem   _healthSystem;
    private SpriteRenderer _spriteRenderer;

    private Vector2 _arenaCenter;
    private float   _arenaRadius;

    private float   _reverseTimer  = 0f;
    private float   _minScanTimer  = 0f;
    private Vector2 _chargeDir;
    private float   _currentSpeed  = 0f;

    private Color     _originalColor;
    private Coroutine _stunCoroutine;

    // Anti-spam de daño de contacto
    private float _damageCooldown = 0f;
    private const float DmgInterval = 0.5f;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        _rb             = GetComponent<Rigidbody2D>();
        _healthSystem   = GetComponent<HealthSystem>();
        _spriteRenderer = GetComponent<SpriteRenderer>()
                       ?? GetComponentInChildren<SpriteRenderer>();

        _rb.gravityScale   = 0f;
        _rb.linearDamping  = 0f;
        _rb.angularDamping = 0f;
        _rb.constraints    = RigidbodyConstraints2D.FreezeRotation;

        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else Debug.LogWarning("[BullEnemyAI] No se encontró 'Player'.");
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
            Debug.LogWarning("[BullEnemyAI] Sin arenaCollider — límites desactivados.");
            _arenaRadius = float.MaxValue;
        }

        _minScanTimer = minScanTime;
        _healthSystem.OnDeath.AddListener(OnDeath);
    }

    private void Update()
    {
        if (_state == State.Dead || playerTransform == null) return;

        if (_damageCooldown > 0f) _damageCooldown -= Time.deltaTime;

        switch (_state)
        {
            case State.Scanning:  TickScanning();  break;
            case State.Charging:  TickCharging();  break;
        }
    }

    private void FixedUpdate()
    {
        if (_state == State.Dead) return;

        if (_state == State.Charging)
            FixedTickCharging();
        else
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 15f);
    }

    // ══════════════════════════════════════════════════════════
    //  ESCANEO
    // ══════════════════════════════════════════════════════════

    private void TickScanning()
    {
        if (_minScanTimer > 0f) _minScanTimer -= Time.deltaTime;

        // Inversión periódica
        if (scanReverseInterval > 0f)
        {
            _reverseTimer += Time.deltaTime;
            if (_reverseTimer >= scanReverseInterval)
            {
                _reverseTimer  = 0f;
                scanDirection  = -scanDirection;
            }
        }

        transform.Rotate(0f, 0f, -scanDirection * scanRotationSpeed * Time.deltaTime);

        if (_minScanTimer <= 0f && PlayerInCone())
            BeginCharge();
    }

    private bool PlayerInCone()
    {
        Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
        if (toPlayer.magnitude > detectionRange) return false;
        return Vector2.Angle(transform.up, toPlayer.normalized) <= detectionHalfAngle;
    }

    // ══════════════════════════════════════════════════════════
    //  EMBESTIDA
    // ══════════════════════════════════════════════════════════

    private void BeginCharge()
    {
        _state        = State.Charging;
        _currentSpeed = 0f;
        _chargeDir    = transform.up.normalized;

        AudioManager.Instance?.PlayBullCharge();
        Debug.Log($"[BullEnemyAI] {gameObject.name} → EMBESTIDA");
    }

    private void TickCharging()
    {
        if (DistanceToBorder() <= brakingMargin * 0.25f)
        {
            BeginStun();
            return;
        }
        TryDamagePlayer();
    }

    private void FixedTickCharging()
    {
        if (DistanceToBorder() <= brakingMargin * 0.25f) return;

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, chargeMaxSpeed,
                                           chargeAcceleration * Time.fixedDeltaTime);

        // Frenado suave al acercarse al borde
        float d = DistanceToBorder();
        if (d < brakingMargin)
            _currentSpeed *= Mathf.Lerp(0.05f, 1f, d / brakingMargin);

        _rb.linearVelocity = _chargeDir * _currentSpeed;
    }

    // ══════════════════════════════════════════════════════════
    //  ATURDIMIENTO
    // ══════════════════════════════════════════════════════════

    private void BeginStun()
    {
        _state = State.Stunned;
        _rb.linearVelocity = Vector2.zero;
        _currentSpeed = 0f;

        AudioManager.Instance?.PlayBullStun();
        Debug.Log($"[BullEnemyAI] {gameObject.name} → ATURDIDO ({stunDuration}s)");

        if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
        _stunCoroutine = StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        if (_spriteRenderer != null) _spriteRenderer.color = stunColor;

        yield return new WaitForSeconds(stunDuration);

        if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

        // Giro animado de 180° antes de volver a escanear
        yield return StartCoroutine(SmoothTurn180());

        _minScanTimer = minScanTime;
        _reverseTimer = 0f;
        _state = State.Scanning;
        _stunCoroutine = null;
        Debug.Log($"[BullEnemyAI] {gameObject.name} → ESCANEO");
    }

    private IEnumerator SmoothTurn180()
    {
        float startAngle = transform.eulerAngles.z;
        float targetAngle = startAngle + 180f;
        float elapsed = 0f;

        // Puedes exponer este valor en el Inspector si quieres ajustarlo
        //float turnDuration = 0.4f;

        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / turnDuration);
            float angle = Mathf.LerpAngle(startAngle, targetAngle, t);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        // Asegura que quede exactamente en 180°
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
    }

    // ══════════════════════════════════════════════════════════
    //  DAÑO DE CONTACTO
    // ══════════════════════════════════════════════════════════

    private void TryDamagePlayer()
    {
        if (_damageCooldown > 0f) return;
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist > damageRadius) return;

        HealthSystem ph = playerTransform.GetComponent<HealthSystem>();
        if (ph == null) return;

        Vector2 hitDir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        if (ph.TakeDamage(chargeDamage, hitDir))
        {
            _damageCooldown = DmgInterval;
            Debug.Log($"[BullEnemyAI] Golpeó jugador: {chargeDamage} dmg.");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DISTANCIA AL BORDE
    // ══════════════════════════════════════════════════════════

    private float DistanceToBorder()
    {
        if (_arenaRadius == float.MaxValue) return float.MaxValue;
        return _arenaRadius - Vector2.Distance(transform.position, _arenaCenter);
    }

    // ══════════════════════════════════════════════════════════
    //  MUERTE
    // ══════════════════════════════════════════════════════════

    private void OnDeath()
    {
        _state = State.Dead;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
        if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

        Debug.Log($"[BullEnemyAI] {gameObject.name} ha muerto.");
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
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Cono de visión
        Vector2 fwd = Application.isPlaying ? (Vector2)transform.up : Vector2.up;
        float rad = detectionHalfAngle * Mathf.Deg2Rad;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawRay(transform.position, (Vector3)RotateVec(fwd,  rad) * detectionRange);
        Gizmos.DrawRay(transform.position, (Vector3)RotateVec(fwd, -rad) * detectionRange);
        Gizmos.DrawRay(transform.position, (Vector3)fwd * detectionRange);

        // Radio de daño
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);

        // Zona de frenado
        if (arenaCollider != null)
        {
            Vector2 c = (Vector2)arenaCollider.transform.position + arenaCollider.offset;
            float r = arenaCollider.radius * Mathf.Max(
                arenaCollider.transform.lossyScale.x,
                arenaCollider.transform.lossyScale.y);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawWireSphere(c, r - brakingMargin);
        }
    }

    private static Vector2 RotateVec(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
}
