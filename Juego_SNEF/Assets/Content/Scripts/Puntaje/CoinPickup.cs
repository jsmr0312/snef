using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class CoinPickup : MonoBehaviour, ILevelResettable
{
    [Header("Recompensa")]
    public int presupuestoGanado = 10;

    [Header("Animación de recogida")]
    public float riseHeight = 0.6f;
    public float riseTime = 0.18f;
    public float dropExtra = 0.8f;
    public float fallGravity = 18f;
    public float spinSpeedOnCollect = 720f;

    [Header("Opcionales")]
    public AudioSource sfx;
    public ParticleSystem particles;

    private Collider _col;
    private RotateAndLevitate _rot;
    private bool _collected = false;

    // Guardamos estado inicial para restaurar en reintento
    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _startScale;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _rot = GetComponent<RotateAndLevitate>();

        _startPos = transform.position;
        _startRot = transform.rotation;
        _startScale = transform.localScale;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;

        _collected = true;

        if (Stats.I) Stats.I.AddPresupuesto(presupuestoGanado);

        if (sfx) sfx.Play();
        if (particles) particles.Play();

        _col.enabled = false;
        if (_rot) _rot.enabled = false;

        StartCoroutine(CollectAnimAndHide());
    }

    IEnumerator CollectAnimAndHide()
    {
        Vector3 startPos = transform.position;

        // SUBIDA
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / riseTime);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            var p = startPos;
            p.y = startPos.y + riseHeight * e;
            transform.position = p;
            transform.Rotate(Vector3.up, spinSpeedOnCollect * Time.deltaTime, Space.World);
            yield return null;
        }

        // CAÍDA
        float v = 0f;
        float targetY = startPos.y - dropExtra;
        var pos = transform.position;

        while (pos.y > targetY)
        {
            v += fallGravity * Time.deltaTime;
            pos.y -= v * Time.deltaTime;
            transform.position = pos;
            transform.Rotate(Vector3.up, spinSpeedOnCollect * Time.deltaTime, Space.World);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    // === ILevelResettable ===
    public void ResetState()
    {
        StopAllCoroutines();

        _collected = false;

        // Restaurar trasform
        transform.position = _startPos;
        transform.rotation = _startRot;
        transform.localScale = _startScale;

        // Reset FX
        if (sfx) { sfx.Stop(); sfx.time = 0f; }
        if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Reactivar y dejar lista para recoger otra vez
        if (_rot) _rot.enabled = true;
        _col.enabled = true;
        gameObject.SetActive(true);
    }
}
