
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class DeathScreenOverlay : MonoBehaviour
{
    public static DeathScreenOverlay Instance { get; private set; }

    public enum Result { Defeat, Victory }

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════

    [Header("── Referencias ──────────────────────────────────")]
    [Tooltip("CanvasGroup que controla el fade del overlay completo. " +
             "Se busca automáticamente si queda vacío.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Texto del mensaje. Cambia según el resultado.")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("── Mensajes ─────────────────────────────────────")]
    [SerializeField] private string defeatMessage  = "HAS MUERTO";
    [SerializeField] private string victoryMessage = "¡VICTORIA!";

    [Header("── Audio ────────────────────────────────────────")]
    [Tooltip("Sonido al mostrar la pantalla de derrota.")]
    [SerializeField] private AudioClip defeatSound;

    [Tooltip("Sonido al mostrar la pantalla de victoria.")]
    [SerializeField] private AudioClip victorySound;

    [Header("── Animación ───────────────────────────────────")]
    [Tooltip("Segundos que tarda el fade-in (de transparente a opaco).")]
    [SerializeField] private float fadeInDuration = 2f;

    [Tooltip("Segundos que el overlay permanece visible antes de poder " +
             "avanzar (incluso con input).")]
    [SerializeField] private float minHoldDuration = 1.5f;

    [Tooltip("Segundos totales antes de avanzar automáticamente. " +
             "0 = espera indefinidamente hasta input (si allowSkip está activo).")]
    [SerializeField] private float totalDisplayDuration = 5f;

    [Tooltip("Segundos que tarda el fade-out antes de cargar el menú.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Tooltip("Permite saltar pulsando cualquier tecla (después de minHoldDuration).")]
    [SerializeField] private bool allowSkip = true;

    [Header("── Escena destino ──────────────────────────────")]
    [SerializeField] private string sceneMenu = "Menu";

    // ── Estado interno ─────────────────────────────────────────
    private bool _isShowing = false;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ══════════════════════════════════════════════════════════
    //  API PÚBLICA
    // ══════════════════════════════════════════════════════════

    /// Muestra la pantalla de fin de partida con fade-in.
    /// result: Defeat o Victory — cambia texto y sonido, misma animación.
    public void Show(Result result)
    {
        if (_isShowing) return;
        _isShowing = true;

        string    message = (result == Result.Victory) ? victoryMessage : defeatMessage;
        AudioClip sound    = (result == Result.Victory) ? victorySound   : defeatSound;

        if (messageText != null)
            messageText.text = message;

        PlaySound(sound);

        canvasGroup.blocksRaycasts = true;
        StartCoroutine(EndSequence());
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        // AudioSource temporal independiente del AudioManager pool
        GameObject tempAudio = new GameObject("EndScreenSound");
        AudioSource src = tempAudio.AddComponent<AudioSource>();
        src.clip = clip;
        src.Play();
        Destroy(tempAudio, clip.length + 0.5f);
    }

    // ══════════════════════════════════════════════════════════
    //  SECUENCIA
    // ══════════════════════════════════════════════════════════

    private IEnumerator EndSequence()
    {
        // ── Fade in ───────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // ── Hold ──────────────────────────────────────────────
        float holdElapsed = 0f;

        while (true)
        {
            holdElapsed += Time.unscaledDeltaTime;

            bool minHoldReached = holdElapsed >= minHoldDuration;
            bool autoAdvance    = totalDisplayDuration > 0f
                                && holdElapsed >= totalDisplayDuration;
            bool inputSkip      = allowSkip && minHoldReached && AnyInputPressed();

            if (autoAdvance || inputSkip)
                break;

            yield return null;
        }

        // ── Fade out ──────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // ── Carga el menú ───────────────────────────────────
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneMenu);
    }

    private bool AnyInputPressed()
    {
        if (Input.anyKeyDown) return true;
        if (Input.GetKeyDown(KeyCode.JoystickButton0)) return true;
        if (Input.GetKeyDown(KeyCode.JoystickButton1)) return true;
        if (Input.GetKeyDown(KeyCode.JoystickButton2)) return true;
        if (Input.GetKeyDown(KeyCode.JoystickButton3)) return true;
        return false;
    }
}
