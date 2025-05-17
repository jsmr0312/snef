using UnityEngine;

public class Billboard : MonoBehaviour
{
    Camera cam;

    void Start()
    {
        // Busca la cámara principal al arrancar
        cam = Camera.main;
    }

    void LateUpdate()
    {
        // Opción A: LookAt con forward y up de la cámara
        transform.LookAt(
            transform.position + cam.transform.rotation * Vector3.forward,
            cam.transform.rotation * Vector3.up
        );

        // —– o —–

        // Opción B: directamente apuntar al vector desde cámara
        // transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
