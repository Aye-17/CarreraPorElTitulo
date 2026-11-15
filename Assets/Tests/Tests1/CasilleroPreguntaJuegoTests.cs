using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CasilleroPreguntaJuegoTests
{
    [Test]
    public void CasilleroNormalSeConvierteEnPreguntaJuego()
    {
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(0, new List<int>());

        Assert.IsInstanceOf<CasilleroPreguntaJuego>(casillero);
        Assert.AreEqual(TipoCasillero.PreguntaJuego, casillero.Tipo);
    }

    [Test]
    public void CasilleroPreguntaConservaSuTipoYComportamiento()
    {
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(2, new List<int>());

        Assert.IsInstanceOf<CasilleroPregunta>(casillero);
        Assert.AreEqual(TipoCasillero.Pregunta, casillero.Tipo);
    }

    [TestCase(3)]
    [TestCase(17)]
    public void OtrosPlanosDeJuegoConservanElMinijuego(int id)
    {
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(id, new List<int>());

        Assert.IsInstanceOf<CasilleroJuego>(casillero);
        Assert.AreEqual(TipoCasillero.Juego, casillero.Tipo);
    }

    [Test]
    public void PlanoEspecialFueraDeLosSolicitadosConservaSuTipo()
    {
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(24, new List<int>());

        Assert.IsInstanceOf<CasilleroEspecial>(casillero);
        Assert.AreEqual(TipoCasillero.EfectoEspecial, casillero.Tipo);
    }

    [Test]
    public void BancoPreguntasJuegosLeeLaRaizDelJson()
    {
        const string json =
            "{\"preguntasjuegos\":[{\"texto\":\"Pregunta de juego\",\"opciones\":[\"A\",\"B\"],\"indiceCorrecta\":1}]}";

        BancoPreguntasJuegos banco =
            JsonUtility.FromJson<BancoPreguntasJuegos>(json);

        Assert.AreEqual(1, banco.preguntasjuegos.Length);
        Assert.AreEqual("Pregunta de juego", banco.preguntasjuegos[0].texto);
        Assert.AreEqual(1, banco.preguntasjuegos[0].indiceCorrecta);
    }

    [TestCase(4)]
    [TestCase(18)]
    public void PlanosCuatroYDieciochoSonBibliotecas(int id)
    {
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(id, new List<int>());

        Assert.IsInstanceOf<CasilleroBiblioteca>(casillero);
        Assert.AreEqual(TipoCasillero.Biblioteca, casillero.Tipo);
    }

    [TestCase(4)]
    [TestCase(18)]
    public void CaerEnUnaBibliotecaEsSoloUnaVisita(int id)
    {
        Jugador jugador = CrearJugador();
        jugador.EstablecerPosicion(id);
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(id, new List<int>());

        casillero.EjecutarEfecto(jugador);

        Assert.IsFalse(jugador.EstaEnBiblioteca);
        Assert.AreEqual(id, jugador.PosicionActualId);
        Assert.AreEqual(0, jugador.IntentosDeSalirBiblioteca);
    }

    [TestCase(11, 18)]
    [TestCase(25, 4)]
    public void PlanosDiezYVeinticincoEnvianALaBibliotecaMasCercana(
        int id,
        int destino)
    {
        CasilleroEnviarBiblioteca casillero =
            TableroFactory.CrearCasillero(
                id,
                new List<int>()
            ) as CasilleroEnviarBiblioteca;

        Assert.IsNotNull(casillero);
        Assert.AreEqual(TipoCasillero.EnviarABiblioteca, casillero.Tipo);
        Assert.AreEqual(destino, casillero.DestinoBibliotecaId);
    }

    [TestCase(11, 18)]
    [TestCase(25, 4)]
    public void PlanoDeEnvioMueveAlJugadorALaBibliotecaMasCercana(
        int id,
        int destino)
    {
        Jugador jugador = CrearJugador();
        CasilleroBase casillero =
            TableroFactory.CrearCasillero(id, new List<int>());

        casillero.EjecutarEfecto(jugador);

        Assert.IsTrue(jugador.EstaEnBiblioteca);
        Assert.AreEqual(destino, jugador.PosicionActualId);
    }

    [Test]
    public void JugadorSaleDeBibliotecaAlSacarSeis()
    {
        Jugador jugador = CrearJugador();
        jugador.EnviarABiblioteca(3);

        Assert.IsTrue(jugador.RegistrarIntentoDeSalirBiblioteca(6));
        Assert.IsFalse(jugador.EstaEnBiblioteca);
        Assert.AreEqual(1, jugador.IntentosDeSalirBiblioteca);
    }

    [Test]
    public void JugadorSaleEnElTercerIntentoYPuedeAvanzar()
    {
        Jugador jugador = CrearJugador();
        jugador.EnviarABiblioteca(17);

        Assert.IsFalse(jugador.RegistrarIntentoDeSalirBiblioteca(2));
        Assert.IsFalse(jugador.RegistrarIntentoDeSalirBiblioteca(4));
        Assert.IsTrue(jugador.RegistrarIntentoDeSalirBiblioteca(3));
        Assert.IsFalse(jugador.EstaEnBiblioteca);

        jugador.Moverse(3, 28);

        Assert.AreEqual(20, jugador.PosicionActualId);
    }

    [Test]
    public void PartidaRedRechazaUnaSegundaTiradaEnElMismoTurno()
    {
        var preguntas = new List<PreguntaData>
        {
            new PreguntaData
            {
                texto = "Pregunta de prueba",
                opciones = new[] { "A", "B" },
                indiceCorrecta = 0
            }
        };
        var partida = new PartidaServidor(
            ConfiguracionPartida.Crear(ModoPartida.Normal),
            new ulong[] { 10, 20 },
            preguntas,
            28
        );

        EventoPartidaServidor primeraTirada =
            partida.SolicitarTirarDado(10);
        EventoPartidaServidor segundaTirada =
            partida.SolicitarTirarDado(10);

        Assert.IsTrue(primeraTirada.EsValido);
        Assert.IsFalse(segundaTirada.EsValido);
    }

    private static Jugador CrearJugador()
    {
        return new Jugador(
            1,
            "Jugador Rojo",
            ConfiguracionPartida.Crear(ModoPartida.Normal)
        );
    }
}
