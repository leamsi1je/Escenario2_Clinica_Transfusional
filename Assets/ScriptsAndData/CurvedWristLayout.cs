using UnityEngine;
using System.Collections.Generic;

public class CurvedWristLayout : MonoBehaviour
{
    [SerializeField] private List<Transform> buttons; // arrastra aquí cada botón, en orden
    [SerializeField] private int columns = 2;
    [SerializeField] private float radius = 0.035f;        // radio del antebrazo, ~3.5cm
    [SerializeField] private float angleStepDegrees = 25f;  // separación angular entre columnas
    [SerializeField] private float rowSpacing = 0.03f;      // separación vertical entre filas (hacia el codo)
    [SerializeField] private float startAngleDegrees = -12.5f; // centra el arco (para 2 columnas: -mitad del angleStep)

    void OnValidate() => ArrangeButtons();
    void Start() => ArrangeButtons();

    private void ArrangeButtons()
    {
        if (buttons == null) return;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null) continue;

            int row = i / columns;
            int col = i % columns;

            float angle = startAngleDegrees + col * angleStepDegrees;
            float rad = angle * Mathf.Deg2Rad;

            // Posición sobre el cilindro: X e Z definen el arco, Y baja por fila (hacia el codo)
            Vector3 localPos = new Vector3(
                Mathf.Sin(rad) * radius,
                -row * rowSpacing,
                Mathf.Cos(rad) * radius - radius // resta el radio para que el arco "abrace" hacia adentro
            );

            buttons[i].localPosition = localPos;
            buttons[i].localRotation = Quaternion.Euler(0f, angle, 0f); // cada botón mira hacia afuera del cilindro
        }
    }
}