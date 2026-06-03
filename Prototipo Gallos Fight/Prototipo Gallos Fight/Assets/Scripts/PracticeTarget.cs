// ============================================================
//  PracticeTarget.cs
//
//  Añade este script al prefab del enemigo de práctica.
//  Se suscribe a OnHealthChanged del HealthSystem y muestra
//  un popup flotante con el daño recibido cada vez que es golpeado.
//
//  El popup sube durante su vida útil y se desvanece.
//  Múltiples golpes generan múltiples popups independientes.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HealthSystem))]
public class PracticeTarget : MonoBehaviour
{
    [Header("── Popup de daño ────────────────────────────────")]
    [Tooltip("Tamaño del texto del popup.")]
    [SerializeField] private int   popupFontSize   = 22;

    [Tooltip("Cuántos segundos dura el popup en pantalla.")]
    [SerializeField] private float popupLifetime   = 1.2f;

    [Tooltip("Velocidad a la que el popup sube (en píxeles por segundo).")]
    [SerializeField] private float popupRiseSpeed  = 55f;

    [Tooltip("Color del popup para golpes normales.")]
    [SerializeField] private Color popupColorNormal = new Color(1f, 0.92f, 0.2f);

    [Tooltip("Color del popup para golpes críticos (daño > critThreshold).")]
    [SerializeField] private Color popupColorCrit   = new Color(1f, 0.3f, 0.1f);

    [Tooltip("Daño a partir del cual se considera crítico (cambia color y tamaño).")]
    [SerializeField] private float critThreshold    = 40f;

    [Tooltip("Multiplicador de tamaño del popup en golpe crítico.")]
    [SerializeField] private float critSizeMultiplier = 1.4f;

    // ── Estructura interna de cada popup activo ────────────────
    private class DamagePopup
    {
        public string  text;
        public float   lifetime;
        public float   elapsed;
        public float   screenX;
        public float   screenY;
        public Color   color;
        public int     fontSize;
    }

    private List<DamagePopup> _popups = new List<DamagePopup>();
    private HealthSystem      _healthSystem;
    private float             _lastMaxHealth;
    private GUIStyle          _popupStyle;
    private bool              _guiInitialized = false;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        _healthSystem = GetComponent<HealthSystem>();
    }

    private void Start()
    {
        _lastMaxHealth = _healthSystem.MaxHealth;
        _healthSystem.OnHealthChanged.AddListener(OnHealthChanged);
    }

    private void Update()
    {
        // Avanza el tiempo de cada popup
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            _popups[i].elapsed  += Time.deltaTime;
            _popups[i].screenY  -= popupRiseSpeed * Time.deltaTime;

            if (_popups[i].elapsed >= _popups[i].lifetime)
                _popups.RemoveAt(i);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CALLBACK DE DAÑO
    // ══════════════════════════════════════════════════════════

    private void OnHealthChanged(float current, float max)
    {
        // Calcula el daño recibido comparando con la salud anterior
        float dmg = _lastMaxHealth - current;
        _lastMaxHealth = current;

        // En modo infinito el HP se restaura, lo que dispara OnHealthChanged
        // con valor mayor — ignoramos eso (dmg negativo = curación)
        if (dmg <= 0f) return;

        SpawnPopup(dmg);
    }

    private void SpawnPopup(float damage)
    {
        // Convierte la posición del enemigo a coordenadas de pantalla
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position);

        // Offset aleatorio horizontal para que no se apilen
        float offsetX = Random.Range(-25f, 25f);

        bool   isCrit    = damage >= critThreshold;
        string dmgText   = isCrit
            ? $"¡{Mathf.RoundToInt(damage)}!"
            : Mathf.RoundToInt(damage).ToString();

        _popups.Add(new DamagePopup
        {
            text     = dmgText,
            lifetime = popupLifetime,
            elapsed  = 0f,
            // OnGUI usa coordenadas desde la esquina superior izquierda
            // pero WorldToScreenPoint usa esquina inferior izquierda → invertimos Y
            screenX  = screenPos.x - 20f + offsetX,
            screenY  = Screen.height - screenPos.y - 20f,
            color    = isCrit ? popupColorCrit : popupColorNormal,
            fontSize = isCrit
                ? Mathf.RoundToInt(popupFontSize * critSizeMultiplier)
                : popupFontSize
        });
    }

    // ══════════════════════════════════════════════════════════
    //  RENDER DE POPUPS
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (_popups.Count == 0) return;

        InitGUI();

        foreach (DamagePopup popup in _popups)
        {
            // Alfa basado en qué tan cerca está de desaparecer
            float t     = popup.elapsed / popup.lifetime;
            float alpha = Mathf.Lerp(1f, 0f, t * t); // desvanecido cuadrático

            Color c = popup.color;
            c.a = alpha;

            // Sombra para legibilidad
            Color shadow = new Color(0f, 0f, 0f, alpha * 0.7f);
            _popupStyle.fontSize         = popup.fontSize;
            _popupStyle.normal.textColor = shadow;
            GUI.Label(new Rect(popup.screenX + 1f, popup.screenY + 1f, 80f, 40f),
                      popup.text, _popupStyle);

            // Texto principal
            _popupStyle.normal.textColor = c;
            GUI.Label(new Rect(popup.screenX, popup.screenY, 80f, 40f),
                      popup.text, _popupStyle);
        }
    }

    private void InitGUI()
    {
        if (_guiInitialized) return;
        _guiInitialized = true;

        _popupStyle           = new GUIStyle(GUI.skin.label);
        _popupStyle.fontStyle = FontStyle.Bold;
        _popupStyle.alignment = TextAnchor.MiddleCenter;
    }
}
