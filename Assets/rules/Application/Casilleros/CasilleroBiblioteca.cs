using System.Collections.Generic;

public class CasilleroBiblioteca : CasilleroBase
{
    public CasilleroBiblioteca(int id, List<int> siguientesIds)
        : base(id, siguientesIds)
    {
        Tipo = TipoCasillero.Biblioteca;
    }

    public override void EjecutarEfecto(Jugador jugador)
    {
        // Landing on the library is only a visit; only send spaces detain players.
    }
}

public class CasilleroEnviarBiblioteca : CasilleroBase
{
    public int DestinoBibliotecaId { get; private set; }

    public CasilleroEnviarBiblioteca(
        int id,
        List<int> siguientesIds,
        int destinoBibliotecaId)
        : base(id, siguientesIds)
    {
        Tipo = TipoCasillero.EnviarABiblioteca;
        DestinoBibliotecaId = destinoBibliotecaId;
    }

    public override void EjecutarEfecto(Jugador jugador)
    {
        jugador?.EnviarABiblioteca(DestinoBibliotecaId);
    }
}
