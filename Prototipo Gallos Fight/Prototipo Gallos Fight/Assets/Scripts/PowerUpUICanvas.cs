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
        public string      hexColor;   // Color de tinte si no hay sprite, o de borde/acento
    }

    // ── Iconos por tipo de powerup ─────────────────────────────
    [Header("Iconos por tipo de powerup")]
    [Tooltip("Sprite que se mostrará en la tarjeta del powerup de Rango.")]
    [SerializeField] private Sprite iconRange;

    [Tooltip("Sprite que se mostrará en la tarjeta del powerup de Daño.")]
    [SerializeField] private Sprite iconDamage;

    [Tooltip("Sprite que se mostrará en la tarjeta del powerup de Velocidad.")]
    [SerializeField] private Sprite iconSpeed;

    [Tooltip("Sprite que se mostrará en la tarjeta del powerup de Vida.")]
    [SerializeField] private Sprite iconHealth;

    [Tooltip("Sprite que se mostrará en la tarjeta del powerup de Disparo.")]
    [SerializeField] private Sprite iconShoot;

    [Tooltip("Si está activo, el Image adoptará el tamaño nativo del sprite " +
             "en lugar de estirarse al tamaño del RectTransform.")]
    [SerializeField] private bool useNativeSpriteSize = false;

    [Tooltip("Si está activo, aplica el hexColor de cada opción como tinte sobre el icono. " +
             "Desactívalo para mostrar el sprite con sus colores originales (recomendado).")]
    [SerializeField] private bool useColorTintOnIcon = false;

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
    [Tooltip("Eje horizontal del D-Pad o stick izquierdo.")]
    [SerializeField] private string gamepadNavAxisX = "DPadX";

    [Tooltip("Botón de confirmación del mando (A = joystick button 0).")]
    [SerializeField] private KeyCode gamepadConfirmKey = KeyCode.JoystickButton0;

    [Tooltip("Umbral del eje de navegación.")]
    [SerializeField, Range(0.1f, 0.9f)] private float navDeadzone = 0.5f;

    [Tooltip("Segundos entre movimientos de navegación (evita saltos rápidos).")]
    [SerializeField] private float navRepeatDelay = 0.25f;

    // ── Estado interno ─────────────────────────────────────────
    private PowerUpOption[]     _options;
    private Action<PowerUpType> _onSelected;
    private int                 _activeOptions  = 0;

    private int   _selectedIndex  = 0;
    private float _navTimer       = 0f;
    private bool  _axisWasNeutral = true;

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
        _options        = options;
        _onSelected     = callback;
        _activeOptions  = 0;
        _selectedIndex  = 0;
        _navTimer       = 0f;
        _axisWasNeutral = true;

        for (int i = 0; i < cards.Length; i++)
        {
            bool hasOption = i < options.Length;
            cards[i].SetActive(hasOption);
            if (!hasOption) continue;

            _activeOptions++;

            // ── Título ─────────────────────────────────────────
            if (i < cardTitles.Length && cardTitles[i] != null)
                cardTitles[i].text = options[i].title;

            // ── Descripción ────────────────────────────────────
            if (i < cardDescriptions.Length && cardDescriptions[i] != null)
                cardDescriptions[i].text = options[i].description;

            // ── Icono ──────────────────────────────────────────
            if (i < cardIcons.Length && cardIcons[i] != null)
            {
                Image icon   = cardIcons[i];
                Sprite sprite = GetSpriteForType(options[i].id);

                if (sprite != null)
                {
                    icon.sprite = sprite;
                    icon.enabled = true;
                    icon.preserveAspect = true;

                    if (useNativeSpriteSize)
                        icon.SetNativeSize();
                }
                else
                {
                    // Sin sprite: muestra solo el color de tinte como fallback
                    icon.sprite  = null;
                    icon.enabled = true;
                }

                // Tinte: si hay sprite y useColorTintOnIcon está OFF → Color.white (sin tinte).
                // Si no hay sprite → siempre aplica el hexColor como fallback de color.
                if (ColorUtility.TryParseHtmlString(options[i].hexColor, out Color col))
                {
                    if (sprite != null)
                        icon.color = useColorTintOnIcon ? col : Color.white;
                    else
                        icon.color = col; // fallback siempre con color cuando no hay sprite
                }
            }

            // ── Botón ──────────────────────────────────────────
            int index = i;
            if (i < cardButtons.Length && cardButtons[i] != null)
            {
                cardButtons[i].onClick.RemoveAllListeners();
                cardButtons[i].onClick.AddListener(() => OnCardSelected(index));
            }
        }

        rootPanel.SetActive(true);
        RefreshGamepadSelection();
    }

    public void Hide()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    // ── Resolución de sprite por tipo ──────────────────────────

    /// Devuelve el sprite asignado en el Inspector para cada PowerUpType.
    /// Si un tipo no tiene sprite, devuelve null (el Image mostrará solo color).
    private Sprite GetSpriteForType(PowerUpType type)
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

    // ── Navegación con mando ───────────────────────────────────

    private void HandleGamepadNavigation()
    {
        if (_activeOptions == 0) return;

        if (_navTimer > 0f)
            _navTimer -= Time.unscaledDeltaTime;

        float axisValue = GetNavAxis();
        bool  axisActive = Mathf.Abs(axisValue) >= navDeadzone;

        if (axisActive && _axisWasNeutral && _navTimer <= 0f)
        {
            int dir = axisValue > 0f ? 1 : -1;
            _selectedIndex  = (_selectedIndex + dir + _activeOptions) % _activeOptions;
            _navTimer       = navRepeatDelay;
            _axisWasNeutral = false;
            RefreshGamepadSelection();
        }

        if (!axisActive)
            _axisWasNeutral = true;

        if (Input.GetKeyDown(gamepadConfirmKey))
            OnCardSelected(_selectedIndex);
    }

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
