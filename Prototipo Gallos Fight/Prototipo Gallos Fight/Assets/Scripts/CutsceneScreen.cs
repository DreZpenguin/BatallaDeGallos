// ============================================================
//  CutsceneScreen.cs
//
//  Pantalla de introducción animada para cada nivel del modo
//  Normal. Se comporta como InstructionsScreen pero con sprites
//  animados mediante OnGUI.
//
//  ANIMACIÓN:
//   · Fase 1 — Entrada (durée = slideInDuration):
//       - Jugador: entra desde la izquierda (fuera de pantalla)
//         hacia la esquina inferior izquierda.
//       - Enemigos: entran desde la derecha (fuera de pantalla)
//         hacia la esquina superior derecha.
//       - Logo VS: hace zoom desde 0 hasta su tamaño final,
//         centrado en pantalla. Aparece con un pequeño rebote.
//   · Fase 2 — Hold (durée = holdDuration):
//       Todo se queda quieto. El jugador puede pulsar cualquier
//       tecla para saltar (después de minTimeBeforeSkip).
//   · Fase 3 — Salida (durée = slideOutDuration):
//       Todo hace fade-out simultáneo.
//   → Carga la escena destino.
//
//  SETUP EN UNITY:
//   1. Crea una escena "Cutscene" y añádela al Build Settings
//      ENTRE Instructions y Lvl1 (o después de Instructions
//      si no la usas para el modo Normal).
//   2. Añade un GameObject vacío "CutsceneManager" con este
//      script.
//   3. Asigna el CutsceneData asset en el campo cutsceneData.
//   4. En MainMenuManager, PlayNormal() ya guarda
//      KEY_LEVEL_INDEX en PlayerPrefs antes de cargar "Cutscene".
//
//  FLUJO COMPLETO:
//   Menu → (PlayNormal guarda índice 0) → Cutscene
//        → (animación) → Lvl1
//   Menu → (PlayNormal guarda índice 1) → Cutscene
//        → (animación) → Lvl2
//   (LevelManager al completar nivel guarda índice siguiente
//    y carga Cutscene de nuevo)
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CutsceneScreen : MonoBehaviour
{
    // ── PlayerPrefs key ────────────────────────────────────────
    /// MainMenuManager y LevelManager guardan aquí el índice
    /// del LevelEntry que debe mostrarse.
    public const string KEY_LEVEL_INDEX = "cutscene_level_index";

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════

    [Header("── Datos ────────────────────────────────────────")]
    [Tooltip("ScriptableObject con los sprites de cada nivel.")]
    [SerializeField] private CutsceneData cutsceneData;

    [Header("── Tiempos ─────────────────────────────────────")]
    [Tooltip("Segundos que tarda la animación de entrada.")]
    [SerializeField] private float slideInDuration  = 0.9f;

    [Tooltip("Segundos que la pantalla permanece estática.")]
    [SerializeField] private float holdDuration     = 1.8f;

    [Tooltip("Segundos que tarda el fade-out de salida.")]
    [SerializeField] private float slideOutDuration = 0.5f;

    [Tooltip("Segundos mínimos antes de que el skip esté habilitado.")]
    [SerializeField] private float minTimeBeforeSkip = 0.6f;

    [Tooltip("Permite saltar la cutscene pulsando cualquier tecla.")]
    [SerializeField] private bool allowSkip = true;

    [Header("── Jugador — Tamaño y Posición ──────────────────")]
    [Tooltip("Tamaño del sprite del jugador en píxeles (ancho × alto).")]
    [SerializeField] private Vector2 playerSize         = new Vector2(220f, 220f);

    [Tooltip("Posición final del jugador en pantalla (esquina superior izquierda del rect), " +
             "en píxeles desde la esquina superior izquierda de la pantalla. " +
             "X positivo = derecha, Y positivo = abajo. " +
             "Valor por defecto: pegado a la esquina inferior izquierda.")]
    [SerializeField] private Vector2 playerFinalPosition = new Vector2(30f, -1f);
    // Nota: Y = -1 es un centinela que indica «calcular automáticamente
    // como sh - playerSize.y - edgeMargin». Cambia a cualquier valor ≥ 0
    // para fijar una posición manual.

    [Tooltip("Margen inferior/izquierdo usado solo cuando playerFinalPosition.y = -1 " +
             "(posición automática en esquina inferior izquierda).")]
    [SerializeField] private float playerEdgeMargin = 30f;

    [Header("── Enemigos — Tamaño y Posición ─────────────────")]
    [Tooltip("Tamaño de cada sprite de enemigo en píxeles (ancho × alto).")]
    [SerializeField] private Vector2 enemySize           = new Vector2(180f, 180f);

    [Tooltip("Posición final del PRIMER enemigo en pantalla (esquina superior izquierda " +
             "del rect), en píxeles desde la esquina superior izquierda de la pantalla. " +
             "X negativo = calcular automáticamente desde el borde derecho. " +
             "Valor por defecto: esquina superior derecha automática.")]
    [SerializeField] private Vector2 enemyFinalPosition  = new Vector2(-1f, 30f);
    // Nota: X = -1 es un centinela que indica «calcular automáticamente
    // como sw - enemySize.x - enemyEdgeMargin». Cambia a cualquier valor ≥ 0
    // para fijar una posición manual.

    [Tooltip("Margen derecho/superior usado solo cuando enemyFinalPosition.x = -1 " +
             "(posición automática en esquina superior derecha).")]
    [SerializeField] private float enemyEdgeMargin = 30f;

    [Tooltip("Separación vertical entre enemigos si hay más de uno.")]
    [SerializeField] private float enemySpacing = 12f;

    [Header("── Logo VS — Tamaño ──────────────────────────────")]
    [Tooltip("Tamaño del logo VS en píxeles (ancho × alto). " +
             "Se centra automáticamente en pantalla.")]
    [SerializeField] private Vector2 vsSize = new Vector2(160f, 160f);

    [Header("── Rebote del logo VS ──────────────────────────")]
    [Tooltip("Factor de sobredimensionado al llegar al tamaño final (rebote). " +
             "1.0 = sin rebote. 1.2 = rebota un 20% más grande antes de volver.")]
    [SerializeField] private float vsBounceOvershoot = 1.18f;

    [Tooltip("Fracción del slideInDuration que dura la fase de rebote.")]
    [SerializeField, Range(0.1f, 0.5f)] private float vsBounceRatio = 0.25f;

    [Header("── Texto de skip ─────────────────────────────────")]
    [SerializeField] private string skipHintText  = "Pulsa cualquier tecla para continuar...";
    [SerializeField] private Color  skipHintColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("── Colores ──────────────────────────────────────")]
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color labelColor      = new Color(1f, 0.9f, 0.2f);

    // ══════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ══════════════════════════════════════════════════════════

    private CutsceneData.LevelEntry _entry;

    // Texturas convertidas desde Sprite para OnGUI
    private Texture2D   _playerTex;
    private Texture2D[] _enemyTexs;
    private Texture2D   _vsTex;
    private Texture2D   _bgTex;
    private Texture2D   _whiteTex;

    // Animación
    private enum Phase { SlideIn, Hold, SlideOut, Done }
    private Phase _phase       = Phase.SlideIn;
    private float _phaseTimer  = 0f;
    private float _globalAlpha = 1f;   // para fade-out

    // Posiciones interpoladas (en px, esquina sup-izq)
    private Rect _playerRect;
    private Rect _vsRect;
    // posición X de salida (fuera de pantalla)
    private float _playerOffscreenX;
    private float _enemyOffscreenX;

    // Posiciones finales (en pantalla)
    private Rect   _playerFinalRect;
    private Rect[] _enemyFinalRects;
    private Rect[] _enemyCurrentRects;
    private Rect   _vsFinalRect;

    private GUIStyle _labelStyle;
    private GUIStyle _skipStyle;
    private bool     _guiInitialized = false;

    private bool _skipped = false;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        // ── Lee índice y entry ────────────────────────────────
        int index = PlayerPrefs.GetInt(KEY_LEVEL_INDEX, 0);

        if (cutsceneData == null || cutsceneData.levelEntries == null
            || cutsceneData.levelEntries.Length == 0)
        {
            Debug.LogError("[CutsceneScreen] CutsceneData no asignado o vacío. " +
                           "Cargando escena 1 directamente.");
            SceneManager.LoadScene(1);
            return;
        }

        index  = Mathf.Clamp(index, 0, cutsceneData.levelEntries.Length - 1);
        _entry = cutsceneData.levelEntries[index];

        // ── Convierte sprites a texturas ──────────────────────
        _playerTex = SpriteToTexture(_entry.playerSprite);
        _vsTex     = SpriteToTexture(_entry.vsSprite);
        _bgTex     = SpriteToTexture(_entry.backgroundSprite);

        if (_entry.enemySprites != null && _entry.enemySprites.Length > 0)
        {
            _enemyTexs = new Texture2D[_entry.enemySprites.Length];
            for (int i = 0; i < _entry.enemySprites.Length; i++)
                _enemyTexs[i] = SpriteToTexture(_entry.enemySprites[i]);
        }
        else
        {
            _enemyTexs = new Texture2D[0];
        }

        // Textura blanca para dibujar rectángulos de color
        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();

        // ── Calcula posiciones ────────────────────────────────
        CalculateRects();

        // ── Inicia la corrutina principal ────────────────────
        StartCoroutine(RunCutscene());
    }

    // ══════════════════════════════════════════════════════════
    //  CÁLCULO DE POSICIONES
    // ══════════════════════════════════════════════════════════

    private void CalculateRects()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        // ── Jugador ───────────────────────────────────────────
        // X: usa playerFinalPosition.x directamente.
        // Y: si es -1 (centinela) calcula automáticamente pegado
        //    a la esquina inferior izquierda; si no, usa el valor manual.
        float pX = playerFinalPosition.x;
        float pY = (playerFinalPosition.y < 0f)
                   ? sh - playerSize.y - playerEdgeMargin
                   : playerFinalPosition.y;

        _playerFinalRect  = new Rect(pX, pY, playerSize.x, playerSize.y);
        _playerOffscreenX = -playerSize.x - 10f;

        // ── VS: siempre centrado en pantalla ──────────────────
        _vsFinalRect = new Rect(
            (sw - vsSize.x) * 0.5f,
            (sh - vsSize.y) * 0.5f,
            vsSize.x,
            vsSize.y
        );

        // ── Enemigos ──────────────────────────────────────────
        // X: si es -1 (centinela) calcula automáticamente desde
        //    el borde derecho; si no, usa el valor manual.
        // Y: posición del primer enemigo; los siguientes se apilan abajo.
        int count = _enemyTexs != null ? _enemyTexs.Length : 0;
        _enemyFinalRects   = new Rect[count];
        _enemyCurrentRects = new Rect[count];
        _enemyOffscreenX   = sw + 10f;

        float eX = (enemyFinalPosition.x < 0f)
                   ? sw - enemySize.x - enemyEdgeMargin
                   : enemyFinalPosition.x;
        float eY = enemyFinalPosition.y;

        for (int i = 0; i < count; i++)
        {
            _enemyFinalRects[i] = new Rect(
                eX,
                eY + i * (enemySize.y + enemySpacing),
                enemySize.x,
                enemySize.y
            );
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CORRUTINA PRINCIPAL
    // ══════════════════════════════════════════════════════════

    private IEnumerator RunCutscene()
    {
        // ── FASE 1: Slide In ──────────────────────────────────
        _phase      = Phase.SlideIn;
        _phaseTimer = 0f;

        while (_phaseTimer < slideInDuration)
        {
            if (!_skipped)
                _phaseTimer += Time.deltaTime;

            if (_skipped) { _phaseTimer = slideInDuration; }
            yield return null;
        }
        _phaseTimer = slideInDuration; // asegura t=1 al final

        // ── FASE 2: Hold ──────────────────────────────────────
        _phase      = Phase.Hold;
        _phaseTimer = 0f;
        float holdElapsed = 0f;

        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.deltaTime;

            if (allowSkip && holdElapsed >= minTimeBeforeSkip && Input.anyKeyDown)
            {
                _skipped = true;
                break;
            }
            yield return null;
        }

        // ── FASE 3: Fade Out ──────────────────────────────────
        _phase      = Phase.SlideOut;
        _phaseTimer = 0f;

        while (_phaseTimer < slideOutDuration)
        {
            _phaseTimer += Time.deltaTime;
            _globalAlpha = 1f - Mathf.Clamp01(_phaseTimer / slideOutDuration);
            yield return null;
        }

        // ── Carga escena destino ──────────────────────────────
        _phase = Phase.Done;
        SceneManager.LoadScene(_entry.targetScene);
    }

    // ══════════════════════════════════════════════════════════
    //  RENDERING (OnGUI)
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (_entry == null || _phase == Phase.Done) return;

        InitGUI();

        float sw = Screen.width;
        float sh = Screen.height;

        // Recalcula si cambió la resolución
        if (Mathf.Abs(sw - _playerFinalRect.x - playerSize.x - playerEdgeMargin) > 2f)
            CalculateRects();

        // ── t de interpolación ────────────────────────────────
        float t = (_phase == Phase.SlideIn)
            ? Mathf.Clamp01(_phaseTimer / slideInDuration)
            : 1f;

        float tEased = EaseOutCubic(t);

        // ── Fondo ─────────────────────────────────────────────
        Color prevColor = GUI.color;

        float alpha = _globalAlpha;
        GUI.color = new Color(backgroundColor.r, backgroundColor.g,
                              backgroundColor.b, backgroundColor.a * alpha);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);

        if (_bgTex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _bgTex, ScaleMode.ScaleAndCrop);
        }

        // ── Jugador ───────────────────────────────────────────
        float playerX = Mathf.Lerp(_playerOffscreenX, _playerFinalRect.x, tEased);
        Rect  pRect   = new Rect(playerX, _playerFinalRect.y,
                                 _playerFinalRect.width, _playerFinalRect.height);
        if (_playerTex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(pRect, _playerTex, ScaleMode.ScaleToFit, true);
        }
        else
        {
            // Fallback: rectángulo azul
            GUI.color = new Color(0.2f, 0.4f, 1f, 0.6f * alpha);
            GUI.DrawTexture(pRect, _whiteTex);
        }

        // ── Enemigos ──────────────────────────────────────────
        if (_enemyTexs != null)
        {
            for (int i = 0; i < _enemyTexs.Length && i < _enemyFinalRects.Length; i++)
            {
                float enemyX = Mathf.Lerp(_enemyOffscreenX,
                                          _enemyFinalRects[i].x, tEased);
                Rect eRect = new Rect(enemyX, _enemyFinalRects[i].y,
                                      _enemyFinalRects[i].width,
                                      _enemyFinalRects[i].height);
                if (_enemyTexs[i] != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.DrawTexture(eRect, _enemyTexs[i], ScaleMode.ScaleToFit, true);
                }
                else
                {
                    GUI.color = new Color(1f, 0.2f, 0.2f, 0.6f * alpha);
                    GUI.DrawTexture(eRect, _whiteTex);
                }
            }
        }

        // ── Logo VS con rebote ────────────────────────────────
        float vsScale = CalculateVSScale(t);
        float vsW     = _vsFinalRect.width  * vsScale;
        float vsH     = _vsFinalRect.height * vsScale;
        Rect  vsRect  = new Rect(
            _vsFinalRect.x + (_vsFinalRect.width  - vsW) * 0.5f,
            _vsFinalRect.y + (_vsFinalRect.height - vsH) * 0.5f,
            vsW, vsH
        );

        if (_vsTex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(vsRect, _vsTex, ScaleMode.ScaleToFit, true);
        }
        else
        {
            // Fallback: texto "VS"
            GUI.color = new Color(1f, 0.85f, 0.1f, alpha);
            GUI.DrawTexture(vsRect, _whiteTex);
            GUI.color = new Color(0.05f, 0.05f, 0.05f, alpha);
            _labelStyle.fontSize = Mathf.RoundToInt(vsRect.height * 0.55f);
            GUI.Label(vsRect, "VS", _labelStyle);
        }

        // ── Label de nivel ────────────────────────────────────
        if (!string.IsNullOrEmpty(_entry.levelLabel))
        {
            float lw = 300f;
            float lh = 36f;
            Rect labelRect = new Rect(
                (sw - lw) * 0.5f,
                _vsFinalRect.y + _vsFinalRect.height + 10f,
                lw, lh
            );
            GUI.color = new Color(labelColor.r, labelColor.g, labelColor.b,
                                  labelColor.a * alpha);
            GUI.Label(labelRect, _entry.levelLabel, _labelStyle);
        }

        // ── Hint de skip ──────────────────────────────────────
        if (allowSkip && _phase == Phase.Hold)
        {
            GUI.color = new Color(skipHintColor.r, skipHintColor.g,
                                  skipHintColor.b, skipHintColor.a * alpha);
            float hw = 400f;
            GUI.Label(new Rect((sw - hw) * 0.5f, sh - 36f, hw, 28f),
                      skipHintText, _skipStyle);
        }

        GUI.color = prevColor;
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS DE ANIMACIÓN
    // ══════════════════════════════════════════════════════════

    /// Curva ease-out cúbica: rápido al principio, frena al final.
    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    /// Calcula la escala del logo VS con rebote:
    ///  0→(1-bounceRatio): escala 0 → vsBounceOvershoot  (ease-out)
    ///  (1-bounceRatio)→1: escala vsBounceOvershoot → 1   (ease-in)
    private float CalculateVSScale(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        float splitPoint = 1f - vsBounceRatio;

        if (t <= splitPoint)
        {
            float localT = t / splitPoint;
            return EaseOutCubic(localT) * vsBounceOvershoot;
        }
        else
        {
            float localT = (t - splitPoint) / vsBounceRatio;
            return Mathf.Lerp(vsBounceOvershoot, 1f, EaseOutCubic(localT));
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CONVERSIÓN SPRITE → TEXTURE2D
    // ══════════════════════════════════════════════════════════

    /// Extrae la región del atlas que ocupa el sprite y la devuelve
    /// como una Texture2D independiente. Retorna null si el sprite
    /// es null o su textura no es legible.
    private Texture2D SpriteToTexture(Sprite sprite)
    {
        if (sprite == null) return null;

        Texture2D src = sprite.texture;

        // Si la textura es legible (Read/Write enabled), extrae la región
        try
        {
            Rect  rect = sprite.textureRect;
            Color[] pixels = src.GetPixels(
                (int)rect.x, (int)rect.y,
                (int)rect.width, (int)rect.height
            );

            Texture2D tex = new Texture2D((int)rect.width, (int)rect.height,
                                           TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        catch
        {
            // La textura no es legible: devuelve la textura original completa
            // (puede verse el atlas entero, pero es el fallback seguro)
            Debug.LogWarning($"[CutsceneScreen] El sprite '{sprite.name}' usa una textura " +
                             $"no legible. Activa 'Read/Write Enabled' en su import settings " +
                             $"para que se muestre correctamente.");
            return src;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  INICIALIZACIÓN DE ESTILOS
    // ══════════════════════════════════════════════════════════

    private void InitGUI()
    {
        if (_guiInitialized) return;
        _guiInitialized = true;

        _labelStyle           = new GUIStyle(GUI.skin.label);
        _labelStyle.fontSize  = 22;
        _labelStyle.fontStyle = FontStyle.Bold;
        _labelStyle.alignment = TextAnchor.MiddleCenter;
        _labelStyle.normal.textColor = labelColor;

        _skipStyle           = new GUIStyle(GUI.skin.label);
        _skipStyle.fontSize  = 13;
        _skipStyle.alignment = TextAnchor.MiddleCenter;
        _skipStyle.normal.textColor = skipHintColor;
    }
}
