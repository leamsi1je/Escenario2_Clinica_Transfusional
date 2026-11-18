using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

/// <summary>
/// Flujo del Escenario 1 según el PDF:
///  - La tablet (en la muñeca) aparece sola cuando la paciente termina de responder el motivo de consulta.
///  - "Indicación transfusional" queda bloqueado hasta revisar los 5 apartados clínicos.
///  - Al elegir A, B o C se registra la respuesta (JSON) sin mostrar Correcto/Incorrecto.
/// </summary>
public class FlujoEscenario1 : MonoBehaviour
{
    [Header("Paciente")]
    public DialogflowIntentClientEscenario1 paciente;
    public AudioSource vozPaciente;

    [Header("Tablet")]
    public GameObject tablet;                 // Panel_seleccion
    [Tooltip("Paneles de los 5 apartados clínicos, en orden del PDF")]
    public List<GameObject> panelesClinicos = new List<GameObject>();

    [Header("Indicación transfusional")]
    public GameObject botonIndicacion;
    public TMP_Text textoBotonIndicacion;
    public string textoBloqueado = "Indicación transfusional (bloqueado)";
    public string textoDesbloqueado = "Indicación transfusional";

    [Header("Alternativas")]
    public GameObject opcionA, opcionB, opcionC;
    public GameObject menuOpciones;           // Btns Opciones
    public GameObject menuPrincipal;          // Btns

    readonly HashSet<GameObject> visitados = new HashSet<GameObject>();
    bool tabletMostrada, desbloqueado, respondido;
    readonly List<(IInteractableView view, Action<InteractableStateChangeArgs> cb)> subs = new List<(IInteractableView, Action<InteractableStateChangeArgs>)>();

    public bool Desbloqueado => desbloqueado;
    public bool Respondido => respondido;
    public IReadOnlyCollection<GameObject> Visitados => visitados;

    /// <summary>Se dispara una sola vez, cuando queda registrada la alternativa (A, B o C).</summary>
    public event Action<string> AlResponder;

    void Start()
    {
        if (tablet) tablet.SetActive(false);
        if (paciente) paciente.onSubtitle.AddListener(_ => { if (!tabletMostrada) StartCoroutine(MostrarTabletAlTerminar()); });
        Bloquear(true);
        Suscribir(opcionA, "A");
        Suscribir(opcionB, "B");
        Suscribir(opcionC, "C");
    }

    void OnDestroy()
    {
        foreach (var s in subs) if (s.view != null) s.view.WhenStateChanged -= s.cb;
    }

    IEnumerator MostrarTabletAlTerminar()
    {
        tabletMostrada = true;
        yield return null;
        while (vozPaciente && vozPaciente.isPlaying) yield return null;
        yield return new WaitForSeconds(0.4f);
        if (tablet) tablet.SetActive(true);
        Debug.Log("[Flujo] Tablet mostrada.");
    }

    void Update()
    {
        // Un apartado cuenta como revisado cuando su panel se abre al menos una vez.
        foreach (var p in panelesClinicos)
            if (p && p.activeInHierarchy && visitados.Add(p))
                Debug.Log($"[Flujo] Revisado: {p.name} ({visitados.Count}/{panelesClinicos.Count})");

        if (!desbloqueado && panelesClinicos.Count > 0 && visitados.Count >= panelesClinicos.Count)
        {
            Bloquear(false);
            Debug.Log("[Flujo] Indicación transfusional: BLOQUEADO → DESBLOQUEADO");
        }
    }

    void Bloquear(bool bloquear)
    {
        desbloqueado = !bloquear;
        if (!botonIndicacion) return;
        foreach (var pi in botonIndicacion.GetComponentsInChildren<PokeInteractable>(true)) pi.enabled = !bloquear;
        if (textoBotonIndicacion)
        {
            textoBotonIndicacion.text = bloquear ? textoBloqueado : textoDesbloqueado;
            textoBotonIndicacion.alpha = bloquear ? 0.45f : 1f;
        }
    }

    void Suscribir(GameObject boton, string letra)
    {
        if (!boton) return;
        var view = boton.GetComponentInChildren<PokeInteractable>(true) as IInteractableView;
        if (view == null) return;
        Action<InteractableStateChangeArgs> cb = args =>
        {
            if (args.NewState == InteractableState.Select) Registrar(letra, boton);
        };
        view.WhenStateChanged += cb;
        subs.Add((view, cb));
    }

    void Registrar(string letra, GameObject boton)
    {
        if (respondido || !desbloqueado) return;
        respondido = true;

        var txt = boton.GetComponentInChildren<TMP_Text>(true);
        var r = new Respuesta
        {
            escenario = "Escenario 1 - Anemia sintomática severa",
            opcion = letra,
            texto = txt ? txt.text : "",
            fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            apartadosRevisados = visitados.Count
        };
        string ruta = Path.Combine(Application.persistentDataPath, "respuestas_escenario1.jsonl");
        try { File.AppendAllText(ruta, JsonUtility.ToJson(r) + Environment.NewLine); }
        catch (Exception e) { Debug.LogError("[Flujo] No se pudo guardar: " + e.Message); }
        Debug.Log($"[Flujo] Respuesta registrada: {letra} → {ruta}");

        // Sin retroalimentación: solo se cierran las opciones para evitar cambios.
        StartCoroutine(CerrarOpciones());
        AlResponder?.Invoke(letra);
    }

    // Se cierra un instante después: si se desactiva en el mismo cuadro del toque,
    // la animación del botón intenta arrancar sobre un objeto inactivo y da error.
    IEnumerator CerrarOpciones()
    {
        yield return new WaitForSeconds(0.35f);
        foreach (var b in new[] { opcionA, opcionB, opcionC })
            if (b) foreach (var pi in b.GetComponentsInChildren<PokeInteractable>(true)) pi.enabled = false;
        if (menuOpciones) menuOpciones.SetActive(false);
        if (menuPrincipal) menuPrincipal.SetActive(true);
    }

    [Serializable]
    class Respuesta
    {
        public string escenario, opcion, texto, fechaHora;
        public int apartadosRevisados;
    }
}
