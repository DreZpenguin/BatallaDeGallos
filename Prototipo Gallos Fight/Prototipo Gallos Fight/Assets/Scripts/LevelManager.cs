
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
    [Tooltip("Índice del LevelEntry en CutsceneData para el nivel SIGUIENTE (0-based). " +
             "Ignorado si isLastLevel = true. " +
             "Lvl1→1, Lvl2→2, Lvl3→3, Lvl4→4, Lvl5 no aplica.")]
    [SerializeField] private int nextLevelEntryIndex = 1;

    [Tooltip("Nombre de la escena de cutscene. Debe coincidir con Build Settings.")]
    [SerializeField] private string sceneCutscene = "Cutscene";

    [Tooltip("(Obsoleto) Se mantiene por compatibilidad pero ya no se usa para la " +
             "transición — la escena destino se define en CutsceneData.targetScene.")]
    [SerializeField] private int nextSceneBuildIndex = -1;

    [Tooltip("Activa esto en el último nivel del juego para volver al menú en vez de " +
             "cargar la cutscene siguiente.")]
    [SerializeField] private bool isLastLevel = false;

    [Tooltip("Si está activo, al completar el último nivel se muestra el overlay de " +
             "victoria (DeathScreenOverlay, fade-in dentro del mismo Canvas) en vez de " +
             "ir directo al menú.")]
    [SerializeField] private bool useEndGameScreenOnVictory = true;

    [Tooltip("Segundos de espera entre que muere el último enemigo y aparece la pantalla de powerup.")]
    [SerializeField] private float delayBeforePowerUp = 0.8f;

    // ── Estado interno ─────────────────────────────────────────
    private int  _enemiesAlive   = 0;
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

        powerUpManager.TriggerPowerUpSelectionWithCallback(OnPowerUpSelected);
        Debug.Log("[LevelManager] Pantalla de selección de poder activada.");
    }

    private void OnPowerUpSelected()
    {
        // ── Último nivel: vuelve al menú ──────────────────────
        if (isLastLevel)
        {
            Debug.Log("[LevelManager] Último nivel completado. Volviendo al menú.");
            PlayerData.Instance?.ResetAll();
            StatsModal.ClearSavedLevels();

            DeathScreenOverlay overlay = useEndGameScreenOnVictory
                ? (DeathScreenOverlay.Instance ?? FindFirstObjectByType<DeathScreenOverlay>())
                : null;

            if (overlay != null)
            {
                overlay.Show(DeathScreenOverlay.Result.Victory);   // mismo overlay, sin cambiar escena
            }
            else
            {
                SceneManager.LoadScene(0);
            }
            return;
        }

        // ── Niveles intermedios: carga cutscene del siguiente ─
        PlayerPrefs.SetInt(CutsceneScreen.KEY_LEVEL_INDEX, nextLevelEntryIndex);
        PlayerPrefs.Save();

        Debug.Log($"[LevelManager] Cargando cutscene para entry {nextLevelEntryIndex}.");
        SceneManager.LoadScene(sceneCutscene);
    }

    // ── API Pública ────────────────────────────────────────────

    /// Registra en caliente un enemigo instanciado en runtime.
    public void RegisterEnemy(HealthSystem hs)
    {
        if (hs == null || _levelCompleted) return;
        hs.OnDeath.AddListener(OnEnemyDied);
        _enemiesAlive++;
        Debug.Log($"[LevelManager] Enemigo registrado en caliente. Total vivos: {_enemiesAlive}");
    }
}
