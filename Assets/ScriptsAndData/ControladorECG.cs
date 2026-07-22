using UnityEngine;
using System.Collections;
using ChartAndGraph;
using TMPro;

public class ControladorECG : MonoBehaviour
{
    [Header("Referencias")]
    public GraphChart graph;
    public TextAsset ecgFile;
    public TextMeshProUGUI textBPM;

    [Header("Configuración Onda")]
    public string categoryName = "ECG";
    public float updateDelay = 0.01f;
    public int puntosPorFrame = 3;
    public float tiempoMaximoPantalla = 5f;

    [Header("Lógica de BPM (Cálculo Médico)")]
    [Tooltip("Valor fijo que se muestra al iniciar la simulación")]
    public int bpmValorInicial = 75; 
    
    [Tooltip("No se mostrarán valores por debajo de este límite")]
    public float bpmMinimo = 65f;

    [Tooltip("Como tus picos llegan a 70, un umbral de 40-50 es ideal")]
    public float umbralDeteccion = 50f; 

    [Header("Configuración Taquicardia (Lógica y Visual)")]
    public float bpmMaximoAdverso = 130f;
    public float incrementoBpm = 2f;
    public float intervaloBpm = 1.0f;
    [Tooltip("BPM a partir del cual el monitor se pone rojo")]
    public float bpmAlerta = 100f;
    
    [Header("Configuración Oscilación")]
    public float variacionMaximaOscilacion = 2f; // El BPM oscilará en +/- 2 (ej. 128 a 132)
    public float tiempoEntreOscilaciones = 2f; // Cada cuántos segundos cambia el número en el pico

    private float tiempoUltimoPico = 0f;
    private bool puedeDetectar = true;

    // --- VARIABLES DE CONTROL ---
    private bool taquicardiaActivada = false;
    private bool llegoAlPicoAdverso = false; 
    private float bpmActual = 75f;
    private Color colorInicialTexto;
    private int puntosPorFrameBase;

    // Referencias a las corrutinas de taquicardia, para poder frenarlas al desactivar.
    private Coroutine subirBpmCoroutine;
    private Coroutine oscilarBpmCoroutine;

    void Start()
    {
        if (graph == null || ecgFile == null)
        {
            Debug.LogError("Asigna el Graph y el CSV en el Inspector");
            return;
        }

        if (textBPM != null)
        {
            colorInicialTexto = textBPM.color;
            bpmActual = bpmValorInicial;
            ActualizarTextoBPM();
        }
        
        puntosPorFrameBase = puntosPorFrame;

        StartCoroutine(ReadECGCSV());
    }

    public void InducirTaquicardiaAdversa()
    {
        if (!taquicardiaActivada)
        {
            taquicardiaActivada = true; 
            llegoAlPicoAdverso = false;
            subirBpmCoroutine = StartCoroutine(SubirBPMGradualmente()); 
        }
    }

    /// <summary>
    /// Desactiva la taquicardia inducida y devuelve el BPM a la normalidad:
    /// frena las corrutinas de subida/oscilación y deja que la detección normal
    /// de picos del ECG (CalcularBPM) vuelva a tomar el control del valor mostrado.
    /// </summary>
    public void DesactivarTaquicardiaAdversa()
    {
        if (!taquicardiaActivada) return; // ya está normal, no hay nada que desactivar

        taquicardiaActivada = false;
        llegoAlPicoAdverso = false;

        if (subirBpmCoroutine != null)
        {
            StopCoroutine(subirBpmCoroutine);
            subirBpmCoroutine = null;
        }
        if (oscilarBpmCoroutine != null)
        {
            StopCoroutine(oscilarBpmCoroutine);
            oscilarBpmCoroutine = null;
        }

        // Volver el BPM mostrado a su valor inicial normal.
        bpmActual = bpmValorInicial;
        ActualizarTextoBPM();

        // Resetear el estado de detección de picos para que arranque limpio.
        tiempoUltimoPico = 0f;
        puedeDetectar = true;
    }

    IEnumerator SubirBPMGradualmente()
    {
        while (bpmActual < bpmMaximoAdverso)
        {
            yield return new WaitForSeconds(intervaloBpm); 
            bpmActual += incrementoBpm; 
            
            if (bpmActual >= bpmMaximoAdverso) 
            {
                bpmActual = bpmMaximoAdverso;
                llegoAlPicoAdverso = true; 
                oscilarBpmCoroutine = StartCoroutine(OscilarBPMEnPico());
            }

            ActualizarTextoBPM(); 
        }
    }

    IEnumerator OscilarBPMEnPico()
    {
        while (llegoAlPicoAdverso) 
        {
            yield return new WaitForSeconds(tiempoEntreOscilaciones);
            
            float fluctuacion = Random.Range(-variacionMaximaOscilacion, variacionMaximaOscilacion);
            bpmActual = bpmMaximoAdverso + fluctuacion;
            
            ActualizarTextoBPM();
        }
    }

    private void ActualizarTextoBPM()
    {
        if (textBPM != null)
        {
            textBPM.text = Mathf.RoundToInt(bpmActual).ToString() + " Bpm"; 
            textBPM.color = (bpmActual >= bpmAlerta) ? Color.red : colorInicialTexto; 
        }
    }

    IEnumerator ReadECGCSV()
    {
        string[] lines = ecgFile.text.Split('\n');
        float currentTimeX = 0f;
        float tiempoAbsoluto = 0f;

        while (true)
        {
            int contadorFrame = 0;
            
            foreach (string line in lines)
            {
                // CORRECCIÓN: El cálculo de la velocidad ahora está DENTRO del bucle.
                // Esto permite que la gráfica acelere en tiempo real mientras el CSV se está leyendo.
                float multiplicadorVelocidad = Mathf.Pow(bpmActual / (float)bpmValorInicial, 1.5f);
                int puntosDinamicos = Mathf.Max(1, Mathf.RoundToInt(puntosPorFrameBase * multiplicadorVelocidad));

                if (string.IsNullOrWhiteSpace(line)) continue;

                if (float.TryParse(line.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float yValue))
                {
                    graph.DataSource.AddPointToCategory(categoryName, currentTimeX, yValue);
                    CalcularBPM(yValue, tiempoAbsoluto);

                    currentTimeX += updateDelay;
                    tiempoAbsoluto += updateDelay;
                    contadorFrame++;
                }

                if (currentTimeX >= tiempoMaximoPantalla)
                {
                    currentTimeX = 0f;
                    graph.DataSource.ClearCategory(categoryName);
                }

                if (contadorFrame >= puntosDinamicos)
                {
                    yield return null;
                    contadorFrame = 0;
                }
            }
            yield return null;
        }
    }

    void CalcularBPM(float valorY, float tiempoActual)
    {
        if (valorY > umbralDeteccion && puedeDetectar && !taquicardiaActivada)
        {
            float intervalo = tiempoActual - tiempoUltimoPico;

            if (intervalo > 0.3f) 
            {
                float bpmCalculado = 60f / intervalo;
                
                if (bpmCalculado >= bpmMinimo)
                {
                    bpmActual = bpmCalculado; 
                    ActualizarTextoBPM();
                }

                tiempoUltimoPico = tiempoActual;
                puedeDetectar = false; 
                StartCoroutine(ResetDeteccion());
            }
        }
    }

    IEnumerator ResetDeteccion()
    {
        yield return new WaitForSeconds(0.2f);
        puedeDetectar = true;
    }
} 