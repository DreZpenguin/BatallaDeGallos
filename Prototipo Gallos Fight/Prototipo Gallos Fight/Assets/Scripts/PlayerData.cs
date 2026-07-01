
using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    [Header("Bonus acumulados (solo lectura en Inspector)")]
    [SerializeField] private float _speedBonus        = 0f;
    [SerializeField] private float _damageBonus       = 0f;
    [SerializeField] private float _healthBonus       = 0f;
    [SerializeField] private int   _extraBullets      = 0;  // stacks daño bala (Shoot)
    [SerializeField] private int   _bulletRangeStacks = 0;  // stacks vel+knockback (Range)

    public float SpeedBonus        => _speedBonus;
    public float DamageBonus       => _damageBonus;
    public float HealthBonus       => _healthBonus;
    public int   ExtraBullets      => _extraBullets;
    public int   BulletRangeStacks => _bulletRangeStacks;

    // Retrocompatibilidad: propiedad RangeBonus devuelve 0 siempre
    // (el sistema de hitbox ya no existe para el jugador)
    public float RangeBonus => 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ── API Pública ────────────────────────────────────────────

    public void AddSpeedBonus(float percent)       { _speedBonus   += percent; Save(); }
    public void AddDamageBonus(float percent)      { _damageBonus  += percent; Save(); }
    public void AddHealthBonus(float flat)         { _healthBonus  += flat;    Save(); }

    public void AddBulletUpgrade()
    {
        _extraBullets++;
        Save();
        Debug.Log($"[PlayerData] +Disparo (daño). Total stacks: {_extraBullets}");
    }

    public void AddBulletRangeStack()
    {
        _bulletRangeStacks++;
        Save();
        Debug.Log($"[PlayerData] +Rango (vel+knockback). Total stacks: {_bulletRangeStacks}");
    }

    // Retrocompatibilidad
    public void AddRangeBonus(float flat)
    {
        // Convierte a un stack de rango (1 stack por llamada)
        AddBulletRangeStack();
    }

    public void ResetAll()
    {
        _speedBonus        = 0f;
        _damageBonus       = 0f;
        _healthBonus       = 0f;
        _extraBullets      = 0;
        _bulletRangeStacks = 0;
        Save();
        Debug.Log("[PlayerData] Todos los datos reseteados.");
    }

    // ── Persistencia ───────────────────────────────────────────

    private const string KEY_SPEED        = "pd_speed";
    private const string KEY_DAMAGE       = "pd_damage";
    private const string KEY_HEALTH       = "pd_health";
    private const string KEY_BULLETS      = "pd_bullets";
    private const string KEY_BULLET_RANGE = "pd_bullet_range";

    public void Save()
    {
        PlayerPrefs.SetFloat(KEY_SPEED,        _speedBonus);
        PlayerPrefs.SetFloat(KEY_DAMAGE,       _damageBonus);
        PlayerPrefs.SetFloat(KEY_HEALTH,       _healthBonus);
        PlayerPrefs.SetInt  (KEY_BULLETS,      _extraBullets);
        PlayerPrefs.SetInt  (KEY_BULLET_RANGE, _bulletRangeStacks);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        _speedBonus        = PlayerPrefs.GetFloat(KEY_SPEED,        0f);
        _damageBonus       = PlayerPrefs.GetFloat(KEY_DAMAGE,       0f);
        _healthBonus       = PlayerPrefs.GetFloat(KEY_HEALTH,       0f);
        _extraBullets      = PlayerPrefs.GetInt  (KEY_BULLETS,      0);
        _bulletRangeStacks = PlayerPrefs.GetInt  (KEY_BULLET_RANGE, 0);
        Debug.Log($"[PlayerData] Cargado — Spd:{_speedBonus:F2} Dmg:{_damageBonus:F2} " +
                  $"HP:{_healthBonus:F0} Balas:{_extraBullets} Rango:{_bulletRangeStacks}");
    }
}
