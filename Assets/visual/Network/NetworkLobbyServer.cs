using System;
using System.Collections.Generic;
using System.Linq;
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
    //public event Action CartelBibliotecaRecibido;
    public event Action<int, int[], int> EstadoVisualActualizado;
    public event Action<int[], int[]> EstadisticasJugadoresActualizadas;
    public event Action<ulong, ulong[]> SeleccionPongRecibida;
    public event Action<ulong, ulong, ulong[]> PongPartidaIniciadaRecibida;
    public event Action<float, float, float, float, int, int> PongEstadoRecibido;
    public event Action<ulong, int, int> PongFinalizadoRecibido;

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
    [SerializeField] private int puntosParaGanarPong = 5;
    [SerializeField] private float velocidadPelotaPong = 550f;
    [SerializeField] private float velocidadPaletaPong = 650f;

    private BancoPreguntas bancoPreguntas;
    private BancoPreguntasJuegos bancoPreguntasJuegos;

    private readonly Dictionary<string, PartidaServidor> partidasPorSala =
        new Dictionary<string, PartidaServidor>();

    private readonly Dictionary<ulong, string> salaPorCliente =
        new Dictionary<ulong, string>();

    private readonly Dictionary<string, EstadoPongServidor> pongPorSala =
        new Dictionary<string, EstadoPongServidor>();

    private const float IntervaloActualizacionPong = 0.05f;

    private sealed class EstadoPongServidor
    {
        public ulong ClientIdRetador;
        public ulong ClientIdRival;
        public ulong ClientIdGanador;
        public ulong[] Participantes;
        public bool RivalSeleccionado;
        public bool EnCurso;
        public bool Terminado;
        public float MovimientoIzquierda;
        public float MovimientoDerecha;
        public float PosicionIzquierda;
        public float PosicionDerecha;
        public float PelotaX;
        public float PelotaY;
        public float DireccionX;
        public float DireccionY;
        public float TiempoDesdeEnvio;
        public int PuntosIzquierda;
        public int PuntosDerecha;
    }

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
        pongPorSala.Clear();
    }

    private void Update()
    {
        if (!IsServer || pongPorSala.Count == 0)
            return;

        float deltaTiempo = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.05f);
        float deltaEnvio = Time.unscaledDeltaTime;
        List<string> partidasFinalizadas = new List<string>();

        foreach (KeyValuePair<string, EstadoPongServidor> entrada in pongPorSala)
        {
            EstadoPongServidor pong = entrada.Value;
            if (!pong.EnCurso)
                continue;

            SimularPong(pong, deltaTiempo);

            pong.TiempoDesdeEnvio += deltaEnvio;
            if (pong.TiempoDesdeEnvio >= IntervaloActualizacionPong)
            {
                pong.TiempoDesdeEnvio = 0f;
                EnviarEstadoPong(entrada.Key, pong);
            }

            if (pong.Terminado)
            {
                partidasFinalizadas.Add(entrada.Key);
            }
        }

        for (int i = 0; i < partidasFinalizadas.Count; i++)
            FinalizarPongEnServidor(partidasFinalizadas[i]);
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
                ConfiguracionPartida.Crear(ModoPartida.Rapido),
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
    public void SolicitarSeleccionarRivalPongServerRpc(
        ulong clientIdRival,
        ServerRpcParams parametrosRpc = default)
    {
        ulong clientIdRetador = parametrosRpc.Receive.SenderClientId;

        if (!salaPorCliente.TryGetValue(clientIdRetador, out string idSala) ||
            !pongPorSala.TryGetValue(idSala, out EstadoPongServidor pong))
        {
            EnviarResultadoACliente(clientIdRetador, "No hay una selección de Pong pendiente.");
            return;
        }

        Sala sala = gestorSalas.ObtenerSala(idSala);
        if (sala == null ||
            pong.RivalSeleccionado ||
            clientIdRetador != pong.ClientIdRetador ||
            clientIdRival == clientIdRetador ||
            !sala.Jugadores.Contains(clientIdRival) ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientIdRival))
        {
            EnviarResultadoACliente(clientIdRetador, "No se puede elegir ese rival para el Pong.");
            return;
        }

        if (!partidasPorSala.TryGetValue(idSala, out PartidaServidor partida))
        {
            EnviarResultadoACliente(clientIdRetador, "No se encontró la partida de la sala.");
            return;
        }

        pong.ClientIdRival = clientIdRival;
        pong.RivalSeleccionado = true;
        pong.EnCurso = true;
        pong.PosicionIzquierda = 0f;
        pong.PosicionDerecha = 0f;
        pong.PelotaX = 0f;
        pong.PelotaY = 0f;
        pong.DireccionX = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        pong.DireccionY = UnityEngine.Random.Range(-0.6f, 0.6f);
        Vector2 direccionInicial = new Vector2(pong.DireccionX, pong.DireccionY).normalized;
        pong.DireccionX = direccionInicial.x;
        pong.DireccionY = direccionInicial.y;

        ulong[] destinatarios = ObtenerDestinatariosSala(sala);
        ClientRpcParams parametrosCliente = CrearParametrosCliente(destinatarios);

        RecibirPongPartidaIniciadaClientRpc(
            pong.ClientIdRetador,
            pong.ClientIdRival,
            pong.Participantes,
            parametrosCliente
        );

        EnviarEstadoPong(idSala, pong);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarControlPongServerRpc(
        float movimiento,
        ServerRpcParams parametrosRpc = default)
    {
        ulong clientId = parametrosRpc.Receive.SenderClientId;

        if (!salaPorCliente.TryGetValue(clientId, out string idSala) ||
            !pongPorSala.TryGetValue(idSala, out EstadoPongServidor pong) ||
            !pong.EnCurso)
        {
            Debug.LogWarning($"[PONG RED] Se rechazó el control del cliente {clientId}: no hay una partida activa.");
            return;
        }

        if (float.IsNaN(movimiento) || float.IsInfinity(movimiento))
        {
            Debug.LogWarning($"[PONG RED] Se rechazó un movimiento inválido del cliente {clientId}.");
            return;
        }

        movimiento = Mathf.Clamp(movimiento, -1f, 1f);

        if (clientId == pong.ClientIdRetador)
            pong.MovimientoIzquierda = movimiento;
        else if (clientId == pong.ClientIdRival)
            pong.MovimientoDerecha = movimiento;
        else
            Debug.LogWarning($"[PONG RED] El cliente {clientId} no participa en esta partida.");
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
        ulong clientIdTurnoVisible =
            partida.Fase == FasePartidaServidor.EsperandoMinijuego
                ? ulong.MaxValue
                : partida.ClientIdDelTurno;

        RecibirEstadoPartidaClientRpc(
            new FixedString512Bytes(estado.Length > 500 ? estado.Substring(0, 500) : estado),
            clientIdTurnoVisible,
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

        if (evento.Tipo == TipoEventoPartida.MinijuegoPendiente)
            IniciarSeleccionPong(sala, partida);
    }

    private void IniciarSeleccionPong(Sala sala, PartidaServidor partida)
    {
        if (!IsServer || pongPorSala.ContainsKey(sala.Id))
            return;

        EstadoPongServidor pong = new EstadoPongServidor
        {
            ClientIdRetador = partida.ClientIdDelTurno,
            Participantes = sala.Jugadores.ToArray()
        };

        if (!pong.Participantes.Contains(pong.ClientIdRetador))
        {
            Debug.LogError("[PONG RED] El jugador del turno no pertenece a la sala.");
            return;
        }

        pongPorSala.Add(sala.Id, pong);

        RecibirSeleccionPongClientRpc(
            pong.ClientIdRetador,
            pong.Participantes,
            CrearParametrosCliente(ObtenerDestinatariosSala(sala))
        );
    }

    private void SimularPong(EstadoPongServidor pong, float deltaTiempo)
    {
        pong.PosicionIzquierda = Mathf.Clamp(
            pong.PosicionIzquierda +
            pong.MovimientoIzquierda * velocidadPaletaPong * deltaTiempo,
            -200f,
            200f
        );

        pong.PosicionDerecha = Mathf.Clamp(
            pong.PosicionDerecha +
            pong.MovimientoDerecha * velocidadPaletaPong * deltaTiempo,
            -200f,
            200f
        );

        pong.PelotaX += pong.DireccionX * velocidadPelotaPong * deltaTiempo;
        pong.PelotaY += pong.DireccionY * velocidadPelotaPong * deltaTiempo;

        if (pong.PelotaY >= 245f)
        {
            pong.PelotaY = 245f;
            pong.DireccionY = -Mathf.Abs(pong.DireccionY);
        }
        else if (pong.PelotaY <= -245f)
        {
            pong.PelotaY = -245f;
            pong.DireccionY = Mathf.Abs(pong.DireccionY);
        }

        if (pong.DireccionX < 0f &&
            Mathf.Abs(pong.PelotaX + 430f) < 30f &&
            Mathf.Abs(pong.PelotaY - pong.PosicionIzquierda) < 70f)
        {
            pong.PelotaX = -400f;
            Vector2 direccion = new Vector2(
                Mathf.Abs(pong.DireccionX),
                pong.DireccionY + UnityEngine.Random.Range(-0.25f, 0.25f)
            ).normalized;
            pong.DireccionX = direccion.x;
            pong.DireccionY = direccion.y;
        }
        else if (pong.DireccionX > 0f &&
                 Mathf.Abs(pong.PelotaX - 430f) < 30f &&
                 Mathf.Abs(pong.PelotaY - pong.PosicionDerecha) < 70f)
        {
            pong.PelotaX = 400f;
            Vector2 direccion = new Vector2(
                -Mathf.Abs(pong.DireccionX),
                pong.DireccionY + UnityEngine.Random.Range(-0.25f, 0.25f)
            ).normalized;
            pong.DireccionX = direccion.x;
            pong.DireccionY = direccion.y;
        }

        if (pong.PelotaX < -520f)
        {
            pong.PuntosDerecha++;
            if (pong.PuntosDerecha >= puntosParaGanarPong)
            {
                pong.ClientIdGanador = pong.ClientIdRival;
                pong.Terminado = true;
                return;
            }

            ReiniciarPelotaPong(pong, -1f);
        }
        else if (pong.PelotaX > 520f)
        {
            pong.PuntosIzquierda++;
            if (pong.PuntosIzquierda >= puntosParaGanarPong)
            {
                pong.ClientIdGanador = pong.ClientIdRetador;
                pong.Terminado = true;
                return;
            }

            ReiniciarPelotaPong(pong, 1f);
        }
    }

    private void ReiniciarPelotaPong(EstadoPongServidor pong, float direccionX)
    {
        pong.PelotaX = 0f;
        pong.PelotaY = 0f;
        Vector2 direccion = new Vector2(
            direccionX,
            UnityEngine.Random.Range(-0.6f, 0.6f)
        ).normalized;
        pong.DireccionX = direccion.x;
        pong.DireccionY = direccion.y;
    }

    private void EnviarEstadoPong(string idSala, EstadoPongServidor pong)
    {
        Sala sala = gestorSalas.ObtenerSala(idSala);
        if (sala == null)
            return;

        RecibirEstadoPongClientRpc(
            pong.PosicionIzquierda,
            pong.PosicionDerecha,
            pong.PelotaX,
            pong.PelotaY,
            pong.PuntosIzquierda,
            pong.PuntosDerecha,
            CrearParametrosCliente(ObtenerDestinatariosSala(sala))
        );
    }

    private void FinalizarPongEnServidor(string idSala)
    {
        if (!pongPorSala.TryGetValue(idSala, out EstadoPongServidor pong))
            return;

        if (!partidasPorSala.TryGetValue(idSala, out PartidaServidor partida))
        {
            Debug.LogError($"[PONG RED] No se encontró la partida de {idSala} al finalizar.");
            pongPorSala.Remove(idSala);
            return;
        }

        Sala sala = gestorSalas.ObtenerSala(idSala);
        if (sala == null)
        {
            Debug.LogError($"[PONG RED] No se encontró la sala {idSala} al finalizar.");
            pongPorSala.Remove(idSala);
            return;
        }

        EventoPartidaServidor evento = partida.FinalizarMinijuego(pong.ClientIdGanador);
        if (!evento.EsValido)
        {
            Debug.LogError($"[PONG RED] No se pudo finalizar el minijuego: {evento.Mensaje}");
            pongPorSala.Remove(idSala);
            return;
        }

        ulong[] destinatarios = ObtenerDestinatariosSala(sala);
        RecibirPongFinalizadoClientRpc(
            pong.ClientIdGanador,
            pong.PuntosIzquierda,
            pong.PuntosDerecha,
            CrearParametrosCliente(destinatarios)
        );

        pongPorSala.Remove(idSala);
        EnviarEstadoPartidaASala(sala, partida, evento, pong.ClientIdGanador);
    }

    private ulong[] ObtenerDestinatariosSala(Sala sala)
    {
        List<ulong> destinatarios = new List<ulong>();
        destinatarios.AddRange(sala.Jugadores);
        destinatarios.AddRange(sala.Espectadores);
        return destinatarios.ToArray();
    }

    private ClientRpcParams CrearParametrosCliente(ulong[] destinatarios)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = destinatarios
            }
        };
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
    private void RecibirSeleccionPongClientRpc(
        ulong clientIdRetador,
        ulong[] participantes,
        ClientRpcParams parametrosRpc = default)
    {
        SeleccionPongRecibida?.Invoke(clientIdRetador, participantes);
    }

    [ClientRpc]
    private void RecibirPongPartidaIniciadaClientRpc(
        ulong clientIdRetador,
        ulong clientIdRival,
        ulong[] participantes,
        ClientRpcParams parametrosRpc = default)
    {
        PongPartidaIniciadaRecibida?.Invoke(
            clientIdRetador,
            clientIdRival,
            participantes
        );
    }

    [ClientRpc]
    private void RecibirEstadoPongClientRpc(
        float posicionIzquierda,
        float posicionDerecha,
        float pelotaX,
        float pelotaY,
        int puntosIzquierda,
        int puntosDerecha,
        ClientRpcParams parametrosRpc = default)
    {
        PongEstadoRecibido?.Invoke(
            posicionIzquierda,
            posicionDerecha,
            pelotaX,
            pelotaY,
            puntosIzquierda,
            puntosDerecha
        );
    }

    [ClientRpc]
    private void RecibirPongFinalizadoClientRpc(
        ulong clientIdGanador,
        int puntosIzquierda,
        int puntosDerecha,
        ClientRpcParams parametrosRpc = default)
    {
        PongFinalizadoRecibido?.Invoke(
            clientIdGanador,
            puntosIzquierda,
            puntosDerecha
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