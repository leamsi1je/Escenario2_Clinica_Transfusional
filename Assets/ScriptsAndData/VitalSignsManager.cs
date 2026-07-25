using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VitalSignsManager : MonoBehaviour
{
    [Header("Archivo de Datos")]
    [Tooltip("Arrastra aquí el archivo paciente_perfecto_unity.csv")]
    public TextAsset archivoCSV;

    [Header("Ondas Extraídas (Listas)")]
    public List<float> frecuenciaCardiaca = new List<float>();
    public List<float> presionArterial = new List<float>();
    public List<float> frecuenciaRespiratoria = new List<float>();
    public List<float> saturacionOxigeno = new List<float>();

    void Start()
    {
        if (archivoCSV != null)
        {
            LeerDatosMedicos();
        }
        else
        {
            Debug.LogError("¡Falta asignar el archivo CSV en el Inspector!");
        }
    }

    void LeerDatosMedicos()
    {
        // 1. Separamos el texto del archivo por saltos de línea
        string[] lineas = archivoCSV.text.Split('\n');

        // 2. Empezamos desde 1 para saltarnos la primera fila (los nombres de las columnas)
        for (int i = 1; i < lineas.Length; i++)
        {
            // Evitamos leer líneas vacías al final del archivo
            if (string.IsNullOrWhiteSpace(lineas[i])) continue;

            // 3. Separamos cada valor usando la coma del CSV
            string[] valores = lineas[i].Split(',');

            if (valores.Length >= 4)
            {
                // 4. Convertimos el texto a decimal (usando CultureInfo para evitar errores con los puntos y comas)
                frecuenciaCardiaca.Add(float.Parse(valores[0], System.Globalization.CultureInfo.InvariantCulture));
                presionArterial.Add(float.Parse(valores[1], System.Globalization.CultureInfo.InvariantCulture));
                frecuenciaRespiratoria.Add(float.Parse(valores[2], System.Globalization.CultureInfo.InvariantCulture));
                saturacionOxigeno.Add(float.Parse(valores[3], System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        Debug.Log($"¡Carga exitosa! Se cargaron {frecuenciaCardiaca.Count} muestras por cada signo vital.");
    }
}