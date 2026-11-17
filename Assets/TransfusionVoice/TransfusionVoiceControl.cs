using UnityEngine;
using Oculus.Voice;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class TransfusionVoiceControl : MonoBehaviour
{
    public AppVoiceExperience voice;
    public DialogflowIntentClient client;
    private bool interrogationActive;
    private bool wasPressed;

    private void Update()
    {
        if (!interrogationActive) return;

        UnityEngine.XR.InputDevice controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool pressed = controller.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool down) && down;
        if ((pressed && !wasPressed) || (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame))
            Escuchar();

        wasPressed = pressed;
    }

    [ContextMenu("1 Iniciar interrogatorio durante reaccion")]
    public void IniciarInterrogatorio()
    {
        interrogationActive = true;
        if (client != null) client.SetInterrogationActive(true);
    }

    [ContextMenu("2 Escuchar una pregunta")]
    public void Escuchar()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Voz] Usar en Play Mode."); return; }
        if (voice == null || voice.Configuration == null) { Debug.LogWarning("[Voz] Falta configurar Wit.ai en español."); return; }
        if (interrogationActive && !voice.Active) voice.Activate();
    }

    [ContextMenu("3 Finalizar interrogatorio")]
    public void FinalizarInterrogatorio()
    {
        interrogationActive = false;
        wasPressed = false;
        if (client != null) client.SetInterrogationActive(false);
        if (voice != null && voice.Active) voice.Deactivate();
    }
}
