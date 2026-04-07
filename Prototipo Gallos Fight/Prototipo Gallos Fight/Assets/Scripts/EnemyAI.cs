using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AttackController))]
public class EnemyAI : MonoBehaviour
{
    // Inspector 
    [Header("Referencias")]
    [Tooltip("Transform del jugador. Si queda vacío se busca automáticamente por tag 'Player'.")]
    [SerializeField] private Transform playerTransform;

    [Header("Rangos")]
    [Tooltip("Distancia a la que el enemigo detecta al jugador y empieza a perseguirlo.")]
    [SerializeField] private float detectionRange = 8f;

    [Tooltip("Distancia a la que el enemigo deja de moverse y comienza a atacar.")]
    [SerializeField] private float attackRange = 1.5f;

    [Header("Movimiento")]
    [Tooltip("Velocidad de desplazamiento del enemigo hacia el jugador.")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Tooltip("Suavizado del giro. Valores bajos = giro instantáneo. Valores altos = giro lento.")]
    [SerializeField] private float rotationSpeed = 10f;

    // Estado interno 

    private enum State { Idle, Chasing, Attacking, Dead }
    private State _currentState = State.Idle;

    private Rigidbody2D _rb;
    private AttackController _attackController;
    private HealthSystem _healthSystem;

    [SerializeField] private GameObject Enemy;
    [SerializeField] private GameObject EnemyInstance;
    [SerializeField] private PowerUpManager PowerUp;

    // Unity Lifecycle 

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _attackController = GetComponent<AttackController>();
        _healthSystem = GetComponent<HealthSystem>();

       
        _rb.gravityScale = 0f;
        _rb.linearDamping = 8f;   // frena rápido al parar
        _rb.angularDamping = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("[EnemyAI] No se encontró ningún GameObject con tag 'Player'. " +
                                 "Asigna el Transform manualmente en el Inspector.");
        }

        // Suscribirse al evento de muerte
        if (_healthSystem != null)
            _healthSystem.OnDeath.AddListener(OnDeath);
    }

    private void Update()
    {
        if (_currentState == State.Dead) return;
        if (playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        UpdateState(distanceToPlayer);
        HandleRotation();
        HandleAttackInput(distanceToPlayer);
    }

    private void FixedUpdate()
    {
        if (_currentState == State.Chasing)
            MoveTowardsPlayer();
        else
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 10f);
    }

    // Máquina de Estados 

    private void UpdateState(float distance)
    {
        if (_currentState == State.Dead) return;

        State newState;

        if (distance <= attackRange)
            newState = State.Attacking;
        else if (distance <= detectionRange)
            newState = State.Chasing;
        else
            newState = State.Idle;

        if (newState != _currentState)
        {
            _currentState = newState;
            Debug.Log($"[EnemyAI] {gameObject.name} cambió a estado: {_currentState}");
        }
    }

    // Movimiento 

    private void MoveTowardsPlayer()
    {
        Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * moveSpeed;
    }

    private void HandleRotation()
    {
        if (playerTransform == null) return;

        Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        float targetAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        float smoothAngle = Mathf.LerpAngle(
            transform.rotation.eulerAngles.z,
            -targetAngle,
            rotationSpeed * Time.deltaTime
        );
        transform.rotation = Quaternion.Euler(0f, 0f, smoothAngle);

        // Notifica al AttackController la dirección de cara
        _attackController?.SetFacingDirection(dir);
    }

    // ── Ataque ────────────────────────────────────────────────────────────────


    /// Simula la tecla de ataque llamando directamente al AttackController
    /// cuando el enemigo está en estado Attacking.
    /// Usa reflexión para invocar el método privado StartAttack, o bien
    /// puedes hacer StartAttack público en AttackController 

    private void HandleAttackInput(float distance)
    {
        if (_currentState != State.Attacking) return;

        // Llama al método TriggerAttack que exponemos en AttackController.
       
        _attackController?.TriggerAttack();
    }

    // Muerte 

    private void OnDeath()
    {
        _currentState = State.Dead;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic; // evita que se mueva tras morir

        // Aquí puedes añadir: animación de muerte, partículas, desactivar colliders, etc.
        Debug.Log($"[EnemyAI] {gameObject.name} ha muerto. IA desactivada.");
        gameObject.SetActive(false); 
    }

    // ── Debug Visual ─────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Rango de detección (amarillo)
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.3f);
        Gizmos.DrawSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de ataque (rojo)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    public void RespawnEnemy() 
    {
        EnemyInstance = Instantiate(Enemy, new Vector3(0, 0, 0), Quaternion.identity);
        PowerUp.RegisterEnemy(EnemyInstance.GetComponent<HealthSystem>());
        
    }
}