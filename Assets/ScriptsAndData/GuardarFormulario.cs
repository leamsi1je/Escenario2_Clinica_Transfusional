using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class GuardarFormulario : MonoBehaviour
{
    [Header("Panel_Paciente")]
    public TMP_InputField inputNombre;
    public TMP_InputField inputEdad;
    public TMP_InputField inputServicio;
    public TMP_InputField inputDiagnostico;
    public TMP_InputField inputIndicacion;
    public Toggle toggleMasculino;
    public Toggle toggleFemenino;

    [Header("Panel_Antecedentes")]
    public Toggle toggleTransfPrevias;
    public Toggle toggleRATprevias;
    public Toggle toggleEmbarazos;
    public Toggle toggleAlergias;
    public TMP_InputField inputOtros;

    [Header("Panel_SignosVitales")]
    public TMP_InputField inputTempAntes;
    public TMP_InputField inputTempDurante;
    public TMP_InputField inputTempDespues;
    public TMP_InputField inputPASAntes;
    public TMP_InputField inputPASDurante;
    public TMP_InputField inputPASDespues;
    public TMP_InputField inputFCAntes;
    public TMP_InputField inputFCDurante;
    public TMP_InputField inputFCDespues;
    public TMP_InputField inputSatO2Antes;
    public TMP_InputField inputSatO2Durante;
    public TMP_InputField inputSatO2Despues;

    [Header("Panel_Sintomas")]
    public Toggle toggleDolorAbdominal;
    public Toggle toggleFiebre;
    public Toggle toggleEscalofrios;
    public Toggle toggleDisnea;
    public Toggle toggleHipotension;
    public Toggle toggleTaquicardia;
    public Toggle toggleUrticaria;
    public Toggle toggleNausea;
    public Toggle togglePrurito;
    public Toggle toggleShock;
    public TMP_InputField inputDiagPresuntivo;

    [Header("Panel_Tratamiento")]
    public TMP_InputField inputAntipiretico;
    public TMP_InputField inputAntihistaminico;
    public TMP_InputField inputBroncodilatador;
    public TMP_InputField inputDiuretico;
    public TMP_InputField inputEpinefrina;
    public TMP_InputField inputOxigeno;
    public TMP_InputField inputOtrosTrat;
    public TMP_InputField inputComentarios;

    [Header("Panel Formulario")]
    public GameObject panelFormulario;

    public void GuardarYCerrar()
    {
        DatosFormulario datos = new DatosFormulario
        {
            nombre = inputNombre.text,
            edad = inputEdad.text,
            servicio = inputServicio.text,
            diagnostico = inputDiagnostico.text,
            indicacion = inputIndicacion.text,
            sexo = toggleMasculino.isOn ? "Masculino" : "Femenino",
            transfPrevias = toggleTransfPrevias.isOn,
            ratPrevias = toggleRATprevias.isOn,
            embarazos = toggleEmbarazos.isOn,
            alergias = toggleAlergias.isOn,
            otrosAntecedentes = inputOtros.text,
            tempAntes = inputTempAntes.text,
            tempDurante = inputTempDurante.text,
            tempDespues = inputTempDespues.text,
            pasAntes = inputPASAntes.text,
            pasDurante = inputPASDurante.text,
            pasDespues = inputPASDespues.text,
            fcAntes = inputFCAntes.text,
            fcDurante = inputFCDurante.text,
            fcDespues = inputFCDespues.text,
            satO2Antes = inputSatO2Antes.text,
            satO2Durante = inputSatO2Durante.text,
            satO2Despues = inputSatO2Despues.text,
            dolorAbdominal = toggleDolorAbdominal.isOn,
            fiebre = toggleFiebre.isOn,
            escalofrios = toggleEscalofrios.isOn,
            disnea = toggleDisnea.isOn,
            hipotension = toggleHipotension.isOn,
            taquicardia = toggleTaquicardia.isOn,
            urticaria = toggleUrticaria.isOn,
            nausea = toggleNausea.isOn,
            prurito = togglePrurito.isOn,
            shock = toggleShock.isOn,
            diagPresuntivo = inputDiagPresuntivo.text,
            antipiretico = inputAntipiretico.text,
            antihistaminico = inputAntihistaminico.text,
            broncodilatador = inputBroncodilatador.text,
            diuretico = inputDiuretico.text,
            epinefrina = inputEpinefrina.text,
            oxigeno = inputOxigeno.text,
            otrosTratamiento = inputOtrosTrat.text,
            comentarios = inputComentarios.text,
            fechaHora = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        };

        string json = JsonUtility.ToJson(datos, true);
        string ruta = Application.persistentDataPath + "/formulario_" + 
                      System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
        File.WriteAllText(ruta, json);
        Debug.Log("Guardado en: " + ruta);

        panelFormulario.SetActive(false);
    }
}

[System.Serializable]
public class DatosFormulario
{
    public string nombre;
    public string edad;
    public string servicio;
    public string diagnostico;
    public string indicacion;
    public string sexo;
    public bool transfPrevias;
    public bool ratPrevias;
    public bool embarazos;
    public bool alergias;
    public string otrosAntecedentes;
    public string tempAntes;
    public string tempDurante;
    public string tempDespues;
    public string pasAntes;
    public string pasDurante;
    public string pasDespues;
    public string fcAntes;
    public string fcDurante;
    public string fcDespues;
    public string satO2Antes;
    public string satO2Durante;
    public string satO2Despues;
    public bool dolorAbdominal;
    public bool fiebre;
    public bool escalofrios;
    public bool disnea;
    public bool hipotension;
    public bool taquicardia;
    public bool urticaria;
    public bool nausea;
    public bool prurito;
    public bool shock;
    public string diagPresuntivo;
    public string antipiretico;
    public string antihistaminico;
    public string broncodilatador;
    public string diuretico;
    public string epinefrina;
    public string oxigeno;
    public string otrosTratamiento;
    public string comentarios;
    public string fechaHora;
}