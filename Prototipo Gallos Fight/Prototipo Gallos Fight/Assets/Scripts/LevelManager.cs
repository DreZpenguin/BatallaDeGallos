using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("Enemigos de este nivel")]
    [Tooltip("Arrastra aquí los HealthSystem de TODOS los enemigos de la escena.")]
    [SerializeField] private HealthSystem[] enemiesInLevel;

    [Header("Referencia al jugador")]
    [Tooltip("PowerUpManager del jugador. Se busca automáticamente si queda vacío.")]
    [SerializeField] private PowerUpManager powerUpManager;

    [Header("Progresión")]
    [Tooltip("Índice de la escena que se cargará tras elegir el powerup. " +
             "Debe coincidir con el Build Index en File > Build Settings.")]
    [SerializeField] private int nextSceneBuildIndex = -1;

    [Tooltip("Activa esto en el último nivel del juego para no intentar cargar ninguna escena.")]
    [SerializeField] private bool isLastLevel = false;

    [Tooltip("Segundos de espera entre que muere el último enemigo y aparece la pantalla de powerup.")]
    [SerializeField] private float delayBeforePowerUp = 0.8f;

    [SerializeField] private PlayerData PowerUpData;

    // ── Estado interno ─────────────────────────────────────────
    private int _enemiesAlive;
    private bool _levelCompleted = false;

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        if (powerUpManager == null)
            powerUpManager = FindFirstObjectByType<PowerUpManager>();

        if (powerUpManager == null)
            Debug.LogError("[LevelManager] No se encontró PowerUpManager en la escena.");
    }

    private void Start()
    {
        if (enemiesInLevel == null || enemiesInLevel.Length == 0)
        {
            Debug.LogWarning("[LevelManager] No hay enemigos asignados en el Inspector.");
            return;
        }

        _enemiesAlive = enemiesInLevel.Length;

        foreach (HealthSystem hs in enemiesInLevel)
        {
            if (hs == null) continue;
            // Escuchamos la muerte de cada enemigo individualmente
            hs.OnDeath.AddListener(OnEnemyDied);
        }

        Debug.Log($"[LevelManager] Nivel iniciado con {_enemiesAlive} enemigo(s).");
    }

    // ── Callback ───────────────────────────────────────────────

    private void OnEnemyDied()
    {
        if (_levelCompleted) return;

        _enemiesAlive--;
        Debug.Log($"[LevelManager] Enemigo derrotado. Quedan {_enemiesAlive}.");

        if (_enemiesAlive <= 0)
        {
            _levelCompleted = true;
            int scene = SceneManager.GetActiveScene().buildIndex;
            if (scene == 5) {
                PowerUpData.ResetAll(); 
                SceneManager.LoadScene(0);
            }
            StartCoroutine(TriggerPowerUpWithDelay());
        }
    }

    // ── Flujo de powerup y transición ─────────────────────────

    private IEnumerator TriggerPowerUpWithDelay()
    {
        yield return new WaitForSeconds(delayBeforePowerUp);

        if (powerUpManager == null)
        {
            Debug.LogError("[LevelManager] PowerUpManager es null. No se puede mostrar powerup.");
            yield break;
        }

        // Pedimos al PowerUpManager que muestre la selección.
        // Al callback de selección le enganchamos la transición de escena.
        powerUpManager.TriggerPowerUpSelectionWithCallback(OnPowerUpSelected);
        Debug.Log("[LevelManager] Pantalla de selección de poder activada.");
    }

    private void OnPowerUpSelected()
    {
        if (isLastLevel)
        {
            Debug.Log("[LevelManager] Último nivel completado. Fin del juego.");
            // Aquí puedes cargar una escena de créditos / menú principal
           
            PowerUpData.ResetAll();
            SceneManager.LoadScene(0);
            

            
            return;
        }

        if (nextSceneBuildIndex < 0)
        {
            Debug.LogWarning("[LevelManager] nextSceneBuildIndex no configurado. " +
                             "Cargando siguiente escena por índice incremental.");
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            SceneManager.LoadScene(next);
        }
        else
        {
            Debug.Log($"[LevelManager] Cargando siguiente escena con Build Index {nextSceneBuildIndex}.1");

            SceneManager.LoadScene(nextSceneBuildIndex);
        }
    }

    // ── API Pública ────────────────────────────────────────────

    /// Registra en caliente un enemigo que fue instanciado en runtime
    /// (útil si spawneas enemigos por código en vez de ponerlos en escena).
    public void RegisterEnemy(HealthSystem hs)
    {
        if (hs == null || _levelCompleted) return;
        hs.OnDeath.AddListener(OnEnemyDied);
        _enemiesAlive++;
        Debug.Log($"[LevelManager] Enemigo registrado en caliente. Total vivos: {_enemiesAlive}");
    }

}
