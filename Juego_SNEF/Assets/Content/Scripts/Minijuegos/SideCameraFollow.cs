using UnityEngine;

/// <summary>
/// Cámara lateral que sigue al jugador con suavizado en posición y rotación, evitando vibraciones.
/// </summary>
public class SideCameraFollow : MonoBehaviour
{
    [Tooltip("Jugador a seguir")] public Transform target;
    [Tooltip("Offset de posición relativo al jugador")] public Vector3 offset = new Vector3(0, 2, -10);
    [Tooltip("Tiempo de suavizado para el movimiento")] public float smoothTime = 0.1f;
    [Tooltip("Tiempo de suavizado para la rotación")] public float rotationSmoothTime = 0.1f;
    [Tooltip("Ángulo de inclinación en grados (pitch)")] public float tiltAngle = 10f;
    [Tooltip("Offset adicional para el punto de mira")] public Vector3 lookAtOffset = Vector3.zero;

    private Vector3 _positionVelocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null)
            return;

        // Suavizado de posición
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, smoothTime);

        // Suavizado de rotación
        Vector3 lookPoint = target.position + lookAtOffset;
        // Calcula la rotación deseada apuntando al objetivo y aplicando tilt
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position) * Quaternion.Euler(tiltAngle, 0, 0);
        // Interpolación suave de rotación
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime / rotationSmoothTime);
    }
}
