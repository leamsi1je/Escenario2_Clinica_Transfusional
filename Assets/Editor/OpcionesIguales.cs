using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hace que las alternativas A, B y C de "Indicación transfusional" se vean idénticas
/// (mismo botón, color, caja y tamaño de letra) para no dar pistas sobre la correcta.
/// Usa la opción B como molde. Menú: Herramientas > Paciente > Igualar opciones A B C
/// </summary>
public static class OpcionesIguales
{
    const string ScenePath = "Assets/Scenes/Escenario1.unity";
    static readonly string[] Textos = {
        "A) Prescribir 2 unidades de concentrado de hematíes.",
        "B) Prescribir 2 unidades de plasma fresco congelado.",
        "C) Prescribir 1 unidad de plaquetas." };

    [MenuItem("Herramientas/Paciente/Igualar opciones A B C")]
    static void Menu() { if (Apply()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool ok = false;
        try { ok = Apply(); } catch (System.Exception e) { Debug.LogError("[OPC] " + e); }
        if (ok) EditorSceneManager.SaveScene(scene);
        Debug.Log("[OPC] Terminado ok=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static Bounds Visual(Transform t, Transform space)
    {
        bool first = true; var b = new Bounds();
        void Add(Vector3 w) { var l = space.InverseTransformPoint(w); if (first) { b = new Bounds(l, Vector3.zero); first = false; } else b.Encapsulate(l); }
        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { Add(r.bounds.min); Add(r.bounds.max); }
        var cs = new Vector3[4];
        foreach (var g in t.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) { g.rectTransform.GetWorldCorners(cs); foreach (var c in cs) Add(c); }
        return b;
    }

    public static bool Apply()
    {
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Panel_seleccion");
        var menu = root ? root.transform.Find("Btns Opciones") : null;
        if (!menu) { Debug.LogError("[OPC] No encontré 'Btns Opciones'"); return false; }

        // Activar temporalmente para medir
        var inactivos = new List<GameObject>();
        for (var t = menu; t; t = t.parent) if (!t.gameObject.activeSelf) { inactivos.Add(t.gameObject); t.gameObject.SetActive(true); }

        var A = menu.Find("Btn_OpcionA"); var B = menu.Find("Btn_OpcionB"); var C = menu.Find("Btn_OpcionC");
        if (!A || !B || !C) { Debug.LogError("[OPC] Faltan botones"); return false; }

        // Centro visual de cada opción (para respetar dónde estaban) y del molde B
        var cA = Visual(A, menu).center; var cB = Visual(B, menu).center; var cC = Visual(C, menu).center;

        Transform Clonar(Transform viejo, string nombre, Vector3 centroDestino, int indice)
        {
            var nuevo = Object.Instantiate(B.gameObject, menu).transform;
            nuevo.name = nombre;
            nuevo.localRotation = B.localRotation; nuevo.localScale = B.localScale;
            // misma columna que B, a la altura que tenía la opción original
            var destino = new Vector3(cB.x, centroDestino.y, cB.z);
            nuevo.localPosition = B.localPosition + (destino - cB);
            nuevo.SetSiblingIndex(indice);
            Object.DestroyImmediate(viejo.gameObject);
            return nuevo;
        }
        int iA = A.GetSiblingIndex(), iC = C.GetSiblingIndex();
        A = Clonar(A, "Btn_OpcionA", cA, iA);
        C = Clonar(C, "Btn_OpcionC", cC, iC);

        // Mismo texto, misma caja y UN SOLO tamaño de letra para las tres (el que hace caber la más larga)
        var botones = new[] { A, B, C };
        var labels = botones.Select(b => b.GetComponentsInChildren<TMP_Text>(true).First()).ToArray();
        // tamaño de partida: el mayor tamaño que tenía la opción B
        float max = Mathf.Max(labels[1].fontSizeMax, labels[1].fontSize);
        if (max <= 0.0001f) max = 36f;
        for (int i = 0; i < 3; i++) { labels[i].text = Textos[i]; labels[i].enableAutoSizing = false; labels[i].enableWordWrapping = true; }
        // bajar hasta que el texto más largo quepa en la caja (todas las cajas son iguales: vienen del molde B)
        var r = labels[1].rectTransform.rect;
        float tam = max;
        for (int k = 0; k < 60; k++)
        {
            bool cabe = labels.All(l => { l.fontSize = tam; var pref = l.GetPreferredValues(l.text, r.width, 0); return pref.y <= r.height * 0.95f; });
            if (cabe) break;
            tam *= 0.95f;
        }
        Debug.Log($"[OPC] caja={r.size} tamaño inicial={max:0.00}");
        foreach (var l in labels)
        {
            l.enableAutoSizing = false;
            l.fontSize = tam;
            l.alignment = TextAlignmentOptions.Center;
            l.overflowMode = TextOverflowModes.Truncate;
            EditorUtility.SetDirty(l);
        }
        Debug.Log($"[OPC] Las tres opciones con tamaño de letra {tam:0.0}");

        // Mismo espacio entre A, B, C y "Regresar"
        float paso = Visual(A, menu).center.y - Visual(B, menu).center.y;
        var bc = Visual(B, menu).center;
        var cNuevo = Visual(C, menu).center;
        C.localPosition += new Vector3(0, (bc.y - paso) - cNuevo.y, 0);
        var reg = menu.Find("Btn_Regresar");
        if (reg)
        {
            var rv = Visual(reg, menu).center;
            float objetivo = Visual(C, menu).center.y - paso;
            reg.localPosition += new Vector3(0, objetivo - rv.y, 0);
        }

        // El flujo apunta a los botones nuevos
        var flujo = Object.FindObjectOfType<FlujoEscenario1>(true);
        if (flujo) { flujo.opcionA = A.gameObject; flujo.opcionB = B.gameObject; flujo.opcionC = C.gameObject; EditorUtility.SetDirty(flujo); }

        Foto(menu);
        foreach (var g in inactivos) g.SetActive(false);
        return true;
    }

    static void Foto(Transform target)
    {
        string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads", "Fotos_Maria");
        Directory.CreateDirectory(dir);
        ShaderUtil.allowAsyncCompilation = false;
        var b = new Bounds(target.position, Vector3.zero);
        foreach (var r in target.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var go = new GameObject("__cam"); var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .2f, .25f); cam.nearClipPlane = 0.005f;
        float dist = Mathf.Max(0.35f, b.extents.magnitude * 2.2f);
        go.transform.position = b.center - target.forward * dist; go.transform.LookAt(b.center, target.up);
        cam.fieldOfView = 2f * Mathf.Atan(b.extents.magnitude * 1.3f / dist) * Mathf.Rad2Deg;
        var layers = target.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
        foreach (var t in layers.Keys) t.gameObject.layer = 31;
        cam.cullingMask = 1 << 31;
        var rt = new RenderTexture(900, 900, 24); cam.targetTexture = rt;
        cam.Render(); cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(900, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, "tablet_opciones.png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        foreach (var kv in layers) kv.Key.gameObject.layer = kv.Value;
    }
}
