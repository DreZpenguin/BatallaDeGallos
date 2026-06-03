// ============================================================
//  PracticeManager.cs
//
//  Modo de práctica. Gestiona:
//   · Un enemigo estacionario con vida infinita que muestra
//     el daño recibido como popup flotante en pantalla.
//   · Tecla configurable para abrir el menú de powerup.
//   · El jugador conserva todos sus powerups y puede probarlos.
//
//  SETUP EN UNITY:
//   1. Crea una escena "Practice" (duplica Lvl1, elimina enemigos
//      y el LevelManager).
//   2. Añade un GameObject vacío "PracticeManager" con este script.
//   3. Crea un prefab de enemigo "PracticeTarget":
//      - Sprite del enemigo que quieras usar de saco de golpes.
//      - Añádele HealthSystem (useInvincibility = false).
//      - Añádele PracticeTarget.cs (script incluido abajo).
//      - NO le añadas ninguna IA (EnemyAI, BullEnemyAI, etc.).
//   4. Asigna el prefab en el campo "targetPrefab".
//   5. Asigna PowerUpUICanvas (el mismo que usas en los niveles).
//   6. Coloca un Transform vacío "targetSpawnPoint" en el centro
//      de la arena y asígnalo.
// ============================================================
using UnityEngine;

public class PracticeManager : MonoBehaviour
{
    [Header("── Referencias ──────────────────────────────────")]
    [Tooltip("Prefab del enemigo estacionario (con PracticeTarget.cs).")]
    [SerializeField] private GameObject targetPrefab;

    [Tooltip("Posición donde spawneará el target. Vacío = origen (0,0,0).")]
    [SerializeField] private Transform targetSpawnPoint;

    [Tooltip("PowerUpUICanvas de la escena (mismo que en los niveles).")]
    [SerializeField] private PowerUpUICanvas powerUpUICanvas;

    [Tooltip("PowerUpManager del jugador.")]
    [SerializeField] private PowerUpManager powerUpManager;

    [Header("── Control ──────────────────────────────────────")]
    [Tooltip("Tecla para abrir el menú de powerup manualmente.")]
    [SerializeField] private KeyCode openPowerUpKey = KeyCode.Tab;

    [Tooltip("Botón de mando para abrir el menú de powerup.")]
    [SerializeField] private KeyCode gamepadOpenKey = KeyCode.JoystickButton6; // Select/Back

    [Header("── UI ───────────────────────────────────────────")]
    [Tooltip("Mostrar instrucciones en pantalla.")]
    [SerializeField] private bool showInstructions = true;

    // ── Estado interno ─────────────────────────────────────────
    private GameObject    _spawnedTarget;
    private GUIStyle      _instructionStyle;
    private bool          _guiInitialized = false;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (powerUpManager == null)
            powerUpManager = FindFirstObjectByType<PowerUpManager>();
        if (powerUpUICanvas == null)
            powerUpUICanvas = FindFirstObjectByType<PowerUpUICanvas>();
    }

    private void Start()
    {
        SpawnTarget();
    }

    private void Update()
    {
        // Abrir menú de powerup con tecla
        if (Input.GetKeyDown(openPowerUpKey) || Input.GetKeyDown(gamepadOpenKey))
            OpenPowerUpMenu();

        // Si el target fue destruido (no debería, pero por si acaso), respawnea
        if (_spawnedTarget == null)
            SpawnTarget();
    }

    // ══════════════════════════════════════════════════════════
    //  SPAWN DEL TARGET
    // ══════════════════════════════════════════════════════════

    private void SpawnTarget()
    {
        if (targetPrefab == null)
        {
            Debug.LogError("[PracticeManager] targetPrefab no asignado.");
            return;
        }

        Vector3 pos = targetSpawnPoint != null
            ? targetSpawnPoint.position
            : Vector3.zero;

        _spawnedTarget = Instantiate(targetPrefab, pos, Quaternion.identity);

        // Asegura que el HealthSystem tenga vida infinita
        HealthSystem hs = _spawnedTarget.GetComponent<HealthSystem>();
        if (hs != null)
            hs.SetInfiniteHealth(true);

        // Desactiva cualquier IA que pueda tener
        DisableAI(_spawnedTarget);

        Debug.Log("[PracticeManager] Target spawneado.");
    }

    private void DisableAI(GameObject target)
    {
        // Desactiva todos los scripts de IA conocidos
        EnemyAI       eai = target.GetComponent<EnemyAI>();
        BullEnemyAI   bai = target.GetComponent<BullEnemyAI>();
        RangedEnemyAI rai = target.GetComponent<RangedEnemyAI>();

        if (eai != null) eai.enabled = false;
        if (bai != null) bai.enabled = false;
        if (rai != null) rai.enabled = false;

        // Congela el Rigidbody para que no se mueva
        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.constraints    = RigidbodyConstraints2D.FreezeAll;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  MENÚ DE POWERUP
    // ══════════════════════════════════════════════════════════

    private void OpenPowerUpMenu()
    {
        if (powerUpManager == null) return;

        // Usa TriggerPowerUpSelection sin callback de transición de escena
        // — al cerrar el menú simplemente vuelve al modo práctica
        powerUpManager.TriggerPowerUpSelectionWithCallback(OnPowerUpSelected);
    }

    private void OnPowerUpSelected()
    {
        Debug.Log("[PracticeManager] Powerup seleccionado. Volviendo a práctica.");
        // No carga ninguna escena — el jugador sigue en práctica
    }

    // ══════════════════════════════════════════════════════════
    //  INSTRUCCIONES EN PANTALLA
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (!showInstructions) return;

        InitGUI();

        float sw = Screen.width;
        float sh = Screen.height;

        string text = $"[{openPowerUpKey}] Abrir menú de powerup   |   " +
                      $"Ataca al enemigo para ver el daño";

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(sw * 0.5f - 260f, sh - 44f, 520f, 30f),
                        Texture2D.whiteTexture);

        GUI.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(sw * 0.5f - 260f, sh - 44f, 520f, 30f),
                  text, _instructionStyle);

        GUI.color = prev;
    }

    private void InitGUI()
    {
        if (_guiInitialized) return;
        _guiInitialized = true;

        _instructionStyle                  = new GUIStyle(GUI.skin.label);
        _instructionStyle.fontSize         = 13;
        _instructionStyle.alignment        = TextAnchor.MiddleCenter;
        _instructionStyle.normal.textColor = Color.white;
    }
}
