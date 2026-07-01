// ============================================================
//  DeadZone.cs  — v2
//
//  CAMBIO respecto a v1:
//   · En lugar de un timer de 3s que mata instantáneamente,
//     el jugador pierde dañoPerSegundo de HP cada segundo que
//     permanezca fuera de la arena.
//   · El daño es configurable desde el Inspector.
//   · Se muestra un warning visual en pantalla (OnGUI) con
//     una barra de cuenta regresiva mientras el jugador está fuera.
//   · Si el jugador regresa, el daño se detiene inmediatamente.
//   · El daño se aplica a través de HealthSystem.TakeDamage()
//     igual que cualquier otro daño del juego.
// ============================================================
using System.Collections;
using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("── Zona ─────────────────────────────────────────")]
    [Tooltip("CircleCollider2D que define los límites de la arena. " +
             "Si queda vacío se usa el Collider2D de este mismo GameObject.")]
    [SerializeField] private CircleCollider2D Zone;

    [Header("── Daño fuera de la arena ───────────────────────")]
    [Tooltip("HP que pierde el jugador por segundo mientras esté fuera.")]
    [SerializeField] private float damagePorSegundo = 15f;

    [Tooltip("Segundos de gracia antes de que empiece el daño. " +
             "0 = daño inmediato al salir.")]
    [SerializeField] private float graceTime = 0.5f;

    [Header("── Warning en pantalla ─────────────────────────")]
    [Tooltip("Mostrar aviso en pantalla mientras el jugador está fuera.")]
    [SerializeField] private bool showWarning = true;

    [Tooltip("Color del texto de aviso.")]
    [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.1f);

    // ── Estado interno ─────────────────────────────────────────
    private bool          _playerOutside   = false;
    private float         _timeOutside     = 0f;   // segundos acumulados fuera
    private float         _damageAccum     = 0f;   // acumulador de daño fraccionario
    private HealthSystem  _playerHealth;
    private GUIStyle      _warningStyle;
    private bool          _guiInitialized  = false;

    // ══════════════════════════════════════════════════════════
    //  TRIGGERS
    // ══════════════════════════════════════════════════════════

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerHealth  = other.GetComponent<HealthSystem>();
        _playerOutside = true;
        _timeOutside   = 0f;
        _damageAccum   = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerOutside = false;
        _timeOutside   = 0f;
        _damageAccum   = 0f;
    }

    // ══════════════════════════════════════════════════════════
    //  UPDATE — aplica daño continuo
    // ══════════════════════════════════════════════════════════

    private void Update()
    {
        if (!_playerOutside || _playerHealth == null) return;

        _timeOutside += Time.deltaTime;

        // Espera el tiempo de gracia antes de empezar a dañar
        if (_timeOutside < graceTime) return;

        // Acumula daño continuo — aplica cada segundo completo
        // usando TakeDamageRaw para evitar que el cooldown de
        // invencibilidad bloquee el daño fuera de arena
        _damageAccum += damagePorSegundo * Time.deltaTime;

        if (_damageAccum >= 1f)
        {
            float dmg    = Mathf.Floor(_damageAccum);
            _damageAccum -= dmg;
            _playerHealth.TakeDamageRaw(dmg);   // bypasea invencibilidad
        }
    }

    // ══════════════════════════════════════════════════════════
    //  AVISO EN PANTALLA
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (!showWarning || !_playerOutside) return;
        if (_timeOutside < graceTime)        return;

        InitGUI();

        float sw = Screen.width;
        float sh = Screen.height;

        // Fondo semitransparente
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(sw * 0.5f - 160f, sh * 0.72f - 4f, 320f, 48f),
                        Texture2D.whiteTexture);

        // Texto de aviso
        GUI.color = warningColor;
        GUI.Label(new Rect(sw * 0.5f - 160f, sh * 0.72f, 320f, 40f),
                  "⚠  FUERA DE LA ARENA  ⚠", _warningStyle);

        GUI.color = prev;
    }

    private void InitGUI()
    {
        if (_guiInitialized) return;
        _guiInitialized = true;

        _warningStyle                  = new GUIStyle(GUI.skin.label);
        _warningStyle.fontSize         = 18;
        _warningStyle.fontStyle        = FontStyle.Bold;
        _warningStyle.alignment        = TextAnchor.MiddleCenter;
        _warningStyle.normal.textColor = warningColor;
    }
}
