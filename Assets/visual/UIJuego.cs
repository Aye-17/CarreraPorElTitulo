using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIJuego : MonoBehaviour
{
    public static UIJuego Instancia;

    [Header("Turno actual")]
    public TextMeshProUGUI textoTurno;


    [Header("Jugadores")]
    public TextMeshProUGUI textoRojo;
    public TextMeshProUGUI textoAzul;
    public TextMeshProUGUI textoVerde;
    public TextMeshProUGUI textoAmarillo;


    [Header("Resultado de pregunta")]
    public GameObject cuadroResultado;
    public TextMeshProUGUI textoResultado;

    [SerializeField] private Texture2D cartelIrABiblioteca;

    private GameObject panelCartelBiblioteca;
    private RawImage imagenCartelBiblioteca;
    private Action alAceptarCartelBiblioteca;

    private void Awake()
    {
        Instancia = this;

        if (cuadroResultado != null){
            cuadroResultado.SetActive(false);
        }

        CrearCartelBiblioteca();
    }

    private void CrearCartelBiblioteca()
    {
        if (cartelIrABiblioteca == null)
        {
#if UNITY_EDITOR
            cartelIrABiblioteca =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/visual/ir a la biblioteca.png"
                );
#endif

            if (cartelIrABiblioteca == null)
            {
                Debug.LogError(
                    "[UIJuego] No se encontró la imagen " +
                    "'Assets/visual/ir a la biblioteca.png'."
                );
                return;
            }
        }

        GameObject canvasObject = GameObject.Find("Canvas");
        Canvas canvas = canvasObject != null
            ? canvasObject.GetComponent<Canvas>()
            : null;

        if (canvas == null)
        {
            Debug.LogError(
                "[UIJuego] No se encontró un Canvas para mostrar el cartel."
            );
            return;
        }

        GameObject panel = new GameObject(
            "PanelCartelBiblioteca",
            typeof(RectTransform),
            typeof(Image)
        );
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect =
            panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image fondo = panel.GetComponent<Image>();
        fondo.color = new Color(0f, 0f, 0f, 0.65f);

        GameObject cartel = new GameObject(
            "CartelIrABiblioteca",
            typeof(RectTransform),
            typeof(RawImage)
        );
        cartel.transform.SetParent(panel.transform, false);

        RectTransform rectTransform = cartel.GetComponent<RectTransform>();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float anchoCanvas = canvasRect.rect.width;
        float altoCanvas = canvasRect.rect.height;

        if (anchoCanvas <= 0f || altoCanvas <= 0f)
        {
            anchoCanvas = Screen.width;
            altoCanvas = Screen.height;
        }

        float aspecto = (float)cartelIrABiblioteca.width /
            cartelIrABiblioteca.height;
        float ancho = anchoCanvas * 0.8f;
        float alto = ancho / aspecto;

        if (alto > altoCanvas * 0.72f)
        {
            alto = altoCanvas * 0.72f;
            ancho = alto * aspecto;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(ancho, alto);

        imagenCartelBiblioteca = cartel.GetComponent<RawImage>();
        imagenCartelBiblioteca.texture = cartelIrABiblioteca;
        imagenCartelBiblioteca.raycastTarget = false;

        CrearBotonAceptarBiblioteca(
            panel.transform,
            alto,
            anchoCanvas
        );

        panelCartelBiblioteca = panel;
        panel.SetActive(false);
    }

    private void CrearBotonAceptarBiblioteca(
        Transform padre,
        float altoCartel,
        float anchoCanvas)
    {
        GameObject objetoBoton = new GameObject(
            "BotonAceptarBiblioteca",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        objetoBoton.transform.SetParent(padre, false);

        RectTransform rectBoton =
            objetoBoton.GetComponent<RectTransform>();
        float altoBoton = 64f;
        float anchoBoton = Mathf.Min(anchoCanvas * 0.5f, 320f);
        rectBoton.anchorMin = new Vector2(0.5f, 0.5f);
        rectBoton.anchorMax = new Vector2(0.5f, 0.5f);
        rectBoton.anchoredPosition = new Vector2(
            0f,
            -(altoCartel * 0.5f) - 18f - (altoBoton * 0.5f)
        );
        rectBoton.sizeDelta = new Vector2(anchoBoton, altoBoton);

        Image fondoBoton = objetoBoton.GetComponent<Image>();
        fondoBoton.color = new Color(0.12f, 0.38f, 0.22f, 1f);

        Button boton = objetoBoton.GetComponent<Button>();
        boton.onClick.AddListener(AceptarCartelBiblioteca);

        GameObject objetoTexto = new GameObject(
            "TextoAceptar",
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );
        objetoTexto.transform.SetParent(objetoBoton.transform, false);

        RectTransform rectTexto =
            objetoTexto.GetComponent<RectTransform>();
        rectTexto.anchorMin = Vector2.zero;
        rectTexto.anchorMax = Vector2.one;
        rectTexto.offsetMin = new Vector2(8f, 4f);
        rectTexto.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI textoAceptar =
            objetoTexto.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI referenciaFuente =
            textoResultado != null ? textoResultado : textoTurno;
        TMP_FontAsset fuente = referenciaFuente != null
            ? referenciaFuente.font
            : TMP_Settings.defaultFontAsset;

        if (fuente != null)
        {
            textoAceptar.font = fuente;
        }

        if (referenciaFuente != null)
        {
            textoAceptar.fontStyle = referenciaFuente.fontStyle;
            textoAceptar.fontSize = referenciaFuente.fontSize;
        }
        else
        {
            textoAceptar.fontSize = 28f;
        }

        textoAceptar.enableAutoSizing = true;
        textoAceptar.fontSizeMin = 18f;
        textoAceptar.fontSizeMax = 36f;
        textoAceptar.alignment = TextAlignmentOptions.Center;
        textoAceptar.color = Color.white;
        textoAceptar.text = "Aceptar";
    }

    public void MostrarCartelIrABiblioteca(
        Action alAceptar = null)
    {
        if (panelCartelBiblioteca == null)
        {
            Debug.LogError(
                "[UIJuego] No se pudo crear el cartel de biblioteca."
            );
            alAceptar?.Invoke();
            return;
        }

        alAceptarCartelBiblioteca = alAceptar;
        panelCartelBiblioteca.transform.SetAsLastSibling();
        panelCartelBiblioteca.SetActive(true);
    }

    private void AceptarCartelBiblioteca()
    {
        panelCartelBiblioteca.SetActive(false);

        Action alAceptar = alAceptarCartelBiblioteca;
        alAceptarCartelBiblioteca = null;
        alAceptar?.Invoke();
    }

    public void MostrarIntentoBiblioteca(
        Jugador jugador,
        int resultadoDado)
    {
        if (jugador == null || textoResultado == null)
            return;

        textoResultado.text =
            $"{ObtenerNombreCorto(jugador.Nombre)} sacó {resultadoDado} " +
            $"en la biblioteca ({jugador.IntentosDeSalirBiblioteca}/3).";
    }

    public void ActualizarInterfaz(){
        if (GameManager.Instancia == null)
            return;


        Jugador jugadorActual = GameManager.Instancia.ObtenerJugadorActual();


        if (jugadorActual != null){
            ActualizarTurno(jugadorActual);
        }

        ActualizarJugador(GameManager.Instancia.ObtenerJugador(0),textoRojo);

        ActualizarJugador(GameManager.Instancia.ObtenerJugador(1),textoAzul);

        ActualizarJugador(GameManager.Instancia.ObtenerJugador(2),textoVerde);


        ActualizarJugador(GameManager.Instancia.ObtenerJugador(3),textoAmarillo);
    }


    private void ActualizarTurno(Jugador jugador){
        if (textoTurno == null)
            return;

        string nombre = ObtenerNombreCorto(jugador.Nombre);

        string simbolo = ObtenerSimbolo(jugador.Id);

        textoTurno.text = $"TURNO DE: {simbolo} {nombre}";
    }

    private void ActualizarJugador(Jugador jugador,TextMeshProUGUI texto){

        if (jugador == null || texto == null)
            return;

        string nombre = ObtenerNombreCorto(jugador.Nombre);

        string simbolo = ObtenerSimbolo(jugador.Id);

        int vueltasTotales = GameManager.Instancia.ObtenerVueltasTotales();

        texto.text =
            $"{simbolo} {nombre}\n" +
            $"{jugador.RespuestasCorrectas} " +
            $"{TextoRespuestas(jugador.RespuestasCorrectas)}\n" +
            $"Vuelta {jugador.RondaActual}/" +
            $"{vueltasTotales}";
    }

    public void MostrarResultado(Jugador jugador,bool respuestaCorrecta){
        if (jugador == null)
            return;

        string nombre = ObtenerNombreCorto(jugador.Nombre);

        if (cuadroResultado != null){
            cuadroResultado.SetActive(true);
        }

        if (textoResultado == null)
            return;

        if (respuestaCorrecta){
            textoResultado.text =
                $"{ObtenerSimbolo(jugador.Id)} " +
                $"{nombre} respondió correctamente";
        }
        else{
            textoResultado.text =
                $"{ObtenerSimbolo(jugador.Id)} " +
                $"{nombre} respondió incorrectamente";
        }

        ActualizarInterfaz();
    }

    public void MostrarMovimientoEspecial(Jugador jugador,int cantidadCasilleros){

        if (jugador == null)
            return;
        
        string nombre = ObtenerNombreCorto(jugador.Nombre);

        if (cuadroResultado != null){
            cuadroResultado.SetActive(true);
        }

        if (textoResultado == null)
            return;

        if (cantidadCasilleros > 0){
            textoResultado.text =
                $"{ObtenerSimbolo(jugador.Id)} " +
                $"{nombre} avanza {cantidadCasilleros} " +
                TextoCasilleros(cantidadCasilleros);
        }
        else if (cantidadCasilleros < 0)
        {
            int retroceso = Mathf.Abs(cantidadCasilleros);
            
            textoResultado.text =
                $"{ObtenerSimbolo(jugador.Id)} " +
                $"{nombre} retrocede {retroceso} " +
                TextoCasilleros(retroceso) + ".";
        }

        ActualizarInterfaz();
    }

    public void MostrarPenalizacion(Jugador jugador){
        if (jugador == null)
            return;

        string nombre = ObtenerNombreCorto(jugador.Nombre);

        if (cuadroResultado != null){
            cuadroResultado.SetActive(true);
        }

        if (textoResultado == null)
            return;

        textoResultado.text =
            $"{ObtenerSimbolo(jugador.Id)} " +
            $"{nombre} está penalizado y pierde un turno";

        ActualizarInterfaz();
    }

    private string ObtenerNombreCorto(
        string nombreCompleto
    )
    {
        if (
            nombreCompleto.StartsWith(
                "Jugador "
            )
        )
        {
            return nombreCompleto
                .Substring(8)
                .ToUpper();
        }


        return nombreCompleto.ToUpper();
    }


    private string ObtenerSimbolo(
        int id
    )
    {
        switch (id)
        {
            case 1:
                return "<color=#E53935>●</color>";

            case 2:
                return "<color=#1E88E5>●</color>";

            case 3:
                return "<color=#43A047>●</color>";

            case 4:
                return "<color=#FDD835>●</color>";

            default:
                return "●";
        }
    }


    private string TextoRespuestas(
        int cantidad
    )
    {
        if (cantidad == 1)
            return "respuesta";

        return "respuestas";
    }


    private string TextoCasilleros(
        int cantidad
    )
    {
        if (cantidad == 1)
            return "casillero";

            return "casilleros";
    }
}