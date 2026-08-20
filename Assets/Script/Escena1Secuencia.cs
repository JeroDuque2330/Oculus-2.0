using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class Escena1Secuencia : MonoBehaviour
{
    [Header("Referencias Principales")]
    [Tooltip("La cámara del casco VR (Main Camera)")]
    public Transform jugadorVR;

    [Tooltip("El objeto XR Origin o Camera Offset para ajustar la altura")]
    public Transform xrOriginTransform;

    [Tooltip("El Prefab de la sombra con la animación de Marcha")]
    public GameObject npcPrefab;

    [Tooltip("El objeto Global Volume con el Post-Processing")]
    public Volume volumeAmbiente;

    [Header("Ajuste de Altura (Anotación 2)")]
    [Tooltip("Reduce la altura de la cámara para que el usuario se sienta más pequeño frente a las sombras")]
    public float reduccionAlturaCamara = 0.35f;

    [Header("Audio")]
    [Tooltip("Sonidos de fondo abrumantes de la ciudad/entorno")]
    public AudioSource audioAmbienteAbrumador;

    [Tooltip("Murmullos que entran a partir del segundo 40")]
    public AudioSource audioMurmullos;

    [Tooltip("Latidos profundos que entran a partir del segundo 40")]
    public AudioSource audioLatidos;

    [Tooltip("Filtro pasa-bajos para el ambiente")]
    public AudioLowPassFilter filtroAmbiente;

    [Header("Configuración de la Multitud")]
    public float distanciaSpawn = 18.0f;
    public float cercaniaAlJugador = 1.2f;
    public float velocidadMin = 0.75f;
    public float velocidadMax = 0.95f;
    public float tiempoEntreSpawns = 0.18f;
    public int multitudInicial = 45;

    [Header("Rojo Carmesí (40s - 60s)")]
    public Color colorRojoCarmesi = new Color(0.85f, 0.05f, 0.05f);

    [Header("Transición")]
    public string nombreEscena2 = "Escena 2";

    // Componentes internos
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private bool permitirSpawns = true;
    public static List<CaminanteMarcha> listaNPCs = new List<CaminanteMarcha>();

    void Start()
    {
        listaNPCs.Clear();

        if (jugadorVR == null && Camera.main != null)
        {
            jugadorVR = Camera.main.transform;
        }

        // Bajar ligeramente la cámara si se especificó el XR Origin o Camera Offset
        if (xrOriginTransform != null)
        {
            xrOriginTransform.position += new Vector3(0, -reduccionAlturaCamara, 0);
        }

        // Asegurar que el Timer esté presente con 60 segundos
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
            timer.tiempoTotalSegundos = 60.0f;
        }

        // Setup inicial de Post-Processing: limpio
        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);

            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.white;
            if (vignette != null)
            {
                vignette.color.value = colorRojoCarmesi;
                vignette.intensity.value = 0f;
            }
        }

        if (audioAmbienteAbrumador != null && !audioAmbienteAbrumador.isPlaying)
        {
            audioAmbienteAbrumador.Play();
        }

        // Generar multitud inicial
        for (int i = 0; i < multitudInicial; i++)
        {
            SpawnearNPC(Random.Range(2.5f, distanciaSpawn));
        }

        StartCoroutine(GeneradorContinuo());
        StartCoroutine(CronologiaEscena1());
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 1 (Total: 60 seg / 1:00 min)
    // =========================================================================
    IEnumerator CronologiaEscena1()
    {
        // ---------------------------------------------------------------------
        // FASE 1 (0s - 40s): Marcha indiferente y ambiente abrumador
        // ---------------------------------------------------------------------
        yield return new WaitForSeconds(40.0f);

        // ---------------------------------------------------------------------
        // FASE 2 (40s - 60s / 20 seg): Rojo carmesí progresivo, murmullos y latidos
        // ---------------------------------------------------------------------
        if (audioMurmullos != null && !audioMurmullos.isPlaying) audioMurmullos.Play();
        if (audioLatidos != null && !audioLatidos.isPlaying) audioLatidos.Play();

        float duracionRojo = 20.0f;
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracionRojo)
        {
            tiempoTranscurrido += Time.deltaTime;
            float factor = Mathf.Clamp01(tiempoTranscurrido / duracionRojo);

            // Tinte progresivo a rojo carmesí envolvente (100% de cobertura)
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.value = Color.Lerp(Color.white, colorRojoCarmesi, factor);
            }
            if (vignette != null)
            {
                vignette.color.value = colorRojoCarmesi;
                vignette.intensity.value = Mathf.Lerp(0f, 1.0f, factor);
            }

            // Volumen de murmullos y latidos subiendo
            if (audioMurmullos != null) audioMurmullos.volume = Mathf.Lerp(0.1f, 0.9f, factor);
            if (audioLatidos != null) audioLatidos.volume = Mathf.Lerp(0.2f, 1.0f, factor);

            // El ambiente de la ciudad se ahoga (filtro pasa-bajos)
            if (filtroAmbiente != null)
            {
                filtroAmbiente.cutoffFrequency = Mathf.Lerp(22000f, 600f, factor);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // TRANSICIÓN AL SEGUNDO 60 A ESCENA 2
        // ---------------------------------------------------------------------
        permitirSpawns = false;
        if (Application.CanStreamedLevelBeLoaded(nombreEscena2))
        {
            SceneManager.LoadScene(nombreEscena2);
        }
        else
        {
            Debug.Log("🏁 Fin Escena 1 (60s). Cargando: " + nombreEscena2);
        }
    }

    IEnumerator GeneradorContinuo()
    {
        while (permitirSpawns)
        {
            SpawnearNPC(distanciaSpawn);
            yield return new WaitForSeconds(tiempoEntreSpawns);
        }
    }

    void SpawnearNPC(float distancia)
    {
        if (npcPrefab == null || jugadorVR == null) return;

        float angulo = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(angulo) * distancia, 0, Mathf.Cos(angulo) * distancia);
        Vector3 posicionSpawn = new Vector3(jugadorVR.position.x + offset.x, npcPrefab.transform.position.y, jugadorVR.position.z + offset.z);

        float radioTangente = Random.Range(0.6f, cercaniaAlJugador);
        float ladoSigno = (Random.value > 0.5f) ? 1f : -1f;
        Vector3 perpendicular = new Vector3(-offset.z, 0, offset.x).normalized * (radioTangente * ladoSigno);
        Vector3 puntoDestino = jugadorVR.position + perpendicular;

        GameObject nuevoNPC = Instantiate(npcPrefab, posicionSpawn, Quaternion.identity);
        nuevoNPC.SetActive(true);

        CaminanteMarcha caminante = nuevoNPC.GetComponent<CaminanteMarcha>();
        if (caminante == null)
        {
            caminante = nuevoNPC.AddComponent<CaminanteMarcha>();
        }

        float velocidad = Random.Range(velocidadMin, velocidadMax);
        caminante.Iniciar(puntoDestino, velocidad);
    }
}