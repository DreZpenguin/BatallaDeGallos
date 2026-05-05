using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header("Bonus por powerup")]
    [SerializeField] private float rangeBonus  = 0.5f;
    [SerializeField] private float damageBonus = 0.25f;
    [SerializeField] private float speedBonus  = 0.30f;
    [SerializeField] private float healthBonus = 50f;

    [Header("Referencias")]
    [SerializeField] private PowerUpUICanvas powerUpUICanvas;
    [SerializeField] private HitboxFront hitboxFront;

    private AttackController   _attackController;
    private PlayerMovement2D   _playerMovement;
    private HealthSystem       _healthSystem;
    private ShootingController _shootingController;

    private float _totalRangeBonus = 0f;

    private const int OPTIONS_TO_SHOW = 3;

    // Callback externo (LevelManager lo usa para saber cuándo transicionar)
    private Action _onPowerUpSelectedCallback;

    private void Awake()
    {
        _attackController   = GetComponent<AttackController>();
        _playerMovement     = GetComponent<PlayerMovement2D>();
        _healthSystem       = GetComponent<HealthSystem>();
        _shootingController = GetComponent<ShootingController>();

        if (powerUpUICanvas == null)
            powerUpUICanvas = FindFirstObjectByType<PowerUpUICanvas>();
        if (hitboxFront == null)
            hitboxFront = GetComponentInChildren<HitboxFront>();

        if (powerUpUICanvas == null)
            Debug.LogError("[PowerUpManager] No se encontró PowerUpUICanvas.");
        if (hitboxFront == null)
            Debug.LogWarning("[PowerUpManager] No se encontró HitboxFront en los hijos del jugador.");
    }

    private void Start()
    {
        ApplySavedData();

        // Registro de enemigos existentes en escena
        // (Los enemigos del LevelManager también son registrados aquí
        //  para el sistema de powerup heredado; LevelManager lleva su
        //  propio contador de muertes independiente.)
        HealthSystem[] allHealth = FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
        int registered = 0;
        foreach (HealthSystem hs in allHealth)
        {
            if (hs.GetComponentInParent<PowerUpManager>() != null) continue;
            if (hs.gameObject == gameObject) continue;
            registered++;
        }
        Debug.Log($"[PowerUpManager] {registered} enemigo(s) encontrado(s) en escena.");
    }

    // ── Aplicar datos guardados ─────────────────────────────────

    private void ApplySavedData()
    {
        if (PlayerData.Instance == null) return;

        if (PlayerData.Instance.SpeedBonus > 0f)
            _playerMovement?.AddSpeedBonus(PlayerData.Instance.SpeedBonus);

        if (PlayerData.Instance.DamageBonus > 0f)
            _attackController?.AddDamageBonus(PlayerData.Instance.DamageBonus);

        if (PlayerData.Instance.RangeBonus > 0f)
        {
            _totalRangeBonus = PlayerData.Instance.RangeBonus;
            hitboxFront?.SetRangeTotal(_totalRangeBonus);
        }

        if (PlayerData.Instance.ExtraBullets > 0)
            _shootingController?.SetBulletUpgradeStacks(PlayerData.Instance.ExtraBullets);

        Debug.Log("[PowerUpManager] Bonus de PlayerData aplicados.");
    }

    // ── Registro de enemigos (API heredada) ─────────────────────

    public void RegisterEnemy(HealthSystem enemyHealth)
    {
        if (enemyHealth == null) return;
        Debug.Log($"[PowerUpManager] Enemigo registrado (sin callback de nivel): {enemyHealth.gameObject.name}");
    }

    // ── API de Selección de PowerUp ────────────────────────────

    /// Muestra la pantalla de selección y, al elegir, invoca <paramref name="onSelected"/>.
    /// Llamado por LevelManager cuando todos los enemigos del nivel han muerto.
    public void TriggerPowerUpSelectionWithCallback(Action onSelected)
    {
        _onPowerUpSelectedCallback = onSelected;
        ShowPowerUpScreen();
    }

    /// Versión sin callback (testing desde ContextMenu o uso standalone).
    [ContextMenu("Trigger PowerUp Selection (Test)")]
    public void TriggerPowerUpSelection() => ShowPowerUpScreen();

    // ── Pantalla de selección ───────────────────────────────────

    private void ShowPowerUpScreen()
    {
        if (powerUpUICanvas == null)
        {
            Debug.LogError("[PowerUpManager] powerUpUICanvas es null.");
            return;
        }

        List<PowerUpUICanvas.PowerUpOption> pool = new List<PowerUpUICanvas.PowerUpOption>
        {
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Range,
                title       = "+RANGO",
                description = $"Aumenta el tamaño de tu hitbox\n+{rangeBonus:F2} unidades",
                hexColor    = "#38D1FF"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Damage,
                title       = "+DAÑO",
                description = $"Aumenta el daño de tu ataque\n+{damageBonus * 100f:F0}% del base",
                hexColor    = "#FF4D3A"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Speed,
                title       = "+VELOCIDAD",
                description = $"Aumenta tu velocidad de movimiento\n+{speedBonus * 100f:F0}% de la base",
                hexColor    = "#4DFF66"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Health,
                title       = "+VIDA",
                description = $"Aumenta tu vida máxima\n+{healthBonus:F0} HP",
                hexColor    = "#FFD93D"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Shoot,
                title       = "+DISPARO",
                description = "Mejora tu proyectil\n+Daño, +Velocidad, +Duración",
                hexColor    = "#C77DFF"
            }
        };

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1); PowerUpUICanvas.PowerUpOption temp = pool[i];
            pool[i] = pool[j];
            pool[j] = temp;
        }

        int count = Mathf.Min(OPTIONS_TO_SHOW, pool.Count);
        PowerUpUICanvas.PowerUpOption[] options = new PowerUpUICanvas.PowerUpOption[count];
        for (int i = 0; i < count; i++)
            options[i] = pool[i];

        Time.timeScale = 0f;
        powerUpUICanvas.Show(options, ApplyPowerUp);
        Debug.Log($"[PowerUpManager] Pantalla de powerup activada con {count} opciones.");
    }

    // ── Aplicación del PowerUp ──────────────────────────────────

    private void ApplyPowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Range:
                _totalRangeBonus += rangeBonus;
                hitboxFront?.SetRangeTotal(_totalRangeBonus);
                PlayerData.Instance?.AddRangeBonus(rangeBonus);
                Debug.Log($"[PowerUpManager] +Rango. Total: {_totalRangeBonus:F2}");
                break;

            case PowerUpType.Damage:
                _attackController?.AddDamageBonus(damageBonus);
                PlayerData.Instance?.AddDamageBonus(damageBonus);
                break;

            case PowerUpType.Speed:
                _playerMovement?.AddSpeedBonus(speedBonus);
                PlayerData.Instance?.AddSpeedBonus(speedBonus);
                break;

            case PowerUpType.Health:
                _healthSystem?.AddMaxHealthBonus(healthBonus);
                PlayerData.Instance?.AddHealthBonus(healthBonus);
                Debug.Log($"[PowerUpManager] +Vida. HP extra: {healthBonus:F0}");
                break;

            case PowerUpType.Shoot:
                _shootingController?.AddBulletStack();
                PlayerData.Instance?.AddBulletUpgrade();
                Debug.Log("[PowerUpManager] +Disparo aplicado.");
                break;
        }

        Time.timeScale = 1f;
        Debug.Log($"[PowerUpManager] Powerup '{type}' aplicado.");

        // Notifica al LevelManager (u otro sistema) que la selección terminó
        Action callback = _onPowerUpSelectedCallback;
        _onPowerUpSelectedCallback = null;
        callback?.Invoke();
    }
}
