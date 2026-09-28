using Fusion;
using UnityEngine;

public class NetworkXRAvatar : NetworkBehaviour
{
    [Header("Network Rig")]
    [SerializeField] private Transform networkHead;
    [SerializeField] private Transform networkLeftHand;
    [SerializeField] private Transform networkRightHand;

    [Header("Visuals hidden on local Quest user")]
    [SerializeField] private Renderer[] localHiddenRenderers;

    private HardwareRigReferences hardware;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        hardware = HardwareRigReferences.Instance;

        if (hardware == null)
        {
            Debug.LogError(
                "HardwareRigReferences not found. " +
                "Check the XR Origin."
            );
            return;
        }

        foreach (Renderer r in localHiddenRenderers)
        {
            if (r != null)
                r.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (hardware == null)
            return;

        // XR Origin
        transform.SetPositionAndRotation(
            hardware.rigRoot.position,
            hardware.rigRoot.rotation
        );

        // Head
        networkHead.SetPositionAndRotation(
            hardware.head.position,
            hardware.head.rotation
        );

        // Left hand
        networkLeftHand.SetPositionAndRotation(
            hardware.leftHand.position,
            hardware.leftHand.rotation
        );

        // Right hand
        networkRightHand.SetPositionAndRotation(
            hardware.rightHand.position,
            hardware.rightHand.rotation
        );
    }
}

