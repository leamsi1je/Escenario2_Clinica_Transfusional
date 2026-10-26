using UnityEngine;
using Oculus.Voice;

public class TransfusionVoiceControl : MonoBehaviour
{
    public AppVoiceExperience voice;
    public DialogflowIntentClient client;
    [TextArea] public string pending = "Asignar Wit español, JSON de servicio y cinco clips. Conectar IniciarInterrogatorio al evento de reacción del facilitador y Escuchar a un botón del estudiante. Conectar FinalizarInterrogatorio al continuar. La sincronización Photon queda pendiente.";

    [ContextMenu("1 Iniciar interrogatorio durante reaccion")]
    public void IniciarInterrogatorio() { if (client != null) client.SetInterrogationActive(true); }

    [ContextMenu("2 Escuchar una pregunta")]
    public void Escuchar()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Voz] Usar en Play Mode."); return; }
        if (voice == null || voice.Configuration == null) { Debug.LogWarning("[Voz] Falta configurar Wit.ai en español."); return; }
        voice.Activate();
    }

    [ContextMenu("3 Finalizar interrogatorio")]
    public void FinalizarInterrogatorio()
    {
        if (client != null) client.SetInterrogationActive(false);
        if (voice != null && voice.Active) voice.Deactivate();
    }
}
