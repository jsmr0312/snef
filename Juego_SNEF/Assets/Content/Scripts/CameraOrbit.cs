using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform target; // El punto alrededor del cual girará la cámara
    public float rotationSpeed = 10f;

    void Update()
    {
        transform.RotateAround(target.position, Vector3.up, rotationSpeed * Time.deltaTime);
        transform.LookAt(target); // Asegura que la cámara siempre mire al objetivo
    }
}
