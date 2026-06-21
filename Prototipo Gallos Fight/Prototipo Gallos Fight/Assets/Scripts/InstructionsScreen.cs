// ============================================================
//  InstructionsScreen.cs
//
//  Muestra una pantalla de instrucciones por un tiempo
//  configurable antes de cargar la escena destino.
//
//  FLUJO:
//   MainMenu → (elige Normal o Infinito) → Escena "Instructions"
//   → espera N segundos (o hasta que el jugador pulse cualquier
//     tecla si skipOnInput = true) → carga la escena destino
//     que fue guardada por el MainMenuManager.
//
//  SETUP EN UNITY:
//   1. Crea una escena nueva llamada "Instructions" y añádela
//      al Build Settings ENTRE el menú y Lvl1/LvlInfinite.
//   2. En la escena coloca:
//        - Main Camera
//        - Canvas (Screen Space - Overlay)
//            └── Image (ocupa toda la pantalla) ← asigna tu PNG aquí
//        - GameObject vacío "InstructionsManager" con este script
//   3. Asigna displayTime e indica si quieres skip con input.
//   4. En MainMenuManager cambia las escenas destino a
//      "Instructions" en lugar de "Lvl1" / "LvlInfinite".
//      El nombre de la escena real se pasa via PlayerPrefs.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class InstructionsScreen : MonoBehaviour
{
    [Header("── Tiempo ───────────────────────────────────────")]
    [Tooltip("Segundos que se muestra la pantalla de instrucciones.")]
    [SerializeField] private float displayTime = 5f;

    [Tooltip("Si está activo, el jugador puede pulsar cualquier tecla " +
             "o botón para saltar la pantalla antes de que termine el tiempo.")]
    [SerializeField] private bool skipOnInput = true;

    [Tooltip("Segundos mínimos antes de que el skip esté disponible " +
             "(evita saltar accidentalmente con la tecla que abrió el menú).")]
    [SerializeField] private float minTimeBeforeSkip = 0.5f;

    [Header("── UI ───────────────────────────────────────────")]
    [Tooltip("Imagen de fondo / instrucciones. Asigna el RectTransform " +
             "de la Image del Canvas.")]
    [SerializeField] private Image instructionsImage;

    [Tooltip("Texto opcional que muestra el tiempo restante o un mensaje de skip.")]
    [SerializeField] private TextMeshProUGUI skipHintText;

    [Tooltip("Texto que se muestra como hint de skip. " +
             "Deja vacío para no mostrar nada.")]
    [SerializeField] private string skipHintMessage = "Pulsa cualquier tecla para continuar...";

    [Header("── Transición ───────────────────────────────────")]
    [Tooltip("Duración del fade de entrada y salida (segundos). 0 = sin fade.")]
    [SerializeField] private float fadeDuration = 0.4f;

    // ── PlayerPrefs key ────────────────────────────────────────
    // MainMenuManager guarda aquí la escena destino antes de
    // cargar Instructions.
    public const string KEY_TARGET_SCENE = "instructions_target_scene";

    // ── Estado interno ─────────────────────────────────────────
    private float  _elapsed      = 0f;
    private bool   _skipping     = false;
    private string _targetScene;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        // Lee la escena destino guardada por MainMenuManager
        _targetScene = PlayerPrefs.GetString(KEY_TARGET_SCENE, "Lvl1");

        // Muestra el hint de skip si hay texto configurado
        if (skipHintText != null)
        {
            skipHintText.text    = skipOnInput ? skipHintMessage : string.Empty;
            skipHintText.enabled = skipOnInput && !string.IsNullOrEmpty(skipHintMessage);
        }

        // Fade de entrada
        StartCoroutine(RunInstructions());
    }

    // ══════════════════════════════════════════════════════════
    //  FLUJO PRINCIPAL
    // ══════════════════════════════════════════════════════════

    private IEnumerator RunInstructions()
    {
        // ── Fade in ───────────────────────────────────────────
        if (fadeDuration > 0f && instructionsImage != null)
            yield return StartCoroutine(FadeImage(instructionsImage, 0f, 1f, fadeDuration));

        // ── Espera (con posibilidad de skip) ─────────────────
        _elapsed = 0f;
        while (_elapsed < displayTime)
        {
            _elapsed += Time.deltaTime;

            // Skip por input (solo después del tiempo mínimo)
            if (skipOnInput && _elapsed >= minTimeBeforeSkip)
            {
                if (Input.anyKeyDown)
                    break;
            }

            yield return null;
        }

        // ── Fade out ──────────────────────────────────────────
        if (!_skipping)
        {
            _skipping = true;

            if (fadeDuration > 0f && instructionsImage != null)
                yield return StartCoroutine(FadeImage(instructionsImage, 1f, 0f, fadeDuration));
        }

        // ── Carga la escena destino ───────────────────────────
        SceneManager.LoadScene(_targetScene);
    }

    // ══════════════════════════════════════════════════════════
    //  FADE
    // ══════════════════════════════════════════════════════════

    private IEnumerator FadeImage(Image img, float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        Color c = img.color;

        while (elapsed < duration)
        {
            elapsed   += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / duration);
            c.a        = Mathf.Lerp(fromAlpha, toAlpha, t);
            img.color  = c;
            yield return null;
        }

        c.a       = toAlpha;
        img.color = c;
    }
}
