// ============================================================
//  InfiniteData.cs
//
//  Singleton persistente entre escenas para el modo infinito.
//  Guarda:
//   · Número de oleada actual
//   · HP actual del jugador (para no regenerarse entre oleadas)
//   · Si el modo infinito está activo (para que HealthSystem
//     y otros sistemas sepan que no deben resetear)
//
//  Se destruye a sí mismo cuando el jugador muere o vuelve
//  al menú (llamando a ResetInfiniteRun()).
// ============================================================
using UnityEngine;

public class InfiniteData : MonoBehaviour
{
    public static InfiniteData Instance { get; private set; }

    // ── Estado de la run ───────────────────────────────────────
    [Header("Estado actual (solo lectura)")]
    [SerializeField] private int   _wave           = 0;
    [SerializeField] private float _playerHP       = -1f;  // -1 = sin dato guardado aún
    [SerializeField] private bool  _isInfiniteMode = false;

    public int   Wave          => _wave;
    public float PlayerHP      => _playerHP;
    public bool  IsInfiniteMode => _isInfiniteMode;

    // ── Constantes de PlayerPrefs ──────────────────────────────
    private const string KEY_WAVE      = "inf_wave";
    private const string KEY_HP        = "inf_hp";
    private const string KEY_ACTIVE    = "inf_active";
    private const string KEY_BESTSCORE = "inf_best";

    public int BestWave => PlayerPrefs.GetInt(KEY_BESTSCORE, 0);

    // ── Lifecycle ──────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── API Pública ────────────────────────────────────────────

    /// Inicia una nueva run infinita desde oleada 1.
    /// Llámalo desde el menú al presionar "Modo Infinito".
    public void StartInfiniteRun()
    {
        _wave           = 1;
        _playerHP       = -1f;   // HealthSystem usará su HP inicial
        _isInfiniteMode = true;
        Save();
        Debug.Log("[InfiniteData] Run iniciada. Oleada 1.");
    }

    /// Avanza a la siguiente oleada y guarda el HP actual del jugador.
    public void AdvanceWave(float currentPlayerHP)
    {
        _wave++;
        _playerHP = currentPlayerHP;
        Save();

        // Actualiza mejor marca
        if (_wave > BestWave)
            PlayerPrefs.SetInt(KEY_BESTSCORE, _wave);

        Debug.Log($"[InfiniteData] Oleada {_wave}. HP guardado: {_playerHP:F0}");
    }

    /// Termina la run (jugador muerto o volvió al menú).
    public void ResetInfiniteRun()
    {
        _wave           = 0;
        _playerHP       = -1f;
        _isInfiniteMode = false;
        Save();
        Debug.Log("[InfiniteData] Run terminada.");
    }

    private void Save()
    {
        PlayerPrefs.SetInt  (KEY_WAVE,   _wave);
        PlayerPrefs.SetFloat(KEY_HP,     _playerHP);
        PlayerPrefs.SetInt  (KEY_ACTIVE, _isInfiniteMode ? 1 : 0);
        PlayerPrefs.Save();
    }
}
