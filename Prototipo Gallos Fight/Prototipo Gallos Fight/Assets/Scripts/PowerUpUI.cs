// ============================================================
//  PowerUpUI.cs  — v2
//  Cambios respecto a v1:
//   · Cada PowerUpType tiene una Texture2D asignable desde el
//     Inspector (sección "Iconos por tipo de powerup").
//   · Al renderizar cada tarjeta, se busca la textura
//     correspondiente al PowerUpType.id con GetTextureForType().
//   · Si hay textura asignada: se dibuja el icono con color blanco
//     (sin tinte) para respetar los colores originales del sprite.
//   · Si NO hay textura: comportamiento de fallback idéntico a v1
//     (rectángulo de color sólido con hexColor).
//   · El campo useColorTintOnIcon permite teñir el icono con el
//     hexColor aunque tenga textura asignada (opcional).
// ============================================================
using System;
using UnityEngine;

public class PowerUpUI : MonoBehaviour
{
    // ── Estructura de datos ────────────────────────────────────

    public struct PowerUpOption
    {
        public PowerUpType id;
        public string      title;
        public string      description;
        public Color       iconColor;
    }

    // ── Iconos por tipo de powerup ─────────────────────────────
    [Header("Iconos por tipo de powerup")]
    [Tooltip("Textura para el powerup de Rango. Si queda vacío se usa el color de fallback.")]
    [SerializeField] private Texture2D iconRange;

    [Tooltip("Textura para el powerup de Daño.")]
    [SerializeField] private Texture2D iconDamage;

    [Tooltip("Textura para el powerup de Velocidad.")]
    [SerializeField] private Texture2D iconSpeed;

    [Tooltip("Textura para el powerup de Vida.")]
    [SerializeField] private Texture2D iconHealth;

    [Tooltip("Textura para el powerup de Disparo.")]
    [SerializeField] private Texture2D iconShoot;

    [Tooltip("Si está activo, aplica el color de la opción como tinte sobre el icono. " +
             "Desactívalo para mostrar la textura con sus colores originales.")]
    [SerializeField] private bool useColorTintOnIcon = false;

    // ── Estado interno ─────────────────────────────────────────

    private bool                _isVisible = false;
    private PowerUpOption[]     _options;
    private Action<PowerUpType> _onSelected;
    private int                 _hoveredIndex = -1;

    // ── Estilos (inicializados lazy) ───────────────────────────

    private GUIStyle _overlayStyle;
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _cardStyle;
    private GUIStyle _cardHoverStyle;
    private GUIStyle _cardTitleStyle;
    private GUIStyle _cardDescStyle;
    private GUIStyle _subtitleStyle;
    private bool     _stylesInitialized = false;

    // ── API Pública ────────────────────────────────────────────

    public void Show(PowerUpOption[] options, Action<PowerUpType> callback)
    {
        _options    = options;
        _onSelected = callback;
        _isVisible  = true;
    }

    public void Hide()
    {
        _isVisible = false;
    }

    // ── Rendering ─────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_isVisible) return;

        InitStyles();

        float sw = Screen.width;
        float sh = Screen.height;

        // Overlay oscuro
        GUI.Box(new Rect(0, 0, sw, sh), GUIContent.none, _overlayStyle);

        // Panel central
        float panelW = Mathf.Min(sw * 0.88f, 860f);
        float panelH = Mathf.Min(sh * 0.70f, 480f);
        float panelX = (sw - panelW) * 0.5f;
        float panelY = (sh - panelH) * 0.5f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, _panelStyle);

        // Título
        GUI.Label(new Rect(panelX, panelY + 18f, panelW, 52f), "ELIGE TU PODER", _titleStyle);
        GUI.Label(new Rect(panelX, panelY + 62f, panelW, 28f),
                  "Selecciona una mejora para continuar", _subtitleStyle);

        if (_options == null || _options.Length == 0) return;

        int   count  = _options.Length;
        float margin = 24f;
        float cardW  = (panelW - margin * (count + 1)) / count;
        float cardH  = panelH - 130f - margin;
        float cardY  = panelY + 110f;

        Event e = Event.current;

        for (int i = 0; i < count; i++)
        {
            float cardX    = panelX + margin + i * (cardW + margin);
            Rect  cardRect = new Rect(cardX, cardY, cardW, cardH);

            bool hovered = cardRect.Contains(e.mousePosition);
            if (hovered) _hoveredIndex = i;

            // Fondo de tarjeta
            Color prevColor = GUI.color;
            GUI.color = hovered ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.92f);
            GUI.Box(cardRect, GUIContent.none, hovered ? _cardHoverStyle : _cardStyle);
            GUI.color = prevColor;

            // ── Icono ──────────────────────────────────────────
            float iconSize = Mathf.Min(cardW * 0.28f, 60f);
            float iconX    = cardX + (cardW - iconSize) * 0.5f;
            float iconY    = cardY + 22f;
            Rect  iconRect = new Rect(iconX, iconY, iconSize, iconSize);

            Texture2D tex = GetTextureForType(_options[i].id);

            Color savedColor = GUI.color;

            if (tex != null)
            {
                // Textura personalizada: dibuja con color blanco o con tinte según opción
                GUI.color = useColorTintOnIcon ? _options[i].iconColor : Color.white;
                GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // Fallback v1: rectángulo de color sólido
                GUI.color = _options[i].iconColor;
                GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
            }

            GUI.color = savedColor;

            // Título de tarjeta
            float textStartY = iconY + iconSize + 14f;
            GUI.Label(new Rect(cardX + 6f, textStartY, cardW - 12f, 36f),
                      _options[i].title, _cardTitleStyle);

            // Descripción
            GUI.Label(new Rect(cardX + 10f, textStartY + 38f, cardW - 20f, cardH - iconSize - 100f),
                      _options[i].description, _cardDescStyle);

            // Botón ELEGIR
            float btnW = cardW - 28f;
            float btnH = 36f;
            float btnX = cardX + 14f;
            float btnY = cardY + cardH - btnH - 14f;

            if (hovered)
            {
                Color c = _options[i].iconColor;
                GUI.backgroundColor = new Color(c.r, c.g, c.b, 0.9f);
            }
            else
            {
                GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            }

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "ELEGIR"))
            {
                GUI.backgroundColor = Color.white;
                PowerUpType chosen = _options[i].id;
                Hide();
                _onSelected?.Invoke(chosen);
                return;
            }

            GUI.backgroundColor = Color.white;
        }
    }

    // ── Resolución de textura por tipo ─────────────────────────

    /// Devuelve la Texture2D asignada en el Inspector para cada PowerUpType.
    /// Devuelve null si no hay textura asignada (usa fallback de color).
    private Texture2D GetTextureForType(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Range:  return iconRange;
            case PowerUpType.Damage: return iconDamage;
            case PowerUpType.Speed:  return iconSpeed;
            case PowerUpType.Health: return iconHealth;
            case PowerUpType.Shoot:  return iconShoot;
            default:                 return null;
        }
    }

    // ── Inicialización de estilos ──────────────────────────────

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var t = new Texture2D(w, h);
            t.SetPixels(pix);
            t.Apply();
            return t;
        }

        _overlayStyle = new GUIStyle();
        _overlayStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.72f));

        _panelStyle = new GUIStyle(GUI.skin.box);
        _panelStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.10f, 0.97f));
        _panelStyle.border = new RectOffset(4, 4, 4, 4);

        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontSize  = 30;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.alignment = TextAnchor.UpperCenter;
        _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

        _subtitleStyle = new GUIStyle(GUI.skin.label);
        _subtitleStyle.fontSize  = 13;
        _subtitleStyle.alignment = TextAnchor.UpperCenter;
        _subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        _cardStyle = new GUIStyle(GUI.skin.box);
        _cardStyle.normal.background = MakeTex(2, 2, new Color(0.13f, 0.13f, 0.17f, 1f));

        _cardHoverStyle = new GUIStyle(GUI.skin.box);
        _cardHoverStyle.normal.background = MakeTex(2, 2, new Color(0.20f, 0.20f, 0.27f, 1f));

        _cardTitleStyle = new GUIStyle(GUI.skin.label);
        _cardTitleStyle.fontSize  = 18;
        _cardTitleStyle.fontStyle = FontStyle.Bold;
        _cardTitleStyle.alignment = TextAnchor.UpperCenter;
        _cardTitleStyle.normal.textColor = Color.white;

        _cardDescStyle = new GUIStyle(GUI.skin.label);
        _cardDescStyle.fontSize  = 12;
        _cardDescStyle.alignment = TextAnchor.UpperCenter;
        _cardDescStyle.wordWrap  = true;
        _cardDescStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
    }
}
