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
    public string nombreCategoria = "PresionArterial";

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
        if (managerDatos == null || grafico == null) return;

        // 1. Obtener la lista correspondiente
        List<float> listaValores = ObtenerListaSegunCanal();

        // RESPALDO: Si la lista de presión está vacía, usamos el ECG para que no haya pantalla negra
        if (listaValores == null || listaValores.Count == 0)
        {
            listaValores = managerDatos.frecuenciaCardiaca;
        }

        if (listaValores == null || listaValores.Count == 0) return;

        temporizador += Time.deltaTime;

        while (temporizador >= tiempoEntrePuntos)
        {
            temporizador -= tiempoEntrePuntos;

            // Barrido de pantalla
            if (posicionX >= limitePantallaX)
            {
                posicionX = 0f;
                LimpiarGrafico();
            }

            // 2. Solo dibuja si la categoría realmente existe
            if (grafico.DataSource.HasCategory(nombreCategoria))
            {
                float valorY = listaValores[indiceActual];

                // 3. AJUSTE DE ESCALA (Crucial para que la Presión Arterial no se salga de la pantalla)
                if (valorY > 10f)
                {
                    valorY = (valorY - 70f) / 65f;
                }
                else
                {
                    valorY = valorY * 0.7f;
                }

                grafico.DataSource.AddPointToCategory(nombreCategoria, posicionX, valorY);
            }

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