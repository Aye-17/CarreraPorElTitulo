using System.Collections.Generic;

public class CasilleroPreguntaJuego : CasilleroBase
{
    public CasilleroPreguntaJuego(int id, List<int> siguientesIds)
        : base(id, siguientesIds)
    {
        Tipo = TipoCasillero.PreguntaJuego;
    }

    public override void EjecutarEfecto(Jugador jugador)
    {
        // La UI se muestra desde GameManager (Visual).
    }
}
