using Meta.WitAi;
using UnityEngine;

public class PacienteRespuestaManager : MonoBehaviour
{
    public AudioSource pacienteAudioSource;
    public AudioClip respuestaCansadaAgitada;

    private void Awake()
    {
        if (pacienteAudioSource == null)
        {
            pacienteAudioSource = GetComponent<AudioSource>();
        }
    }

    [MatchIntent("preguntar_motivo_consulta")]
    public void OnPreguntarMotivoConsulta()
    {
        Debug.Log("Intención detectada: preguntar_motivo_consulta");
        pacienteAudioSource.clip = respuestaCansadaAgitada;
        pacienteAudioSource.Play();
    }
}