// ============================================================
//  StatsModal.cs  — v2
//
//  CAMBIOS respecto a v1:
//   · Los niveles de cada stat ahora persisten entre escenas
//     vía PlayerPrefs (igual que PlayerData / InfiniteData),
//     en vez de vivir solo en memoria del componente.
//   · El componente YA NO usa DontDestroyOnLoad — cada escena
//     tiene su propio StatsModal con su propio Canvas/UI, pero
//     todos leen y escriben los mismos PlayerPrefs.
//   · Nuevo método estático ClearSavedLevels() para borrar el
//     progreso guardado. Debe llamarse:
//       - Al volver al menú principal (PauseManager / LevelManager)
//       - Al morir el jugador (HealthSystem)
//       - Al iniciar una nueva partida del modo Normal (MainMenuManager.PlayNormal)
//   · RefreshAllLevels() ahora se llama en Start() leyendo
//     siempre desde PlayerPrefs, así que cada nueva escena
//     muestra el progreso acumulado correctamente.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatsModal : MonoBehaviour
{
    public static StatsModal Instance { get; private set; }

    // ══════════════════════════════════════════════════════════
    //  PLAYERPREFS KEYS (estáticas — accesibles sin instancia)
    // ══════════════════════════════════════════════════════════

    private const string KEY_RANGE  = "stats_level_range";
    private const string KEY_DAMAGE = "stats_level_damage";
    private const string KEY_SPEED  = "stats_level_speed";
    private const string KEY_HEALTH = "stats_level_health";
    private const string KEY_SHOOT  = "stats_level_shoot";

    /// Borra el progreso de niveles guardado. Llamar al volver al
    /// menú, al morir, o al iniciar una nueva run del modo Normal.
    public static void ClearSavedLevels()
    {
        PlayerPrefs.DeleteKey(KEY_RANGE);
        PlayerPrefs.DeleteKey(KEY_DAMAGE);
        PlayerPrefs.DeleteKey(KEY_SPEED);
        PlayerPrefs.DeleteKey(KEY_HEALTH);
        PlayerPrefs.DeleteKey(KEY_SHOOT);
        PlayerPrefs.Save();
        Debug.Log("[StatsModal] Niveles de stats borrados.");
    }

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════

    [Header("── Panel ────────────────────────────────────────")]
    [SerializeField] private RectTransform statsPanel;
    [SerializeField] private float visibleX = 0f;
    [SerializeField] private float hiddenX  = 400f;

    [Header("── Animación ───────────────────────────────────")]
    [SerializeField] private float slideSpeed = 1200f;

    [Header("── Control ──────────────────────────────────────")]
    [SerializeField] private KeyCode toggleKey         = KeyCode.E;
    [SerializeField] private KeyCode gamepadToggleKey  = KeyCode.JoystickButton3;

    [Header("── Iconos (mismo orden que PowerUpType) ─────────")]
    [SerializeField] private Image iconRange;
    [SerializeField] private Image iconDamage;
    [SerializeField] private Image iconSpeed;
    [SerializeField] private Image iconHealth;
    [SerializeField] private Image iconShoot;

    [Header("── Sprites de iconos ───────────────────────────")]
    [SerializeField] private Sprite spriteRange;
    [SerializeField] private Sprite spriteDamage;
    [SerializeField] private Sprite spriteSpeed;
    [SerializeField] private Sprite spriteHealth;
    [SerializeField] private Sprite spriteShoot;

    [Header("── Textos de nivel ─────────────────────────────")]
    [SerializeField] private TextMeshProUGUI levelRange;
    [SerializeField] private TextMeshProUGUI levelDamage;
    [SerializeField] private TextMeshProUGUI levelSpeed;
    [SerializeField] private TextMeshProUGUI levelHealth;
    [SerializeField] private TextMeshProUGUI levelShoot;

    [Header("── Prefijo de nivel ───────────────────────────")]
    [SerializeField] private string levelPrefix = "LvL ";

    // ══════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ══════════════════════════════════════════════════════════

    private bool      _isVisible    = false;
    private Coroutine _slideRoutine = null;

    // Niveles actuales — se cargan desde PlayerPrefs en Start()
    private int _levelRange;
    private int _levelDamage;
    private int _levelSpeed;
    private int _levelHealth;
    private int _levelShoot;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        // Nota: ya NO se usa singleton con DontDestroyOnLoad.
        // Cada escena tiene su propia instancia conectada a su propio Canvas,
        // pero todas comparten los mismos datos vía PlayerPrefs.
        Instance = this;

        if (statsPanel != null)
        {
            Vector2 pos = statsPanel.anchoredPosition;
            pos.x = hiddenX;
            statsPanel.anchoredPosition = pos;
        }
    }

    private void Start()
    {
        AssignSprite(iconRange,  spriteRange);
        AssignSprite(iconDamage, spriteDamage);
        AssignSprite(iconSpeed,  spriteSpeed);
        AssignSprite(iconHealth, spriteHealth);
        AssignSprite(iconShoot,  spriteShoot);

        LoadLevelsFromPrefs();
        RefreshAllLevels();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        bool pressed = Input.GetKeyDown(toggleKey)
                    || Input.GetKeyDown(gamepadToggleKey);
        if (pressed)
            Toggle();
    }

    // ══════════════════════════════════════════════════════════
    //  API PÚBLICA
    // ══════════════════════════════════════════════════════════

    public void Show()
    {
        if (_isVisible) return;
        _isVisible = true;
        SlideTo(visibleX);
    }

    public void Hide()
    {
        if (!_isVisible) return;
        _isVisible = false;
        SlideTo(hiddenX);
    }

    public void Toggle()
    {
        if (_isVisible) Hide();
        else            Show();
    }

    /// Registra una mejora aplicada, actualiza el nivel en pantalla
    /// Y lo guarda en PlayerPrefs para que persista entre escenas.
    public void RegisterUpgrade(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Range:
                _levelRange++;
                SetLevelText(levelRange, _levelRange);
                PlayerPrefs.SetInt(KEY_RANGE, _levelRange);
                break;
            case PowerUpType.Damage:
                _levelDamage++;
                SetLevelText(levelDamage, _levelDamage);
                PlayerPrefs.SetInt(KEY_DAMAGE, _levelDamage);
                break;
            case PowerUpType.Speed:
                _levelSpeed++;
                SetLevelText(levelSpeed, _levelSpeed);
                PlayerPrefs.SetInt(KEY_SPEED, _levelSpeed);
                break;
            case PowerUpType.Health:
                _levelHealth++;
                SetLevelText(levelHealth, _levelHealth);
                PlayerPrefs.SetInt(KEY_HEALTH, _levelHealth);
                break;
            case PowerUpType.Shoot:
                _levelShoot++;
                SetLevelText(levelShoot, _levelShoot);
                PlayerPrefs.SetInt(KEY_SHOOT, _levelShoot);
                break;
        }
        PlayerPrefs.Save();
    }

    /// Resetea los niveles en memoria Y en PlayerPrefs.
    /// Equivalente a llamar StatsModal.ClearSavedLevels() + refrescar UI.
    public void ResetLevels()
    {
        ClearSavedLevels();
        LoadLevelsFromPrefs();
        RefreshAllLevels();
    }

    // ══════════════════════════════════════════════════════════
    //  PERSISTENCIA
    // ══════════════════════════════════════════════════════════

    private void LoadLevelsFromPrefs()
    {
        _levelRange  = PlayerPrefs.GetInt(KEY_RANGE,  1);
        _levelDamage = PlayerPrefs.GetInt(KEY_DAMAGE, 1);
        _levelSpeed  = PlayerPrefs.GetInt(KEY_SPEED,  1);
        _levelHealth = PlayerPrefs.GetInt(KEY_HEALTH, 1);
        _levelShoot  = PlayerPrefs.GetInt(KEY_SHOOT,  1);
    }

    // ══════════════════════════════════════════════════════════
    //  ANIMACIÓN
    // ══════════════════════════════════════════════════════════

    private void SlideTo(float targetX)
    {
        if (_slideRoutine != null)
            StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideRoutine(targetX));
    }

    private IEnumerator SlideRoutine(float targetX)
    {
        while (true)
        {
            Vector2 pos     = statsPanel.anchoredPosition;
            float   current = pos.x;
            float   next    = Mathf.MoveTowards(current, targetX,
                                                 slideSpeed * Time.unscaledDeltaTime);
            pos.x = next;
            statsPanel.anchoredPosition = pos;

            if (Mathf.Abs(next - targetX) < 0.5f)
            {
                pos.x = targetX;
                statsPanel.anchoredPosition = pos;
                break;
            }
            yield return null;
        }
        _slideRoutine = null;
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private void RefreshAllLevels()
    {
        SetLevelText(levelRange,  _levelRange);
        SetLevelText(levelDamage, _levelDamage);
        SetLevelText(levelSpeed,  _levelSpeed);
        SetLevelText(levelHealth, _levelHealth);
        SetLevelText(levelShoot,  _levelShoot);
    }

    private void SetLevelText(TextMeshProUGUI tmp, int level)
    {
        if (tmp != null)
            tmp.text = $"{levelPrefix}{level}";
    }

    private void AssignSprite(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite         = sprite;
        img.preserveAspect = true;
        img.enabled        = true;
    }
}
