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

    [Header("Estado")]
    [SerializeField] private TextMeshProUGUI textoSesion;
    [SerializeField] private TextMeshProUGUI textoEstadoPartida;
    [SerializeField] private TextMeshProUGUI textoResultado;
    [SerializeField] private Button botonTirarDado;

    [Header("Estadisticas por Jugador (Rojo, Azul, Verde, Amarillo)")]
    [SerializeField] private SlotJugadorUI[] slotsJugadores = new SlotJugadorUI[4];

    [Header("Pregunta")]
    [SerializeField] private GameObject panelPreguntaRed;
    [SerializeField] private TextMeshProUGUI textoPreguntaRed;
    [SerializeField] private TextMeshProUGUI textoTemporizadorRed;
    [SerializeField] private Button[] botonesOpcionesRed;

    [Header("Temporizador")]
    [SerializeField] private float tiempoPorPregunta = 15f;

    private NetworkLobbyServer lobbyServidor;
    private bool esMiTurno;
    private bool preguntaRespondida;
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

        if (panelPreguntaRed != null)
            panelPreguntaRed.SetActive(false);

        if (botonTirarDado != null)
            botonTirarDado.onClick.AddListener(SolicitarTirarDado);

        lobbyServidor.EstadoPartidaRecibido += ActualizarEstadoPartida;
        lobbyServidor.EstadoVisualActualizado += ActualizarVisual3D;
        lobbyServidor.EstadisticasJugadoresActualizadas += ActualizarEstadisticasJugadores;
        lobbyServidor.ResultadoRecibido += MostrarResultado;
        lobbyServidor.PreguntaRecibida += MostrarPregunta;
        lobbyServidor.CartelBibliotecaRecibido += MostrarCartelBiblioteca;

        ActualizarTextoSesion();
        lobbyServidor.SolicitarEstadoActualServerRpc();
    }

    private void OnDestroy()
    {
        DetenerTemporizador();

        if (lobbyServidor == null)
            return;

        lobbyServidor.EstadoPartidaRecibido -= ActualizarEstadoPartida;
        lobbyServidor.EstadoVisualActualizado -= ActualizarVisual3D;
        lobbyServidor.EstadisticasJugadoresActualizadas -= ActualizarEstadisticasJugadores;
        lobbyServidor.ResultadoRecibido -= MostrarResultado;
        lobbyServidor.PreguntaRecibida -= MostrarPregunta;
        lobbyServidor.CartelBibliotecaRecibido -= MostrarCartelBiblioteca;
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

    private void MostrarPregunta(string texto, string[] opciones)
    {
        Debug.Log("[DIAGNOSTICO] Se ejecutó MostrarPregunta en la interfaz.");
        Debug.Log($"[JuegoRedUI] MOSTRANDO PREGUNTA: {texto}");

        if (panelPreguntaRed == null)
        {
            Debug.LogError("[JuegoRedUI] ERROR: PanelPreguntaRed no está asignado en el Inspector.");
            return;
        }

        preguntaRespondida = false;

        if (botonTirarDado != null)
            botonTirarDado.interactable = false;

        panelPreguntaRed.SetActive(true);

        if (textoPreguntaRed != null)
        {
            textoPreguntaRed.gameObject.SetActive(true);
            textoPreguntaRed.text = texto;
        }
        else
        {
            Debug.LogError("[JuegoRedUI] No está asignado TextoPreguntaRed.");
        }

        if (botonesOpcionesRed == null || botonesOpcionesRed.Length == 0)
        {
            Debug.LogError("[JuegoRedUI] No hay botones de opciones asignados.");
            return;
        }

        for (int i = 0; i < botonesOpcionesRed.Length; i++)
        {
            Button boton = botonesOpcionesRed[i];

            if (boton == null)
                continue;

            int indiceOpcion = i;

            boton.gameObject.SetActive(true);

            TextMeshProUGUI textoBoton =
                boton.GetComponentInChildren<TextMeshProUGUI>(true);

            if (textoBoton != null)
            {
                textoBoton.gameObject.SetActive(true);

                if (opciones != null && i < opciones.Length)
                    textoBoton.text = opciones[i];
                else
                    textoBoton.text = "";
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

        CanvasGroup cg = panelPreguntaRed.GetComponent<CanvasGroup>();

        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        IniciarTemporizador();

        Debug.Log("[JuegoRedUI] Pregunta mostrada y configurada correctamente.");
    }

    private void IniciarTemporizador()
    {
        DetenerTemporizador();

        if (textoTemporizadorRed != null)
        {
            textoTemporizadorRed.gameObject.SetActive(true);
            textoTemporizadorRed.text =
                "TIEMPO: " + Mathf.CeilToInt(tiempoPorPregunta);
        }

        coroutineTemporizador = StartCoroutine(TemporizadorPreguntaRed());
    }

    private void DetenerTemporizador()
    {
        if (coroutineTemporizador != null)
        {
            StopCoroutine(coroutineTemporizador);
            coroutineTemporizador = null;
        }
    }

    private IEnumerator TemporizadorPreguntaRed()
    {
        float tiempoRestante = tiempoPorPregunta;

        while (tiempoRestante > 0f)
        {
            if (preguntaRespondida)
                yield break;

            if (textoTemporizadorRed != null)
            {
                textoTemporizadorRed.text =
                    "TIEMPO: " + Mathf.CeilToInt(tiempoRestante);
            }

            yield return null;
            tiempoRestante -= Time.deltaTime;
        }

        if (preguntaRespondida)
            yield break;

        preguntaRespondida = true;

        if (textoTemporizadorRed != null)
            textoTemporizadorRed.text = "TIEMPO: 0";

        if (panelPreguntaRed != null)
            panelPreguntaRed.SetActive(false);

        if (lobbyServidor != null)
            lobbyServidor.SolicitarResponderPreguntaServerRpc(99);

        coroutineTemporizador = null;
    }

    private void ResponderPregunta(int opcionSeleccionada)
    {
        if (preguntaRespondida)
            return;

        preguntaRespondida = true;
        DetenerTemporizador();

        Debug.Log($"[JuegoRedUI] Botón de respuesta presionado. Opción elegida: {opcionSeleccionada}");

        if (panelPreguntaRed != null)
            panelPreguntaRed.SetActive(false);

        if (lobbyServidor != null)
            lobbyServidor.SolicitarResponderPreguntaServerRpc(opcionSeleccionada);
    }
}