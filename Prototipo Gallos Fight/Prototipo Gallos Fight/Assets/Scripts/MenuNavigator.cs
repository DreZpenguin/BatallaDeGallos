
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuNavigator : MonoBehaviour
{
    // ── Datos de un panel ──────────────────────────────────────
    [Serializable]
    public class PanelData
    {
        [Tooltip("Identificador único del panel. Usa este string en SwitchToPanel().")]
        public string id;

        [Tooltip("GameObject raíz del panel.")]
        public GameObject rootObject;

        [Tooltip("Botones navegables del panel en orden. " +
                 "Para paneles con sliders, deja este array vacío o solo con el botón de salir.")]
        public Button[] buttons;

        [Tooltip("Sliders del panel (opcional). Se navegan con arriba/abajo, " +
                 "se ajustan con izquierda/derecha o LB/RB.")]
        public Slider[] sliders;

        [Tooltip("Dirección de navegación entre botones.")]
        public NavDirection navDirection = NavDirection.Vertical;

        [Tooltip("Activa si este panel puede volver atrás con B/Escape.")]
        public bool hasBackPanel = false;

        [Tooltip("ID del panel al que vuelve con B/Escape.")]
        public string backPanelId = "Main";
    }

    public enum NavDirection { Vertical, Horizontal }

    // ── Inspector ──────────────────────────────────────────────

    [Header("Paneles del menú")]
    [SerializeField] private PanelData[] panels;

    [Header("Panel inicial")]
    [SerializeField] private string initialPanelId = "Main";

    [Header("Control — Mando Xbox")]
    [SerializeField] private string navAxisY = "DPadY";
    [SerializeField] private string navAxisX = "DPadX";
    [SerializeField] private KeyCode gamepadConfirmKey = KeyCode.JoystickButton0;
    [SerializeField] private KeyCode gamepadBackKey    = KeyCode.JoystickButton1;

    [Tooltip("Eje para ajustar sliders horizontalmente (stick izq horizontal o DPad X).")]
    [SerializeField] private string sliderAxisX = "DPadX";

    [Tooltip("Cuánto mueve el slider por segundo con el mando.")]
    [SerializeField] private float sliderStep = 0.5f;

    [SerializeField, Range(0.1f, 0.9f)] private float navDeadzone    = 0.5f;
    [SerializeField] private float navRepeatDelay  = 0.2f;
    [SerializeField] private float sliderRepeatDelay = 0.05f;

    [Header("Control — Teclado (fallback)")]
    [SerializeField] private KeyCode kbUpKey      = KeyCode.UpArrow;
    [SerializeField] private KeyCode kbDownKey    = KeyCode.DownArrow;
    [SerializeField] private KeyCode kbLeftKey    = KeyCode.LeftArrow;
    [SerializeField] private KeyCode kbRightKey   = KeyCode.RightArrow;
    [SerializeField] private KeyCode kbConfirmKey = KeyCode.Return;
    [SerializeField] private KeyCode kbBackKey    = KeyCode.Escape;

    [Header("Opciones")]
    [SerializeField] private bool wrapAround = true;

    // ── Estado interno ─────────────────────────────────────────
    private PanelData _currentPanel;
    private int       _selectedIndex  = 0;   // índice unificado: botones primero, sliders después
    private float     _navTimer       = 0f;
    private float     _sliderTimer    = 0f;
    private bool      _axisWasNeutral = true;
    private bool      _sliderAxisWasNeutral = true;
    private bool      _isActive       = true;

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Start()
    {
        // Desactiva la navegación automática del EventSystem
        // para evitar que JoystickButton0 active botones por su cuenta
        DisableEventSystemNavigation();

        foreach (var p in panels)
            if (p.rootObject != null)
                p.rootObject.SetActive(false);

        SwitchToPanel(initialPanelId);
    }

    private void Update()
    {
        if (!_isActive || _currentPanel == null) return;

        _navTimer    -= Time.unscaledDeltaTime;
        _sliderTimer -= Time.unscaledDeltaTime;

        HandleNavigationInput();
        HandleConfirmInput();
        HandleBackInput();
        HandleSliderInput();
    }

    // ── Desactiva navegación automática del EventSystem ────────

    private void DisableEventSystemNavigation()
    {
        // Desactiva el StandaloneInputModule para que no procese
        // JoystickButton0 como Submit automáticamente
        var inputModule = FindFirstObjectByType<StandaloneInputModule>();
        if (inputModule != null)
        {
            inputModule.submitButton    = "";   // vacía el botón de submit
            inputModule.cancelButton    = "";   // vacía el botón de cancel
            inputModule.horizontalAxis  = "";
            inputModule.verticalAxis    = "";
        }
    }

    // ── Cambio de panel ────────────────────────────────────────

    public void SwitchToPanel(string targetId)
    {
        if (_currentPanel != null && _currentPanel.rootObject != null)
            _currentPanel.rootObject.SetActive(false);

        PanelData found = null;
        foreach (var p in panels)
        {
            if (p.id == targetId) { found = p; break; }
        }

        if (found == null)
        {
            Debug.LogWarning($"[MenuNavigator] Panel '{targetId}' no encontrado.");
            return;
        }

        _currentPanel        = found;
        _selectedIndex       = 0;
        _navTimer            = 0f;
        _axisWasNeutral      = true;
        _sliderAxisWasNeutral = true;

        if (_currentPanel.rootObject != null)
            _currentPanel.rootObject.SetActive(true);

        RefreshSelection();
        Debug.Log($"[MenuNavigator] → Panel: {targetId}");
    }

    // ── Activación externa ─────────────────────────────────────

    public void SetActive(bool active)
    {
        _isActive = active;
        if (!active)
            EventSystem.current?.SetSelectedGameObject(null);
        else
            RefreshSelection();
    }

    // ── Navegación ─────────────────────────────────────────────

    private void HandleNavigationInput()
    {
        int totalItems = TotalItems();
        if (totalItems == 0) return;
        if (_navTimer > 0f) return;

        int dir = 0;

        if (_currentPanel.navDirection == NavDirection.Vertical)
        {
            float axisV = GetAxis(navAxisY);
            if (Mathf.Abs(axisV) >= navDeadzone && _axisWasNeutral)
                dir = axisV > 0f ? -1 : 1;

            if (Input.GetKeyDown(kbUpKey))   dir = -1;
            if (Input.GetKeyDown(kbDownKey)) dir =  1;

            _axisWasNeutral = Mathf.Abs(axisV) < navDeadzone;
        }
        else
        {
            float axisH = GetAxis(navAxisX);
            if (Mathf.Abs(axisH) >= navDeadzone && _axisWasNeutral)
                dir = axisH > 0f ? 1 : -1;

            if (Input.GetKeyDown(kbLeftKey))  dir = -1;
            if (Input.GetKeyDown(kbRightKey)) dir =  1;

            _axisWasNeutral = Mathf.Abs(axisH) < navDeadzone;
        }

        if (dir == 0) return;

        if (wrapAround)
            _selectedIndex = (_selectedIndex + dir + totalItems) % totalItems;
        else
            _selectedIndex = Mathf.Clamp(_selectedIndex + dir, 0, totalItems - 1);

        _navTimer = navRepeatDelay;
        RefreshSelection();
    }

    private void HandleConfirmInput()
    {
        bool confirm = Input.GetKeyDown(gamepadConfirmKey)
                    || Input.GetKeyDown(kbConfirmKey);
        if (!confirm) return;

        // Solo confirma si el índice apunta a un botón (no a un slider)
        int btnCount = ButtonCount();
        if (_selectedIndex < btnCount)
            PressButton(_selectedIndex);
    }

    private void HandleBackInput()
    {
        bool back = Input.GetKeyDown(gamepadBackKey)
                 || Input.GetKeyDown(kbBackKey);
        if (!back) return;

        if (_currentPanel.hasBackPanel)
        {
            SwitchToPanel(_currentPanel.backPanelId);
            AudioManager.Instance?.PlayUIButton();
        }
    }

    private void HandleSliderInput()
    {
        int btnCount    = ButtonCount();
        int sliderCount = SliderCount();
        if (sliderCount == 0) return;

        // ¿El índice actual apunta a un slider?
        int sliderIndex = _selectedIndex - btnCount;
        if (sliderIndex < 0 || sliderIndex >= sliderCount) return;

        Slider slider = _currentPanel.sliders[sliderIndex];
        if (slider == null) return;

        float axisH = GetAxis(sliderAxisX);
        bool  leftDown  = Input.GetKey(kbLeftKey);
        bool  rightDown = Input.GetKey(kbRightKey);

        float input = 0f;
        if (Mathf.Abs(axisH) >= navDeadzone) input = axisH;
        if (leftDown)  input = -1f;
        if (rightDown) input =  1f;

        if (Mathf.Abs(input) > 0f && _sliderTimer <= 0f)
        {
            slider.value = Mathf.Clamp01(slider.value + input * sliderStep * sliderRepeatDelay);
            _sliderTimer = sliderRepeatDelay;
        }
    }

    // ── Helpers de conteo ──────────────────────────────────────

    private int ButtonCount() =>
        (_currentPanel?.buttons != null) ? _currentPanel.buttons.Length : 0;

    private int SliderCount() =>
        (_currentPanel?.sliders != null) ? _currentPanel.sliders.Length : 0;

    private int TotalItems() => ButtonCount() + SliderCount();

    // ── Presionar botón ────────────────────────────────────────

    private void PressButton(int index)
    {
        if (_currentPanel.buttons == null || index >= _currentPanel.buttons.Length) return;

        Button btn = _currentPanel.buttons[index];
        if (btn != null && btn.interactable)
        {
            AudioManager.Instance?.PlayUIButton();
            btn.onClick.Invoke();   // invoca directamente sin pasar por EventSystem
        }
    }

    // ── Highlight visual ───────────────────────────────────────

    private void RefreshSelection()
    {
        if (_currentPanel == null) return;

        // Quita selección del EventSystem (solo visual, no funcional)
        EventSystem.current?.SetSelectedGameObject(null);

        int btnCount = ButtonCount();

        if (_selectedIndex < btnCount)
        {
            // Selecciona el botón visualmente
            Button btn = _currentPanel.buttons[_selectedIndex];
            if (btn != null)
                btn.Select();   // solo highlight, no dispara onClick
        }
        else
        {
            // Selecciona el slider visualmente
            int sliderIndex = _selectedIndex - btnCount;
            if (_currentPanel.sliders != null
                && sliderIndex < _currentPanel.sliders.Length
                && _currentPanel.sliders[sliderIndex] != null)
            {
                _currentPanel.sliders[sliderIndex].Select();
            }
        }
    }

    private float GetAxis(string axisName)
    {
        if (string.IsNullOrEmpty(axisName)) return 0f;
        try   { return Input.GetAxis(axisName); }
        catch { return 0f; }
    }
}
