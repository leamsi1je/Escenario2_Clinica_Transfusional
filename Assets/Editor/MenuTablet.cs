using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú principal de la tablet en 2 columnas (más compacto: no hay que estirar la mano).
///   Datos del paciente     | Antecedentes
///   Funciones vitales      | Examen físico
///   Examen de laboratorio  | Indicación transfusional
///              Regresar (centrado)
/// Menú: Herramientas > Paciente > Menú de la tablet en 2 columnas
/// </summary>
public static class MenuTablet
{
    const string ScenePath = "Assets/Scenes/Escenario1.unity";
    public static readonly string[] Orden = {
        "Btn_DatosPaciente", "Btn_Antecedentes",
        "Btn_FuncionesVitales", "Btn_ExamenFisico",
        "Btn_ExamenLaboratorio", "Btn_IndicacionTransfucional",
        "Btn_Regresar" };

    [MenuItem("Herramientas/Paciente/Menú de la tablet en 2 columnas")]
    static void Menu() { if (Apply()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool ok = false;
        try { ok = Apply(); } catch (System.Exception e) { Debug.LogError("[MENU] " + e); }
        if (ok) EditorSceneManager.SaveScene(scene);
        Debug.Log("[MENU] Terminado ok=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool Apply()
    {
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Panel_seleccion");
        var btns = root ? root.transform.Find("Btns") : null;
        if (!btns) { Debug.LogError("[MENU] No encontré Panel_seleccion/Btns"); return false; }
        var inactivos = new List<GameObject>();
        for (var t = btns; t; t = t.parent) if (!t.gameObject.activeSelf) { inactivos.Add(t.gameObject); t.gameObject.SetActive(true); }
        bool ok = IgualarIndicacion(btns) && Ordenar(btns) && IgualarLetra(btns);
        if (ok) Foto(btns);
        foreach (var g in inactivos) g.SetActive(false);
        return ok;
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

    public static bool Ordenar(Transform btns)
    {
        var botones = Orden.Select(n => btns.Find(n)).ToList();
        if (botones.Any(b => !b)) { Debug.LogError("[MENU] Faltan botones: " + string.Join(", ", Orden.Where(n => !btns.Find(n)))); return false; }

        var vis = botones.Select(b => Visual(b, btns)).ToList();
        float ancho = vis.Max(v => v.size.x), alto = vis.Max(v => v.size.y);
        float pasoX = ancho * 1.08f, pasoY = alto * 1.12f;
        // centro y parte de arriba del menú actual
        float centroX = vis.Average(v => v.center.x);
        float arriba = vis.Max(v => v.center.y);
        float z = vis[0].center.z;

        for (int i = 0; i < botones.Count; i++)
        {
            int fila = i / 2, col = i % 2;
            bool ultimoSolo = i == botones.Count - 1 && col == 0;
            float x = ultimoSolo ? centroX : centroX + (col == 0 ? -pasoX / 2f : pasoX / 2f);
            var destino = new Vector3(x, arriba - fila * pasoY, z);
            var actual = Visual(botones[i], btns).center;
            botones[i].localPosition += destino - actual;
            botones[i].SetSiblingIndex(i);
        }
        Debug.Log($"[MENU] Menú en 2 columnas (botón {ancho:0.000} x {alto:0.000})");
        return true;
    }

    static UnityEventBase EventoSelect(Transform boton, out Component dueño)
    {
        dueño = null;
        foreach (var c in boton.GetComponents<Component>())
        {
            if (!c) continue;
            var f = c.GetType().GetField("_whenSelect", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null && typeof(UnityEventBase).IsAssignableFrom(f.FieldType)) { dueño = c; return (UnityEventBase)f.GetValue(c); }
        }
        return null;
    }

    // "Indicación transfusional" pasa a tener la misma forma que los demás botones (clon de "Datos del paciente"),
    // conservando lo que hace: mostrar las opciones A/B/C y ocultar el menú.
    static bool IgualarIndicacion(Transform btns)
    {
        var ind = btns.Find("Btn_IndicacionTransfucional");
        var datos = btns.Find("Btn_DatosPaciente");
        if (!ind || !datos) return false;
        if (ind.name == "Btn_IndicacionTransfucional" && ind.GetComponent<MarcaIgualado>()) return true; // ya hecho

        var evOrig = EventoSelect(ind, out _);
        if (evOrig == null) { Debug.LogError("[MENU] Indicación sin evento de selección"); return false; }
        var acciones = new List<(Object target, string metodo)>();
        for (int i = 0; i < evOrig.GetPersistentEventCount(); i++)
            acciones.Add((evOrig.GetPersistentTarget(i), evOrig.GetPersistentMethodName(i)));

        var clon = Object.Instantiate(datos.gameObject, btns).transform;
        clon.localPosition = ind.localPosition; clon.SetSiblingIndex(ind.GetSiblingIndex());
        var ev = EventoSelect(clon, out var dueño);
        while (ev.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(ev, 0);
        foreach (var (target, metodo) in acciones)
        {
            var accion = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, metodo);
            UnityEventTools.AddVoidPersistentListener(ev, accion);
        }
        EditorUtility.SetDirty(dueño);

        Object.DestroyImmediate(ind.gameObject);
        clon.name = "Btn_IndicacionTransfucional";
        clon.gameObject.AddComponent<MarcaIgualado>();
        var lbl = clon.GetComponentsInChildren<TMP_Text>(true).First();
        lbl.text = "Indicación transfusional";

        var flujo = Object.FindObjectOfType<FlujoEscenario1>(true);
        if (flujo) { flujo.botonIndicacion = clon.gameObject; flujo.textoBotonIndicacion = lbl; EditorUtility.SetDirty(flujo); }
        Debug.Log($"[MENU] Indicación igualada ({acciones.Count} acciones: {string.Join(", ", acciones.Select(a => a.metodo))})");
        return true;
    }

    // Un solo tamaño de letra para todos los botones, sin cortar palabras ("Antecedent-es").
    static bool IgualarLetra(Transform btns)
    {
        var labels = Orden.Select(n => btns.Find(n).GetComponentsInChildren<TMP_Text>(true).First()).ToList();
        var datosL = labels[0];
        // punto de partida: la altura de la caja (la letra nunca será más alta que el botón)
        float tam = datosL.rectTransform.rect.height * 0.45f;
        foreach (var l in labels) { l.enableAutoSizing = false; l.enableWordWrapping = true; }
        for (int k = 0; k < 80; k++)
        {
            bool cabe = labels.All(l =>
            {
                l.fontSize = tam;
                var r = l.rectTransform.rect;
                bool palabras = l.text.Split(' ').All(w => l.GetPreferredValues(w).x <= r.width * 0.92f);
                return palabras && l.GetPreferredValues(l.text, r.width, 0).y <= r.height * 0.95f;
            });
            if (cabe) break;
            tam *= 0.96f;
        }
        foreach (var l in labels) { l.fontSize = tam; l.alignment = TextAlignmentOptions.Center; EditorUtility.SetDirty(l); }

        // Centrar cada texto sobre su botón (el de "Regresar" quedaba bajo)
        for (int i = 0; i < labels.Count; i++)
        {
            var boton = btns.Find(Orden[i]);
            var rs = boton.GetComponentsInChildren<Renderer>(true).Where(r => !(r is MeshRenderer mr && mr.GetComponent<TMP_Text>())).ToList();
            if (rs.Count == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var rt = labels[i].rectTransform;
            var cs = new Vector3[4]; rt.GetWorldCorners(cs);
            Vector3 centroTexto = (cs[0] + cs[2]) / 2f;
            Vector3 d = b.center - centroTexto;
            // solo en el plano del botón (sin cambiar la profundidad)
            d = Vector3.Project(d, rt.right) + Vector3.Project(d, rt.up);
            rt.position += d;
        }
        Debug.Log($"[MENU] Misma letra para todos los botones: {tam:0.0000}");
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
        File.WriteAllBytes(Path.Combine(dir, "tablet_menu.png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        foreach (var kv in layers) kv.Key.gameObject.layer = kv.Value;
    }
}
