using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    public float speed = 5.0f;
    private Rigidbody rb;

    void Start()
    {
        // Obtenemos el Rigidbody al iniciar
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Capturamos el movimiento de las flechas o WASD
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        // Creamos el vector de movimiento
        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);

        // Movemos el objeto usando el Rigidbody para que respete colisiones
        rb.MovePosition(transform.position + movement * speed * Time.deltaTime);
    }
}