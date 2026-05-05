using UnityEngine;

public class PlayerData : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────
    public static PlayerData Instance { get; private set; }

    // ── Datos persistentes ────────────────────────────────────
    [Header("Bonus acumulados (solo lectura en Inspector)")]
    [SerializeField] private float _speedBonus   = 0f;
    [SerializeField] private float _damageBonus  = 0f;
    [SerializeField] private float _rangeBonus   = 0f;
    [SerializeField] private float _healthBonus  = 0f;   // flat HP extra
    [SerializeField] private int   _extraBullets = 0;    // cantidad de veces que se eligió el powerup de disparo

    // Propiedades públicas de solo lectura
    public float SpeedBonus   => _speedBonus;
    public float DamageBonus  => _damageBonus;
    public float RangeBonus   => _rangeBonus;
    public float HealthBonus  => _healthBonus;
    public int   ExtraBullets => _extraBullets;

    // ── Unity Lifecycle ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Carga datos guardados en disco (PlayerPrefs)
        Load();
    }

    // ── API Pública ────────────────────────────────────────────
    public void AddSpeedBonus(float percent)
    {
        _speedBonus += percent;
        Save();
    }

    public void AddDamageBonus(float percent)
    {
        _damageBonus += percent;
        Save();
    }

    public void AddRangeBonus(float flat)
    {
        _rangeBonus += flat;
        Save();
    }

    public void AddHealthBonus(float flat)
    {
        _healthBonus += flat;
        Save();
    }

    public void AddBulletUpgrade()
    {
        _extraBullets++;
        Save();
    }

    /// Resetea todos los datos (útil para "Nueva partida")
    public void ResetAll()
    {
        _speedBonus   = 0f;
        _damageBonus  = 0f;
        _rangeBonus   = 0f;
        _healthBonus  = 0f;
        _extraBullets = 0;
        Save();
        Debug.Log("[PlayerData] Todos los datos reseteados.");
    }

    // ── Persistencia con PlayerPrefs ───────────────────────────
    private const string KEY_SPEED   = "pd_speed";
    private const string KEY_DAMAGE  = "pd_damage";
    private const string KEY_RANGE   = "pd_range";
    private const string KEY_HEALTH  = "pd_health";
    private const string KEY_BULLETS = "pd_bullets";

    public void Save()
    {
        PlayerPrefs.SetFloat(KEY_SPEED,   _speedBonus);
        PlayerPrefs.SetFloat(KEY_DAMAGE,  _damageBonus);
        PlayerPrefs.SetFloat(KEY_RANGE,   _rangeBonus);
        PlayerPrefs.SetFloat(KEY_HEALTH,  _healthBonus);
        PlayerPrefs.SetInt  (KEY_BULLETS, _extraBullets);
        PlayerPrefs.Save();
        Debug.Log("[PlayerData] Datos guardados en PlayerPrefs.");
    }

    public void Load()
    {
        _speedBonus   = PlayerPrefs.GetFloat(KEY_SPEED,   0f);
        _damageBonus  = PlayerPrefs.GetFloat(KEY_DAMAGE,  0f);
        _rangeBonus   = PlayerPrefs.GetFloat(KEY_RANGE,   0f);
        _healthBonus  = PlayerPrefs.GetFloat(KEY_HEALTH,  0f);
        _extraBullets = PlayerPrefs.GetInt  (KEY_BULLETS, 0);
        Debug.Log($"[PlayerData] Datos cargados — Speed:{_speedBonus:F2} Dmg:{_damageBonus:F2} Range:{_rangeBonus:F2} HP:{_healthBonus:F0} Bullets:{_extraBullets}");
    }
}
