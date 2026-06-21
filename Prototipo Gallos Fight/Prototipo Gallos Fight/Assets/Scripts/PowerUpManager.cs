// ============================================================
//  PowerUpManager.cs  — v2
//  Cambios respecto a v1:
//   · PowerUpType.Range → ya NO modifica la hitbox melee.
//     Ahora aumenta velocidad + knockback del disparo.
//   · PowerUpType.Shoot → solo aumenta el daño del disparo.
//   · Se elimina la referencia a HitboxFront.
//   · ApplySavedData carga los nuevos stacks separados.
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header("Bonus por powerup")]
    [SerializeField] private float damageBonus    = 0.25f;
    [SerializeField] private float speedBonus     = 0.30f;
    [SerializeField] private float healthBonus    = 50f;

    [Header("Bonus de disparo (PowerUp Rango)")]
    [Tooltip("Velocidad plana que se añade a la bala por cada stack de Rango.")]
    [SerializeField] private float bulletSpeedBonus     = 2f;
    [Tooltip("Bonus de knockback multiplicador por cada stack de Rango.")]
    [SerializeField] private float bulletKnockbackBonus = 0.5f;

    [Header("Bonus de disparo (PowerUp Disparo)")]
    [Tooltip("Daño plano que se añade a la bala por cada stack de Disparo.")]
    [SerializeField] private float bulletDamageBonus = 5f;

    [Header("Referencias")]
    [SerializeField] private PowerUpUICanvas powerUpUICanvas;

    private AttackController   _attackController;
    private PlayerMovement2D   _playerMovement;
    private HealthSystem       _healthSystem;
    private ShootingController _shootingController;

    private const int OPTIONS_TO_SHOW = 3;

    private Action _onPowerUpSelectedCallback;

    private void Awake()
    {
        _attackController   = GetComponent<AttackController>();
        _playerMovement     = GetComponent<PlayerMovement2D>();
        _healthSystem       = GetComponent<HealthSystem>();
        _shootingController = GetComponent<ShootingController>();

        if (powerUpUICanvas == null)
            powerUpUICanvas = FindFirstObjectByType<PowerUpUICanvas>();

        if (powerUpUICanvas == null)
            Debug.LogError("[PowerUpManager] No se encontró PowerUpUICanvas.");
    }

    private void Start()
    {
        ApplySavedData();
    }

    // ── Aplicar datos guardados ─────────────────────────────────

    private void ApplySavedData()
    {
        if (PlayerData.Instance == null) return;

        if (PlayerData.Instance.SpeedBonus > 0f)
            _playerMovement?.AddSpeedBonus(PlayerData.Instance.SpeedBonus);

        if (PlayerData.Instance.DamageBonus > 0f)
            _attackController?.AddDamageBonus(PlayerData.Instance.DamageBonus);

        if (PlayerData.Instance.ExtraBullets > 0)
            _shootingController?.SetBulletDamageStacks(PlayerData.Instance.ExtraBullets);

        if (PlayerData.Instance.BulletRangeStacks > 0)
            _shootingController?.SetBulletRangeStacks(PlayerData.Instance.BulletRangeStacks);

        Debug.Log("[PowerUpManager] Bonus de PlayerData aplicados.");
    }

    // ── Registro de enemigos (API heredada) ─────────────────────

    public void RegisterEnemy(HealthSystem enemyHealth) { }

    // ── API de Selección de PowerUp ────────────────────────────

    public void TriggerPowerUpSelectionWithCallback(Action onSelected)
    {
        _onPowerUpSelectedCallback = onSelected;
        ShowPowerUpScreen();
    }

    [ContextMenu("Trigger PowerUp Selection (Test)")]
    public void TriggerPowerUpSelection() => ShowPowerUpScreen();

    // ── Pantalla de selección ───────────────────────────────────

    private void ShowPowerUpScreen()
    {
        if (powerUpUICanvas == null) return;

        List<PowerUpUICanvas.PowerUpOption> pool = new List<PowerUpUICanvas.PowerUpOption>
        {
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Range,
                title       = "Disparo potente",
                description = $"Mejora el proyectil\n+{bulletSpeedBonus:F0} velocidad, +{bulletKnockbackBonus:F1}× knockback",
                hexColor    = "#38D1FF"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Damage,
                title       = "Daño",
                description = $"Aumenta el daño de tu ataque\n+{damageBonus * 100f:F0}% del base",
                hexColor    = "#FF4D3A"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Speed,
                title       = "Velocidad",
                description = $"Aumenta tu velocidad\n+{speedBonus * 100f:F0}% de la base",
                hexColor    = "#4DFF66"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Health,
                title       = "Vida",
                description = $"Aumenta tu vida máxima\n+{healthBonus:F0} HP",
                hexColor    = "#FFD93D"
            },
            new PowerUpUICanvas.PowerUpOption
            {
                id          = PowerUpType.Shoot,
                title       = "Daño de bala",
                description = $"Aumenta el daño del proyectil\n+{bulletDamageBonus:F0} de daño",
                hexColor    = "#C77DFF"
            }
        };

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            PowerUpUICanvas.PowerUpOption temp = pool[i];
            pool[i] = pool[j];
            pool[j] = temp;
        }

        int count = Mathf.Min(OPTIONS_TO_SHOW, pool.Count);
        PowerUpUICanvas.PowerUpOption[] options = new PowerUpUICanvas.PowerUpOption[count];
        for (int i = 0; i < count; i++)
            options[i] = pool[i];

        Time.timeScale = 0f;
        powerUpUICanvas.Show(options, ApplyPowerUp);
    }

    // ── Aplicación del PowerUp ──────────────────────────────────

    private void ApplyPowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Range:
                // Velocidad + knockback del disparo
                _shootingController?.AddBulletSpeedBonus(bulletSpeedBonus);
                _shootingController?.AddBulletKnockbackBonus(bulletKnockbackBonus);
                PlayerData.Instance?.AddBulletRangeStack();
                Debug.Log($"[PowerUpManager] +Rango disparo. Vel+{bulletSpeedBonus} Knock+{bulletKnockbackBonus}×");
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
                break;

            case PowerUpType.Shoot:
                // Solo daño de bala
                _shootingController?.AddBulletDamageBonus(bulletDamageBonus);
                PlayerData.Instance?.AddBulletUpgrade();
                Debug.Log($"[PowerUpManager] +Daño bala: {bulletDamageBonus:F0}");
                break;
        }

        Time.timeScale = 1f;

        Action callback = _onPowerUpSelectedCallback;
        _onPowerUpSelectedCallback = null;
        callback?.Invoke();
    }
}
