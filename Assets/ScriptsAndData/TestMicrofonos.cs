using System.Collections;
using UnityEngine;

public class TestMicrofonos : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        foreach (var dispositivo in Microphone.devices)
        {
            Debug.Log("[TEST] HABLA AHORA (3 segundos) con: " + dispositivo);

            AudioClip clip = Microphone.Start(dispositivo, false, 3, 16000);
            yield return new WaitForSeconds(3.2f);
            Microphone.End(dispositivo);

            if (clip == null)
            {
                Debug.Log("[TEST] " + dispositivo + " -> no se pudo abrir");
                continue;
            }

            float[] datos = new float[clip.samples * clip.channels];
            clip.GetData(datos, 0);

            float pico = 0f;
            foreach (float d in datos)
                pico = Mathf.Max(pico, Mathf.Abs(d));

            Debug.Log("[TEST] " + dispositivo + " -> volumen máximo: " + pico.ToString("F4"));
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("[TEST] Terminó la prueba");
    }
}