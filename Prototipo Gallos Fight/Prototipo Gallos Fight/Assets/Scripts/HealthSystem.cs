using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class HealthSystem : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Invencibilidad tras recibir daño")]
    [SerializeField] private bool  useInvincibility      = true;
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float _invincibilityTimer = 0f;

    [Header("Knockback")]
    [Tooltip("Velocidad inicial del empuje (unidades/segundo).")]
    [SerializeField] private float knockbackForce = 10f;

    [Tooltip("Segundos que tarda el empuje en llegar a 0.")]
    [SerializeField] private float knockbackDuration = 0.25f;

    [Tooltip("Resistencia al knockback. 0 = sin resistencia (empuje normal). " +
             "1 = inmune total (sin empuje). Úsalo en el Toro con 0.90–0.99.")]
    [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0f;

    [Header("Flash de color al recibir daño")]
    [SerializeField] private Color hitColor         = Color.red;
    [SerializeField] private float hitColorDuration = 0.15f;

    private float _maxHealthBonus = 0f;

    private Rigidbody2D    _rb;
    private SpriteRenderer _spriteRenderer;
    private Color          _originalColor;
    private Coroutine      _flashCoroutine;
    private Coroutine      _knockbackCoroutine;

    private bool _isPlayer;

    [Header("Eventos")]
    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent               OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth     => maxHealth + _maxHealthBonus;
    public bool  IsAlive       => currentHealth > 0f;

    private void Awake()
    {
        _isPlayer     = gameObject.CompareTag("Player");
        currentHealth = MaxHealth;

        _rb = GetComponent<Rigidbody2D>();

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
    }

    private void Start()
    {
        if (_isPlayer && PlayerData.Instance != null)
        {
            float bonus = PlayerData.Instance.HealthBonus;
            if (bonus > 0f)
                AddMaxHealthBonus(bonus);
        }
    }

    private void Update()
    {
        if (_invincibilityTimer > 0f)
            _invincibilityTimer -= Time.deltaTime;
    }

    // ── TakeDamage ─────────────────────────────────────────────

    public bool TakeDamage(float amount)
    {
        return TakeDamage(amount, Vector2.zero);
    }

    public bool TakeDamage(float amount, Vector2 hitDirection)
    {
        if (!IsAlive) return false;
        if (useInvincibility && _invincibilityTimer > 0f) return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (useInvincibility)
            _invincibilityTimer = invincibilityDuration;

        // ── Knockback con resistencia ──────────────────────────
        // La fuerza efectiva = knockbackForce * (1 - resistencia).
        // Con resistencia = 0.95 → solo el 5 % del empuje original.
        float effectiveForce = knockbackForce * (1f - knockbackResistance);

        if (_rb != null && effectiveForce > 0.01f && hitDirection != Vector2.zero)
        {
            if (_knockbackCoroutine != null)
                StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(
                KnockbackRoutine(hitDirection.normalized, effectiveForce));
        }

        // Flash
        if (_spriteRenderer != null)
        {
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(HitFlash());
        }

        // Sonido
        if (_isPlayer)
            AudioManager.Instance?.PlayPlayerHit();
        else
            AudioManager.Instance?.PlayEnemyHit();

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
        {
            if (_isPlayer)
            {
                AudioManager.Instance?.PlayPlayerDie();
                SceneManager.LoadScene(0);
            }
            else
                AudioManager.Instance?.PlayEnemyDie();

            OnDeath?.Invoke();
        }

        return true;
    }

    // ── Corrutinas ─────────────────────────────────────────────

    private IEnumerator KnockbackRoutine(Vector2 direction, float force)
    {
        float elapsed = 0f;

        while (elapsed < knockbackDuration)
        {
            float t            = 1f - (elapsed / knockbackDuration);
            float currentSpeed = force * t;

            _rb.MovePosition(_rb.position + direction * currentSpeed * Time.fixedDeltaTime);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _knockbackCoroutine = null;
    }

    private IEnumerator HitFlash()
    {
        _spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(hitColorDuration);
        _spriteRenderer.color = _originalColor;
        _flashCoroutine = null;
    }

    // ── API Pública ────────────────────────────────────────────

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void AddMaxHealthBonus(float flat)
    {
        _maxHealthBonus += flat;
        currentHealth   += flat;
        currentHealth    = Mathf.Min(currentHealth, MaxHealth);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }
}
