using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// - Punto de inicio: el usuario aparece sentado frente a María, a la altura de sus ojos.
/// - Monitor y CPU corridos para no tapar la vista.
/// - Consultorio más claro y con color: luz, paredes, cuadros.
/// Menú: Herramientas > Paciente > Arreglar inicio y consultorio
/// </summary>
public static class ConsultorioSetup
{
    const string ScenePath = "Assets/Scenes/Escenario1.unity";
    const string Dir = "Assets/Archivos_nueva_escena/Consultorio";
    const float DistanciaAPaciente = 1.30f;

    [MenuItem("Herramientas/Paciente/Arreglar inicio y consultorio")]
    static void Menu() { if (Apply()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool ok = false;
        try { ok = Apply(); } catch (System.Exception e) { Debug.LogError("[CONS] " + e); }
        if (ok) EditorSceneManager.SaveScene(scene);
        Debug.Log("[CONS] Terminado ok=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static Transform[] All() => EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

    public static bool Apply()
    {
        var all = All();
        var rig = all.FirstOrDefault(t => t.parent == null && t.name.Contains("Camera Rig"));
        var eye = rig ? rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CenterEyeAnchor") : null;
        var maria = all.FirstOrDefault(t => t.parent == null && t.name == "Paciente_Maria");
        var room = all.FirstOrDefault(t => t.parent == null && t.name.StartsWith("consultorio_final"));
        if (!rig || !eye || !maria || !room) { Debug.LogError("[CONS] Falta rig/ojo/María/consultorio"); return false; }

        var head = maria.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Head");
        var eL = maria.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:LeftEye");
        var eR = maria.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:RightEye");
        Vector3 ojosMaria = (eL.position + eR.position) / 2;
        // María mira hacia el escritorio/doctor: su frente horizontal
        Vector3 lat = (eR.position - eL.position).normalized;
        Vector3 frente = Vector3.Cross(lat, Vector3.up).normalized;
        if (Vector3.Dot(frente, ojosMaria - head.position) < 0) frente = -frente;

        // ---------- 1) Punto de inicio ----------
        Vector3 inicio = ojosMaria + frente * DistanciaAPaciente;
        var punto = all.FirstOrDefault(t => t.name == "PuntoInicio_Doctor")?.gameObject ?? new GameObject("PuntoInicio_Doctor");
        punto.transform.SetPositionAndRotation(inicio, Quaternion.LookRotation(-frente, Vector3.up));
        Shot("inicio_antes.png", inicio, ojosMaria);

        // en el editor el rig también queda mirando a la paciente
        rig.rotation = Quaternion.LookRotation(-frente, Vector3.up);
        rig.position += inicio - eye.position;
        // El visor mide la altura desde el piso real (FloorLevel): el rig va al nivel del piso
        // para que, aunque el ajuste tarde, nunca se aparezca dentro del techo.
        rig.position = new Vector3(rig.position.x, 0f, rig.position.z);
        var ini = rig.GetComponent<InicioFrenteAPaciente>();
        if (!ini) ini = rig.gameObject.AddComponent<InicioFrenteAPaciente>();
        ini.ojoCentral = eye; ini.cabezaPaciente = head; ini.puntoInicio = punto.transform;
        // altura fija a la de los ojos (no depender de cómo detecta el piso el visor)
        var ovr = rig.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c && c.GetType().Name == "OVRManager");
        Debug.Log($"[CONS] Inicio en {inicio} (ojos de María {ojosMaria}), frente={frente}");

        // ---------- 2) Monitor y CPU fuera de la línea de visión ----------
        Transform R(string n) => room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        Vector3 lado = Vector3.Cross(Vector3.up, frente).normalized; // perpendicular a la línea de visión
        void Apartar(Transform t, float minDist)
        {
            if (!t) return;
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float d = Vector3.Dot(b.center - ojosMaria, lado);
            float need = minDist + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(lado.x), Mathf.Abs(lado.y), Mathf.Abs(lado.z)));
            if (Mathf.Abs(d) >= need) return;
            float s = d >= 0 ? 1 : -1;
            t.position += lado * s * (need - Mathf.Abs(d));
            Debug.Log($"[CONS] {t.name} apartado {(need - Mathf.Abs(d)):0.00} m");
        }
        Apartar(R("Monitor_27__Curved"), 0.38f);
        Apartar(R("Cube_054_Cube_055"), 0.38f);
        Apartar(R("10105_Computer_Keyboard_v1_L3"), 0.25f);

        // ---------- 3) Luz ----------
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.80f, 0.84f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.72f, 0.68f, 0.63f);
        RenderSettings.ambientGroundColor = new Color(0.40f, 0.37f, 0.34f);
        RenderSettings.ambientIntensity = 1f;
        foreach (var l in Object.FindObjectsOfType<Light>(true).Where(l => l.type == LightType.Directional))
        {
            if (l.name == "Directional Light")
            {
                // desde el lado del doctor y desde arriba: ilumina la cara de María y el fondo
                l.transform.rotation = Quaternion.LookRotation((-frente + Vector3.down * 0.9f + lado * 0.35f).normalized, Vector3.up);
                l.intensity = 1.15f; l.color = new Color(1f, 0.95f, 0.88f);
            }
            else l.intensity = Mathf.Min(l.intensity, 0.5f);
        }

        // ---------- 4) Colores ----------
        Directory.CreateDirectory(Dir);
        Material Tinted(Material src, string name, Color c)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);   // materiales glTF del consultorio
            if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", 0.85f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }
        void Paint(Transform t, string name, Color c)
        {
            if (!t) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i]) mats[i] = Tinted(OriginalOf(mats[i]), name, c);
                r.sharedMaterials = mats;
            }
        }
        var paredes = room.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m && (m.name == "Paredes" || m.name == "pared_menta"))).ToList();
        foreach (var r in paredes) Paint(r.transform, "pared_menta", new Color(0.82f, 0.92f, 0.88f));     // verde menta suave
        Paint(R("pared2"), "pared_acento", new Color(0.66f, 0.82f, 0.94f));                                 // pared del fondo celeste
        Paint(R("techo"), "techo_blanco", new Color(0.98f, 0.97f, 0.94f));
        var techoMat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/techo_blanco.mat");
        if (techoMat && techoMat.HasProperty("_Cull")) { techoMat.SetFloat("_Cull", 0f); techoMat.doubleSidedGI = true; } // visible desde adentro
        Paint(R("Plane001_09 - Default_0"), "piso_madera", new Color(0.80f, 0.68f, 0.54f));                 // piso cálido
        Paint(R("Cube_013_Cube_015"), "escritorio_madera", new Color(0.86f, 0.74f, 0.58f));

        // ---------- 5) Cuadros ----------
        // el paisaje en la pared del fondo (detrás de María, a su costado); las flores en la pared lateral
        Cuadro("Cuadro_Paisaje", "cuadro_paisaje.png", room, 1.05f, ojosMaria + Vector3.up * 0.30f + lado * 0.85f, -frente);
        Cuadro("Cuadro_Flores", "cuadro_flores.png", room, 0.55f, ojosMaria + Vector3.up * 0.30f - frente * 0.6f, -lado);

        // Ojos de María: sin brillo propio para que se vea el iris café
        var ojos = AssetDatabase.LoadAssetAtPath<Material>("Assets/Archivos_nueva_escena/Maria/Materiales/Maria_high-poly.mat");
        if (ojos)
        {
            if (ojos.HasProperty("_EmissionColor")) ojos.SetColor("_EmissionColor", Color.white * 0.04f);
            if (ojos.HasProperty("_BaseColor")) ojos.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f));
            if (ojos.HasProperty("_Smoothness")) ojos.SetFloat("_Smoothness", 0.75f);
            // la córnea (círculo lila de la textura) es transparente: se recorta para ver el iris
            ojos.SetFloat("_AlphaClip", 1f); ojos.SetFloat("_Cutoff", 0.5f); ojos.EnableKeyword("_ALPHATEST_ON");
            ojos.renderQueue = 2450;
            EditorUtility.SetDirty(ojos);
        }

        AssetDatabase.SaveAssets();
        Shot("inicio_despues.png", inicio, ojosMaria);
        Shot("maria_ojos.png", ojosMaria + frente * 0.45f, ojosMaria, 30f);
        Shot("inicio_despues_amplio.png", inicio, ojosMaria, 100f);
        return true;
    }

    // Si ya se pintó antes, volver a partir del material original guardado como referencia.
    static Material OriginalOf(Material m) => m;

    static void Cuadro(string name, string png, Transform room, float ancho, Vector3 origen, Vector3 dir)
    {
        var old = All().FirstOrDefault(t => t.name == name); if (old) Object.DestroyImmediate(old.gameObject);
        string texPath = $"{Dir}/{png}";
        AssetDatabase.ImportAsset(texPath);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (!tex) { Debug.LogWarning("[CONS] falta " + png); return; }

        // buscar la pared con un rayo
        var colliders = new System.Collections.Generic.List<MeshCollider>();
        foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.ToLower().Contains("pared")) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colliders.Add(mc);
        }
        Physics.SyncTransforms();
        bool hit = Physics.Raycast(origen, dir, out var h, 10f);
        string paredNombre = hit ? h.collider.name : "";
        foreach (var c in colliders) Object.DestroyImmediate(c);
        if (!hit) { Debug.LogWarning("[CONS] No encontré pared para " + name); return; }

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.position = h.point + h.normal * 0.015f;
        q.transform.rotation = Quaternion.LookRotation(-h.normal, Vector3.up); // cara visible hacia la habitación
        q.transform.localScale = new Vector3(ancho, ancho * tex.height / tex.width, 1f);
        string matPath = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.1f);
        m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * 0.25f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        q.GetComponent<MeshRenderer>().sharedMaterial = m;
        q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Debug.Log($"[CONS] {name} en {q.transform.position} (pared {paredNombre})");
    }

    static void Shot(string file, Vector3 from, Vector3 to, float fov = 75f)
    {
        string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads", "Fotos_Maria");
        Directory.CreateDirectory(dir);
        ShaderUtil.allowAsyncCompilation = false;
        // ocultar el rig del jugador (manos/controles) durante la foto
        var rigs = EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name.Contains("Camera Rig") && g.activeSelf).ToList();
        foreach (var g in rigs) g.SetActive(false);
        var go = new GameObject("__cam"); var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.02f; cam.fieldOfView = fov;
        go.transform.position = from; go.transform.LookAt(to, Vector3.up);
        var rt = new RenderTexture(1280, 960, 24); cam.targetTexture = rt;
        cam.Render(); cam.Render(); RenderTexture.active = rt;
        var t = new Texture2D(1280, 960, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); t.Apply();
        File.WriteAllBytes(Path.Combine(dir, file), t.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        foreach (var g in rigs) g.SetActive(true);
    }
}
