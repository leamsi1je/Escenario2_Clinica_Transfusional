using UnityEngine;
using System.Collections;

public class AlternarParantes : MonoBehaviour
{
    [Header("Objetos a alternar")]
    public GameObject parantePlasma;
    public GameObject paranteSangre;

    [Header("Estado inicial")]
    public bool mostrarPlasmaAlInicio = true;

    [Header("Notificaciones")]
    public GameObject notificacion1;
    public GameObject notificacion2;

    [Header("Configuración de Notificaciones")]
    public float tiempoVisibleNotificacion = 3f;
    public float distanciaPanel = 0.8f;
    public float alturaExtra = 0f;
    public float velocidadSeguimiento = 5f;

    private Transform camaraVR;
    private Vector3 escalaOriginalNotificacion1;
    private Vector3 escalaOriginalNotificacion2;

    void Awake()
    {
        if (notificacion1 != null) notificacion1.SetActive(false);
        if (notificacion2 != null) notificacion2.SetActive(false);
    }

    void Start()
    {
        if (Camera.main != null) camaraVR = Camera.main.transform;

        if (notificacion1 != null) escalaOriginalNotificacion1 = notificacion1.transform.localScale;
        if (notificacion2 != null) escalaOriginalNotificacion2 = notificacion2.transform.localScale;

        if (parantePlasma != null) parantePlasma.SetActive(mostrarPlasmaAlInicio);
        if (paranteSangre != null) paranteSangre.SetActive(!mostrarPlasmaAlInicio);
    }

    void Update()
    {
        if (notificacion1 != null && notificacion1.activeInHierarchy && camaraVR != null)
            SeguirCamara(notificacion1);

        if (notificacion2 != null && notificacion2.activeInHierarchy && camaraVR != null)
            SeguirCamara(notificacion2);
    }

    void SeguirCamara(GameObject panel)
    {
        Vector3 destino = camaraVR.position + (camaraVR.forward * distanciaPanel);
        destino.y += alturaExtra;
        panel.transform.position = Vector3.Lerp(panel.transform.position, destino, Time.deltaTime * velocidadSeguimiento);
        panel.transform.LookAt(camaraVR);
        panel.transform.Rotate(0, 180, 0);
    }

    public void AlternarObjetos()
    {
        bool plasmaActivo = parantePlasma.activeSelf;

        parantePlasma.SetActive(!plasmaActivo);
        paranteSangre.SetActive(plasmaActivo);

        StopAllCoroutines();
        StartCoroutine(SecuenciaNotificaciones());
    }

    IEnumerator SecuenciaNotificaciones()
    {
        if (notificacion1 != null) notificacion1.SetActive(false);
        if (notificacion2 != null) notificacion2.SetActive(false);

        yield return StartCoroutine(AnimarPanel(notificacion1, escalaOriginalNotificacion1));

        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(AnimarPanel(notificacion2, escalaOriginalNotificacion2));
    }

    IEnumerator AnimarPanel(GameObject panel, Vector3 escalaOriginal)
    {
        if (panel == null) yield break;

        if (camaraVR != null)
        {
            Vector3 posFrente = camaraVR.position + (camaraVR.forward * distanciaPanel);
            posFrente.y += alturaExtra;
            panel.transform.position = posFrente;
        }

        panel.transform.localScale = new Vector3(escalaOriginal.x, 0f, escalaOriginal.z);
        panel.SetActive(true);

        float duracion = 0.30f;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float suavizado = Mathf.SmoothStep(0f, 1f, t / duracion);
            panel.transform.localScale = new Vector3(escalaOriginal.x, Mathf.Lerp(0f, escalaOriginal.y, suavizado), escalaOriginal.z);
            yield return null;
        }
        panel.transform.localScale = escalaOriginal;

        yield return new WaitForSeconds(tiempoVisibleNotificacion);

        t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            panel.transform.localScale = new Vector3(escalaOriginal.x, Mathf.Lerp(escalaOriginal.y, 0f, t / duracion), escalaOriginal.z);
            yield return null;
        }

        panel.SetActive(false);
    }
}