using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    

    [Header("Bonus por powerup")]
    [Tooltip("Cantidad FLAT sumada al tamaño del collider de la hitbox. Ej: 0.5 = +0.5 unidades")]
    [SerializeField] private float rangeBonus = 0.5f;

    [Tooltip("Porcentaje del daño base que se añade. Ej: 0.25 = +25% del daño base")]
    [SerializeField] private float damageBonus = 0.25f;

    [Tooltip("Porcentaje de la velocidad base que se añade. Ej: 0.30 = +30% de la velocidad base")]
    [SerializeField] private float speedBonus = 0.30f;

    // Referencias 

    [Header("Referencias")]
    [SerializeField] private PowerUpUI powerUpUI;
    [SerializeField] private HitboxFront hitboxFront;

    private AttackController _attackController;
    private PlayerMovement2D _playerMovement;
    private float _totalRangeBonus = 0f;

    // Unity Lifecycle

    private void Awake()
    {
        _attackController = GetComponent<AttackController>();
        _playerMovement   = GetComponent<PlayerMovement2D>();

        if (powerUpUI == null)
            powerUpUI = FindFirstObjectByType<PowerUpUI>();

        if (hitboxFront == null)
            hitboxFront = GetComponentInChildren<HitboxFront>();

        if (powerUpUI == null)
            Debug.LogError("[PowerUpManager] ¡No se encontró PowerUpUI en la escena! Crea un GameObject vacío y añádele el script PowerUpUI.");
        if (hitboxFront == null)
            Debug.LogWarning("[PowerUpManager] No se encontró HitboxFront en los hijos del jugador.");
    }

    private void Start()
    {
        // Busca todos los HealthSystem en la escena y se suscribe a los que NO
        // sean el propio jugador (el jugador también tiene HealthSystem).
        HealthSystem[] allHealth = FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
        int registered = 0;

        foreach (HealthSystem hs in allHealth)
        {
            // Salta el HealthSystem que vive en el mismo GameObject que este script
            // (o en cualquier padre/hijo del jugador)
            if (hs.GetComponentInParent<PowerUpManager>() != null) continue;
            if (hs.gameObject == gameObject) continue;

            RegisterEnemy(hs);
            registered++;
        }

        Debug.Log($"[PowerUpManager] {registered} enemigo(s) registrado(s) automáticamente.");

        if (registered == 0)
            Debug.LogWarning("[PowerUpManager] No se encontró ningún enemigo en la escena. " +
                             "Asegúrate de que el enemigo tiene un componente HealthSystem.");
    }

 

    /// Registra manualmente un enemigo. Úsalo desde un spawner si en el futuro
    /// tus enemigos se instancian dinámicamente.
    public void RegisterEnemy(HealthSystem enemyHealth)
    {
        if (enemyHealth == null) return;
        // Usamos lambda con referencia para evitar suscripciones duplicadas
        enemyHealth.OnDeath.AddListener(OnEnemyDied);
        Debug.Log($"[PowerUpManager] Enemigo registrado: {enemyHealth.gameObject.name}");
    }



    [ContextMenu("Trigger PowerUp Selection (Test)")]
    public void TriggerPowerUpSelection()
    {
        OnEnemyDied();
    }

    // Lógica Interna 

    private void OnEnemyDied()
    {
        if (powerUpUI == null)
        {
            Debug.LogError("[PowerUpManager] powerUpUI es null. ¿Añadiste el script PowerUpUI a un GameObject en la escena?");
            return;
        }

        PowerUpUI.PowerUpOption[] options = new PowerUpUI.PowerUpOption[]
        {
            new PowerUpUI.PowerUpOption
            {
                id          = PowerUpType.Range,
                title       = "+RANGO",
                description = $"Aumenta el tamaño\nde tu hitbox\n+{rangeBonus:F2} unidades",
                iconColor   = new Color(0.2f, 0.8f, 1f)
            },
            new PowerUpUI.PowerUpOption
            {
                id          = PowerUpType.Damage,
                title       = "+DAÑO",
                description = $"Aumenta el daño\nde tu ataque\n+{damageBonus * 100f:F0}% del base",
                iconColor   = new Color(1f, 0.3f, 0.2f)
            },
            new PowerUpUI.PowerUpOption
            {
                id          = PowerUpType.Speed,
                title       = "+VELOCIDAD",
                description = $"Aumenta tu\nvelocidad de movimiento\n+{speedBonus * 100f:F0}% de la base",
                iconColor   = new Color(0.3f, 1f, 0.4f)
            }
        };

        Time.timeScale = 0f;
        powerUpUI.Show(options, ApplyPowerUp);
        Debug.Log("[PowerUpManager] Pantalla de powerup activada.");
    }

    private void ApplyPowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Range:
                _totalRangeBonus += rangeBonus;
                hitboxFront?.SetRangeTotal(_totalRangeBonus);
                Debug.Log($"[PowerUpManager] +Rango aplicado. Total acumulado: {_totalRangeBonus:F2}");
                break;

            case PowerUpType.Damage:
                _attackController?.AddDamageBonus(damageBonus);
                break;

            case PowerUpType.Speed:
                _playerMovement?.AddSpeedBonus(speedBonus);
                break;
        }

        Time.timeScale = 1f;
        Debug.Log($"[PowerUpManager] Powerup '{type}' aplicado. Juego reanudado.");
    }
}
