using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class HealthSystem : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth    = 100f;
    [SerializeField] private float currentHealth;

    [Header("Invencibilidad tras recibir daño")]
    [SerializeField] private bool  useInvincibility      = true;
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float _invincibilityTimer = 0f;

    [Header("Knockback")]
    [Tooltip("Velocidad inicial del empuje (unidades/segundo).")]
    [SerializeField] private float knockbackForce    = 10f;
    [Tooltip("Segundos que tarda el empuje en llegar a 0 (decaimiento lineal).")]
    [SerializeField] private float knockbackDuration = 0.25f;

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
        _isPlayer = gameObject.CompareTag("Player");
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

        // Knockback gradual
        if (_rb != null && knockbackForce > 0f && hitDirection != Vector2.zero)
        {
            if (_knockbackCoroutine != null)
                StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(hitDirection.normalized));
        }

        // Flash
        if (_spriteRenderer != null)
        {
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(HitFlash());
        }

        // ── Sonido de daño ─────────────────────────────────────
        if (_isPlayer)
            AudioManager.Instance?.PlayPlayerHit();
        else
            AudioManager.Instance?.PlayEnemyHit();

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
       // Debug.Log($"{gameObject.name} recibió {amount} de daño. HP: {currentHealth}/{MaxHealth}");

        if (currentHealth <= 0f)
        {
            // ── Sonido de muerte ───────────────────────────────
            if (_isPlayer)
            {
                AudioManager.Instance?.PlayPlayerDie();
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);             
            }
            else
                AudioManager.Instance?.PlayEnemyDie();

            OnDeath?.Invoke();            
            //Debug.Log($"{gameObject.name} murió.");
            
        }

        return true;
    }

    // ── Corrutinas ─────────────────────────────────────────────

    private IEnumerator KnockbackRoutine(Vector2 direction)
    {
        float elapsed = 0f;

        while (elapsed < knockbackDuration)
        {
            float t            = 1f - (elapsed / knockbackDuration);
            float currentSpeed = knockbackForce * t;

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
       // Debug.Log($"[HealthSystem] HP máximo aumentado en {flat}. Total: {MaxHealth:F0} | Actual: {currentHealth:F0}");
    }

    private void OnGUI()
    {
        if (!_isPlayer) return;
        GUI.Label(new Rect(10, 90, 260, 30), $"HP: {currentHealth:F0} / {MaxHealth:F0}");
    }
    
}
