using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;

/// <summary>
/// Paso del Escenario 1 al Escenario 2 (según el flujo del grupo):
///  - Estudiante (con visor): al registrar A/B/C aparece el panel "IR A ESCENA 2";
///    al presionar INGRESAR se carga el Escenario 2.
///  - Doctor/facilitador (PC sin visor): no hace el Escenario 1, pasa directo al Escenario 2.
/// El rol se decide igual que en FusionBootstrap: Quest = Estudiante; PC sin visor activo = Doctor.
/// </summary>
public class TransicionEscenario2 : MonoBehaviour
{
    public enum RolEnEditor { Automatico, Doctor, Estudiante }

    [Header("Referencias")]
    public FlujoEscenario1 flujo;
    [Tooltip("Panel grande \"IR A ESCENA 2\" (empieza oculto)")]
    public GameObject panel;
    public Button botonIngresar;

    [Header("Escenario 2")]
    [Tooltip("Nombre de la escena del Escenario 2 (debe estar en Build Settings)")]
    public string escena2 = "Escena_nueva";
    [Tooltip("Segundos entre registrar la respuesta y mostrar el panel")]
    public float esperaTrasRespuesta = 3f;
    [Tooltip("Distancia del panel frente a la cabeza (m)")]
    public float distancia = 1.3f;

    [Header("Pruebas en el Editor")]
    [Tooltip("Automático: sin visor activo = Doctor (salta al Escenario 2), con visor = Estudiante. " +
             "Elige Estudiante para probar el Escenario 1 en el Editor sin visor.")]
    public RolEnEditor rolEnEditor = RolEnEditor.Automatico;

    bool cargando;

    void Awake()
    {
        if (panel) panel.SetActive(false);
        if (EsDoctor())
        {
            Debug.Log("[Transición] Rol Doctor: se omite el Escenario 1 y se carga " + escena2);
            Cargar();
        }
    }

    void Start()
    {
        if (cargando) return;
        if (flujo) flujo.AlResponder += _ => StartCoroutine(MostrarLuego());
        else Debug.LogWarning("[Transición] Falta la referencia a FlujoEscenario1", this);
        if (botonIngresar) botonIngresar.onClick.AddListener(Ingresar);
        else Debug.LogWarning("[Transición] Falta el botón INGRESAR", this);
        OVRManager.HMDMounted += AlPonerseVisor;
    }

    void OnDestroy() => OVRManager.HMDMounted -= AlPonerseVisor;

    bool EsDoctor()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return false;
#else
#if UNITY_EDITOR
        if (rolEnEditor == RolEnEditor.Doctor) return true;
        if (rolEnEditor == RolEnEditor.Estudiante) return false;
#endif
        var display = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
        return !(display != null && display.running);
#endif
    }

    IEnumerator MostrarLuego()
    {
        yield return new WaitForSecondsRealtime(esperaTrasRespuesta);
        MostrarPanel();
    }

    /// <summary>Muestra el panel "IR A ESCENA 2" frente al usuario.</summary>
    public void MostrarPanel()
    {
        if (!panel || cargando) return;
        panel.SetActive(true);
        ColocarAlFrente();
        Debug.Log("[Transición] Panel IR A ESCENA 2 mostrado.");
    }

    // Tras la retroalimentación el estudiante vuelve a ponerse el visor: el panel reaparece al frente.
    void AlPonerseVisor()
    {
        if (panel && panel.activeSelf) ColocarAlFrente();
    }

    void ColocarAlFrente()
    {
        var cam = Camera.main ? Camera.main.transform : null;
        if (!cam) return;
        var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(cam.up, Vector3.up);
        fwd.Normalize();
        panel.transform.position = cam.position + fwd * distancia;
        panel.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }

    public void Ingresar()
    {
        if (cargando) return;
        Debug.Log("[Transición] INGRESAR → " + escena2);
        if (botonIngresar) botonIngresar.interactable = false;
        Cargar();
    }

    void Cargar()
    {
        if (cargando) return;
        if (!Application.CanStreamedLevelBeLoaded(escena2))
        {
            Debug.LogError($"[Transición] La escena '{escena2}' no está en Build Settings.", this);
            return;
        }
        cargando = true;
        SceneManager.LoadScene(escena2, LoadSceneMode.Single);
    }
}
