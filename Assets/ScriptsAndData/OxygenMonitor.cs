using UnityEngine;
using System.Collections;
using TMPro;

public class OxygenMonitor : MonoBehaviour
{
    public MonitorTextos perfilClinico;
    void OnEnable()
    {
        if (perfilClinico != null) perfilClinico.alActualizarSignosVitales.AddListener(SincronizarPerfilClinico);
    }
    void OnDisable()
    {
        if (perfilClinico != null) perfilClinico.alActualizarSignosVitales.RemoveListener(SincronizarPerfilClinico);
    }
    void SincronizarPerfilClinico()
    {
        if (perfilClinico != null && perfilClinico.BPMActual > 0 && textoSaturacion != null)
            textoSaturacion.text = perfilClinico.OxigenoActual + " %";
    }

    [Header("Referencias")]
    // Ahora pedimos un texto en lugar de un gráfico
    public TextMeshProUGUI textoSaturacion; 
    public TextAsset oxygenFile; 

    [Header("Configuración")]
    // Como son números, no necesitamos que cambie a la velocidad de la luz
    // 0.5 o 1 segundo de retraso se ve mucho más natural en un número.
    public float updateDelay = 1f; 

    void Start()
    {
        if (perfilClinico != null) { SincronizarPerfilClinico(); return; }
        if (textoSaturacion == null || oxygenFile == null)
        {
            Debug.LogError("Falta asignar el Texto o el archivo CSV en el Inspector");
            return;
        }

        StartCoroutine(ReadOxygenCSV());
    }

    IEnumerator ReadOxygenCSV()
    {
        string[] lines = oxygenFile.text.Split('\n');
        
        while (true) 
        {
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (float.TryParse(line.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float yValue))
                {
                    // Convertimos el número a texto. 
                    // ToString("0") le quita los decimales para que se vea limpio (ej: 98 en lugar de 98.6)
                    // Le agregamos el "%" al final.
                    textoSaturacion.text = yValue.ToString("0") + " %"; 
                }

                // Esperamos el tiempo definido antes de mostrar el siguiente número
                yield return new WaitForSeconds(updateDelay);
            }
            
            yield return null; 
        }
    }
}