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

    [Header("Efectos Visuales (Segundo 30 al 42)")]
    [Tooltip("Color rojo carmesí envolvente")]
    public Color colorRojoCarmesi = new Color(0.85f, 0.05f, 0.05f, 1.0f);

    [Header("Efecto de Párpados Somático (Final de Escena)")]
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

        // Setup inicial del Post-Processing: Escena normal y limpia
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
            audioAmbienteNormal.volume = 0.8f;
        }
        else if (audioAmbienteNormal != null && clipAmbienteNormal != null)
        {
            audioAmbienteNormal.clip = clipAmbienteNormal;
        }

        // 2. Configurar Audio de Ambiente Abrumador (FiltroAmbiente.wav - 30s a 60s)
        if (audioAmbienteAbrumador == null && clipAmbienteAbrumador != null)
        {
            audioAmbienteAbrumador = gameObject.AddComponent<AudioSource>();
            audioAmbienteAbrumador.clip = clipAmbienteAbrumador;
            audioAmbienteAbrumador.loop = true;
            audioAmbienteAbrumador.playOnAwake = false;
            audioAmbienteAbrumador.spatialBlend = 0f;
            audioAmbienteAbrumador.volume = 0f; // Empieza en 0 y sube al segundo 30
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

        // Iniciar reproducción del ambiente normal al arrancar
        if (audioAmbienteNormal != null && !audioAmbienteNormal.isPlaying)
        {
            audioAmbienteNormal.volume = 0.8f;
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

            // El ambiente normal se apaga mientras el abrumador y los latidos suben
            if (audioAmbienteNormal != null) audioAmbienteNormal.volume = Mathf.Lerp(0.8f, 0.05f, factor);
            if (audioAmbienteAbrumador != null) audioAmbienteAbrumador.volume = Mathf.Lerp(0.1f, 0.95f, factor);
            if (audioLatidos != null) audioLatidos.volume = Mathf.Lerp(0.2f, 1.0f, factor);
            if (audioMurmullos != null) audioMurmullos.volume = Mathf.Lerp(0.1f, 0.9f, factor);

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

        // Carrera libre antes del cierre de ojos
        float tiempoCarreraLibre = Mathf.Max(1.0f, 10.0f - duracionEfectoParpados);
        yield return new WaitForSeconds(tiempoCarreraLibre);

        // Efecto visual de pestañeo humano y cierre de ojos
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
        float inicioPesadez = duracion * 0.40f;

        while (t < duracion)
        {
            t += Time.deltaTime;

            if (t < inicioPesadez)
            {
                float factorPre = t / inicioPesadez;
                float microParpadeo = Mathf.Sin(t * 7.5f);
                float pesadezInicial = (microParpadeo > 0.82f) ? 0.22f : 0f;
                SetCierreParpados(Mathf.Lerp(0f, 0.25f, factorPre) + pesadezInicial);
            }
            else
            {
                float progresoCierre = Mathf.Clamp01((t - inicioPesadez) / (duracion - inicioPesadez));
                float curvaCierre = Mathf.SmoothStep(0.25f, 1.0f, progresoCierre);
                float luchaParpadeo = Mathf.Sin(t * 12.0f) * 0.08f * (1.0f - progresoCierre);
                SetCierreParpados(Mathf.Clamp01(curvaCierre + luchaParpadeo));
            }

            yield return null;
        }

        SetCierreParpados(1.0f);
    }

    IEnumerator GeneradorContinuo()
    {
        while (permitirSpawns)
        {
            listaNPCs.RemoveAll(item => item == null);

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

        Terrain terreno = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
        if (terreno != null)
        {
            posSpawn.y = terreno.SampleHeight(posSpawn) + terreno.transform.position.y;
        }
        else
        {
            posSpawn.y = posCentro.y;
        }

        GameObject nuevoNPC = Instantiate(npcPrefab, posSpawn, Quaternion.identity);

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