// ============================================================
//  BullEnemyAI.cs  — v4
//
//  CAMBIOS respecto a v3:
//   · Embestida explosiva: se elimina la aceleración gradual.
//     El toro sale a chargeMaxSpeed en el primer FixedUpdate.
//     Se añade chargeImpulseDelay (frames de espera tras soltar
//     constraints) para asegurar que el motor de física procese
//     el cambio antes de aplicar velocidad.
//   · Camera shake al chocar con el borde (BeginStun).
//     Implementado como corrutina interna que mueve el Transform
//     de la cámara. No requiere paquetes externos.
//     Parámetros: shakeDuration, shakeMagnitude, shakeFrequency.
//   · Se mantiene toda la personalización de v3.
//
//  ESTADOS:
//   ESCANEO → PRE_CHARGE → EMBESTIDA → ATURDIDO → ESCANEO
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

    [Tooltip("CircleCollider2D de la arena (isTrigger = true).")]
    [SerializeField] private CircleCollider2D arenaCollider;

    // ──────────────────────────────────────────────────────────
    [Header("── Escaneo ──────────────────────────────────────")]

    [Tooltip("Velocidad de rotación durante el escaneo (grados/segundo).")]
    [SerializeField] private float scanRotationSpeed = 20f;

    [Tooltip("Dirección inicial: +1 = horario, -1 = antihorario.")]
    [SerializeField] private float scanDirection = 1f;

    [Tooltip("Cada cuántos segundos invierte la dirección. 0 = nunca.")]
    [SerializeField] private float scanReverseInterval = 3f;

    [Tooltip("Ángulo de medio-cono frontal (grados).")]
    [SerializeField] private float detectionHalfAngle = 25f;

    [Tooltip("Distancia máxima de detección.")]
    [SerializeField] private float detectionRange = 12f;

    [Tooltip("Tiempo mínimo en ESCANEO antes de poder detectar.")]
    [SerializeField] private float minScanTime = 0.5f;

    [Tooltip("Segundos sin ver al jugador para rotar hacia el centro. 0 = desactivado.")]
    [SerializeField] private float noVisionCenterTime = 3f;

    [Tooltip("Velocidad de rotación al corregir hacia el centro (grados/segundo).")]
    [SerializeField] private float centerCorrectionSpeed = 45f;

    // ──────────────────────────────────────────────────────────
    [Header("── Pre-Embestida (telegrafío) ───────────────────")]

    [Tooltip("Segundos de pausa antes de embestir. 0 = sin telegrafío.")]
    [SerializeField] private float preChargeDelay = 0.5f;

    [Tooltip("Amplitud del shake visual del toro durante el telegrafío.")]
    [SerializeField] private float telegraphShakeAmplitude = 0.08f;

    [Tooltip("Frecuencia del shake del toro (Hz).")]
    [SerializeField] private float telegraphShakeFrequency = 14f;

    // ──────────────────────────────────────────────────────────
    [Header("── Embestida ────────────────────────────────────")]

    [Tooltip("Velocidad de la embestida (unidades/segundo). " +
             "Se aplica instantáneamente — sin aceleración gradual.")]
    [SerializeField] private float chargeMaxSpeed = 22f;

    [Tooltip("Margen antes del borde donde empieza a frenar.")]
    [SerializeField] private float brakingMargin = 1.2f;

    [Tooltip("Multiplicador de frenado. 1 = frena normalmente, " +
             "valores menores = frena más agresivo cerca del borde.")]
    [SerializeField, Range(0.01f, 1f)] private float brakingStrength = 0.08f;

    [Tooltip("Distancia al borde que dispara el aturdimiento. Debe ser < brakingMargin.")]
    [SerializeField] private float borderStunThreshold = 0.25f;

    // ──────────────────────────────────────────────────────────
    [Header("── Aturdimiento ─────────────────────────────────")]

    [Tooltip("Segundos de aturdimiento al chocar con el borde.")]
    [SerializeField] private float stunDuration = 1.2f;

    [Tooltip("Color del flash durante el aturdimiento.")]
    [SerializeField] private Color stunColor = new Color(1f, 0.6f, 0f);

    [Tooltip("Segundos del giro de vuelta al centro.")]
    [SerializeField] private float turnDuration = 0.4f;

    // ──────────────────────────────────────────────────────────
    [Header("── Camera Shake (al chocar con el borde) ─────────")]

    [Tooltip("Cámara principal. Se busca automáticamente si queda vacío.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Duración del camera shake (segundos).")]
    [SerializeField] private float shakeDuration = 0.35f;

    [Tooltip("Intensidad máxima del shake (unidades de mundo).")]
    [SerializeField] private float shakeMagnitude = 0.28f;

    [Tooltip("Frecuencia del shake (Hz). Valores altos = vibración rápida.")]
    [SerializeField] private float shakeFrequency = 30f;

    [Tooltip("Curva de decaimiento del shake. Si queda vacía se usa decaimiento lineal.")]
    [SerializeField] private AnimationCurve shakeDecayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // ──────────────────────────────────────────────────────────
    [Header("── Inactividad inicial ──────────────────────────")]
    [Tooltip("Segundos que el toro permanece inactivo al iniciar la escena.")]
    [SerializeField] private float startupDelay = 1f;

    [Header("── Daño de contacto ──────────────────────────────")]

    [Tooltip("Hitbox hijo del toro (GameObject con BullHitbox.cs). " +
             "Se busca automáticamente si queda vacío.")]
    [SerializeField] private BullHitbox bullHitbox;

    [Tooltip("Daño al jugador durante la embestida.")]
    [SerializeField] private float chargeDamage   = 30f;

    [Tooltip("Cooldown entre golpes consecutivos (segundos). " +
             "Evita daño múltiple si el jugador queda dentro del trigger.")]
    [SerializeField] private float damageCooldownDuration = 0.5f;

    // ══════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ══════════════════════════════════════════════════════════

    private enum State { Scanning, PreCharge, Charging, Stunned, Dead }
    private State _state = State.Scanning;

    private Rigidbody2D    _rb;
    private HealthSystem   _healthSystem;
    private SpriteRenderer _spriteRenderer;

    private Vector2 _arenaCenter;
    private float   _arenaRadius;

    // Escaneo
    private float _reverseTimer  = 0f;
    private float _minScanTimer  = 0f;
    private float _noVisionTimer = 0f;

    // Embestida
    private Vector2 _chargeDir;
    private float   _currentSpeed     = 0f;
    private Vector2 _preChargeAnchor;
    private float   _distanceFromAnchor = 0f;
    private const float MinDistBeforeBorderCheck = 0.5f;

    // Aturdimiento / corrutinas
    private Color     _originalColor;
    private Coroutine _activeCoroutine;
    private Coroutine _shakeCoroutine;

    // Camera shake — posición original de la cámara
    private Vector3 _cameraOriginalPos;

    // Anti-spam de daño
    private float _damageCooldown = 0f;
    private float _startupTimer   = 0f;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        _rb             = GetComponent<Rigidbody2D>();
        _healthSystem   = GetComponent<HealthSystem>();
        _spriteRenderer = GetComponent<SpriteRenderer>()
                       ?? GetComponentInChildren<SpriteRenderer>();

        // Busca BullHitbox en hijos si no está asignada
        if (bullHitbox == null)
            bullHitbox = GetComponentInChildren<BullHitbox>();

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
            Debug.LogWarning("[BullEnemyAI] Sin arenaCollider.");
            _arenaRadius = float.MaxValue;
        }

        // Cámara
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera != null)
            _cameraOriginalPos = targetCamera.transform.localPosition;

        _minScanTimer  = minScanTime;
        _noVisionTimer = 0f;
        _healthSystem.OnDeath.AddListener(OnDeath);

        _startupTimer = startupDelay;
    }

    private void Update()
    {
        if (_state == State.Dead || playerTransform == null) return;

        if (_startupTimer > 0f)
        {
            _startupTimer -= Time.deltaTime;
            return;
        }

        if (_damageCooldown > 0f)
            _damageCooldown -= Time.deltaTime;

        switch (_state)
        {
            case State.Scanning: TickScanning(); break;
            case State.Charging: TickCharging(); break;
        }
    }

    private void FixedUpdate()
    {
        if (_state == State.Dead) return;
        if (_startupTimer > 0f) return;

        switch (_state)
        {
            case State.Charging:
                FixedTickCharging();
                break;

            case State.Scanning:
            case State.Stunned:
                _rb.linearVelocity = Vector2.Lerp(
                    _rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 15f);
                break;
            // PreCharge: constraints = FreezeAll, no tocar velocity.
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ESCANEO
    // ══════════════════════════════════════════════════════════

    private void TickScanning()
    {
        if (_minScanTimer > 0f)
            _minScanTimer -= Time.deltaTime;

        if (scanReverseInterval > 0f)
        {
            _reverseTimer += Time.deltaTime;
            if (_reverseTimer >= scanReverseInterval)
            {
                _reverseTimer = 0f;
                scanDirection = -scanDirection;
            }
        }

        bool playerVisible = PlayerInCone();

        if (noVisionCenterTime > 0f)
        {
            if (!playerVisible)
            {
                _noVisionTimer += Time.deltaTime;
                if (_noVisionTimer >= noVisionCenterTime)
                {
                    RotateTowardCenter();
                    return;
                }
            }
            else
            {
                _noVisionTimer = 0f;
            }
        }

        transform.Rotate(0f, 0f, -scanDirection * scanRotationSpeed * Time.deltaTime);

        if (_minScanTimer <= 0f && playerVisible)
            BeginPreCharge();
    }

    private void RotateTowardCenter()
    {
        Vector2 toCenter  = (_arenaCenter - (Vector2)transform.position).normalized;
        float targetAngle = -(Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg);
        float newAngle    = Mathf.MoveTowardsAngle(
            transform.eulerAngles.z, targetAngle, centerCorrectionSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);

        if (Mathf.Abs(Mathf.DeltaAngle(newAngle, targetAngle)) < 1f)
            _noVisionTimer = 0f;
    }

    private bool PlayerInCone()
    {
        Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
        if (toPlayer.magnitude > detectionRange) return false;
        return Vector2.Angle(transform.up, toPlayer) <= detectionHalfAngle;
    }

    // ══════════════════════════════════════════════════════════
    //  PRE-EMBESTIDA
    // ══════════════════════════════════════════════════════════

    private void BeginPreCharge()
    {
        _state = State.PreCharge;

        _chargeDir          = transform.up.normalized;
        _preChargeAnchor    = _rb.position;
        _distanceFromAnchor = 0f;

        _rb.linearVelocity = Vector2.zero;
        _rb.constraints    = RigidbodyConstraints2D.FreezeAll;

        bullHitbox?.SetActive(false);  // ← asegura que esté desactivada

        Debug.Log($"[BullEnemyAI] {gameObject.name} → PRE-EMBESTIDA ({preChargeDelay}s)");

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(PreChargeRoutine());
    }

    private IEnumerator PreChargeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < preChargeDelay)
        {
            elapsed += Time.deltaTime;

            if (telegraphShakeAmplitude > 0f)
            {
                float offsetX    = Mathf.Sin(elapsed * telegraphShakeFrequency * Mathf.PI * 2f)
                                   * telegraphShakeAmplitude;
                Vector2 perp     = new Vector2(-_chargeDir.y, _chargeDir.x);
                Vector2 shakePos = _preChargeAnchor + perp * offsetX;
                _rb.MovePosition(shakePos);
            }

            yield return null;
        }

        // Restaura posición exacta y desbloquea
        _rb.MovePosition(_preChargeAnchor);
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        _activeCoroutine = null;
        BeginCharge();
    }

    // ══════════════════════════════════════════════════════════
    //  EMBESTIDA — EXPLOSIVA (sin aceleración gradual)
    // ══════════════════════════════════════════════════════════

    private void BeginCharge()
    {
        _state              = State.Charging;
        _distanceFromAnchor = 0f;

        _currentSpeed      = chargeMaxSpeed;
        _rb.linearVelocity = _chargeDir * _currentSpeed;

        bullHitbox?.SetActive(true);   // ← activa el trigger de daño

        AudioManager.Instance?.PlayBullCharge();
        Debug.Log($"[BullEnemyAI] {gameObject.name} → EMBESTIDA explosiva a {chargeMaxSpeed} u/s");
    }

    private void TickCharging()
    {
        if (_distanceFromAnchor >= MinDistBeforeBorderCheck
            && DistanceToBorder() <= borderStunThreshold)
        {
            BeginStun();
            return;
        }
    }

    private void FixedTickCharging()
    {
        if (_distanceFromAnchor >= MinDistBeforeBorderCheck
            && DistanceToBorder() <= borderStunThreshold)
            return;

        // Frenado suave solo al acercarse al borde
        float d = DistanceToBorder();
        if (d < brakingMargin && d > 0f)
        {
            // Reduce la velocidad actual proporcionalmente al acercamiento
            float brakeFactor = Mathf.Lerp(brakingStrength, 1f, d / brakingMargin);
            _currentSpeed = chargeMaxSpeed * brakeFactor;
        }
        else
        {
            // Fuera del brakingMargin: velocidad completa siempre
            _currentSpeed = chargeMaxSpeed;
        }

        _rb.linearVelocity  = _chargeDir * _currentSpeed;
        _distanceFromAnchor = Vector2.Distance(_rb.position, _preChargeAnchor);
    }

    // ══════════════════════════════════════════════════════════
    //  ATURDIMIENTO + CAMERA SHAKE
    // ══════════════════════════════════════════════════════════

    private void BeginStun()
    {
        _state             = State.Stunned;
        _rb.linearVelocity = Vector2.zero;
        _currentSpeed      = 0f;

        bullHitbox?.SetActive(false);  // ← desactiva el trigger de daño

        AudioManager.Instance?.PlayBullStun();
        Debug.Log($"[BullEnemyAI] {gameObject.name} → ATURDIDO ({stunDuration}s)");

        // ── Camera shake ──────────────────────────────────────
        if (targetCamera != null)
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                // Restaura posición antes de iniciar nuevo shake
                targetCamera.transform.localPosition = _cameraOriginalPos;
            }
            _shakeCoroutine = StartCoroutine(CameraShakeRoutine());
        }

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(StunRoutine());
    }

    private IEnumerator CameraShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // Decaimiento: usa la curva si tiene claves, sino lineal
            float normalizedTime = elapsed / shakeDuration;
            float decay = shakeDecayCurve != null && shakeDecayCurve.length > 0
                ? shakeDecayCurve.Evaluate(normalizedTime)
                : 1f - normalizedTime;

            // Posición oscilante con frecuencia configurable
            float t        = elapsed * shakeFrequency;
            float offsetX  = (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * shakeMagnitude * decay;
            float offsetY  = (Mathf.PerlinNoise(0f, t) * 2f - 1f) * shakeMagnitude * decay;

            targetCamera.transform.localPosition =
                _cameraOriginalPos + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        // Restaura posición exacta
        targetCamera.transform.localPosition = _cameraOriginalPos;
        _shakeCoroutine = null;
    }

    private IEnumerator StunRoutine()
    {
        if (_spriteRenderer != null)
            _spriteRenderer.color = stunColor;

        yield return new WaitForSeconds(stunDuration);

        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;

        yield return StartCoroutine(TurnTowardCenter());

        _minScanTimer    = minScanTime;
        _reverseTimer    = 0f;
        _noVisionTimer   = 0f;
        _state           = State.Scanning;
        _activeCoroutine = null;
        Debug.Log($"[BullEnemyAI] {gameObject.name} → ESCANEO");
    }

    private IEnumerator TurnTowardCenter()
    {
        Vector2 toCenter     = (_arenaCenter - (Vector2)transform.position).normalized;
        float targetAngleDeg = -(Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg);
        float startAngle     = transform.eulerAngles.z;
        float elapsed        = 0f;

        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.SmoothStep(0f, 1f, elapsed / turnDuration);
            float angle = Mathf.LerpAngle(startAngle, targetAngleDeg, t);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, targetAngleDeg);
    }

    // ══════════════════════════════════════════════════════════
    //  DAÑO DE CONTACTO — llamado desde BullHitbox
    // ══════════════════════════════════════════════════════════

    /// Llamado por BullHitbox cuando el trigger toca al jugador.
    public void OnHitboxContact(HealthSystem playerHealth, Vector3 contactPosition)
    {
        if (_damageCooldown > 0f) return;
        if (_state != State.Charging) return;

        Vector2 hitDir = ((Vector2)contactPosition - (Vector2)transform.position).normalized;

        if (playerHealth.TakeDamage(chargeDamage, hitDir))
        {
            _damageCooldown = damageCooldownDuration;
            Debug.Log($"[BullEnemyAI] Golpeó al jugador: {chargeDamage} dmg.");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  UTILIDADES
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
        _rb.constraints    = RigidbodyConstraints2D.FreezeRotation;
        _rb.bodyType       = RigidbodyType2D.Kinematic;

        bullHitbox?.SetActive(false);  // ← desactiva el trigger al morir

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);

        // Restaura cámara si estaba haciendo shake
        if (_shakeCoroutine != null)
        {
            StopCoroutine(_shakeCoroutine);
            if (targetCamera != null)
                targetCamera.transform.localPosition = _cameraOriginalPos;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;

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
        float   rad = detectionHalfAngle * Mathf.Deg2Rad;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawRay(transform.position, (Vector3)RotateVec(fwd,  rad) * detectionRange);
        Gizmos.DrawRay(transform.position, (Vector3)RotateVec(fwd, -rad) * detectionRange);
        Gizmos.DrawRay(transform.position, (Vector3)fwd * detectionRange);

        // Radio de daño — ahora es el collider del BullHitbox hijo

        // Zona de frenado y stun en la arena
        if (arenaCollider != null)
        {
            Vector2 c = (Vector2)arenaCollider.transform.position + arenaCollider.offset;
            float   r = arenaCollider.radius * Mathf.Max(
                arenaCollider.transform.lossyScale.x,
                arenaCollider.transform.lossyScale.y);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawWireSphere(c, r - brakingMargin);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(c, r - borderStunThreshold);
        }

        // Ancla y dirección de carga (solo en Play)
        if (Application.isPlaying && (_state == State.PreCharge || _state == State.Charging))
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_preChargeAnchor, 0.15f);
            Gizmos.DrawRay(_preChargeAnchor, _chargeDir * 2f);
        }
    }

    private static Vector2 RotateVec(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
    public void ApplyScaling(float damageMult, float speedMult)
    {
        chargeDamage *= damageMult;
        chargeMaxSpeed *= speedMult;
    }
    public void SetArena(CircleCollider2D arena)
    {
        arenaCollider = arena;
        if (arenaCollider != null)
        {
            _arenaCenter = (Vector2)arenaCollider.transform.position + arenaCollider.offset;
            _arenaRadius = arenaCollider.radius * Mathf.Max(
                arenaCollider.transform.lossyScale.x,
                arenaCollider.transform.lossyScale.y);
        }
    }

}
