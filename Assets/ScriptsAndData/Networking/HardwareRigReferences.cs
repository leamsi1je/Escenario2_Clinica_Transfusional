using UnityEngine;

public class HardwareRigReferences : MonoBehaviour
{
    public static HardwareRigReferences Instance { get; private set; }

    [Header("XR Tracking References")]
    public Transform rigRoot;
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private void Awake()
    {
        Instance = this;
        HideTrackedHandMesh(leftHand);
        HideTrackedHandMesh(rightHand);
    }

    private static void HideTrackedHandMesh(Transform hand)
    {
        if (hand == null)
            return;

        // Keep tracking active and let the rig's interaction hand provide the visible mesh.
        foreach (OVRMeshRenderer visual in hand.GetComponentsInChildren<OVRMeshRenderer>(true))
        {
            if (visual.TryGetComponent(out SkinnedMeshRenderer mesh))
                mesh.forceRenderingOff = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}