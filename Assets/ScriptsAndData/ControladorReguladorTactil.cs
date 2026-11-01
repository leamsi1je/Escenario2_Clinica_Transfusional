using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class ControladorReguladorTactil : MonoBehaviour
{
    [Header("Límites de Movimiento (LOCAL Y)")]
    public float limiteSuperior = 0.05f;
    public float limiteInferior = -0.05f;

    [Header("Configuración Táctil")]
    public string tagDedo = "Hand";
    public float limiteVelocidadAgarre = 0.5f;
    public float velocidadRodillo = 8f;

    [Header("Flujo")]
    public ParticleSystem sistemaGoteo;
    public float tasaGoteoMaximo = 2f;
    [Tooltip("El caso comienza con la sangre fluyendo.")]
    public bool iniciarAbierto = true;
    // Las instancias heredadas conservan su posición y tasa iniciales.
    public bool inicializarFlujoAlIniciar;

    [Header("Eventos para escenario y multijugador")]
    public UnityEvent alCerrarRegulador = new UnityEvent();
    public UnityEvent alAbrirRegulador = new UnityEvent();
    public UnityEvent<float> alCambiarFlujo = new UnityEvent<float>();
    // Solo procede de un desplazamiento táctil, nunca de Abrir/Cerrar por código.
    public UnityEvent<float> alMovimientoManual = new UnityEvent<float>();

    [Header("Notificación")]
    public GameObject panelNotificacion;
    public TextMeshProUGUI textoNotificacion;
    public float tiempoVisibleNotificacion = 3f;

    [Header("Posición VR")]
    public float distanciaPanel = 0.8f;
    public float alturaExtra;
    public float velocidadSeguimiento = 5f;

    private ParticleSystem.EmissionModule emisionGoteo;
    private Vector3 posicionInicialLocal;
    private Transform camaraVR;
    private Vector3 escalaOriginalPanel;
    private Collider dedoActual;
    private float ultimoYLocalDedo;
    private bool flujoCerrado;
    private bool inicializado;
    private bool controlExterno;
    private Func<bool> permitirMovimiento;
    private Vector3 origenCerrado;
    private Vector3 ultimoContacto;
    private const float RecorridoManual = .05f;
    private Quaternion rotacionInicialLocal;
    private Rigidbody cuerpoManual;

    public float FlujoNormalizado { get; private set; }
    public bool EstaCerrado => FlujoNormalizado <= 0.02f;

    private void Start()
    {
        Inicializar();
        if (!controlExterno && inicializarFlujoAlIniciar) EstablecerFlujoNormalizado(iniciarAbierto ? 1f : 0f, false);
    }

    private void Inicializar()
    {
        if (inicializado) return;
        inicializado = true;
        posicionInicialLocal = transform.localPosition;
        rotacionInicialLocal = transform.localRotation;
        cuerpoManual = GetComponent<Rigidbody>();

        if (sistemaGoteo != null)
            emisionGoteo = sistemaGoteo.emission;

        if (panelNotificacion != null)
        {
            escalaOriginalPanel = panelNotificacion.transform.localScale;
            panelNotificacion.SetActive(false);
        }

        if (Camera.main != null)
            camaraVR = Camera.main.transform;

    }

    public void ConfigurarControlManual(bool abierto, Func<bool> permiso)
    {
        Inicializar();
        if (!controlExterno)
            origenCerrado = transform.parent.InverseTransformPoint(
                transform.position - Vector3.up * (abierto ? RecorridoManual : 0f));
        controlExterno = true;
        if (cuerpoManual != null)
        {
            cuerpoManual.useGravity = false;
            cuerpoManual.isKinematic = true;
        }
        permitirMovimiento = permiso;
        enabled = true;
        EstablecerFlujoNormalizado(abierto ? 1f : 0f, false);
    }

    private void Update()
    {
        if (panelNotificacion == null ||
            !panelNotificacion.activeInHierarchy ||
            camaraVR == null)
            return;

        Vector3 destino = camaraVR.position + camaraVR.forward * distanciaPanel;
        destino.y += alturaExtra;
        panelNotificacion.transform.position = Vector3.Lerp(
            panelNotificacion.transform.position,
            destino,
            Time.deltaTime * velocidadSeguimiento);
        panelNotificacion.transform.LookAt(camaraVR);
        panelNotificacion.transform.Rotate(0f, 180f, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || dedoActual != null || !EsMano(other) ||
            (controlExterno && (permitirMovimiento == null || !permitirMovimiento()))) return;

        dedoActual = other;
        ultimoContacto = other.transform.position;
        ultimoYLocalDedo =
            transform.parent.InverseTransformPoint(other.transform.position).y;
    }

    private void OnTriggerStay(Collider other)
    {
        // Captura sin salto si la mano ya estaba en contacto al autorizar.
        if (dedoActual == null) OnTriggerEnter(other);
        if (!isActiveAndEnabled || other != dedoActual) return;
        if (controlExterno)
        {
            if (permitirMovimiento == null || !permitirMovimiento()) { dedoActual = null; return; }
            Vector3 contacto = other.transform.position;
            float desplazamiento = contacto.y - ultimoContacto.y;
            ultimoContacto = contacto;
            // Eje vertical MUNDIAL: el FBX sanguíneo tiene ejes locales rotados.
            // Sin salto al centro del dedo, sin límites absolutos de otro modelo.
            if (Mathf.Abs(desplazamiento) < .00001f) return;
            MoverManualmente(FlujoNormalizado + desplazamiento / RecorridoManual);
            return;
        }

        Vector3 dedoLocal =
            transform.parent.InverseTransformPoint(other.transform.position);
        float distancia = Mathf.Abs(dedoLocal.y - ultimoYLocalDedo);
        float velocidad = distancia / Mathf.Max(Time.deltaTime, 0.0001f);
        ultimoYLocalDedo = dedoLocal.y;

        if (velocidad > limiteVelocidadAgarre) return;

        Vector3 posicion = transform.localPosition;
        float destinoY = Mathf.Clamp(
            dedoLocal.y,
            limiteInferior,
            limiteSuperior);
        posicion.y = Mathf.Lerp(
            posicion.y,
            destinoY,
            Time.deltaTime * velocidadRodillo);
        posicion.x = posicionInicialLocal.x;
        posicion.z = posicionInicialLocal.z;
        transform.localPosition = posicion;

        AplicarFlujoDesdePosicion(posicion.y, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == dedoActual)
            dedoActual = null;
    }

    private void LateUpdate()
    {
        // Un Grabbable heredado no puede sacar el interruptor de su guía.
        if (!controlExterno) return;
        if (cuerpoManual != null && !cuerpoManual.isKinematic) cuerpoManual.isKinematic = true;
        EstablecerFlujoNormalizado(FlujoNormalizado, false);
    }

    public void AbrirCompletamente()
    {
        EstablecerFlujoNormalizado(1f, true);
    }

    public void CerrarCompletamente()
    {
        EstablecerFlujoNormalizado(0f, true);
    }

    public void EstablecerFlujoNormalizado(float flujo, bool notificar = true)
    {
        Inicializar(); // Puede llamarse desde Awake de otro componente.
        flujo = Mathf.Clamp01(flujo);

        if (controlExterno)
        {
            FlujoNormalizado = flujo;
            transform.position = transform.parent.TransformPoint(origenCerrado) +
                Vector3.up * RecorridoManual * flujo;
            transform.localRotation = rotacionInicialLocal;
            return; // Sin emisión ni confirmación automática de una acción manual.
        }

        Vector3 posicion = transform.localPosition;
        posicion.y = Mathf.Lerp(limiteInferior, limiteSuperior, flujo);
        posicion.x = posicionInicialLocal.x;
        posicion.z = posicionInicialLocal.z;
        transform.localPosition = posicion;

        AplicarFlujo(flujo, notificar);
    }

    private bool EsMano(Collider otro)
    {
        for (Transform t = otro.transform; t != null; t = t.parent)
            if (t.CompareTag(tagDedo)) return true;
        return false;
    }

    private void MoverManualmente(float flujo)
    {
        if (!controlExterno || permitirMovimiento == null || !permitirMovimiento()) return;
        EstablecerFlujoNormalizado(flujo, false);
        alMovimientoManual.Invoke(FlujoNormalizado);
    }

#if UNITY_EDITOR
    // Simulación explícita para probar sin visor. No existe en el build Quest.
    public void SimularGestoEnEditor(float flujo) => MoverManualmente(flujo);
#endif

    public void MostrarIndicacion(string mensaje, bool persistente = false)
    {
        Inicializar();
        if (panelNotificacion == null) return;
        StopAllCoroutines();
        StartCoroutine(AnimarPanel(mensaje, persistente));
    }

    public void OcultarIndicacion()
    {
        StopAllCoroutines();
        if (panelNotificacion != null) panelNotificacion.SetActive(false);
    }

    private void AplicarFlujoDesdePosicion(float posicionY, bool notificar)
    {
        float flujo = Mathf.InverseLerp(
            limiteInferior,
            limiteSuperior,
            posicionY);
        AplicarFlujo(flujo, notificar);
    }

    private void AplicarFlujo(float flujo, bool notificar)
    {
        FlujoNormalizado = Mathf.Clamp01(flujo);

        if (sistemaGoteo != null)
            emisionGoteo.rateOverTime = FlujoNormalizado * tasaGoteoMaximo;

        alCambiarFlujo.Invoke(FlujoNormalizado);

        bool cerradoAhora = EstaCerrado;
        if (cerradoAhora != flujoCerrado)
        {
            flujoCerrado = cerradoAhora;

            if (flujoCerrado)
            {
                alCerrarRegulador.Invoke();
                if (notificar)
                    MostrarNotificacion("Regulador cerrado correctamente");
            }
            else
            {
                alAbrirRegulador.Invoke();
            }
        }
    }

    private void MostrarNotificacion(string mensaje)
    {
        if (panelNotificacion == null) return;

        StopAllCoroutines();
        StartCoroutine(AnimarPanel(mensaje));
    }

    private IEnumerator AnimarPanel(string mensaje, bool persistente = false)
    {
        if (textoNotificacion != null)
        {
            textoNotificacion.text = mensaje;
            textoNotificacion.enableAutoSizing = true;
            textoNotificacion.fontSizeMin = 18f;
            textoNotificacion.fontSizeMax = 41.5f;
        }

        if (camaraVR != null)
        {
            Vector3 frente =
                camaraVR.position + camaraVR.forward * distanciaPanel;
            frente.y += alturaExtra;
            panelNotificacion.transform.position = frente;
        }

        panelNotificacion.transform.localScale =
            new Vector3(escalaOriginalPanel.x, 0f, escalaOriginalPanel.z);
        panelNotificacion.SetActive(true);

        const float duracion = 0.3f;
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float suave = Mathf.SmoothStep(0f, 1f, tiempo / duracion);
            panelNotificacion.transform.localScale = new Vector3(
                escalaOriginalPanel.x,
                Mathf.Lerp(0f, escalaOriginalPanel.y, suave),
                escalaOriginalPanel.z);
            yield return null;
        }

        panelNotificacion.transform.localScale = escalaOriginalPanel;
        while (persistente) yield return null; // Hasta confirmar/reiniciar, sin avance por tiempo.
        yield return new WaitForSeconds(tiempoVisibleNotificacion);

        tiempo = 0f;
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            panelNotificacion.transform.localScale = new Vector3(
                escalaOriginalPanel.x,
                Mathf.Lerp(escalaOriginalPanel.y, 0f, tiempo / duracion),
                escalaOriginalPanel.z);
            yield return null;
        }

        panelNotificacion.SetActive(false);
    }
}
