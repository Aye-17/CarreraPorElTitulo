using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;



public class JuegoRedUI : MonoBehaviour
{

    
    [System.Serializable]
    private struct SlotJugadorUI
    {
        public string nombreJugador;
        public GameObject contenedorJugador;
        public TextMeshProUGUI textoEstadisticas;
        
        
    }

    [System.Serializable]
    private struct BloquePanelPregunta
    {
        public GameObject panel;
        public TextMeshProUGUI textoPregunta;
        public TextMeshProUGUI textoTemporizador;
        public Button[] botonesOpciones;
    }

    [Header("Estado")]
    [SerializeField] private TextMeshProUGUI textoSesion;
    [SerializeField] private TextMeshProUGUI textoEstadoPartida;
    [SerializeField] private TextMeshProUGUI textoResultado;
    [SerializeField] private Button botonTirarDado;

    [Header("Estadisticas por Jugador (Rojo, Azul, Verde, Amarillo)")]
    [SerializeField] private SlotJugadorUI[] slotsJugadores = new SlotJugadorUI[4];

    [Header("Pregunta Normal")]
    [SerializeField] private BloquePanelPregunta panelPreguntaNormal;

    [Header("Pregunta de Juegos")]
    [SerializeField] private BloquePanelPregunta panelPreguntaJuego;

    [Header("Temporizador y Demora")]
    [SerializeField] private float segundosEsperaPregunta = 1.5f;
    [SerializeField] private float tiempoPorPregunta = 15f;

    private NetworkLobbyServer lobbyServidor;
    private bool esMiTurno;
    private bool preguntaRespondida;
    private BloquePanelPregunta panelActivoActual;
    private Coroutine coroutineMostrarPregunta;
    private Coroutine coroutineTemporizador;

    private void Start()
    {
        lobbyServidor = NetworkLobbyServer.Instance != null
            ? NetworkLobbyServer.Instance
            : FindObjectOfType<NetworkLobbyServer>();

        if (lobbyServidor == null)
        {
            Debug.LogWarning(
                "[JuegoRedUI] JuegoRed debe iniciarse desde LobbyMultijugador."
            );

            if (botonTirarDado != null)
                botonTirarDado.interactable = false;

            if (textoResultado != null)
                textoResultado.text =
                    "Inicia una partida desde LobbyMultijugador.";

            return;
        }

        if (botonTirarDado != null)
            botonTirarDado.interactable = false;

        OcultarTodosLosPanelesPregunta();

        if (botonTirarDado != null)
            botonTirarDado.onClick.AddListener(SolicitarTirarDado);

        lobbyServidor.EstadoPartidaRecibido += ActualizarEstadoPartida;
        lobbyServidor.EstadoVisualActualizado += ActualizarVisual3D;
        lobbyServidor.EstadisticasJugadoresActualizadas += ActualizarEstadisticasJugadores;
        lobbyServidor.ResultadoRecibido += MostrarResultado;
        lobbyServidor.PreguntaRecibida += MostrarPregunta;
        //lobbyServidor.CartelBibliotecaRecibido += MostrarCartelBiblioteca;

        ActualizarTextoSesion();
        lobbyServidor.SolicitarEstadoActualServerRpc();
    }

    private void OnDestroy()
    {
        DetenerEsperaPregunta();
        DetenerTemporizador();

        if (lobbyServidor == null)
            return;

        lobbyServidor.EstadoPartidaRecibido -= ActualizarEstadoPartida;
        lobbyServidor.EstadoVisualActualizado -= ActualizarVisual3D;
        lobbyServidor.EstadisticasJugadoresActualizadas -= ActualizarEstadisticasJugadores;
        lobbyServidor.ResultadoRecibido -= MostrarResultado;
        lobbyServidor.PreguntaRecibida -= MostrarPregunta;
        //lobbyServidor.CartelBibliotecaRecibido -= MostrarCartelBiblioteca;
    }

    private void OcultarTodosLosPanelesPregunta()
    {
        if (panelPreguntaNormal.panel != null)
            panelPreguntaNormal.panel.SetActive(false);

        if (panelPreguntaJuego.panel != null)
            panelPreguntaJuego.panel.SetActive(false);
    }

    private void ActualizarVisual3D(
        int indiceJugadorTurno,
        int[] posicionesCasilleros,
        int valorDado)
    {
        if (GameManager.Instancia != null)
        {
            GameManager.Instancia.SincronizarEstadoDesdeRed(
                indiceJugadorTurno,
                posicionesCasilleros,
                valorDado
            );
        }
    }

    private void ActualizarEstadisticasJugadores(
        int[] vueltasPorJugador,
        int[] correctasPorJugador)
    {
        if (slotsJugadores == null)
            return;

        int cantidadActivos = vueltasPorJugador != null ? vueltasPorJugador.Length : 0;

        for (int i = 0; i < slotsJugadores.Length; i++)
        {
            SlotJugadorUI slot = slotsJugadores[i];
            bool estaEnPartida = i < cantidadActivos;

            if (slot.contenedorJugador != null)
                slot.contenedorJugador.SetActive(estaEnPartida);

            if (!estaEnPartida || slot.textoEstadisticas == null)
                continue;

            int vuelta = vueltasPorJugador[i];
            int correctas = (correctasPorJugador != null && i < correctasPorJugador.Length)
                ? correctasPorJugador[i]
                : 0;

            string etiqueta = string.IsNullOrWhiteSpace(slot.nombreJugador)
                ? $"Jugador {i + 1}"
                : slot.nombreJugador;

            slot.textoEstadisticas.text =
                $"{etiqueta}\nVuelta: {vuelta} | Correctas: {correctas}";
        }
    }

    private void ActualizarTextoSesion()
    {
        if (textoSesion == null)
            return;

        ulong clientId = NetworkManager.Singleton.LocalClientId;

        textoSesion.text =
            $"ID: {clientId} | Sala: {SesionRedCliente.IdSalaActual}";
    }

    private void SolicitarTirarDado()
    {
        if (lobbyServidor != null)
        {
            Debug.Log("[DIAGNOSTICO] Se recibió una solicitud para tirar el dado.");
            lobbyServidor.SolicitarTirarDadoServerRpc();
        }
    }

    private void ActualizarEstadoPartida(
        string estado,
        ulong clientIdDelTurno)
    {
        if (textoEstadoPartida != null)
            textoEstadoPartida.text = estado;

        if (botonTirarDado != null && NetworkManager.Singleton != null)
        {
            bool esMiTurno =
                NetworkManager.Singleton.LocalClientId == clientIdDelTurno;

            this.esMiTurno = esMiTurno;
            botonTirarDado.interactable = esMiTurno;
        }
    }

    private void MostrarResultado(string mensaje)
    {
        if (textoResultado != null)
            textoResultado.text = mensaje;

        Debug.Log($"[RED] {mensaje}");
    }

    private void MostrarCartelBiblioteca()
    {
        if (UIJuego.Instancia == null)
        {
            Debug.LogError(
                "[JuegoRedUI] No se encontró UIJuego para mostrar el cartel."
            );
            return;
        }

        if (botonTirarDado != null)
            botonTirarDado.interactable = false;

        UIJuego.Instancia.MostrarCartelIrABiblioteca(
            () =>
            {
                if (botonTirarDado != null)
                    botonTirarDado.interactable = esMiTurno;
            }
        );
    }

    private void MostrarPregunta(string texto, string[] opciones, bool esPreguntaJuego)
    {
        BloquePanelPregunta bloqueElegido = esPreguntaJuego && panelPreguntaJuego.panel != null
            ? panelPreguntaJuego
            : panelPreguntaNormal;

        if (bloqueElegido.panel == null)
        {
            Debug.LogError("[JuegoRedUI] ERROR: No hay panel de pregunta asignado en el Inspector.");
            return;
        }

        if (botonTirarDado != null)
            botonTirarDado.interactable = false;

        DetenerEsperaPregunta();
        DetenerTemporizador();

        coroutineMostrarPregunta = StartCoroutine(
            MostrarPreguntaConDemora(bloqueElegido, texto, opciones)
        );
    }

    private IEnumerator MostrarPreguntaConDemora(
        BloquePanelPregunta bloque,
        string texto,
        string[] opciones)
    {
        if (segundosEsperaPregunta > 0f)
        {
            yield return new WaitForSeconds(segundosEsperaPregunta);
        }

        coroutineMostrarPregunta = null;

        if (bloque.panel == null)
            yield break;

        OcultarTodosLosPanelesPregunta();
        panelActivoActual = bloque;
        preguntaRespondida = false;

        bloque.panel.SetActive(true);

        if (bloque.textoPregunta != null)
        {
            bloque.textoPregunta.gameObject.SetActive(true);
            bloque.textoPregunta.text = texto;
        }

        if (bloque.botonesOpciones != null && bloque.botonesOpciones.Length > 0)
        {
            for (int i = 0; i < bloque.botonesOpciones.Length; i++)
            {
                Button boton = bloque.botonesOpciones[i];
                if (boton == null) continue;

                int indiceOpcion = i;
                boton.gameObject.SetActive(true);

                TextMeshProUGUI textoBoton =
                    boton.GetComponentInChildren<TextMeshProUGUI>(true);

                if (textoBoton != null)
                {
                    textoBoton.gameObject.SetActive(true);
                    textoBoton.text = (opciones != null && i < opciones.Length)
                        ? opciones[i]
                        : "";
                }

                boton.onClick.RemoveAllListeners();

                if (opciones != null && i < opciones.Length)
                {
                    boton.interactable = true;
                    boton.onClick.AddListener(() => ResponderPregunta(indiceOpcion));
                }
                else
                {
                    boton.interactable = false;
                }
            }
        }

        CanvasGroup cg = bloque.panel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        IniciarTemporizador(bloque.textoTemporizador);
    }

    private void DetenerEsperaPregunta()
    {
        if (coroutineMostrarPregunta != null)
        {
            StopCoroutine(coroutineMostrarPregunta);
            coroutineMostrarPregunta = null;
        }
    }

    private void IniciarTemporizador(TextMeshProUGUI textoTimer)
    {
        DetenerTemporizador();

        if (textoTimer != null)
        {
            textoTimer.gameObject.SetActive(true);
            textoTimer.text =
                "TIEMPO: " + Mathf.CeilToInt(tiempoPorPregunta);
        }

        coroutineTemporizador = StartCoroutine(TemporizadorPreguntaRed(textoTimer));
    }

    private void DetenerTemporizador()
    {
        if (coroutineTemporizador != null)
        {
            StopCoroutine(coroutineTemporizador);
            coroutineTemporizador = null;
        }
    }

    private IEnumerator TemporizadorPreguntaRed(TextMeshProUGUI textoTimer)
    {
        float tiempoRestante = tiempoPorPregunta;

        while (tiempoRestante > 0f)
        {
            if (preguntaRespondida)
                yield break;

            if (textoTimer != null)
            {
                textoTimer.text =
                    "TIEMPO: " + Mathf.CeilToInt(tiempoRestante);
            }

            yield return null;
            tiempoRestante -= Time.deltaTime;
        }

        if (preguntaRespondida)
            yield break;

        preguntaRespondida = true;

        if (textoTimer != null)
            textoTimer.text = "TIEMPO: 0";

        OcultarTodosLosPanelesPregunta();

        if (lobbyServidor != null)
            lobbyServidor.SolicitarResponderPreguntaServerRpc(99);

        coroutineTemporizador = null;
    }

    private void ResponderPregunta(int opcionSeleccionada)
    {
        if (preguntaRespondida)
            return;

        preguntaRespondida = true;
        DetenerEsperaPregunta();
        DetenerTemporizador();
        OcultarTodosLosPanelesPregunta();

        if (lobbyServidor != null)
            lobbyServidor.SolicitarResponderPreguntaServerRpc(opcionSeleccionada);
    }
}