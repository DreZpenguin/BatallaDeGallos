// ============================================================
//  BulletController.cs  — v3
//  Cambios:
//   · La bala ignora el collider de la arena al ENTRAR (no se destruye).
//   · La bala se destruye al SALIR del collider de la arena.
//   · El tag de la arena es configurable desde el Inspector ("Arena" por defecto).
//   · El resto del comportamiento es idéntico a v2.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BulletController : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Tag del GameObject que contiene el CircleCollider2D de la arena. " +
             "Asigna este mismo tag al objeto de la arena en Unity.")]
    [SerializeField] private string arenaTag = "Arena";

    private float      _damage   = 10f;
    private float      _lifetime = 3f;
    private float      _speed    = 12f;
    private GameObject _owner;
    private Vector2    _direction;

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    /// Llamado por ShootingController / RangedEnemyAI justo después de Instantiate.
    public void Init(float damage, float speed, float lifetime, Vector2 direction, GameObject owner)
    {
        _damage    = damage;
        _speed     = speed;
        _lifetime  = lifetime;
        _owner     = owner;
        _direction = direction.normalized;

        _rb.linearVelocity = _direction * _speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, _lifetime);
    }

    // ── Colisiones ─────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignora al dueño y sus hijos
        if (_owner != null && other.gameObject == _owner) return;
        if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;

        // Ignora el collider de la arena al entrar
        // (la bala se instancia dentro, por eso lo detectaría aquí)
        if (other.CompareTag(arenaTag)) return;

        // Daño a entidades con HealthSystem
        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null)
        {
            bool hit = health.TakeDamage(_damage, _direction);
            if (hit)
                Debug.Log($"[Bullet] Impactó a {other.name} por {_damage:F1} de daño.");
            Destroy(gameObject);
            return;
        }

        // Cualquier otro objeto sólido (paredes, tilemap, etc.) la destruye
        Destroy(gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Se destruye al salir del límite de la arena
        if (other.CompareTag(arenaTag))
        {
            Debug.Log("[Bullet] Salió de la arena. Destruida.");
            Destroy(gameObject);
        }
    }
}
