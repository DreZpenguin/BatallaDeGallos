// ============================================================
//  BulletController.cs  — v4
//  Cambios respecto a v3:
//   · Llama a AudioManager.Instance.PlayBulletImpact() al golpear
//     un objetivo con HealthSystem.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BulletController : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Tag del GameObject que contiene el CircleCollider2D de la arena.")]
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
        if (_owner != null && other.gameObject == _owner) return;
        if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;
        if (other.CompareTag(arenaTag)) return;

        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null)
        {
            bool hit = health.TakeDamage(_damage, _direction);
            if (hit)
            {
                AudioManager.Instance?.PlayBulletImpact();
                Debug.Log($"[Bullet] Impactó a {other.name} por {_damage:F1} de daño.");
            }
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(arenaTag))
        {
            Debug.Log("[Bullet] Salió de la arena. Destruida.");
            Destroy(gameObject);
        }
    }
}
