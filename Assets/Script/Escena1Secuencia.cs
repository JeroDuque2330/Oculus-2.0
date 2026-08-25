using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class Escena1Secuencia : MonoBehaviour
{
    private static Escena1Secuencia instance;

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
    public float reduccionAlturaCamara = 0.0f;

    [Tooltip("Ajuste fino de altura de los pies de los NPCs con el suelo (en metros)")]
    public float offsetAlturaPiesNPC = 0.0f;

    [Header("Clips de Audio (Arrastra tus archivos de Audio .wav / .mp3 aquí)")]
    [Tooltip("Audio del ambiente normal de la ciudad/calle (Suena del segundo 0 al 30)")]
    public AudioClip clipAmbienteNormal;

    [Tooltip("Audio del ambiente abrumador/filtrado (FiltroAmbiente.wav - Entra a partir del segundo 30)")]
    public AudioClip clipAmbienteAbrumador;

    [Tooltip("Audio de los latidos profundos del corazón (AudioLatidos.wav - Entra a partir del segundo 30)")]
    public AudioClip clipLatidos;

    [Tooltip("Clip de audio para murmullos o susurros (Opcional)")]
    public AudioClip clipMurmullos;

    [Header("AudioSources (Opcional - Se crean solos si no los asignas)")]
    [Tooltip("Fuente de audio para el ambiente normal inicial")]
    public AudioSource audioAmbienteNormal;

    [Tooltip("Fuente de audio para el ambiente abrumador")]
    public AudioSource audioAmbienteAbrumador;

    [Tooltip("Fuente de audio para los latidos")]
    public AudioSource audioLatidos;

    [Tooltip("Fuente de audio para los murmullos")]
    public AudioSource audioMurmullos;

    [Tooltip("Filtro pasa-bajos para el ambiente (opcional)")]
    public AudioLowPassFilter filtroAmbiente;

    [Header("Configuración de Multitud")]
    public float distanciaSpawn = 18.0f;
    [Tooltip("Radio de carrera súper cercano al jugador (en metros)")]
    public float radioMinimoCercano = 0.65f;
    public float velocidadCaminarMin = 0.8f;
    public float velocidadCaminarMax = 1.3f;
    public float velocidadCorrerMin = 4.0f;
    public float velocidadCorrerMax = 6.0f;
    public int limiteMaximoNPCs = 25;
    public int multitudInicial = 14;
    public float tiempoEntreSpawns = 2.0f;

    [Header("Efectos Visuales (Rojo y Angustia)")]
    public Color colorRojoCarmesi = new Color(0.85f, 0.04f, 0.04f, 1f);

    public static List<CaminanteMarcha> listaNPCs = new List<CaminanteMarcha>();

    // Post-processing interno
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;

    // Párpados UI VR
    private GameObject overlayParpadosObj;
    private RectTransform rectParpadoSuperior;
    private RectTransform rectParpadoInferior;

    private bool permitirSpawns = true;
    private float alturaCalleDetectada = 15.49f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        listaNPCs.Clear();
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

        // Buscar y calibrar la altura exacta del suelo de la calle
        if (npcPrefab != null)
        {
            alturaCalleDetectada = npcPrefab.transform.position.y;
        }
        else
        {
            alturaCalleDetectada = 15.49f;
        }

        // Asegurar que la cámara tenga Post-Processing ACTIVADO en URP
        if (jugadorVR != null)
        {
            UniversalAdditionalCameraData cameraData = jugadorVR.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
            }
        }

        // Buscar el Volume global si no fue asignado
        if (volumeAmbiente == null)
        {
            volumeAmbiente = FindFirstObjectByType<Volume>();
        }

        // Configurar Post-Processing completamente limpio al inicio (Escena normal)
        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);

            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.overrideState = true;
                colorAdjustments.colorFilter.value = Color.white;
            }
            if (vignette != null)
            {
                vignette.color.overrideState = true;
                vignette.color.value = Color.black;
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0f;
            }
        }

        if (filtroAmbiente != null)
        {
            filtroAmbiente.cutoffFrequency = 22000f;
        }

        // 1. Configurar Audio de Ambiente Normal (0s - 30s)
        if (audioAmbienteNormal == null && clipAmbienteNormal != null)
        {
            audioAmbienteNormal = gameObject.AddComponent<AudioSource>();
            audioAmbienteNormal.clip = clipAmbienteNormal;
            audioAmbienteNormal.loop = true;
            audioAmbienteNormal.playOnAwake = false;
            audioAmbienteNormal.spatialBlend = 0f;
            audioAmbienteNormal.volume = 1.0f;
        }
        else if (audioAmbienteNormal != null && clipAmbienteNormal != null)
        {
            audioAmbienteNormal.clip = clipAmbienteNormal;
            audioAmbienteNormal.volume = 1.0f;
        }

        // 2. Configurar Audio de Ambiente Abrumador (FiltroAmbiente.wav - 30s a 60s)
        if (audioAmbienteAbrumador == null && clipAmbienteAbrumador != null)
        {
            audioAmbienteAbrumador = gameObject.AddComponent<AudioSource>();
            audioAmbienteAbrumador.clip = clipAmbienteAbrumador;
            audioAmbienteAbrumador.loop = true;
            audioAmbienteAbrumador.playOnAwake = false;
            audioAmbienteAbrumador.spatialBlend = 0f;
            audioAmbienteAbrumador.volume = 0f;
        }
        else if (audioAmbienteAbrumador != null && clipAmbienteAbrumador != null)
        {
            audioAmbienteAbrumador.clip = clipAmbienteAbrumador;
        }

        // 3. Configurar Audio de Latidos (AudioLatidos.wav - 30s a 60s)
        if (audioLatidos == null && clipLatidos != null)
        {
            audioLatidos = gameObject.AddComponent<AudioSource>();
            audioLatidos.clip = clipLatidos;
            audioLatidos.loop = true;
            audioLatidos.playOnAwake = false;
            audioLatidos.spatialBlend = 0f;
            audioLatidos.volume = 0f;
        }
        else if (audioLatidos != null && clipLatidos != null)
        {
            audioLatidos.clip = clipLatidos;
        }

        // 4. Configurar Murmullos
        if (audioMurmullos == null && clipMurmullos != null)
        {
            audioMurmullos = gameObject.AddComponent<AudioSource>();
            audioMurmullos.clip = clipMurmullos;
            audioMurmullos.loop = true;
            audioMurmullos.playOnAwake = false;
            audioMurmullos.spatialBlend = 0f;
            audioMurmullos.volume = 0f;
        }
        else if (audioMurmullos != null && clipMurmullos != null)
        {
            audioMurmullos.clip = clipMurmullos;
        }

        AudioListener.volume = 1.0f;

        // Iniciar reproducción del ambiente normal al arrancar
        if (audioAmbienteNormal != null && !audioAmbienteNormal.isPlaying)
        {
            audioAmbienteNormal.volume = 1.0f;
            audioAmbienteNormal.Play();
        }

        if (audioAmbienteAbrumador != null && audioAmbienteAbrumador.isPlaying) audioAmbienteAbrumador.Stop();
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

        // Párpado Superior Humano
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

        // Párpado Inferior Humano
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
    // 1. (0s - 30s): Caminan normal, ambiente normal de ciudad.
    // 2. (30s - 42s): Se torna rojo del todo, entra el ambiente abrumador y latidos.
    // 3. (42s - 50s): Se quedan quietos y mirando al personaje.
    // 4. (50s - 60s): Corren alrededor del personaje muy cerca y creciendo de tamaño.
    //                 Al final (~5.5s), transición de pesadez y cierre de ojos.
    // 5. (60s): Transición inmediata a Escena 2.
    // =========================================================================
    IEnumerator CronologiaEscena1()
    {
        // ---------------------------------------------------------------------
        // FASE 1 (0s - 30s / 30 seg): CAMINAN NORMAL Y AMBIENTE NORMAL
        // ---------------------------------------------------------------------
        yield return new WaitForSeconds(30.0f);

        // ---------------------------------------------------------------------
        // FASE 2 (30s - 42s / 12 seg): SE TORNA ROJO DEL TODO, AMBIENTE ABRUMADOR Y LATIDOS
        // ---------------------------------------------------------------------
        if (audioAmbienteAbrumador != null && !audioAmbienteAbrumador.isPlaying) audioAmbienteAbrumador.Play();
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

            // El ambiente normal se apaga mientras el abrumador y los latidos suben con máxima fuerza
            if (audioAmbienteNormal != null) audioAmbienteNormal.volume = Mathf.Lerp(1.0f, 0.05f, factor);
            if (audioAmbienteAbrumador != null) audioAmbienteAbrumador.volume = Mathf.Lerp(0.3f, 1.0f, factor);
            if (audioLatidos != null) audioLatidos.volume = Mathf.Lerp(0.4f, 1.0f, factor);
            if (audioMurmullos != null) audioMurmullos.volume = Mathf.Lerp(0.3f, 1.0f, factor);

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
        permitirSpawns = false;
        for (int i = 0; i < listaNPCs.Count; i++)
        {
            if (listaNPCs[i] != null)
            {
                listaNPCs[i].QuedarseTotalmenteQuietoMirando(jugadorVR);
            }
        }

        yield return new WaitForSeconds(8.0f);

        // ---------------------------------------------------------------------
        // FASE 4 (50s - 60s / 10 seg): CORREN ALREDEDOR MUY CERCA Y CRECEN
        // ---------------------------------------------------------------------
        int mitad = listaNPCs.Count / 2;
        for (int i = 0; i < listaNPCs.Count; i++)
        {
            if (listaNPCs[i] != null)
            {
                float sentido = (i < mitad) ? 1.0f : -1.0f;
                float radio = Random.Range(radioMinimoCercano, radioMinimoCercano + 0.45f);
                float velCorrer = Random.Range(velocidadCorrerMin, velocidadCorrerMax);

                listaNPCs[i].sentidoGiro = sentido;
                listaNPCs[i].CorrerMuyCercaRodeando(jugadorVR, radio, velCorrer, 1.6f);
            }
        }

        // Temblor de angustia creciente en la cabeza del jugador
        float tCorrer = 0f;
        Vector3 posOriginalXR = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;

        while (tCorrer < 4.5f)
        {
            tCorrer += Time.deltaTime;
            float factorTemblor = tCorrer / 10.0f;

            if (xrOriginTransform != null)
            {
                float shakeX = (Mathf.PerlinNoise(Time.time * 28f, 0f) - 0.5f) * factorTemblor * 0.08f;
                float shakeY = (Mathf.PerlinNoise(0f, Time.time * 28f) - 0.5f) * factorTemblor * 0.04f;
                float shakeZ = (Mathf.PerlinNoise(Time.time * 28f, Time.time * 28f) - 0.5f) * factorTemblor * 0.08f;
                xrOriginTransform.position = posOriginalXR + new Vector3(shakeX, shakeY, shakeZ);
            }
            yield return null;
        }

        // Últimos 5.5 segundos (54.5s a 60s): Cierre de párpados y transición a Escena 2
        CrearOverlayParpados();

        float duracionCierre = 5.5f;
        float tCierre = 0f;

        while (tCierre < duracionCierre)
        {
            tCierre += Time.deltaTime;
            float factorCierre = Mathf.Clamp01(tCierre / duracionCierre);
            float curvaCierre = Mathf.SmoothStep(0f, 1f, factorCierre);

            SetCierreParpados(curvaCierre);

            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = Mathf.Lerp(0f, -10f, curvaCierre);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // SEGUNDO 60 (1:00 MINUTO EXACTO): CAMBIO INMEDIATO A ESCENA 2
        // ---------------------------------------------------------------------
        Debug.Log("🏁 Fin Escena 1 (60s exactos): Cargando Escena 2...");
        if (Application.CanStreamedLevelBeLoaded("Escena 2"))
        {
            SceneManager.LoadScene("Escena 2");
        }
        else
        {
            int indexActual = SceneManager.GetActiveScene().buildIndex;
            if (indexActual + 1 < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(indexActual + 1);
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }
    }

    IEnumerator GeneradorContinuo()
    {
        while (permitirSpawns)
        {
            for (int i = listaNPCs.Count - 1; i >= 0; i--)
            {
                if (listaNPCs[i] == null)
                {
                    listaNPCs.RemoveAt(i);
                }
            }

            if (listaNPCs.Count < limiteMaximoNPCs)
            {
                SpawnearNPC(distanciaSpawn);
            }

            yield return new WaitForSeconds(tiempoEntreSpawns);
        }
    }

    void SpawnearNPC(float distancia)
    {
        if (npcPrefab == null) return;

        Vector3 posCentro = (jugadorVR != null) ? jugadorVR.position : Vector3.zero;

        float angulo = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 posSpawn = posCentro + new Vector3(Mathf.Cos(angulo) * distancia, 0, Mathf.Sin(angulo) * distancia);

        // Altura exacta de la calle tomada directamente del modelo original en la escena
        posSpawn.y = alturaCalleDetectada + offsetAlturaPiesNPC;

        GameObject nuevoNPC = Instantiate(npcPrefab, posSpawn, Quaternion.identity);

        // Asegurar escala exacta
        nuevoNPC.transform.localScale = npcPrefab.transform.localScale;

        // Poner NPC y todos sus hijos en Ignore Raycast
        nuevoNPC.layer = 2; // Ignore Raycast
        foreach (Transform child in nuevoNPC.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = 2;
        }

        // Desactivar colliders de los NPCs
        Collider[] colliders = nuevoNPC.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            c.enabled = false;
        }

        CaminanteMarcha caminante = nuevoNPC.GetComponent<CaminanteMarcha>();
        if (caminante == null)
        {
            caminante = nuevoNPC.AddComponent<CaminanteMarcha>();
        }

        float velCaminar = Random.Range(velocidadCaminarMin, velocidadCaminarMax);
        caminante.IniciarCaminataNormal(posCentro, velCaminar);

        listaNPCs.Add(caminante);
    }
}