// ============================================================
//  MenuNavigator.cs  — v1
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuNavigator : MonoBehaviour
{
    // ── Enum de paneles ────────────────────────────────────────
    /// Extiende este enum para añadir nuevos menús sin tocar otra lógica.
    public enum MenuPanel
    {
        Main,
        Options,
        Credits,
        // Añade aquí: Pause, Inventory, etc.
    }

    // ── Datos de un panel ──────────────────────────────────────
    [Serializable]
    public class PanelData
    {
        [Tooltip("Identificador del panel (debe coincidir con el enum MenuPanel).")]
        public MenuPanel id;

        [Tooltip("GameObject raíz del panel. Se activa/desactiva al cambiar de panel.")]
        public GameObject rootObject;

        [Tooltip("Botones del panel en orden de navegación (arriba→abajo o izq→der).")]
        public Button[] buttons;

        [Tooltip("Dirección de navegación: Vertical (arriba/abajo) u Horizontal (izq/der).")]
        public NavDirection navDirection = NavDirection.Vertical;

        [Tooltip("Panel al que vuelve el botón B / Escape. Deja vacío para no hacer nada.")]
        public bool hasBackPanel = false;
        public MenuPanel backPanel = MenuPanel.Main;
    }

    public enum NavDirection { Vertical, Horizontal }

    // ── Inspector ──────────────────────────────────────────────

    [Header("Paneles del menú")]
    [SerializeField] private PanelData[] panels;

    [Header("Panel inicial")]
    [SerializeField] private MenuPanel initialPanel = MenuPanel.Main;

    [Header("Control — Mando Xbox")]
    [Tooltip("Eje de navegación vertical (D-Pad / stick izq). Configura 'DPadY' en el Input Manager.")]
    [SerializeField] private string navAxisY = "DPadY";

    [Tooltip("Eje de navegación horizontal (D-Pad / stick izq). Para paneles horizontales.")]
    [SerializeField] private string navAxisX = "DPadX";

    [Tooltip("Botón de confirmación del mando (A = joystick button 0).")]
    [SerializeField] private KeyCode gamepadConfirmKey = KeyCode.JoystickButton0;

    [Tooltip("Botón de cancelar / volver (B = joystick button 1).")]
    [SerializeField] private KeyCode gamepadBackKey = KeyCode.JoystickButton1;

    [Tooltip("Umbral del eje para considerar que hay input de navegación.")]
    [SerializeField, Range(0.1f, 0.9f)] private float navDeadzone = 0.5f;

    [Tooltip("Segundos entre cada movimiento de navegación (evita saltos rápidos).")]
    [SerializeField] private float navRepeatDelay = 0.2f;

    [Header("Control — Teclado (fallback)")]
    [SerializeField] private KeyCode kbUpKey      = KeyCode.UpArrow;
    [SerializeField] private KeyCode kbDownKey    = KeyCode.DownArrow;
    [SerializeField] private KeyCode kbLeftKey    = KeyCode.LeftArrow;
    [SerializeField] private KeyCode kbRightKey   = KeyCode.RightArrow;
    [SerializeField] private KeyCode kbConfirmKey = KeyCode.Return;
    [SerializeField] private KeyCode kbBackKey    = KeyCode.Escape;

    [Header("Opciones")]
    [Tooltip("Si está activo, al llegar al último botón el cursor salta al primero (y viceversa).")]
    [SerializeField] private bool wrapAround = true;

    // ── Estado interno ─────────────────────────────────────────
    private PanelData _currentPanel;
    private int       _selectedIndex  = 0;
    private float     _navTimer       = 0f;
    private bool      _axisWasNeutral = true;
    private bool      _isActive       = true;   // permite pausar la navegación externamente

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Start()
    {
        // Desactiva todos los paneles al inicio y activa el inicial
        foreach (var p in panels)
            if (p.rootObject != null)
                p.rootObject.SetActive(false);

        SwitchToPanel(initialPanel);
    }

    private void Update()
    {
        if (!_isActive || _currentPanel == null) return;

        _navTimer -= Time.unscaledDeltaTime;

        HandleNavigationInput();
        HandleConfirmInput();
        HandleBackInput();
    }

    // ── Cambio de panel ────────────────────────────────────────

    /// Cambia al panel indicado. Desactiva el panel actual y activa el nuevo.
    public void SwitchToPanel(MenuPanel target)
    {
        // Desactiva panel actual
        if (_currentPanel != null && _currentPanel.rootObject != null)
            _currentPanel.rootObject.SetActive(false);

        // Busca el nuevo panel
        PanelData found = null;
        foreach (var p in panels)
        {
            if (p.id == target)
            {
                found = p;
                break;
            }
        }

        if (found == null)
        {
            Debug.LogWarning($"[MenuNavigator] Panel '{target}' no encontrado en el array 'panels'.");
            return;
        }

        _currentPanel  = found;
        _selectedIndex = 0;
        _navTimer      = 0f;
        _axisWasNeutral = true;

        if (_currentPanel.rootObject != null)
            _currentPanel.rootObject.SetActive(true);

        RefreshSelection();
        Debug.Log($"[MenuNavigator] Cambió al panel: {target}");
    }

    // ── Activación externa ─────────────────────────────────────

    /// Activa o pausa la navegación (útil cuando otro sistema toma el control, ej. PowerUpUICanvas).
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
        if (_currentPanel.buttons == null || _currentPanel.buttons.Length == 0) return;
        if (_navTimer > 0f) return;

        int dir = 0;

        if (_currentPanel.navDirection == NavDirection.Vertical)
        {
            float axisV = GetAxis(navAxisY);

            // Eje analógico
            if (Mathf.Abs(axisV) >= navDeadzone && _axisWasNeutral)
                dir = axisV > 0f ? -1 : 1;   // eje Y positivo = D-Pad arriba = subir índice

            // Teclado
            if (Input.GetKeyDown(kbUpKey))   dir = -1;
            if (Input.GetKeyDown(kbDownKey))  dir =  1;

            _axisWasNeutral = Mathf.Abs(axisV) < navDeadzone;
        }
        else // Horizontal
        {
            float axisH = GetAxis(navAxisX);

            if (Mathf.Abs(axisH) >= navDeadzone && _axisWasNeutral)
                dir = axisH > 0f ? 1 : -1;

            if (Input.GetKeyDown(kbLeftKey))  dir = -1;
            if (Input.GetKeyDown(kbRightKey)) dir =  1;

            _axisWasNeutral = Mathf.Abs(axisH) < navDeadzone;
        }

        if (dir == 0) return;

        int count = _currentPanel.buttons.Length;
        if (wrapAround)
            _selectedIndex = (_selectedIndex + dir + count) % count;
        else
            _selectedIndex = Mathf.Clamp(_selectedIndex + dir, 0, count - 1);

        _navTimer = navRepeatDelay;
        RefreshSelection();
    }

    private void HandleConfirmInput()
    {
        bool confirm = Input.GetKeyDown(gamepadConfirmKey) || Input.GetKeyDown(kbConfirmKey);
        if (!confirm) return;

        PressCurrentButton();
    }

    private void HandleBackInput()
    {
        bool back = Input.GetKeyDown(gamepadBackKey) || Input.GetKeyDown(kbBackKey);
        if (!back) return;

        if (_currentPanel.hasBackPanel)
        {
            SwitchToPanel(_currentPanel.backPanel);
            AudioManager.Instance?.PlayPowerUpSelect(); // reutiliza el sonido de UI
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private void PressCurrentButton()
    {
        if (_currentPanel.buttons == null || _currentPanel.buttons.Length == 0) return;
        if (_selectedIndex >= _currentPanel.buttons.Length) return;

        Button btn = _currentPanel.buttons[_selectedIndex];
        if (btn != null && btn.interactable)
        {
            AudioManager.Instance?.PlayPowerUpSelect(); // sonido de confirmación de UI
            btn.onClick.Invoke();
        }
    }

    private void RefreshSelection()
    {
        if (_currentPanel == null || _currentPanel.buttons == null) return;

        // Limpia selección anterior
        EventSystem.current?.SetSelectedGameObject(null);

        if (_selectedIndex < _currentPanel.buttons.Length && _currentPanel.buttons[_selectedIndex] != null)
            EventSystem.current?.SetSelectedGameObject(_currentPanel.buttons[_selectedIndex].gameObject);
    }

    private float GetAxis(string axisName)
    {
        if (string.IsNullOrEmpty(axisName)) return 0f;
        try   { return Input.GetAxis(axisName); }
        catch { return 0f; }
    }

    // ── ContextMenu (testing rápido desde Editor) ──────────────

    [ContextMenu("Test → Switch to Main")]
    private void TestMain()    => SwitchToPanel(MenuPanel.Main);

    [ContextMenu("Test → Switch to Options")]
    private void TestOptions() => SwitchToPanel(MenuPanel.Options);

    [ContextMenu("Test → Switch to Credits")]
    private void TestCredits() => SwitchToPanel(MenuPanel.Credits);
}
