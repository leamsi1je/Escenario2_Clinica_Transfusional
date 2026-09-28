using Fusion;
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
public class FusionBootstrap : SimulationBehaviour, IPlayerJoined
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

        await Connect();
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

        StartGameResult result = await runner.StartGame(
            new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = roomName,
                PlayerCount = 2,
                Scene = sceneInfo,
                SceneManager =
                    GetComponent<NetworkSceneManagerDefault>()
            }
        );

        if (result.Ok)
        {
            Debug.Log(
                $"Connected to room: {roomName}"
            );
        }
        else
        {
            Debug.LogError(
                $"Photon connection failed: {result.ShutdownReason}"
            );
        }
    }

    void IPlayerJoined.PlayerJoined(PlayerRef player)
    {
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

        Debug.Log(
            $"{CurrentRole} network player spawned."
        );
    }
}