using Fusion;
using Fusion.Photon.Realtime;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AppRole
{
    Doctor,
    Student
}

[RequireComponent(typeof(NetworkRunner))]
[RequireComponent(typeof(NetworkSceneManagerDefault))]
public class FusionBootstrap : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [Header("Session")]
    [SerializeField] private string roomName = "TransfusionRoom01";

    [Header("Network Player Prefabs")]
    [SerializeField] private GameObject doctorPlayerPrefab;
    [SerializeField] private GameObject studentPlayerPrefab;

    [Header("Local rigs")]
    [SerializeField] private GameObject doctorObserverRig;
    [SerializeField] private GameObject studentXRRig;

    [Header("Testing inside Unity Editor")]
    [SerializeField] private AppRole editorRole = AppRole.Doctor;

    private NetworkRunner runner;

    public static AppRole CurrentRole { get; private set; }

    private async void Start()
    {
        runner = GetComponent<NetworkRunner>();

        CurrentRole = DetermineRole();

        ConfigureLocalRig();

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
        return editorRole;
#endif
    }

    private void ConfigureLocalRig()
    {
        if (doctorObserverRig != null)
            doctorObserverRig.SetActive(CurrentRole == AppRole.Doctor);

        if (studentXRRig != null)
            studentXRRig.SetActive(CurrentRole == AppRole.Student);

        Debug.Log($"Local role: {CurrentRole}");
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
                ? " Keep the Doctor connected in Unity Editor before launching the APK."
                : string.Empty;
            Debug.LogError(
                $"[Photon] {CurrentRole} could not join {roomName}: {result.ShutdownReason}.{studentHint}"
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
