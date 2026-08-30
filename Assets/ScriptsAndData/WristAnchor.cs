using UnityEngine;
using Oculus.Interaction.Input;

public class WristAnchor : MonoBehaviour
{
    [SerializeField] private Hand hand; // arrastra aquí el objeto que tiene el componente "Hand (Script)"
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.02f, 0.03f);
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(90f, 0f, 90f);
    [SerializeField] private float smoothSpeed = 15f;

    void LateUpdate()
    {
        if (hand == null) return;

        if (!hand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose))
        {
            return; // la mano no está trackeada este frame, no actualizamos
        }

        Vector3 targetPos = wristPose.position + wristPose.rotation * positionOffset;
        Quaternion targetRot = wristPose.rotation * Quaternion.Euler(rotationOffsetEuler);

        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, smoothSpeed * Time.deltaTime);
    }
}