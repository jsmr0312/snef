using UnityEngine;

public class CameraRootMover : MonoBehaviour
{
    [Tooltip("Todos los posibles CameraRoot de los personajes")]
    public Transform[] cameraRoots;
    [Tooltip("Desplazamiento opcional desde el root")]
    public Vector3 offset;

    private Transform _current;

    void LateUpdate()
    {
        // 1) Buscamos el primer root activo
        Transform activo = null;
        foreach (var t in cameraRoots)
        {
            if (t != null && t.gameObject.activeInHierarchy)
            {
                activo = t;
                break;
            }
        }

        // 2) Si cambió, guardamos y seguimos
        if (activo != _current)
            _current = activo;

        // 3) Actualizamos posición
        if (_current != null)
            transform.position = _current.position + offset;
    }
}
