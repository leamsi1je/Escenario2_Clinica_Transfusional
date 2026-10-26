using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogflowIntentClient))]
public class DialogflowIntentClientEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying && GUILayout.Button("Probar frase escrita"))
        {
            var client = (DialogflowIntentClient)target;
            var phrase = serializedObject.FindProperty("testPhrase").stringValue;
            client.DetectIntent(phrase);
        }
    }
}
