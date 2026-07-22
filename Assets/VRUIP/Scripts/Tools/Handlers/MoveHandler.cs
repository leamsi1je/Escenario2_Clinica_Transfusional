using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VRUIP
{
    public class MoveHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Transform movableObject;
        [SerializeField] private Image handleImage;

        [Header("Axis")] [SerializeField] private bool x, y;

        private Camera _camera;
        private bool _isMoving;
        private float _cameraClipPlane;
        private bool _isVR;
#if !META_SDK
        private LineRenderer _pointer;
#endif
        private RectTransform _handleRectTransform;
        private Vector2 _originalSize;

        // Drag tracking.
        private Vector3 _pointerStartWorldPos;
        private Vector3 _objectStartWorldPos;
        private Quaternion _objectStartRotation;

        // Orbit ("door hinge around the user") tracking for horizontal (X) drag.
        private float _orbitRadius;
        private float _orbitStartAngle; // radians, around world Y axis
        private Vector3 _orbitTangent0;  // world-space tangent direction at drag start

        private const float SPEED = 10;

        private void Start()
        {
            #if (OCULUS_INTEGRATION || XR_ITK || META_SDK)
            _isVR = true;
            #else
            _isVR = false;
            #endif
            _camera = VRUIPManager.instance.mainCamera;
            #if !META_SDK
            _pointer = VRUIPManager.instance.lineRenderer;
            #endif
            if (_camera != null) _cameraClipPlane = transform.position.z - _camera.transform.position.z;
            _handleRectTransform = GetComponent<RectTransform>();
            _originalSize = _handleRectTransform.sizeDelta;
        }

        private void FixedUpdate()
        {
            if (!_isMoving) return;

            Vector3 currentPointerWorldPos;

            if (!_isVR)
            {
                var mousePosition = Mouse.current.position.ReadValue();
                currentPointerWorldPos = _camera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, _cameraClipPlane));
            }
            else
            {
#if META_SDK
                if (!VRUIPManager.instance.GetRayInteractorPosition(out currentPointerWorldPos)) return;
#else
                if (_pointer == null) return;
                currentPointerWorldPos = _pointer.GetPosition(_pointer.positionCount - 1);
#endif
            }

            // How far the pointer has moved in world space since the drag started.
            var worldDelta = currentPointerWorldPos - _pointerStartWorldPos;

            // --- Horizontal (X axis) drag: orbit around the camera, like a door hinged at the user. ---
            var angleDelta = 0f;
            if (x && _orbitRadius > 0.0001f)
            {
                var tangentDistance = Vector3.Dot(worldDelta, _orbitTangent0);
                angleDelta = tangentDistance / _orbitRadius;
            }

            var camPos = _camera.transform.position;
            var newAngle = _orbitStartAngle + angleDelta;
            var newDir = new Vector3(Mathf.Sin(newAngle), 0f, Mathf.Cos(newAngle));
            var newFlatPos = camPos + newDir * _orbitRadius;

            // --- Vertical (Y axis) drag: straight up/down, independent of the orbit. ---
            var newY = _objectStartWorldPos.y + (y ? worldDelta.y : 0f);

            var targetPosition = new Vector3(newFlatPos.x, newY, newFlatPos.z);

            // Rotate rigidly by the same angle swept, like the panel is on a rigid arm from the camera.
            var targetRotation = Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.up) * _objectStartRotation;

            if (!_isVR)
            {
                movableObject.position = Vector3.Lerp(movableObject.position, targetPosition, Time.deltaTime * SPEED);
                movableObject.rotation = Quaternion.Slerp(movableObject.rotation, targetRotation, Time.deltaTime * SPEED);
            }
            else
            {
                movableObject.position = targetPosition;
                movableObject.rotation = targetRotation;
            }
        }

        // On pointer down, set handle image alpha to .5, set isMoving to true and record starting state.
        public void OnPointerDown(PointerEventData eventData)
        {
            _isMoving = true;
            _objectStartWorldPos = movableObject.position;
            _objectStartRotation = movableObject.rotation;

            var camPos = _camera.transform.position;

#if META_SDK
            if (!VRUIPManager.instance.GetRayInteractorPosition(out _pointerStartWorldPos))
            {
                _pointerStartWorldPos = eventData.pointerCurrentRaycast.worldPosition;
            }
#else
            _pointerStartWorldPos = eventData.pointerCurrentRaycast.worldPosition;
#endif

            // Set up the orbit pivot data (hinge = camera position) for the horizontal drag.
            var flat = new Vector3(_objectStartWorldPos.x - camPos.x, 0f, _objectStartWorldPos.z - camPos.z);
            _orbitRadius = flat.magnitude;
            if (_orbitRadius > 0.0001f)
            {
                var dir0 = flat / _orbitRadius;
                _orbitStartAngle = Mathf.Atan2(dir0.x, dir0.z);
                _orbitTangent0 = new Vector3(dir0.z, 0f, -dir0.x);
            }
            else
            {
                _orbitStartAngle = 0f;
                _orbitTangent0 = Vector3.right;
            }

            handleImage.SetAlpha(.5f);
        }

        // On pointer up, set handle image alpha back to 1 and set isMoving to false.
        public void OnPointerUp(PointerEventData eventData)
        {
            _isMoving = false;
            handleImage.SetAlpha(1);
        }
    }
}