using UnityEngine;

/// <summary>
/// Anima un prefab persistente. No crea mallas, materiales, textos ni parantes
/// durante Play. El recipiente y la etiqueta se preparan en el editor.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class VisualSalinaIndependiente : MonoBehaviour
{
    public const string RutaPrefab = "Infusion/NaClReferencia/Parante_NaCl_Referencia";
    public const float DiametroSonda = .005f;

    // El buscador pesado sigue pausado. La presentación usa una guía de
    // coste fijo, validada con la escena, sin capturar ni serializar mallas.
    public static bool CalculoSondaSuspendido => true;

    [SerializeField] private Transform salidaGotero;
    [SerializeField] private Transform puntoAdministracion;
    [SerializeField] private Transform accesoPaciente;
    [SerializeField] private Transform gota;
    [SerializeField] private Transform inicioGota;
    [SerializeField] private Transform finGota;
    [SerializeField] private LineRenderer sonda;
    [SerializeField] private Transform conectorPaciente;
    [SerializeField] private Renderer cintaAcceso;
    [SerializeField] private Vector3 centroCintaRelativoAMano;
    [SerializeField] private Quaternion rotacionCintaRelativaAMano = Quaternion.identity;
    [SerializeField] private bool cintaFijada;
    [SerializeField] private int versionEscala;
    [SerializeField] private int versionModelo;
    [SerializeField, HideInInspector] private GeometriaSondaNaCl geometriaCamilla;
    private Transform soporteRegulador;

    public Transform PuntoAdministracionNaCl => puntoAdministracion;
    public Transform SalidaGotero => salidaGotero;
    public Transform AccesoPaciente => accesoPaciente;
    public Renderer CintaAcceso => cintaAcceso;
    public bool CintaFijada => cintaFijada;
    public bool Fluyendo => fluyendo;
    public int VersionModelo => versionModelo;
    public void MarcarModeloV4() => versionModelo = 4;
    public void MarcarModeloV5() => versionModelo = 5;
    public bool RutaLibre => rutaLibre;
    public Vector3 PuntoAccesoVisual => cintaFijada && cintaAcceso != null
        ? cintaAcceso.bounds.center : accesoPaciente != null ? accesoPaciente.position : Vector3.zero;
    public Vector3 InicioSonda => sonda != null && sonda.positionCount > 0
        ? sonda.GetPosition(0) : Vector3.zero;
    public Vector3 FinSonda => sonda != null && sonda.positionCount > 0
        ? sonda.GetPosition(sonda.positionCount - 1) : Vector3.zero;

    private bool fluyendo;
    private float faseGota;
    private bool grosorConfigurado, rutaLibre, avisoRuta;
    private float proximaRuta;
    private Vector3 ultimoInicio, ultimoFin;
    private readonly Vector3[] puntosGuiados = new Vector3[RutaGuiadaNaCl.CantidadPuntos];
    private readonly Collider[] contactosGuiados = new Collider[32];
    private Transform marcoCamilla, raizPaciente;

    // Sustituye el +20 % antiguo por la altura REAL del soporte sanguíneo.
    // V5 recupera la cercanía al paciente y sitúa la bolsa delante de la pantalla.
    public bool AplicarEscalaReferencia(Transform paranteSangre = null)
    {
        if (versionEscala >= 3 || versionModelo < 5) return false;
        Vector3 posicion, escala;
        if (!UbicacionNaClReferencia.CalcularPresentacion(this, paranteSangre,
            out posicion,out escala)) return false;
        transform.position = posicion;
        transform.localScale = escala;
        versionEscala = 3;
        ActualizarSonda();
        return true;
    }

    // Al crear el prefab se serializan estas referencias.
    public void Configurar(Transform salida, Transform administracion,
        Transform visualGota, Transform inicio, Transform fin,
        LineRenderer linea, Transform conector)
    {
        salidaGotero = salida;
        puntoAdministracion = administracion;
        gota = visualGota;
        inicioGota = inicio;
        finGota = fin;
        sonda = linea;
        conectorPaciente = conector;
    }

    public void ConectarPaciente(Transform acceso)
    {
        if (accesoPaciente != acceso)
        {
            cintaAcceso = null;
            cintaFijada = false;
        }
        accesoPaciente = acceso;
        ActualizarSonda();
    }

    /// <summary>
    /// Traslada la cinta del antebrazo a 2 cm de la muñeca, conservando su
    /// separación radial de la piel. No utiliza el pivote del FBX como centro.
    /// </summary>
    public bool FijarCintaEnMuneca(Renderer cinta)
    {
        if (cinta == null || accesoPaciente == null || accesoPaciente.parent == null ||
            (cintaFijada && cintaAcceso == cinta)) return false;
        Vector3 eje = accesoPaciente.position - accesoPaciente.parent.position;
        if (eje.sqrMagnitude < 0.0001f) return false;
        eje.Normalize();
        Vector3 centro = cinta.bounds.center;
        Vector3 objetivoLongitudinal = accesoPaciente.position - eje * 0.02f;
        Vector3 nuevoCentro = centro + eje * Vector3.Dot(objetivoLongitudinal - centro, eje);
        cintaAcceso = cinta;
        centroCintaRelativoAMano = accesoPaciente.InverseTransformPoint(nuevoCentro);
        rotacionCintaRelativaAMano = Quaternion.Inverse(accesoPaciente.rotation) * cinta.transform.rotation;
        cintaFijada = true;
        ActualizarSonda();
        return true;
    }

    public void EstablecerUso(bool activo)
    {
        fluyendo = activo;
        if (!activo) faseGota = 0f;
        if (gota != null) gota.gameObject.SetActive(Application.isPlaying && activo);
        // La presencia del equipo NO depende del flujo. F5/F7 mantienen el
        // soporte, recipiente y sonda visibles; solo la gota se activa con NaCl.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        grosorConfigurado = false;
        proximaRuta = 0f;
        if (gota != null) gota.gameObject.SetActive(Application.isPlaying && fluyendo);
        ActualizarSonda();
    }

    private void Update()
    {
        if (!Application.isPlaying || !fluyendo || gota == null ||
            inicioGota == null || finGota == null) return;
        // Indicador visual; no representa una prescripción de caudal.
        faseGota = Mathf.Repeat(faseGota + Time.deltaTime * 1.5f, 1f);
        gota.position = Vector3.Lerp(inicioGota.position, finGota.position, faseGota);
    }

    private void LateUpdate() => ActualizarSonda();
    public void RefrescarSonda() { proximaRuta = 0f; ActualizarSonda(); }

#if UNITY_EDITOR
    public void PrepararGeometriaCamilla(bool forzar = false)
    {
        if (CalculoSondaSuspendido) return;
        var escena = gameObject.scene;
        if (!escena.IsValid() || !escena.isLoaded) return;
        if (!forzar && geometriaCamilla != null && geometriaCamilla.Vigente) return;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (MeshFilter filtro in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            // Corrección acotada a la pieza compuesta señalada por la consola.
            // No excluir Rectangle05 ni modificar ningún objeto de la camilla.
            if (filtro.name != "Rectangle05" || filtro.transform.parent == null ||
                filtro.transform.parent.name != "Camilla" || filtro.sharedMesh == null) continue;
            if (!forzar && geometriaCamilla != null && geometriaCamilla.Corresponde(filtro)) return;
            geometriaCamilla = GeometriaSondaNaCl.Capturar(filtro);
            UnityEditor.EditorUtility.SetDirty(this);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            proximaRuta = 0f;
            return;
        }
    }
#endif

    private void ActualizarSonda()
    {
        // No invocar RutaSondaNaCl.Calcular, capturar geometría, lanzar pruebas
        // ni registrar miles de overrides del prefab desde ExecuteAlways.
        var escenaActual = gameObject.scene;
        if (!escenaActual.IsValid() || !escenaActual.isLoaded) return;
        if (sonda == null || salidaGotero == null) return;
        if (accesoPaciente == null) return; // Prefab aislado, sin paciente.
        ActualizarCinta(); // Mantener el anclaje ya guardado en la muñeca.
        Vector3 inicio=salidaGotero.position, fin=PuntoAccesoVisual;
        bool movida=(inicio-ultimoInicio).sqrMagnitude>.000001f || (fin-ultimoFin).sqrMagnitude>.000001f;
        float ahora=Time.realtimeSinceStartup; // También avanza fuera de Play.
        if (grosorConfigurado && !movida && ahora<proximaRuta) return;
        proximaRuta=ahora+.5f;
        bool actualizarPuntos=!grosorConfigurado || movida || !rutaLibre;
        if (!grosorConfigurado)
        {
            sonda.widthCurve = AnimationCurve.Constant(0f,1f,1f);
            sonda.widthMultiplier = DiametroSonda;
            sonda.useWorldSpace = true;
            sonda.alignment = LineAlignment.View;
            sonda.generateLightingData = true;
            sonda.numCapVertices = 6;
            sonda.numCornerVertices = 8;
            grosorConfigurado = true;
        }
        if (raizPaciente == null)
        {
            Transform candidato=accesoPaciente;
            while (candidato != null && candidato.name != "Paciente") candidato=candidato.parent;
            raizPaciente=candidato;
        }
        if (marcoCamilla == null && raizPaciente != null && raizPaciente.parent != null)
            marcoCamilla=raizPaciente.parent.Find("Camilla");
        Vector3 centroRegulador;
        RutaGuiadaNaCl.Generar(inicio,fin,puntosGuiados,out centroRegulador);
        rutaLibre=RutaGuiadaNaCl.FueraDeCamilla(puntosGuiados,marcoCamilla,DiametroSonda*.5f);
        string bloqueo="fuera de la región de camilla verificada";
        if (rutaLibre) rutaLibre=ComprobarFisicaGuiada(out bloqueo);
        if (!rutaLibre && !avisoRuta)
        {
            Debug.LogWarning("[NaCl] Guía de sonda bloqueada por "+bloqueo+
                ". No se ejecutará un buscador automático ni se atravesará el obstáculo.",this);
            avisoRuta=true;
        }
        if (rutaLibre) avisoRuta=false;
        if (sonda.enabled != rutaLibre) sonda.enabled=rutaLibre;
        if (rutaLibre && (actualizarPuntos || sonda.positionCount != puntosGuiados.Length))
        {
            sonda.positionCount=puntosGuiados.Length;
            sonda.SetPositions(puntosGuiados);
        }
        ultimoInicio=inicio; ultimoFin=fin;
        if (soporteRegulador == null) soporteRegulador = transform.Find("Regulador_NaCl");
        if (soporteRegulador != null)
        {
            if ((soporteRegulador.position-centroRegulador).sqrMagnitude>1e-10f)
                soporteRegulador.position=centroRegulador;
            if (Quaternion.Angle(soporteRegulador.rotation,transform.rotation)>.001f)
                soporteRegulador.rotation=transform.rotation;
            Vector3 escalaRaiz = transform.lossyScale;
            Vector3 escala=new Vector3(1f/Mathf.Max(.0001f,Mathf.Abs(escalaRaiz.x)),
                1f/Mathf.Max(.0001f,Mathf.Abs(escalaRaiz.y)),1f/Mathf.Max(.0001f,Mathf.Abs(escalaRaiz.z)));
            if ((soporteRegulador.localScale-escala).sqrMagnitude>1e-10f) soporteRegulador.localScale=escala;
        }
        if (conectorPaciente != null && rutaLibre)
        {
            if (!conectorPaciente.gameObject.activeSelf) conectorPaciente.gameObject.SetActive(true);
            if ((conectorPaciente.position-fin).sqrMagnitude>1e-10f) conectorPaciente.position=fin;
            Quaternion rotacion=Quaternion.FromToRotation(Vector3.up,puntosGuiados[puntosGuiados.Length-2]-fin);
            if (Quaternion.Angle(conectorPaciente.rotation,rotacion)>.001f) conectorPaciente.rotation=rotacion;
        }
    }

    private bool ComprobarFisicaGuiada(out string bloqueo)
    {
        Physics.SyncTransforms();
        for (int i=1;i<puntosGuiados.Length;i++)
        {
            int cantidad=Physics.OverlapCapsuleNonAlloc(puntosGuiados[i-1],puntosGuiados[i],
                DiametroSonda*.5f+.003f,contactosGuiados,~0,QueryTriggerInteraction.Ignore);
            if (cantidad == contactosGuiados.Length) { bloqueo="demasiados contactos físicos"; return false; }
            for (int j=0;j<cantidad;j++)
            {
                Collider contacto=contactosGuiados[j];
                Transform t=contacto.transform;
                if (contacto.gameObject.scene != gameObject.scene || t.IsChildOf(transform) ||
                    (raizPaciente != null && t.IsChildOf(raizPaciente))) continue;
                bool mano=false;
                for (Transform padre=t;padre != null;padre=padre.parent)
                    if (padre.CompareTag("Hand")) { mano=true; break; }
                if (mano) continue; // Dedos de interacción, no mobiliario.
                bloqueo=contacto.name; return false;
            }
        }
        bloqueo=null; return true;
    }

    private void ActualizarCinta()
    {
        if (!cintaFijada || cintaAcceso == null || accesoPaciente == null) return;
        Transform t = cintaAcceso.transform;
        Quaternion rotacion = accesoPaciente.rotation * rotacionCintaRelativaAMano;
        if (Quaternion.Angle(t.rotation,rotacion) > 0.001f) t.rotation = rotacion;
        Vector3 centroDeseado = accesoPaciente.TransformPoint(centroCintaRelativoAMano);
        Vector3 desplazamiento = centroDeseado - cintaAcceso.bounds.center;
        if (desplazamiento.sqrMagnitude > 0.000000001f)
            t.position += desplazamiento;
    }
}
