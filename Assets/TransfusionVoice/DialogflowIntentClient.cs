using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

/// <summary>
/// Recibe el texto transcrito (Meta Voice SDK), consulta Dialogflow ES y reproduce la respuesta de la paciente.
/// Conecta DetectIntent(string) al evento "On Full Transcription" del AppVoiceExperience.
/// </summary>
public class DialogflowIntentClient : MonoBehaviour
{
    [Serializable]
    public class IntentResponse
    {
        public string intentName = "preguntar_molestias";
        public AudioClip clip;
        [TextArea] public string subtitle = "Me siento caliente.";
    }

    [Header("Credenciales (JSON de la cuenta de servicio)")]
    [SerializeField] TextAsset serviceAccountJson;
    [SerializeField] string languageCode = "es";

    [Header("Respuestas de la paciente")]
    [SerializeField] AudioSource patientAudio;
    [SerializeField, Range(0f, 1f)] float minConfidence = 0.6f;
    [SerializeField] List<IntentResponse> responses = new List<IntentResponse> {
        new IntentResponse { intentName = "preguntar_molestias", subtitle = "Me siento caliente." },
        new IntentResponse { intentName = "preguntar_otras_molestias", subtitle = "No, no tengo otras molestias." },
        new IntentResponse { intentName = "preguntar_falta_aire", subtitle = "No, no me falta el aire." },
        new IntentResponse { intentName = "preguntar_dolor_espalda", subtitle = "No, no me duele la espalda." },
        new IntentResponse { intentName = "preguntar_picazon", subtitle = "No, no tengo picazón." }
    };

    [Header("Eventos opcionales")]
    public UnityEvent<string> onSubtitle;   // p.ej. para mostrar texto en pantalla
    public UnityEvent onNoMatch;            // la frase no corresponde a ninguna pregunta clínica

    [Header("Prueba en el Editor")]
    [SerializeField] string testPhrase = "¿Cómo se siente?";

    [Header("Estado del escenario")]
    [Tooltip("Activar al recibir la reacción del facilitador; desactivar al terminar el interrogatorio.")]
    [SerializeField] bool interrogationActive;
    public void SetInterrogationActive(bool active)
    {
        interrogationActive = active;
        if (!active && patientAudio != null) patientAudio.Stop();
    }

    DialogflowAuth auth;
    string sessionId;
    bool busy;

    void Awake()
    {
        sessionId = Guid.NewGuid().ToString();
        if (serviceAccountJson == null) { Debug.LogError("[Dialogflow] Falta asignar el JSON de la cuenta de servicio."); return; }
        try { auth = new DialogflowAuth(serviceAccountJson.text); }
        catch (Exception ex) { Debug.LogError("[Dialogflow] " + ex.Message); }
    }

    public void DetectIntent(string transcription)
    {
        if (!interrogationActive) return;
        if (auth == null) { Debug.LogWarning("[Dialogflow] Asigna el JSON de servicio antes de probar."); return; }
        if (string.IsNullOrWhiteSpace(transcription)) return;
        if (busy || (patientAudio != null && patientAudio.isPlaying)) return;
        StartCoroutine(DetectIntentRoutine(transcription));
    }

    [ContextMenu("Probar frase de prueba")]
    void RunTestPhrase() => DetectIntent(testPhrase);

    IEnumerator DetectIntentRoutine(string text)
    {
        busy = true;

        string error = null;
        yield return auth.EnsureToken(e => error = e);
        if (error != null) { Debug.LogError("[Dialogflow] " + error); busy = false; yield break; }

        string url = $"https://dialogflow.googleapis.com/v2/projects/{auth.ProjectId}/agent/sessions/{sessionId}:detectIntent";
        var body = new DetectIntentRequest
        {
            queryInput = new QueryInput { text = new TextInput { text = text, languageCode = languageCode } }
        };

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.timeout = 30;
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            req.SetRequestHeader("Authorization", "Bearer " + auth.Token);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 401) auth.Invalidate();
                Debug.LogError($"[Dialogflow] detectIntent falló ({req.responseCode}): {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                HandleResult(JsonUtility.FromJson<DetectIntentResponse>(req.downloadHandler.text)?.queryResult);
            }
        }

        busy = false;
    }

    void HandleResult(QueryResult r)
    {
        if (!interrogationActive) return;
        if (r == null || r.intent == null || string.IsNullOrEmpty(r.intent.displayName))
        {
            onNoMatch?.Invoke();
            return;
        }

        Debug.Log($"[Dialogflow] \"{r.queryText}\" → {r.intent.displayName} (confianza {r.intentDetectionConfidence:0.00})");

        if (r.intent.isFallback || r.intentDetectionConfidence < minConfidence)
        {
            onNoMatch?.Invoke();
            return;
        }

        var match = responses.Find(x => x.intentName == r.intent.displayName);
        if (match == null)
        {
            Debug.Log($"[Dialogflow] El intent '{r.intent.displayName}' no tiene respuesta asignada en Responses.");
            return; // p.ej. conversacion_general: la paciente no responde
        }

        Debug.Log("[Paciente] " + match.subtitle);
        PlayClip(match.clip);
        onSubtitle?.Invoke(match.subtitle);
    }

    void PlayClip(AudioClip clip)
    {
        if (!DiagnoseAudio(clip)) return;

        patientAudio.clip = clip;
        patientAudio.Play();
        Debug.Log($"[Dialogflow] Reproduciendo '{clip.name}' ({clip.length:0.0} s) en '{patientAudio.gameObject.name}'.");
    }

    // Revisa las causas más comunes de "no se escucha nada" y las informa en la consola.
    bool DiagnoseAudio(AudioClip clip)
    {
        if (patientAudio == null) { Debug.LogError("[Audio] Patient Audio no está asignado."); return false; }
        if (clip == null) { Debug.LogError("[Audio] El Clip de esta respuesta no está asignado."); return false; }
        if (!patientAudio.gameObject.activeInHierarchy) { Debug.LogError($"[Audio] El objeto '{patientAudio.gameObject.name}' está desactivado."); return false; }
        if (!patientAudio.enabled) { Debug.LogError("[Audio] El componente Audio Source está desactivado (checkbox)."); return false; }

        if (patientAudio.mute) Debug.LogWarning("[Audio] El Audio Source tiene Mute activado.");
        if (patientAudio.volume <= 0.01f) Debug.LogWarning("[Audio] El volumen del Audio Source está en 0.");
        if (AudioListener.pause) Debug.LogWarning("[Audio] AudioListener.pause está activo: el audio global está en pausa.");
        if (AudioListener.volume <= 0.01f) Debug.LogWarning("[Audio] AudioListener.volume está en 0.");

        var listeners = FindObjectsOfType<AudioListener>();
        if (listeners.Length == 0)
            Debug.LogWarning("[Audio] No hay ningún AudioListener activo en la escena: no se puede oír nada.");
        else if (listeners.Length > 1)
            Debug.LogWarning($"[Audio] Hay {listeners.Length} AudioListeners activos; debería haber solo uno.");

        if (patientAudio.spatialBlend > 0.5f && listeners.Length > 0)
        {
            float dist = Vector3.Distance(listeners[0].transform.position, patientAudio.transform.position);
            if (dist > patientAudio.maxDistance)
                Debug.LogWarning($"[Audio] El oyente está a {dist:0.0} m y el Max Distance es {patientAudio.maxDistance:0.0} m: el sonido 3D no llega.");
        }

        if (clip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogError($"[Audio] El clip '{clip.name}' no se pudo cargar. Revisa que el .wav sea válido.");
            return false;
        }
        return true;
    }

    // ---------- DTOs para JsonUtility ----------
    [Serializable] class DetectIntentRequest { public QueryInput queryInput; }
    [Serializable] class QueryInput { public TextInput text; }
    [Serializable] class TextInput { public string text; public string languageCode; }
    [Serializable] class DetectIntentResponse { public QueryResult queryResult; }
    [Serializable] class QueryResult { public string queryText; public IntentInfo intent; public float intentDetectionConfidence; }
    [Serializable] class IntentInfo { public string displayName; public bool isFallback; }
}
