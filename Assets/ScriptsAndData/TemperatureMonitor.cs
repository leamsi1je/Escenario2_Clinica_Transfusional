using UnityEngine;
using System.Collections;
using TMPro;

public class TemperatureMonitor : MonoBehaviour
{
    [Header("Referencias Monitor")]
    public TextMeshProUGUI textoTemperatura; 
    public TextAsset temperatureFile; 

    [Header("Referencias Paciente")]
    public Animator pacienteAnimator;
    public SkinnedMeshRenderer rendererCabeza;

    [Header("Configuración Fiebre (Animación)")]
    public string nombreEstadoAnimacionFiebre = "FiebreSevera";
    public string nombreEstadoAnimacionNormal = "Idle";

    [Header("Configuración Fiebre (Lógica y Visual)")]
    public float updateDelay = 2f; 
    public float temperaturaMaxima = 40.0f;
    public float incrementoFiebre = 0.5f;
    public float intervaloFiebre = 1.0f; 
    
    [Tooltip("Temperatura a la que empieza a aparecer el rubor")]
    public float tempInicioRubor = 38.0f; 
    
    [Tooltip("Temperatura a la que el rubor llega a su máximo")]
    public float tempMaxRubor = 40.0f;
    
    [Tooltip("Temperatura a la que el monitor se pone rojo")]
    public float temperaturaAlerta = 39.0f;

    [Tooltip("Temperatura normal a la que vuelve el paciente al desactivar la fiebre")]
    public float temperaturaNormal = 36.5f;

    [Header("Configuración Oscilación")]
    [Tooltip("Cuánto subirá o bajará la temperatura en el pico máximo (ej. 0.2 = de 39.8 a 40.2)")]
    public float variacionMaximaOscilacion = 0.2f; 
    [Tooltip("Cada cuántos segundos fluctúa el número")]
    public float tiempoEntreOscilaciones = 2.5f; 

    private float temperaturaActual = 36.5f; 
    private bool fiebreActivada = false;
    private bool llegoAlPicoFiebre = false; // Variable de control para la oscilación
    private Color colorInicialTexto; 

    // Referencias a las corrutinas activas, para poder frenarlas al desactivar la fiebre.
    private Coroutine csvCoroutine;
    private Coroutine subirTemperaturaCoroutine;
    private Coroutine oscilarCoroutine;

    // Propiedad del Shader URP que regula la opacidad/intensidad del detalle
    private const string PROPIEDAD_INTENSIDAD_DETALLE = "_DetailAlbedoMapScale";

    void Start()
    {
        if (textoTemperatura != null) colorInicialTexto = textoTemperatura.color; 
        
        // Asegurarnos de que el rubor empiece en 0 (totalmente invisible)
        ActualizarIntensidadRubor(0f);
        
        ActualizarTextoYVisuales();
        if (temperatureFile != null) csvCoroutine = StartCoroutine(ReadTemperatureCSV());
    }

    public void InducirFiebreAdversa()
    {
        if (!fiebreActivada)
        {
            fiebreActivada = true; 
            llegoAlPicoFiebre = false; // Reiniciamos el estado por si acaso
            if (csvCoroutine != null) StopCoroutine(csvCoroutine);

            if (pacienteAnimator != null)
            {
                pacienteAnimator.CrossFadeInFixedTime(nombreEstadoAnimacionFiebre, 0.5f);
            }

            temperaturaActual = 36.5f;
            ActualizarTextoYVisuales(); 
            subirTemperaturaCoroutine = StartCoroutine(SubirTemperaturaGradualmente()); 
        }
    }

    /// <summary>
    /// Desactiva la fiebre inducida y devuelve al paciente a su estado normal:
    /// frena las corrutinas de fiebre/oscilación, vuelve la temperatura a su valor
    /// normal, quita el rubor, regresa la animación a Idle y reanuda la lectura
    /// normal del CSV de temperatura.
    /// </summary>
    public void DesactivarFiebreAdversa()
    {
        if (!fiebreActivada) return; // ya está normal, no hay nada que desactivar

        fiebreActivada = false;
        llegoAlPicoFiebre = false;

        // Frenar las corrutinas de fiebre y oscilación si siguen corriendo.
        if (subirTemperaturaCoroutine != null)
        {
            StopCoroutine(subirTemperaturaCoroutine);
            subirTemperaturaCoroutine = null;
        }
        if (oscilarCoroutine != null)
        {
            StopCoroutine(oscilarCoroutine);
            oscilarCoroutine = null;
        }

        // Volver la animación del paciente a su estado normal.
        if (pacienteAnimator != null)
        {
            pacienteAnimator.CrossFadeInFixedTime(nombreEstadoAnimacionNormal, 0.5f);
        }

        // Resetear temperatura y visuales (texto, color, rubor) a valores normales.
        temperaturaActual = temperaturaNormal;
        ActualizarTextoYVisuales();

        // Reanudar la lectura normal del CSV de temperatura.
        if (csvCoroutine != null) StopCoroutine(csvCoroutine);
        if (temperatureFile != null) csvCoroutine = StartCoroutine(ReadTemperatureCSV());
    }

    IEnumerator SubirTemperaturaGradualmente()
    {
        while (temperaturaActual < temperaturaMaxima)
        {
            yield return new WaitForSeconds(intervaloFiebre); 
            temperaturaActual += incrementoFiebre; 
            
            if (temperaturaActual >= temperaturaMaxima) 
            {
                temperaturaActual = temperaturaMaxima;
                llegoAlPicoFiebre = true; // Llegó al tope, iniciamos oscilación
                oscilarCoroutine = StartCoroutine(OscilarTemperaturaEnPico());
            }

            ActualizarTextoYVisuales(); 
        }
    }

    IEnumerator OscilarTemperaturaEnPico()
    {
        while (llegoAlPicoFiebre) // Mientras el paciente siga en estado febril máximo
        {
            yield return new WaitForSeconds(tiempoEntreOscilaciones);
            
            // Genera un ligero cambio aleatorio (ej. +/- 0.2 grados)
            float fluctuacion = Random.Range(-variacionMaximaOscilacion, variacionMaximaOscilacion);
            temperaturaActual = temperaturaMaxima + fluctuacion;
            
            ActualizarTextoYVisuales();
        }
    }

    private void ActualizarTextoYVisuales()
    {
        if (textoTemperatura != null)
        {
            textoTemperatura.text = temperaturaActual.ToString("F1") + " °C"; 
            
            // El monitor sigue saltando a rojo solo a los 39°C
            textoTemperatura.color = (temperaturaActual >= temperaturaAlerta) ? Color.red : colorInicialTexto; 
        }

        // --- LÓGICA PROGRESIVA DEL RUBOR ---
        // InverseLerp calcula automáticamente un valor entre 0 y 1 basado en la temperatura actual
        float intensidad = Mathf.InverseLerp(tempInicioRubor, tempMaxRubor, temperaturaActual);
        ActualizarIntensidadRubor(intensidad);
    }

    private void ActualizarIntensidadRubor(float intensidad)
    {
        if (rendererCabeza != null && rendererCabeza.material != null)
        {
            // Le pasamos el nivel de intensidad (0.0 a 1.0) directamente al material
            rendererCabeza.material.SetFloat(PROPIEDAD_INTENSIDAD_DETALLE, intensidad);
        }
    }

    IEnumerator ReadTemperatureCSV()
    {
        if (temperatureFile == null) yield break;
        string[] lines = temperatureFile.text.Split('\n');
        
        while (!fiebreActivada) 
        {
            foreach (string line in lines)
            {
                if (fiebreActivada) yield break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (float.TryParse(line.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float tempValue))
                {
                    if (tempValue > 30.0f && tempValue < 45.0f) 
                    {
                        temperaturaActual = tempValue;
                        ActualizarTextoYVisuales(); 
                    }
                }
                yield return new WaitForSeconds(updateDelay);
            }
            yield return null; 
        }
    }
}