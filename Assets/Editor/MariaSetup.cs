using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CrazyMinnow.SALSA;
using CrazyMinnow.SALSA.OneClicks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sienta a la paciente María Rodríguez (MPFB) en el lugar de la paciente anterior (silla del consultorio),
/// configura SALSA (visemas por blendshapes), PacienteVida (parpadeo, mirada, respiración)
/// y le pasa la voz de la paciente (DialogflowIntentClientEscenario1).
/// Menú: Herramientas > Paciente > Configurar María
/// </summary>
public class MariaSetup : OneClickBase
{
    const string ScenePath = "Assets/Scenes/Escenario1.unity";
    const string MariaFbx = "Assets/Archivos_nueva_escena/Maria/Maria.fbx";
    const string MatDir = "Assets/Archivos_nueva_escena/Maria/Materiales";
    const string TexDir = "Assets/Archivos_nueva_escena/Maria/Texturas";
    const string InstanceName = "Paciente_Maria";

    [MenuItem("Herramientas/Paciente/Configurar María")]
    static void Menu() { if (Configure()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        bool ok = false;
        try { ok = Configure(); }
        catch (System.Exception e) { Debug.LogError("[MARIA] " + e); }
        if (ok) EditorSceneManager.SaveScene(scene);
        Debug.Log("[MARIA] Terminado ok=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // ------------------------------------------------------------------
    public static bool Configure()
    {
        PrepareImporter();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MariaFbx);
        if (!prefab) { Debug.LogError("[MARIA] No existe " + MariaFbx); return false; }

        // Quitar una versión anterior
        foreach (var old in FindAll(t => t.name == InstanceName && t.parent == null)) Object.DestroyImmediate(old.gameObject);

        var maria = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        maria.name = InstanceName;
        var M = Bones(maria.transform, @"^mixamorig:");

        // ---- Paciente anterior (Meshy): define dónde y hacia dónde se sienta María ----
        var client = Object.FindObjectOfType<DialogflowIntentClientEscenario1>(true);
        SerializedObject so = client ? new SerializedObject(client) : null;
        var oldAudio = so?.FindProperty("patientAudio").objectReferenceValue as AudioSource;
        var oldPatient = FindAll(t => t.parent == null && t.name.StartsWith("Meshy_AI_Graceful")).FirstOrDefault();
        if (!oldPatient && oldAudio) oldPatient = oldAudio.transform;
        if (!oldPatient) { Debug.LogError("[MARIA] No encontré la paciente anterior para tomar su lugar."); return false; }
        var oldBounds = WorldBounds(oldPatient);
        Vector3 F = Vector3.ProjectOnPlane(oldPatient.forward, Vector3.up).normalized;

        // ---- Pose sentada ----
        PoseSeated(maria.transform, M, F);
        PlaceLike(maria, oldBounds, F, M);

        // ---- Materiales ----
        SetupMaterials(maria);

        // ---- Voz ----
        var head = M["Head"];
        var vozGo = new GameObject("VozPaciente");
        vozGo.transform.SetParent(head, false);
        Vector3 eyesMid = (M["LeftEye"].position + M["RightEye"].position) * 0.5f;
        vozGo.transform.position = eyesMid - (head.position - M["Neck"].position).normalized * 0.07f;
        var voz = vozGo.AddComponent<AudioSource>();
        if (oldAudio) EditorUtility.CopySerialized(oldAudio, voz);
        voz.playOnAwake = false; voz.loop = false;
        if (so != null) { so.FindProperty("patientAudio").objectReferenceValue = voz; so.ApplyModifiedPropertiesWithoutUndo(); }
        // Se oculta (no se desactiva): en ese objeto vive DialogflowIntentClientEscenario1, que debe seguir activo.
        foreach (var r in oldPatient.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in oldPatient.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        Debug.Log("[MARIA] Paciente anterior oculta (sigue activa): " + oldPatient.name);

        // ---- SALSA ----
        SetupSalsa(maria, voz);

        // ---- Vida (parpadeo, mirada, respiración) ----
        var vida = maria.AddComponent<PacienteVida>();
        vida.head = M["Head"]; vida.neck = M["Neck"]; vida.chest = M["Spine2"];
        vida.eyeL = M["LeftEye"]; vida.eyeR = M["RightEye"];

        Debug.Log($"[MARIA] Lista. pos={maria.transform.position} altura≈{HeightOf(M):0.00} m");
        Screenshots(maria, M);
        return true;
    }

    // ------------------------------------------------------------------
    static void PrepareImporter()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(MariaFbx);
        if (!imp) return;
        bool dirty = false;
        if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; dirty = true; }
        if (!imp.importBlendShapes) { imp.importBlendShapes = true; dirty = true; }
        if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
        // recalcular normales: las exportadas desde MPFB salen invertidas en media cara
        if (imp.importNormals != ModelImporterNormals.Calculate) { imp.importNormals = ModelImporterNormals.Calculate; imp.normalSmoothingAngle = 60f; dirty = true; }
        if (imp.importBlendShapeNormals != ModelImporterNormals.Calculate) { imp.importBlendShapeNormals = ModelImporterNormals.Calculate; dirty = true; }
        if (dirty) imp.SaveAndReimport();
        Directory.CreateDirectory(TexDir);
        if (!Directory.EnumerateFiles(TexDir, "*.png").Any())
        {
            imp.ExtractTextures(TexDir);
            AssetDatabase.Refresh();
        }
    }

    static IEnumerable<Transform> FindAll(System.Func<Transform, bool> pred) =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(pred).ToList();

    static Dictionary<string, Transform> Bones(Transform root, string prefixRegex)
    {
        var d = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string k = Regex.Replace(Regex.Replace(t.name, prefixRegex, ""), @"_\d+$", "");
            if (!d.ContainsKey(k)) d[k] = t;
        }
        return d;
    }

    static Quaternion Frame(Vector3 primary, Vector3 secondary) =>
        Quaternion.LookRotation(primary.normalized, Vector3.Cross(primary, secondary).normalized);

    static void Aim(Transform bone, Vector3 from, Vector3 to)
    {
        if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
    }

    // Pose sentada en silla: muslos horizontales, piernas verticales, manos sobre el regazo.
    static void PoseSeated(Transform root, Dictionary<string, Transform> M, Vector3 F)
    {
        Vector3 up = Vector3.up, right = Vector3.Cross(up, F).normalized;
        Vector3 P(string k) => M[k].position;

        // 1) Cadera mirando hacia F
        var fm = Frame(P("Spine") - P("Hips"), P("RightUpLeg") - P("LeftUpLeg"));
        var ft = Frame(up, right);
        root.rotation = ft * Quaternion.Inverse(fm) * root.rotation;

        // 2) Columna ligeramente reclinada, cabeza un poco inclinada hacia adelante (cansada)
        Vector3 back = (up * 0.99f - F * 0.10f).normalized;
        foreach (var c in new[] { new[] { "Hips", "Spine" }, new[] { "Spine", "Spine1" }, new[] { "Spine1", "Spine2" }, new[] { "Spine2", "Neck" } })
            Aim(M[c[0]], P(c[1]) - P(c[0]), back);
        Aim(M["Neck"], P("Head") - P("Neck"), (up + F * 0.12f).normalized);
        // cabeza: mirada al frente y algo hacia abajo
        Vector3 mm = (P("LeftEye") + P("RightEye")) / 2;
        Vector3 latM = (P("RightEye") - P("LeftEye")).normalized;
        Vector3 upM = Vector3.ProjectOnPlane(P("Head") - P("Neck"), latM);
        Vector3 fwdM = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(mm - P("Head"), upM), latM).normalized;
        var hm = Frame(fwdM, latM);
        Vector3 gaze = (F - up * 0.30f).normalized;
        var ht = Frame(gaze, right);
        // conservar la relación ojos/cabeza del modelo: alinear marco medido con el objetivo
        M["Head"].rotation = ht * Quaternion.Inverse(hm) * M["Head"].rotation;

        foreach (var s in new[] { "Left", "Right" })
        {
            float side = s == "Left" ? -1f : 1f;
            Vector3 lat = right * side;
            // Piernas
            Aim(M[s + "UpLeg"], P(s + "Leg") - P(s + "UpLeg"), (F - up * 0.08f + lat * 0.08f).normalized);
            Aim(M[s + "Leg"], P(s + "Foot") - P(s + "Leg"), (-up + F * 0.12f).normalized);
            if (M.ContainsKey(s + "ToeBase"))
                Aim(M[s + "Foot"], P(s + "ToeBase") - P(s + "Foot"), (F - up * 0.35f).normalized);
            // Brazos: hombro hacia abajo, antebrazo al regazo
            Aim(M[s + "Arm"], P(s + "ForeArm") - P(s + "Arm"), (-up * 0.88f + F * 0.30f + lat * 0.10f).normalized);
            Aim(M[s + "ForeArm"], P(s + "Hand") - P(s + "ForeArm"), (F * 0.90f - up * 0.25f - lat * 0.30f).normalized);
            var mid = M[s + "Hand"].GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("Middle1"));
            if (mid) Aim(M[s + "Hand"], mid.position - P(s + "Hand"), (F * 0.75f - up * 0.55f - lat * 0.25f).normalized);
        }
    }

    // Límites en mundo calculados con los vértices (funciona aunque el objeto esté desactivado).
    static Bounds WorldBounds(Transform t)
    {
        bool first = true; var b = new Bounds();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = m.MultiplyPoint3x4(v);
                if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
            }
        }
        Debug.Log($"[MARIA] Paciente anterior: centro={b.center} tamaño={b.size}");
        return b;
    }

    // Ubica a María donde estaba la paciente anterior: misma base (piso), mismo frente, mismo centro lateral.
    static void PlaceLike(GameObject maria, Bounds old, Vector3 F, Dictionary<string, Transform> M)
    {
        Vector3 right = Vector3.Cross(Vector3.up, F).normalized;
        // Medidas con huesos (más confiables que la malla en modo edición)
        float soleY = Mathf.Min(M["LeftFoot"].position.y, M["RightFoot"].position.y) - 0.075f;
        float rear = Vector3.Dot(M["Hips"].position, F) - 0.14f;         // glúteos
        float latMid = Vector3.Dot(M["Hips"].position, right);

        var oc = new[] { old.min, old.max };
        float oldRear = Mathf.Min(Vector3.Dot(old.min, F), Vector3.Dot(old.max, F),
                                  Vector3.Dot(new Vector3(old.min.x, 0, old.max.z), F), Vector3.Dot(new Vector3(old.max.x, 0, old.min.z), F));
        float oldLat = Vector3.Dot(old.center, right);

        Vector3 d = Vector3.up * (old.min.y - soleY) + F * (oldRear + 0.04f - rear) + right * (oldLat - latMid);
        maria.transform.position += d;
        float top = M["Head"].position.y + 0.17f;
        Debug.Log($"[MARIA] Colocada: desplazamiento={d} altura sentada≈{top - old.min.y:0.00} m (anterior {old.size.y:0.00})");
    }

    static Transform Get(Dictionary<string, Transform> d, string keys)
    {
        foreach (var k in keys.Split('|')) if (d.TryGetValue(k, out var t)) return t;
        return null;
    }

    static float HeightOf(Dictionary<string, Transform> M) =>
        Vector3.Distance(M["Head"].position, M["Hips"].position) + Vector3.Distance(M["Hips"].position, M["LeftUpLeg"].position) +
        Vector3.Distance(M["LeftUpLeg"].position, M["LeftLeg"].position) + Vector3.Distance(M["LeftLeg"].position, M["LeftFoot"].position);

    // ------------------------------------------------------------------
    static void SetupMaterials(GameObject maria)
    {
        Directory.CreateDirectory(MatDir);
        var made = new Dictionary<Material, Material>();
        foreach (var r in maria.GetComponentsInChildren<Renderer>(true))
        {
            string rn = r.name.ToLower();
            bool hair = rn.Contains("hair") || rn.Contains("bob") || rn.Contains("short") || rn.Contains("ponytail") || rn.Contains("braid");
            bool cutout = hair || rn.Contains("eyebrow") || rn.Contains("eyelash");
            bool clothes = rn.Contains("suit") || rn.Contains("dress") || rn.Contains("shirt") || rn.Contains("pants") || rn.Contains("gown");
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (!src) continue;
                if (!made.TryGetValue(src, out var m))
                {
                    m = new Material(src) { name = src.name };
                    var tex = TextureFor(rn + " " + src.name.ToLower());
                    if (tex) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
                    if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
                    m.DisableKeyword("_NORMALMAP");
                    if (hair && tex)
                    {
                        var gray = MakeGrayHair(tex);
                        if (gray) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", gray); m.mainTexture = gray; }
                    }
                    if (hair && m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
                    if (clothes || rn.Contains("shoes"))
                    {
                        // pijama de hospital celeste liso (sin el estampado original); zapatos claros
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
                        m.mainTexture = null;
                        if (m.HasProperty("_BaseColor"))
                            m.SetColor("_BaseColor", clothes ? new Color(0.70f, 0.84f, 0.88f) : new Color(0.85f, 0.85f, 0.82f));
                    }
                    bool eyes = rn.Contains("high-poly") || rn.Contains("low-poly");
                    if (eyes) cutout = true; // la córnea es transparente en la textura
                    if (eyes || !cutout)
                    {
                        // opaco (el material importado de los ojos venía transparente)
                        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        m.SetOverrideTag("RenderType", "Opaque");
                        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
                        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 1f);
                        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 0f);
                        if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c); }
                        m.renderQueue = 2000;
                    }
                    if (cutout)
                    {
                        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
                        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                        m.renderQueue = 2450;
                    }
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", hair ? 0.25f : 0.3f);
                    // relleno suave: la escena tiene poca luz ambiental y la cara quedaba muy oscura
                    var baseTex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetTexture("_EmissionMap", baseTex);
                        var bc = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                        m.SetColor("_EmissionColor", bc * (rn.Contains("high-poly") ? 0.04f : 0.28f));
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    string path = $"{MatDir}/{Sanitize(src.name)}.mat";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(m, path);
                    made[src] = m;
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
            r.receiveShadows = false; // evitar que la sombra del monitor le corte la cara por la mitad
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; // evita que ojos/cejas se recorten
        }
        AssetDatabase.SaveAssets();
    }

    // Texturas de MPFB extraídas del FBX, elegidas por el nombre de la malla/material.
    static Texture2D TextureFor(string n)
    {
        string f = n.Contains("teeth") ? "teeth" :
                   n.Contains("tongue") ? "tongue01_diffuse" :
                   n.Contains("eyebrow") ? "eyebrow001" :
                   n.Contains("eyelash") ? "eyelashes01" :
                   n.Contains("high-poly") || n.Contains("eye") ? "brown_eye" :
                   n.Contains("ponytail") || n.Contains("hair") ? "ponytail01_diffuse" :
                   n.Contains("shoes") ? "shoes01_diffuse" :
                   n.Contains("suit") ? "female_casualsuit01_diffuse" :
                   n.Contains("body") || n.Contains("cuerpo") ? "old_lightskinned_female_diffuse" : null;
        if (f == null) return null;
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{f}.png");
        if (!t) Debug.LogWarning("[MARIA] Falta textura " + f);
        return t;
    }

    static string Sanitize(string s) => Regex.Replace(s, @"[^\w\-]+", "_");

    static Texture2D MakeGrayHair(Texture tex)
    {
        string src = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(src) || !File.Exists(src)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!t.LoadImage(File.ReadAllBytes(src))) return null;
        var px = t.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            float l = (0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f;
            float v = Mathf.Clamp01(0.55f + l * 0.6f);        // canoso: claro y desaturado
            byte g = (byte)(v * 255f);
            px[i] = new Color32(g, g, (byte)Mathf.Min(255, g + 4), px[i].a);
        }
        t.SetPixels32(px); t.Apply();
        string dst = $"{TexDir}/pelo_canoso.png";
        File.WriteAllBytes(dst, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(dst);
        var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
        imp.alphaIsTransparency = true; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }

    // ------------------------------------------------------------------
    static void SetupSalsa(GameObject go, AudioSource voz)
    {
        Init();
        NewConfiguration(OneClickConfiguration.ConfigType.Salsa);
        AddSmrSearch("^.*$");
        autoAdjustAnalysis = true; autoAdjustMicrophone = false;
        loCutoff = 0.015f; hiCutoff = 0.75f;
        useAdvDyn = true; advDynPrimaryBias = 0.5f;
        useAdvDynJitter = true; advDynJitterAmount = 0.10f; advDynJitterProb = 0.20f; advDynSecondaryMix = 0f;
        emphasizerTrigger = 0.2f;

        // de menor a mayor apertura de boca
        var visemes = new[] { ("w", "viseme_U"), ("f", "viseme_FF"), ("t", "viseme_TH"), ("ee", "viseme_E"), ("oh", "viseme_O"), ("aa", "viseme_aa") };
        foreach (var (name, shape) in visemes)
        {
            NewExpression(name);
            AddShapeComponent(new[] { shape }, 0.08f, 0f, 0.06f, shape, 1f, false);
        }

        NewConfiguration(OneClickConfiguration.ConfigType.Emoter);
        AddSmrSearch("^.*$");
        useRandomEmotes = true; isChancePerEmote = true; numRandomEmotesPerCycle = 1;
        randomEmoteMinTimer = 2f; randomEmoteMaxTimer = 5f; randomChance = 0.5f;
        useRandomFrac = true; randomFracBias = 0.4f;
        useRandomHoldDuration = true; randomHoldDurationMin = 0.3f; randomHoldDurationMax = 1.2f;
        // gestos de paciente cansada / preocupada
        NewExpression("cejasPreocupada"); AddEmoteFlags(true, true, false, 0.6f);
        AddShapeComponent(new[] { "browInnerUp" }, 0.4f, 0.3f, 0.4f, "browInnerUp", 1f, false);
        NewExpression("ojosCansados"); AddEmoteFlags(true, false, false, 0.5f);
        AddShapeComponent(new[] { "eyeSquintLeft" }, 0.5f, 0.5f, 0.5f, "eyeSquintLeft", 1f, false);
        AddShapeComponent(new[] { "eyeSquintRight" }, 0.5f, 0.5f, 0.5f, "eyeSquintRight", 1f, false);
        NewExpression("bocaTriste"); AddEmoteFlags(true, true, false, 0.4f);
        AddShapeComponent(new[] { "mouthFrownLeft" }, 0.4f, 0.3f, 0.4f, "mouthFrownLeft", 1f, false);
        AddShapeComponent(new[] { "mouthFrownRight" }, 0.4f, 0.3f, 0.4f, "mouthFrownRight", 1f, false);

        DoOneClickiness(go);
        var salsa = go.GetComponent<Salsa>();
        salsa.audioSrc = voz;
        salsa.useExternalAnalysis = false;
        AddQueueProcessor(go);
        Debug.Log($"[MARIA] SALSA visemas={salsa.visemes.Count} emotes={go.GetComponent<Emoter>()?.emotes.Count}");
    }

    // ------------------------------------------------------------------
    static void Screenshots(GameObject maria, Dictionary<string, Transform> M)
    {
        string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads", "Fotos_Maria");
        Directory.CreateDirectory(dir);
        var head = M["Head"];
        Vector3 mid = (M["LeftEye"].position + M["RightEye"].position) / 2;
        Vector3 up = (head.position - M["Neck"].position).normalized;
        Vector3 lat = (M["RightEye"].position - M["LeftEye"].position).normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(mid - head.position, up), lat).normalized;

        var camGo = new GameObject("__cam"); var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.01f;
        var lightGo = new GameObject("__light"); var li = lightGo.AddComponent<Light>();
        li.type = LightType.Point; li.range = 4f; li.intensity = 1.2f;
        var rt = new RenderTexture(768, 768, 24); cam.targetTexture = rt;

        ShaderUtil.allowAsyncCompilation = false;
        var smrs = maria.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        void Shape(string n, float w)
        {
            foreach (var s in smrs)
            {
                int i = s.sharedMesh ? s.sharedMesh.GetBlendShapeIndex(n) : -1;
                if (i < 0) continue;
                if (s.name.Contains("Cuerpo"))
                {
                    var a = new Mesh(); s.SetBlendShapeWeight(i, 0); s.BakeMesh(a);
                    var b = new Mesh(); s.SetBlendShapeWeight(i, 100); s.BakeMesh(b);
                    var va = a.vertices; var vb = b.vertices; float mx = 0;
                    for (int k = 0; k < va.Length; k++) mx = Mathf.Max(mx, (va[k] - vb[k]).magnitude);
                    Debug.Log($"[MARIA] Forma '{n}' en {s.name}: desplazamiento máx = {mx * 100f:0.0} cm");
                    Object.DestroyImmediate(a); Object.DestroyImmediate(b);
                }
                s.SetBlendShapeWeight(i, w);
            }
        }
        // En modo editor las formas no siempre se ven en Camera.Render: se fotografía una copia horneada.
        var baked = new List<GameObject>();
        void BakeForShot()
        {
            foreach (var s in smrs)
            {
                var mesh = new Mesh(); s.BakeMesh(mesh);
                var g = new GameObject("__baked"); g.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterials = s.sharedMaterials; mr.receiveShadows = false;
                s.enabled = false; baked.Add(g);
            }
        }
        void Unbake() { foreach (var g in baked) Object.DestroyImmediate(g); baked.Clear(); foreach (var s in smrs) s.enabled = true; }
        void Shot(string f)
        {
            cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(768, 768, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, f), tex.EncodeToPNG());
            RenderTexture.active = null; Object.DestroyImmediate(tex);
        }
        try
        {
            cam.fieldOfView = 28;
            camGo.transform.position = mid + fwd * 0.55f; camGo.transform.LookAt(mid - up * 0.03f, up);
            lightGo.transform.position = mid + fwd * 0.8f + up * 0.4f;
            Shot("unity_0_descartar.png"); // primer render: calienta shaders
            Shot("unity_1_cara.png");
            var lights = Object.FindObjectsOfType<Light>().Where(l => l != li).ToList();
            var prevShadows = lights.Select(l => l.shadows).ToList();
            foreach (var l in lights) l.shadows = LightShadows.None;
            Shot("unity_1b_sin_sombras.png");
            for (int k = 0; k < lights.Count; k++) lights[k].shadows = prevShadows[k];
            foreach (var l in lights) Debug.Log($"[MARIA] luz {l.name} tipo={l.type} sombras={l.shadows} rot={l.transform.eulerAngles} pos={l.transform.position}");
            Shape("viseme_aa", 100); BakeForShot(); Shot("unity_2_hablando.png"); Unbake(); Shape("viseme_aa", 0);
            Shape("eyeBlinkLeft", 100); Shape("eyeBlinkRight", 100); BakeForShot(); Shot("unity_3_parpadeo.png"); Unbake();
            Shape("eyeBlinkLeft", 0); Shape("eyeBlinkRight", 0);

            cam.fieldOfView = 55;
            Vector3 hips = M["Hips"].position;
            camGo.transform.position = hips + fwd * 2.2f + Vector3.up * 1.0f;
            camGo.transform.LookAt(hips + Vector3.up * 0.1f, Vector3.up);
            lightGo.transform.position = hips + fwd * 1.5f + Vector3.up * 1.5f; li.range = 8f;
            Shot("unity_4_en_sillon.png");
            // Vista lateral aislada (solo María) para revisar la pose
            var others = EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(g => g != maria && g.activeSelf && g != camGo && g != lightGo).ToList();
            foreach (var g in others) g.SetActive(false);
            Vector3 rightW = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(fwd, Vector3.up).normalized);
            Vector3 c = M["Hips"].position + Vector3.up * 0.1f;
            cam.fieldOfView = 50; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.35f, .35f, .4f);
            camGo.transform.position = c + rightW * 2.5f; camGo.transform.LookAt(c, Vector3.up);
            lightGo.transform.position = c + rightW * 2f + Vector3.up; li.range = 6f; li.intensity = 2f;
            Shot("unity_5_pose_lado.png");
            camGo.transform.position = c + Vector3.ProjectOnPlane(fwd, Vector3.up).normalized * 2.5f; camGo.transform.LookAt(c, Vector3.up);
            lightGo.transform.position = camGo.transform.position + Vector3.up;
            Shot("unity_6_pose_frente.png");
            foreach (var g in others) g.SetActive(true);
            Debug.Log("[MARIA] Fotos en " + dir);
        }
        catch (System.Exception e) { Debug.LogWarning("[MARIA] Fotos: " + e.Message); }
        finally
        {
            cam.targetTexture = null; Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo);
        }
    }
}
