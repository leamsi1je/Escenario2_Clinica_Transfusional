using Oculus.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

public class DoctorObserverControls : MonoBehaviour
{
    [SerializeField] private Camera observerCamera;
    [SerializeField] private Collider feverButton;
    [SerializeField] private InteractableUnityEventWrapper feverInteraction;
    [SerializeField] private Transform controlRoomDoor;
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float interactionDistance = 6f;

    private Quaternion initialCameraRotation;
    private float pitch;
    private bool triggerWasPressed;
    private bool doorOpen;

    private void Awake()
    {
        initialCameraRotation = observerCamera.transform.localRotation;
        // La puerta se guarda abierta para que la entrada se vea desde Scene View.
        doorOpen = controlRoomDoor != null && Quaternion.Angle(controlRoomDoor.localRotation, Quaternion.identity) > 1f;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool headsetTracked = head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool lookingWithMouse = !headsetTracked && mouse != null && mouse.rightButton.isPressed;
        if (lookingWithMouse)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(Vector3.up, mouseDelta.x, Space.World);
            pitch = Mathf.Clamp(pitch - mouseDelta.y, -80f, 80f);
            observerCamera.transform.localRotation = initialCameraRotation * Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector2 movement = Vector2.zero;
        if (keyboard != null)
        {
            movement.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            movement.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        }

        if (headsetTracked && leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)
            && stick.sqrMagnitude > 0.04f)
        {
            movement += stick;
        }

        Move(Vector2.ClampMagnitude(movement, 1f));

        bool triggerPressed = headsetTracked &&
            rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
        if (triggerPressed && !triggerWasPressed)
        {
            TryPressFever(observerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));
        }
        triggerWasPressed = triggerPressed;

        if (!headsetTracked && mouse != null && mouse.leftButton.wasPressedThisFrame && !lookingWithMouse)
        {
            Vector2 pointer = mouse.position.ReadValue();
            if (observerCamera.pixelRect.Contains(pointer))
                TryPressFever(observerCamera.ScreenPointToRay(pointer));
        }

        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            PressFever();
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame && controlRoomDoor != null &&
            Vector3.ProjectOnPlane(observerCamera.transform.position - controlRoomDoor.position, Vector3.up).sqrMagnitude <= 4f)
            doorOpen = !doorOpen;

        if (controlRoomDoor != null)
        {
            Quaternion target = Quaternion.Euler(0f, doorOpen ? -85f : 0f, 0f);
            controlRoomDoor.localRotation = Quaternion.RotateTowards(controlRoomDoor.localRotation, target, 170f * Time.deltaTime);
        }
    }

    private void Move(Vector2 movement)
    {
        Vector3 forward = Vector3.ProjectOnPlane(observerCamera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 delta = (right * movement.x + forward * movement.y) * (moveSpeed * Time.deltaTime);
        float distance = delta.magnitude;
        if (distance <= 0f)
            return;

        Vector3 head = observerCamera.transform.position;
        if (Physics.CapsuleCast(head - Vector3.up * 1.35f, head - Vector3.up * .15f, .16f,
                delta / distance, out RaycastHit hit, distance + .02f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - .02f);

        transform.position += delta.normalized * distance;
    }

    private void TryPressFever(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && hit.collider == feverButton)
        {
            PressFever();
        }
    }

    private void PressFever()
    {
        if (feverButton.enabled && feverInteraction.isActiveAndEnabled)
            feverInteraction.WhenSelect.Invoke();
    }
}
