using System.Collections;
using UnityEngine;

public class AttackController : MonoBehaviour
{
    [Header("Ataque")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackCooldown = 0.4f;

    [Header("Hitbox")]
    [SerializeField] private Collider2D frontHitbox;
    [SerializeField] private float hitboxActiveDuration = 0.12f;

    // Bonus acumulado de daño (sumatorio de porcentajes, ej: 0.5 = +50%)
    private float _damageBonus = 0f;

    private float _cooldownTimer = 0f;
    private Vector2 _facingDirection = Vector2.up;

    public float BaseDamage    => attackDamage;
    public float CurrentDamage => attackDamage * (1f + _damageBonus);

    private bool _isPlayer;

    private void Awake()
    {
        _isPlayer = gameObject.CompareTag("Player");

        if (frontHitbox == null)
        {
            Transform hitboxTransform = transform.Find("HitboxFront");
            if (hitboxTransform != null)
                frontHitbox = hitboxTransform.GetComponent<Collider2D>();
        }

        if (frontHitbox != null)
            frontHitbox.enabled = false;
        else
            Debug.LogWarning($"[AttackController] No se encontró la hitbox frontal en {gameObject.name}.");
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(attackKey) && _cooldownTimer <= 0f)
            StartAttack();
    }

    // ── API Pública ────────────────────────────────────────────
    public void TriggerAttack()
    {
        if (_cooldownTimer <= 0f)
            StartAttack();
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (direction != Vector2.zero)
            _facingDirection = direction.normalized;
    }

    public void AddDamageBonus(float percent)
    {
        _damageBonus += percent;
        Debug.Log($"[AttackController] Daño aumentado. Bonus total: {_damageBonus * 100f:F0}% | Daño actual: {CurrentDamage:F1}");
    }

    // ── Ataque ─────────────────────────────────────────────────
    private void StartAttack()
    {
        _cooldownTimer = attackCooldown;

        // Sonido según quien ataca
        if (_isPlayer)
            AudioManager.Instance?.PlayPlayerAttack();
        else
            AudioManager.Instance?.PlayEnemyAttack();

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        if (frontHitbox != null)
            frontHitbox.enabled = true;

        yield return new WaitForSeconds(hitboxActiveDuration);

        if (frontHitbox != null)
            frontHitbox.enabled = false;
    }

    public void OnHitboxTriggerEnter(Collider2D other)
    {
        if (other.gameObject == gameObject) return;

        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null)
        {
            float dmg = CurrentDamage;
            Vector2 hitDir = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;

            bool hit = health.TakeDamage(dmg, hitDir);
            if (hit)
                Debug.Log($"[AttackController] {gameObject.name} golpeó a {other.name} por {dmg:F1}.");
        }
    }

    // ── Debug GUI ──────────────────────────────────────────────
    private void OnGUI()
    {
        if (!_isPlayer) return;

        GUI.Label(new Rect(10, 115, 260, 50),
            $"Cooldown ataque: {Mathf.Max(0f, _cooldownTimer):F1}s\n" +
            $"Daño actual: {CurrentDamage:F1} (base {attackDamage} +{_damageBonus * 100f:F0}%)");
    }
}
