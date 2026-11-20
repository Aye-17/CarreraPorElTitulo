using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MiniJuegoManager : MonoBehaviour
{
    public static MiniJuegoManager Instancia;

    // =========================================================
    // CONFIGURACIÓN
    // =========================================================

    [Header("Configuración del Pong")]
    public int puntosParaGanar = 5;

    public float velocidadPelota = 550f;

    public float velocidadPaleta = 650f;

    // =========================================================
    // JUGADORES
    // =========================================================

    private Jugador jugadorRetador;
    private Jugador jugadorRetado;

    // =========================================================
    // UI
    // =========================================================

    private Canvas canvas;

    private GameObject panelPrincipal;
    private GameObject panelSeleccion;
    private GameObject panelPong;
    private GameObject panelResultado;

    private TextMeshProUGUI tituloSeleccion;

    private TextMeshProUGUI textoJugadorIzquierda;
    private TextMeshProUGUI textoJugadorDerecha;

    private TextMeshProUGUI textoMarcador;

    private TextMeshProUGUI textoResultado;

    // =========================================================
    // PONG
    // =========================================================

    private RectTransform cancha;

    private RectTransform paletaIzquierda;
    private RectTransform paletaDerecha;
    private RectTransform pelota;

    private Image imagenPelota;

    private Vector2 direccionPelota;

    private int puntosIzquierda;
    private int puntosDerecha;

    private bool jugando = false;
    private bool modoRedPongActivo;
    private bool resultadoRedPong;
    private ulong clientIdRetadorRed;
    private ulong clientIdRivalRed;
    private ulong[] participantesPongRed;
    private float tiempoEnvioControlRed;
    private NetworkLobbyServer lobbyServidor;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;

        CrearCanvas();
        CrearInterfaz();

        OcultarTodo();
    }

    private void Start()
    {
        lobbyServidor = NetworkLobbyServer.Instance != null
            ? NetworkLobbyServer.Instance
            : FindObjectOfType<NetworkLobbyServer>();

        if (lobbyServidor == null)
            return;

        lobbyServidor.SeleccionPongRecibida += MostrarSeleccionPongRed;
        lobbyServidor.PongPartidaIniciadaRecibida += IniciarPongRed;
        lobbyServidor.PongEstadoRecibido += AplicarEstadoPongRed;
        lobbyServidor.PongFinalizadoRecibido += MostrarResultadoPongRed;
    }

    private void OnDestroy()
    {
        if (lobbyServidor == null)
            return;

        lobbyServidor.SeleccionPongRecibida -= MostrarSeleccionPongRed;
        lobbyServidor.PongPartidaIniciadaRecibida -= IniciarPongRed;
        lobbyServidor.PongEstadoRecibido -= AplicarEstadoPongRed;
        lobbyServidor.PongFinalizadoRecibido -= MostrarResultadoPongRed;
    }

    private void Update()
    {
        if (modoRedPongActivo)
        {
            ActualizarControlesPongRed();
            return;
        }

        if (!jugando)
            return;

        MoverPaletas();
        MoverPelota();
    }

    // =========================================================
    // INICIAR MINIJUEGO
    // =========================================================

    public void IniciarMinijuego(Jugador jugador)
    {
        if (jugador == null)
            return;

        jugadorRetador = jugador;

        puntosIzquierda = 0;
        puntosDerecha = 0;

        jugando = false;

        panelPrincipal.SetActive(true);
        panelSeleccion.SetActive(true);
        panelPong.SetActive(false);
        panelResultado.SetActive(false);

        tituloSeleccion.text =
            jugadorRetador.Nombre +
            "\nELIGE A QUIÉN RETAR";

        CrearBotonesJugadores();
    }

    private void MostrarSeleccionPongRed(
        ulong clientIdRetador,
        ulong[] participantes)
    {
        modoRedPongActivo = false;
        jugando = false;
        resultadoRedPong = false;
        clientIdRetadorRed = clientIdRetador;
        participantesPongRed = participantes;

        panelPrincipal.SetActive(true);
        panelSeleccion.SetActive(true);
        panelPong.SetActive(false);
        panelResultado.SetActive(false);

        bool soyRetador =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClientId == clientIdRetador;

        if (soyRetador)
        {
            tituloSeleccion.text = "ELIGE A QUIÉN RETAR\nTodos verán la partida";
            CrearBotonesRivalesPongRed();
        }
        else
        {
            tituloSeleccion.text =
                $"Jugador {ObtenerNumeroJugadorRed(clientIdRetador, participantes)} " +
                "está eligiendo rival.\nTodos verán el Pong.";
            LimpiarBotonesRivalesPongRed();
        }
    }

    private void CrearBotonesRivalesPongRed()
    {
        LimpiarBotonesRivalesPongRed();

        if (participantesPongRed == null)
            return;

        int cantidadBotones = 0;
        for (int i = 0; i < participantesPongRed.Length; i++)
        {
            ulong clientIdRival = participantesPongRed[i];
            if (clientIdRival == clientIdRetadorRed)
                continue;

            GameObject botonObjeto = CrearBoton(
                panelSeleccion.transform,
                "BotonRivalRed" + i,
                $"Jugador {i + 1}"
            );

            RectTransform rect = botonObjeto.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420, 70);
            rect.anchoredPosition = new Vector2(0, -120 - cantidadBotones * 90);

            Button boton = botonObjeto.GetComponent<Button>();
            boton.onClick.AddListener(() =>
            {
                if (lobbyServidor == null)
                {
                    Debug.LogError("[MiniJuegoManager] No se encontró NetworkLobbyServer.");
                    return;
                }

                boton.interactable = false;
                tituloSeleccion.text = "Esperando que el servidor inicie el Pong...";
                lobbyServidor.SolicitarSeleccionarRivalPongServerRpc(clientIdRival);
            });

            cantidadBotones++;
        }
    }

    private void LimpiarBotonesRivalesPongRed()
    {
        for (int i = panelSeleccion.transform.childCount - 1; i >= 0; i--)
        {
            Transform hijo = panelSeleccion.transform.GetChild(i);
            if (hijo.name.StartsWith("BotonRivalRed"))
                Destroy(hijo.gameObject);
        }
    }

    private void IniciarPongRed(
        ulong clientIdRetador,
        ulong clientIdRival,
        ulong[] participantes)
    {
        modoRedPongActivo = true;
        resultadoRedPong = false;
        jugando = false;
        clientIdRetadorRed = clientIdRetador;
        clientIdRivalRed = clientIdRival;
        participantesPongRed = participantes;
        tiempoEnvioControlRed = 0f;

        panelPrincipal.SetActive(true);
        panelSeleccion.SetActive(false);
        panelPong.SetActive(true);
        panelResultado.SetActive(false);

        textoJugadorIzquierda.text =
            $"Jugador {ObtenerNumeroJugadorRed(clientIdRetador, participantes)} (W/S)";
        textoJugadorDerecha.text =
            $"Jugador {ObtenerNumeroJugadorRed(clientIdRival, participantes)} (↑/↓)";

        paletaIzquierda.anchoredPosition = new Vector2(-430f, 0f);
        paletaDerecha.anchoredPosition = new Vector2(430f, 0f);
        pelota.anchoredPosition = Vector2.zero;
        puntosIzquierda = 0;
        puntosDerecha = 0;
        ActualizarMarcador();
    }

    private void AplicarEstadoPongRed(
        float posicionIzquierda,
        float posicionDerecha,
        float pelotaX,
        float pelotaY,
        int marcadorIzquierda,
        int marcadorDerecha)
    {
        if (!modoRedPongActivo)
            return;

        paletaIzquierda.anchoredPosition = new Vector2(-430f, posicionIzquierda);
        paletaDerecha.anchoredPosition = new Vector2(430f, posicionDerecha);
        pelota.anchoredPosition = new Vector2(pelotaX, pelotaY);
        puntosIzquierda = marcadorIzquierda;
        puntosDerecha = marcadorDerecha;
        ActualizarMarcador();
    }

    private void MostrarResultadoPongRed(
        ulong clientIdGanador,
        int marcadorIzquierda,
        int marcadorDerecha)
    {
        modoRedPongActivo = false;
        jugando = false;
        resultadoRedPong = true;
        panelPong.SetActive(false);
        panelResultado.SetActive(true);

        int numeroGanador = ObtenerNumeroJugadorRed(
            clientIdGanador,
            participantesPongRed
        );

        textoResultado.text =
            $"¡GANÓ EL JUGADOR {numeroGanador}!\n\n" +
            $"{marcadorIzquierda} - {marcadorDerecha}\n\n" +
            "+1 RESPUESTA CORRECTA";
    }

    private void ActualizarControlesPongRed()
    {
        if (lobbyServidor == null || NetworkManager.Singleton == null)
            return;

        ulong clientIdLocal = NetworkManager.Singleton.LocalClientId;
        float movimiento = 0f;

        if (clientIdLocal == clientIdRetadorRed)
        {
            if (Input.GetKey(KeyCode.W))
                movimiento = 1f;
            else if (Input.GetKey(KeyCode.S))
                movimiento = -1f;
        }
        else if (clientIdLocal == clientIdRivalRed)
        {
            if (Input.GetKey(KeyCode.UpArrow))
                movimiento = 1f;
            else if (Input.GetKey(KeyCode.DownArrow))
                movimiento = -1f;
        }
        else
        {
            return;
        }

        tiempoEnvioControlRed += Time.unscaledDeltaTime;
        if (tiempoEnvioControlRed < 0.05f)
            return;

        tiempoEnvioControlRed = 0f;
        lobbyServidor.SolicitarControlPongServerRpc(movimiento);
    }

    private int ObtenerNumeroJugadorRed(
        ulong clientId,
        ulong[] participantes)
    {
        if (participantes != null)
        {
            for (int i = 0; i < participantes.Length; i++)
            {
                if (participantes[i] == clientId)
                    return i + 1;
            }
        }

        return 0;
    }

    // =========================================================
    // BOTONES DE JUGADORES
    // =========================================================

    private void CrearBotonesJugadores()
    {
        // Eliminamos botones anteriores
        for (int i = panelSeleccion.transform.childCount - 1; i >= 0; i--)
        {
            Transform hijo =
                panelSeleccion.transform.GetChild(i);

            if (hijo.name.StartsWith("BotonRival"))
            {
                Destroy(hijo.gameObject);
            }
        }

        int cantidadBotones = 0;

        for (int i = 0; i < 4; i++)
        {
            Jugador jugador =
                GameManager.Instancia.ObtenerJugador(i);

            if (jugador == null)
                continue;

            if (jugador == jugadorRetador)
                continue;

            int indiceJugador = i;

            GameObject botonObjeto =
                CrearBoton(
                    panelSeleccion.transform,
                    "BotonRival" + i,
                    jugador.Nombre
                );

            RectTransform rect =
                botonObjeto.GetComponent<RectTransform>();

            rect.sizeDelta =
                new Vector2(420, 70);

            rect.anchoredPosition =
                new Vector2(
                    0,
                    -120 - cantidadBotones * 90
                );

            Button boton =
                botonObjeto.GetComponent<Button>();

            boton.onClick.AddListener(
                () =>
                {
                    Jugador rival =
                        GameManager.Instancia
                        .ObtenerJugador(indiceJugador);

                    SeleccionarRival(rival);
                }
            );

            cantidadBotones++;
        }
    }

    private void SeleccionarRival(Jugador rival)
    {
        if (rival == null)
            return;

        jugadorRetado = rival;

        panelSeleccion.SetActive(false);
        panelPong.SetActive(true);

        PrepararPong();

        StartCoroutine(ComenzarPong());
    }

    // =========================================================
    // PREPARAR PONG
    // =========================================================

    private void PrepararPong()
    {
        puntosIzquierda = 0;
        puntosDerecha = 0;

        textoJugadorIzquierda.text =
            jugadorRetador.Nombre;

        textoJugadorDerecha.text =
            jugadorRetado.Nombre;

        ActualizarMarcador();

        paletaIzquierda.anchoredPosition =
            new Vector2(
                -430,
                0
            );

        paletaDerecha.anchoredPosition =
            new Vector2(
                430,
                0
            );

        pelota.anchoredPosition =
            Vector2.zero;

        direccionPelota =
            new Vector2(
                Random.value > 0.5f ? 1 : -1,
                Random.Range(-0.6f, 0.6f)
            ).normalized;
    }

    private IEnumerator ComenzarPong()
    {
        jugando = false;

        yield return new WaitForSecondsRealtime(1f);

        jugando = true;
    }

    // =========================================================
    // PALETAS
    // =========================================================

    private void MoverPaletas()
    {
        float movimientoIzquierda = 0f;

        if (Input.GetKey(KeyCode.W))
        {
             movimientoIzquierda = 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            movimientoIzquierda = -1f;
        }

        // El jugador de la izquierda usa W/S.
        // El jugador de la derecha usa flechas.
        float movimientoDerecha = 0f;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            movimientoDerecha = 1f;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            movimientoDerecha = -1f;
        }

        // Si ambos jugadores usan el teclado:
        // izquierda = W/S
        // derecha = flechas

        if (
            Input.GetKey(KeyCode.W)
        )
        {
            movimientoIzquierda = 1f;
        }

        if (
            Input.GetKey(KeyCode.S)
        )
        {
            movimientoIzquierda = -1f;
        }

        MoverPaleta(
            paletaIzquierda,
            movimientoIzquierda
        );

        MoverPaleta(
            paletaDerecha,
            movimientoDerecha
        );
    }

    private void MoverPaleta(
        RectTransform paleta,
        float movimiento
    )
    {
        if (paleta == null)
            return;

        Vector2 posicion =
            paleta.anchoredPosition;

        posicion.y +=
            movimiento *
            velocidadPaleta *
            Time.unscaledDeltaTime;

        posicion.y =
            Mathf.Clamp(
                posicion.y,
                -200,
                200
            );

        paleta.anchoredPosition =
            posicion;
    }

    // =========================================================
    // PELOTA
    // =========================================================

    private void MoverPelota()
    {
        Vector2 posicion =
            pelota.anchoredPosition;

        posicion +=
            direccionPelota *
            velocidadPelota *
            Time.unscaledDeltaTime;

        // ARRIBA / ABAJO
        if (posicion.y >= 245)
        {
            posicion.y = 245;

            direccionPelota.y =
                -Mathf.Abs(
                    direccionPelota.y
                );
        }

        if (posicion.y <= -245)
        {
            posicion.y = -245;

            direccionPelota.y =
                Mathf.Abs(
                    direccionPelota.y
                );
        }

        // PALETA IZQUIERDA
        if (
            direccionPelota.x < 0 &&
            Mathf.Abs(
                posicion.x -
                paletaIzquierda.anchoredPosition.x
            ) < 30 &&
            Mathf.Abs(
                posicion.y -
                paletaIzquierda.anchoredPosition.y
            ) < 70
        )
        {
            posicion.x =
                paletaIzquierda.anchoredPosition.x
                + 30;

            direccionPelota.x =
                Mathf.Abs(
                    direccionPelota.x
                );

            direccionPelota.y +=
                Random.Range(-0.25f, 0.25f);

            direccionPelota.Normalize();
        }

        // PALETA DERECHA
        if (
            direccionPelota.x > 0 &&
            Mathf.Abs(
                posicion.x -
                paletaDerecha.anchoredPosition.x
            ) < 30 &&
            Mathf.Abs(
                posicion.y -
                paletaDerecha.anchoredPosition.y
            ) < 70
        )
        {
            posicion.x =
                paletaDerecha.anchoredPosition.x
                - 30;

            direccionPelota.x =
                -Mathf.Abs(
                    direccionPelota.x
                );

            direccionPelota.y +=
                Random.Range(-0.25f, 0.25f);

            direccionPelota.Normalize();
        }

        // SALIÓ POR LA IZQUIERDA
        if (posicion.x < -520)
        {
            puntosDerecha++;

            ActualizarMarcador();

            if (puntosDerecha >= puntosParaGanar)
            {
                FinalizarPong(
                    jugadorRetado
                );

                return;
            }

            ReiniciarPelota(
                -1
            );

            return;
        }

        // SALIÓ POR LA DERECHA
        if (posicion.x > 520)
        {
            puntosIzquierda++;

            ActualizarMarcador();

            if (puntosIzquierda >= puntosParaGanar)
            {
                FinalizarPong(
                    jugadorRetador
                );

                return;
            }

            ReiniciarPelota(
                1
            );

            return;
        }

        pelota.anchoredPosition =
            posicion;
    }

    private void ReiniciarPelota(
        int direccionInicial
    )
    {
        pelota.anchoredPosition =
            Vector2.zero;

        direccionPelota =
            new Vector2(
                direccionInicial,
                Random.Range(
                    -0.6f,
                    0.6f
                )
            ).normalized;
    }

    // =========================================================
    // MARCADOR
    // =========================================================

    private void ActualizarMarcador()
    {
        textoMarcador.text =
            puntosIzquierda +
            "     -     " +
            puntosDerecha;
    }

    // =========================================================
    // FINALIZAR
    // =========================================================

    private void FinalizarPong(
        Jugador ganador
    )
    {
        jugando = false;

        panelPong.SetActive(false);
        panelResultado.SetActive(true);

        if (ganador == jugadorRetador)
        {
            ganador.SumarRespuestaPorMinijuego();

            textoResultado.text =
                "¡GANASTE!\n\n" +
                ganador.Nombre +
                "\n\n+1 RESPUESTA";
        }
        else
        {
            textoResultado.text =
                "¡" +
                ganador.Nombre +
                " GANÓ!\n\n" +
                ganador.Nombre +
                "\n\n+1 RESPUESTA";
            
            ganador.SumarRespuestaPorMinijuego();
        }

        if (UIJuego.Instancia != null)
        {
            UIJuego.Instancia.ActualizarInterfaz();
        }
    }

    // =========================================================
    // BOTÓN CONTINUAR
    // =========================================================

    public void ContinuarDespuesDelMinijuego()
    {
        panelPrincipal.SetActive(false);

        if (resultadoRedPong)
        {
            resultadoRedPong = false;
            participantesPongRed = null;
            return;
        }

        jugadorRetador = null;
        jugadorRetado = null;

        if (GameManager.Instancia != null)
        {
            GameManager.Instancia.ReanudarTurno();
        }
    }

    // =========================================================
    // CREAR CANVAS
    // =========================================================

    private void CrearCanvas()
    {
        GameObject canvasObjeto =
            new GameObject(
                "Canvas_MiniJuego"
            );

        canvas =
            canvasObjeto.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 100;

        canvasObjeto.AddComponent<
            CanvasScaler
        >();

        canvasObjeto.AddComponent<
            GraphicRaycaster
        >();

        DontDestroyOnLoad(
            canvasObjeto
        );
    }

    // =========================================================
    // CREAR INTERFAZ
    // =========================================================

    private void CrearInterfaz()
    {
        panelPrincipal =
            CrearPanel(
                canvas.transform,
                "PanelMiniJuego"
            );

        RectTransform panelRect =
            panelPrincipal.GetComponent<
                RectTransform
            >();

        panelRect.anchorMin =
            Vector2.zero;

        panelRect.anchorMax =
            Vector2.one;

        panelRect.offsetMin =
            Vector2.zero;

        panelRect.offsetMax =
            Vector2.zero;

        // -------------------------------------------------
        // SELECCIÓN
        // -------------------------------------------------

        panelSeleccion =
            CrearPanel(
                panelPrincipal.transform,
                "PanelSeleccion"
            );

        RectTransform seleccionRect =
            panelSeleccion.GetComponent<
                RectTransform
            >();

        seleccionRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        seleccionRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        seleccionRect.sizeDelta =
            new Vector2(
                650,
                600
            );

        seleccionRect.anchoredPosition =
            Vector2.zero;

        tituloSeleccion =
            CrearTexto(
                panelSeleccion.transform,
                "Titulo",
                "",
                38
            );

        RectTransform tituloRect =
            tituloSeleccion.rectTransform;

        tituloRect.anchoredPosition =
            new Vector2(
                0,
                180
            );

        tituloRect.sizeDelta =
            new Vector2(
                600,
                150
            );

        // -------------------------------------------------
        // PONG
        // -------------------------------------------------

        panelPong =
            CrearPanel(
                panelPrincipal.transform,
                "PanelPong"
            );

        RectTransform pongRect =
            panelPong.GetComponent<
                RectTransform
            >();

        pongRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        pongRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        pongRect.sizeDelta =
            new Vector2(
                1100,
                700
            );

        pongRect.anchoredPosition =
            Vector2.zero;

        // Cancha
        GameObject canchaObjeto =
            CrearPanel(
                panelPong.transform,
                "Cancha"
            );

        cancha =
            canchaObjeto.GetComponent<
                RectTransform
            >();

        cancha.sizeDelta =
            new Vector2(
                1000,
                550
            );

        cancha.anchoredPosition =
            new Vector2(
                0,
                -20
            );

        // Jugadores
        textoJugadorIzquierda =
            CrearTexto(
                panelPong.transform,
                "JugadorIzquierda",
                "",
                25
            );

        textoJugadorIzquierda.rectTransform
            .anchoredPosition =
            new Vector2(
                -300,
                300
            );

        textoJugadorIzquierda.rectTransform
            .sizeDelta =
            new Vector2(
                300,
                50
            );

        textoJugadorDerecha =
            CrearTexto(
                panelPong.transform,
                "JugadorDerecha",
                "",
                25
            );

        textoJugadorDerecha.rectTransform
            .anchoredPosition =
            new Vector2(
                300,
                300
            );

        textoJugadorDerecha.rectTransform
            .sizeDelta =
            new Vector2(
                300,
                50
            );

        // Marcador
        textoMarcador =
            CrearTexto(
                panelPong.transform,
                "Marcador",
                "0     -     0",
                45
            );

        textoMarcador.rectTransform
            .anchoredPosition =
            new Vector2(
                0,
                290
            );

        textoMarcador.rectTransform
            .sizeDelta =
            new Vector2(
                300,
                70
            );

        // Paletas
        paletaIzquierda =
            CrearRectangulo(
                cancha.transform,
                "PaletaIzquierda",
                new Vector2(
                    25,
                    120
                )
            );

        paletaDerecha =
            CrearRectangulo(
                cancha.transform,
                "PaletaDerecha",
                new Vector2(
                    25,
                    120
                )
            );

        paletaIzquierda.anchoredPosition =
            new Vector2(
                -430,
                0
            );

        paletaDerecha.anchoredPosition =
            new Vector2(
                430,
                0
            );

        // Pelota
        pelota =
            CrearRectangulo(
                cancha.transform,
                "Pelota",
                new Vector2(
                    25,
                    25
                )
            );

        imagenPelota =
            pelota.GetComponent<Image>();

        pelota.anchoredPosition =
            Vector2.zero;

        // -------------------------------------------------
        // RESULTADO
        // -------------------------------------------------

        panelResultado =
            CrearPanel(
                panelPrincipal.transform,
                "PanelResultado"
            );

        RectTransform resultadoRect =
            panelResultado.GetComponent<
                RectTransform
            >();

        resultadoRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        resultadoRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        resultadoRect.sizeDelta =
            new Vector2(
                650,
                500
            );

        resultadoRect.anchoredPosition =
            Vector2.zero;

        textoResultado =
            CrearTexto(
                panelResultado.transform,
                "Resultado",
                "",
                42
            );

        textoResultado.rectTransform
            .anchoredPosition =
            new Vector2(
                0,
                60
            );

        textoResultado.rectTransform
            .sizeDelta =
            new Vector2(
                600,
                250
            );

        GameObject botonContinuar =
            CrearBoton(
                panelResultado.transform,
                "Continuar",
                "CONTINUAR"
            );

        botonContinuar
            .GetComponent<RectTransform>()
            .anchoredPosition =
            new Vector2(
                0,
                -140
            );

        botonContinuar
            .GetComponent<RectTransform>()
            .sizeDelta =
            new Vector2(
                350,
                75
            );

        botonContinuar
            .GetComponent<Button>()
            .onClick
            .AddListener(
                ContinuarDespuesDelMinijuego
            );
    }

    // =========================================================
    // CREADORES DE UI
    // =========================================================

    private GameObject CrearPanel(
        Transform padre,
        string nombre
    )
    {
        GameObject objeto =
            new GameObject(
                nombre
            );

        objeto.transform.SetParent(
            padre,
            false
        );

        Image imagen =
            objeto.AddComponent<Image>();

        imagen.color =
            new Color(
                0.04f,
                0.04f,
                0.08f,
                0.97f
            );

        return objeto;
    }

    private TextMeshProUGUI CrearTexto(
        Transform padre,
        string nombre,
        string texto,
        float tamaño
    )
    {
        GameObject objeto =
            new GameObject(
                nombre
            );

        objeto.transform.SetParent(
            padre,
            false
        );

        TextMeshProUGUI textoTMP =
            objeto.AddComponent<
                TextMeshProUGUI
            >();

        textoTMP.text = texto;
        textoTMP.fontSize = tamaño;
        textoTMP.alignment =
            TextAlignmentOptions.Center;

        textoTMP.color =
            Color.white;

        return textoTMP;
    }

    private GameObject CrearBoton(
        Transform padre,
        string nombre,
        string texto
    )
    {
        GameObject objeto =
            new GameObject(
                nombre
            );

        objeto.transform.SetParent(
            padre,
            false
        );

        Image imagen =
            objeto.AddComponent<Image>();

        imagen.color =
            new Color(
                0.25f,
                0.25f,
                0.7f,
                1f
            );

        Button boton =
            objeto.AddComponent<Button>();

        GameObject textoObjeto =
            new GameObject(
                "Texto"
            );

        textoObjeto.transform.SetParent(
            objeto.transform,
            false
        );

        TextMeshProUGUI textoTMP =
            textoObjeto.AddComponent<
                TextMeshProUGUI
            >();

        textoTMP.text = texto;
        textoTMP.fontSize = 28;
        textoTMP.alignment =
            TextAlignmentOptions.Center;

        textoTMP.color =
            Color.white;

        RectTransform textoRect =
            textoTMP.rectTransform;

        textoRect.anchorMin =
            Vector2.zero;

        textoRect.anchorMax =
            Vector2.one;

        textoRect.offsetMin =
            Vector2.zero;

        textoRect.offsetMax =
            Vector2.zero;

        return objeto;
    }

    private RectTransform CrearRectangulo(
        Transform padre,
        string nombre,
        Vector2 tamaño
    )
    {
        GameObject objeto =
            new GameObject(
                nombre
            );

        objeto.transform.SetParent(
            padre,
            false
        );

        Image imagen =
            objeto.AddComponent<Image>();

        imagen.color =
            Color.white;

        RectTransform rect =
            objeto.GetComponent<
                RectTransform
            >();

        rect.sizeDelta =
            tamaño;

        return rect;
    }

    // =========================================================
    // OCULTAR TODO
    // =========================================================

    private void OcultarTodo()
    {
        if (panelPrincipal != null)
        {
            panelPrincipal.SetActive(false);
        }
    }
}