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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}