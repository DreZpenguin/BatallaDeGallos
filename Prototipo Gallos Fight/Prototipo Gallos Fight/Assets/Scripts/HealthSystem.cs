// ============================================================
//  HealthSystem.cs  — v3
//
//  CAMBIOS respecto a v2:
//   · Nuevo enum EnemyType (Normal, Variant, Ranged, Bull) y
//     campo serializado enemyType en el Inspector.
//   · TakeDamage() y OnDeath ahora llaman al método de audio
//     correspondiente al tipo de enemigo en vez de siempre
//     PlayEnemyHit() / PlayEnemyDie().
//   · El jugador no se ve afectado (sigue usando PlayPlayerHit/Die).
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class HealthSystem : MonoBehaviour
{
    // ── Tipo de enemigo (define qué sonidos usa) ───────────────
    public enum EnemyType { Normal, Variant, Ranged, Bull }

    [Header("Tipo de enemigo")]
    [Tooltip("Solo relevante en enemigos. Define qué canal de audio usa al recibir daño " +
             "o morir. Ignorado si este GameObject tiene el tag 'Player'.")]
    [SerializeField] private EnemyType enemyType = EnemyType.Normal;

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

    [Header("Partículas de muerte")]
    [Tooltip("Prefab de efecto de partículas que se instancia al morir " +
             "(en la posición de este GameObject). Funciona para jugador y enemigos. " +
             "Deja vacío para no usar partículas.")]
    [SerializeField] private GameObject deathParticlesPrefab;

    [Tooltip("Segundos que vive el GameObject de partículas antes de destruirse. " +
             "Debe ser mayor o igual a la duración del efecto.")]
    [SerializeField] private float deathParticlesLifetime = 2f;

    private float _maxHealthBonus  = 0f;
    private bool  _infiniteHealth  = false;  // modo práctica

    private Rigidbody2D    _rb;
    private SpriteRenderer _spriteRenderer;
    private Color          _originalColor;
    private Coroutine      _flashCoroutine;
    private Coroutine      _knockbackCoroutine;

    private bool _isPlayer;

    [Header("Pantalla de muerte")]
    [Tooltip("Si está activo, al morir el jugador se muestra el overlay de muerte " +
             "(fade-in dentro del mismo Canvas) en vez de ir directo al menú. " +
             "Requiere un DeathScreenOverlay en la escena. Solo aplica si _isPlayer.")]
    [SerializeField] private bool useDeathScreenOnDeath = true;

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
        return TakeDamage(amount, Vector2.zero, 1f);
    }

    public bool TakeDamage(float amount, Vector2 hitDirection)
    {
        return TakeDamage(amount, hitDirection, 1f);
    }

    /// Aplica daño directo ignorando el cooldown de invencibilidad.
    /// Usar exclusivamente para daño continuo (DeadZone) donde el
    /// cooldown de invencibilidad bloquearía el daño real.
    /// No aplica knockback ni flash de color.
    public void TakeDamageRaw(float amount)
    {
        if (!IsAlive) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
        {
            if (_infiniteHealth)
            {
                currentHealth = MaxHealth;
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
                return;
            }

            if (_isPlayer)
            {
                AudioManager.Instance?.PlayPlayerDie();
                SpawnDeathParticles();
                PlayerData.Instance?.ResetAll();
                StatsModal.ClearSavedLevels();

                DeathScreenOverlay overlay = useDeathScreenOnDeath
                    ? (DeathScreenOverlay.Instance ?? FindFirstObjectByType<DeathScreenOverlay>())
                    : null;

                if (overlay != null)
                    overlay.Show(DeathScreenOverlay.Result.Defeat);
                else
                    SceneManager.LoadScene(0);
            }
            else
            {
                PlayEnemyDieSound();
                SpawnDeathParticles();
            }

            OnDeath?.Invoke();
        }
    }

    public bool TakeDamage(float amount, Vector2 hitDirection, float knockbackMultiplier)
    {
        if (!IsAlive) return false;
        if (useInvincibility && _invincibilityTimer > 0f) return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (useInvincibility)
            _invincibilityTimer = invincibilityDuration;

        // ── Knockback con resistencia y multiplicador externo ──
        float effectiveForce = knockbackForce * (1f - knockbackResistance) * knockbackMultiplier;

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
            PlayEnemyHitSound();

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
        {
            // Si tiene vida infinita (modo práctica) se restaura al máximo
            if (_infiniteHealth)
            {
                currentHealth = MaxHealth;
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
                return true;
            }
            if (_isPlayer)
            {
                AudioManager.Instance?.PlayPlayerDie();
                SpawnDeathParticles();
                PlayerData.Instance?.ResetAll();
                StatsModal.ClearSavedLevels();

                DeathScreenOverlay overlay = useDeathScreenOnDeath
                    ? (DeathScreenOverlay.Instance ?? FindFirstObjectByType<DeathScreenOverlay>())
                    : null;

                if (overlay != null)
                {
                    overlay.Show(DeathScreenOverlay.Result.Defeat);   // fade-in dentro del Canvas actual, sin cambiar escena
                }
                else
                {
                    SceneManager.LoadScene(0);
                }
            }
            else
            {
                PlayEnemyDieSound();
                SpawnDeathParticles();
            }

            OnDeath?.Invoke();
        }

        return true;
    }

    // ── Partículas de muerte ─────────────────────────────────

    private void SpawnDeathParticles()
    {
        if (deathParticlesPrefab == null) return;

        GameObject fx = Instantiate(deathParticlesPrefab, transform.position, Quaternion.identity);
        Destroy(fx, deathParticlesLifetime);
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

    /// Activa la vida infinita (modo práctica).
    /// El enemigo recibe daño y muestra los popups, pero nunca muere.
    public void SetInfiniteHealth(bool infinite)
    {
        _infiniteHealth = infinite;
        if (infinite)
        {
            currentHealth = MaxHealth;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }
    }

    // ── Barra de vida (OnGUI) ──────────────────────────────────

    [Header("Barra de vida — UI")]
    [Tooltip("Ancho de la barra de vida en píxeles.")]
    [SerializeField] private float hpBarWidth      = 220f;
    [Tooltip("Alto de la barra de vida en píxeles.")]
    [SerializeField] private float hpBarHeight     = 22f;
    [Tooltip("Margen desde el borde derecho de la pantalla.")]
    [SerializeField] private float hpBarMarginX    = 20f;
    [Tooltip("Margen desde el borde superior de la pantalla.")]
    [SerializeField] private float hpBarMarginY    = 20f;
    [Tooltip("Color de la barra cuando la vida es alta (>60%).")]
    [SerializeField] private Color hpColorHigh     = new Color(0.15f, 0.85f, 0.25f);
    [Tooltip("Color de la barra cuando la vida es media (30–60%).")]
    [SerializeField] private Color hpColorMid      = new Color(0.95f, 0.75f, 0.05f);
    [Tooltip("Color de la barra cuando la vida es baja (<30%).")]
    [SerializeField] private Color hpColorLow      = new Color(0.90f, 0.15f, 0.10f);
    [Tooltip("Color del fondo de la barra.")]
    [SerializeField] private Color hpColorBg       = new Color(0.10f, 0.10f, 0.10f, 0.85f);
    [Tooltip("Color del borde de la barra.")]
    [SerializeField] private Color hpColorBorder   = new Color(0f, 0f, 0f, 0.90f);
    [Tooltip("Tamaño del texto de HP.")]
    [SerializeField] private int   hpFontSize      = 13;
    [Tooltip("Mostrar texto numérico dentro de la barra.")]
    [SerializeField] private bool  hpShowText      = true;

    // Texturas generadas en runtime (una sola vez)
    private Texture2D _texWhite;
    private GUIStyle  _hpTextStyle;
    private bool      _guiInitialized = false;

    private void InitGUI()
    {
        if (_guiInitialized) return;
        _guiInitialized = true;

        _texWhite = new Texture2D(1, 1);
        _texWhite.SetPixel(0, 0, Color.white);
        _texWhite.Apply();

        _hpTextStyle                  = new GUIStyle(GUI.skin.label);
        _hpTextStyle.fontSize         = hpFontSize;
        _hpTextStyle.fontStyle        = FontStyle.Bold;
        _hpTextStyle.alignment        = TextAnchor.MiddleCenter;
        _hpTextStyle.normal.textColor = Color.white;
    }

    // ── Helpers de audio por tipo de enemigo ───────────────────

    private void PlayEnemyHitSound()
    {
        switch (enemyType)
        {
            case EnemyType.Variant: AudioManager.Instance?.PlayVariantHit(); break;
            case EnemyType.Ranged:  AudioManager.Instance?.PlayRangedHit();  break;
            case EnemyType.Bull:    AudioManager.Instance?.PlayBullHit();    break;
            default:                AudioManager.Instance?.PlayEnemyHit();   break;
        }
    }

    private void PlayEnemyDieSound()
    {
        switch (enemyType)
        {
            case EnemyType.Variant: AudioManager.Instance?.PlayVariantDie(); break;
            case EnemyType.Ranged:  AudioManager.Instance?.PlayRangedDie();  break;
            case EnemyType.Bull:    AudioManager.Instance?.PlayBullDie();    break;
            default:                AudioManager.Instance?.PlayEnemyDie();   break;
        }
    }

    // ── Barra de vida (OnGUI) ──────────────────────────────────

    private void OnGUI()
    {
        if (!_isPlayer) return;

        InitGUI();

        float sw = Screen.width;

        // Posición: esquina superior derecha
        float barX = sw - hpBarWidth - hpBarMarginX;
        float barY = hpBarMarginY;

        float ratio = MaxHealth > 0f ? Mathf.Clamp01(currentHealth / MaxHealth) : 0f;

        // ── Color dinámico según % de vida ────────────────────
        Color barColor;
        if (ratio > 0.6f)
            barColor = hpColorHigh;
        else if (ratio > 0.3f)
            barColor = Color.Lerp(hpColorMid, hpColorHigh, (ratio - 0.3f) / 0.3f);
        else
            barColor = Color.Lerp(hpColorLow, hpColorMid, ratio / 0.3f);

        float border = 2f;

        // ── Borde exterior ────────────────────────────────────
        GUI.color = hpColorBorder;
        GUI.DrawTexture(new Rect(barX - border, barY - border,
                                 hpBarWidth + border * 2f, hpBarHeight + border * 2f), _texWhite);

        // ── Fondo ─────────────────────────────────────────────
        GUI.color = hpColorBg;
        GUI.DrawTexture(new Rect(barX, barY, hpBarWidth, hpBarHeight), _texWhite);

        // ── Relleno de vida ───────────────────────────────────
        if (ratio > 0f)
        {
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barX, barY, hpBarWidth * ratio, hpBarHeight), _texWhite);
        }

        // ── Texto HP ──────────────────────────────────────────
        if (hpShowText)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY, hpBarWidth, hpBarHeight),
                      $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(MaxHealth)}",
                      _hpTextStyle);
        }

        // Restaura color de GUI
        GUI.color = Color.white;
    }
}
