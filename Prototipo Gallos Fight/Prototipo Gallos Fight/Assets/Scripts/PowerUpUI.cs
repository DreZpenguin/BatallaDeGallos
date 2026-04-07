using System;
using UnityEngine;

public class PowerUpUI : MonoBehaviour
{
    //  Estructuras de Datos

    public struct PowerUpOption
    {
        public PowerUpType id;
        public string      title;
        public string      description;
        public Color       iconColor;
    }

    // Estado Interno 

    private bool                    _isVisible = false;
    private PowerUpOption[]         _options;
    private Action<PowerUpType>     _onSelected;
    private int                     _hoveredIndex = -1;

    //  Estilos (inicializados lazy para compatibilidad con OnGUI) 

    private GUIStyle _overlayStyle;
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _cardStyle;
    private GUIStyle _cardHoverStyle;
    private GUIStyle _cardTitleStyle;
    private GUIStyle _cardDescStyle;
    private GUIStyle _subtitleStyle;
    private bool     _stylesInitialized = false;

    //  API Pública 


    /// Muestra el panel de selección de powerup.
    /// callback se invoca con el PowerUpType elegido.
  
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

    //  Rendering 

    private void OnGUI()
    {
        if (!_isVisible) return;

        InitStyles();

        float sw = Screen.width;
        float sh = Screen.height;

        //  Overlay oscuro semitransparente 
        GUI.Box(new Rect(0, 0, sw, sh), GUIContent.none, _overlayStyle);

        // Panel Central 
        float panelW = Mathf.Min(sw * 0.88f, 860f);
        float panelH = Mathf.Min(sh * 0.70f, 480f);
        float panelX = (sw - panelW) * 0.5f;
        float panelY = (sh - panelH) * 0.5f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, _panelStyle);

        // Título 
        float titleH = 52f;
        GUI.Label(new Rect(panelX, panelY + 18f, panelW, titleH), "ELIGE TU PODER", _titleStyle);
        GUI.Label(new Rect(panelX, panelY + 62f, panelW, 28f), "Selecciona una mejora para continuar", _subtitleStyle);

        // Tarjetas
        if (_options == null || _options.Length == 0) return;

        int   count    = _options.Length;
        float margin   = 24f;
        float cardW    = (panelW - margin * (count + 1)) / count;
        float cardH    = panelH - 130f - margin;
        float cardY    = panelY + 110f;

        Event e = Event.current;

        for (int i = 0; i < count; i++)
        {
            float cardX = panelX + margin + i * (cardW + margin);
            Rect  cardRect = new Rect(cardX, cardY, cardW, cardH);

            // Detecta hover
            bool hovered = cardRect.Contains(e.mousePosition);
            if (hovered) _hoveredIndex = i;

            // Fondo de tarjeta
            Color prevColor = GUI.color;
            if (hovered)
            {
                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.Box(cardRect, GUIContent.none, _cardHoverStyle);
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.92f);
                GUI.Box(cardRect, GUIContent.none, _cardStyle);
            }
            GUI.color = prevColor;

            // ── Indicador de color / ícono ────────────────────────────────
            float iconSize = Mathf.Min(cardW * 0.28f, 60f);
            float iconX    = cardX + (cardW - iconSize) * 0.5f;
            float iconY    = cardY + 22f;

            Color savedColor = GUI.color;
            GUI.color = _options[i].iconColor;
            GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), Texture2D.whiteTexture);
            GUI.color = savedColor;

            //  Título de la tarjeta 
            float textStartY = iconY + iconSize + 14f;
            GUI.Label(new Rect(cardX + 6f, textStartY, cardW - 12f, 36f),
                      _options[i].title, _cardTitleStyle);

            //  Descripción 
            GUI.Label(new Rect(cardX + 10f, textStartY + 38f, cardW - 20f, cardH - iconSize - 100f),
                      _options[i].description, _cardDescStyle);

            //  Botón de selección 
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

    // Inicialización de Estilos 
    // Inicialización de Estilos 

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // Textura helper
        Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var t = new Texture2D(w, h);
            t.SetPixels(pix);
            t.Apply();
            return t;
        }

        // Overlay
        _overlayStyle = new GUIStyle();
        _overlayStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.72f));

        // Panel
        _panelStyle = new GUIStyle(GUI.skin.box);
        _panelStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.10f, 0.97f));
        _panelStyle.border = new RectOffset(4, 4, 4, 4);

        // Título
        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontSize  = 30;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.alignment = TextAnchor.UpperCenter;
        _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

        // Subtítulo
        _subtitleStyle = new GUIStyle(GUI.skin.label);
        _subtitleStyle.fontSize  = 13;
        _subtitleStyle.alignment = TextAnchor.UpperCenter;
        _subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        // Tarjeta normal
        _cardStyle = new GUIStyle(GUI.skin.box);
        _cardStyle.normal.background = MakeTex(2, 2, new Color(0.13f, 0.13f, 0.17f, 1f));

        // Tarjeta hover
        _cardHoverStyle = new GUIStyle(GUI.skin.box);
        _cardHoverStyle.normal.background = MakeTex(2, 2, new Color(0.20f, 0.20f, 0.27f, 1f));

        // Título de tarjeta
        _cardTitleStyle = new GUIStyle(GUI.skin.label);
        _cardTitleStyle.fontSize  = 18;
        _cardTitleStyle.fontStyle = FontStyle.Bold;
        _cardTitleStyle.alignment = TextAnchor.UpperCenter;
        _cardTitleStyle.normal.textColor = Color.white;

        // Descripción de tarjeta
        _cardDescStyle = new GUIStyle(GUI.skin.label);
        _cardDescStyle.fontSize  = 12;
        _cardDescStyle.alignment = TextAnchor.UpperCenter;
        _cardDescStyle.wordWrap  = true;
        _cardDescStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
    }
}
