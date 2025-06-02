using UnityEngine;

public class RotateObject : MonoBehaviour
{
    public float rotationSpeed = 90f; // grados por segundo

    void Update()
    {
        // Rota el objeto alrededor del eje Y a la velocidad definida
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }
}
