// ============================================================
//  PowerUpUICanvas.cs  — NUEVO (reemplaza PowerUpUI.cs)
//  Maneja el panel de selección de powerup usando Canvas de Unity.
//
//  SETUP (ver guía completa al final del archivo):
//   1. Crea un Canvas en la escena (UI > Canvas)
//   2. Asigna este script al Canvas GameObject
//   3. Arrastra los elementos del Canvas a los campos del Inspector
//
//  ⚠️ PowerUpUI.cs DEBE ser ELIMINADO del proyecto.
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerUpUICanvas : MonoBehaviour
{
    // ── Estructura de datos ────────────────────────────────────
    [Serializable]
    public struct PowerUpOption
    {
        public PowerUpType id;
        public string      title;
        public string      description;
        public string      hexColor;   // ej: "#FF4D3A"
    }

    // ── Referencias del Canvas (asignar en Inspector) ──────────
    [Header("Panel raíz (se activa/desactiva)")]
    [Tooltip("El GameObject raíz de todo el panel de powerup (Panel_PowerUp)")]
    [SerializeField] private GameObject rootPanel;

    [Header("Tarjetas — deben ser exactamente 5 en orden")]
    [Tooltip("Array de 5 GameObjects, uno por cada tarjeta de powerup")]
    [SerializeField] private GameObject[] cards;   // 5 elementos

    [Header("Textos de cada tarjeta (mismo orden que cards[])")]
    [SerializeField] private TextMeshProUGUI[] cardTitles;        // 5
    [SerializeField] private TextMeshProUGUI[] cardDescriptions;  // 5

    [Header("Imágenes de icono de cada tarjeta (mismo orden)")]
    [Tooltip("Image component del icono circular/cuadrado de cada tarjeta")]
    [SerializeField] private Image[] cardIcons;   // 5

    [Header("Botones de selección (mismo orden)")]
    [SerializeField] private Button[] cardButtons;  // 5

    // ── Estado interno ─────────────────────────────────────────
    private PowerUpOption[]     _options;
    private Action<PowerUpType> _onSelected;

    // ── Unity Lifecycle ────────────────────────────────────────
    private void Awake()
    {
        // Oculta el panel al inicio
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    // ── API Pública ────────────────────────────────────────────

    /// Muestra el panel con las opciones dadas.
    /// callback se invoca con el PowerUpType elegido.
    public void Show(PowerUpOption[] options, Action<PowerUpType> callback)
    {
        _options    = options;
        _onSelected = callback;

        // Configura cada tarjeta
        for (int i = 0; i < cards.Length; i++)
        {
            bool hasOption = i < options.Length;
            cards[i].SetActive(hasOption);

            if (!hasOption) continue;

            // Textos
            if (i < cardTitles.Length && cardTitles[i] != null)
                cardTitles[i].text = options[i].title;

            if (i < cardDescriptions.Length && cardDescriptions[i] != null)
                cardDescriptions[i].text = options[i].description;

            // Color del icono
            if (i < cardIcons.Length && cardIcons[i] != null)
            {
                if (ColorUtility.TryParseHtmlString(options[i].hexColor, out Color col))
                    cardIcons[i].color = col;
            }

            // Botón — capturamos i en una variable local para el closure
            int index = i;
            if (i < cardButtons.Length && cardButtons[i] != null)
            {
                cardButtons[i].onClick.RemoveAllListeners();
                cardButtons[i].onClick.AddListener(() => OnCardSelected(index));
            }
        }

        rootPanel.SetActive(true);
    }

    public void Hide()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    // ── Callback de tarjeta ────────────────────────────────────
    private void OnCardSelected(int index)
    {
        if (_options == null || index >= _options.Length) return;

        PowerUpType chosen = _options[index].id;
        Hide();
        _onSelected?.Invoke(chosen);
    }
}

/*
═══════════════════════════════════════════════════════════════
  GUÍA DE SETUP DEL CANVAS EN UNITY
  (Lee esto antes de configurar la escena)
═══════════════════════════════════════════════════════════════

PASO 0 — ELIMINAR PowerUpUI.cs
  · Borra el archivo PowerUpUI.cs del proyecto.
  · Quita cualquier referencia a PowerUpUI en el Inspector.

PASO 1 — Crear el Canvas
  · Hierarchy → Click derecho → UI → Canvas
  · Nómbralo: "Canvas_PowerUp"
  · Canvas component:
      Render Mode   : Screen Space - Overlay
      UI Scale Mode : Scale With Screen Size
      Reference     : 1920 × 1080
  · Añade "Canvas Scaler" y "Graphic Raycaster" (suelen estar por defecto).

PASO 2 — Crear el Panel Raíz
  · Hijo del Canvas: UI → Panel
  · Nómbralo: "Panel_PowerUp"
  · Rect Transform: Stretch horizontal y vertical (ancla a toda la pantalla)
  · Image → Color: (0, 0, 0, 0.75)  ← overlay oscuro semitransparente
  · IMPORTANTE: empieza DESACTIVADO (desactiva el GameObject en el Inspector)

PASO 3 — Panel central (contenedor de tarjetas)
  · Hijo de Panel_PowerUp: UI → Panel
  · Nómbralo: "Panel_Center"
  · Rect Transform:
      Width: 1400   Height: 600
      Pivot/Anchor: Center (0.5, 0.5)
      Pos X: 0     Pos Y: 0
  · Image → Color: (20, 20, 26, 245)  ← fondo oscuro casi opaco

PASO 4 — Título
  · Hijo de Panel_Center: UI → Text - TextMeshPro
  · Nómbralo: "Text_Title"
  · Texto: "ELIGE TU PODER"
  · Fuente: cualquier fuente bold que tengas importada
  · Font Size: 52   Alignment: Center   Color: #FFD93D

PASO 5 — Horizontal Layout Group (para las tarjetas)
  · Hijo de Panel_Center: UI → Empty Object → nómbralo "Cards_Container"
  · Añade componente: Horizontal Layout Group
      Child Alignment : Middle Center
      Spacing         : 24
      Child Force Expand Width/Height: NO
  · Rect Transform: ajusta para que ocupe la parte inferior del Panel_Center
    Ej.: Top: 80, Bottom: 0, Left: 24, Right: 24

PASO 6 — Crear UNA tarjeta (luego la duplicas × 5)
  · Hijo de Cards_Container: UI → Panel
  · Nómbralo: "Card_0"
  · Rect Transform: Width 230, Height 460
  · Image → Color: (33, 33, 43, 255)
  · Añade componente: Vertical Layout Group
      Padding: 16 en todos los lados
      Spacing: 12
      Child Alignment: Upper Center
      Child Force Expand: NO

  Hijos de Card_0:
  ┌─ "Img_Icon" (UI > Image)
  │   Width: 72   Height: 72
  │   (Color se asigna por script)
  │
  ├─ "Text_Title" (UI > Text - TextMeshPro)
  │   Font Size: 22   Bold   Alignment: Center   Color: White
  │
  ├─ "Text_Desc" (UI > Text - TextMeshPro)
  │   Font Size: 13   Alignment: Center   Color: #BBBBBB
  │   Enable Word Wrap: YES
  │
  └─ "Btn_Select" (UI > Button - TextMeshPro)
      Height: 46
      Image Color: (38, 38, 38, 255)
      Text del botón: "ELEGIR"   Font Size: 16   Bold   Color: White

PASO 7 — Duplicar la tarjeta
  · Duplica Card_0 cuatro veces → Card_1, Card_2, Card_3, Card_4
  · Deben quedar dentro de Cards_Container en orden

PASO 8 — Añadir PowerUpUICanvas al Canvas
  · Selecciona el Canvas_PowerUp
  · Add Component → PowerUpUICanvas
  · Asigna en el Inspector:
      Root Panel      → Panel_PowerUp
      Cards [0..4]    → Card_0 … Card_4
      Card Titles     → Text_Title de cada Card (0..4)
      Card Descriptions → Text_Desc de cada Card (0..4)
      Card Icons      → Img_Icon de cada Card (0..4)
      Card Buttons    → Btn_Select de cada Card (0..4)

PASO 9 — Conectar con PowerUpManager
  · Selecciona el GameObject del jugador
  · En PowerUpManager (Inspector):
      Power Up UI Canvas → arrastra el Canvas_PowerUp

PASO 10 — EventSystem
  · Asegúrate de que existe un EventSystem en la escena
    (se crea automáticamente al crear el primer Canvas).

═══════════════════════════════════════════════════════════════
  GUÍA DE SETUP DE PlayerData (Sistema de Guardado)
═══════════════════════════════════════════════════════════════

PASO 1 — Crear el GameObject persistente
  · Hierarchy → Create Empty
  · Nómbralo: "PlayerData"
  · Add Component → PlayerData
  · ¡NO lo pongas como hijo de ningún otro objeto!

PASO 2 — Múltiples escenas
  · El singleton DontDestroyOnLoad hace que sobreviva entre escenas
    automáticamente. No necesitas nada más.
  · Si quieres empezar con datos guardados en el disco desde la escena 1,
    asegúrate de que PlayerData exista en la primera escena.

PASO 3 — Resetear progreso
  · Llama PlayerData.Instance.ResetAll() desde un botón de "Nueva Partida"
  · O directamente desde el Inspector con clic derecho en el componente.

═══════════════════════════════════════════════════════════════
  GUÍA DE SETUP DE ShootingController + BulletController
═══════════════════════════════════════════════════════════════

CREAR EL PREFAB DE BALA:
  1. Hierarchy → Create Empty → "Bullet"
  2. Add Component: Rigidbody2D
       Gravity Scale: 0
       Collision Detection: Continuous
  3. Add Component: CircleCollider2D
       Is Trigger: YES   Radius: 0.15
  4. (Opcional) Add Component: SpriteRenderer con tu sprite de bala
  5. Add Component: BulletController
  6. Arrastra el GameObject a la carpeta Prefabs → ya tienes el prefab
  7. Elimina el GameObject de la escena

CONFIGURAR ShootingController en el jugador:
  · Selecciona el jugador
  · Add Component: ShootingController
  · Bullet Prefab    → el prefab que creaste
  · Fire Point       → (opcional) un Transform hijo del jugador, ej. "FirePoint"
                        posicionado en la punta del arma
  · Shoot Key        → KeyCode.Mouse1 (clic derecho) por defecto
  · Base Damage      → 10
  · Base Speed       → 12
  · Base Lifetime    → 3
  · Shoot Cooldown   → 0.5

═══════════════════════════════════════════════════════════════
*/
