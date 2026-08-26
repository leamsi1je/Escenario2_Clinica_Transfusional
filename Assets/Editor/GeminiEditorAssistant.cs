using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

public class GeminiEditorAssistant : EditorWindow
{
    private string apiKey = ""; // Pon tu API Key aquí
    private string prompt = "Ej: Busca el objeto con el tag 'Enemigo' o el script 'EnemyAI' y hazlo el doble de grande...";
    private string responseText = "Aquí aparecerá el estado de la operación...";
    private bool isWaiting = false;
    private Vector2 scrollPosition;

    private TransformInstruction pendingInstruction = null;
    private GameObject pendingObject = null;

    [MenuItem("Herramientas/Asistente Gemini Avanzado")]
    public static void ShowWindow()
    {
        GetWindow<GeminiEditorAssistant>("Asistente Gemini Avanzado");
    }

    private void OnGUI()
    {
        GUILayout.Label("Gemini Pro - Asistente Autónomo", EditorStyles.boldLabel);
        apiKey = EditorGUILayout.TextField("API Key:", apiKey);
        GUILayout.Space(10);

        GUI.enabled = !isWaiting && !string.IsNullOrEmpty(apiKey) && pendingInstruction == null;

        GUILayout.Label("¿Qué quieres hacer en la escena? (Gemini buscará el objeto)", EditorStyles.label);
        prompt = EditorGUILayout.TextArea(prompt, GUILayout.Height(60));
        GUILayout.Space(10);

        // --- BOTÓN PRINCIPAL ÚNICO ---
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("Preguntar a Gemini", GUILayout.Height(40)))
        {
            AnalizarAccionEnEscena();
        }
        GUI.backgroundColor = Color.white;

        GUI.enabled = true; // Restaurar UI interactiva

        // --- SECCIÓN DE CONFIRMACIÓN ---
        if (pendingInstruction != null && pendingObject != null)
        {
            GUILayout.Space(15);
            EditorGUILayout.HelpBox($"⚠️ GEMINI ELIGIÓ EL OBJETO: '{pendingObject.name}'\n\nEXPLICACIÓN:\n{pendingInstruction.explanation}", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("✅ Confirmar Cambios", GUILayout.Height(40)))
            {
                AplicarCambiosConfirmados();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("❌ Cancelar", GUILayout.Height(40)))
            {
                pendingInstruction = null;
                pendingObject = null;
                responseText = "Operación cancelada por el usuario.";
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(15);
        }

        GUILayout.Space(15);
        GUILayout.Label("Log / Estado:", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.SelectableLabel(responseText, EditorStyles.wordWrappedLabel, GUILayout.Height(250));
        EditorGUILayout.EndScrollView();
    }

    // NUEVA FUNCIÓN: Escanea Nombre, Tags y Scripts para que Gemini elija
    private string EscanearEscenaParaBusqueda()
    {
        GameObject[] todosLosObjetos = FindObjectsOfType<GameObject>();
        StringBuilder sb = new StringBuilder();
        int contador = 0;

        foreach (GameObject obj in todosLosObjetos)
        {
            if (!obj.activeInHierarchy) continue;

            Component[] componentes = obj.GetComponents<Component>();
            List<string> nombresComponentes = new List<string>();
            bool esRelevante = false;

            foreach (var comp in componentes)
            {
                if (comp == null) continue;
                string compName = comp.GetType().Name;

                // Ignoramos el Transform porque todos lo tienen, pero anotamos el resto
                if (compName != "Transform") nombresComponentes.Add(compName);

                // Consideramos relevante si tiene Scripts personalizados (MonoBehaviour), mallas, colisiones o luces
                if (comp is MonoBehaviour || comp is MeshRenderer || comp is Collider || comp is Light || comp is Camera)
                {
                    esRelevante = true;
                }
            }

            if (esRelevante)
            {
                sb.AppendLine($"- Nombre: '{obj.name}' | Tag: '{obj.tag}' | Pos: {obj.transform.position} | Escala: {obj.transform.localScale} | Componentes/Scripts: [{string.Join(", ", nombresComponentes)}]");
                contador++;

                if (contador >= 80) // Límite de seguridad
                {
                    sb.AppendLine("... (Límite de objetos alcanzado).");
                    break;
                }
            }
        }
        return sb.ToString();
    }

    private async void AnalizarAccionEnEscena()
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            EditorUtility.DisplayDialog("Aviso", "Por favor, escribe lo que quieres hacer antes de preguntar.", "OK");
            return;
        }

        isWaiting = true;
        responseText = "1. Analizando todos los objetos, tags y scripts de la escena...\n2. Pensando qué objeto modificar y cómo...";
        Repaint();

        string contextoEscena = EscanearEscenaParaBusqueda();

        string promptJSON = $@"
Eres un asistente experto de Unity 3D. 
El usuario quiere modificar un objeto en la escena, pero NO lo ha seleccionado manualmente.
Tu tarea es leer la petición del usuario, buscar en la lista de objetos disponibles cuál es el más adecuado (basándote en su Nombre, Tag, o Componentes/Scripts) y generar los nuevos valores de Transformación.

Petición del usuario: '{prompt}'

Lista de objetos en la escena actual:
{contextoEscena}

Responde ÚNICAMENTE con un objeto JSON válido con esta estructura exacta. En 'targetObjectName' pon exactamente el Nombre del objeto que elegiste modificar. En 'explanation', explica por qué elegiste ese objeto (ej: 'Lo elegí porque tiene el Tag Enemigo') y qué cambios vas a aplicarle:
{{
    ""targetObjectName"": ""NombreExactoDelObjeto"",
    ""posX"": 0.0, ""posY"": 0.0, ""posZ"": 0.0,
    ""rotX"": 0.0, ""rotY"": 0.0, ""rotZ"": 0.0,
    ""scaleX"": 1.0, ""scaleY"": 1.0, ""scaleZ"": 1.0,
    ""modifyPosition"": true,
    ""modifyRotation"": true,
    ""modifyScale"": true,
    ""explanation"": ""Tu explicación en español justificando tu elección y los cambios""
}}";

        string jsonResult = await EnviarPeticion(promptJSON);

        if (!string.IsNullOrEmpty(jsonResult))
        {
            jsonResult = jsonResult.Replace("```json", "").Replace("```", "").Trim();
            try
            {
                pendingInstruction = JsonUtility.FromJson<TransformInstruction>(jsonResult);

                // Buscamos el objeto que Gemini eligió por su nombre
                pendingObject = GameObject.Find(pendingInstruction.targetObjectName);

                if (pendingObject != null)
                {
                    // Seleccionamos el objeto en el editor visualmente para que el usuario sepa de qué hablamos
                    Selection.activeGameObject = pendingObject;
                    EditorGUIUtility.PingObject(pendingObject);

                    responseText = $"¡Propuesta lista! Gemini ha encontrado el objeto '{pendingObject.name}'. Lee la advertencia amarilla.";
                }
                else
                {
                    responseText = $"Gemini sugirió modificar '{pendingInstruction.targetObjectName}', pero no pude encontrar ese objeto en la escena.";
                    pendingInstruction = null;
                }
            }
            catch (System.Exception e)
            {
                responseText = $"Error al leer el JSON de Gemini: {e.Message}\nRecibido: {jsonResult}";
            }
        }
        isWaiting = false;
        Repaint();
    }

    private void AplicarCambiosConfirmados()
    {
        if (pendingObject == null || pendingInstruction == null) return;

        Transform t = pendingObject.transform;
        Undo.RegisterCompleteObjectUndo(t, "Modificar Objeto Autónomo con Gemini");

        if (pendingInstruction.modifyPosition) t.position = new Vector3(pendingInstruction.posX, pendingInstruction.posY, pendingInstruction.posZ);
        if (pendingInstruction.modifyRotation) t.eulerAngles = new Vector3(pendingInstruction.rotX, pendingInstruction.rotY, pendingInstruction.rotZ);
        if (pendingInstruction.modifyScale) t.localScale = new Vector3(pendingInstruction.scaleX, pendingInstruction.scaleY, pendingInstruction.scaleZ);

        responseText = $"✅ ¡Éxito! El objeto '{pendingObject.name}' fue modificado.\n\nResumen de IA:\n{pendingInstruction.explanation}";
        pendingInstruction = null;
        pendingObject = null;
        EditorUtility.SetDirty(t);
        Repaint();
    }

    private async Task<string> EnviarPeticion(string textoEnviar)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={apiKey}";

        GeminiRequest requestData = new GeminiRequest
        {
            contents = new Content[] { new Content { parts = new Part[] { new Part { text = textoEnviar } } } }
        };

        string jsonBody = JsonUtility.ToJson(requestData);
        byte[] rawData = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(rawData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone) { await Task.Delay(100); }

            if (request.result == UnityWebRequest.Result.Success)
            {
                GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                if (response?.candidates != null && response.candidates.Length > 0)
                {
                    return response.candidates[0].content.parts[0].text;
                }
            }
            else
            {
                responseText = $"Error de conexión: {request.error}\nDetalles: {request.downloadHandler.text}";
            }
        }
        return null;
    }
}

// Clases de datos añadidas al final del archivo para evitar errores de compilación
[System.Serializable]
public class TransformInstruction
{
    public string targetObjectName; // ¡Nuevo campo para que Gemini nos diga qué eligió!
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ;
    public float scaleX, scaleY, scaleZ;
    public bool modifyPosition, modifyRotation, modifyScale;
    public string explanation;
}

[System.Serializable]
public class GeminiResponse { public Candidate[] candidates; }
[System.Serializable]
public class Candidate { public Content content; }
[System.Serializable]
public class Content { public Part[] parts; }
[System.Serializable]
public class Part { public string text; }
[System.Serializable]
public class GeminiRequest { public Content[] contents; }