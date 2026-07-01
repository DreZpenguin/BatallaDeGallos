// ============================================================
//  CutsceneData.cs
//
//  ScriptableObject que almacena los datos visuales de la
//  cutscene de introducción para cada nivel del modo Normal.
//
//  SETUP EN UNITY:
//   1. Click derecho en Project → BatallaGallos → CutsceneData
//   2. Crea un asset (ej. "CutsceneLevels").
//   3. Rellena el array levelEntries con un entry por nivel
//      (5 entries para Lvl1–Lvl5).
//   4. Asigna el asset en el campo "cutsceneData" de
//      CutsceneScreen en la escena Cutscene.
// ============================================================
using UnityEngine;

[CreateAssetMenu(menuName = "BatallaGallos/CutsceneData", fileName = "CutsceneData")]
public class CutsceneData : ScriptableObject
{
    [System.Serializable]
    public class LevelEntry
    {
        [Header("Identificación")]
        [Tooltip("Nombre de la escena de juego que se cargará al terminar esta cutscene. " +
                 "Debe coincidir EXACTAMENTE con el nombre en Build Settings.")]
        public string targetScene = "Lvl1";

        [Header("Sprites")]
        [Tooltip("Sprite del jugador. Se desplaza de izquierda a derecha.")]
        public Sprite playerSprite;

        [Tooltip("Sprites de los enemigos (mínimo 1, máximo 4). " +
                 "Se despliegan en la esquina superior derecha.")]
        public Sprite[] enemySprites = new Sprite[1];

        [Tooltip("Índice del slot del Inspector de CutsceneScreen que define el " +
                 "tamaño y posición del enemigo. 0 = Enemigo 1, 1 = Enemigo 2, etc. " +
                 "Lvl1→0, Lvl2→1, Lvl3→2, Lvl4→3, Lvl5→4")]
        public int enemyInspectorSlot = 0;

        [Tooltip("Sprite del logo VS. Aparece en el centro y hace zoom-in.")]
        public Sprite vsSprite;

        [Tooltip("Sprite de fondo. Cubre toda la pantalla. " +
                 "Deja vacío para usar fondo negro sólido.")]
        public Sprite backgroundSprite;

        [Header("Título opcional")]
        [Tooltip("Texto que aparece bajo el logo VS (ej. 'NIVEL 1'). " +
                 "Deja vacío para no mostrar nada.")]
        public string levelLabel = "";
    }

    [Header("Entries por nivel (índice 0 = Lvl1, 1 = Lvl2, etc.)")]
    [Tooltip("Un LevelEntry por cada nivel del modo Normal.")]
    public LevelEntry[] levelEntries;
}
