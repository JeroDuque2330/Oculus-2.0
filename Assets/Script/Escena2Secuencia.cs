using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class Escena2Secuencia : MonoBehaviour
{
    private static Escena2Secuencia instance;

    [Header("Referencias Principales")]
    [Tooltip("La cámara del casco VR (Main Camera)")]
    public Transform jugadorVR;

    [Tooltip("El objeto XR Origin o Camera Offset para controlar la posición en VR")]
    public Transform xrOriginTransform;

    [Tooltip("El objeto Global Volume con el Post-Processing")]
    public Volume volumeAmbiente;

    [Header("Duración de Escena (60 segundos exactos)")]
    [Tooltip("Tiempo total en segundos para la Escena 2")]
    public float tiempoTotalEscena = 60.0f;

    [Tooltip("Duración de la fase final donde el charco consume al jugador (20 segundos: del segundo 40 al 60)")]
    public float duracionFaseCharco = 20.0f;

    [Header("Charco y Manos (Fase Final: 40s - 60s)")]
    [Tooltip("GameObject o Prefab del modelo charco.fbx")]
    public GameObject charcoObjeto;

    [Tooltip("Posición en el suelo donde emergerán las manos. Si está vacío, se coloca automáticamente en el suelo bajo el jugador")]
    public Transform puntoSpawnCharco;

    [Tooltip("Nombre del parámetro Trigger o Estado en el Animator del Charco (Dejar vacío si se reproduce por defecto)")]
    public string parametroTriggerAnimator = "";

    [Tooltip("Profundidad en metros a la que el jugador se hundirá en el suelo")]
    public float profundidadHundimiento = 3.0f;

    [Tooltip("Ángulo de inclinación forzada hacia abajo para mirar el charco")]
    public float anguloMirarAbajo = 70.0f;

    [Tooltip("Activar temblor angustioso de cámara")]
    public bool aplicarTemblorCamara = true;

    [Header("Audio")]
    [Tooltip("Música y estática ambiental que abruma")]
    public AudioSource audioMusicaEstatica;

    [Tooltip("Sonido de manos emergiendo y atrapando al jugador")]
    public AudioSource audioManosCharco;

    [Header("Efectos de Niebla / Partículas")]
    [Tooltip("Partículas o GameObject de la niebla espesa")]
    public GameObject nieblaEspesa;

    [Header("PRUEBAS / DEBUGER (Para probar en el Editor)")]
    [Tooltip("¡ACTIVAR PARA PROBAR DE INMEDIATO! Salta directamente a las manos, temblor y hundimiento")]
    public bool probarFaseCharcoDeInmediato = false;

    // Componentes internos y de Post-Processing
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private FilmGrain filmGrain;
    private Renderer[] renderersCharco;
    private Animator animatorCharco;
    private Image blackoutImage;

    void Awake()
    {
        // Evitar ejecuciones duplicadas si el script está en más de un objeto en la escena
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        // FORZAR SIEMPRE 60 SEGUNDOS Y 20 DE CHARCO
        tiempoTotalEscena = 60.0f;
        duracionFaseCharco = 20.0f;
    }

    void Start()
    {
        if (jugadorVR == null && Camera.main != null)
        {
            jugadorVR = Camera.main.transform;
        }

        if (xrOriginTransform == null && jugadorVR != null)
        {
            if (jugadorVR.parent != null) xrOriginTransform = jugadorVR.parent;
            else xrOriginTransform = jugadorVR;
        }

        // Buscar automáticamente el charco si no fue asignado
        if (charcoObjeto == null)
        {
            GameObject foundCharco = GameObject.Find("charco");
            if (foundCharco == null) foundCharco = GameObject.Find("charco.fbx");
            if (foundCharco == null)
            {
                Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (var r in allRenderers)
                {
                    if (r.gameObject.name.ToLower().Contains("charco"))
                    {
                        foundCharco = r.gameObject;
                        break;
                    }
                }
            }
            charcoObjeto = foundCharco ?? this.gameObject;
        }

        // Obtener renderers y animator para ocultar el charco al inicio
        renderersCharco = charcoObjeto.GetComponentsInChildren<Renderer>(true);
        animatorCharco = charcoObjeto.GetComponentInChildren<Animator>(true);

        EstablecerVisibilidadCharco(false);

        // Crear overlay negro 100% opaco para garantizar oscuridad total
        CrearBlackoutOverlay();

        // Configurar TimerVR en 60 segundos y oculto
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
        }
        timer.tiempoTotalSegundos = 60.0f;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);
            volumeAmbiente.profile.TryGet(out filmGrain);

            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.white;
        }

        StartCoroutine(CronologiaEscena2());
    }

    private void CrearBlackoutOverlay()
    {
        if (jugadorVR == null) return;

        GameObject overlayObj = new GameObject("VR_Blackout_Overlay");
        overlayObj.transform.SetParent(jugadorVR, false);
        overlayObj.transform.localPosition = new Vector3(0, 0, 0.25f);
        overlayObj.transform.localRotation = Quaternion.identity;

        Canvas canvas = overlayObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 9999;

        RectTransform rect = overlayObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(3f, 3f);

        GameObject imgObj = new GameObject("BlackImage");
        imgObj.transform.SetParent(overlayObj.transform, false);
        blackoutImage = imgObj.AddComponent<Image>();
        blackoutImage.color = new Color(0f, 0f, 0f, 1f); // Comienza en negro para el fade-in

        RectTransform imgRect = imgObj.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.sizeDelta = Vector2.zero;
    }

    private void SetBlackoutAlpha(float alpha)
    {
        if (blackoutImage != null)
        {
            blackoutImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }
    }

    private void EstablecerVisibilidadCharco(bool visible)
    {
        if (renderersCharco != null)
        {
            for (int i = 0; i < renderersCharco.Length; i++)
            {
                if (renderersCharco[i] != null) renderersCharco[i].enabled = visible;
            }
        }
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 2 (Total: 60 seg / 1:00 min)
    // 
    // 1. (0s - 10s): Levantarse (Fade-in suave desde negro y elevación de cámara).
    // 2. (10s - 40s): Exploración, estática y música ambiental creciente en la niebla.
    // 3. (40s - 60s): ¡CLÍMAX! Surgen las manos, la cámara mira hacia abajo,
    //                 empieza a temblar, es arrastrado hacia abajo y la pantalla
    //                 se va tornando negra del todo.
    // 4. (60s): Oscuridad 100% total y fin de la experiencia.
    // =========================================================================
    IEnumerator CronologiaEscena2()
    {
        // MODO PRUEBA: Ir directamente a las manos, temblor y hundimiento
        if (probarFaseCharcoDeInmediato)
        {
            Debug.Log("🧪 MODO PRUEBA ACTIVADO: Iniciando fase de manos y hundimiento de inmediato...");
            yield return StartCoroutine(EjecutarFaseCharcoYConsumo());
            yield break;
        }

        // ---------------------------------------------------------------------
        // FASE 1 (0s - 10s / 10 seg): LEVANTARSE (Fade-in y posición de pie)
        // ---------------------------------------------------------------------
        SetBlackoutAlpha(1.0f);

        Vector3 posicionOriginalXR = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;
        if (xrOriginTransform != null)
        {
            xrOriginTransform.position = posicionOriginalXR - new Vector3(0, 0.7f, 0);
        }

        float tFase1 = 0f;
        float duracionFase1 = 10.0f;
        while (tFase1 < duracionFase1)
        {
            tFase1 += Time.deltaTime;
            float factor = Mathf.Clamp01(tFase1 / duracionFase1);

            // Fade-in a visión clara
            SetBlackoutAlpha(1.0f - factor);

            if (xrOriginTransform != null)
            {
                xrOriginTransform.position = Vector3.Lerp(posicionOriginalXR - new Vector3(0, 0.7f, 0), posicionOriginalXR, factor);
            }

            yield return null;
        }

        SetBlackoutAlpha(0.0f);

        // ---------------------------------------------------------------------
        // FASE 2 (10s - 40s / 30 seg): EXPLORACIÓN, ESTÁTICA Y NIEBLA
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null && !audioMusicaEstatica.isPlaying) audioMusicaEstatica.Play();
        if (nieblaEspesa != null) nieblaEspesa.SetActive(true);

        float duracionFase2 = 30.0f; // 30 segundos (del segundo 10 al 40)
        float tFase2 = 0f;

        while (tFase2 < duracionFase2)
        {
            tFase2 += Time.deltaTime;
            float factorFase2 = Mathf.Clamp01(tFase2 / duracionFase2);

            if (audioMusicaEstatica != null)
            {
                audioMusicaEstatica.volume = Mathf.Lerp(0.15f, 0.85f, factorFase2);
            }

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(0f, 0.35f, factorFase2);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 3 (40s - 60s / 20 seg): LAS MANOS SURGEN, CÁMARA MIRA ABAJO,
        // TIEMBLA, ES ARRASTRADO HACIA ABAJO Y PANTALLA SE TORNA NEGRA DEL TODO
        // ---------------------------------------------------------------------
        yield return StartCoroutine(EjecutarFaseCharcoYConsumo());
    }

    IEnumerator EjecutarFaseCharcoYConsumo()
    {
        // 1. Posicionar el charco exactamente en el suelo visible bajo los pies del jugador
        if (charcoObjeto != null)
        {
            Vector3 posSuelo = (xrOriginTransform != null) ? xrOriginTransform.position : jugadorVR.position;
            
            // Detectar el suelo real con Raycast para no quedar bajo tierra
            RaycastHit hit;
            if (Physics.Raycast(jugadorVR.position + Vector3.up * 1f, Vector3.down, out hit, 50f))
            {
                posSuelo = hit.point;
            }
            else if (xrOriginTransform != null)
            {
                posSuelo = new Vector3(jugadorVR.position.x, xrOriginTransform.position.y, jugadorVR.position.z);
            }

            Vector3 dirMirada = Vector3.ProjectOnPlane(jugadorVR.forward, Vector3.up).normalized;
            if (dirMirada == Vector3.zero) dirMirada = Vector3.forward;

            if (puntoSpawnCharco != null)
            {
                charcoObjeto.transform.position = puntoSpawnCharco.position;
                charcoObjeto.transform.rotation = puntoSpawnCharco.rotation;
            }
            else
            {
                // Colocar el charco directamente frente a los pies en el suelo visible
                charcoObjeto.transform.position = posSuelo + (dirMirada * 0.55f) + new Vector3(0f, 0.05f, 0f);
                charcoObjeto.transform.rotation = Quaternion.LookRotation(dirMirada);
                charcoObjeto.transform.localScale = Vector3.one * 1.5f; // Escala visible
            }

            // Hacer visible el charco
            EstablecerVisibilidadCharco(true);

            // Iniciar animación de las manos emergiendo
            if (animatorCharco == null && charcoObjeto != null)
            {
                animatorCharco = charcoObjeto.GetComponentInChildren<Animator>(true);
            }

            if (animatorCharco != null)
            {
                animatorCharco.enabled = true;
                if (!string.IsNullOrEmpty(parametroTriggerAnimator))
                {
                    animatorCharco.SetTrigger(parametroTriggerAnimator);
                }
                else
                {
                    animatorCharco.Play(0, -1, 0f);
                }
            }
            else
            {
                Animation legacyAnim = charcoObjeto.GetComponentInChildren<Animation>(true);
                if (legacyAnim != null)
                {
                    legacyAnim.enabled = true;
                    legacyAnim.Play();
                }
            }
        }

        // Sonido de manos emergiendo y atrapando
        if (audioManosCharco != null) audioManosCharco.Play();

        // 2. Transición del clímax: Arrastre hacia abajo, cámara mira abajo, temblor y oscurecimiento a negro total
        float tFase3 = 0f;
        Vector3 posInicialArrastre = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;
        float rotInicialY = (xrOriginTransform != null) ? xrOriginTransform.localEulerAngles.y : 0f;

        while (tFase3 < duracionFaseCharco)
        {
            tFase3 += Time.deltaTime;
            float factor = Mathf.Clamp01(tFase3 / duracionFaseCharco);

            // Curva suave de arrastre hacia el fondo
            float progresoHundimiento = Mathf.SmoothStep(0f, 1f, factor);

            // Temblor angustioso que aumenta a medida que lo arrastran
            float intensidadTemblor = aplicarTemblorCamara ? Mathf.Lerp(0.02f, 0.09f, factor) : 0f;
            float temblorPosX = (Mathf.PerlinNoise(Time.time * 36f, 0f) - 0.5f) * intensidadTemblor;
            float temblorPosY = (Mathf.PerlinNoise(0f, Time.time * 36f) - 0.5f) * (intensidadTemblor * 0.5f);
            float temblorPosZ = (Mathf.PerlinNoise(Time.time * 36f, Time.time * 36f) - 0.5f) * intensidadTemblor;

            float temblorRotX = (Mathf.PerlinNoise(Time.time * 42f, 15f) - 0.5f) * (intensidadTemblor * 45f);
            float temblorRotZ = (Mathf.PerlinNoise(15f, Time.time * 42f) - 0.5f) * (intensidadTemblor * 45f);

            // A) ARRASTRAR HACIA ABAJO (Hundimiento en el suelo)
            if (xrOriginTransform != null)
            {
                Vector3 posDescenso = posInicialArrastre - new Vector3(0f, progresoHundimiento * profundidadHundimiento, 0f);
                xrOriginTransform.position = posDescenso + new Vector3(temblorPosX, temblorPosY, temblorPosZ);

                // B) LA CÁMARA MIRA HACIA ABAJO (Inclinación forzada hacia las manos y el suelo)
                float inclinacionX = Mathf.Lerp(0f, anguloMirarAbajo, Mathf.SmoothStep(0f, 1f, factor * 1.8f));
                xrOriginTransform.localRotation = Quaternion.Euler(inclinacionX + temblorRotX, rotInicialY, temblorRotZ);
            }

            // Para pruebas en Editor / no VR, inclinar también la cámara si es objeto directo
            if (xrOriginTransform == jugadorVR && jugadorVR != null)
            {
                float inclinacionX = Mathf.Lerp(0f, anguloMirarAbajo, Mathf.SmoothStep(0f, 1f, factor * 1.8f));
                jugadorVR.localRotation = Quaternion.Euler(inclinacionX + temblorRotX, jugadorVR.localEulerAngles.y, temblorRotZ);
            }

            // C) LA PANTALLA SE VA TORNANDO NEGRA DEL TODO (Overlay + PostProcessing)
            SetBlackoutAlpha(factor);

            if (vignette != null)
            {
                vignette.color.value = Color.black;
                vignette.intensity.value = Mathf.Lerp(0.2f, 1.0f, factor);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // CIERRE ABRUPTO: Oscuridad 100% total y silencio absoluto (Muerte)
        // ---------------------------------------------------------------------
        SetBlackoutAlpha(1.0f); // 100% NEGRO TOTAL GARANTIZADO

        if (audioMusicaEstatica != null) audioMusicaEstatica.Stop();
        if (audioManosCharco != null) audioManosCharco.Stop();

        Debug.Log("🏁 Fin Escena 2 (60s): Jugador totalmente consumido bajo el suelo en oscuridad absoluta.");
    }
}