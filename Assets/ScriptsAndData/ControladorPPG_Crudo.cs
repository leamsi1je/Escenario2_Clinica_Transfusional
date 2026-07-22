using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using ChartAndGraph;

public class ControladorPPG_Crudo : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí tu objeto 'Grafico_ppg'")]
    public GraphChart graficaPlugin; 
    [Tooltip("Arrastra el nuevo CSV limpio que sacaste de PhysioNet")]
    public TextAsset archivoDatosCSV;

    [Header("Configuración de la Onda")]
    public string CategoryName = "PPG";
    
    [Tooltip("Frecuencia de dibujado. 0.01s a 0.02s es ideal.")]
    public float UpdateDelay = 0.01f;
    
    [Tooltip("Los datos de PhysioNet vienen muy rápido (125Hz). Usa 2 o 3 puntos por frame para que se dibuje a velocidad real.")]
    public int PuntosPorFrame = 2;
    
    public float TiempoMaximoPantalla = 5f;

    [Header("Ajuste de Escala (¡Ajustar al probar!)")]
    [Tooltip("Los datos crudos a veces están descentrados. Modifica esto para subir o bajar la línea en tu pantalla negra.")]
    public float valorBaseY = 0.5f; 
    [Tooltip("Si la onda se ve muy pequeña o muy grande, cambia este número.")]
    public float multiplicadorAmplitud = 2f;

    private List<float> datosPPG = new List<float>();
    private int indiceDato = 0;
    private float temporizador = 0f;
    private float tiempoX = 0f;

    void Start()
    {
        CargarDatosDesdeCSV();
        
        if (graficaPlugin != null)
        {
            graficaPlugin.DataSource.ClearCategory(CategoryName);
        }
    }

    void CargarDatosDesdeCSV()
    {
        if (archivoDatosCSV == null) return;

        string[] lineas = archivoDatosCSV.text.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string linea in lineas)
        {
            if (float.TryParse(linea.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float valorNumerico))
            {
                datosPPG.Add(valorNumerico);
            }
        }
        
        Debug.Log("¡Cargados " + datosPPG.Count + " datos crudos de PhysioNet!");
    }

    void Update()
    {
        if (datosPPG.Count == 0 || graficaPlugin == null) return;

        temporizador += Time.deltaTime;

        if (temporizador >= UpdateDelay)
        {
            temporizador = 0f;

            for (int i = 0; i < PuntosPorFrame; i++)
            {
                // 1. Leemos el dato crudo exacto del paciente
                float valorCrudo = datosPPG[indiceDato];

                // 2. Lo centramos y lo escalamos para que encaje bonito en la pantalla
                float valorY_Final = (valorCrudo - valorBaseY) * multiplicadorAmplitud;

                // 3. Lo inyectamos a tu plugin
                graficaPlugin.DataSource.AddPointToCategory(CategoryName, tiempoX, valorY_Final);
                tiempoX += UpdateDelay;

                // 4. Efecto de arrastre del monitor
                float inicioX = tiempoX - TiempoMaximoPantalla;
                if (inicioX > 0)
                {
                    graficaPlugin.HorizontalScrolling = inicioX; 
                }

                indiceDato++;
                if (indiceDato >= datosPPG.Count)
                {
                    indiceDato = 0; // Hacemos un loop infinito
                }
            }
        }
    }
}