using UnityEngine;
using UnityEngine.UI;
public class presupuestoPlayer : MonoBehaviour
{
     public float vida = 100;
    public Image barraDePresupuesto;

    // Update is called once per frame
    void Update()
    {
        vida = Mathf.Clamp(vida, 0, 10000);
        barraDePresupuesto.fillAmount = vida / 10000; 
    }
}
