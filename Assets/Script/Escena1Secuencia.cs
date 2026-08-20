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

    [Header("Ajuste de Altura")]
    [Tooltip("Reduce la altura de la cámara para que el usuario se sienta más pequeño frente a las sombras")]
    public float reduccionAlturaCamara = 0.35f;

    [Header("Audio")]
    [Tooltip("Sonidos de fondo abrumantes de la ciudad/entorno")]
    public AudioSource audioAmbienteAbrumador;

    [Tooltip("Murmullos que entran a partir del segundo 30")]
    public AudioSource audioMurmullos;

    [Tooltip("Latidos profundos que entran a partir del segundo 30")]
    public AudioSource audioLatidos;

    [Tooltip("Filtro pasa-bajos para el ambiente")]
    public AudioLowPassFilter filtroAmbiente;

    [Header("Configuración de Multitud")]
    public float distanciaSpawn = 18.0f;
    public float radioMinimoCercano = 1.1f;
    public float radioMaximoCercano = 2.2f;
    public float velocidadCaminarMin = 0.75f;
    public float velocidadCaminarMax = 0.95f;
    public float velocidadCorrerMin = 2.4f;
    public float velocidadCorrerMax = 3.2f;
    public float tiempoEntreSpawns = 0.25f;
    public int multitudInicial = 65;
    public int limiteMaximoNPCs = 85;

    [Header("Rojo Carmesí (A partir del segundo 30)")]
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

        // Asegurar que el Timer esté presente pero TOTALMENTE OCULTO en el visor
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
        }
        timer.tiempoTotalSegundos = 60.0f;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        // 1. Setup inicial: La escena empieza completamente NORMAL y limpia
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

        if (filtroAmbiente != null)
        {
            filtroAmbiente.cutoffFrequency = 22000f; // Audio normal y nítido
        }

        if (audioAmbienteAbrumador != null && !audioAmbienteAbrumador.isPlaying)
        {
            audioAmbienteAbrumador.Play();
        }

        if (audioMurmullos != null && audioMurmullos.isPlaying) audioMurmullos.Stop();
        if (audioLatidos != null && audioLatidos.isPlaying) audioLatidos.Stop();

        // Generar multitud inicial caminando normalmente por la escena
        for (int i = 0; i < multitudInicial; i++)
        {
            SpawnearNPC(Random.Range(2.5f, distanciaSpawn));
        }

        StartCoroutine(GeneradorContinuo());
        StartCoroutine(CronologiaEscena1());
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 1 (Total: 60 seg / 1:00 min)
    // 
    // 1. (0s - 30s): Caminan normal y escena normal.
    // 2. (30s - 42s): Se torna rojo del todo con latidos y murmullos.
    // 3. (42s - 50s): Se quedan quietos y mirando al personaje.
    // 4. (50s - 60s): Corren alrededor del personaje muy cerca por 10 segundos completos.
    // 5. (60s): Al terminar los 10 segundos corriendo, cambia a Escena 2.
    // =========================================================================
    IEnumerator CronologiaEscena1()
    {
        // ---------------------------------------------------------------------
        // FASE 1 (0s - 30s / 30 seg): CAMINAN NORMAL Y ESCENA NORMAL
        // ---------------------------------------------------------------------
        yield return new WaitForSeconds(30.0f);

        // ---------------------------------------------------------------------
        // FASE 2 (30s - 42s / 12 seg): SE TORNA ROJO DEL TODO
        // ---------------------------------------------------------------------
        if (audioMurmullos != null && !audioMurmullos.isPlaying) audioMurmullos.Play();
        if (audioLatidos != null && !audioLatidos.isPlaying) audioLatidos.Play();

        float duracionRojo = 12.0f;
        float tiempoRojo = 0f;

        while (tiempoRojo < duracionRojo)
        {
            tiempoRojo += Time.deltaTime;
            float factor = Mathf.Clamp01(tiempoRojo / duracionRojo);

            // Tinte progresivo a rojo carmesí envolvente (pantalla roja del todo)
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

            // El ambiente de la ciudad se va ahogando
            if (filtroAmbiente != null)
            {
                filtroAmbiente.cutoffFrequency = Mathf.Lerp(22000f, 600f, factor);
            }

            yield return null;
        }

        // Pantalla completamente roja asegurada al 100%
        if (colorAdjustments != null) colorAdjustments.colorFilter.value = colorRojoCarmesi;
        if (vignette != null) vignette.intensity.value = 1.0f;

        // ---------------------------------------------------------------------
        // FASE 3 (42s - 50s / 8 seg): SE QUEDAN TOTALMENTE QUIETOS
        // ---------------------------------------------------------------------
        permitirSpawns = false; // Detener nuevos spawns
        for (int i = 0; i < listaNPCs.Count; i++)
        {
            CaminanteMarcha npc = listaNPCs[i];
            if (npc != null)
            {
                npc.QuedarseTotalmenteQuietoMirando(jugadorVR);
            }
        }

        yield return new WaitForSeconds(8.0f);

        // ---------------------------------------------------------------------
        // FASE 4 (50s - 60s / 10 SEGUNDOS COMPLETOS): CORREN ALREDEDOR MUY CERCA
        // ---------------------------------------------------------------------
        for (int i = 0; i < listaNPCs.Count; i++)
        {
            CaminanteMarcha npc = listaNPCs[i];
            if (npc != null)
            {
                float radioCercano = Random.Range(radioMinimoCercano, radioMaximoCercano);
                float velocidadCorrer = Random.Range(velocidadCorrerMin, velocidadCorrerMax);
                npc.CorrerMuyCercaRodeando(jugadorVR, radioCercano, velocidadCorrer);
            }
        }

        // NO CAMBIAR DE ESCENA HASTA QUE CORRAN POR 10 SEGUNDOS ALREDEDOR
        yield return new WaitForSeconds(10.0f);

        // ---------------------------------------------------------------------
        // SEGUNDO 60: TRANSICIÓN A ESCENA 2 (Tras los 10s exactos corriendo)
        // ---------------------------------------------------------------------
        if (Application.CanStreamedLevelBeLoaded(nombreEscena2))
        {
            SceneManager.LoadScene(nombreEscena2);
        }
        else
        {
            Debug.Log("🏁 Fin Escena 1 (60s con 10s corriendo alrededor). Cargando: " + nombreEscena2);
        }
    }

    IEnumerator GeneradorContinuo()
    {
        while (permitirSpawns)
        {
            if (listaNPCs.Count < limiteMaximoNPCs)
            {
                SpawnearNPC(distanciaSpawn);
            }
            yield return new WaitForSeconds(tiempoEntreSpawns);
        }
    }

    void SpawnearNPC(float distancia)
    {
        if (npcPrefab == null || jugadorVR == null) return;

        float angulo = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(angulo) * distancia, 0, Mathf.Cos(angulo) * distancia);
        Vector3 posicionSpawn = new Vector3(jugadorVR.position.x + offset.x, npcPrefab.transform.position.y, jugadorVR.position.z + offset.z);

        float radioTangente = Random.Range(1.8f, 4.5f);
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

        float velocidad = Random.Range(velocidadCaminarMin, velocidadCaminarMax);
        float radioCercano = Random.Range(radioMinimoCercano, radioMaximoCercano);
        float sentido = (Random.value > 0.5f) ? 1f : -1f;

        caminante.IniciarMarchaIndiferente(jugadorVR, puntoDestino, velocidad, radioCercano, sentido);
    }
}