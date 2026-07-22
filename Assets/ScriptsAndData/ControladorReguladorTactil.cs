using UnityEngine;
using System.Collections;
using TMPro;

public class ControladorReguladorTactil : MonoBehaviour
{
    [Header("Límites de Movimiento (LOCAL Y)")]
    [Tooltip("Distancia máxima que sube la rueda respecto al Cuerpo Blanco (Eje Y)")]
    public float limiteSuperior = 0.05f; 
    [Tooltip("Distancia máxima que baja la rueda respecto al Cuerpo Blanco (Eje Y)")]
    public float limiteInferior = -0.05f; 

    [Header("Configuración Táctil")]
    [Tooltip("El Tag que debe tener tu dedo (o cubo) para mover la rueda")]
    public string tagDedo = "Hand";

    [Header("Simulación de Fricción (Resbalón)")]
    [Tooltip("Si mueves el dedo más rápido que esto, resbalará y no moverá la rueda. (Ej: 0.5)")]
    public float limiteVelocidadAgarre = 0.5f; 
    [Tooltip("Velocidad con la que la rueda persigue al dedo cuando vas lento (fuerza).")]
    public float velocidadRodillo = 8f; 

    [Header("Referencias de la Sangre y UI")]
    public ParticleSystem sistemaGoteo;
    private ParticleSystem.EmissionModule emisionGoteo;
    public float tasaGoteoMaximo = 2f; 
    public GameObject panelNotificacion;
    public TextMeshProUGUI textoNotificacion;
    public float tiempoVisibleNotificacion = 3f;
    
    [Header("Configuración de Posición VR")]
    public float distanciaPanel = 0.8f;
    public float alturaExtra = 0f; 
    public float velocidadSeguimiento = 5f;

    private bool cerradoNotificado = false;
    private Vector3 posicionInicialLocal;
    private Transform camaraVR;
    private Vector3 escalaOriginalPanel; 
    
    // Variables para calcular la velocidad del dedo
    private Collider dedoActual;
    private float ultimoYLocalDedo;

    void Start()
    {
        posicionInicialLocal = transform.localPosition;
        
        if (sistemaGoteo != null) emisionGoteo = sistemaGoteo.emission;
        
        if (panelNotificacion != null) 
        {
            escalaOriginalPanel = panelNotificacion.transform.localScale;
            panelNotificacion.SetActive(false);
        }
        
        if (Camera.main != null) camaraVR = Camera.main.transform;
    }

    void Update()
    {
        if (panelNotificacion != null && panelNotificacion.activeInHierarchy && camaraVR != null)
        {
            Vector3 posicionDestino = camaraVR.position + (camaraVR.forward * distanciaPanel);
            posicionDestino.y += alturaExtra; 
            panelNotificacion.transform.position = Vector3.Lerp(panelNotificacion.transform.position, posicionDestino, Time.deltaTime * velocidadSeguimiento);
            panelNotificacion.transform.LookAt(camaraVR);
            panelNotificacion.transform.Rotate(0, 180, 0); 
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (dedoActual == null && other.CompareTag(tagDedo))
        {
            dedoActual = other; 
            // Registramos la posición del dedo referenciada al espacio del chasis blanco
            ultimoYLocalDedo = transform.parent.InverseTransformPoint(other.transform.position).y;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other == dedoActual)
        {
            // Ubicamos al dedo en relación al cuerpo blanco (parent)
            Vector3 posicionDedoEnPadre = transform.parent.InverseTransformPoint(other.transform.position);
            
            // --- CÁLCULO DE VELOCIDAD DE DESLIZAMIENTO ---
            float distanciaRecorrida = Mathf.Abs(posicionDedoEnPadre.y - ultimoYLocalDedo);
            float velocidadDedo = distanciaRecorrida / Time.deltaTime;
            
            ultimoYLocalDedo = posicionDedoEnPadre.y;

            // Si el jugador no está deslizando excesivamente rápido (resbalón)
            if (velocidadDedo <= limiteVelocidadAgarre)
            {
                Vector3 posLocalActual = transform.localPosition;

                // Calculamos el destino en el EJE Y
                float yDestino = Mathf.Clamp(posicionDedoEnPadre.y, limiteInferior, limiteSuperior);
                
                // Aplicamos el movimiento suavizado
                posLocalActual.y = Mathf.Lerp(posLocalActual.y, yDestino, Time.deltaTime * velocidadRodillo);
                
                // Bloqueamos la X y la Z para que la rueda no se salga del carril
                posLocalActual.x = posicionInicialLocal.x;
                posLocalActual.z = posicionInicialLocal.z;
                
                transform.localPosition = posLocalActual;
                ActualizarGoteoYNotificacion(posLocalActual.y);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other == dedoActual) 
        {
            dedoActual = null; 
        }
    }

    void ActualizarGoteoYNotificacion(float posY)
    {
        float porcentajeAbierto = Mathf.InverseLerp(limiteInferior, limiteSuperior, posY);
        if (sistemaGoteo != null) emisionGoteo.rateOverTime = porcentajeAbierto * tasaGoteoMaximo;

        if (porcentajeAbierto <= 0.02f) 
        {
            if (!cerradoNotificado)
            {
                MostrarNotificacion("Regulador cerrado correctamente");
                cerradoNotificado = true;
            }
        }
        else cerradoNotificado = false;
    }

    void MostrarNotificacion(string mensaje)
    {
        if (panelNotificacion != null)
        {
            StopAllCoroutines();
            StartCoroutine(AnimarPanel(mensaje)); 
        }
    }

    IEnumerator AnimarPanel(string mensaje)
    {
        if (textoNotificacion != null) textoNotificacion.text = mensaje;
        if (camaraVR != null)
        {
            Vector3 posicionFrente = camaraVR.position + (camaraVR.forward * distanciaPanel);
            posicionFrente.y += alturaExtra; 
            panelNotificacion.transform.position = posicionFrente;
        }
        
        panelNotificacion.transform.localScale = new Vector3(escalaOriginalPanel.x, 0f, escalaOriginalPanel.z); 
        panelNotificacion.SetActive(true);
        
        float tiempoAnimacion = 0.30f; 
        float tiempoPasado = 0f;
        
        while (tiempoPasado < tiempoAnimacion)
        {
            tiempoPasado += Time.deltaTime;
            float porcentaje = tiempoPasado / tiempoAnimacion;
            float suavizado = Mathf.SmoothStep(0f, 1f, porcentaje);
            float nuevaEscalaY = Mathf.Lerp(0f, escalaOriginalPanel.y, suavizado);
            panelNotificacion.transform.localScale = new Vector3(escalaOriginalPanel.x, nuevaEscalaY, escalaOriginalPanel.z);
            yield return null;
        }
        panelNotificacion.transform.localScale = escalaOriginalPanel;
        
        yield return new WaitForSeconds(tiempoVisibleNotificacion);
        
        tiempoPasado = 0f;
        while (tiempoPasado < tiempoAnimacion)
        {
            tiempoPasado += Time.deltaTime;
            float porcentaje = tiempoPasado / tiempoAnimacion;
            float nuevaEscalaY = Mathf.Lerp(escalaOriginalPanel.y, 0f, porcentaje);
            panelNotificacion.transform.localScale = new Vector3(escalaOriginalPanel.x, nuevaEscalaY, escalaOriginalPanel.z);
            yield return null;
        }
        panelNotificacion.SetActive(false);
    }

}