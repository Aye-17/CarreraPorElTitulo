using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkLobbyServer : NetworkBehaviour
{
    public static NetworkLobbyServer Instance { get; private set; }

    // (textoPregunta, opciones, esPreguntaJuego)
    public event Action<string, string[], bool> PreguntaRecibida;
    public event Action<string, ulong> EstadoPartidaRecibido;
    public event Action<string> ResumenActualizado;
    public event Action<string> ResultadoRecibido;
    public event Action CartelBibliotecaRecibido;
    public event Action<int, int[], int> EstadoVisualActualizado;
    public event Action<int[], int[]> EstadisticasJugadoresActualizadas;

    private readonly NetworkVariable<FixedString512Bytes> resumenSalas =
        new NetworkVariable<FixedString512Bytes>(
            new FixedString512Bytes("No hay salas creadas."),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private GestorSalas gestorSalas;

    [Header("Configuración de Gameplay")]
    [SerializeField] private TextAsset archivoPreguntas;
    [SerializeField] private TextAsset archivoPreguntasJuegos;
    [SerializeField] private int cantidadCasilleros = 28;

    private BancoPreguntas bancoPreguntas;
    private BancoPreguntasJuegos bancoPreguntasJuegos;

    private readonly Dictionary<string, PartidaServidor> partidasPorSala =
        new Dictionary<string, PartidaServidor>();

    private readonly Dictionary<ulong, string> salaPorCliente =
        new Dictionary<ulong, string>();

    public string ResumenActual => resumenSalas.Value.ToString();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        resumenSalas.OnValueChanged += AlCambiarResumenSalas;

        if (IsServer)
        {
            gestorSalas = new GestorSalas();
            CargarPreguntasDelServidor();
            ActualizarResumenEnServidor();
        }

        MostrarResumen(resumenSalas.Value.ToString());
    }

    public override void OnNetworkDespawn()
    {
        resumenSalas.OnValueChanged -= AlCambiarResumenSalas;
    }

    private void CargarPreguntasDelServidor()
    {
        if (archivoPreguntas == null)
        {
            Debug.LogError("[NetworkLobbyServer] Falta Preguntas.json.");
            return;
        }

        bancoPreguntas = JsonUtility.FromJson<BancoPreguntas>(archivoPreguntas.text);

        if (bancoPreguntas == null ||
            bancoPreguntas.preguntas == null ||
            bancoPreguntas.preguntas.Length == 0)
        {
            Debug.LogError("[NetworkLobbyServer] El banco de preguntas es inválido.");
        }

        if (archivoPreguntasJuegos != null)
        {
            bancoPreguntasJuegos = JsonUtility.FromJson<BancoPreguntasJuegos>(archivoPreguntasJuegos.text);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarCrearSalaServerRpc(
        FixedString32Bytes nombreSala,
        int maxJugadores,
        ServerRpcParams parametrosRpc = default
    )
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (salaPorCliente.ContainsKey(clientId))
        {
            EnviarResultadoACliente(clientId, "Ya pertenecés a una sala.");
            return;
        }

        Sala nuevaSala = gestorSalas.CrearSala(
            clientId,
            nombreSala.ToString(),
            maxJugadores
        );

        if (nuevaSala == null)
        {
            EnviarResultadoACliente(clientId, "Configuración de sala inválida.");
            return;
        }

        salaPorCliente.Add(clientId, nuevaSala.Id);
        ActualizarResumenEnServidor();
        EnviarResultadoACliente(clientId, $"Sala creada: {nuevaSala.Id}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarUnirseSalaServerRpc(
        FixedString32Bytes idSala,
        ServerRpcParams parametrosRpc = default
    )
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (salaPorCliente.ContainsKey(clientId))
        {
            EnviarResultadoACliente(clientId, "Ya pertenecés a una sala.");
            return;
        }

        Sala sala = gestorSalas.ObtenerSala(idSala.ToString());

        if (sala == null)
        {
            EnviarResultadoACliente(clientId, "La sala indicada no existe.");
            return;
        }

        ResultadoUnionSala resultado = sala.IntentarUnir(clientId);

        if (resultado == ResultadoUnionSala.Jugador)
        {
            salaPorCliente.Add(clientId, sala.Id);
            ActualizarResumenEnServidor();
            EnviarResultadoACliente(clientId, $"Te uniste a {sala.Id} como jugador.");
            return;
        }

        if (resultado == ResultadoUnionSala.Espectador)
        {
            salaPorCliente.Add(clientId, sala.Id);
            ActualizarResumenEnServidor();
            EnviarResultadoACliente(clientId, $"Te uniste a {sala.Id} como espectador.");
            return;
        }

        if (resultado == ResultadoUnionSala.SalaLlena)
        {
            EnviarResultadoACliente(clientId, "La sala está llena.");
            return;
        }

        if (resultado == ResultadoUnionSala.SalaTerminada)
        {
            EnviarResultadoACliente(clientId, "La sala ya terminó.");
            return;
        }

        EnviarResultadoACliente(clientId, "Ya estabas dentro de esa sala.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarIniciarPartidaServerRpc(ServerRpcParams parametrosRpc = default)
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (!salaPorCliente.TryGetValue(clientId, out string idSala))
        {
            EnviarResultadoACliente(clientId, "No pertenecés a ninguna sala.");
            return;
        }

        Sala sala = gestorSalas.ObtenerSala(idSala);

        if (sala == null)
        {
            EnviarResultadoACliente(clientId, "La sala no existe.");
            return;
        }

        if (bancoPreguntas == null || bancoPreguntas.preguntas == null || bancoPreguntas.preguntas.Length == 0)
        {
            EnviarResultadoACliente(clientId, "No hay preguntas configuradas en el servidor.");
            return;
        }

        PartidaServidor partida;

        try
        {
            partida = new PartidaServidor(
                ConfiguracionPartida.Crear(ModoPartida.Normal),
                sala.Jugadores,
                bancoPreguntas.preguntas,
                cantidadCasilleros
            );
        }
        catch (Exception excepcion)
        {
            EnviarResultadoACliente(clientId, $"No se pudo crear la partida: {excepcion.Message}");
            return;
        }

        bool inicioCorrecto = sala.IntentarIniciar(clientId);

        if (!inicioCorrecto)
        {
            EnviarResultadoACliente(clientId, "Solo el creador puede iniciar una sala con al menos dos jugadores.");
            return;
        }

        partidasPorSala.Add(sala.Id, partida);

        EnviarEstadoPartidaASala(
            sala,
            partida,
            EventoPartidaServidor.Crear(
                TipoEventoPartida.TurnoCambiado,
                "La partida comenzó.",
                0,
                partida.ClientIdDelTurno
            ),
            clientId
        );

        ActualizarResumenEnServidor();
        EnviarResultadoACliente(clientId, $"La partida de {sala.Id} comenzó.");

        CargarJuegoParaSala(sala);
    }

    private void CargarJuegoParaSala(Sala sala)
    {
        List<ulong> destinatarios = new List<ulong>();
        destinatarios.AddRange(sala.Jugadores);
        destinatarios.AddRange(sala.Espectadores);

        ClientRpcParams parametrosCliente = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = destinatarios.ToArray()
            }
        };

        PrepararJuegoClientRpc(
            new FixedString32Bytes(sala.Id),
            parametrosCliente
        );

        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                "JuegoRed",
                LoadSceneMode.Single
            );
        }
    }

    [ClientRpc]
    private void PrepararJuegoClientRpc(
        FixedString32Bytes idSala,
        ClientRpcParams parametrosRpc = default
    )
    {
        SesionRedCliente.EstablecerSala(idSala.ToString());
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarEstadoActualServerRpc(ServerRpcParams parametrosRpc = default)
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (!salaPorCliente.TryGetValue(clientId, out string idSala)) return;

        Sala sala = gestorSalas.ObtenerSala(idSala);
        if (sala == null || sala.Estado != EstadoSala.EnJuego) return;

        if (!partidasPorSala.TryGetValue(idSala, out PartidaServidor partida)) return;

        EnviarEstadoPartidaASala(sala, partida, partida.ObtenerEstadoActual(), clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarTirarDadoServerRpc(ServerRpcParams parametrosRpc = default)
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (!salaPorCliente.TryGetValue(clientId, out string idSala))
        {
            EnviarResultadoACliente(clientId, "No pertenecés a una sala.");
            return;
        }

        Sala sala = gestorSalas.ObtenerSala(idSala);

        if (sala == null || sala.Estado != EstadoSala.EnJuego)
        {
            EnviarResultadoACliente(clientId, "La partida no está en curso.");
            return;
        }

        if (!partidasPorSala.TryGetValue(idSala, out PartidaServidor partida))
        {
            EnviarResultadoACliente(clientId, "No existe estado de partida.");
            return;
        }

        EventoPartidaServidor evento = partida.SolicitarTirarDado(clientId);

        if (!evento.EsValido)
        {
            EnviarResultadoACliente(clientId, evento.Mensaje);
            return;
        }

        EnviarEstadoPartidaASala(sala, partida, evento, clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarResponderPreguntaServerRpc(
        int opcionSeleccionada,
        ServerRpcParams parametrosRpc = default
    )
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (!salaPorCliente.TryGetValue(clientId, out string idSala))
        {
            EnviarResultadoACliente(clientId, "No pertenecés a una sala.");
            return;
        }

        if (!partidasPorSala.TryGetValue(idSala, out PartidaServidor partida))
        {
            EnviarResultadoACliente(clientId, "No existe una partida activa.");
            return;
        }

        Sala sala = gestorSalas.ObtenerSala(idSala);
        EventoPartidaServidor evento = partida.SolicitarRespuesta(clientId, opcionSeleccionada);

        if (!evento.EsValido)
        {
            EnviarResultadoACliente(clientId, evento.Mensaje);
            return;
        }

        EnviarEstadoPartidaASala(sala, partida, evento, clientId);
    }

    private void ActualizarResumenEnServidor()
    {
        if (!IsServer) return;
        resumenSalas.Value = new FixedString512Bytes(ConstruirResumenSalas());
    }

    private string ConstruirResumenSalas()
    {
        if (gestorSalas == null || gestorSalas.Salas.Count == 0)
        {
            return "No hay salas creadas.";
        }

        StringBuilder resumen = new StringBuilder();

        foreach (Sala sala in gestorSalas.Salas)
        {
            resumen.AppendLine(
                $"{sala.Id} | {sala.Nombre} | " +
                $"{sala.Jugadores.Count}/{sala.MaxJugadores} jugadores | " +
                $"{sala.Estado} | " +
                $"{sala.Espectadores.Count} espectadores"
            );
        }

        return resumen.ToString();
    }

    private void EnviarResultadoACliente(ulong clientId, string mensaje)
    {
        ClientRpcParams parametrosCliente = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        RecibirResultadoClientRpc(new FixedString128Bytes(mensaje ?? ""), parametrosCliente);
    }

    private void EnviarEstadoPartidaASala(
        Sala sala,
        PartidaServidor partida,
        EventoPartidaServidor evento,
        ulong clientIdEjecutor
    )
    {
        List<ulong> destinatarios = new List<ulong>();
        destinatarios.AddRange(sala.Jugadores);
        destinatarios.AddRange(sala.Espectadores);

        ClientRpcParams parametrosCliente = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = destinatarios.ToArray()
            }
        };

        string estado = ConstruirEstadoPartida(partida, evento);
        RecibirEstadoPartidaClientRpc(
            new FixedString512Bytes(estado.Length > 500 ? estado.Substring(0, 500) : estado),
            partida.ClientIdDelTurno,
            parametrosCliente
        );

        int cantidadJugadores = partida.Jugadores.Count;
        int[] posiciones = new int[cantidadJugadores];
        int[] vueltas = new int[cantidadJugadores];
        int[] correctas = new int[cantidadJugadores];
        int indiceTurno = 0;
        int indiceEjecutor = 0;

        for (int i = 0; i < cantidadJugadores; i++)
        {
            Jugador jugador = partida.Jugadores[i];
            posiciones[i] = jugador.PosicionActualId;
            vueltas[i] = jugador.RondaActual;
            correctas[i] = jugador.RespuestasCorrectas;

            if (i < sala.Jugadores.Count)
            {
                if (sala.Jugadores[i] == partida.ClientIdDelTurno)
                    indiceTurno = i;

                if (sala.Jugadores[i] == clientIdEjecutor)
                    indiceEjecutor = i;
            }
        }

        int indiceVisual = evento.ResultadoDado > 0 ? indiceEjecutor : indiceTurno;

        RecibirEstadoVisualClientRpc(indiceVisual, posiciones, evento.ResultadoDado, parametrosCliente);
        RecibirEstadisticasJugadoresClientRpc(vueltas, correctas, parametrosCliente);

        if (evento.Tipo == TipoEventoPartida.PreguntaPendiente && evento.Pregunta != null)
        {
            int posActual = (indiceEjecutor >= 0 && indiceEjecutor < posiciones.Length)
                ? posiciones[indiceEjecutor]
                : -1;

            bool esPreguntaJuego = EsCasilleroPreguntaJuego(posActual);
            EnviarPreguntaACliente(partida.ClientIdDelTurno, evento.Pregunta, esPreguntaJuego);
        }
    }

    private bool EsCasilleroPreguntaJuego(int indiceCasillero)
    {
        if (indiceCasillero < 0 || GameManager.Instancia == null || GameManager.Instancia.tableroManager == null)
            return false;

        var lista = GameManager.Instancia.tableroManager.ListaCasilleros;
        if (lista == null) return false;

        for (int i = 0; i < lista.Count; i++)
        {
            if (lista[i] != null && lista[i].Id == indiceCasillero)
            {
                return lista[i].Tipo == TipoCasillero.PreguntaJuego;
            }
        }

        return false;
    }

    private string ConstruirEstadoPartida(PartidaServidor partida, EventoPartidaServidor evento)
    {
        StringBuilder estado = new StringBuilder();
        estado.AppendLine(evento.Mensaje);
        estado.AppendLine($"Turno: Cliente {partida.ClientIdDelTurno}");
        return estado.ToString();
    }

    private void EnviarPreguntaACliente(ulong clientId, PreguntaVisible pregunta, bool esPreguntaJuego)
    {
        if (pregunta == null) return;

        string texto = pregunta.Texto ?? "¿Pregunta?";
        string op0 = (pregunta.Opciones != null && pregunta.Opciones.Length > 0) ? pregunta.Opciones[0] : "Opción A";
        string op1 = (pregunta.Opciones != null && pregunta.Opciones.Length > 1) ? pregunta.Opciones[1] : "Opción B";
        string op2 = (pregunta.Opciones != null && pregunta.Opciones.Length > 2) ? pregunta.Opciones[2] : "Opción C";
        string op3 = (pregunta.Opciones != null && pregunta.Opciones.Length > 3) ? pregunta.Opciones[3] : "Opción D";

        ClientRpcParams parametrosCliente = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        RecibirPreguntaClientRpc(
            new FixedString128Bytes(texto),
            new FixedString128Bytes(op0),
            new FixedString128Bytes(op1),
            new FixedString128Bytes(op2),
            new FixedString128Bytes(op3),
            esPreguntaJuego,
            parametrosCliente
        );
    }

    [ClientRpc]
    private void RecibirEstadoVisualClientRpc(
        int indiceJugadorTurno,
        int[] posicionesCasilleros,
        int valorDado,
        ClientRpcParams parametrosRpc = default
    )
    {
        EstadoVisualActualizado?.Invoke(indiceJugadorTurno, posicionesCasilleros, valorDado);
    }

    [ClientRpc]
    private void RecibirEstadisticasJugadoresClientRpc(
        int[] vueltasPorJugador,
        int[] correctasPorJugador,
        ClientRpcParams parametrosRpc = default
    )
    {
        EstadisticasJugadoresActualizadas?.Invoke(vueltasPorJugador, correctasPorJugador);
    }

    [ClientRpc]
    private void RecibirEstadoPartidaClientRpc(
        FixedString512Bytes estado,
        ulong clientIdDelTurno,
        ClientRpcParams parametrosRpc = default
    )
    {
        EstadoPartidaRecibido?.Invoke(estado.ToString(), clientIdDelTurno);
    }

    [ClientRpc]
    private void RecibirPreguntaClientRpc(
        FixedString128Bytes texto,
        FixedString128Bytes opcion0,
        FixedString128Bytes opcion1,
        FixedString128Bytes opcion2,
        FixedString128Bytes opcion3,
        bool esPreguntaJuego,
        ClientRpcParams parametrosRpc = default
    )
    {
        PreguntaRecibida?.Invoke(
            texto.ToString(),
            new string[]
            {
                opcion0.ToString(),
                opcion1.ToString(),
                opcion2.ToString(),
                opcion3.ToString()
            },
            esPreguntaJuego
        );
    }

    [ClientRpc]
    private void RecibirResultadoClientRpc(
        FixedString128Bytes mensaje,
        ClientRpcParams parametrosRpc = default
    )
    {
        ResultadoRecibido?.Invoke(mensaje.ToString());
    }

    private void AlCambiarResumenSalas(FixedString512Bytes anterior, FixedString512Bytes actual)
    {
        MostrarResumen(actual.ToString());
    }

    private void MostrarResumen(string resumen)
    {
        ResumenActualizado?.Invoke(resumen);
    }
}