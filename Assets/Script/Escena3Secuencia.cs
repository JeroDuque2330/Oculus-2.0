using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class Escena3Secuencia : MonoBehaviour
{
    [Header("Referencias Principales")]
    [Tooltip("La cámara del casco VR (Main Camera)")]
    public Transform jugadorVR;

    [Tooltip("El objeto XR Origin o Camera Offset para controlar la posición en VR")]
    public Transform xrOriginTransform;

    [Tooltip("El objeto Global Volume con el Post-Processing")]
    public Volume volumeAmbiente;

    [Header("Duración de Escena")]
    [Tooltip("Tiempo total en segundos para la escena 3 (Predeterminado: 150s / 2:30 min)")]
    public float tiempoTotalEscena = 150.0f;

    [Tooltip("Duración de la fase final donde el charco consume al jugador (en segundos)")]
    public float duracionFaseCharco = 20.0f;

    [Header("Charco y Manos (Fase Final)")]
    [Tooltip("GameObject o Prefab del modelo charco.fbx (si está vacío, se usará automáticamente este mismo objeto)")]
    public GameObject charcoObjeto;

    [Tooltip("Posición en el suelo donde emergerán las manos. Si está vacío, se coloca automáticamente bajo el jugador")]
    public Transform puntoSpawnCharco;

    [Tooltip("Nombre del parámetro Trigger o Estado en el Animator del Charco (Dejar en blanco si se reproduce automáticamente)")]
    public string parametroTriggerAnimator = "";

    [Tooltip("Profundidad en metros a la que el jugador se hundirá en el suelo")]
    public float profundidadHundimiento = 2.5f;

    [Tooltip("Inclinación/Temblor de la cámara al ser consumido por las manos")]
    public bool aplicarTemblorCamara = true;

    [Header("Audio")]
    [Tooltip("Música y estática ambiental que abruma lentamente")]
    public AudioSource audioMusicaEstatica;

    [Tooltip("Sonido de manos emergiendo y atrapando al jugador")]
    public AudioSource audioManosCharco;

    [Header("Efectos de Niebla / Partículas")]
    [Tooltip("Partículas o GameObject de la niebla espesa")]
    public GameObject nieblaEspesa;

    [Header("PRUEBAS / DEBUGER (Para probar en el Editor)")]
    [Tooltip("¡ACTIVAR PARA PROBAR DE INMEDIATO! Salta directamente a la animación de las manos y bajada de cámara al dar Play sin esperar 2 minutos")]
    public bool probarFaseCharcoDeInmediato = false;

    // Componentes de Post-Processing e internos
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private FilmGrain filmGrain;
    private Renderer[] renderersCharco;
    private Animator animatorCharco;

    void Start()
    {
        if (jugadorVR == null && Camera.main != null)
        {
            jugadorVR = Camera.main.transform;
        }

        // Si charcoObjeto no está asignado en el Inspector, usar este mismo GameObject
        if (charcoObjeto == null)
        {
            charcoObjeto = this.gameObject;
        }

        // Obtener renderers y animator para ocultar/mostrar sin desactivar este script
        renderersCharco = charcoObjeto.GetComponentsInChildren<Renderer>(true);
        animatorCharco = charcoObjeto.GetComponentInChildren<Animator>(true);

        // Ocultar charco visualmente al inicio sin matar este script
        EstablecerVisibilidadCharco(false);

        // Configurar el Timer VR con el tiempo total especificado y OCULTO
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
        }
        timer.tiempoTotalSegundos = tiempoTotalEscena;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);
            volumeAmbiente.profile.TryGet(out filmGrain);
        }

        StartCoroutine(CronologiaEscena3());
    }

    private void EstablecerVisibilidadCharco(bool visible)
    {
        if (renderersCharco != null)
        {
            foreach (var r in renderersCharco)
            {
                if (r != null) r.enabled = visible;
            }
        }

        // Si charcoObjeto es un objeto separado de este script, usar SetActive de forma segura
        if (charcoObjeto != null && charcoObjeto != this.gameObject)
        {
            charcoObjeto.SetActive(visible);
        }
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 3
    // =========================================================================
    IEnumerator CronologiaEscena3()
    {
        // SI ESTÁ EN MODO PRUEBA: Ir directamente a las manos y bajada de cámara
        if (probarFaseCharcoDeInmediato)
        {
            Debug.Log("🧪 MODO PRUEBA ACTIVADO: Iniciando animación de manos y hundimiento de cámara de inmediato...");
            yield return StartCoroutine(EjecutarFaseCharcoYConsumo());
            yield break;
        }

        // ---------------------------------------------------------------------
        // FASE 1 (0s - 10s): Levantarse (Fade-in suave y posición de pie)
        // ---------------------------------------------------------------------
        if (vignette != null)
        {
            vignette.color.value = Color.black;
            vignette.intensity.value = 1.0f;
        }

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

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(1.0f, 0.0f, factor);
            }

            if (xrOriginTransform != null)
            {
                xrOriginTransform.position = Vector3.Lerp(posicionOriginalXR - new Vector3(0, 0.7f, 0), posicionOriginalXR, factor);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 2: Exploración, estática y música abrumadora
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null && !audioMusicaEstatica.isPlaying) audioMusicaEstatica.Play();
        if (nieblaEspesa != null) nieblaEspesa.SetActive(true);

        float duracionFase2 = Mathf.Max(5.0f, tiempoTotalEscena - duracionFase1 - duracionFaseCharco);
        float tFase2 = 0f;

        while (tFase2 < duracionFase2)
        {
            tFase2 += Time.deltaTime;
            float factorFase2 = Mathf.Clamp01(tFase2 / duracionFase2);

            if (audioMusicaEstatica != null)
            {
                audioMusicaEstatica.volume = Mathf.Lerp(0.15f, 0.95f, factorFase2);
            }

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(0f, 0.45f, factorFase2);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 3 (Fase Final): Las manos emergen y la cámara se hunde progresivamente
        // ---------------------------------------------------------------------
        yield return StartCoroutine(EjecutarFaseCharcoYConsumo());
    }

    IEnumerator EjecutarFaseCharcoYConsumo()
    {
        // 1. Posicionar el charco bajo los pies del jugador
        if (charcoObjeto != null)
        {
            if (puntoSpawnCharco != null)
            {
                charcoObjeto.transform.position = puntoSpawnCharco.position;
                charcoObjeto.transform.rotation = puntoSpawnCharco.rotation;
            }
            else if (jugadorVR != null)
            {
                Vector3 posJugadorSuelo = new Vector3(jugadorVR.position.x, 0f, jugadorVR.position.z);
                Vector3 dirMiradaHorizonte = Vector3.ProjectOnPlane(jugadorVR.forward, Vector3.up).normalized;
                
                charcoObjeto.transform.position = posJugadorSuelo + (dirMiradaHorizonte * 0.15f);
                if (dirMiradaHorizonte != Vector3.zero)
                {
                    charcoObjeto.transform.rotation = Quaternion.LookRotation(dirMiradaHorizonte);
                }
            }

            // Hacer visible el charco y sus renderers
            EstablecerVisibilidadCharco(true);

            // Iniciar animación del charco / manos
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
                Animation legacyAnim = charcoObjeto.GetComponentInChildren<Animation>();
                if (legacyAnim != null)
                {
                    legacyAnim.Play();
                }
            }
        }

        if (audioManosCharco != null) audioManosCharco.Play();

        // 2. Transición simultánea: Las manos emergen MIENTRAS la cámara desciende
        float tFase3 = 0f;
        Vector3 posInicialArrastre = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;

        while (tFase3 < duracionFaseCharco)
        {
            tFase3 += Time.deltaTime;
            float factorFase3 = Mathf.Clamp01(tFase3 / duracionFaseCharco);

            // Curva suave de aceleración: Las manos emergen y la cámara empieza a bajar coordinadamente
            float hundimientoProgreso = Mathf.SmoothStep(0f, 1f, factorFase3);

            // Arrastrar físicamente la cámara hacia el suelo (hacia el charco)
            if (xrOriginTransform != null)
            {
                Vector3 offsetBajada = new Vector3(0, hundimientoProgreso * profundidadHundimiento, 0);
                
                // Temblor de cámara angustioso mientras es consumido
                if (aplicarTemblorCamara && factorFase3 < 0.92f)
                {
                    float temblorX = (Mathf.PerlinNoise(Time.time * 28f, 0f) - 0.5f) * 0.05f * factorFase3;
                    float temblorZ = (Mathf.PerlinNoise(0f, Time.time * 28f) - 0.5f) * 0.05f * factorFase3;
                    offsetBajada += new Vector3(temblorX, 0, temblorZ);
                }

                xrOriginTransform.position = posInicialArrastre - offsetBajada;
            }

            // Viñeta a negro simultánea que cubre el campo visual
            if (vignette != null)
            {
                vignette.color.value = Color.black;
                vignette.intensity.value = Mathf.Lerp(0.35f, 1.0f, factorFase3);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // CIERRE ABRUPTO: Oscuridad total y silencio absoluto (pantalla de muerte)
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null) audioMusicaEstatica.Stop();
        if (audioManosCharco != null) audioManosCharco.Stop();
        if (vignette != null) vignette.intensity.value = 1.0f;

        Debug.Log("🏁 Experiencia VR completada: Jugador totalmente consumido por el charco de manos.");
    }
}