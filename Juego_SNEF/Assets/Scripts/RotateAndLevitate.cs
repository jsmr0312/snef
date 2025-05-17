using UnityEngine;

public class RotateAndLevitate : MonoBehaviour
{
    public float rotationSpeed = 90f;       // Grados por segundo
    public float levitationAmplitude = 0.5f; // Altura máxima de levitación
    public float levitationFrequency = 1f;   // Ciclos por segundo

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Rotación continua
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);

        // Movimiento de levitación (arriba y abajo)
        float newY = startPosition.y + Mathf.Sin(Time.time * Mathf.PI * 2f * levitationFrequency) * levitationAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
