using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Reproduce los seis signos vitales solicitados desde perfiles CSV derivados
/// de PhysioNet. Cada perfil contiene 60 muestras y se repite en bucle.
/// </summary>
public class MonitorTextos : MonoBehaviour
{
    private const string RUTA_NORMAL = "PhysioNet/estado_normal";
    private const string RUTA_REACCION = "PhysioNet/estado_reaccion";

    private struct MuestraVital
    {
        public int sistolica;
        public int diastolica;
        public int bpm;
        public int respiracion;
        public float temperatura;
        public int oxigeno;
    }

    [Header("Conecta aquí tus textos de UI")]
    public TextMeshProUGUI textBPM;
    public TextMeshProUGUI textOxigeno;
    public TextMeshProUGUI textPresion;
    public TextMeshProUGUI textRespiracion;
    public TextMeshProUGUI textTemperatura;

    [Header("Valores Base (compatibilidad ORI)")]
    public int baseBPM = 80;
    public int baseOxigeno = 98;
    public int baseSistolica = 120;
    public int baseDiastolica = 80;
    public int baseRespiracion = 16;
    public float baseTemperatura = 36.5f;
    public bool usarPerfilesClinicos;
    private float timer;

    [Header("Datos PhysioNet (opcional en Inspector)")]
    [Tooltip("Si queda vacío, se carga Resources/PhysioNet/estado_normal.csv.")]
    public TextAsset datosEstadoNormal;
    [Tooltip("Si queda vacío, se carga Resources/PhysioNet/estado_reaccion.csv.")]
    public TextAsset datosEstadoReaccion;
    [Min(0.1f)] public float segundosPorMuestra = 2f;
    public bool repetirPerfil = true;

    [Header("Inicio")]
    public bool mostrarEstadoEstableAlIniciar = true;

    [Header("Evento de sincronización")]
    [Tooltip("Se ejecuta después de aplicar cada fila del CSV.")]
    public UnityEvent alActualizarSignosVitales = new UnityEvent();

    public int BPMActual { get; private set; }
    public int OxigenoActual { get; private set; }
    public int SistolicaActual { get; private set; }
    public int DiastolicaActual { get; private set; }
    public int RespiracionActual { get; private set; }
    public float TemperaturaActual { get; private set; }
    public bool EstadoReaccionActivo { get; private set; }
    public int MuestraActual { get; private set; }

    private readonly List<MuestraVital> muestrasNormales = new List<MuestraVital>();
    private readonly List<MuestraVital> muestrasReaccion = new List<MuestraVital>();
    private List<MuestraVital> perfilActivo;
    private Coroutine reproduccionCoroutine;

    private void Awake()
    {
        if (usarPerfilesClinicos) CargarPerfiles();
    }

    private void Start()
    {
        if (!usarPerfilesClinicos) { ActualizarValoresBase(); return; }
        if (mostrarEstadoEstableAlIniciar && perfilActivo == null)
            MostrarEstadoEstable();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && perfilActivo != null && reproduccionCoroutine == null)
            reproduccionCoroutine = StartCoroutine(ReproducirPerfil());
    }

    private void Update()
    {
        if (usarPerfilesClinicos) return;
        timer += Time.deltaTime;
        if (timer >= 2f) { timer = 0f; ActualizarValoresBase(); }
    }

    private void ActualizarValoresBase()
    {
        AplicarSignosVitales(baseBPM + Random.Range(-2, 3),
            baseOxigeno + Random.Range(-1, 2), baseSistolica + Random.Range(-3, 4),
            baseDiastolica + Random.Range(-2, 3), baseRespiracion + Random.Range(-1, 2),
            baseTemperatura + Random.Range(-0.2f, 0.3f));
    }

    public void ReiniciarMonitor()
    {
        perfilActivo = null;
        MostrarEstadoEstable();
    }

    private void OnDisable()
    {
        if (reproduccionCoroutine != null)
        {
            StopCoroutine(reproduccionCoroutine);
            reproduccionCoroutine = null;
        }
    }

    public void MostrarEstadoEstable()
    {
        ActivarPerfil(muestrasNormales, false);
    }

    public void MostrarReaccionTransfusional()
    {
        ActivarPerfil(muestrasReaccion, true);
    }

    public void MostrarRecuperacion()
    {
        MostrarEstadoEstable();
    }

    /// <summary>
    /// Método para Button/OnClick: normal -> reacción -> normal en cada pulsación.
    /// </summary>
    public void AlternarEstadoDesdeBoton()
    {
        if (EstadoReaccionActivo)
            MostrarEstadoEstable();
        else
            MostrarReaccionTransfusional();
    }

    public void AplicarSignosVitales(
        int bpm,
        int oxigeno,
        int sistolica,
        int diastolica,
        int respiracion,
        float temperatura)
    {
        BPMActual = Mathf.Max(0, bpm);
        OxigenoActual = Mathf.Clamp(oxigeno, 0, 100);
        SistolicaActual = Mathf.Max(0, sistolica);
        DiastolicaActual = Mathf.Max(0, diastolica);
        RespiracionActual = Mathf.Max(0, respiracion);
        TemperaturaActual = temperatura;

        if (textBPM != null) textBPM.text = BPMActual + " Bpm";
        if (textOxigeno != null) textOxigeno.text = OxigenoActual + " %";
        if (textPresion != null) textPresion.text = SistolicaActual + "/" + DiastolicaActual;
        if (textRespiracion != null) textRespiracion.text = RespiracionActual.ToString();
        if (textTemperatura != null) textTemperatura.text = TemperaturaActual.ToString("F1") + " °C";

        alActualizarSignosVitales.Invoke();
    }

    private void CargarPerfiles()
    {
        if (datosEstadoNormal == null)
            datosEstadoNormal = Resources.Load<TextAsset>(RUTA_NORMAL);
        if (datosEstadoReaccion == null)
            datosEstadoReaccion = Resources.Load<TextAsset>(RUTA_REACCION);

        ParsearCsv(datosEstadoNormal, muestrasNormales, "estado normal");
        ParsearCsv(datosEstadoReaccion, muestrasReaccion, "estado de reacción");
    }

    private void ActivarPerfil(List<MuestraVital> perfil, bool reaccion)
    {
        if (perfil == null || perfil.Count == 0)
        {
            Debug.LogError("MonitorTextos: el perfil solicitado no contiene datos válidos.");
            return;
        }

        // Las cuatro llamadas del instructor comparten una sola transición.
        // Repetir la misma orden no reinicia la muestra ni el reloj del perfil.
        if (perfilActivo == perfil && EstadoReaccionActivo == reaccion) return;
        perfilActivo = perfil;
        EstadoReaccionActivo = reaccion;
        MuestraActual = 0;
        AplicarMuestra(perfilActivo[MuestraActual]);

        if (reproduccionCoroutine == null && isActiveAndEnabled)
            reproduccionCoroutine = StartCoroutine(ReproducirPerfil());
    }

    private IEnumerator ReproducirPerfil()
    {
        while (true)
        {
            yield return new WaitForSeconds(segundosPorMuestra);

            if (perfilActivo == null || perfilActivo.Count == 0)
                continue;

            int siguiente = MuestraActual + 1;
            if (siguiente >= perfilActivo.Count)
            {
                if (!repetirPerfil)
                    continue;
                siguiente = 0;
            }

            MuestraActual = siguiente;
            AplicarMuestra(perfilActivo[MuestraActual]);
        }
    }

    private void AplicarMuestra(MuestraVital muestra)
    {
        AplicarSignosVitales(
            muestra.bpm,
            muestra.oxigeno,
            muestra.sistolica,
            muestra.diastolica,
            muestra.respiracion,
            muestra.temperatura);
    }

    private void ParsearCsv(
        TextAsset archivo,
        List<MuestraVital> destino,
        string nombrePerfil)
    {
        destino.Clear();

        if (archivo == null)
        {
            Debug.LogError(
                "MonitorTextos: no se encontró el CSV de " + nombrePerfil +
                ". Verifica Assets/Resources/PhysioNet.");
            return;
        }

        string[] lineas = archivo.text.Split('\n');
        for (int i = 1; i < lineas.Length; i++)
        {
            string linea = lineas[i].Trim();
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            string[] columnas = linea.Split(',');
            if (columnas.Length < 9)
            {
                Debug.LogWarning(
                    "MonitorTextos: fila " + (i + 1) + " incompleta en " +
                    archivo.name + ".");
                continue;
            }

            MuestraVital muestra;
            if (!int.TryParse(columnas[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out muestra.sistolica) ||
                !int.TryParse(columnas[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out muestra.diastolica) ||
                !int.TryParse(columnas[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out muestra.bpm) ||
                !int.TryParse(columnas[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out muestra.respiracion) ||
                !float.TryParse(columnas[7], NumberStyles.Float, CultureInfo.InvariantCulture, out muestra.temperatura) ||
                !int.TryParse(columnas[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out muestra.oxigeno))
            {
                Debug.LogWarning(
                    "MonitorTextos: fila " + (i + 1) + " inválida en " +
                    archivo.name + ".");
                continue;
            }

            destino.Add(muestra);
        }

        if (destino.Count == 0)
            Debug.LogError("MonitorTextos: no se pudo leer " + archivo.name + ".");
    }
}
