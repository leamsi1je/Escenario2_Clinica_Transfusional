// using UnityEngine;
// using System.Collections;
// using ChartAndGraph; // Asegúrate de tener el asset instalado

// public class OxygenMonitor : MonoBehaviour
// {
//     [Header("Referencias")]
//     public GraphChart graph;
//     public TextAsset oxygen; // Aquí arrastras tu archivo oxygen.csv

//     [Header("Configuración")]
//     public string categoryName = "OXYGEN"; // El nombre que pusiste en el Data del inspector
//     public float updateDelay = 0.05f; // Velocidad de la señal (ajusta a tu gusto)

//     void Start()
//     {
//         if (graph == null || oxygen == null)
//         {
//             Debug.LogError("Falta asignar el Graph o el archivo Oxygen en el Inspector");
//             return;
//         }

//         // Iniciamos la lectura
//         StartCoroutine(ReadOxygenCSV());
//     }

//     IEnumerator ReadOxygenCSV()
//     {
//         // Dividimos el contenido del CSV por líneas
//         string[] lines = oxygen.text.Split('\n');

//         foreach (string line in lines)
//         {
//             if (string.IsNullOrWhiteSpace(line))
//                 continue;

//             // Separamos Tiempo de Valor (asumiendo formato: tiempo,valor)
//             string[] values = line.Split(',');

//             if (values.Length >= 2)
//             {
//                 if (float.TryParse(values[0], out float x) &&
//                     float.TryParse(values[1], out float y))
//                 {
//                     // Normalización (ej: 90–100 → 0.9–1.0)
//                     float normalizedY = y / 100f;

//                     graph.DataSource.AddPointToCategory(categoryName, x, normalizedY);
//                 }
//             }

//             // Simula tiempo real
//             yield return new WaitForSeconds(updateDelay);
//         }
//     }
// }

using UnityEngine;
using System.Collections;
using TMPro; // NUEVO: Librería necesaria para usar TextMeshPro

public class OxygenMonitor : MonoBehaviour
{
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