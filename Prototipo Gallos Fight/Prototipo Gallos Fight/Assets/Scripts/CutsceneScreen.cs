// ============================================================
//  CutsceneScreen.cs  — v4
//
//  CAMBIOS respecto a v3:
//   · Eliminada la conversión Sprite→Texture2D completamente.
//     Ahora se guardan los Sprite directamente y se dibujan
//     con GUI.DrawTextureWithTexCoords, que calcula el UV rect
//     del sprite dentro del atlas y lo dibuja sin recuadro negro
//     ni necesidad de Read/Write Enabled.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CutsceneScreen : MonoBehaviour
{
    public const string KEY_LEVEL_INDEX = "cutscene_level_index";

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════

    [Header("── Datos ────────────────────────────────────────")]
    [SerializeField] private CutsceneData cutsceneData;

    [Header("── Tiempos ─────────────────────────────────────")]
    [SerializeField] private float slideInDuration   = 0.9f;
    [SerializeField] private float holdDuration      = 1.8f;
    [SerializeField] private float slideOutDuration  = 0.5f;
    [SerializeField] private float minTimeBeforeSkip = 0.6f;
    [SerializeField] private bool  allowSkip         = true;

    [Header("── Jugador ─────────────────────────────────────")]
    [Tooltip("Tamaño en píxeles del sprite del jugador.")]
    [SerializeField] private Vector2 playerSize          = new Vector2(220f, 220f);
    [Tooltip("Posición final (px desde esquina sup-izq). Y = -1 → auto: borde inferior izq.")]
    [SerializeField] private Vector2 playerFinalPosition = new Vector2(30f, -1f);
    [SerializeField] private float   playerEdgeMargin    = 30f;

    [Header("── Enemigo 1 ────────────────────────────────────")]
    [SerializeField] private Vector2 enemy0Size          = new Vector2(180f, 180f);
    [Tooltip("Posición final. X = -1 → auto: borde derecho.")]
    [SerializeField] private Vector2 enemy0FinalPosition = new Vector2(-1f, 30f);
    [SerializeField] private float   enemy0EdgeMargin    = 30f;

    [Header("── Enemigo 2 ────────────────────────────────────")]
    [SerializeField] private Vector2 enemy1Size          = new Vector2(180f, 180f);
    [SerializeField] private Vector2 enemy1FinalPosition = new Vector2(-1f, 220f);
    [SerializeField] private float   enemy1EdgeMargin    = 30f;

    [Header("── Enemigo 3 ────────────────────────────────────")]
    [SerializeField] private Vector2 enemy2Size          = new Vector2(180f, 180f);
    [SerializeField] private Vector2 enemy2FinalPosition = new Vector2(-1f, 410f);
    [SerializeField] private float   enemy2EdgeMargin    = 30f;

    [Header("── Enemigo 4 ────────────────────────────────────")]
    [SerializeField] private Vector2 enemy3Size          = new Vector2(180f, 180f);
    [SerializeField] private Vector2 enemy3FinalPosition = new Vector2(-1f, 30f);
    [SerializeField] private float   enemy3EdgeMargin    = 30f;

    [Header("── Enemigo 5 ────────────────────────────────────")]
    [SerializeField] private Vector2 enemy4Size          = new Vector2(180f, 180f);
    [SerializeField] private Vector2 enemy4FinalPosition = new Vector2(-1f, 220f);
    [SerializeField] private float   enemy4EdgeMargin    = 30f;

    [Header("── Logo VS ──────────────────────────────────────")]
    [SerializeField] private Vector2 vsSize            = new Vector2(160f, 160f);
    [SerializeField] private float   vsBounceOvershoot = 1.18f;
    [SerializeField, Range(0.1f, 0.5f)] private float vsBounceRatio = 0.25f;

    [Header("── Texto de skip ───────────────────────────────")]
    [SerializeField] private string skipHintText  = "Pulsa cualquier tecla para continuar...";
    [SerializeField] private Color  skipHintColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("── Colores ──────────────────────────────────────")]
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color labelColor      = new Color(1f, 0.9f, 0.2f);

    // ══════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ══════════════════════════════════════════════════════════

    private CutsceneData.LevelEntry _entry;

    // Sprites directos (sin conversión)
    private Sprite   _playerSprite;
    private Sprite[] _enemySprites;
    private Sprite   _vsSprite;
    private Sprite   _bgSprite;

    private Texture2D _whiteTex;

    private enum Phase { SlideIn, Hold, SlideOut, Done }
    private Phase _phase       = Phase.SlideIn;
    private float _phaseTimer  = 0f;
    private float _globalAlpha = 1f;

    private float  _playerOffscreenX;
    private float  _enemyOffscreenX;
    private Rect   _playerFinalRect;
    private Rect   _vsFinalRect;
    private Rect[] _enemyFinalRects;

    private GUIStyle _labelStyle;
    private GUIStyle _skipStyle;
    private bool     _guiInitialized = false;
    private bool     _skipped        = false;

    private Vector2[] _enemySizes;
    private Vector2[] _enemyPositions;
    private float[]   _enemyMargins;

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        _enemySizes     = new Vector2[] { enemy0Size, enemy1Size, enemy2Size,
                                          enemy3Size, enemy4Size };
        _enemyPositions = new Vector2[] { enemy0FinalPosition, enemy1FinalPosition,
                                          enemy2FinalPosition, enemy3FinalPosition,
                                          enemy4FinalPosition };
        _enemyMargins   = new float[]   { enemy0EdgeMargin, enemy1EdgeMargin,
                                          enemy2EdgeMargin, enemy3EdgeMargin,
                                          enemy4EdgeMargin };

        int index = PlayerPrefs.GetInt(KEY_LEVEL_INDEX, 0);

        if (cutsceneData == null || cutsceneData.levelEntries == null
            || cutsceneData.levelEntries.Length == 0)
        {
            Debug.LogError("[CutsceneScreen] CutsceneData no asignado o vacío.");
            SceneManager.LoadScene(1);
            return;
        }

        index  = Mathf.Clamp(index, 0, cutsceneData.levelEntries.Length - 1);
        _entry = cutsceneData.levelEntries[index];

        // Guarda los sprites directamente — sin conversión
        _playerSprite = _entry.playerSprite;
        _vsSprite     = _entry.vsSprite;
        _bgSprite     = _entry.backgroundSprite;

        int enemyCount = (_entry.enemySprites != null) ? _entry.enemySprites.Length : 0;
        _enemySprites  = new Sprite[enemyCount];
        for (int i = 0; i < enemyCount; i++)
            _enemySprites[i] = _entry.enemySprites[i];

        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();

        CalculateRects();
        StartCoroutine(RunCutscene());
    }

    // ══════════════════════════════════════════════════════════
    //  POSICIONES
    // ══════════════════════════════════════════════════════════

    private void CalculateRects()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        float pX = playerFinalPosition.x;
        float pY = (playerFinalPosition.y < 0f)
                   ? sh - playerSize.y - playerEdgeMargin
                   : playerFinalPosition.y;
        _playerFinalRect  = new Rect(pX, pY, playerSize.x, playerSize.y);
        _playerOffscreenX = -playerSize.x - 10f;

        _vsFinalRect = new Rect(
            (sw - vsSize.x) * 0.5f,
            (sh - vsSize.y) * 0.5f,
            vsSize.x, vsSize.y
        );

        int count = (_enemySprites != null) ? _enemySprites.Length : 0;
        _enemyFinalRects = new Rect[count];
        _enemyOffscreenX = sw + 10f;

        for (int i = 0; i < count; i++)
        {
            int     slot   = Mathf.Min(i, _enemySizes.Length - 1);
            Vector2 sz     = _enemySizes[slot];
            Vector2 pos    = _enemyPositions[slot];
            float   margin = _enemyMargins[slot];

            float eX = (pos.x < 0f) ? sw - sz.x - margin : pos.x;
            _enemyFinalRects[i] = new Rect(eX, pos.y, sz.x, sz.y);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CORRUTINA
    // ══════════════════════════════════════════════════════════

    private IEnumerator RunCutscene()
    {
        _phase      = Phase.SlideIn;
        _phaseTimer = 0f;

        while (_phaseTimer < slideInDuration)
        {
            if (!_skipped) _phaseTimer += Time.deltaTime;
            else           _phaseTimer  = slideInDuration;
            yield return null;
        }
        _phaseTimer = slideInDuration;

        _phase = Phase.Hold;
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.deltaTime;
            if (allowSkip && holdElapsed >= minTimeBeforeSkip && Input.anyKeyDown)
            { _skipped = true; break; }
            yield return null;
        }

        _phase      = Phase.SlideOut;
        _phaseTimer = 0f;
        while (_phaseTimer < slideOutDuration)
        {
            _phaseTimer  += Time.deltaTime;
            _globalAlpha  = 1f - Mathf.Clamp01(_phaseTimer / slideOutDuration);
            yield return null;
        }

        _phase = Phase.Done;
        SceneManager.LoadScene(_entry.targetScene);
    }

    // ══════════════════════════════════════════════════════════
    //  RENDERING
    // ══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (_entry == null || _phase == Phase.Done) return;

        InitGUI();

        float sw = Screen.width;
        float sh = Screen.height;

        // Recalcula rects si cambió resolución
        if (_playerFinalRect.width <= 0f) CalculateRects();

        float t      = (_phase == Phase.SlideIn)
                       ? Mathf.Clamp01(_phaseTimer / slideInDuration)
                       : 1f;
        float tEased = EaseOutCubic(t);
        float alpha  = _globalAlpha;

        Color prev = GUI.color;

        // Fondo
        GUI.color = new Color(backgroundColor.r, backgroundColor.g,
                              backgroundColor.b, backgroundColor.a * alpha);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);

        if (_bgSprite != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            DrawSprite(_bgSprite, new Rect(0, 0, sw, sh));
        }

        // Jugador
        float playerX = Mathf.Lerp(_playerOffscreenX, _playerFinalRect.x, tEased);
        Rect  pRect   = new Rect(playerX, _playerFinalRect.y,
                                 _playerFinalRect.width, _playerFinalRect.height);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        if (_playerSprite != null)
            DrawSprite(_playerSprite, pRect);
        else
        {
            GUI.color = new Color(0.2f, 0.4f, 1f, 0.6f * alpha);
            GUI.DrawTexture(pRect, _whiteTex);
        }

        // Enemigos
        if (_enemySprites != null && _enemyFinalRects != null)
        {
            for (int i = 0; i < _enemySprites.Length && i < _enemyFinalRects.Length; i++)
            {
                float eX    = Mathf.Lerp(_enemyOffscreenX, _enemyFinalRects[i].x, tEased);
                Rect  eRect = new Rect(eX, _enemyFinalRects[i].y,
                                       _enemyFinalRects[i].width, _enemyFinalRects[i].height);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                if (_enemySprites[i] != null)
                    DrawSprite(_enemySprites[i], eRect);
                else
                {
                    GUI.color = new Color(1f, 0.2f, 0.2f, 0.6f * alpha);
                    GUI.DrawTexture(eRect, _whiteTex);
                }
            }
        }

        // VS
        float vsScale = CalculateVSScale(t);
        float vsW     = _vsFinalRect.width  * vsScale;
        float vsH     = _vsFinalRect.height * vsScale;
        Rect  vsRect  = new Rect(
            _vsFinalRect.x + (_vsFinalRect.width  - vsW) * 0.5f,
            _vsFinalRect.y + (_vsFinalRect.height - vsH) * 0.5f,
            vsW, vsH
        );

        GUI.color = new Color(1f, 1f, 1f, alpha);
        if (_vsSprite != null)
            DrawSprite(_vsSprite, vsRect);
        else
        {
            GUI.color = new Color(1f, 0.85f, 0.1f, alpha);
            GUI.DrawTexture(vsRect, _whiteTex);
            GUI.color = new Color(0.05f, 0.05f, 0.05f, alpha);
            _labelStyle.fontSize = Mathf.RoundToInt(vsRect.height * 0.55f);
            GUI.Label(vsRect, "VS", _labelStyle);
        }

        // Label de nivel
        if (!string.IsNullOrEmpty(_entry.levelLabel))
        {
            Rect labelRect = new Rect((sw - 300f) * 0.5f,
                                      _vsFinalRect.y + _vsFinalRect.height + 10f,
                                      300f, 36f);
            GUI.color = new Color(labelColor.r, labelColor.g, labelColor.b,
                                  labelColor.a * alpha);
            GUI.Label(labelRect, _entry.levelLabel, _labelStyle);
        }

        // Skip hint
        if (allowSkip && _phase == Phase.Hold)
        {
            GUI.color = new Color(skipHintColor.r, skipHintColor.g,
                                  skipHintColor.b, skipHintColor.a * alpha);
            GUI.Label(new Rect((sw - 400f) * 0.5f, sh - 36f, 400f, 28f),
                      skipHintText, _skipStyle);
        }

        GUI.color = prev;
    }

    // ── Dibuja un sprite respetando su UV rect en el atlas ────

    private void DrawSprite(Sprite sprite, Rect screenRect)
    {
        if (sprite == null) return;

        Texture2D tex     = sprite.texture;
        Rect      texRect = sprite.textureRect;

        // UV normalizado dentro del atlas
        Rect uvRect = new Rect(
            texRect.x      / tex.width,
            texRect.y      / tex.height,
            texRect.width  / tex.width,
            texRect.height / tex.height
        );

        GUI.DrawTextureWithTexCoords(screenRect, tex, uvRect, true);
    }

    // ══════════════════════════════════════════════════════════
    //  ANIMACIÓN
    // ══════════════════════════════════════════════════════════

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float CalculateVSScale(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        float split = 1f - vsBounceRatio;
        if (t <= split)
            return EaseOutCubic(t / split) * vsBounceOvershoot;
        else
            return Mathf.Lerp(vsBounceOvershoot, 1f,
                              EaseOutCubic((t - split) / vsBounceRatio));
    }

    // ══════════════════════════════════════════════════════════
    //  GUI STYLES
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
