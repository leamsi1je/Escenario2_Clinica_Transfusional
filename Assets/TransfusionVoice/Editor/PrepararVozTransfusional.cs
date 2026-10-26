using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Oculus.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;

public static class PrepararVozTransfusional
{
    [MenuItem("Tools/Transfusion/Preparar interrogatorio de voz")]
    public static void Preparar()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Escena_nueva") { Debug.LogError("Abrir Escena_nueva antes de preparar la voz."); return; }
        var root = GameObject.Find("SistemaVozPaciente");
        if (root == null) { root = new GameObject("SistemaVozPaciente"); Undo.RegisterCreatedObjectUndo(root, "Preparar voz"); }
        var patient = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.scene == scene && x.name == "Paciente");
        var audioObj = patient != null ? patient : root;
        var audio = audioObj.GetComponent<AudioSource>();
        if (audio == null) { audio = Undo.AddComponent<AudioSource>(audioObj); audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 1; audio.maxDistance = 15; }
        var client = root.GetComponent<DialogflowIntentClient>();
        if (client == null) client = Undo.AddComponent<DialogflowIntentClient>(root);
        var so = new SerializedObject(client);
        so.FindProperty("patientAudio").objectReferenceValue = audio;
        so.ApplyModifiedProperties();
        var voice = Resources.FindObjectsOfTypeAll<AppVoiceExperience>().FirstOrDefault(x => x.gameObject.scene == scene);
        if (voice == null) voice = Undo.AddComponent<AppVoiceExperience>(root);
        if (voice.RuntimeConfiguration == null) voice.RuntimeConfiguration = new WitRuntimeConfiguration();
        voice.RuntimeConfiguration.transcribeOnly = true;
        if (voice.RuntimeConfiguration.witConfiguration == null)
        {
            var configs = AssetDatabase.FindAssets("t:WitConfiguration");
            if (configs.Length == 1) voice.RuntimeConfiguration.witConfiguration = AssetDatabase.LoadAssetAtPath<WitConfiguration>(AssetDatabase.GUIDToAssetPath(configs[0]));
        }
        bool wired = Enumerable.Range(0, voice.VoiceEvents.OnFullTranscription.GetPersistentEventCount()).Any(i => voice.VoiceEvents.OnFullTranscription.GetPersistentTarget(i) == client && voice.VoiceEvents.OnFullTranscription.GetPersistentMethodName(i) == "DetectIntent");
        if (!wired) UnityEventTools.AddPersistentListener(voice.VoiceEvents.OnFullTranscription, client.DetectIntent);
        var control = root.GetComponent<TransfusionVoiceControl>();
        if (control == null) control = Undo.AddComponent<TransfusionVoiceControl>(root);
        control.voice = voice;
        control.client = client;
        EditorUtility.SetDirty(voice); EditorUtility.SetDirty(client); EditorUtility.SetDirty(control); EditorUtility.SetDirty(audio);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("[Transfusion] Voz preparada: 5 respuestas, AudioSource y OnFullTranscription -> DetectIntent. Pendientes: Wit, credenciales, clips y eventos del escenario.");
    }
}
