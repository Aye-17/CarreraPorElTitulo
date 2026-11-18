using UnityEngine;

public class CamaraSigue : MonoBehaviour
{
    [Header("Jugador que sigue la cámara")]
    public Transform objetivo;

    [Header("Distancia de la cámara respecto al jugador")]
    public Vector3 offset = new Vector3(0, 5f, -5f);

    [Header("Suavizado del movimiento")]
    public float velocidadSuavizado = 10f;

    public void SeguirJugador(Transform nuevoObjetivo)
    {
        objetivo = nuevoObjetivo;

        if (objetivo != null)
        {
            // Coloca inmediatamente la cámara cerca del nuevo jugador al cambiar
            transform.position = objetivo.position + offset;
        }
    }

    private void LateUpdate()
    {
        if (objetivo == null)
            return;

        // Posición deseada basada estrictamente en la posición actual del Transform del objetivo sincronizado en red
        Vector3 posicionDeseada = objetivo.position + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            posicionDeseada,
            velocidadSuavizado * Time.deltaTime
        );
    }
}