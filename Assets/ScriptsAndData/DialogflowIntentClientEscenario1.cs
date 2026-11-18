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
public class DialogflowIntentClientEscenario1 : MonoBehaviour
{
    [Serializable]
    public class IntentResponse
    {
        public string intentName = "preguntar_motivo_consulta";
        public AudioClip clip;
        [TextArea] public string subtitle = "Me siento cansada y agitada.";
    }

    [Header("Credenciales (JSON de la cuenta de servicio)")]
    [SerializeField] TextAsset serviceAccountJson;
    [SerializeField] string languageCode = "es";

    [Header("Respuestas de la paciente")]
    [SerializeField] AudioSource patientAudio;
    [Tooltip("Confianza mínima de Dialogflow. Las frases recortadas por el micrófono suelen llegar con ~0.55.")]
    [SerializeField, Range(0f, 1f)] float minConfidence = 0.45f;
    [Tooltip("Si Dialogflow duda, se acepta la pregunta del motivo de consulta cuando contiene estas expresiones.")]
    [SerializeField] List<string> palabrasMotivoConsulta = new List<string> {
        "motivo", "consulta", "la trae", "le trae", "te trae", "qué siente", "que siente", "cómo se siente", "como se siente",
        "molestia", "por qué vino", "por que vino", "qué le pasa", "que le pasa", "qué tiene", "que tiene", "síntoma", "sintoma",
        "razón por la que", "razon por la que", "porque vino", "porque ha venido", "ha venido", "la trajo", "le trajo", "a qué viene", "a que viene", "qué le duele", "que le duele" };
    [SerializeField] List<IntentResponse> responses = new List<IntentResponse> { new IntentResponse() };

    [Header("Eventos opcionales")]
    public UnityEvent<string> onSubtitle;   // p.ej. para mostrar texto en pantalla
    public UnityEvent onNoMatch;            // la frase no corresponde a ninguna pregunta clínica

    [Header("Prueba en el Editor")]
    [SerializeField] string testPhrase = "¿Qué la trae por aquí?";

    DialogflowAuth auth;
    string sessionId;
    bool busy;
    string pendiente; // frase que llegó mientras se procesaba otra
    string fragmento; float fragmentoHora; // pedazo corto no entendido (p.ej. "porque") para unir con el siguiente
    const float VentanaFragmento = 4f;

    void Awake()
    {
        sessionId = Guid.NewGuid().ToString();
        if (serviceAccountJson == null) { Debug.LogError("[Dialogflow] Falta asignar el JSON de la cuenta de servicio."); return; }
        try { auth = new DialogflowAuth(serviceAccountJson.text); }
        catch (Exception ex) { Debug.LogError("[Dialogflow] " + ex.Message); }
        // valores guardados en la escena con el umbral antiguo (0.6)
        if (minConfidence > 0.45f) minConfidence = 0.45f;
    }

    // Pide el token al iniciar para que la primera pregunta no tenga demora extra.
    IEnumerator Start()
    {
        if (auth == null) yield break;
        string error = null;
        yield return auth.EnsureToken(e => error = e);
        if (error != null) Debug.LogWarning("[Dialogflow] Token inicial: " + error);
        else Debug.Log("[Dialogflow] Conexión lista.");
    }

    public void DetectIntent(string transcription)
    {
        if (auth == null || string.IsNullOrWhiteSpace(transcription)) return;
        if (patientAudio != null && patientAudio.isPlaying) return;
        if (busy) { pendiente = transcription; return; } // se procesa al terminar la actual
        // Si antes llegó un pedazo corto sin sentido ("porque"), se une con esta frase ("vino").
        if (!string.IsNullOrEmpty(fragmento) && Time.time - fragmentoHora < VentanaFragmento)
        {
            transcription = fragmento + " " + transcription;
            Debug.Log($"[Dialogflow] Uniendo frase cortada: \"{transcription}\"");
        }
        fragmento = null;
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
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            req.SetRequestHeader("Authorization", "Bearer " + auth.Token);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 401) auth.Invalidate();
                Debug.LogError($"[Dialogflow] detectIntent falló ({req.responseCode}): {req.error}\n{req.downloadHandler.text}");
                HandleResult(null, text); // sin conexión: aún se puede reconocer por palabras clave
            }
            else
            {
                HandleResult(JsonUtility.FromJson<DetectIntentResponse>(req.downloadHandler.text)?.queryResult, text);
            }
        }

        busy = false;

        // Si llegó otra frase mientras se procesaba esta, se procesa ahora.
        if (!string.IsNullOrWhiteSpace(pendiente))
        {
            string siguiente = pendiente; pendiente = null;
            DetectIntent(siguiente);
        }
    }

    void HandleResult(QueryResult r, string texto)
    {
        bool reconocida = r != null && r.intent != null && !string.IsNullOrEmpty(r.intent.displayName);
        if (reconocida)
            Debug.Log($"[Dialogflow] \"{r.queryText}\" → {r.intent.displayName} (confianza {r.intentDetectionConfidence:0.00})");

        bool valida = reconocida && !r.intent.isFallback && r.intentDetectionConfidence >= minConfidence;
        IntentResponse match = valida ? responses.Find(x => x.intentName == r.intent.displayName) : null;

        // Red de seguridad: Dialogflow dudó, pero la frase claramente pregunta por el motivo de consulta.
        if (match == null && PareceMotivoConsulta(texto))
        {
            match = responses.Find(x => x.intentName == "preguntar_motivo_consulta");
            if (match != null) Debug.Log($"[Dialogflow] \"{texto}\" aceptada como motivo de consulta por palabras clave.");
        }

        if (match == null)
        {
            if (valida) Debug.Log($"[Dialogflow] El intent '{r.intent.displayName}' no tiene respuesta asignada en Responses.");
            // Guardar pedazos cortos (1-3 palabras) por si la frase se cortó y llega el resto.
            if (!string.IsNullOrWhiteSpace(texto) && texto.Trim().Split(' ').Length <= 3)
            { fragmento = texto.Trim().TrimEnd('.', '?', '!'); fragmentoHora = Time.time; }
            onNoMatch?.Invoke(); // p.ej. "Buenos días": la paciente no entrega información clínica
            return;
        }

        PlayClip(match.clip);
        onSubtitle?.Invoke(match.subtitle);
    }

    bool PareceMotivoConsulta(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        string t = texto.ToLowerInvariant();
        foreach (var p in palabrasMotivoConsulta)
            if (!string.IsNullOrEmpty(p) && t.Contains(p.ToLowerInvariant())) return true;
        return false;
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
