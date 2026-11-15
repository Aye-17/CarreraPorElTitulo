using System.Collections.Generic;

public static class TableroFactory
{
    private static readonly HashSet<int>
        casillerosPregunta =
            new HashSet<int>
            {
                2, 5, 7, 9, 12, 14,
                16, 19, 21, 23, 26
            };

    private static readonly HashSet<int>
        casillerosJuego =
            new HashSet<int>
            {
                3, 17
            };

    private static readonly HashSet<int>
        casillerosEspeciales =
            new HashSet<int>
            {
                10, 24
            };

    private static readonly HashSet<int>
        casillerosBiblioteca =
            new HashSet<int>
            {
                4, 18
            };

    private static readonly Dictionary<int, int>
        casillerosEnviarBiblioteca =
            new Dictionary<int, int>
            {
                { 11, 18 },
                { 25, 4 }
            };

    public static List<CasilleroBase>
        CrearTablero(int cantidadCasilleros)
    {
        List<CasilleroBase> tablero =
            new List<CasilleroBase>();

        for (
            int i = 0;
            i < cantidadCasilleros;
            i++
        )
        {
            List<int> siguientesIds =
                i == cantidadCasilleros - 1
                    ? new List<int>()
                    : new List<int> { i + 1 };

            tablero.Add(
                CrearCasillero(
                    i,
                    siguientesIds
                )
            );
        }

        return tablero;
    }

    public static CasilleroBase CrearCasillero(
        int id,
        List<int> siguientesIds)
    {
        if (casillerosBiblioteca.Contains(id))
        {
            return new CasilleroBiblioteca(
                id,
                siguientesIds
            );
        }

        if (casillerosEnviarBiblioteca.TryGetValue(
            id,
            out int destinoBiblioteca))
        {
            return new CasilleroEnviarBiblioteca(
                id,
                siguientesIds,
                destinoBiblioteca
            );
        }

        if (casillerosPregunta.Contains(id))
        {
            return new CasilleroPregunta(
                id,
                siguientesIds
            );
        }

        if (casillerosJuego.Contains(id))
        {
            return new CasilleroJuego(
                id,
                siguientesIds
            );
        }

        if (casillerosEspeciales.Contains(id))
        {
            return new CasilleroEspecial(
                id,
                siguientesIds
            );
        }

        return new CasilleroPreguntaJuego(
            id,
            siguientesIds
        );
    }
}