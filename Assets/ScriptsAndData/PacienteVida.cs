using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detalles que humanizan a la paciente:
///  - Parpadeo aleatorio (blendshapes eyeBlinkLeft / eyeBlinkRight).
///  - Mirada y cabeza que siguen al usuario (cámara del visor), con límites.
///  - Respiración suave (pecho).
/// SALSA se encarga de la boca; este script no toca los visemas.
/// </summary>
[DefaultExecutionOrder(1000)]
public class PacienteVida : MonoBehaviour
{
    [Header("Huesos (los asigna MariaSetup)")]
    public Transform head, neck, chest, eyeL, eyeR;

    [Header("Mirada")]
    [Tooltip("Vacío = cámara principal (visor).")]
    public Transform objetivo;
    public float distanciaMaxima = 4f;
    [Range(0, 1)] public float pesoCabeza = 0.35f;
    public float limiteCabeza = 30f, limiteOjos = 25f;
    public float suavizado = 4f;

    [Header("Parpadeo")]
    public Vector2 intervaloParpadeo = new Vector2(2.5f, 6f);
    public float duracionParpadeo = 0.16f;

    [Header("Respiración")]
    public float respiracionesPorMinuto = 20f; // FR 22 rpm en el caso clínico
    public float amplitudRespiracion = 1.2f;   // grados

    struct Blink { public SkinnedMeshRenderer smr; public int index; }
    readonly List<Blink> blinks = new List<Blink>();

    Quaternion headRest, eyeLRest, eyeRRest, chestRest;
    Vector3 faceFwdLocal; // dirección de la cara en espacio del cuello (padre de la cabeza)
    Quaternion headOffset = Quaternion.identity, eyeOffset = Quaternion.identity;
    float nextBlink, blinkT = -1f;

    void Start()
    {
        if (!objetivo && Camera.main) objetivo = Camera.main.transform;

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.sharedMesh) continue;
            foreach (var n in new[] { "eyeBlinkLeft", "eyeBlinkRight" })
            {
                int i = smr.sharedMesh.GetBlendShapeIndex(n);
                if (i >= 0) blinks.Add(new Blink { smr = smr, index = i });
            }
        }

        if (head) headRest = head.localRotation;
        if (eyeL) eyeLRest = eyeL.localRotation;
        if (eyeR) eyeRRest = eyeR.localRotation;
        if (chest) chestRest = chest.localRotation;

        if (head && eyeL && eyeR && head.parent)
        {
            Vector3 mid = (eyeL.position + eyeR.position) * 0.5f;
            Vector3 up = neck ? (head.position - neck.position).normalized : head.up;
            Vector3 lat = (eyeR.position - eyeL.position).normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(mid - head.position, up), lat).normalized;
            faceFwdLocal = head.parent.InverseTransformDirection(fwd);
        }
        nextBlink = Time.time + Random.Range(intervaloParpadeo.x, intervaloParpadeo.y);
    }

    void LateUpdate()
    {
        // ---- Respiración ----
        if (chest)
        {
            float a = Mathf.Sin(Time.time * respiracionesPorMinuto / 60f * Mathf.PI * 2f) * amplitudRespiracion;
            chest.localRotation = chestRest * Quaternion.Euler(-a, 0f, 0f);
        }

        // ---- Mirada ----
        if (head && head.parent && objetivo)
        {
            head.localRotation = headRest;
            if (eyeL) eyeL.localRotation = eyeLRest;
            if (eyeR) eyeR.localRotation = eyeRRest;

            Vector3 fwd = head.parent.TransformDirection(faceFwdLocal);
            Vector3 mid = eyeL && eyeR ? (eyeL.position + eyeR.position) * 0.5f : head.position;
            Vector3 to = objetivo.position - mid;
            bool cerca = to.magnitude <= distanciaMaxima;

            Quaternion full = Quaternion.FromToRotation(fwd, to.normalized);
            float ang = Quaternion.Angle(Quaternion.identity, full);
            Quaternion headTarget = Quaternion.identity, eyeTarget = Quaternion.identity;
            if (cerca && ang < 100f)
            {
                headTarget = Quaternion.RotateTowards(Quaternion.identity, Quaternion.Slerp(Quaternion.identity, full, pesoCabeza), limiteCabeza);
                Quaternion rest = Quaternion.Inverse(headTarget) * full;
                eyeTarget = Quaternion.RotateTowards(Quaternion.identity, rest, limiteOjos);
            }
            float k = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
            headOffset = Quaternion.Slerp(headOffset, headTarget, k);
            eyeOffset = Quaternion.Slerp(eyeOffset, eyeTarget, k * 2f);

            head.rotation = headOffset * head.rotation;
            if (eyeL) eyeL.rotation = eyeOffset * eyeL.rotation;
            if (eyeR) eyeR.rotation = eyeOffset * eyeR.rotation;
        }

        // ---- Parpadeo ----
        if (blinkT < 0f && Time.time >= nextBlink) blinkT = 0f;
        float w = 0f;
        if (blinkT >= 0f)
        {
            blinkT += Time.deltaTime;
            float p = blinkT / duracionParpadeo;
            w = p < 0.5f ? p * 2f : 2f - p * 2f;
            if (p >= 1f)
            {
                blinkT = -1f; w = 0f;
                // a veces parpadeo doble
                nextBlink = Time.time + (Random.value < 0.15f ? 0.25f : Random.Range(intervaloParpadeo.x, intervaloParpadeo.y));
            }
        }
        foreach (var b in blinks) if (b.smr) b.smr.SetBlendShapeWeight(b.index, Mathf.Clamp01(w) * 100f);
    }
}
