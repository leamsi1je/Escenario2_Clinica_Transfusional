using UnityEngine;

public class LazyFollowPanel : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform targetCamera;

    [Header("Distancia y Confort")]
    [Tooltip("Distancia hacia el frente de la cámara")]
    [SerializeField] private float distanceFromHead = 1.2f;

    [Tooltip("Ángulo muerto en grados. Si miras dentro de este cono, el panel no se mueve")]
    [Range(5f, 60f)]
    [SerializeField] private float deadzoneAngle = 20f;

    [Tooltip("Velocidad con la que el panel se reacomoda cuando sales de la zona muerta")]
    [SerializeField] private float followSpeed = 4f;

    [Tooltip("Mantiene el panel a nivel horizontal sin rotar arriba/abajo bruscamente")]
    [SerializeField] private bool lockPitch = false;

    private void Awake()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        if (panel == null)
        {
            panel = gameObject;
        }
    }

    private void LateUpdate()
    {
        if (!panel.activeSelf || targetCamera == null) return;

        UpdateLazyFollow();
    }

    /// <summary>
    /// Asigna este método al OnClick() de tu botón.
    /// </summary>
    public void TogglePanel()
    {
        bool willBeActive = !panel.activeSelf;
        panel.SetActive(willBeActive);

        if (willBeActive && targetCamera != null)
        {
            // Centrado inmediato al abrir
            SnapToFront();
        }
    }

    private void SnapToFront()
    {
        Vector3 forward = GetGazeForward();
        panel.transform.position = targetCamera.position + (forward * distanceFromHead);
        FaceCamera();
    }

    private void UpdateLazyFollow()
    {
        Vector3 cameraPos = targetCamera.position;
        Vector3 toPanel = panel.transform.position - cameraPos;
        Vector3 gazeDir = GetGazeForward();

        // Ángulo entre hacia dónde miras y la posición actual del panel
        float angleToPanel = Vector3.Angle(gazeDir, toPanel);

        // Si la mirada sale de la zona muerta, interpolamos hacia la nueva posición central
        if (angleToPanel > deadzoneAngle)
        {
            Vector3 targetPosition = cameraPos + (gazeDir * distanceFromHead);
            panel.transform.position = Vector3.Lerp(
                panel.transform.position, 
                targetPosition, 
                followSpeed * Time.deltaTime
            );
        }

        // Mantiene el panel orientado hacia la cara del usuario
        FaceCamera();
    }

    private Vector3 GetGazeForward()
    {
        Vector3 forward = targetCamera.forward;
        if (lockPitch)
        {
            forward.y = 0f;
            forward.Normalize();
        }
        return forward;
    }

    private void FaceCamera()
    {
        // Vector desde la cámara hacia el panel para evitar invertir el Canvas
        Vector3 lookDir = panel.transform.position - targetCamera.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            panel.transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}