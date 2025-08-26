using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class CoinPickup : MonoBehaviour
{
    [Header("Recompensa")]
    public int presupuestoGanado = 10;

    [Header("Animación de recogida")]
    public float riseHeight = 0.6f;      // cuánto sube antes de caer
    public float riseTime = 0.18f;       // tiempo de subida
    public float dropExtra = 0.8f;       // cuánto más bajo cae respecto a la altura inicial
    public float fallGravity = 18f;      // aceleración de la caída
    public float spinSpeedOnCollect = 720f; // rotación rápida durante la animación

    [Header("Opcionales")]
    public AudioSource sfx;                 // sonido de recogida (opcional)
    public ParticleSystem particles;        // partículas (opcional)

    private Collider _col;
    private RotateAndLevitate _rot;
    private bool _collected = false;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _rot = GetComponent<RotateAndLevitate>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;

        _collected = true;

        // sumar presupuesto
        if (Stats.I) Stats.I.AddPresupuesto(presupuestoGanado);

        // feedback
        if (sfx) sfx.Play();
        if (particles) particles.Play();

        // evitar dobles recogidas y apagar animación de idle
        _col.enabled = false;
        if (_rot) _rot.enabled = false;

        StartCoroutine(CollectAnimAndHide());
    }

    IEnumerator CollectAnimAndHide()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // 1) SUBIDA con easing suave (ease-out)
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / riseTime);
            // EaseOutCubic
            float e = 1f - Mathf.Pow(1f - k, 3f);
            Vector3 p = startPos;
            p.y = startPos.y + riseHeight * e;
            transform.position = p;

            // rotación extra mientras sube
            transform.Rotate(Vector3.up, spinSpeedOnCollect * Time.deltaTime, Space.World);

            yield return null;
        }

        // 2) CAÍDA acelerada hasta quedar más abajo que el punto inicial
        float v = 0f; // velocidad vertical
        float targetY = startPos.y - dropExtra;
        Vector3 pos = transform.position;

        while (pos.y > targetY)
        {
            v += fallGravity * Time.deltaTime; // acelera hacia abajo
            pos.y -= v * Time.deltaTime;
            transform.position = pos;

            // sigue girando durante la caída
            transform.Rotate(Vector3.up, spinSpeedOnCollect * Time.deltaTime, Space.World);

            yield return null;
        }

        // 3) Desaparecer
        gameObject.SetActive(false);
    }
}
