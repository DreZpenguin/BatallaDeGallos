
using UnityEngine;

public class BullHitbox : MonoBehaviour
{
    private BullEnemyAI  _bull;
    private Collider2D   _collider;

    private void Awake()
    {
        _bull     = GetComponentInParent<BullEnemyAI>();
        _collider = GetComponent<Collider2D>();

        if (_collider != null)
            _collider.isTrigger = true;

        // Empieza desactivado — BullEnemyAI lo activa al embestir
        SetActive(false);
    }

    /// Activa o desactiva el collider del trigger.
    public void SetActive(bool active)
    {
        if (_collider != null)
            _collider.enabled = active;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Solo daña al jugador
        if (!other.CompareTag("Player")) return;

        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health == null) return;

        // Delega al toro para que maneje el daño y el cooldown
        _bull?.OnHitboxContact(health, other.transform.position);
    }
}
