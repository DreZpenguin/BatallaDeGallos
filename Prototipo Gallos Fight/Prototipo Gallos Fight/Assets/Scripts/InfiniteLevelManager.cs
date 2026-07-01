
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InfiniteLevelManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────

    [Header("── Configuración de Escalado ───────────────────")]
    [Tooltip("Arrastra aquí EnemyScalingConfig (v1 curvas) o EnemyScalingConfigV2 (tabla). " +
             "Ambos son compatibles — puedes cambiar entre ellos sin tocar código.")]
    [SerializeField] private EnemyScalingConfigBase scalingConfig;

    [Header("── Prefabs de Enemigos ─────────────────────────")]
    [Tooltip("Prefab del enemigo normal (EnemyAI + HealthSystem).")]
    [SerializeField] private GameObject normalEnemyPrefab;

    [Tooltip("Prefab de la variante del enemigo normal. Comparte la misma IA (EnemyAI) " +
             "pero puede tener stats base, sprite o comportamiento distintos.")]
    [SerializeField] private GameObject variantEnemyPrefab;

    [Tooltip("Prefab del enemigo a distancia (RangedEnemyAI + HealthSystem).")]
    [SerializeField] private GameObject rangedEnemyPrefab;

    [Tooltip("Prefab del toro (BullEnemyAI + HealthSystem).")]
    [SerializeField] private GameObject bullEnemyPrefab;

    [Header("── Puntos de Spawn ──────────────────────────────")]
    [Tooltip("GameObjects vacíos que definen dónde pueden aparecer los enemigos. " +
             "Colócalos dentro de la arena sin que se solapen con obstáculos.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Distancia mínima al jugador para considerar un spawnPoint válido.")]
    [SerializeField] private float minSpawnDistToPlayer = 3f;

    [Header("── Referencias ──────────────────────────────────")]
    [Tooltip("PowerUpManager del jugador. Se busca automáticamente si queda vacío.")]
    [SerializeField] private PowerUpManager powerUpManager;

    [Tooltip("HealthSystem del jugador. Se busca automáticamente si queda vacío.")]
    [SerializeField] private HealthSystem playerHealthSystem;

    [Tooltip("CircleCollider2D de la arena (para asignarlo a los enemigos).")]
    [SerializeField] private CircleCollider2D arenaCollider;

    [Header("── Tiempos ──────────────────────────────────────")]
    [Tooltip("Segundos de espera entre que muere el último enemigo y aparece el powerup.")]
    [SerializeField] private float delayBeforePowerUp = 0.8f;

    [Tooltip("Segundos de espera entre el powerup y el spawn de la siguiente oleada " +
             "(solo si no se recarga la escena — ver reloadSceneEachWave).")]
    [SerializeField] private float delayBeforeSpawn = 0.5f;

    [Tooltip("Si está activo, recarga la escena entre oleadas (más limpio, " +
             "evita acumulación de objetos). Recomendado: ON.")]
    [SerializeField] private bool reloadSceneEachWave = true;

    [Header("── UI Oleada ─────────────────────────────────────")]
    [Tooltip("Texto opcional para mostrar el número de oleada. " +
             "Asigna un TextMeshProUGUI si quieres mostrarlo.")]
    [SerializeField] private TMPro.TextMeshProUGUI waveLabel;

    // ── Estado interno ─────────────────────────────────────────

    private int  _currentWave    = 1;
    private int  _enemiesAlive   = 0;
    private bool _waveCompleted  = false;
    private List<GameObject> _spawnedEnemies = new List<GameObject>();

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        // Busca referencias automáticamente si no están asignadas
        if (powerUpManager == null)
            powerUpManager = FindFirstObjectByType<PowerUpManager>();

        if (playerHealthSystem == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerHealthSystem = p.GetComponent<HealthSystem>();
        }

        if (arenaCollider == null)
            arenaCollider = FindFirstObjectByType<CircleCollider2D>();
    }

    private void Start()
    {
        // Obtiene la oleada actual de InfiniteData
        if (InfiniteData.Instance != null && InfiniteData.Instance.IsInfiniteMode)
        {
            _currentWave = InfiniteData.Instance.Wave;

            // Restaura HP del jugador si hay uno guardado (sin regenerar entre oleadas)
            float savedHP = InfiniteData.Instance.PlayerHP;
            if (savedHP > 0f && playerHealthSystem != null)
            {
                // Forzamos el HP directamente: lo curamos desde 1 hasta el valor guardado
                // Primero llevamos a 1 de HP (sin morir) y luego curamos hasta savedHP
                RestorePlayerHP(savedHP);
            }
        }
        else
        {
            // Sin InfiniteData → crea uno de emergencia (por si se entra directamente en editor)
            _currentWave = 1;
            Debug.LogWarning("[InfiniteLevelManager] InfiniteData no encontrado. Iniciando en oleada 1.");
        }

        UpdateWaveLabel();

        // Suscribe al evento de muerte del jugador
        if (playerHealthSystem != null)
            playerHealthSystem.OnDeath.AddListener(OnPlayerDied);

        StartCoroutine(SpawnWaveRoutine());
    }

    // ══════════════════════════════════════════════════════════
    //  RESTAURAR HP DEL JUGADOR
    // ══════════════════════════════════════════════════════════

    /// Restaura el HP del jugador al valor guardado sin exceder su MaxHealth.
    /// Usa reflexión interna de HealthSystem a través de Heal.
    private void RestorePlayerHP(float targetHP)
    {
        if (playerHealthSystem == null) return;

        // Curamos desde el HP inicial (que HealthSystem pone en MaxHealth al Awake)
        // hasta el HP guardado. Si savedHP < MaxHealth, el jugador empieza herido.
        float current = playerHealthSystem.CurrentHealth;
        float diff    = targetHP - current;

        if (diff > 0f)
            playerHealthSystem.Heal(diff);
        else if (diff < 0f)
            // TakeDamage sin dirección ni knockback — solo reduce HP
            playerHealthSystem.TakeDamage(-diff);

        Debug.Log($"[InfiniteLevelManager] HP restaurado a {targetHP:F0}");
    }

    // ══════════════════════════════════════════════════════════
    //  SPAWN DE OLEADA
    // ══════════════════════════════════════════════════════════

    private IEnumerator SpawnWaveRoutine()
    {
        yield return new WaitForSeconds(delayBeforeSpawn);

        _waveCompleted = false;
        _enemiesAlive  = 0;
        _spawnedEnemies.Clear();

        if (scalingConfig == null)
        {
            Debug.LogError("[InfiniteLevelManager] scalingConfig no asignado.");
            yield break;
        }

        int wave = _currentWave;
        int normalCount  = scalingConfig.GetNormalCount(wave);
        int variantCount = scalingConfig.GetVariantCount(wave);
        int rangedCount  = scalingConfig.GetRangedCount(wave);
        int bullCount    = scalingConfig.GetBullCount(wave);

        Debug.Log($"[InfiniteLevelManager] Oleada {wave}: " +
                  $"{normalCount} normales, {variantCount} variantes, " +
                  $"{rangedCount} a distancia, {bullCount} toros.");

        // Spawn de cada tipo
        SpawnEnemies(normalEnemyPrefab,  normalCount,  wave);
        SpawnEnemies(variantEnemyPrefab, variantCount, wave);
        SpawnEnemies(rangedEnemyPrefab,  rangedCount,  wave);
        SpawnEnemies(bullEnemyPrefab,    bullCount,    wave);

        if (_enemiesAlive == 0)
        {
            Debug.LogWarning("[InfiniteLevelManager] No se spawneó ningún enemigo. " +
                             "Revisa los prefabs y el ScalingConfig.");
        }
    }

    private void SpawnEnemies(GameObject prefab, int count, int wave)
    {
        if (prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector2 spawnPos = GetSpawnPosition();
            if (spawnPos == Vector2.zero) continue; // sin punto válido

            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

            // Aplica escalado de stats
            ApplyScaling(enemy, wave);

            // Asigna la arena al enemigo si la necesita
            AssignArena(enemy);

            // Registra la muerte
            HealthSystem hs = enemy.GetComponent<HealthSystem>();
            if (hs != null)
            {
                hs.OnDeath.AddListener(OnEnemyDied);
                _enemiesAlive++;
            }

            _spawnedEnemies.Add(enemy);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ESCALADO DE STATS
    // ══════════════════════════════════════════════════════════

    private void ApplyScaling(GameObject enemy, int wave)
    {
        HealthSystem hs = enemy.GetComponent<HealthSystem>();
        if (hs != null)
        {
            // Añade HP extra según la curva de escalado
            float baseHP    = hs.MaxHealth;
            float targetHP  = baseHP * scalingConfig.GetHealthMult(wave);
            float bonusHP   = targetHP - baseHP;
            if (bonusHP > 0f)
                hs.AddMaxHealthBonus(bonusHP);
        }

        // Escalado de daño en AttackController (enemigos melee)
        AttackController ac = enemy.GetComponent<AttackController>();
        if (ac != null)
        {
            float mult = scalingConfig.GetDamageMult(wave) - 1f; // bonus sobre el 100% base
            if (mult > 0f)
                ac.AddDamageBonus(mult);
        }

        // Escalado de velocidad en EnemyAI (melee)
        EnemyAI eai = enemy.GetComponent<EnemyAI>();
        if (eai != null)
            eai.ApplySpeedScale(scalingConfig.GetSpeedMult(wave));

        // Escalado específico de RangedEnemyAI
        RangedEnemyAI rai = enemy.GetComponent<RangedEnemyAI>();
        if (rai != null)
            rai.ApplyScaling(
                scalingConfig.GetDamageMult(wave),
                scalingConfig.GetBulletSpeedMult(wave),
                scalingConfig.GetShootCooldownMult(wave),
                scalingConfig.GetSpeedMult(wave));

        // Escalado específico de BullEnemyAI
        BullEnemyAI bai = enemy.GetComponent<BullEnemyAI>();
        if (bai != null)
            bai.ApplyScaling(
                scalingConfig.GetDamageMult(wave),
                scalingConfig.GetSpeedMult(wave));
    }

    private void AssignArena(GameObject enemy)
    {
        if (arenaCollider == null) return;

        RangedEnemyAI rai = enemy.GetComponent<RangedEnemyAI>();
        if (rai != null) rai.SetArena(arenaCollider);

        BullEnemyAI bai = enemy.GetComponent<BullEnemyAI>();
        if (bai != null) bai.SetArena(arenaCollider);
    }

    // ══════════════════════════════════════════════════════════
    //  PUNTOS DE SPAWN
    // ══════════════════════════════════════════════════════════

    private Vector2 GetSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[InfiniteLevelManager] Sin spawnPoints asignados.");
            return Vector2.zero;
        }

        // Filtra puntos demasiado cerca del jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerPos = player != null ? (Vector2)player.transform.position : Vector2.zero;

        List<Transform> valid = new List<Transform>();
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            if (Vector2.Distance(sp.position, playerPos) >= minSpawnDistToPlayer)
                valid.Add(sp);
        }

        if (valid.Count == 0) valid.AddRange(spawnPoints); // fallback: usa todos

        // Añade un pequeño offset aleatorio para que no se apilen
        Transform chosen = valid[Random.Range(0, valid.Count)];
        Vector2   offset = Random.insideUnitCircle * 0.5f;
        return (Vector2)chosen.position + offset;
    }

    // ══════════════════════════════════════════════════════════
    //  CALLBACKS
    // ══════════════════════════════════════════════════════════

    private void OnEnemyDied()
    {
        if (_waveCompleted) return;

        _enemiesAlive--;
        Debug.Log($"[InfiniteLevelManager] Enemigo derrotado. Quedan {_enemiesAlive}.");

        if (_enemiesAlive <= 0)
        {
            _waveCompleted = true;
            StartCoroutine(WaveCompleteRoutine());
        }
    }

    private void OnPlayerDied()
    {
        Debug.Log("[InfiniteLevelManager] Jugador muerto. Fin de la run infinita.");

        if (InfiniteData.Instance != null)
            InfiniteData.Instance.ResetInfiniteRun();

        // Resetea también los powerups del jugador
        if (PlayerData.Instance != null)
            PlayerData.Instance.ResetAll();

        // Vuelve al menú
        SceneManager.LoadScene(0);
    }

    // ══════════════════════════════════════════════════════════
    //  COMPLETAR OLEADA
    // ══════════════════════════════════════════════════════════

    private IEnumerator WaveCompleteRoutine()
    {
        yield return new WaitForSeconds(delayBeforePowerUp);

        if (powerUpManager == null)
        {
            Debug.LogError("[InfiniteLevelManager] PowerUpManager es null.");
            AdvanceWaveAndContinue();
            yield break;
        }

        powerUpManager.TriggerPowerUpSelectionWithCallback(AdvanceWaveAndContinue);
    }

    private void AdvanceWaveAndContinue()
    {
        // Guarda HP actual antes de recargar
        float currentHP = playerHealthSystem != null
            ? playerHealthSystem.CurrentHealth
            : -1f;

        if (InfiniteData.Instance != null)
            InfiniteData.Instance.AdvanceWave(currentHP);

        if (reloadSceneEachWave)
        {
            // Recarga la misma escena limpia
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            // Sin recargar: spawnea la siguiente oleada en la misma sesión
            _currentWave++;
            UpdateWaveLabel();
            StartCoroutine(SpawnWaveRoutine());
        }
    }

    // ══════════════════════════════════════════════════════════
    //  UI
    // ══════════════════════════════════════════════════════════

    private void UpdateWaveLabel()
    {
        if (waveLabel != null)
            waveLabel.text = $"Oleada {_currentWave}";
    }
}
