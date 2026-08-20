using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class Escena2Secuencia : MonoBehaviour
{
    [Header("Referencias Principales")]
    [Tooltip("La cámara del casco VR (Main Camera)")]
    public Transform jugadorVR;

    [Tooltip("El objeto Global Volume con el Post-Processing")]
    public Volume volumeAmbiente;

    [Header("Colección de Sombras / NPCs")]
    [Tooltip("Lista de sombras presentes en la escena. Si está vacía, se buscan automáticamente en escena")]
    public List<SombraAnimador> sombrasEnEscena = new List<SombraAnimador>();

    [Header("Audio")]
    [Tooltip("Sonido de vidrio que se rompe tajantemente (0s - 10s)")]
    public AudioSource audioVidrioRoto;

    [Tooltip("Gritos y regaños imperceptibles que crecen (40s - 60s)")]
    public AudioSource audioGritosReganos;

    [Tooltip("Latidos y sonidos de angustia durante el parpadeo (60s - 90s)")]
    public AudioSource audioAngustiaLatidos;

    [Header("Transición")]
    public string nombreEscena3 = "Escena 3";

    // Componentes de Post-Processing
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private LensDistortion lensDistortion;

    void Start()
    {
        if (jugadorVR == null && Camera.main != null)
        {
            jugadorVR = Camera.main.transform;
        }

        // Buscar todas las sombras si la lista no fue asignada manualmente
        if (sombrasEnEscena.Count == 0)
        {
            sombrasEnEscena.AddRange(FindObjectsByType<SombraAnimador>(FindObjectsSortMode.None));
        }

        // Configurar el Timer VR para 90 segundos (1:30 min) oculto en el visor
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
        }
        timer.tiempoTotalSegundos = 90.0f;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        // Obtener componentes de Post-Processing
        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);
            volumeAmbiente.profile.TryGet(out lensDistortion);
        }

        StartCoroutine(CronologiaEscena2());
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 2 (Total: 90 seg / 1:30 min)
    // =========================================================================
    IEnumerator CronologiaEscena2()
    {
        // ---------------------------------------------------------------------
        // FASE 1 (0s - 10s / 10 seg): Quiebre súbito del rojo carmesí (Vidrio roto)
        // ---------------------------------------------------------------------
        // Iniciar en rojo carmesí saturado
        if (colorAdjustments != null) colorAdjustments.colorFilter.value = new Color(0.85f, 0.05f, 0.05f);
        if (vignette != null)
        {
            vignette.color.value = new Color(0.85f, 0.05f, 0.05f);
            vignette.intensity.value = 0.95f;
        }

        yield return new WaitForSeconds(1.0f);

        // ¡QUIEBRE DE VIDRIO!
        if (audioVidrioRoto != null) audioVidrioRoto.Play();

        // Corte seco e instantáneo a visión completamente nítida
        if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.white;
        if (vignette != null)
        {
            vignette.color.value = Color.black;
            vignette.intensity.value = 0f;
        }

        yield return new WaitForSeconds(9.0f);

        // ---------------------------------------------------------------------
        // FASE 2 (10s - 40s / 30 seg): Silencio y giro de cabezas (Vista derecha / Vista izquierda)
        // ---------------------------------------------------------------------
        // Detener marcha y hacer que giren la cabeza escalonadamente
        for (int i = 0; i < sombrasEnEscena.Count; i++)
        {
            SombraAnimador sombra = sombrasEnEscena[i];
            if (sombra != null)
            {
                float retraso = Random.Range(0.2f, 3.5f);
                StartCoroutine(sombra.GirarYMirarJugador(jugadorVR, retraso));
            }
        }

        yield return new WaitForSeconds(30.0f);

        // ---------------------------------------------------------------------
        // FASE 3 (40s - 60s / 20 seg): Las sombras se acercan, crecen y gritos crecientes
        // ---------------------------------------------------------------------
        if (audioGritosReganos != null && !audioGritosReganos.isPlaying) audioGritosReganos.Play();

        foreach (var sombra in sombrasEnEscena)
        {
            if (sombra != null)
            {
                sombra.IniciarAcorralamiento(jugadorVR, 1.2f, 1.5f);
            }
        }

        float tFase3 = 0f;
        while (tFase3 < 20.0f)
        {
            tFase3 += Time.deltaTime;
            float factor = Mathf.Clamp01(tFase3 / 20.0f);

            // Subir volumen de gritos y regaños
            if (audioGritosReganos != null) audioGritosReganos.volume = Mathf.Lerp(0.2f, 1.0f, factor);

            // Distorsión óptica de lente para que se vean más deformadas y grandes
            if (lensDistortion != null)
            {
                lensDistortion.intensity.value = Mathf.Lerp(0f, -0.6f, factor);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 4 (60s - 90s / 30 seg): Cerco total, parpadeo negro y sonidos de angustia
        // ---------------------------------------------------------------------
        if (audioAngustiaLatidos != null && !audioAngustiaLatidos.isPlaying) audioAngustiaLatidos.Play();

        float tFase4 = 0f;
        while (tFase4 < 30.0f)
        {
            tFase4 += Time.deltaTime;
            float factorFinal = Mathf.Clamp01(tFase4 / 30.0f);

            // Simulación de parpadeo a negro oscilante
            if (vignette != null)
            {
                vignette.color.value = Color.black;
                float parpadeo = Mathf.Abs(Mathf.Sin(tFase4 * 2.5f)) * 0.7f + (factorFinal * 0.35f);
                vignette.intensity.value = Mathf.Clamp01(parpadeo);
            }

            if (audioAngustiaLatidos != null) audioAngustiaLatidos.volume = Mathf.Lerp(0.3f, 1.0f, factorFinal);

            yield return null;
        }

        // Fundido total a negro
        if (vignette != null) vignette.intensity.value = 1.0f;

        // ---------------------------------------------------------------------
        // TRANSICIÓN AL SEGUNDO 90 A ESCENA 3
        // ---------------------------------------------------------------------
        if (Application.CanStreamedLevelBeLoaded(nombreEscena3))
        {
            SceneManager.LoadScene(nombreEscena3);
        }
        else
        {
            Debug.Log("🏁 Fin Escena 2 (90s). Cargando: " + nombreEscena3);
        }
    }
}