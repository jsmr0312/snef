[System.Serializable]
public class MetricaEvento
{
    public string name;
    public Contenido contenido;

    [System.Serializable]
    public class Contenido
    {
        public string nombre;       
        public string tiempo;       
    }
}
