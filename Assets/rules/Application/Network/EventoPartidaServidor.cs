public class EventoPartidaServidor
{
    public bool EsValido { get; private set; }
    public TipoEventoPartida Tipo { get; private set; }
    public string Mensaje { get; private set; }
    public int ResultadoDado { get; private set; }
    public ulong ClientIdTurno { get; private set; }
    public PreguntaVisible Pregunta { get; private set; }
    public bool MostrarCartelBiblioteca { get; private set; }

    private EventoPartidaServidor(
        bool esValido,
        TipoEventoPartida tipo,
        string mensaje,
        int resultadoDado,
        ulong clientIdTurno,
        PreguntaVisible pregunta,
        bool mostrarCartelBiblioteca)
    {
        EsValido = esValido;
        Tipo = tipo;
        Mensaje = mensaje;
        ResultadoDado = resultadoDado;
        ClientIdTurno = clientIdTurno;
        Pregunta = pregunta;
        MostrarCartelBiblioteca = mostrarCartelBiblioteca;
    }

    public static EventoPartidaServidor Crear(
        TipoEventoPartida tipo,
        string mensaje,
        int resultadoDado,
        ulong clientIdTurno,
        bool mostrarCartelBiblioteca = false)
    {
        return new EventoPartidaServidor(
            true,
            tipo,
            mensaje,
            resultadoDado,
            clientIdTurno,
            null,
            mostrarCartelBiblioteca
        );
    }

    public static EventoPartidaServidor CrearPregunta(
        string mensaje,
        ulong clientIdTurno,
        PreguntaVisible pregunta)
    {
        return new EventoPartidaServidor(
            true,
            TipoEventoPartida.PreguntaPendiente,
            mensaje,
            0,
            clientIdTurno,
            pregunta,
            false
        );
    }

    public static EventoPartidaServidor AccionInvalida(
        string mensaje,
        ulong clientIdTurno)
    {
        return new EventoPartidaServidor(
            false,
            TipoEventoPartida.AccionInvalida,
            mensaje,
            0,
            clientIdTurno,
            null,
            false
        );
    }
}