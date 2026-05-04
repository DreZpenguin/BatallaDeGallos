// ============================================================
//  AudioManager.cs
//  Singleton persistente entre escenas.
//  Centraliza todos los efectos de sonido del juego.
//
//  SETUP EN UNITY:
//   1. Crea un GameObject vacío "AudioManager" en la primera escena.
//   2. Añade este script.
//   3. Arrastra tus AudioClips a los campos del Inspector.
//   4. El GameObject sobrevive entre escenas automáticamente.
//
//  USO DESDE OTROS SCRIPTS:
//   AudioManager.Instance.PlayPlayerAttack();
//   AudioManager.Instance.PlayPlayerShoot();
//   AudioManager.Instance.PlayPlayerHit();
//   AudioManager.Instance.PlayEnemyHit();
//   AudioManager.Instance.PlayEnemyDie();
//   AudioManager.Instance.PlayBullCharge();
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

    [Header("── Enemigos ─────────────────────────────────────")]
    [Tooltip("Sonido genérico cuando un enemigo recibe daño.")]
    [SerializeField] private AudioClip enemyHitClip;

    [Tooltip("Sonido cuando un enemigo muere.")]
    [SerializeField] private AudioClip enemyDieClip;

    [Tooltip("Sonido del ataque cuerpo a cuerpo enemigo.")]
    [SerializeField] private AudioClip enemyAttackClip;

    [Header("── Enemigo Toro ─────────────────────────────────")]
    [Tooltip("Sonido al iniciar la embestida del toro.")]
    [SerializeField] private AudioClip bullChargeClip;

    [Tooltip("Sonido cuando el toro choca con el borde de la arena y queda aturdido.")]
    [SerializeField] private AudioClip bullStunClip;

    [Header("── Proyectil ────────────────────────────────────")]
    [Tooltip("Sonido cuando una bala impacta a un objetivo.")]
    [SerializeField] private AudioClip bulletImpactClip;

    [Header("── UI / PowerUp ──────────────────────────────────")]
    [Tooltip("Sonido al seleccionar un powerup.")]
    [SerializeField] private AudioClip powerUpSelectClip;

    // ══════════════════════════════════════════════════════════
    //  VOLÚMENES — ajustables desde Inspector
    // ══════════════════════════════════════════════════════════

    [Header("── Volúmenes (0-1) ──────────────────────────────")]
    [Range(0f, 1f)] [SerializeField] private float playerAttackVolume  = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float playerShootVolume   = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float playerHitVolume     = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float playerDieVolume     = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float enemyHitVolume      = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float enemyDieVolume      = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float enemyAttackVolume   = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float bullChargeVolume    = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float bullStunVolume      = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float bulletImpactVolume  = 0.6f;
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

    // ── Enemigos ───────────────────────────────────────────────

    public void PlayEnemyHit()
        => Play(enemyHitClip, enemyHitVolume, Random.Range(0.90f, 1.10f));

    public void PlayEnemyDie()
        => Play(enemyDieClip, enemyDieVolume, Random.Range(0.95f, 1.05f));

    public void PlayEnemyAttack()
        => Play(enemyAttackClip, enemyAttackVolume, Random.Range(0.95f, 1.05f));

    // ── Toro ───────────────────────────────────────────────────

    public void PlayBullCharge()
        => Play(bullChargeClip, bullChargeVolume);

    public void PlayBullStun()
        => Play(bullStunClip, bullStunVolume);

    // ── Proyectil ──────────────────────────────────────────────

    public void PlayBulletImpact()
        => Play(bulletImpactClip, bulletImpactVolume, Random.Range(0.90f, 1.10f));

    // ── UI ─────────────────────────────────────────────────────

    public void PlayPowerUpSelect()
        => Play(powerUpSelectClip, powerUpSelectVolume);
}
