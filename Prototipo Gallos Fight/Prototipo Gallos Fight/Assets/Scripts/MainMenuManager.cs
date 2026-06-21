// ============================================================
//  MainMenuManager.cs  — v2
//
//  CAMBIO respecto a v1:
//   · PlayNormal() ahora guarda el índice de nivel (0 para Lvl1)
//     en PlayerPrefs con CutsceneScreen.KEY_LEVEL_INDEX y carga
//     la escena "Cutscene" en lugar de "Instructions".
//   · PlayInfinite() y PlayPractice() no cambian.
//   · Se añade el campo sceneCutscene en el Inspector.
//
//  El resto del archivo es idéntico a v1.
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
    [SerializeField] private AudioMixer audioMixer;

    [Header("── Sliders de Audio ────────────────────────────")]
    [SerializeField] private Slider sliderMaster;
    [SerializeField] private Slider sliderMusic;
    [SerializeField] private Slider sliderSFX;

    private const string PARAM_MASTER = "MasterVolume";
    private const string PARAM_MUSIC  = "MusicVolume";
    private const string PARAM_SFX    = "SFXVolume";

    private const string KEY_MASTER = "audio_master";
    private const string KEY_MUSIC  = "audio_music";
    private const string KEY_SFX    = "audio_sfx";

    // ══════════════════════════════════════════════════════════
    //  PANTALLA
    // ══════════════════════════════════════════════════════════

    [Header("── Pantalla ─────────────────────────────────────")]
    [SerializeField] private Toggle toggleFullscreen;

    // ══════════════════════════════════════════════════════════
    //  ESCENAS
    // ══════════════════════════════════════════════════════════

    [Header("── Escenas ──────────────────────────────────────")]
    [Tooltip("Nombre de la escena de cutscene (introducción animada por nivel).")]
    [SerializeField] private string sceneCutscene   = "Cutscene";

    [Tooltip("Nombre de la escena de instrucciones (usada solo por Infinito si quieres).")]
    [SerializeField] private string sceneInstructions = "Instructions";

    [Tooltip("Nombre exacto de la escena del modo infinito.")]
    [SerializeField] private string sceneInfinite   = "LvlInfiniteV2";

    [Tooltip("Nombre exacto de la escena de práctica.")]
    [SerializeField] private string scenePractice   = "PracticeLvl";

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        ShowPanel(panelMain);
        LoadAudioSettings();

        if (toggleFullscreen != null)
        {
            toggleFullscreen.isOn = Screen.fullScreen;
            toggleFullscreen.onValueChanged.AddListener(SetFullscreen);
        }

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

    public void OpenModoDeJuego() => ShowPanel(panelModoDeJuego);
    public void OpenOptions()
    {
        ShowPanel(panelOptions);
        RefreshSliders();
    }
    public void OpenCredits() => ShowPanel(panelCredits);
    public void BackToMain()  => ShowPanel(panelMain);

    // ══════════════════════════════════════════════════════════
    //  CARGA DE ESCENAS
    // ══════════════════════════════════════════════════════════

    /// Modo Normal: guarda índice 0 (Lvl1 es el primer entry)
    /// y carga la cutscene de introducción.
    public void PlayNormal()
    {
        if (InfiniteData.Instance != null)
            InfiniteData.Instance.ResetInfiniteRun();

        // Índice 0 → primer LevelEntry del CutsceneData (Lvl1)
        PlayerPrefs.SetInt(CutsceneScreen.KEY_LEVEL_INDEX, 0);
        PlayerPrefs.Save();

        SceneManager.LoadScene(sceneCutscene);
    }

    public void PlayInfinite()
    {
        if (InfiniteData.Instance != null)
            InfiniteData.Instance.StartInfiniteRun();
        else
            Debug.LogWarning("[MainMenuManager] InfiniteData no encontrado.");

        PlayerPrefs.SetString(InstructionsScreen.KEY_TARGET_SCENE, sceneInfinite);
        SceneManager.LoadScene(sceneInstructions);
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
