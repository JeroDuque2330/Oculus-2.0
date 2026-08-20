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

    [Header("Charco y Manos (130s - 150s)")]
    [Tooltip("GameObject o Prefab del modelo charco.fbx con la animación de las manos")]
    public GameObject charcoObjeto;

    [Tooltip("Posición en el suelo donde emergerán las manos. Si está vacío, se coloca frente al jugador")]
    public Transform puntoSpawnCharco;

    [Header("Audio")]
    [Tooltip("Música y estática ambiental que abruma lentamente (10s - 130s)")]
    public AudioSource audioMusicaEstatica;

    [Tooltip("Sonido de manos emergiendo y atrapando al jugador (130s - 150s)")]
    public AudioSource audioManosCharco;

    [Header("Efectos de Niebla / Partículas")]
    [Tooltip("Partículas o GameObject de la niebla espesa")]
    public GameObject nieblaEspesa;

    // Componentes de Post-Processing
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private FilmGrain filmGrain;

    void Start()
    {
        if (jugadorVR == null && Camera.main != null)
        {
            jugadorVR = Camera.main.transform;
        }

        // Configurar el Timer VR para 150 segundos (2:30 min)
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
            timer.tiempoTotalSegundos = 150.0f;
        }

        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);
            volumeAmbiente.profile.TryGet(out filmGrain);
        }

        if (charcoObjeto != null)
        {
            charcoObjeto.SetActive(false);
        }

        StartCoroutine(CronologiaEscena3());
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 3 (Total: 150 seg / 2:30 min)
    // =========================================================================
    IEnumerator CronologiaEscena3()
    {
        // ---------------------------------------------------------------------
        // FASE 1 (0s - 10s / 10 seg): Levantarse (Fade-in suave y posición de pie)
        // ---------------------------------------------------------------------
        if (vignette != null)
        {
            vignette.color.value = Color.black;
            vignette.intensity.value = 1.0f;
        }

        Vector3 posicionOriginalXR = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;
        if (xrOriginTransform != null)
        {
            // Empezar en el suelo
            xrOriginTransform.position = posicionOriginalXR - new Vector3(0, 0.7f, 0);
        }

        float tFase1 = 0f;
        while (tFase1 < 10.0f)
        {
            tFase1 += Time.deltaTime;
            float factor = Mathf.Clamp01(tFase1 / 10.0f);

            // Aclarar la visión
            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(1.0f, 0.0f, factor);
            }

            // Subir suavemente la posición a nivel de pie
            if (xrOriginTransform != null)
            {
                xrOriginTransform.position = Vector3.Lerp(posicionOriginalXR - new Vector3(0, 0.7f, 0), posicionOriginalXR, factor);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 2 (10s - 130s / 120 seg / 2:00 min): Exploración, estática y música abrumadora
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null && !audioMusicaEstatica.isPlaying) audioMusicaEstatica.Play();
        if (nieblaEspesa != null) nieblaEspesa.SetActive(true);

        float tFase2 = 0f;
        float duracionFase2 = 120.0f;

        while (tFase2 < duracionFase2)
        {
            tFase2 += Time.deltaTime;
            float factorFase2 = Mathf.Clamp01(tFase2 / duracionFase2);

            // Aumento gradual de volumen de música y estática
            if (audioMusicaEstatica != null)
            {
                audioMusicaEstatica.volume = Mathf.Lerp(0.15f, 0.95f, factorFase2);
            }

            // Estática / Viñeta suave envolvente
            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(0f, 0.45f, factorFase2);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 3 (130s - 150s / 20 seg): Manos de charco.fbx atrapan y arrastran (Anotación 1)
        // ---------------------------------------------------------------------
        // Posicionar y activar el charco
        if (charcoObjeto != null)
        {
            if (puntoSpawnCharco != null)
            {
                charcoObjeto.transform.position = puntoSpawnCharco.position;
            }
            else if (jugadorVR != null)
            {
                Vector3 posSuelo = new Vector3(jugadorVR.position.x, 0f, jugadorVR.position.z);
                charcoObjeto.transform.position = posSuelo + (jugadorVR.forward * 0.5f);
            }
            charcoObjeto.SetActive(true);
        }

        if (audioManosCharco != null) audioManosCharco.Play();

        float tFase3 = 0f;
        float duracionFase3 = 20.0f;
        Vector3 posInicialArrastre = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;

        while (tFase3 < duracionFase3)
        {
            tFase3 += Time.deltaTime;
            float factorFase3 = Mathf.Clamp01(tFase3 / duracionFase3);

            // Arrastrar físicamente hacia abajo (hacia el charco)
            if (xrOriginTransform != null)
            {
                xrOriginTransform.position = posInicialArrastre - new Vector3(0, factorFase3 * 1.8f, 0);
            }

            // Oscurecimiento hacia la oscuridad total
            if (vignette != null)
            {
                vignette.color.value = Color.black;
                vignette.intensity.value = Mathf.Lerp(0.45f, 1.0f, factorFase3);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // CIERRE ABRUPTO: Oscuridad total y silencio absoluto (como pantalla de muerte)
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null) audioMusicaEstatica.Stop();
        if (audioManosCharco != null) audioManosCharco.Stop();
        if (vignette != null) vignette.intensity.value = 1.0f;

        Debug.Log("🏁 Experiencia VR 'El ruido de estar solo' completada con éxito.");
    }
}