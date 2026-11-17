using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(HardwareRigReferences))]
public sealed class StudentXRLocomotion : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 1.35f;
    [SerializeField, Range(15f, 90f)] private float turnAngle = 30f;

    private CharacterController body;
    private Transform head;
    private bool turnWasPressed;

    private void Awake()
    {
        body = GetComponent<CharacterController>();
        head = GetComponent<HardwareRigReferences>().head;
    }

    private void Update()
    {
        if (head == null || !OVRManager.hasVrFocus)
            return;

        float turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
        if (!turnWasPressed && Mathf.Abs(turn) >= .7f)
        {
            transform.RotateAround(head.position, Vector3.up, Mathf.Sign(turn) * turnAngle);
            turnWasPressed = true;
        }
        else if (Mathf.Abs(turn) <= .3f)
        {
            turnWasPressed = false;
        }

        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        if (stick.sqrMagnitude <= .04f)
            return;

        Vector3 headOffset = transform.InverseTransformPoint(head.position);
        body.height = Mathf.Clamp(headOffset.y, .9f, 2.2f);
        body.center = new Vector3(headOffset.x, body.height * .5f + .02f, headOffset.z);

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = right * stick.x + forward * stick.y;
        body.Move(Vector3.ClampMagnitude(direction, 1f) * (speed * Time.deltaTime));
    }
}
