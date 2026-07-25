using UnityEngine;
using TMPro; // Necesario para TextMeshPro

public class MonitorTextos : MonoBehaviour
{
    [Header("Conecta aquí tus textos de UI")]
    public TextMeshProUGUI textBPM;
    public TextMeshProUGUI textOxigeno;
    public TextMeshProUGUI textPresion;
    public TextMeshProUGUI textRespiracion;
    public TextMeshProUGUI textTemperatura;

    [Header("Valores Base")]
    public int baseBPM = 80;
    public int baseOxigeno = 98;
    public int baseSistolica = 120;
    public int baseDiastolica = 80;
    public int baseRespiracion = 16;
    public float baseTemperatura = 36.5f;

    private float timer = 0f;

    void Start()
    {
        ActualizarTextos();
    }

    void Update()
    {
        // Actualizamos los valores cada 2 segundos para que parezca real
        timer += Time.deltaTime;
        if (timer >= 2f)
        {
            ActualizarTextos();
            timer = 0f;
        }
    }

    void ActualizarTextos()
    {
        // Añadimos pequeñas variaciones aleatorias para simular vida
        int bpmActual = baseBPM + Random.Range(-2, 3);
        int oxigenoActual = Mathf.Clamp(baseOxigeno + Random.Range(-1, 2), 0, 100);
        int sistolicaActual = baseSistolica + Random.Range(-3, 4);
        int diastolicaActual = baseDiastolica + Random.Range(-2, 3);
        int respActual = baseRespiracion + Random.Range(-1, 2);
        float tempActual = baseTemperatura + Random.Range(-0.2f, 0.3f);

        // Asignamos los valores a los textos en pantalla
        if (textBPM != null) textBPM.text = bpmActual.ToString();
        if (textOxigeno != null) textOxigeno.text = oxigenoActual.ToString() + " %";
        if (textPresion != null) textPresion.text = sistolicaActual.ToString() + "/" + diastolicaActual.ToString();
        if (textRespiracion != null) textRespiracion.text = respActual.ToString();
        if (textTemperatura != null) textTemperatura.text = tempActual.ToString("F1") + " °C"; // "F1" muestra 1 decimal
    }
}