using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crea en el Escenario 1 el panel grande "IR A ESCENA 2" con el botón INGRESAR
/// (copiado del panel "Datos del paciente" para mantener el mismo estilo) y el
/// controlador TransicionEscenario2.
/// Menú: Herramientas > Paciente > Crear panel IR A ESCENA 2
/// </summary>
public static class TransicionSetup
{
    const string ScenePath = "Assets/Scenes/Escenario1.unity";
    const string PanelName = "Panel_IrEscena2";

    [MenuItem("Herramientas/Paciente/Crear panel IR A ESCENA 2")]
    static void Menu() { if (Apply()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool ok = false;
        try { ok = Apply(); } catch (System.Exception e) { Debug.LogError("[TRANSICION] " + e); }
        if (ok) EditorSceneManager.SaveScene(scene);
        Debug.Log("[TRANSICION] Terminado ok=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static GameObject Root(string name) =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

    public static bool Apply()
    {
        var flujo = Object.FindObjectOfType<FlujoEscenario1>(true);
        if (!flujo) { Debug.LogError("[TRANSICION] Falta FlujoEscenario1 (ejecuta antes 'Aplicar flujo del PDF')"); return false; }
        var src = Root("Panel_DatosPaciente");
        if (!src) { Debug.LogError("[TRANSICION] Falta Panel_DatosPaciente"); return false; }

        // Rehacer desde cero si ya existía
        var oldPanel = Root(PanelName); if (oldPanel) Object.DestroyImmediate(oldPanel);
        foreach (var t in Object.FindObjectsOfType<TransicionEscenario2>(true)) Object.DestroyImmediate(t.gameObject);

        // 1) Panel (copia de "Datos del paciente"), un poco más grande
        var panel = Object.Instantiate(src);
        panel.name = PanelName;
        panel.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
        panel.transform.localScale = src.transform.localScale * 1.25f;
        panel.SetActive(false);

        var textos = panel.GetComponentsInChildren<TMP_Text>(true);
        var titulo = textos.FirstOrDefault(t => t.name == "Title");
        if (titulo) titulo.text = "IR A ESCENA 2";
        var cuerpo = textos.FirstOrDefault(t => t.name == "TextDatosPaciente");
        if (cuerpo)
        {
            cuerpo.name = "TextIrEscena2";
            cuerpo.text = "Escenario 2\nReacción transfusional febril no hemolítica\n\nCuando estés listo, presiona INGRESAR.";
            cuerpo.alignment = TextAlignmentOptions.Center;
        }

        // 2) El botón VOLVER pasa a ser INGRESAR (sin su acción de cerrar el panel)
        var cerrar = panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cerrar_X");
        if (!cerrar) { Debug.LogError("[TRANSICION] No encontré el botón Cerrar_X/VOLVER en el panel"); Object.DestroyImmediate(panel); return false; }
        cerrar.name = "Btn_Ingresar";
        var boton = cerrar.GetComponentInChildren<Button>(true);
        if (!boton) { Debug.LogError("[TRANSICION] Btn_Ingresar no tiene Button"); Object.DestroyImmediate(panel); return false; }
        boton.onClick = new Button.ButtonClickedEvent();
        var bc = cerrar.GetComponents<MonoBehaviour>().FirstOrDefault(m => m && m.GetType().Name == "ButtonController");
        if (bc)
        {
            var bso = new SerializedObject(bc);
            var tp = bso.FindProperty("text"); if (tp != null) { tp.stringValue = "INGRESAR"; bso.ApplyModifiedPropertiesWithoutUndo(); }
        }
        var bt = cerrar.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if (bt) bt.text = "INGRESAR";
        if (cerrar is RectTransform rt && rt.sizeDelta.x < 200) rt.sizeDelta = new Vector2(200, rt.sizeDelta.y);

        // 3) Controlador de la transición
        var go = new GameObject("TransicionEscenario2");
        var tr = go.AddComponent<TransicionEscenario2>();
        tr.flujo = flujo;
        tr.panel = panel;
        tr.botonIngresar = boton;

        foreach (var c in new Object[] { panel, go, tr }) EditorUtility.SetDirty(c);
        Debug.Log($"[TRANSICION] Creado {PanelName} (título={(titulo ? "ok" : "NO")}, texto={(cuerpo ? "ok" : "NO")}, botón=ok) y TransicionEscenario2.");
        return true;
    }
}
