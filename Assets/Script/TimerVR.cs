using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimerVR : MonoBehaviour
{
    [Header("Configuración del Temporizador")]
    [Tooltip("Tiempo total en segundos para la cuenta regresiva de esta escena")]
    public float tiempoTotalSegundos = 60.0f;

    [Tooltip("Mostrar el temporizador visualmente en el casco (Desactivado para ocultar en todas las escenas)")]
    public bool mostrarHUD = false;

    [Header("Posición frente al visor VR")]
    [Tooltip("Distancia hacia adelante desde la cámara")]
    public float distanciaFrente = 1.4f;

    [Tooltip("Altura vertical sobre la línea de visión")]
    public float alturaOffset = 0.42f;

    [Header("Referencias Opcionales (Se autogeneran si están vacías)")]
    public Transform camaraVR;
    public TextMeshProUGUI textoTiempoTMP;
    public Text textoTiempoUI;

    private float tiempoRestante;
    private GameObject hudCanvasObj;
    private bool tiempoTerminado = false;

    public float TiempoRestante => tiempoRestante;
    public bool EsTiempoTerminado => tiempoTerminado;

    void Awake()
    {
        tiempoRestante = tiempoTotalSegundos;

        if (camaraVR == null && Camera.main != null)
        {
            camaraVR = Camera.main.transform;
        }

        if (mostrarHUD)
        {
            if (textoTiempoTMP == null && textoTiempoUI == null)
            {
                ConstruirHUDAutomatico();
            }
        }
        else
        {
            OcultarHUD();
        }
    }

    void Update()
    {
        if (camaraVR == null && Camera.main != null)
        {
            camaraVR = Camera.main.transform;
        }

        // Cuenta regresiva interna
        if (tiempoRestante > 0f)
        {
            tiempoRestante -= Time.deltaTime;
            if (tiempoRestante <= 0f)
            {
                tiempoRestante = 0f;
                tiempoTerminado = true;
            }
        }

        if (mostrarHUD)
        {
            ActualizarTexto();
            ActualizarPosicionHUD();
        }
    }

    public void OcultarHUD()
    {
        mostrarHUD = false;
        if (hudCanvasObj != null)
        {
            hudCanvasObj.SetActive(false);
        }
        if (textoTiempoTMP != null)
        {
            textoTiempoTMP.gameObject.SetActive(false);
        }
        if (textoTiempoUI != null)
        {
            textoTiempoUI.gameObject.SetActive(false);
        }
        GameObject existingHUD = GameObject.Find("HUD_Timer_VR");
        if (existingHUD != null)
        {
            existingHUD.SetActive(false);
        }
    }

    private void ActualizarTexto()
    {
        int minutos = Mathf.FloorToInt(tiempoRestante / 60f);
        int segundos = Mathf.FloorToInt(tiempoRestante % 60f);
        string textoFormateado = string.Format("{0:00}:{1:00}", minutos, segundos);

        if (textoTiempoTMP != null)
        {
            textoTiempoTMP.text = textoFormateado;
        }
        else if (textoTiempoUI != null)
        {
            textoTiempoUI.text = textoFormateado;
        }
    }

    private void ActualizarPosicionHUD()
    {
        if (hudCanvasObj == null || camaraVR == null) return;

        Vector3 posicionDeseada = camaraVR.position + (camaraVR.forward * distanciaFrente) + (camaraVR.up * alturaOffset);
        hudCanvasObj.transform.position = Vector3.Lerp(hudCanvasObj.transform.position, posicionDeseada, Time.deltaTime * 10f);
        hudCanvasObj.transform.rotation = Quaternion.Slerp(hudCanvasObj.transform.rotation, Quaternion.LookRotation(hudCanvasObj.transform.position - camaraVR.position), Time.deltaTime * 10f);
    }

    private void ConstruirHUDAutomatico()
    {
        hudCanvasObj = new GameObject("HUD_Timer_VR");
        Canvas canvas = hudCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        hudCanvasObj.AddComponent<CanvasScaler>();

        RectTransform rectCanvas = hudCanvasObj.GetComponent<RectTransform>();
        rectCanvas.sizeDelta = new Vector2(220, 70);
        rectCanvas.localScale = Vector3.one * 0.0018f;

        // Panel de fondo translúcido
        GameObject panelFondo = new GameObject("Fondo");
        panelFondo.transform.SetParent(hudCanvasObj.transform, false);
        Image imgFondo = panelFondo.AddComponent<Image>();
        imgFondo.color = new Color(0f, 0f, 0f, 0.45f);
        RectTransform rectFondo = panelFondo.GetComponent<RectTransform>();
        rectFondo.anchorMin = Vector2.zero;
        rectFondo.anchorMax = Vector2.one;
        rectFondo.sizeDelta = Vector2.zero;

        // Texto del cronómetro
        GameObject textoObj = new GameObject("TextoTimer");
        textoObj.transform.SetParent(hudCanvasObj.transform, false);
        textoTiempoTMP = textoObj.AddComponent<TextMeshProUGUI>();
        textoTiempoTMP.alignment = TextAlignmentOptions.Center;
        textoTiempoTMP.fontSize = 32;
        textoTiempoTMP.color = new Color(0.95f, 0.95f, 0.95f, 0.9f);
        textoTiempoTMP.text = "00:00";
        RectTransform rectTexto = textoObj.GetComponent<RectTransform>();
        rectTexto.anchorMin = Vector2.zero;
        rectTexto.anchorMax = Vector2.one;
        rectTexto.sizeDelta = Vector2.zero;

        if (camaraVR != null)
        {
            hudCanvasObj.transform.position = camaraVR.position + (camaraVR.forward * distanciaFrente) + (camaraVR.up * alturaOffset);
            hudCanvasObj.transform.rotation = camaraVR.rotation;
        }
    }
}