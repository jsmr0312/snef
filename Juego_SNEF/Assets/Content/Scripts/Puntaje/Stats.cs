using UnityEngine;
using System;
using UnityEngine.SceneManagement;



public class Stats : MonoBehaviour
{
    public static Stats I { get; private set; }

    [Header("Valores")]
    [SerializeField] private int presupuesto = 0;
    [SerializeField] private int puntaje = 0;

    // Eventos para que la UI de cada escena se actualice
    public event Action<int> OnPresupuestoChanged;
    public event Action<int> OnPuntajeChanged;

    public int Presupuesto => presupuesto;
    public int Puntaje => puntaje;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // Emite el valor inicial para que el HUD de la escena que cargue lo pinte
        ForceRefresh();
    }

    public void AddPresupuesto(int cantidad)
    {
        if (cantidad == 0) return;
        presupuesto += cantidad;
        if (presupuesto < 0) presupuesto = 0;
        Debug.Log($"[Stats] AddPresupuesto({cantidad}) => {presupuesto}");
        OnPresupuestoChanged?.Invoke(presupuesto);
    }

    public void AddPuntaje(int cantidad)
    {
        if (cantidad == 0) return;
        puntaje += cantidad;
        if (puntaje < 0) puntaje = 0;
        Debug.Log($"[Stats] AddPuntaje({cantidad}) => {puntaje}");
        OnPuntajeChanged?.Invoke(puntaje);
    }

    // Útil al entrar a una escena nueva para pintar los valores actuales
    public void ForceRefresh()
    {
        Debug.Log($"[Stats] ForceRefresh | Presupuesto={presupuesto}  Puntaje={puntaje}");
        OnPresupuestoChanged?.Invoke(presupuesto);
        OnPuntajeChanged?.Invoke(puntaje);
    }

    private void OnActiveSceneChanged(Scene prev, Scene next) => ForceRefresh();

    void OnEnable() { SceneManager.activeSceneChanged += OnActiveSceneChanged; }
    void OnDisable() { SceneManager.activeSceneChanged -= OnActiveSceneChanged; }

}
