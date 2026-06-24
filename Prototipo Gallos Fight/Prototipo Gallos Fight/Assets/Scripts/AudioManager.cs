// ============================================================
//  AudioManager.cs  — v2
//
//  CAMBIOS respecto a v1:
//   · Jugador: añadido playerDashClip / PlayPlayerDash().
//   · Enemigos diferenciados:
//       - EnemyAI (normal):   usa enemyHit/Die/AttackClip    (sin cambio)
//       - EnemyAI (variante): usa variantHit/Die/AttackClip
//       - RangedEnemyAI:      usa rangedHit/Die/AttackClip
//       - BullEnemyAI:        ya usaba bullCharge/StunClip   (sin cambio)
//         + añadido bullHitClip / bullDieClip
//   · UI: añadido uiButtonClip / PlayUIButton() separado de
//     PlayPowerUpSelect(). Los botones de menú usan PlayUIButton().
//
//  SETUP EN UNITY:
//   1. Crea un GameObject vacío "AudioManager" en la primera escena.
//   2. Añade este script.
//   3. Arrastra tus AudioClips a los campos del Inspector.
//   4. El GameObject sobrevive entre escenas automáticamente.
// ============================================================
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static AudioManager Instance { get; private set; }

    // ══════════════════════════════════════════════════════════
    //  CLIPS — asignar en Inspector
    // ══════════════════════════════════════════════════════════

    [Header("── Jugador ──────────────────────────────────────")]
    [Tooltip("Sonido del ataque cuerpo a cuerpo del jugador.")]
    [SerializeField] private AudioClip playerAttackClip;

    [Tooltip("Sonido del disparo del jugador.")]
    [SerializeField] private AudioClip playerShootClip;

    [Tooltip("Sonido cuando el jugador recibe daño.")]
    [SerializeField] private AudioClip playerHitClip;

    [Tooltip("Sonido cuando el jugador muere.")]
    [SerializeField] private AudioClip playerDieClip;

    [Tooltip("Sonido al realizar el dash.")]
    [SerializeField] private AudioClip playerDashClip;

    [Header("── Enemigo Normal (EnemyAI) ─────────────────────")]
    [Tooltip("Sonido cuando el enemigo normal recibe daño.")]
    [SerializeField] private AudioClip enemyHitClip;

    [Tooltip("Sonido cuando el enemigo normal muere.")]
    [SerializeField] private AudioClip enemyDieClip;

    [Tooltip("Sonido del ataque cuerpo a cuerpo del enemigo normal.")]
    [SerializeField] private AudioClip enemyAttackClip;

    [Header("── Enemigo Variante (EnemyAI variante) ──────────")]
    [Tooltip("Sonido cuando el enemigo variante recibe daño. " +
             "Vacío = usa el sonido del enemigo normal como fallback.")]
    [SerializeField] private AudioClip variantHitClip;

    [Tooltip("Sonido cuando el enemigo variante muere. Vacío = fallback normal.")]
    [SerializeField] private AudioClip variantDieClip;

    [Tooltip("Sonido del ataque del enemigo variante. Vacío = fallback normal.")]
    [SerializeField] private AudioClip variantAttackClip;

    [Header("── Enemigo a Distancia (RangedEnemyAI) ──────────")]
    [Tooltip("Sonido cuando el enemigo a distancia recibe daño. " +
             "Vacío = usa el sonido del enemigo normal como fallback.")]
    [SerializeField] private AudioClip rangedHitClip;

    [Tooltip("Sonido cuando el enemigo a distancia muere. Vacío = fallback normal.")]
    [SerializeField] private AudioClip rangedDieClip;

    [Tooltip("Sonido del disparo del enemigo a distancia. Vacío = fallback normal.")]
    [SerializeField] private AudioClip rangedAttackClip;

    [Header("── Enemigo Toro (BullEnemyAI) ───────────────────")]
    [Tooltip("Sonido cuando el toro recibe daño. Vacío = fallback normal.")]
    [SerializeField] private AudioClip bullHitClip;

    [Tooltip("Sonido cuando el toro muere. Vacío = fallback normal.")]
    [SerializeField] private AudioClip bullDieClip;

    [Tooltip("Sonido al iniciar la embestida del toro.")]
    [SerializeField] private AudioClip bullChargeClip;

    [Tooltip("Sonido cuando el toro choca con el borde de la arena y queda aturdido.")]
    [SerializeField] private AudioClip bullStunClip;

    [Header("── Proyectil ────────────────────────────────────")]
    [Tooltip("Sonido cuando una bala impacta a un objetivo.")]
    [SerializeField] private AudioClip bulletImpactClip;

    [Header("── UI ───────────────────────────────────────────")]
    [Tooltip("Sonido al pulsar cualquier botón de menú (navegación, confirmación).")]
    [SerializeField] private AudioClip uiButtonClip;

    [Tooltip("Sonido al seleccionar un powerup (más enfático que el botón de menú).")]
    [SerializeField] private AudioClip powerUpSelectClip;

    // ══════════════════════════════════════════════════════════
    //  VOLÚMENES — ajustables desde Inspector
    // ══════════════════════════════════════════════════════════

    [Header("── Volúmenes (0-1) ──────────────────────────────")]
    [Range(0f, 1f)] [SerializeField] private float playerAttackVolume  = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float playerShootVolume   = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float playerHitVolume     = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float playerDieVolume     = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float playerDashVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float enemyHitVolume      = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float enemyDieVolume      = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float enemyAttackVolume   = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float variantHitVolume    = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float variantDieVolume    = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float variantAttackVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float rangedHitVolume     = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float rangedDieVolume     = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float rangedAttackVolume  = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float bullHitVolume       = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float bullDieVolume       = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float bullChargeVolume    = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float bullStunVolume      = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float bulletImpactVolume  = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float uiButtonVolume      = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float powerUpSelectVolume = 0.9f;

    [Header("── AudioSource pool (se crean automáticamente) ──")]
    [Tooltip("Cuántos AudioSources simultáneos soporta el manager. " +
             "Auméntalo si escuchas cortes de sonido.")]
    [SerializeField] private int audioSourcePoolSize = 8;

    // ── Pool de AudioSources ───────────────────────────────────
    private AudioSource[] _pool;
    private int _poolIndex = 0;

    // ══════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildPool();
    }

    // ── Construcción del pool ──────────────────────────────────

    private void BuildPool()
    {
        _pool = new AudioSource[audioSourcePoolSize];
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            _pool[i] = gameObject.AddComponent<AudioSource>();
            _pool[i].playOnAwake = false;
        }
    }

    // ── Reproductor genérico ───────────────────────────────────

    private void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        // Busca un AudioSource libre en el pool
        AudioSource source = GetFreeSource();
        source.clip   = clip;
        source.volume = volume;
        source.pitch  = pitch;
        source.Play();
    }

    private AudioSource GetFreeSource()
    {
        // Recorre el pool buscando uno que no esté sonando
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].isPlaying)
                return _pool[i];
        }

        // Si todos están ocupados, sobrescribe el más antiguo (round-robin)
        AudioSource s = _pool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _pool.Length;
        return s;
    }

    // ══════════════════════════════════════════════════════════
    //  API PÚBLICA — llamar desde otros scripts
    // ══════════════════════════════════════════════════════════

    // ── Jugador ────────────────────────────────────────────────

    public void PlayPlayerAttack()
        => Play(playerAttackClip, playerAttackVolume, Random.Range(0.95f, 1.05f));

    public void PlayPlayerShoot()
        => Play(playerShootClip, playerShootVolume, Random.Range(0.95f, 1.05f));

    public void PlayPlayerHit()
        => Play(playerHitClip, playerHitVolume, Random.Range(0.90f, 1.10f));

    public void PlayPlayerDie()
        => Play(playerDieClip, playerDieVolume);

    public void PlayPlayerDash()
        => Play(playerDashClip, playerDashVolume, Random.Range(0.95f, 1.05f));

    // ── Enemigo Normal ─────────────────────────────────────────

    public void PlayEnemyHit()
        => Play(enemyHitClip, enemyHitVolume, Random.Range(0.90f, 1.10f));

    public void PlayEnemyDie()
        => Play(enemyDieClip, enemyDieVolume, Random.Range(0.95f, 1.05f));

    public void PlayEnemyAttack()
        => Play(enemyAttackClip, enemyAttackVolume, Random.Range(0.95f, 1.05f));

    // ── Enemigo Variante ───────────────────────────────────────
    // Si no tiene clip propio, cae al sonido del enemigo normal.

    public void PlayVariantHit()
        => Play(variantHitClip != null ? variantHitClip : enemyHitClip,
                variantHitVolume, Random.Range(0.90f, 1.10f));

    public void PlayVariantDie()
        => Play(variantDieClip != null ? variantDieClip : enemyDieClip,
                variantDieVolume, Random.Range(0.95f, 1.05f));

    public void PlayVariantAttack()
        => Play(variantAttackClip != null ? variantAttackClip : enemyAttackClip,
                variantAttackVolume, Random.Range(0.95f, 1.05f));

    // ── Enemigo a Distancia ────────────────────────────────────

    public void PlayRangedHit()
        => Play(rangedHitClip != null ? rangedHitClip : enemyHitClip,
                rangedHitVolume, Random.Range(0.90f, 1.10f));

    public void PlayRangedDie()
        => Play(rangedDieClip != null ? rangedDieClip : enemyDieClip,
                rangedDieVolume, Random.Range(0.95f, 1.05f));

    public void PlayRangedAttack()
        => Play(rangedAttackClip != null ? rangedAttackClip : enemyAttackClip,
                rangedAttackVolume, Random.Range(0.95f, 1.05f));

    // ── Toro ───────────────────────────────────────────────────

    public void PlayBullHit()
        => Play(bullHitClip != null ? bullHitClip : enemyHitClip,
                bullHitVolume, Random.Range(0.90f, 1.10f));

    public void PlayBullDie()
        => Play(bullDieClip != null ? bullDieClip : enemyDieClip,
                bullDieVolume, Random.Range(0.95f, 1.05f));

    public void PlayBullCharge()
        => Play(bullChargeClip, bullChargeVolume);

    public void PlayBullStun()
        => Play(bullStunClip, bullStunVolume);

    // ── Proyectil ──────────────────────────────────────────────

    public void PlayBulletImpact()
        => Play(bulletImpactClip, bulletImpactVolume, Random.Range(0.90f, 1.10f));

    // ── UI ─────────────────────────────────────────────────────

    /// Botones de menú, navegación, confirmación general.
    public void PlayUIButton()
        => Play(uiButtonClip, uiButtonVolume, Random.Range(0.97f, 1.03f));

    /// Selección de powerup — más enfático que un botón de menú.
    public void PlayPowerUpSelect()
        => Play(powerUpSelectClip, powerUpSelectVolume);
}
