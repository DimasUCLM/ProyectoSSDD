namespace Restaurante;

public class Ciclomotor
{
    public string Id { get; private set; }
    public bool EnUso { get; private set; }

    public Ciclomotor(int id)
    {
        Id = $"Moto-{id}";
        EnUso = false;
    }

    public void Asignar()
    {
        EnUso = true;
    }

    public void Devolver()
    {
        EnUso = false;
    }
}