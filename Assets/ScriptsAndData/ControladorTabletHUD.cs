using UnityEngine;
using System.Collections;

public class ControladorTabletHUD : MonoBehaviour
{
    [Header("Referencias del Panel")]
    public GameObject panelPrincipal;

    [Header("Configuración de Aparición")]
    public float distanciaPanel = 1.2f;
    public float alturaExtra = 0.0f;

    private Transform camaraVR;
    private bool panelActivo = false;

    void Start()
    {
        BuscarCamaraVR();
        if (panelPrincipal != null)
            panelPrincipal.SetActive(false);
    }

    private void BuscarCamaraVR()
    {
        if (Camera.main != null)
        {
            camaraVR = Camera.main.transform;
        }
    }

    // El Update ya NO tiene la lógica de seguimiento (Lazy Follow) para que el panel se quede fijo.

    public void AlternarPanelYControles()
    {
        if (panelPrincipal == null) return;
        if (camaraVR == null) BuscarCamaraVR();

        panelActivo = !panelActivo;

        if (panelActivo)
        {
            // Coloca el panel exactamente al frente del jugador en el momento de abrirse
            Vector3 posicionFrente = camaraVR.position + (camaraVR.forward * distanciaPanel);
            posicionFrente.y += alturaExtra;
            panelPrincipal.transform.position = posicionFrente;

            // Orientar hacia el jugador + girar 180 en Y para mostrar la cara frontal
            Vector3 direccionMirada = camaraVR.position - posicionFrente;
            direccionMirada.y = 0;
            if (direccionMirada != Vector3.zero)
            {
                panelPrincipal.transform.rotation = Quaternion.LookRotation(direccionMirada);
                panelPrincipal.transform.Rotate(0, 180, 0);
            }

            panelPrincipal.SetActive(true);
        }
        else
        {
            panelPrincipal.SetActive(false);
        }
    }
}