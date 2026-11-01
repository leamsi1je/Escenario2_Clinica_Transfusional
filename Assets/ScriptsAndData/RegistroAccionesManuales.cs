using System.Collections.Generic;

/// <summary>
/// Contrato común para el panel y los detectores físicos. No decide el orden
/// de Ximena, ni da por ejecutada una acción al seleccionar una alternativa.
/// </summary>
public sealed class RegistroAccionesManuales
{
    public const string CerrarSangre = "suspender-sangre";
    public const string AbrirNaCl = "activar-nacl";
    public string Pendiente { get; private set; }
    private readonly HashSet<string> completadas = new HashSet<string>();

    public bool Solicitar(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || completadas.Contains(id)) return false;
        if (Pendiente != null && Pendiente != id) return false;
        Pendiente = id;
        return true;
    }

    public bool Confirmar(string id)
    {
        if (id == null || Pendiente != id) return false;
        completadas.Add(id);
        Pendiente = null;
        return true;
    }

    public void Reiniciar()
    {
        Pendiente = null;
        completadas.Clear();
    }
}
