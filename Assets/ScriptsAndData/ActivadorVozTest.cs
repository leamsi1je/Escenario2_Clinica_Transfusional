using System.Collections;
using Oculus.Voice;
using UnityEngine;

/// <summary>
/// Mantiene el micrófono escuchando siempre y recupera frases que se cortan.
///  - Vuelve a escuchar de inmediato al terminar cada frase (antes esperaba hasta 1 s y se perdía el inicio).
///  - Si la conexión con Wit se corta o se detiene sin "transcripción final", envía la última transcripción
///    parcial a Dialogflow para no perder la pregunta.
/// </summary>
public class ActivadorVozTest : MonoBehaviour
{
    public AppVoiceExperience appVoiceExperience;
    [Tooltip("Destino de las frases recuperadas. Vacío = se busca en la escena.")]
    public DialogflowIntentClientEscenario1 dialogflow;
    [Tooltip("Espera tras detenerse antes de usar la transcripción parcial (s)")]
    public float esperaParcial = 0.8f;

    string ultimaParcial = "";
    bool finalRecibida;
    int frase;

    void Start()
    {
        if (!dialogflow) dialogflow = FindObjectOfType<DialogflowIntentClientEscenario1>(true);
        var ev = appVoiceExperience.VoiceEvents;

        // Pocos mensajes: el Console de Unity se congela (y llega a cerrarse) si se llena de mensajes de voz.
        ev.OnStartListening.AddListener(() => { ultimaParcial = ""; finalRecibida = false; frase++; });
        ev.OnStoppedListening.AddListener(() => StartCoroutine(RecuperarParcial(frase)));
        ev.OnPartialTranscription.AddListener(t => { if (!string.IsNullOrWhiteSpace(t)) ultimaParcial = t; });
        ev.OnFullTranscription.AddListener(t => { Debug.Log("[VOZ] Transcripción final: " + t); finalRecibida = true; });
        ev.OnError.AddListener((err, msg) => { Debug.LogWarning("[VOZ] Error: " + err + " - " + msg); StartCoroutine(RecuperarParcial(frase)); });

        StartCoroutine(MantenerEscuchando());
    }

    IEnumerator RecuperarParcial(int id)
    {
        string parcial = ultimaParcial;
        yield return new WaitForSeconds(esperaParcial);
        // Solo si es la misma frase, no llegó la transcripción final y había texto parcial
        if (id != frase || finalRecibida || string.IsNullOrWhiteSpace(parcial)) yield break;
        finalRecibida = true; // no enviarla dos veces
        Debug.Log("[VOZ] Se cortó la frase; usando la transcripción parcial: " + parcial);
        if (dialogflow) dialogflow.DetectIntent(parcial);
    }

    IEnumerator MantenerEscuchando()
    {
        yield return new WaitForSeconds(1f);
        var espera = new WaitForSeconds(0.1f);
        while (true)
        {
            if (!appVoiceExperience.Active)
            {
                appVoiceExperience.Activate();
            }
            yield return espera;
        }
    }
}
