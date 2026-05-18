// ============================================================
//  PowerUpUICanvas.cs  — v2
//  Cambios respecto a v1:
//   · Navegación con mando Xbox: D-Pad / stick izquierdo para
//     moverse entre tarjetas, botón Sur (A) para confirmar.
//   · Los campos de mando son configurables desde el Inspector.
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PowerUpUICanvas : MonoBehaviour
{
    // ── Estructura de datos ────────────────────────────────────
    [Serializable]
    public struct PowerUpOption
    {
        public PowerUpType id;
        public string      title;
        public string      description;
        public string      hexColor;
    }

    // ── Referencias del Canvas ─────────────────────────────────
    [Header("Panel raíz (se activa/desactiva)")]
    [SerializeField] private GameObject rootPanel;

    [Header("Tarjetas — deben ser exactamente 5 en orden")]
    [SerializeField] private GameObject[] cards;

    [Header("Textos de cada tarjeta (mismo orden que cards[])")]
    [SerializeField] private TextMeshProUGUI[] cardTitles;
    [SerializeField] private TextMeshProUGUI[] cardDescriptions;

    [Header("Imágenes de icono de cada tarjeta (mismo orden)")]
    [SerializeField] private Image[] cardIcons;

    [Header("Botones de selección (mismo orden)")]
    [SerializeField] private Button[] cardButtons;

    // ── Control — Mando Xbox ───────────────────────────────────
    [Header("Control — Mando Xbox (Selección de PowerUp)")]
    [Tooltip("Eje horizontal del D-Pad o stick izquierdo. Configura en Input Manager como 'DPadX' o usa 'Horizontal'.")]
    [SerializeField] private string gamepadNavAxisX    = "DPadX";

    [Tooltip("Botón de confirmación del mando (A = joystick button 0).")]
    [SerializeField] private KeyCode gamepadConfirmKey = KeyCode.JoystickButton0;

    [Tooltip("Umbral del eje de navegación para considerar que hay input.")]
    [SerializeField, Range(0.1f, 0.9f)] private float navDeadzone = 0.5f;

    [Tooltip("Segundos de espera entre cada movimiento de navegación (evita saltos rápidos).")]
    [SerializeField] private float navRepeatDelay = 0.25f;

    // ── Estado interno ─────────────────────────────────────────
    private PowerUpOption[]     _options;
    private Action<PowerUpType> _onSelected;
    private int                 _activeOptions   = 0;

    // Navegación con mando
    private int   _selectedIndex   = 0;
    private float _navTimer        = 0f;
    private bool  _axisWasNeutral  = true;   // evita movimiento continuo sin soltar

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    private void Update()
    {
        if (rootPanel == null || !rootPanel.activeSelf) return;

        HandleGamepadNavigation();
    }

    // ── API Pública ────────────────────────────────────────────

    public void Show(PowerUpOption[] options, Action<PowerUpType> callback)
    {
        _options      = options;
        _onSelected   = callback;
        _activeOptions = 0;
        _selectedIndex = 0;
        _navTimer      = 0f;
        _axisWasNeutral = true;

        for (int i = 0; i < cards.Length; i++)
        {
            bool hasOption = i < options.Length;
            cards[i].SetActive(hasOption);

            if (!hasOption) continue;

            _activeOptions++;

            if (i < cardTitles.Length && cardTitles[i] != null)
                cardTitles[i].text = options[i].title;

            if (i < cardDescriptions.Length && cardDescriptions[i] != null)
                cardDescriptions[i].text = options[i].description;

            if (i < cardIcons.Length && cardIcons[i] != null)
            {
                if (ColorUtility.TryParseHtmlString(options[i].hexColor, out Color col))
                    cardIcons[i].color = col;
            }

            int index = i;
            if (i < cardButtons.Length && cardButtons[i] != null)
            {
                cardButtons[i].onClick.RemoveAllListeners();
                cardButtons[i].onClick.AddListener(() => OnCardSelected(index));
            }
        }

        rootPanel.SetActive(true);

        // Selecciona el primer botón con el EventSystem
        RefreshGamepadSelection();
    }

    public void Hide()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    // ── Navegación con mando ───────────────────────────────────

    private void HandleGamepadNavigation()
    {
        if (_activeOptions == 0) return;

        // Cooldown de repetición
        if (_navTimer > 0f)
        {
            _navTimer -= Time.unscaledDeltaTime;  // usa unscaledDeltaTime porque Time.timeScale = 0
        }

        // ── D-Pad / stick izquierdo horizontal ────────────────
        float axisValue = GetNavAxis();
        bool  axisActive = Mathf.Abs(axisValue) >= navDeadzone;

        if (axisActive && _axisWasNeutral && _navTimer <= 0f)
        {
            int dir = axisValue > 0f ? 1 : -1;
            _selectedIndex = (_selectedIndex + dir + _activeOptions) % _activeOptions;
            _navTimer      = navRepeatDelay;
            _axisWasNeutral = false;
            RefreshGamepadSelection();
        }

        if (!axisActive)
            _axisWasNeutral = true;

        // ── D-Pad también como botones digitales (joystick button 7/8) ──
        // D-Pad derecha: JoystickButton7 | D-Pad izquierda: JoystickButton6 (varía por driver)
        // Se cubre con el eje, pero añadimos botón de confirmación.

        // ── Confirmación ──────────────────────────────────────
        if (Input.GetKeyDown(gamepadConfirmKey))
        {
            OnCardSelected(_selectedIndex);
        }
    }

    /// Refresca el foco del EventSystem en el botón actualmente seleccionado.
    private void RefreshGamepadSelection()
    {
        if (_selectedIndex < cardButtons.Length && cardButtons[_selectedIndex] != null)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            EventSystem.current?.SetSelectedGameObject(cardButtons[_selectedIndex].gameObject);
        }
    }

    private float GetNavAxis()
    {
        if (string.IsNullOrEmpty(gamepadNavAxisX)) return 0f;
        try   { return Input.GetAxis(gamepadNavAxisX); }
        catch { return 0f; }
    }

    // ── Callback de tarjeta ────────────────────────────────────

    private void OnCardSelected(int index)
    {
        if (_options == null || index >= _options.Length) return;

        AudioManager.Instance?.PlayPowerUpSelect();

        PowerUpType chosen = _options[index].id;
        Hide();
        _onSelected?.Invoke(chosen);
    }
}
