using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinishTrigger : MonoBehaviour
{
    Collider _col;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Solo notifica. El manager se encarga de ignorar dobles llamadas.
        var manager = FindObjectOfType<CorreYGanaManager>();
        if (manager != null) manager.NotificarMeta();
    }
}
