using Fusion;
using Fusion.Photon.Realtime;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public enum AppRole
{
    Doctor,
    Student
}

[RequireComponent(typeof(NetworkRunner))]
[RequireComponent(typeof(NetworkSceneManagerDefault))]
public class FusionBootstrap : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    private enum EditorRole
    {
        Doctor,
        Student,
        Automatic
    }

    [Header("Session")]
    [SerializeField] private string roomName = "TransfusionRoom01";

    [Header("Network Player Prefabs")]
    [SerializeField] private GameObject doctorPlayerPrefab;
    [SerializeField] private GameObject studentPlayerPrefab;

    [Header("Local rigs")]
    [SerializeField] private GameObject doctorObserverRig;
    [SerializeField] private GameObject studentXRRig;

    [Header("Escenario 2")]
    [SerializeField] private GameObject doctorControlPanel;
    [SerializeField] private MonitorTextos monitor;
    [SerializeField] private ControladorInfusionVisual infusion;
    [SerializeField] private TransfusionVoiceControl patientVoice;

    [Header("Testing inside Unity Editor")]
    [Tooltip("Automatic uses the Student rig when an XR display is running. Select Doctor or Student to override it.")]
    [SerializeField] private EditorRole editorRole = EditorRole.Automatic;
#if UNITY_EDITOR
    [Tooltip("Activar para conectar el Student del Editor a la sala que abrió Doctor. Desactivado permite probar el visor y los mandos en local.")]
    [SerializeField] private bool connectStudentInEditor;
#endif

    private NetworkRunner runner;

    private bool IsLocalStudentDemo
    {
        get
        {
#if UNITY_EDITOR
            return CurrentRole == AppRole.Student && !connectStudentInEditor;
#else
            return false;
#endif
        }
    }

    public static AppRole CurrentRole { get; private set; }

    private void Awake()
    {
        runner = GetComponent<NetworkRunner>();
        CurrentRole = DetermineRole();
        ConfigureLocalRig();
    }

    private async void Start()
    {
        if (IsLocalStudentDemo)
        {
            Debug.Log("[Prueba XR] Student en modo local. El movimiento, las manos y la reacción de demostración están disponibles.", this);
            return;
        }

        try
        {
            await Connect();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Photon] {CurrentRole} failed to start {roomName}: {exception}");
        }
    }

    private AppRole DetermineRole()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return AppRole.Student;
#else
        if (editorRole == EditorRole.Doctor)
            return AppRole.Doctor;
        if (editorRole == EditorRole.Student)
            return AppRole.Student;

        var display = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
        return display != null && display.running ? AppRole.Student : AppRole.Doctor;
#endif
    }

    private void ConfigureLocalRig()
    {
        if (doctorObserverRig != null)
            doctorObserverRig.SetActive(CurrentRole == AppRole.Doctor);

        if (studentXRRig != null)
            studentXRRig.SetActive(CurrentRole == AppRole.Student);

        if (doctorControlPanel != null)
            doctorControlPanel.SetActive(CurrentRole == AppRole.Doctor || IsLocalStudentDemo);

        Debug.Log($"Local role: {CurrentRole}");
    }

    public void ActivateReaction()
    {
        if (IsLocalStudentDemo)
        {
            ApplyReaction();
            return;
        }

        if (CurrentRole != AppRole.Doctor)
            return;

        if (runner.IsRunning && runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject doctor) &&
            doctor.TryGetComponent(out DoctorReactionState reaction))
        {
            reaction.Activate();
            return;
        }

        Debug.LogWarning("[Photon] Espera a que el médico se conecte a la sala para activar la reacción.", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Demostración / Activar reacción local")]
    private void ActivateReactionForEditorDemo()
    {
        if (Application.isPlaying)
            ApplyReaction();
    }
#endif

    internal void ApplyReaction()
    {
        if (monitor != null)
            monitor.MostrarReaccionTransfusional();
        if (infusion != null)
            infusion.NotificarReaccion();
        if (CurrentRole == AppRole.Student && patientVoice != null)
            patientVoice.IniciarInterrogatorio();

        Debug.Log($"[Escenario 2] Reacción aplicada | Rol: {CurrentRole}", this);
    }

    private async Task Connect()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.buildIndex < 0)
        {
            Debug.LogError(
                "The current scene must be added to Build Settings."
            );
            return;
        }

        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();

        sceneInfo.AddSceneRef(
            SceneRef.FromIndex(activeScene.buildIndex),
            LoadSceneMode.Single
        );

        var appSettings = PhotonAppSettings.Global.AppSettings;
        Debug.Log(
            $"[Photon] Connecting | Role: {CurrentRole} | Scene: {activeScene.name} | " +
            $"Room: {roomName} | Requested region: {appSettings.FixedRegion} | " +
            $"Reachability: {Application.internetReachability}"
        );

        StartGameResult result = await runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = roomName,
                PlayerCount = 2,
                EnableClientSessionCreation = CurrentRole == AppRole.Doctor,
                Scene = sceneInfo,
                SceneManager =
                    GetComponent<NetworkSceneManagerDefault>()
            }
        );

        if (result.Ok)
        {
            LogSession("Connected");
        }
        else
        {
            string studentHint = CurrentRole == AppRole.Student
                ? " Abre la sala desde Doctor antes de conectar al estudiante."
                : string.Empty;
            Debug.LogWarning(
                $"[Photon] {CurrentRole} no pudo entrar a {roomName}: {result.ShutdownReason}.{studentHint}", this
            );
        }
    }

    void IPlayerJoined.PlayerJoined(PlayerRef player)
    {
        LogSession($"Player {player} joined");

        if (player != runner.LocalPlayer)
            return;

        GameObject selectedPrefab =
            CurrentRole == AppRole.Doctor
                ? doctorPlayerPrefab
                : studentPlayerPrefab;

        if (selectedPrefab == null)
        {
            Debug.LogError(
                $"No prefab assigned for {CurrentRole}"
            );
            return;
        }

        NetworkObject playerObject =
            runner.Spawn(
                selectedPrefab,
                Vector3.zero,
                Quaternion.identity
            );

        runner.SetPlayerObject(
            player,
            playerObject
        );

        Debug.Log($"[Photon] {CurrentRole} spawned {selectedPrefab.name} for {player}.");
    }

    void IPlayerLeft.PlayerLeft(PlayerRef player)
    {
        LogSession($"Player {player} left");
    }

    private void LogSession(string message)
    {
        var session = runner.SessionInfo;
        if (!session.IsValid)
            return;

        var players = runner.ActivePlayers.ToArray();
        Debug.Log(
            $"[Photon] {message} | Role: {CurrentRole} | Room: {session.Name} | " +
            $"Region: {session.Region} | Players: {players.Length}/{session.MaxPlayers} " +
            $"[{string.Join(", ", players)}]"
        );
    }
}
