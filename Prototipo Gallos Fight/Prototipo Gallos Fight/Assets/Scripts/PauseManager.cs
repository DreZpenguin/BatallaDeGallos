// ============================================================
//  PauseManager.cs
//
//  Gestiona el menú de pausa in-game.
//  · ESC (o botón de mando configurable) abre/cierra la pausa.
//  · Panel principal de pausa: Reanudar, Opciones, Menú Principal.
//  · Subpanel de Opciones: sliders de audio + fullscreen + Volver.
//  · Usa el mismo AudioMixer que el menú principal.
//  · Time.timeScale = 0 mientras está pausado.
//
//  SETUP EN UNITY:
//   1. Añade este script a un GameObject en la escena de juego.
//   2. Crea en el Canvas de la escena:
//        Canvas
//         └── PauseRoot (desactivado por defecto)
//              ├── PausePanel      → botones Reanudar, Opciones, Menú Principal
//              └── PauseOptions    → sliders, toggle fullscreen, botón Volver
//   3. Asigna todos los campos del Inspector.
//   4. Asigna el mismo AudioMixer que usa MainMenuManager.
//   5. Solo activa este script en escenas de juego
//      (Lvl1–Lvl5, LvlInfinite, Practice).
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public class PauseManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  PANELES
    // ══════════════════════════════════════════════════════════

    [Header("── Paneles de Pausa ────────────────────────────")]
    [Tooltip("Raíz que contiene todos los paneles de pausa. " +
             "Debe estar desactivado al inicio.")]
    [SerializeField] private GameObject pauseRoot;

    [Tooltip("Panel principal de pausa (Reanudar, Opciones, Menú Principal).")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("Subpanel de opciones dentro de la pausa.")]
    [SerializeField] private GameObject pauseOptionsPanel;

    // ══════════════════════════════════════════════════════════
    //  AUDIO
    // ══════════════════════════════════════════════════════════

    [Header("── Audio Mixer ──────────────────────────────────")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("── Sliders de Audio (en el panel de opciones) ──")]
    [SerializeField] private Slider sliderMaster;
    [SerializeField] private Slider sliderMusic;
    [SerializeField] private Slider sliderSFX;

    private const string PARAM_MASTER = "MasterVolume";
    private const string PARAM_MUSIC  = "MusicVolume";
    private const string PARAM_SFX    = "SFXVolume";
    private const string KEY_MASTER   = "audio_master";
    private const string KEY_MUSIC    = "audio_music";
    private const string KEY_SFX      = "audio_sfx";

    // ══════════════════════════════════════════════════════════
    //  PANTALLA
    // ══════════════════════════════════════════════════════════

    [Header("── Pantalla ─────────────────────────────────────")]
    [SerializeField] private Toggle toggleFullscreen;

    // ══════════════════════════════════════════════════════════
    //  CONTROL
    // ══════════════════════════════════════════════════════════

    [Header("── Control ──────────────────────────────────────")]
    [Tooltip("Tecla para abrir/cerrar la pausa.")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    [Tooltip("Botón de mando para pausar (Start = joystick button 7 en Xbox).")]
    [SerializeField] private KeyCode gamepadPauseKey = KeyCode.JoystickButton7;

    // ══════════════════════════════════════════════════════════
    //  ESCENAS
    // ══════════════════════════════════════════════════════════

    [Header("── Escenas ──────────────────────────────────────")]
    [Tooltip("Nombre o índice de la escena del menú principal.")]
    [SerializeField] private string sceneMenu = "Menu";

    // ── Estado interno ─────────────────────────────────────────
    private bool _isPaused = false;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        // Asegura que la pausa empiece desactivada
        if (pauseRoot != null) pauseRoot.SetActive(false);
        Time.timeScale = 1f;

        // Carga configuración de audio guardada y aplica sliders
        LoadAudioSettings();

        // Conecta sliders
        if (sliderMaster != null) sliderMaster.onValueChanged.AddListener(SetMasterVolume);
        if (sliderMusic  != null) sliderMusic.onValueChanged.AddListener(SetMusicVolume);
        if (sliderSFX    != null) sliderSFX.onValueChanged.AddListener(SetSFXVolume);

        // Fullscreen toggle
        if (toggleFullscreen != null)
        {
            toggleFullscreen.isOn = Screen.fullScreen;
            toggleFullscreen.onValueChanged.AddListener(SetFullscreen);
        }
    }

    private void Update()
    {
        bool pausePressed = Input.GetKeyDown(pauseKey) || Input.GetKeyDown(gamepadPauseKey);

        if (pausePressed)
        {
            if (_isPaused)
                Resume();
            else
                Pause();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PAUSA / REANUDAR
    // ══════════════════════════════════════════════════════════

    public void Pause()
    {
        _isPaused      = true;
        Time.timeScale = 0f;

        if (pauseRoot != null) pauseRoot.SetActive(true);
        ShowPausePanel();

        Debug.Log("[PauseManager] Juego pausado.");
    }

    public void Resume()
    {
        _isPaused      = false;
        Time.timeScale = 1f;

        if (pauseRoot != null) pauseRoot.SetActive(false);

        Debug.Log("[PauseManager] Juego reanudado.");
    }

    // ══════════════════════════════════════════════════════════
    //  NAVEGACIÓN DE PANELES
    // ══════════════════════════════════════════════════════════

    private void ShowPausePanel()
    {
        if (pausePanel != null)        pausePanel.SetActive(true);
        if (pauseOptionsPanel != null) pauseOptionsPanel.SetActive(false);
    }

    public void OpenPauseOptions()
    {
        if (pausePanel != null)        pausePanel.SetActive(false);
        if (pauseOptionsPanel != null) pauseOptionsPanel.SetActive(true);
        RefreshSliders();
    }

    public void BackToPauseMain()
    {
        ShowPausePanel();
    }

    // ══════════════════════════════════════════════════════════
    //  BOTONES DE PAUSA
    // ══════════════════════════════════════════════════════════

    /// Botón "Reanudar"
    public void OnResumeButton() => Resume();

    /// Botón "Menú Principal"
    public void OnMainMenuButton()
    {
        Time.timeScale = 1f;
        _isPaused = false;

        // Resetea datos de modo infinito si estaba activo
        if (InfiniteData.Instance != null && InfiniteData.Instance.IsInfiniteMode)
            InfiniteData.Instance.ResetInfiniteRun();

        // Resetea powerups si sale al menú
        if (PlayerData.Instance != null)
            PlayerData.Instance.ResetAll();
        StatsModal.ClearSavedLevels();

        SceneManager.LoadScene(sceneMenu);
    }

    // ══════════════════════════════════════════════════════════
    //  AUDIO
    // ══════════════════════════════════════════════════════════

    private void ApplyVolume(string parameter, float linearValue)
    {
        if (audioMixer == null) return;
        float dB = Mathf.Log10(Mathf.Max(linearValue, 0.001f)) * 20f;
        audioMixer.SetFloat(parameter, dB);
    }

    public void SetMasterVolume(float value)
    {
        ApplyVolume(PARAM_MASTER, value);
        PlayerPrefs.SetFloat(KEY_MASTER, value);
        AudioManager.Instance?.PlaySliderSFX();
    }

    public void SetMusicVolume(float value)
    {
        ApplyVolume(PARAM_MUSIC, value);
        PlayerPrefs.SetFloat(KEY_MUSIC, value);
        AudioManager.Instance?.PlaySliderMusic();
    }

    public void SetSFXVolume(float value)
    {
        ApplyVolume(PARAM_SFX, value);
        PlayerPrefs.SetFloat(KEY_SFX, value);
        AudioManager.Instance?.PlaySliderSFX();
    }

    private void LoadAudioSettings()
    {
        float master = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float music  = PlayerPrefs.GetFloat(KEY_MUSIC,  1f);
        float sfx    = PlayerPrefs.GetFloat(KEY_SFX,    1f);

        ApplyVolume(PARAM_MASTER, master);
        ApplyVolume(PARAM_MUSIC,  music);
        ApplyVolume(PARAM_SFX,    sfx);

        if (sliderMaster != null) sliderMaster.SetValueWithoutNotify(master);
        if (sliderMusic  != null) sliderMusic.SetValueWithoutNotify(music);
        if (sliderSFX    != null) sliderSFX.SetValueWithoutNotify(sfx);
    }

    private void RefreshSliders()
    {
        if (sliderMaster != null) sliderMaster.SetValueWithoutNotify(PlayerPrefs.GetFloat(KEY_MASTER, 1f));
        if (sliderMusic  != null) sliderMusic.SetValueWithoutNotify(PlayerPrefs.GetFloat(KEY_MUSIC,   1f));
        if (sliderSFX    != null) sliderSFX.SetValueWithoutNotify(PlayerPrefs.GetFloat(KEY_SFX,       1f));
    }

    // ══════════════════════════════════════════════════════════
    //  PANTALLA
    // ══════════════════════════════════════════════════════════

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("fullscreen", isFullscreen ? 1 : 0);
    }

    // ══════════════════════════════════════════════════════════
    //  PROPIEDAD PÚBLICA
    // ══════════════════════════════════════════════════════════

    public bool IsPaused => _isPaused;
}
