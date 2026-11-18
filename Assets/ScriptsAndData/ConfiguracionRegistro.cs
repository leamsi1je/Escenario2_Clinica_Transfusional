using UnityEngine;

/// <summary>
/// Reduce el peso de los mensajes en el Console: los mensajes normales y advertencias se guardan sin
/// el detalle de llamadas (stack trace). Los errores lo conservan.
/// Con el detalle completo, el Console de Unity 2022.3 se congelaba y terminaba en un cierre
/// ("GfxDevice::UpdateBufferRanges: range out of bounds").
/// </summary>
public static class ConfiguracionRegistro
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Configurar()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
    }
}
