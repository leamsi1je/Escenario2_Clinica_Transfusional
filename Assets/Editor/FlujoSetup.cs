using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ajusta la escena al flujo del PDF (Escenario 1):
/// datos clínicos correctos, apartado "Antecedentes", botones VOLVER, bloqueo de "Indicación transfusional",
/// registro de la respuesta y voz humanizada de la paciente.
/// Menú: Herramientas > Paciente > Aplicar flujo del PDF
/// </summary>
public static class FlujoSetup
{
    const string ScenePath = "Assets/Scenes/Escenario1.unity";
    const string VozPath = "Assets/Audio/maria_respuesta_motivo.mp3";

    [MenuItem("Herramientas/Paciente/Aplicar flujo del PDF")]
    static void Menu() { if (Apply()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool ok = false;
        try { ok = Apply(); } catch (System.Exception e) { Debug.LogError("[FLUJO] " + e); }
        if (ok) EditorSceneManager.SaveScene(scene);
        Debug.Log("[FLUJO] Terminado ok=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static Transform[] All() => EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
    static Transform Find(string path)
    {
        var parts = path.Split('/');
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        if (!root) return null;
        var t = root.transform;
        for (int i = 1; i < parts.Length && t; i++) t = t.Find(parts[i]);
        if (!t) Debug.LogWarning("[FLUJO] No encontrado: " + path);
        return t;
    }
    static void SetText(string path, string txt)
    {
        var t = Find(path); if (!t) return;
        var tmp = t.GetComponent<TMP_Text>(); if (!tmp) return;
        Undo.RecordObject(tmp, "texto"); tmp.text = txt; EditorUtility.SetDirty(tmp);
    }
    static TMP_Text Label(Transform btn) => btn ? btn.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault() : null;

    public static bool Apply()
    {
        // 1) La paciente anterior queda invisible pero ACTIVA: ahí vive DialogflowIntentClientEscenario1.
        var client = Object.FindObjectOfType<DialogflowIntentClientEscenario1>(true);
        if (!client) { Debug.LogError("[FLUJO] Falta DialogflowIntentClientEscenario1"); return false; }
        client.gameObject.SetActive(true);
        client.enabled = true;
        foreach (var r in client.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in client.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        Debug.Log("[FLUJO] DialogflowIntentClientEscenario1 activo en " + client.name + " (modelo oculto).");

        // 2) Voz humanizada
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VozPath);
        var so = new SerializedObject(client);
        if (clip)
        {
            so.FindProperty("responses").GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[FLUJO] Voz de la paciente: {clip.name} ({clip.length:0.0} s)");
        }
        else Debug.LogWarning("[FLUJO] No encontré " + VozPath);
        var voz = so.FindProperty("patientAudio").objectReferenceValue as AudioSource;

        // 3) Contenido clínico según el PDF
        SetText("Panel_DatosPaciente/Content/Body/TextDatosPaciente",
            "Nombre: María Rodríguez\nEdad: 68 años\nSexo: femenino\nPeso: 62 kg");
        SetText("Panel_FuncionesVitales/Content/Body/TextFuncionesVitales",
            "Presión arterial: 108/68 mmHg\nFrecuencia cardíaca: 110 lpm\nFrecuencia respiratoria: 22 rpm\nTemperatura: 36,8 °C\nSaturación de oxígeno: 96 %");
        SetText("Panel_ExamenFisico/Content/Header/Title", "Examen físico");
        SetText("Panel_ExamenFisico/Content/Body/Text_Diagnostico",
            "Presenta:\n- Palidez mucocutánea marcada.\n- Fatiga intensa.\n- Disnea de pequeños esfuerzos.\n- Mareos.\n- Taquicardia.\n\nNo presenta:\n- Sangrado activo.\n- Petequias.\n- Equimosis.\n- Alteración neurológica.");
        SetText("Panel_ExamenLaboratorio/Content/Header/Title", "Examen de laboratorio");
        SetText("Panel_IndicacionTransfucional/Content/Header/Title", "Indicación transfusional");
        SetText("Panel_seleccion/Btns Opciones/Btn_OpcionB/Visuals/ButtonVisual/Canvas/Text (TMP)", "Prescribir 2 unidades de plasma fresco congelado.");
        SetText("Panel_seleccion/Btns Opciones/Btn_OpcionA/Visuals/ButtonVisual/Canvas/Text (TMP)", "A) Prescribir 2 unidades de concentrado de hematíes.");
        SetText("Panel_seleccion/Btns Opciones/Btn_OpcionB/Visuals/ButtonVisual/Canvas/Text (TMP)", "B) Prescribir 2 unidades de plasma fresco congelado.");
        SetText("Panel_seleccion/Btns Opciones/Btn_OpcionC/Visuals/ButtonVisual/Canvas/Text (TMP)", "C) Prescribir 1 unidad de plaquetas.");

        // Formato de las alternativas A/B/C: lo hace OpcionesIguales (las tres idénticas).

        // 4) Apartado "Antecedentes" (panel + botón), copiado de "Datos del paciente"
        var panelDatos = Find("Panel_DatosPaciente");
        var btns = Find("Panel_seleccion/Btns");
        var btnDatos = btns ? btns.Find("Btn_DatosPaciente") : null;
        if (!panelDatos || !btnDatos) return false;

        var oldPanel = Find("Panel_Antecedentes"); if (oldPanel) Object.DestroyImmediate(oldPanel.gameObject);
        var oldBtn = btns.Find("Btn_Antecedentes"); if (oldBtn) Object.DestroyImmediate(oldBtn.gameObject);

        var panelAnt = Object.Instantiate(panelDatos.gameObject, panelDatos.parent);
        panelAnt.name = "Panel_Antecedentes";
        panelAnt.transform.SetSiblingIndex(panelDatos.GetSiblingIndex() + 1);
        panelAnt.SetActive(false);
        SetText("Panel_Antecedentes/Content/Header/Title", "Antecedentes");
        var body = panelAnt.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "TextDatosPaciente");
        if (body) { body.name = "TextAntecedentes"; body.GetComponent<TMP_Text>().text = "- Hipertensión arterial controlada.\n- Gastritis crónica.\n- Niega sangrado activo.\n- Sin antecedentes hematológicos."; }

        var btnAnt = Object.Instantiate(btnDatos.gameObject, btns);
        btnAnt.name = "Btn_Antecedentes";
        var lt = Label(btnAnt.transform); if (lt) lt.text = "Antecedentes";
        // El botón copiado apuntaba al panel de Datos: lo redirigimos al de Antecedentes
        var lazyDatos = panelDatos.GetComponent<LazyFollowPanel>();
        var lazyAnt = panelAnt.GetComponent<LazyFollowPanel>();
        int rewired = 0;
        foreach (var c in btnAnt.GetComponentsInChildren<Component>(true))
        {
            if (!c) continue;
            var cso = new SerializedObject(c); var it = cso.GetIterator(); bool changed = false;
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (it.objectReferenceValue == lazyDatos) { it.objectReferenceValue = lazyAnt; changed = true; rewired++; }
                    else if (it.objectReferenceValue == panelDatos.gameObject) { it.objectReferenceValue = panelAnt; changed = true; rewired++; }
                }
            if (changed) cso.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log($"[FLUJO] Antecedentes creado (referencias redirigidas: {rewired}).");

        // 5) Orden y etiquetas de los botones como en el PDF (6 apartados + Regresar)
        string[] orden = { "Btn_DatosPaciente", "Btn_Antecedentes", "Btn_FuncionesVitales", "Btn_ExamenFisico", "Btn_ExamenLaboratorio", "Btn_IndicacionTransfucional", "Btn_Regresar" };
        string[] etiquetas = { "Datos del paciente", "Antecedentes", "Funciones vitales", "Examen físico", "Examen de laboratorio", "Indicación transfusional", "Regresar" };
        var botones = orden.Select(n => btns.Find(n)).ToList();
        if (botones.Any(b => !b)) { Debug.LogError("[FLUJO] Faltan botones en Btns"); return false; }
        for (int i = 0; i < botones.Count; i++) { var l = Label(botones[i]); if (l) l.text = etiquetas[i]; }
        MenuTablet.Ordenar(btns); // 2 columnas

        // 6) Botones de cierre de los paneles → "VOLVER"
        foreach (var p in new[] { "Panel_DatosPaciente", "Panel_Antecedentes", "Panel_FuncionesVitales", "Panel_ExamenFisico", "Panel_ExamenLaboratorio" })
        {
            var x = Find(p + "/Content/Body/Cerrar_X"); if (!x) continue;
            var bc = x.GetComponents<MonoBehaviour>().FirstOrDefault(m => m && m.GetType().Name == "ButtonController");
            if (bc) { var bso = new SerializedObject(bc); var tp = bso.FindProperty("text"); if (tp != null) { tp.stringValue = "VOLVER"; bso.ApplyModifiedPropertiesWithoutUndo(); } }
            var bt = x.Find("Button Text"); if (bt) bt.GetComponent<TMP_Text>().text = "VOLVER";
            var rt = x as RectTransform; if (rt && rt.sizeDelta.x < 140) rt.sizeDelta = new Vector2(140, rt.sizeDelta.y);
        }

        // 6b) Que el texto de cada panel quepa dentro del panel
        foreach (var p in new[] { "Panel_DatosPaciente", "Panel_Antecedentes", "Panel_FuncionesVitales", "Panel_ExamenFisico", "Panel_ExamenLaboratorio" })
        {
            var pt = Find(p); if (!pt) continue;
            var bodyT = pt.Find("Content/Body");
            var bg = pt.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "Background");
            var textos = bodyT.GetComponentsInChildren<TMP_Text>(true).Where(t => t.transform.parent == bodyT).ToList();
            if (bg) textos.AddRange(bg.GetComponentsInChildren<TMP_Text>(true).Where(t => t.transform.parent == bg));
            foreach (var tmp in textos)
            {
                float max = 44f; // tamaño original de los paneles
                tmp.enableAutoSizing = true; tmp.fontSizeMax = max; tmp.fontSizeMin = Mathf.Max(8f, max * 0.4f);
                tmp.overflowMode = TextOverflowModes.Truncate;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.enableWordWrapping = true;
                var r = tmp.rectTransform;
                // el texto pasa a ser hijo del fondo oscuro, así siempre queda dentro del panel
                if (bg && r.parent != bg) r.SetParent(bg, false);
                r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
                var lp = r.localPosition; r.localPosition = new Vector3(lp.x, lp.y, 0f);
                // ocupar todo el panel, debajo del título y del botón VOLVER
                r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 0.5f);
                r.offsetMin = new Vector2(45, 30); r.offsetMax = new Vector2(-45, -115);
                r.localPosition = new Vector3(r.localPosition.x, r.localPosition.y, 0f);
                tmp.fontSizeMin = Mathf.Max(14f, max * 0.55f);
                EditorUtility.SetDirty(tmp);
                Debug.Log($"[FLUJO] body {bodyT.GetComponent<RectTransform>().rect.size}");
                Debug.Log($"[FLUJO] {p}/{tmp.name} rect={r.rect.size} anchors={r.anchorMin}-{r.anchorMax} pos={r.anchoredPosition} font={max} bg={(bg ? bg.rect.size.ToString() : "-")}");
            }
        }

        // 7) Controlador del flujo
        var old = Object.FindObjectOfType<FlujoEscenario1>(true); if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("FlujoEscenario1");
        var f = go.AddComponent<FlujoEscenario1>();
        f.paciente = client;
        f.vozPaciente = voz;
        f.tablet = Find("Panel_seleccion").gameObject;
        f.panelesClinicos = new List<GameObject> {
            Find("Panel_DatosPaciente").gameObject, panelAnt, Find("Panel_FuncionesVitales").gameObject,
            Find("Panel_ExamenFisico").gameObject, Find("Panel_ExamenLaboratorio").gameObject };
        f.botonIndicacion = btns.Find("Btn_IndicacionTransfucional").gameObject;
        f.textoBotonIndicacion = Label(btns.Find("Btn_IndicacionTransfucional"));
        var opc = Find("Panel_seleccion/Btns Opciones");
        f.menuOpciones = opc.gameObject; f.menuPrincipal = btns.gameObject;
        f.opcionA = opc.Find("Btn_OpcionA").gameObject; f.opcionB = opc.Find("Btn_OpcionB").gameObject; f.opcionC = opc.Find("Btn_OpcionC").gameObject;

        Screenshots(btns, panelAnt.transform);
        return true;
    }

    // Lee de la copia de seguridad (antes de los cambios) la caja original de estos textos y la vuelve a aplicar.
    static void RestaurarCajasDeTexto(string[] paths)
    {
        string src = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Escena_nueva_BACKUP_antes_SALSA.unity");
        const string tmpPath = "Assets/__backup_tmp.unity";
        if (!File.Exists(src)) { Debug.LogWarning("[FLUJO] Sin copia de seguridad"); return; }
        File.Copy(src, tmpPath, true); AssetDatabase.ImportAsset(tmpPath);
        var active = EditorSceneManager.GetActiveScene();
        var bk = EditorSceneManager.OpenScene(tmpPath, OpenSceneMode.Additive);
        try
        {
            foreach (var path in paths)
            {
                var parts = path.Split('/');
                var root = bk.GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
                Transform o = root ? root.transform : null;
                for (int i = 1; i < parts.Length && o; i++) o = o.Find(parts[i]);
                var cur = Find(path);
                if (!o || !cur) continue;
                var a = (RectTransform)cur; var b = (RectTransform)o;
                a.anchorMin = b.anchorMin; a.anchorMax = b.anchorMax; a.pivot = b.pivot;
                a.sizeDelta = b.sizeDelta; a.anchoredPosition3D = b.anchoredPosition3D;
                a.localRotation = b.localRotation; a.localScale = b.localScale;
                var ta = cur.GetComponent<TMP_Text>(); var tb = o.GetComponent<TMP_Text>();
                if (ta && tb) { ta.alignment = tb.alignment; ta.fontStyle = tb.fontStyle; ta.fontSize = tb.fontSize; }
                Debug.Log("[FLUJO] Caja de texto restaurada: " + path);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(bk, true);
            AssetDatabase.DeleteAsset(tmpPath);
            EditorSceneManager.SetActiveScene(active);
        }
    }

    // Coloca los botones en una columna, espaciados según lo que realmente se ve de cada uno.
    static void Relayout(List<Transform> originales, List<Transform> enOrden)
    {
        var parent = enOrden[0].parent;
        Bounds Vis(Transform t)
        {
            bool first = true; var bb = new Bounds();
            void Add(Vector3 w) { var l = parent.InverseTransformPoint(w); if (first) { bb = new Bounds(l, Vector3.zero); first = false; } else bb.Encapsulate(l); }
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { var e = r.bounds; Add(e.min); Add(e.max); }
            var cs = new Vector3[4];
            foreach (var g in t.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) { g.rectTransform.GetWorldCorners(cs); foreach (var c in cs) Add(c); }
            return bb;
        }
        var refB = Vis(enOrden[0]);
        float h = enOrden.Max(t => Vis(t).size.y);
        float step = h * 1.12f;
        float top = enOrden.Max(t => Vis(t).center.y);
        for (int i = 0; i < enOrden.Count; i++)
        {
            var t = enOrden[i];
            var vb = Vis(t);
            Vector3 offset = vb.center - t.localPosition;
            var target = new Vector3(refB.center.x, top - i * step, refB.center.z);
            t.localPosition = target - offset;
            t.SetSiblingIndex(i);
            Debug.Log($"[FLUJO] {t.name}: visual h={vb.size.y:0.000} → y={target.y:0.000}");
        }
        Debug.Log($"[FLUJO] Botones en columna, paso {step:0.000}");
    }

    static void Screenshots(Transform btns, Transform panelAnt)
    {
        string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads", "Fotos_Maria");
        Directory.CreateDirectory(dir);
        ShaderUtil.allowAsyncCompilation = false;
        var camGo = new GameObject("__cam"); var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.005f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .2f, .25f);
        var rt = new RenderTexture(900, 900, 24); cam.targetTexture = rt;
        void Shot(string file, Transform target, float dist, bool flip)
        {
            var wasActive = new List<GameObject>();
            for (var t = target; t; t = t.parent) if (!t.gameObject.activeSelf) { wasActive.Add(t.gameObject); t.gameObject.SetActive(true); }
            var b = new Bounds(target.position, Vector3.zero);
            foreach (var r in target.GetComponentsInChildren<Renderer>().Where(r => r.enabled)) b.Encapsulate(r.bounds);
            var corners = new Vector3[4];
            foreach (var rtf in target.GetComponentsInChildren<RectTransform>())
            {
                if (!rtf.GetComponent<UnityEngine.UI.Graphic>() && !rtf.GetComponent<TMP_Text>()) continue;
                rtf.GetWorldCorners(corners); foreach (var c in corners) b.Encapsulate(c);
            }
            dist = Mathf.Max(dist, b.extents.magnitude * 2.2f);
            var n = target.forward * (flip ? -1 : 1);
            cam.transform.position = b.center + n * dist; cam.transform.LookAt(b.center, target.up);
            cam.fieldOfView = 2f * Mathf.Atan(b.extents.magnitude * 1.3f / dist) * Mathf.Rad2Deg;
            var layers = target.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
            foreach (var t in layers.Keys) t.gameObject.layer = 31;
            cam.cullingMask = 1 << 31;
            cam.Render(); cam.Render(); RenderTexture.active = rt;
            foreach (var kv in layers) kv.Key.gameObject.layer = kv.Value;
            var tex = new Texture2D(900, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG()); RenderTexture.active = null;
            foreach (var g in wasActive) g.SetActive(false);
        }
        try
        {
            Shot("tablet_menu.png", btns, 0.35f, true);
            Shot("tablet_opciones.png", btns.parent.Find("Btns Opciones"), 0.35f, true);
            foreach (var p in new[] { "Panel_DatosPaciente", "Panel_Antecedentes", "Panel_FuncionesVitales", "Panel_ExamenFisico", "Panel_ExamenLaboratorio" })
                Shot("panel_" + p.Substring(6) + ".png", Find(p), 1.5f, true);
        }
        catch (System.Exception e) { Debug.LogWarning("[FLUJO] fotos: " + e.Message); }
        finally { cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); }
    }
}
