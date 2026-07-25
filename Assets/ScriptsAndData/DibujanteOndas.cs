using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChartAndGraph;
public class DibujanteOndas : MonoBehaviour

{

    public enum TipoOnda { ECG, PPG, PresionArterial, Respiracion }



    [Header("Referencias")]

    public VitalSignsManager managerDatos;

    public GraphChart grafico;



    [Header("Configuración de Señal")]

    public TipoOnda canalDeDatos = TipoOnda.ECG;

    [Tooltip("Debe coincidir EXACTAMENTE con el nombre en Data -> Categories")]

    public string nombreCategoria = "PPG";



    private float puntosPorSegundo = 125f;

    private float tiempoEntrePuntos;

    private float temporizador = 0f;

    private int indiceActual = 0;

    private float posicionX = 0f;

    private float limitePantallaX = 5f;



    void Start()

    {

        if (grafico == null) grafico = GetComponent<GraphChart>();

        tiempoEntrePuntos = 1f / puntosPorSegundo;



        LimpiarGrafico();

    }



    void Update()

    {

        if (managerDatos == null) return;



        // Validar que la lista correspondiente tenga datos cargados

        List<float> listaValores = ObtenerListaSegunCanal();

        if (listaValores == null || listaValores.Count == 0) return;



        temporizador += Time.deltaTime;



        while (temporizador >= tiempoEntrePuntos)

        {

            temporizador -= tiempoEntrePuntos;



            // 1. Efecto Barrido: al llegar a 5 segundos limpia la pantalla

            if (posicionX >= limitePantallaX)

            {

                posicionX = 0f;

                LimpiarGrafico();

            }



            // 2. Extraer el dato correspondiente de la lista

            float valorY = listaValores[indiceActual];

            grafico.DataSource.AddPointToCategory(nombreCategoria, posicionX, valorY);



            // 3. Avanzar X e índice

            posicionX += tiempoEntrePuntos;

            indiceActual++;



            if (indiceActual >= listaValores.Count)

            {

                indiceActual = 0;

            }

        }

    }



    List<float> ObtenerListaSegunCanal()

    {

        switch (canalDeDatos)

        {

            case TipoOnda.ECG: return managerDatos.frecuenciaCardiaca;

            case TipoOnda.PPG: return managerDatos.saturacionOxigeno;

            case TipoOnda.PresionArterial: return managerDatos.presionArterial;

            case TipoOnda.Respiracion: return managerDatos.frecuenciaRespiratoria;

            default: return managerDatos.frecuenciaCardiaca;

        }

    }



    void LimpiarGrafico()

    {

        if (grafico != null && grafico.DataSource.HasCategory(nombreCategoria))

        {

            grafico.DataSource.ClearCategory(nombreCategoria);

        }

    }

}