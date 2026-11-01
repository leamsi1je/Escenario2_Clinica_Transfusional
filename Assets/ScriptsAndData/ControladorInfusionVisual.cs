using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

/// <summary>
/// Solo bolsas/vías: sangre al ingresar, suspensión explícita y luego NaCl.
/// No modifica el monitor ni descubre/conecta botones por su texto.
/// No convierte la mitad izquierda de una malla sanguínea en solución salina.
/// </summary>
[DisallowMultipleComponent]
public class ControladorInfusionVisual : MonoBehaviour
{
    public enum FluidoActivo { Ninguno, Sangre, NaCl }

    [SerializeField] private Transform paranteSangre;
    [SerializeField] private ParticleSystem goteoSangre;
    [SerializeField] private Transform accesoVenosoPaciente;
    [SerializeField] private RecursosInfusion recursos;
    [SerializeField] private VisualSalinaIndependiente salina;
    [SerializeField] private bool iniciarAutomaticamente = true;

    public EstadoInfusion Estado { get; private set; }
    public bool SangreFluyendo => Estado != null && Estado.SangreFluyendo;
    public bool NaClFluyendo => Estado != null && Estado.NaClFluyendo;
    public FluidoActivo FluidoActual => SangreFluyendo ? FluidoActivo.Sangre :
        NaClFluyendo ? FluidoActivo.NaCl : FluidoActivo.Ninguno;
    // El coordinador antiguo no debe abrir/cerrar este regulador por código:
    // la confirmación requiere su evento alMovimientoManual.
    public ControladorReguladorTactil ReguladorSangre => reguladorSangre;
    public VisualSalinaIndependiente VisualSalina => salina;
    [Header("Panel: autorización NO equivale a ejecución")]
    public UnityEvent<string> alSolicitarAccionManual = new UnityEvent<string>();
    public UnityEvent<string> alMostrarInstruccion = new UnityEvent<string>();
    public UnityEvent<string> alCompletarAccionManual = new UnityEvent<string>();
    public string AccionManualPendiente => acciones.Pendiente;

    private bool salinaCreadaDurantePlay;
    private bool preparado;
    private bool avisoReferencias;
    private readonly List<ControladorReguladorTactil> reguladoresPausados =
        new List<ControladorReguladorTactil>();
    private float tasaSangreOriginal;
    private ControladorReguladorTactil reguladorSangre;
    private ControladorReguladorTactil reguladorNaCl;
    private readonly RegistroAccionesManuales acciones = new RegistroAccionesManuales();
    private readonly List<Renderer> tramosSangre = new List<Renderer>();
    private readonly List<Material[]> materialesTramos = new List<Material[]>();
    private bool? viaRoja;

    private void Awake()
    {
        Estado = new EstadoInfusion();
        ResolverReferencias();
    }

    private void Start()
    {
        if (iniciarAutomaticamente && Estado.Actual == EstadoInfusion.Fase.SinIniciar)
            ReiniciarPrueba();
    }

    private void LateUpdate()
    {
        // Un solo propietario de emisión: un regulador/Start heredado no
        // puede reabrir sangre mientras NaCl está activo.
        if (preparado) AplicarEstadoVisual();
    }

    public void ResolverReferencias()
    {
        if (Estado == null) Estado = new EstadoInfusion();
        if (paranteSangre == null)
            paranteSangre = BuscarEnEscena("Parante_sangre");
        if (paranteSangre == null) return;
        if (goteoSangre == null)
        {
            Transform gotero = BuscarHijo(paranteSangre, "Goteo");
            if (gotero != null) goteoSangre = gotero.GetComponent<ParticleSystem>();
        }
        if (accesoVenosoPaciente == null)
            accesoVenosoPaciente = BuscarEnEscena("RightHand_083");
        if (recursos == null)
            recursos = Resources.Load<RecursosInfusion>("Infusion/RecursosInfusion");

        if (recursos == null || recursos.plasticoNaCl == null || recursos.sangre == null ||
            goteoSangre == null || accesoVenosoPaciente == null)
        {
            if (!avisoReferencias)
                Debug.LogError("[Infusión] Faltan recursos, Goteo o RightHand_083; no se iniciará una infusión incompleta.", this);
            avisoReferencias = true;
            return;
        }
        if (preparado) return;

        RestaurarMaterialesSanguineos();
        DesactivarRestosGenerados();
        if (!Application.isPlaying) return;

        foreach (ControladorReguladorTactil regulador in
            FindObjectsOfType<ControladorReguladorTactil>(true))
        {
            if (regulador.gameObject.scene == gameObject.scene &&
                regulador.sistemaGoteo == goteoSangre && regulador.enabled)
            {
                reguladoresPausados.Add(regulador);
                regulador.enabled = false;
            }
        }

        // Se conserva la tasa visual existente; no se inventa una dosis/caudal.
        tasaSangreOriginal = Mathf.Max(0f, goteoSangre.emission.rateOverTime.constantMax);
        if (tasaSangreOriginal <= 0f) tasaSangreOriginal = 2f;

        if (salina == null)
            foreach (VisualSalinaIndependiente candidato in FindObjectsOfType<VisualSalinaIndependiente>(true))
                if (candidato.gameObject.scene == gameObject.scene) { salina = candidato; break; }
        if (salina == null)
        {
            // Para escenas sin instancia guardada: cargar el MISMO prefab.
            // Nunca reconstruir mallas ni textos durante Play.
            GameObject prefab = Resources.Load<GameObject>(VisualSalinaIndependiente.RutaPrefab);
            Vector3 posicion;
            Quaternion rotacion;
            Transform acceso;
            if (prefab == null || !UbicacionNaClReferencia.Calcular(gameObject.scene,
                paranteSangre, out posicion, out rotacion, out acceso))
            {
                Debug.LogError("[Infusión] Falta el prefab NaCl o sus referencias. Abre Escena_nueva fuera de Play y espera la importación.", this);
                return;
            }
            GameObject objeto = Instantiate(prefab, posicion, rotacion);
            SceneManager.MoveGameObjectToScene(objeto, gameObject.scene);
            salina = objeto.GetComponent<VisualSalinaIndependiente>();
            salina.ConectarPaciente(acceso);
            salinaCreadaDurantePlay = true;
        }
        reguladorSangre = paranteSangre.Find("Palanca")?.GetComponent<ControladorReguladorTactil>();
        reguladorNaCl = salina.GetComponentInChildren<ControladorReguladorTactil>(true);
        if (reguladorSangre == null || reguladorNaCl == null)
        {
            Debug.LogError("[Infusión] Faltan los interruptores físicos. Abre Escena_nueva fuera de Play para actualizar el prefab NaCl.", this);
            return;
        }
        reguladorSangre.ConfigurarControlManual(true,
            () => Estado.Pendiente == EstadoInfusion.Accion.CerrarSangre);
        reguladorNaCl.ConfigurarControlManual(false,
            () => Estado.Pendiente == EstadoInfusion.Accion.AbrirNaCl);
        reguladorSangre.alMovimientoManual.AddListener(RegistrarCierreManual);
        reguladorNaCl.alMovimientoManual.AddListener(RegistrarAperturaManual);
        // Reutiliza la notificación VR que ya existe; no crea ni modifica el panel clínico.
        reguladorNaCl.panelNotificacion = reguladorSangre.panelNotificacion;
        reguladorNaCl.textoNotificacion = reguladorSangre.textoNotificacion;
        salina.AplicarEscalaReferencia(paranteSangre);
        preparado = true;
        AplicarEstadoVisual();
    }

    public void AsignarVisualSalina(VisualSalinaIndependiente visual) => salina = visual;

    private void RestaurarMaterialesSanguineos()
    {
        if (recursos.sangreOriginal == null) return;
        foreach (RecursosInfusion.ParteSanguinea parte in recursos.sangreOriginal)
        {
            Transform objeto = paranteSangre.Find(parte.ruta);
            Renderer renderer = objeto != null ? objeto.GetComponent<Renderer>() : null;
            if (renderer == null || parte.materiales == null || parte.materiales.Length == 0)
                continue;
            MeshFilter filtro = renderer.GetComponent<MeshFilter>();
            int cantidad = filtro != null && filtro.sharedMesh != null
                ? filtro.sharedMesh.subMeshCount : parte.materiales.Length;
            Material[] originales = new Material[cantidad];
            for (int i = 0; i < cantidad; i++)
                originales[i] = parte.materiales[Mathf.Min(i, parte.materiales.Length - 1)];
            renderer.sharedMaterials = originales;
            renderer.SetPropertyBlock(null); // Retira colores blancos/celestes de versiones previas.
            renderer.enabled = true;
        }
        if (tramosSangre.Count == 0)
            foreach (string ruta in new[] { "Via_2 sangre", "Via_1.sangre", "Tubos_1/Sangre" })
            {
                Renderer tramo = paranteSangre.Find(ruta)?.GetComponent<Renderer>();
                if (tramo == null) continue;
                tramosSangre.Add(tramo);
                materialesTramos.Add(tramo.sharedMaterials);
            }
        viaRoja = null;
    }

    private void DesactivarRestosGenerados()
    {
        // Solo nombres creados por versiones anteriores de ESTE módulo.
        // Se desactivan, no se borran ni se tocan el parante de medicamentos o UI.
        string[] nombres = { "Goteo_NaCl", "Sonda_NaCl", "Conexion_NaCl_Paciente",
            "Etiqueta_NaCl_09_Runtime", "Etiqueta_Sangre_Runtime" };
        foreach (string nombre in nombres)
        {
            Transform objeto = nombre.StartsWith("Etiqueta_", StringComparison.Ordinal)
                ? BuscarEnEscena(nombre) : BuscarHijo(paranteSangre, nombre);
            if (objeto != null) objeto.gameObject.SetActive(false);
        }
    }

    // El panel valida sus pasos; aquí solo autoriza un gesto, nunca lo ejecuta.
    public void ReiniciarPrueba()
    {
        ResolverReferencias();
        if (!preparado) return;
        Estado.Reiniciar();
        acciones.Reiniciar();
        reguladorSangre.OcultarIndicacion();
        reguladorSangre.EstablecerFlujoNormalizado(1f, false);
        reguladorNaCl.EstablecerFlujoNormalizado(0f, false);
        AplicarEstadoVisual();
        Informar();
    }

    public void NotificarReaccion()
    {
        Estado.NotificarReaccion();
        AplicarEstadoVisual();
        Debug.Log("[Infusión] Aviso de reacción: no cambia el flujo ni llama al monitor/avatar.", this);
    }

    public void SuspenderSangreValidada()
    {
        if (!preparado || !SangreFluyendo ||
            !acciones.Solicitar(RegistroAccionesManuales.CerrarSangre)) return;
        Estado.SuspenderSangreValidada();
        Indicar(RegistroAccionesManuales.CerrarSangre,
            "Ahora baja manualmente el interruptor de SANGRE hasta cerrarlo.");
    }

    public void ActivarNaClValidado()
    {
        if (!preparado) return;
        if (Estado.Actual != EstadoInfusion.Fase.SangreSuspendida ||
            !acciones.Solicitar(RegistroAccionesManuales.AbrirNaCl))
        {
            Debug.LogWarning("[Infusión] NaCl rechazado: primero debe validarse la suspensión de sangre.", this);
            return;
        }
        Estado.ActivarNaClValidado();
        Indicar(RegistroAccionesManuales.AbrirNaCl,
            "Ahora sube manualmente el interruptor de NaCl 0,9 % para abrirlo.");
    }

    private void RegistrarCierreManual(float apertura)
    {
        if (apertura > .02f || acciones.Pendiente != RegistroAccionesManuales.CerrarSangre ||
            !Estado.ConfirmarCierreManual()) return;
        Completar(RegistroAccionesManuales.CerrarSangre);
    }

    private void RegistrarAperturaManual(float apertura)
    {
        if (apertura < .98f || acciones.Pendiente != RegistroAccionesManuales.AbrirNaCl ||
            !Estado.ConfirmarAperturaManual()) return;
        Completar(RegistroAccionesManuales.AbrirNaCl);
    }

    // Las otras acciones usan el MISMO contrato. El detector de cada compañero
    // confirma solo su interacción real; nunca conectar Confirmar al botón de respuesta.
    // El panel conserva la validación y el orden de sus cinco acciones.
    public bool AutorizarAccionValidada(string id, string instruccion)
    {
        if (!preparado || id == RegistroAccionesManuales.CerrarSangre ||
            id == RegistroAccionesManuales.AbrirNaCl || !acciones.Solicitar(id)) return false;
        Indicar(id, instruccion);
        return true;
    }

    public void RegistrarEjecucionManual(string id)
    {
        if (id == RegistroAccionesManuales.CerrarSangre || id == RegistroAccionesManuales.AbrirNaCl)
            return; // Estos dos solo los puede confirmar su regulador táctil.
        Completar(id);
    }

    private void Indicar(string id, string texto)
    {
        alSolicitarAccionManual.Invoke(id);
        alMostrarInstruccion.Invoke(texto);
        reguladorSangre.MostrarIndicacion(texto, true);
        Debug.Log("[Acción manual pendiente] " + texto, this);
    }

    private void Completar(string id)
    {
        if (!acciones.Confirmar(id)) return;
        AplicarEstadoVisual();
        alCompletarAccionManual.Invoke(id);
        reguladorSangre.MostrarIndicacion("Acción manual completada.");
        Informar();
    }

    // Métodos antiguos conservados para no romper referencias serializadas.
    public void MostrarSangreFluyendo() => ReiniciarPrueba();
    public void MantenerSangreDuranteReaccion() => NotificarReaccion();
    public void DetenerSangre() => SuspenderSangreValidada();
    public void IniciarNaCl() => ActivarNaClValidado();
    public void DetenerTodo()
    {
        if (Estado == null) return;
        Estado.DetenerTodo();
        acciones.Reiniciar();
        AplicarEstadoVisual();
    }

    private void AplicarEstadoVisual()
    {
        if (!preparado || Estado == null) return;
        ParticleSystem.EmissionModule emision = goteoSangre.emission;
        emision.enabled = SangreFluyendo;
        emision.rateOverTime = SangreFluyendo ? tasaSangreOriginal : 0f;
        if (SangreFluyendo)
        {
            if (!goteoSangre.isPlaying) goteoSangre.Play(true);
        }
        else if (goteoSangre.isPlaying || goteoSangre.particleCount > 0)
            goteoSangre.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Solo los tres tramos con sangre: nunca el atlas Soporte con las bolsas.
        if (viaRoja != SangreFluyendo)
        {
            for (int i = 0; i < tramosSangre.Count; i++)
            {
                if (tramosSangre[i] == null) continue;
                Material[] materiales = materialesTramos[i];
                if (!SangreFluyendo)
                {
                    materiales = new Material[materiales.Length];
                    for (int j = 0; j < materiales.Length; j++) materiales[j] = recursos.plasticoNaCl;
                }
                tramosSangre[i].sharedMaterials = materiales;
            }
            viaRoja = SangreFluyendo;
        }
        if (salina != null) salina.EstablecerUso(NaClFluyendo);
    }

    private void Informar() => Debug.Log("[Infusión] " + Estado.Actual +
        " | sangre=" + SangreFluyendo + " | NaCl=" + NaClFluyendo, this);

    private Transform BuscarEnEscena(string nombre)
    {
        foreach (Transform candidato in FindObjectsOfType<Transform>(true))
            if (candidato.gameObject.scene == gameObject.scene && candidato.name == nombre)
                return candidato;
        return null;
    }

    private static Transform BuscarHijo(Transform raiz, string nombre)
    {
        foreach (Transform candidato in raiz.GetComponentsInChildren<Transform>(true))
            if (candidato.name == nombre) return candidato;
        return null;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (goteoSangre != null)
            goteoSangre.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (salina != null)
        {
            if (salinaCreadaDurantePlay) Destroy(salina.gameObject);
            else salina.EstablecerUso(false);
        }
        salinaCreadaDurantePlay = false;
        if (reguladorSangre != null) reguladorSangre.alMovimientoManual.RemoveListener(RegistrarCierreManual);
        if (reguladorNaCl != null) reguladorNaCl.alMovimientoManual.RemoveListener(RegistrarAperturaManual);
        foreach (ControladorReguladorTactil regulador in reguladoresPausados)
            if (regulador != null) regulador.enabled = true;
        reguladoresPausados.Clear();
        Estado?.DetenerTodo();
        acciones.Reiniciar();
        preparado = false;
    }
}
