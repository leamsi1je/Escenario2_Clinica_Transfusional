using System.Collections;
using UnityEngine;

/// <summary>
/// Coloca al usuario sentado frente a la paciente, con los ojos a su altura y mirándola de frente.
/// - Se repite durante los primeros segundos hasta que el visor entrega una posición estable.
/// - Se repite al ponerse el visor, al recuperar el seguimiento y al recentrar.
/// - Si el usuario queda a una altura absurda (techo/suelo) o muy lejos, se vuelve a acomodar.
/// Va en el objeto del Camera Rig.
/// </summary>
[DefaultExecutionOrder(-50)]
public class InicioFrenteAPaciente : MonoBehaviour
{
    [Tooltip("Cámara del visor (CenterEyeAnchor). Vacío = Camera.main")]
    public Transform ojoCentral;
    [Tooltip("Cabeza de la paciente (hacia donde mirar)")]
    public Transform cabezaPaciente;
    [Tooltip("Punto donde deben quedar los ojos del usuario")]
    public Transform puntoInicio;
    [Tooltip("Diferencia de altura respecto de los ojos de la paciente (m)")]
    public float ajusteAltura = 0.03f;

    [Header("Seguridad")]
    [Tooltip("Segundos iniciales en los que se re-alinea continuamente")]
    public float segundosIniciales = 3f;
    [Tooltip("Altura mínima y máxima válidas de los ojos (m). Fuera de esto se re-alinea.")]
    public Vector2 alturaValida = new Vector2(0.6f, 1.9f);
    [Tooltip("Distancia horizontal máxima al punto de inicio antes de re-alinear (m)")]
    public float distanciaMaxima = 2.5f;

    float alinearHasta;
    bool giroPendiente;

    void OnEnable()
    {
        OVRManager.HMDMounted += Programar;
        OVRManager.TrackingAcquired += Programar;
        if (OVRManager.display != null) OVRManager.display.RecenteredPose += Programar;
    }

    void OnDisable()
    {
        OVRManager.HMDMounted -= Programar;
        OVRManager.TrackingAcquired -= Programar;
        if (OVRManager.display != null) OVRManager.display.RecenteredPose -= Programar;
    }

    IEnumerator Start()
    {
        if (!ojoCentral && Camera.main) ojoCentral = Camera.main.transform;
        yield return null;
        Programar();
    }

    // Re-alinear cada cuadro durante unos segundos (el visor tarda en estabilizarse).
    void Programar() { alinearHasta = Time.unscaledTime + segundosIniciales; giroPendiente = true; }

    void LateUpdate()
    {
        if (!ojoCentral || !puntoInicio) return;

        if (Time.unscaledTime < alinearHasta)
        {
            // El giro se corrige una sola vez, cuando el visor ya reporta la cabeza (si no, marearía).
            bool cabezaDetectada = ojoCentral.localPosition.sqrMagnitude > 1e-6f || ojoCentral.localRotation != Quaternion.identity;
            bool girar = giroPendiente && cabezaDetectada;
            Alinear(girar);
            if (girar) giroPendiente = false;
            return;
        }

        // Red de seguridad: altura imposible o demasiado lejos del lugar del doctor.
        Vector3 p = ojoCentral.position;
        Vector3 d = p - puntoInicio.position; d.y = 0f;
        if (p.y < alturaValida.x || p.y > alturaValida.y || d.magnitude > distanciaMaxima)
        {
            if (Time.unscaledTime > 10f) Debug.Log($"[Inicio] Posición fuera de rango ({p}); re-alineando.");
            Programar();
        }
    }

    [ContextMenu("Alinear ahora")]
    public void Alinear() => Alinear(true);

    public void Alinear(bool girar)
    {
        if (!ojoCentral || !puntoInicio) return;

        Vector3 destino = puntoInicio.position;
        if (cabezaPaciente) destino.y = cabezaPaciente.position.y + ajusteAltura;

        // 1) Girar el rig alrededor de la cabeza del usuario para mirar a la paciente
        Vector3 mirar = cabezaPaciente ? cabezaPaciente.position - destino : puntoInicio.forward;
        mirar.y = 0f;
        Vector3 actual = ojoCentral.forward; actual.y = 0f;
        if (girar && mirar.sqrMagnitude > 1e-4f && actual.sqrMagnitude > 1e-4f)
        {
            float giro = Vector3.SignedAngle(actual, mirar, Vector3.up);
            if (Mathf.Abs(giro) > 0.5f) transform.RotateAround(ojoCentral.position, Vector3.up, giro);
        }

        // 2) Mover el rig para que los ojos queden en el punto de inicio
        transform.position += destino - ojoCentral.position;
    }
}
