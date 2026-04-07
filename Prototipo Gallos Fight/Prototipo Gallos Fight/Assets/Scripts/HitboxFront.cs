using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HitboxFront : MonoBehaviour
{
    private AttackController _attackController;
    private Collider2D _collider;

    // Tamaños base guardados al Awake para poder escalar desde el origen
    private Vector2 _baseBoxSize;
    private float _baseCircleRadius;
    private bool _isBox;

    //  Unity Lifecycle 

    private void Awake()
    {
        _attackController = GetComponentInParent<AttackController>();
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;

        // Detecta el tipo de collider y guarda el tamaño base
        if (_collider is BoxCollider2D box)
        {
            _isBox = true;
            _baseBoxSize = box.size;
        }
        else if (_collider is CircleCollider2D circle)
        {
            _isBox = false;
            _baseCircleRadius = circle.radius;
        }
        else
        {
            Debug.LogWarning("[HitboxFront] Tipo de Collider2D no soportado para +Rango. Usa Box o Circle.");
        }
    }

    // Detección

    private void OnTriggerEnter2D(Collider2D other)
    {
        _attackController?.OnHitboxTriggerEnter(other);
    }

    // API Pública 


    /// Aumenta el tamaño de la hitbox sumando flatAmount a cada dimensión del collider base.
    /// Puede llamarse múltiples veces; cada llamada suma sobre el base, no sobre el valor actual.
    /// Ejemplo: AddRangeBonus(0.5f) en un BoxCollider2D de size(1,1) → size(1.5, 1.5)
   
    public void AddRangeBonus(float flatAmount)
    {
        if (_collider is BoxCollider2D box)
        {
            // Recalcula siempre desde la base + todos los bonuses para evitar drift
            // El caller (PowerUpManager) acumula el total y llama SetRangeTotal en su lugar.
            box.size = _baseBoxSize + Vector2.one * flatAmount;
            Debug.Log($"[HitboxFront] BoxCollider2D size → {box.size}");
        }
        else if (_collider is CircleCollider2D circle)
        {
            circle.radius = _baseCircleRadius + flatAmount;
            Debug.Log($"[HitboxFront] CircleCollider2D radius → {circle.radius}");
        }
    }

    /// Aplica el total acumulado de bonus de rango (llamado internamente desde PowerUpManager).
    /// Así el cálculo siempre parte del valor base y no se acumula drift. 
    public void SetRangeTotal(float totalFlatBonus)
    {
        if (_collider is BoxCollider2D box)
            box.size = _baseBoxSize + Vector2.one * totalFlatBonus;
        else if (_collider is CircleCollider2D circle)
            circle.radius = _baseCircleRadius + totalFlatBonus;
    }
}
