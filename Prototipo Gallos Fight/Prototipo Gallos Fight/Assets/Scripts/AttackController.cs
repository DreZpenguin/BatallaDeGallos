
using System.Collections;
using UnityEngine;

public class AttackController : MonoBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackDamage       = 25f;
    [SerializeField] private float attackCooldown     = 0.4f;

    [Header("Hitbox")]
    [SerializeField] private Collider2D frontHitbox;
    [SerializeField] private float hitboxActiveDuration = 0.12f;

    [Header("VFX de Garra")]
    [Tooltip("Animator del GameObject hijo ClawVFX. " +
             "Solo necesita el estado 'Attack' — no hace falta estado Idle.")]
    [SerializeField] private Animator clawVFXAnimator;

    [Tooltip("Nombre del Trigger en el Animator que activa la animación de garra.")]
    [SerializeField] private string vfxTriggerName = "Attack";

    [Tooltip("Si está activo, el VFX se posiciona en el centro del collider " +
             "de la hitbox en cada ataque (recomendado).")]
    [SerializeField] private bool snapToHitbox = true;

    // SpriteRenderer del VFX — controlamos visibilidad por código
    private SpriteRenderer _vfxRenderer;
    private Coroutine      _vfxCoroutine;

    // ── Control — Teclado/Ratón ────────────────────────────────
    [Header("Control — Teclado / Ratón")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

    // ── Control — Mando Xbox ───────────────────────────────────
    [Header("Control — Mando Xbox")]
    [Tooltip("Botón del mando para atacar (se puede dejar en None si usas el gatillo).")]
    [SerializeField] private KeyCode gamepadAttackKey = KeyCode.None;

    [Tooltip("Eje del gatillo derecho (RT) para atacar. " +
             "Mismo nombre que en ShootingController. Deja vacío para no usarlo.")]
    [SerializeField] private string gamepadTriggerAxis = "RT";

    [Tooltip("Umbral del gatillo para considerar que está presionado.")]
    [SerializeField, Range(0f, 0.99f)] private float triggerThreshold = 0.3f;

    // ── Estado interno ─────────────────────────────────────────
    private float   _damageBonus     = 0f;
    private float   _cooldownTimer   = 0f;
    private Vector2 _facingDirection = Vector2.up;
    private bool    _isPlayer;
    private bool    _triggerWasDown  = false;

    public float BaseDamage    => attackDamage;
    public float CurrentDamage => attackDamage * (1f + _damageBonus);

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        _isPlayer = gameObject.CompareTag("Player");

        // Busca la hitbox automáticamente si no está asignada
        if (frontHitbox == null)
        {
            Transform t = transform.Find("HitboxFront");
            if (t != null) frontHitbox = t.GetComponent<Collider2D>();
        }

        if (frontHitbox != null)
            frontHitbox.enabled = false;
        else
            Debug.LogWarning($"[AttackController] No se encontró HitboxFront en {gameObject.name}.");

        // Busca el Animator del ClawVFX automáticamente si no está asignado
        if (clawVFXAnimator == null)
        {
            Transform vfxT = transform.Find("ClawVFX");
            if (vfxT != null)
                clawVFXAnimator = vfxT.GetComponent<Animator>();
        }

        // Cachea el SpriteRenderer y lo oculta al inicio
        if (clawVFXAnimator != null)
        {
            _vfxRenderer = clawVFXAnimator.GetComponent<SpriteRenderer>();
            if (_vfxRenderer == null)
                _vfxRenderer = clawVFXAnimator.GetComponentInChildren<SpriteRenderer>();

            if (_vfxRenderer != null)
                _vfxRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        bool attackPressed = Input.GetKeyDown(attackKey)
                          || Input.GetKeyDown(gamepadAttackKey);

        // Gatillo RT — igual que en ShootingController
        float triggerValue = GetTriggerAxis();
        bool  triggerDown  = triggerValue >= triggerThreshold;
        if (triggerDown && !_triggerWasDown)
            attackPressed = true;
        _triggerWasDown = triggerDown;

        if (attackPressed && _cooldownTimer <= 0f)
            StartAttack();
    }

    // ══════════════════════════════════════════════════════════
    //  API PÚBLICA
    // ══════════════════════════════════════════════════════════

    public void TriggerAttack()
    {
        if (_cooldownTimer <= 0f)
            StartAttack();
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (direction != Vector2.zero)
            _facingDirection = direction.normalized;
    }

    public void AddDamageBonus(float percent)
    {
        _damageBonus += percent;
        Debug.Log($"[AttackController] Daño +{percent * 100f:F0}%. " +
                  $"Total: {CurrentDamage:F1}");
    }

    private float GetTriggerAxis()
    {
        if (string.IsNullOrEmpty(gamepadTriggerAxis)) return 0f;
        try   { return Input.GetAxis(gamepadTriggerAxis); }
        catch { return 0f; }
    }

    // ══════════════════════════════════════════════════════════
    //  ATAQUE
    // ══════════════════════════════════════════════════════════

    private void StartAttack()
    {
        _cooldownTimer = attackCooldown;

        if (_isPlayer) AudioManager.Instance?.PlayPlayerAttack();
        else           AudioManager.Instance?.PlayEnemyAttack();

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        // ── 1. Activa la hitbox ───────────────────────────────
        if (frontHitbox != null)
            frontHitbox.enabled = true;

        // ── 2. Posiciona y lanza el VFX ──────────────────────
        PlayClawVFX();

        yield return new WaitForSeconds(hitboxActiveDuration);

        // ── 3. Desactiva la hitbox ────────────────────────────
        if (frontHitbox != null)
            frontHitbox.enabled = false;

        // El VFX sigue corriendo su animación hasta que el
        // Animator vuelva solo al estado Idle — no lo cortamos.
    }

    // ══════════════════════════════════════════════════════════
    //  VFX
    // ══════════════════════════════════════════════════════════

    private void PlayClawVFX()
    {
        if (clawVFXAnimator == null) return;

        // Cancela ocultado anterior si el jugador ataca antes de que termine
        if (_vfxCoroutine != null)
        {
            StopCoroutine(_vfxCoroutine);
            _vfxCoroutine = null;
        }

        // ── Posiciona ANTES de mostrar ────────────────────────
        if (snapToHitbox && frontHitbox != null)
        {
            clawVFXAnimator.transform.position = frontHitbox.bounds.center;
            clawVFXAnimator.transform.rotation = transform.rotation;
        }

        // ── Muestra el renderer ───────────────────────────────
        if (_vfxRenderer != null)
            _vfxRenderer.enabled = true;

        // ── Reinicia el Animator desde el frame 0 y dispara ──
        clawVFXAnimator.Rebind();          // resetea al estado inicial
        clawVFXAnimator.Update(0f);        // aplica el rebind sin avanzar tiempo
        clawVFXAnimator.ResetTrigger(vfxTriggerName);
        clawVFXAnimator.SetTrigger(vfxTriggerName);

        // ── Oculta tras la duración del clip ─────────────────
        float clipDuration = GetClipDuration(vfxTriggerName);
        _vfxCoroutine = StartCoroutine(HideVFXAfter(clipDuration));
    }

    /// Obtiene la duración del clip cuyo nombre coincide (insensible a mayúsculas).
    /// Si no lo encuentra usa hitboxActiveDuration como fallback.
    private float GetClipDuration(string clipName)
    {
        if (clawVFXAnimator == null) return hitboxActiveDuration;

        foreach (AnimationClip clip in clawVFXAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.ToLower().Contains(clipName.ToLower()))
                return clip.length;
        }

        // Fallback: duración de la hitbox
        return hitboxActiveDuration;
    }

    private IEnumerator HideVFXAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_vfxRenderer != null)
            _vfxRenderer.enabled = false;

        _vfxCoroutine = null;
    }

    // ══════════════════════════════════════════════════════════
    //  HITBOX CALLBACK
    // ══════════════════════════════════════════════════════════

    public void OnHitboxTriggerEnter(Collider2D other)
    {
        if (other.gameObject == gameObject) return;

        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null)
        {
            float   dmg    = CurrentDamage;
            Vector2 hitDir = ((Vector2)other.transform.position
                            - (Vector2)transform.position).normalized;

            bool hit = health.TakeDamage(dmg, hitDir);
            if (hit)
                Debug.Log($"[AttackController] {gameObject.name} → " +
                          $"{other.name} por {dmg:F1} dmg.");
        }
    }
}
