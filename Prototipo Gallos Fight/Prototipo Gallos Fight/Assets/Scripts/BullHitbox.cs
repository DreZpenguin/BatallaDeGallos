// ============================================================
//  BullHitbox.cs
//
//  Componente que va en un GameObject HIJO del toro.
//  Tiene un Collider2D (isTrigger = true) que BullEnemyAI
//  activa solo durante la embestida y desactiva el resto
//  del tiempo.
//
//  SETUP EN UNITY:
//   1. Crea un GameObject hijo del toro llamado "BullHitbox".
//   2. Añádele un CircleCollider2D o CapsuleCollider2D
//      ajustado al cuerpo del toro. Marca isTrigger = true.
//   3. Añade este script al mismo GameObject hijo.
//   4. En BullEnemyAI, arrastra el hijo al campo
//      "Bull Hitbox" en el Inspector (o se busca automático).
//
//  NOTA: el GameObject padre del toro también necesita un
//  Collider2D NO trigger para la física normal (empuje, etc.).
// ============================================================
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
