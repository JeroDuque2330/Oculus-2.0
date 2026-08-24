using UnityEngine;
using UnityEngine.UI;
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
    [Tooltip("Radio de carrera súper cercano al jugador (en metros)")]
    public float radioMinimoCercano = 0.65f;
    public float radioMaximoCercano = 1.15f;
    public float velocidadCaminarMin = 0.75f;
    public float velocidadCaminarMax = 0.95f;
    public float velocidadCorrerMin = 3.4f;
    public float velocidadCorrerMax = 4.2f;

    [Header("Escala Gigante al Correr (Para agobiar al jugador)")]
    [Tooltip("Multiplicador de tamaño mínimo al empezar a correr")]
    public float escalaGiganteMin = 1.45f;
    [Tooltip("Multiplicador de tamaño máximo al empezar a correr")]
    public float escalaGiganteMax = 1.65f;

    public float tiempoEntreSpawns = 0.25f;
    public int multitudInicial = 65;
    public int limiteMaximoNPCs = 85;

    [Header("Rojo Carmesí (A partir del segundo 30)")]
    public Color colorRojoCarmesi = new Color(0.85f, 0.05f, 0.05f);

    [Header("Transición de Párpados (Entrecerrar Ojos)")]
    [Tooltip("Activar el efecto visual de entrecerrar y cerrar los ojos antes de cambiar a Escena 2")]
    public bool activarEfectoParpados = true;
    [Tooltip("Duración más humana y pausada de la pesadez, pestañeo y cierre final de ojos (en segundos)")]
    public float duracionEfectoParpados = 5.5f;

    [Header("Transición")]
    public string nombreEscena2 = "Escena 2";

    // Componentes internos y de Párpados
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private bool permitirSpawns = true;
    public static List<CaminanteMarcha> listaNPCs = new List<CaminanteMarcha>();

    private GameObject overlayParpadosObj;
    private RectTransform rectParpadoSuperior;
    private RectTransform rectParpadoInferior;

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

        // Construir el sistema visual de párpados frente al visor VR
        CrearOverlayParpados();
        SetCierreParpados(0f); // Ojos abiertos al inicio

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

    private void CrearOverlayParpados()
    {
        if (jugadorVR == null) return;

        overlayParpadosObj = new GameObject("VR_Parpados_Overlay");
        overlayParpadosObj.transform.SetParent(jugadorVR, false);
        overlayParpadosObj.transform.localPosition = new Vector3(0, 0, 0.22f);
        overlayParpadosObj.transform.localRotation = Quaternion.identity;

        Canvas canvas = overlayParpadosObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 9999;

        RectTransform rectCanvas = overlayParpadosObj.GetComponent<RectTransform>();
        rectCanvas.sizeDelta = new Vector2(3.2f, 3.2f);

        // Párpado Superior Humano (Hace el 70% del recorrido hacia abajo)
        GameObject superiorObj = new GameObject("ParpadoSuperior");
        superiorObj.transform.SetParent(overlayParpadosObj.transform, false);
        Image imgSuperior = superiorObj.AddComponent<Image>();
        imgSuperior.color = Color.black;

        rectParpadoSuperior = superiorObj.GetComponent<RectTransform>();
        rectParpadoSuperior.anchorMin = new Vector2(0f, 0.32f);
        rectParpadoSuperior.anchorMax = new Vector2(1f, 1f);
        rectParpadoSuperior.pivot = new Vector2(0.5f, 1f);
        rectParpadoSuperior.anchoredPosition = Vector2.zero;
        rectParpadoSuperior.sizeDelta = Vector2.zero;
        rectParpadoSuperior.localScale = new Vector3(1f, 0f, 1f);

        // Párpado Inferior Humano (Hace el 30% del recorrido hacia arriba)
        GameObject inferiorObj = new GameObject("ParpadoInferior");
        inferiorObj.transform.SetParent(overlayParpadosObj.transform, false);
        Image imgInferior = inferiorObj.AddComponent<Image>();
        imgInferior.color = Color.black;

        rectParpadoInferior = inferiorObj.GetComponent<RectTransform>();
        rectParpadoInferior.anchorMin = new Vector2(0f, 0f);
        rectParpadoInferior.anchorMax = new Vector2(1f, 0.32f);
        rectParpadoInferior.pivot = new Vector2(0.5f, 0f);
        rectParpadoInferior.anchoredPosition = Vector2.zero;
        rectParpadoInferior.sizeDelta = Vector2.zero;
        rectParpadoInferior.localScale = new Vector3(1f, 0f, 1f);
    }

    /// <summary>
    /// Establece el cierre de párpados: 0 = Ojos totalmente abiertos, 1 = Ojos totalmente cerrados.
    /// </summary>
    public void SetCierreParpados(float cierre)
    {
        cierre = Mathf.Clamp01(cierre);
        if (rectParpadoSuperior != null)
        {
            rectParpadoSuperior.localScale = new Vector3(1f, cierre, 1f);
        }
        if (rectParpadoInferior != null)
        {
            rectParpadoInferior.localScale = new Vector3(1f, cierre, 1f);
        }
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 1 (Total: 60 seg / 1:00 min)
    // 
    // 1. (0s - 30s): Caminan normal y escena normal.
    // 2. (30s - 42s): Se torna rojo del todo con latidos y murmullos.
    // 3. (42s - 50s): Se quedan quietos y mirando al personaje.
    // 4. (50s - 60s): Corren alrededor del personaje muy cerca y creciendo de tamaño.
    //                 Al final (~5.5s), transición humana de pesadez y cierre de ojos.
    // 5. (60s): Cierre de ojos 100% y transición inmediata a Escena 2.
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
        // FASE 4 (50s - 60s / 10 SEGUNDOS COMPLETOS): CORREN MUY CERCA, CRECEN Y ENTRECIERRAN OJOS
        // ---------------------------------------------------------------------
        for (int i = 0; i < listaNPCs.Count; i++)
        {
            CaminanteMarcha npc = listaNPCs[i];
            if (npc != null)
            {
                float radioCercano = Random.Range(radioMinimoCercano, radioMaximoCercano);
                float velocidadCorrer = Random.Range(velocidadCorrerMin, velocidadCorrerMax);
                float multiplicadorEscala = Random.Range(escalaGiganteMin, escalaGiganteMax);
                npc.CorrerMuyCercaRodeando(jugadorVR, radioCercano, velocidadCorrer, multiplicadorEscala);
            }
        }

        // Carrera libre antes de que los párpados empiecen a pesar
        float tiempoCarreraLibre = Mathf.Max(1.0f, 10.0f - duracionEfectoParpados);
        yield return new WaitForSeconds(tiempoCarreraLibre);

        // Efecto visual somático de pestañeo humano y cierre de ojos
        if (activarEfectoParpados)
        {
            yield return StartCoroutine(SecuenciaEntrecerrarOjos(duracionEfectoParpados));
        }
        else
        {
            yield return new WaitForSeconds(duracionEfectoParpados);
        }

        // ---------------------------------------------------------------------
        // SEGUNDO 60: TRANSICIÓN A ESCENA 2 (Con los ojos completamente cerrados)
        // ---------------------------------------------------------------------
        if (Application.CanStreamedLevelBeLoaded(nombreEscena2))
        {
            SceneManager.LoadScene(nombreEscena2);
        }
        else
        {
            Debug.Log("🏁 Fin Escena 1 (60s con transición humana de párpados). Cargando: " + nombreEscena2);
        }
    }

    IEnumerator SecuenciaEntrecerrarOjos(float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float factor = Mathf.Clamp01(t / duracion);

            float cierre = 0f;
            // Micro-temblor involuntario por estrés ocular
            float temblor = (Mathf.PerlinNoise(Time.time * 28f, 0f) - 0.5f) * 0.032f;

            if (factor < 0.20f)
            {
                // 1. Pesadez inicial: Los ojos se entrecierran ligeramente (35%)
                float subT = factor / 0.20f;
                cierre = Mathf.Lerp(0f, 0.35f, Mathf.SmoothStep(0f, 1f, subT));
            }
            else if (factor < 0.44f)
            {
                // 2. Primer pestañeo humano: Caída rápida (88%) y apertura perezosa y lenta (30%)
                float subT = (factor - 0.20f) / 0.24f;
                if (subT < 0.35f)
                {
                    float cT = subT / 0.35f;
                    cierre = Mathf.Lerp(0.35f, 0.88f, Mathf.SmoothStep(0f, 1f, cT));
                }
                else
                {
                    float oT = (subT - 0.35f) / 0.65f;
                    cierre = Mathf.Lerp(0.88f, 0.30f, Mathf.SmoothStep(0f, 1f, oT));
                }
            }
            else if (factor < 0.70f)
            {
                // 3. Segundo pestañeo pesado: Casi cierre (96%), micro-pausa de cansancio y reapertura difícil (50%)
                float subT = (factor - 0.44f) / 0.26f;
                if (subT < 0.30f)
                {
                    float cT = subT / 0.30f;
                    cierre = Mathf.Lerp(0.30f, 0.96f, Mathf.SmoothStep(0f, 1f, cT));
                }
                else if (subT < 0.45f)
                {
                    cierre = 0.96f; // Ojos casi cerrados por agotamiento
                }
                else
                {
                    float oT = (subT - 0.45f) / 0.55f;
                    cierre = Mathf.Lerp(0.96f, 0.50f, Mathf.SmoothStep(0f, 1f, oT));
                }
            }
            else
            {
                // 4. Caída final lenta y pesada: Rendición total hacia el negro absoluto (100%)
                float subT = (factor - 0.70f) / 0.30f;
                cierre = Mathf.Lerp(0.50f, 1.0f, Mathf.SmoothStep(0f, 1f, subT));
            }

            // Aplicar el micro-temblor orgánico en estados intermedios
            if (cierre > 0.05f && cierre < 0.98f)
            {
                cierre = Mathf.Clamp01(cierre + temblor);
            }

            SetCierreParpados(cierre);

            // Ahogar progresivamente los audios en sincronía con la pérdida de conciencia
            if (factor > 0.40f)
            {
                float factorAudio = (factor - 0.40f) / 0.60f;
                if (audioMurmullos != null) audioMurmullos.volume = Mathf.Lerp(0.9f, 0.05f, factorAudio);
                if (audioLatidos != null) audioLatidos.volume = Mathf.Lerp(1.0f, 0.15f, factorAudio);
                if (filtroAmbiente != null) filtroAmbiente.cutoffFrequency = Mathf.Lerp(600f, 120f, factorAudio);
            }

            yield return null;
        }

        SetCierreParpados(1.0f); // Ojos 100% cerrados
        yield return new WaitForSeconds(0.3f);
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