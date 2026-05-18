using UnityEngine;

// ============================================================
//  JoystickDiagnostic.cs
//  Agrega este script a cualquier GameObject vacio en la escena.
//  Muestra en pantalla el valor de TODOS los ejes del joystick
//  en tiempo real. Mueve el stick derecho horizontalmente y
//  mira cual eje cambia. Ese es tu eje X del stick derecho.
//  Elimina este script una vez identificados los ejes correctos.
// ============================================================

public class JoystickDiagnostic : MonoBehaviour
{
    private void OnGUI()
    {
        GUI.Box(new Rect(8, 8, 320, 420), "");
        
        string info = "=== JOYSTICK DIAGNOSTIC ===\n";
        info += "Mueve el stick derecho y observa\n";
        info += "cual eje cambia de valor.\n\n";

        // Lee los 20 ejes posibles directamente
        for (int i = 1; i <= 20; i++)
        {
            float val = 0f;
            try { val = Input.GetAxisRaw("joystick axis " + i); }
            catch { val = -99f; }

            // Resalta en mayuscula si el valor supera 0.1
            string marker = Mathf.Abs(val) > 0.1f ? "  <<<" : "";
            info += $"joystick axis {i,2}: {val:F3}{marker}\n";
        }

        // Tambien muestra los ejes nombrados del Input Manager
        info += "\n--- Input Manager ---\n";
        info += $"Horizontal:   {SafeAxis("Horizontal"):F3}\n";
        info += $"Vertical:     {SafeAxis("Vertical"):F3}\n";
        info += $"RightStickX:  {SafeAxis("RightStickX"):F3}\n";
        info += $"RightStickY:  {SafeAxis("RightStickY"):F3}\n";
        info += $"RT:           {SafeAxis("RT"):F3}\n";
        info += $"DPadX:        {SafeAxis("DPadX"):F3}\n";

        GUI.Label(new Rect(12, 12, 312, 412), info);
    }

    private float SafeAxis(string name)
    {
        try   { return Input.GetAxis(name); }
        catch { return -99f; }
    }
}
