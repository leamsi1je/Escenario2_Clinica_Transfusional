using UnityEngine;

public class AvatarBodyFollower : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private Transform body;

    [SerializeField] private float verticalOffset = 0.65f;

    private void LateUpdate()
    {
        if (head == null || body == null)
            return;

        body.position =
            head.position -
            Vector3.up * verticalOffset;

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.001f)
        {
            body.rotation =
                Quaternion.LookRotation(
                    forward.normalized,
                    Vector3.up
                );
        }
    }
}
