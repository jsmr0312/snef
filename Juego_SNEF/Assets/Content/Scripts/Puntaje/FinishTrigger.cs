using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinishTrigger : MonoBehaviour
{
    public int puntajeAlLlegar = 300;
    bool _usado = false;
    Collider _col;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_usado) return;
        if (!other.CompareTag("Player")) return;

        _usado = true;
        if (Stats.I) Stats.I.AddPuntaje(puntajeAlLlegar);

        // si quieres que desaparezca el trigger tras activarse:
        // gameObject.SetActive(false);
    }
}
