
using UnityEngine;
using UnityEngine.Events;

public class HealthSystem : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Invencibilidad tras recibir daño")]
    [SerializeField] private bool useInvincibility = true;
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float _invincibilityTimer = 0f;

    [Header("Eventos")]
    public UnityEvent<float, float> OnHealthChanged; // (currentHP, maxHP)
    public UnityEvent OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (_invincibilityTimer > 0f)
            _invincibilityTimer -= Time.deltaTime;
    }

    public bool TakeDamage(float amount)
    {
        if (!IsAlive) return false;
        if (useInvincibility && _invincibilityTimer > 0f) return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (useInvincibility)
            _invincibilityTimer = invincibilityDuration;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"{gameObject.name} recibió {amount} de daño. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            OnDeath?.Invoke();
            Debug.Log($"{gameObject.name} murió.");
        }

        return true;
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnGUI()
    {
        if (!gameObject.CompareTag("Player")) return;
        GUI.Label(new Rect(10, 90, 220, 30),
            $"HP: {currentHealth:F0} / {maxHealth:F0}");
    }
}
