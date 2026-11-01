/// <summary>
/// Dominio de infusión del Escenario 2. No conoce UI, monitor, fármacos ni Photon.
/// Validar una decisión solo autoriza el gesto; no modifica el flujo.
/// </summary>
public sealed class EstadoInfusion
{
    public enum Fase { SinIniciar, SangreActiva, SangreSuspendida, NaClActivo }

    public Fase Actual { get; private set; }
    public bool ReaccionNotificada { get; private set; }
    public enum Accion { Ninguna, CerrarSangre, AbrirNaCl }
    public Accion Pendiente { get; private set; }
    public bool SangreFluyendo => Actual == Fase.SangreActiva;
    public bool NaClFluyendo => Actual == Fase.NaClActivo;

    // Única operación que permite reiniciar sangre después de suspenderla.
    public void Reiniciar()
    {
        Actual = Fase.SangreActiva;
        ReaccionNotificada = false;
        Pendiente = Accion.Ninguna;
    }

    // Una reacción no equivale a una orden de suspensión. Tampoco reinicia
    // sangre si el aviso llega tarde o duplicado después de suspenderla.
    public void NotificarReaccion() => ReaccionNotificada = true;

    public bool SuspenderSangreValidada()
    {
        if (Actual == Fase.SangreSuspendida) return true;
        if (Actual != Fase.SangreActiva) return false;
        Pendiente = Accion.CerrarSangre;
        return true;
    }

    public bool ActivarNaClValidado()
    {
        if (Actual == Fase.NaClActivo) return true;
        if (Actual != Fase.SangreSuspendida) return false;
        Pendiente = Accion.AbrirNaCl;
        return true;
    }

    public bool ConfirmarCierreManual()
    {
        if (Actual != Fase.SangreActiva || Pendiente != Accion.CerrarSangre) return false;
        Actual = Fase.SangreSuspendida;
        Pendiente = Accion.Ninguna;
        return true;
    }

    public bool ConfirmarAperturaManual()
    {
        if (Actual != Fase.SangreSuspendida || Pendiente != Accion.AbrirNaCl) return false;
        Actual = Fase.NaClActivo;
        Pendiente = Accion.Ninguna;
        return true;
    }

    public void DetenerTodo()
    {
        Actual = Fase.SinIniciar;
        Pendiente = Accion.Ninguna;
    }
}
