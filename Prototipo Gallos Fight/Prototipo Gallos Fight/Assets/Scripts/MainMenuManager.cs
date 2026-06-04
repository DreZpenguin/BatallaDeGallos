// ============================================================
//  MainMenuManager.cs
//
//  Gestiona todos los paneles del menú principal:
//   · Main        → botones Jugar, Práctica, Opciones, Créditos, Salir
//   · ModoDeJuego → botones Modo Normal, Modo Infinito, Volver
//   · Options     → sliders de audio, checkbox fullscreen, Volver
//   · Credits     → bibliografía, Volver
//
//  SETUP EN UNITY:
//   1. Añade este script al Canvas (o a un hijo "MenuManager").
//   2. Crea la jerarquía de paneles en el Canvas y asígnalos.
//   3. Los sliders deben tener valor mínimo 0.001 y máximo 1
//      (logarítmico: usa Mathf.Log10 internamente para dB).
//   4. Crea un AudioMixer en Assets con tres grupos expuestos:
//      "MasterVolume", "MusicVolume", "SFXVolume".
//   5. Asigna el AudioMixer al campo audioMixer del Inspector.
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  PANELES
    // ══════════════════════════════════════════════════════════

    [Header("── Paneles ──────────────────────────────────────")]
    [Tooltip("Panel raíz del menú principal (Jugar, Práctica, Opciones, Créditos, Salir).")]
    [SerializeField] private GameObject panelMain;

    [Tooltip("Panel de selección de modo de juego (Normal, Infinito, Volver).")]
    [SerializeField] private GameObject panelModoDeJuego;

    [Tooltip("Panel de opciones de audio y pantalla.")]
    [SerializeField] private GameObject panelOptions;

    [Tooltip("Panel de créditos / bibliografía.")]
    [SerializeField] private GameObject panelCredits;

    // ══════════════════════════════════════════════════════════
    //  AUDIO
    // ══════════════════════════════════════════════════════════

    [Header("── Audio Mixer ──────────────────────────────────")]
    [Tooltip("AudioMixer principal del proyecto. Debe tener expuestos: " +
             "MasterVolume, MusicVolume, SFXVolume.")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("── Sliders de Audio ────────────────────────────")]
    [Tooltip("Slider de volumen general (0.001 – 1).")]
    [SerializeField] private Slider sliderMaster;

    [Tooltip("Slider de volumen de música (0.001 – 1).")]
    [SerializeField] private Slider sliderMusic;

    [Tooltip("Slider de volumen de SFX (0.001 – 1).")]
    [SerializeField] private Slider sliderSFX;

    // ── Nombres de los parámetros expuestos en el AudioMixer ──
    private const string PARAM_MASTER = "MasterVolume";
    private const string PARAM_MUSIC  = "MusicVolume";
    private const string PARAM_SFX    = "SFXVolume";

    // ── PlayerPrefs keys ──────────────────────────────────────
    private const string KEY_MASTER = "audio_master";
    private const string KEY_MUSIC  = "audio_music";
    private const string KEY_SFX    = "audio_sfx";

    // ══════════════════════════════════════════════════════════
    //  PANTALLA
    // ══════════════════════════════════════════════════════════

    [Header("── Pantalla ─────────────────────────────────────")]
    [Tooltip("Toggle de pantalla completa.")]
    [SerializeField] private Toggle toggleFullscreen;

    // ══════════════════════════════════════════════════════════
    //  ESCENAS
    // ══════════════════════════════════════════════════════════

    [Header("── Escenas ──────────────────────────────────────")]
    [Tooltip("Nombre exacto de la escena del modo normal (primer nivel).")]
    [SerializeField] private string sceneNormal   = "Lvl1";

    [Tooltip("Nombre exacto de la escena del modo infinito.")]
    [SerializeField] private string sceneInfinite = "LvlInfiniteV2";

    [Tooltip("Nombre exacto de la escena de práctica.")]
    [SerializeField] private string scenePractice = "Practice";

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        // Activa solo el panel principal
        ShowPanel(panelMain);

        // Carga y aplica los valores de audio guardados
        LoadAudioSettings();

        // Estado inicial del fullscreen toggle
        if (toggleFullscreen != null)
        {
            toggleFullscreen.isOn = Screen.fullScreen;
            toggleFullscreen.onValueChanged.AddListener(SetFullscreen);
        }

        // Conecta sliders a sus callbacks
        if (sliderMaster != null) sliderMaster.onValueChanged.AddListener(SetMasterVolume);
        if (sliderMusic  != null) sliderMusic.onValueChanged.AddListener(SetMusicVolume);
        if (sliderSFX    != null) sliderSFX.onValueChanged.AddListener(SetSFXVolume);
    }

    // ══════════════════════════════════════════════════════════
    //  NAVEGACIÓN DE PANELES
    // ══════════════════════════════════════════════════════════

    private void ShowPanel(GameObject target)
    {
        panelMain?.SetActive(false);
        panelModoDeJuego?.SetActive(false);
        panelOptions?.SetActive(false);
        panelCredits?.SetActive(false);

        target?.SetActive(true);
    }

    // ── Llamados por los botones del menú principal ────────────

    public void OpenModoDeJuego() => ShowPanel(panelModoDeJuego);
    public void OpenOptions()
    {
        ShowPanel(panelOptions);
        // Refresca los sliders con los valores actuales al abrir
        RefreshSliders();
    }
    public void OpenCredits()     => ShowPanel(panelCredits);
    public void BackToMain()      => ShowPanel(panelMain);

    // ══════════════════════════════════════════════════════════
    //  CARGA DE ESCENAS
    // ══════════════════════════════════════════════════════════

    public void PlayNormal()
    {
        // Resetea datos del modo infinito por si venía de ahí
        if (InfiniteData.Instance != null)
            InfiniteData.Instance.ResetInfiniteRun();

        SceneManager.LoadScene(sceneNormal);
    }

    public void PlayInfinite()
    {
        if (InfiniteData.Instance != null)
            InfiniteData.Instance.StartInfiniteRun();
        else
            Debug.LogWarning("[MainMenuManager] InfiniteData no encontrado.");

        SceneManager.LoadScene(sceneInfinite);
    }

    public void PlayPractice()
    {
        SceneManager.LoadScene(scenePractice);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("[MainMenuManager] Saliendo del juego.");
    }

    // ══════════════════════════════════════════════════════════
    //  AUDIO
    // ══════════════════════════════════════════════════════════

    /// Convierte valor lineal del slider (0.001–1) a dB y lo aplica al mixer.
    private void ApplyVolume(string parameter, float linearValue)
    {
        if (audioMixer == null) return;
        // Fórmula estándar: dB = 20 * log10(valor lineal)
        float dB = Mathf.Log10(Mathf.Max(linearValue, 0.001f)) * 20f;
        audioMixer.SetFloat(parameter, dB);
    }

    public void SetMasterVolume(float value)
    {
        ApplyVolume(PARAM_MASTER, value);
        PlayerPrefs.SetFloat(KEY_MASTER, value);
    }

    public void SetMusicVolume(float value)
    {
        ApplyVolume(PARAM_MUSIC, value);
        PlayerPrefs.SetFloat(KEY_MUSIC, value);
    }

    public void SetSFXVolume(float value)
    {
        ApplyVolume(PARAM_SFX, value);
        PlayerPrefs.SetFloat(KEY_SFX, value);
    }

    private void LoadAudioSettings()
    {
        float master = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float music  = PlayerPrefs.GetFloat(KEY_MUSIC,  1f);
        float sfx    = PlayerPrefs.GetFloat(KEY_SFX,    1f);

        ApplyVolume(PARAM_MASTER, master);
        ApplyVolume(PARAM_MUSIC,  music);
        ApplyVolume(PARAM_SFX,    sfx);

        // Actualiza los sliders sin disparar callbacks (evita loop)
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
        Debug.Log($"[MainMenuManager] Fullscreen: {isFullscreen}");
    }
}
