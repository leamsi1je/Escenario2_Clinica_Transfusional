using UnityEngine;

public class NavFormulario : MonoBehaviour
{
    public GameObject panelPaciente;
    public GameObject panelAntecedentes;
    public GameObject panelSignos;
    public GameObject panelSintomas;
    public GameObject panelTratamiento;

    void Start()
    {
        MostrarPanel(panelPaciente);
    }

    public void MostrarPaciente()     { MostrarPanel(panelPaciente); }
    public void MostrarAntecedentes() { MostrarPanel(panelAntecedentes); }
    public void MostrarSignos()       { MostrarPanel(panelSignos); }
    public void MostrarSintomas()     { MostrarPanel(panelSintomas); }
    public void MostrarTratamiento()  { MostrarPanel(panelTratamiento); }

    void MostrarPanel(GameObject panel)
    {
        panelPaciente.SetActive(false);
        panelAntecedentes.SetActive(false);
        panelSignos.SetActive(false);
        panelSintomas.SetActive(false);
        panelTratamiento.SetActive(false);
        panel.SetActive(true);
    }
}