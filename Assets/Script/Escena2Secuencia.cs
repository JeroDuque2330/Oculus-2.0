using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Charco y Manos Tentaculares")]
    [Tooltip("GameObject o Prefab del modelo charco.fbx")]
    public GameObject charcoObjeto;

    [Tooltip("Cantidad de grupos de manos/charcos que emergerán alrededor del jugador")]
    [Range(1, 10)]
    public int cantidadCharcosManos = 5;

    [Tooltip("Radio del círculo en el suelo donde emergerán las manos alrededor del jugador (en metros)")]
    public float radioDistribucionManos = 0.65f;

    [Tooltip("Elevación extra de las manos sobre el suelo para que se vean más altas y amenazantes")]
    public float elevacionManos = 0.35f;

    [Tooltip("Posición en el suelo donde emergerán las manos (opcional)")]
    public Transform puntoSpawnCharco;

    [Tooltip("Profundidad final a la que el personaje es tragado por la tierra")]
    public float profundidadHundimiento = 3.5f;

    [Tooltip("Ángulo de inclinación hacia abajo para ver el suelo y las manos")]
    public float anguloMirarAbajo = 68.0f;

    [Header("Clips de Audio (Arrastra tus archivos de Audio .wav / .mp3 aquí)")]
    [Tooltip("Clip de audio para las manos emergiendo del charco (Audio Manos Charco.wav)")]
    public AudioClip clipManosCharco;

    [Tooltip("Clip de audio para la música y estática ambiental")]
    public AudioClip clipMusicaEstatica;

    [Header("AudioSources (Opcional - Se crean solos si no los asignas)")]
    [Tooltip("Música y estática ambiental que abruma")]
    public AudioSource audioMusicaEstatica;

    [Tooltip("Sonido de manos emergiendo y atrapando al jugador")]
    public AudioSource audioManosCharco;

    [Header("Efectos de Niebla / Partículas")]
    [Tooltip("Partículas o GameObject de la niebla")]
    public GameObject nieblaEspesa;

    [Header("PRUEBAS / DEBUGER (Para probar en el Editor)")]
    [Tooltip("¡ACTIVAR PARA PROBAR DE INMEDIATO! Salta directamente al segundo 30 al dar Play")]
    public bool probarFaseCharcoDeInmediato = false;

    // Componentes internos de Post-Processing
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;

    // Gestión de múltiples manos duplicadas
    private List<GameObject> listaInstanciasCharcos = new List<GameObject>();
    private List<Vector3> listaPosOcultas = new List<Vector3>();
    private List<Vector3> listaPosEmergidas = new List<Vector3>();
    private List<Renderer[]> listaRenderersCharcos = new List<Renderer[]>();

    private List<Transform> huesosManos = new List<Transform>();
    private List<Quaternion> rotacionesOriginales = new List<Quaternion>();
    private List<float> desfasesHuesos = new List<float>();
    private bool activarOndulacionTentaculos = false;

    private Light luzDireccional;
    private float intensidadLuzOriginal = 1f;
    private float alturaSueloReal = 0f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        tiempoTotalEscena = 60.0f;
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

        // Asegurar que la cámara tenga Post-Processing ACTIVADO en URP
        if (jugadorVR != null)
        {
            UniversalAdditionalCameraData cameraData = jugadorVR.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
            }
        }

        // Buscar la luz principal para atenuarla durante el oscurecimiento
        luzDireccional = FindFirstObjectByType<Light>();
        if (luzDireccional != null)
        {
            intensidadLuzOriginal = luzDireccional.intensity;
        }

        // Buscar el charco si no fue asignado
        if (charcoObjeto == null)
        {
            GameObject found = GameObject.Find("charco") ?? GameObject.Find("charco.fbx");
            if (found == null)
            {
                Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (var r in all)
                {
                    if (r.gameObject.name.ToLower().Contains("charco"))
                    {
                        found = r.gameObject;
                        break;
                    }
                }
            }
            charcoObjeto = found ?? this.gameObject;
        }

        // Ocultar el charco base al inicio
        if (charcoObjeto != null)
        {
            Renderer[] rBase = charcoObjeto.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rBase) if (r != null) r.enabled = false;
        }

        // Buscar volume si no fue asignado
        if (volumeAmbiente == null)
        {
            volumeAmbiente = FindFirstObjectByType<Volume>();
        }

        // Configurar Post-Processing completamente limpio al inicio
        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);

            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.overrideState = true;
                colorAdjustments.colorFilter.value = Color.white;
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = 0f;
            }
            if (vignette != null)
            {
                vignette.color.overrideState = true;
                vignette.color.value = Color.black;
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0f;
                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = 0.2f;
            }
        }

        // Configuración automática de AudioSources a partir de los clips
        if (audioManosCharco == null && clipManosCharco != null)
        {
            audioManosCharco = gameObject.AddComponent<AudioSource>();
            audioManosCharco.clip = clipManosCharco;
            audioManosCharco.loop = true;
            audioManosCharco.playOnAwake = false;
            audioManosCharco.spatialBlend = 0f;
            audioManosCharco.volume = 1f;
        }
        else if (audioManosCharco != null && clipManosCharco != null)
        {
            audioManosCharco.clip = clipManosCharco;
        }

        if (audioMusicaEstatica == null && clipMusicaEstatica != null)
        {
            audioMusicaEstatica = gameObject.AddComponent<AudioSource>();
            audioMusicaEstatica.clip = clipMusicaEstatica;
            audioMusicaEstatica.loop = true;
            audioMusicaEstatica.playOnAwake = false;
            audioMusicaEstatica.spatialBlend = 0f;
            audioMusicaEstatica.volume = 1.0f;
        }
        else if (audioMusicaEstatica != null && clipMusicaEstatica != null)
        {
            audioMusicaEstatica.clip = clipMusicaEstatica;
            audioMusicaEstatica.volume = 1.0f;
        }

        AudioListener.volume = 1.0f;

        // Timer oculto
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null) timer = gameObject.AddComponent<TimerVR>();
        timer.tiempoTotalSegundos = 60.0f;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        StartCoroutine(CronologiaEscena2());
    }

    private void EstablecerVisibilidadTodosCharcos(bool visible)
    {
        for (int i = 0; i < listaRenderersCharcos.Count; i++)
        {
            Renderer[] arr = listaRenderersCharcos[i];
            if (arr != null)
            {
                for (int j = 0; j < arr.Length; j++)
                {
                    if (arr[j] != null) arr[j].enabled = visible;
                }
            }
        }
    }

    void Update()
    {
        // Movimiento sinuoso y ondulante como TENTÁCULOS vivos en TODAS las manos duplicadas
        if (activarOndulacionTentaculos && huesosManos.Count > 0)
        {
            float tiempo = Time.time * 3.6f;
            for (int i = 0; i < huesosManos.Count; i++)
            {
                Transform h = huesosManos[i];
                if (h != null)
                {
                    float desfase = desfasesHuesos[i];
                    float rotX = Mathf.Sin(tiempo + desfase) * 24f;
                    float rotZ = Mathf.Cos(tiempo * 0.85f + desfase * 1.3f) * 20f;
                    float rotY = Mathf.Sin(tiempo * 0.6f + desfase * 0.8f) * 16f;

                    h.localRotation = rotacionesOriginales[i] * Quaternion.Euler(rotX, rotY, rotZ);
                }
            }
        }
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 2 (60 SEGUNDOS)
    // 
    // 1. (0s - 30s): La escena empieza NORMAL (visión clara y limpia del bosque).
    // 2. (30s - 42s): Al segundo 30:
    //                 - La cámara mira hacia abajo suavemente hacia el suelo.
    //                 - MÚLTIPLES MANOS emergen MÁS ALTAS alrededor del jugador
    //                   y se retuercen como tentáculos.
    // 3. (42s - 60s): LAS MANOS TRAGAN AL JUGADOR:
    //                 - Las manos se alzan y se cierran hacia el cuerpo/cámara del jugador.
    //                 - El jugador es arrastrado hacia abajo hundiéndose en la tierra.
    //                 - MIENTRAS LO TRAGAN, la cámara se va oscureciendo suavemente
    //                   hasta llegar al 100% de negro absoluto al segundo 60.
    // 4. (60s): Oscuridad total y fin de la experiencia.
    // =========================================================================
    IEnumerator CronologiaEscena2()
    {
        // ---------------------------------------------------------------------
        // FASE 1 (0s - 30s / 30 seg): ESCENA TOTALMENTE NORMAL Y LIMPIA
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null && !audioMusicaEstatica.isPlaying) audioMusicaEstatica.Play();
        if (nieblaEspesa != null) nieblaEspesa.SetActive(true);

        if (!probarFaseCharcoDeInmediato)
        {
            float tFase1 = 0f;
            while (tFase1 < 30.0f)
            {
                tFase1 += Time.deltaTime;
                if (audioMusicaEstatica != null)
                {
                    audioMusicaEstatica.volume = Mathf.Lerp(0.5f, 1.0f, tFase1 / 30.0f);
                }
                yield return null;
            }
        }
        else
        {
            Debug.Log("🧪 MODO PRUEBA ACTIVADO: Iniciando directamente al segundo 30...");
        }

        // ---------------------------------------------------------------------
        // FASE 2 (30s - 60s / 30 seg): CLÍMAX - MANOS ALTAS, TRAGADO Y OSCURECIMIENTO
        // ---------------------------------------------------------------------
        if (audioManosCharco != null && !audioManosCharco.isPlaying) audioManosCharco.Play();

        // 1. Calcular posición exacta del suelo frente al jugador
        alturaSueloReal = (xrOriginTransform != null) ? xrOriginTransform.position.y : (jugadorVR.position.y - 1.4f);
        Terrain terreno = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
        if (terreno != null)
        {
            alturaSueloReal = terreno.SampleHeight(jugadorVR.position) + terreno.transform.position.y;
        }

        Vector3 posCentroJugador = new Vector3(jugadorVR.position.x, alturaSueloReal, jugadorVR.position.z);
        Vector3 dirMirada = Vector3.ProjectOnPlane(jugadorVR.forward, Vector3.up).normalized;
        if (dirMirada == Vector3.zero) dirMirada = Vector3.forward;

        // 2. CREAR Y DISTRIBUIR MÚLTIPLES CHARCOS / MANOS MÁS ALTAS ALREDEDOR DEL JUGADOR
        listaInstanciasCharcos.Clear();
        listaPosOcultas.Clear();
        listaPosEmergidas.Clear();
        listaRenderersCharcos.Clear();
        huesosManos.Clear();
        rotacionesOriginales.Clear();
        desfasesHuesos.Clear();

        if (charcoObjeto != null)
        {
            int total = Mathf.Max(1, cantidadCharcosManos);

            for (int i = 0; i < total; i++)
            {
                GameObject instancia;
                if (i == 0)
                {
                    instancia = charcoObjeto;
                }
                else
                {
                    instancia = Instantiate(charcoObjeto);
                    instancia.name = "charco_duplicado_" + i;
                }

                // Distribuir en arco y círculo alrededor de los pies del jugador
                float anguloPaso;
                float distanciaR;
                if (i == 0)
                {
                    anguloPaso = 0f; // Justo enfrente
                    distanciaR = 0.45f;
                }
                else
                {
                    // Distribuir alrededor en 360° con variaciones
                    float anguloOffset = (360f / (total - 1)) * (i - 1) + Random.Range(-15f, 15f);
                    anguloPaso = anguloOffset;
                    distanciaR = Random.Range(radioDistribucionManos * 0.75f, radioDistribucionManos * 1.25f);
                }

                Quaternion rotOffset = Quaternion.Euler(0f, anguloPaso, 0f);
                Vector3 dirOffset = rotOffset * dirMirada;
                Vector3 posEmergida = posCentroJugador + (dirOffset * distanciaR);

                float ySuelo = (terreno != null) ? (terreno.SampleHeight(posEmergida) + terreno.transform.position.y) : alturaSueloReal;
                posEmergida.y = ySuelo + elevacionManos; // Más altas sobre el suelo

                Vector3 posOculta = posEmergida - new Vector3(0f, 1.4f, 0f);

                // Orientar cada grupo de manos mirando hacia el centro/jugador
                Vector3 mirarAlCentro = (posCentroJugador - posEmergida).normalized;
                if (mirarAlCentro == Vector3.zero) mirarAlCentro = dirMirada;

                instancia.transform.position = posOculta;
                instancia.transform.rotation = Quaternion.LookRotation(mirarAlCentro);
                instancia.transform.localScale = Vector3.one * Random.Range(1.2f, 1.5f); // Manos más grandes y amenazantes

                listaInstanciasCharcos.Add(instancia);
                listaPosOcultas.Add(posOculta);
                listaPosEmergidas.Add(posEmergida);

                Renderer[] renderers = instancia.GetComponentsInChildren<Renderer>(true);
                listaRenderersCharcos.Add(renderers);

                // Registrar todos los huesos de esta instancia de manos para la ondulación tentacular
                Transform[] transformsInstancia = instancia.GetComponentsInChildren<Transform>(true);
                foreach (var t in transformsInstancia)
                {
                    if (t != instancia.transform)
                    {
                        huesosManos.Add(t);
                        rotacionesOriginales.Add(t.localRotation);
                        desfasesHuesos.Add(Random.Range(0f, 10f));
                    }
                }
            }

            // Hacer visibles todos los charcos y activar movimiento orgánico
            EstablecerVisibilidadTodosCharcos(true);
            activarOndulacionTentaculos = true;
        }

        Vector3 posOriginalXR = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;
        float rotInicialY = (xrOriginTransform != null) ? xrOriginTransform.localEulerAngles.y : 0f;

        float duracionClimax = 30.0f; // Del segundo 30 al 60 (30 segundos completos)
        float tClimax = 0f;

        while (tClimax < duracionClimax)
        {
            tClimax += Time.deltaTime;
            float factor = Mathf.Clamp01(tClimax / duracionClimax);

            // A) EMERGER (30s - 42s / tClimax: 0 - 12): Las manos emergen altas de la tierra
            float factorSalida = Mathf.Clamp01(tClimax / 12.0f);
            float progresoSalida = Mathf.SmoothStep(0f, 1f, factorSalida);

            // B) TRAGAR AL JUGADOR (42s - 60s / tClimax: 12 - 30):
            // Las manos se alzan y se cierran hacia el jugador mientras éste es jalado hacia abajo
            float factorTragado = Mathf.Clamp01((tClimax - 12.0f) / 18.0f);
            float curvaTragado = Mathf.SmoothStep(0f, 1f, factorTragado);

            for (int i = 0; i < listaInstanciasCharcos.Count; i++)
            {
                GameObject inst = listaInstanciasCharcos[i];
                if (inst != null)
                {
                    Vector3 posBase = Vector3.Lerp(listaPosOcultas[i], listaPosEmergidas[i], progresoSalida);
                    // Cuando empieza el tragado, las manos se alzan hacia arriba y se cierran hacia el centro (envolviendo al jugador)
                    Vector3 haciaCentro = (posCentroJugador - listaPosEmergidas[i]) * 0.45f;
                    Vector3 alcanceArriba = new Vector3(0f, 0.55f * curvaTragado, 0f);

                    inst.transform.position = posBase + (haciaCentro * curvaTragado) + alcanceArriba;
                }
            }

            // C) La cámara mira hacia abajo hacia las manos en el suelo (30s - 38s)
            float factorInclinacion = Mathf.Clamp01(tClimax / 8.0f);
            float inclinacion = Mathf.Lerp(0f, anguloMirarAbajo, Mathf.SmoothStep(0f, 1f, factorInclinacion));

            // D) Temblor angustioso que aumenta a medida que lo tragan
            float intensidadTemblor = Mathf.Lerp(0.015f, 0.13f, factor);
            float shakeX = (Mathf.PerlinNoise(Time.time * 35f, 0f) - 0.5f) * intensidadTemblor;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * 35f) - 0.5f) * (intensidadTemblor * 0.5f);
            float shakeZ = (Mathf.PerlinNoise(Time.time * 35f, Time.time * 35f) - 0.5f) * intensidadTemblor;
            float shakeRot = (Mathf.PerlinNoise(Time.time * 38f, 10f) - 0.5f) * (intensidadTemblor * 38f);

            // E) El jugador es jalado y tragado bajo la tierra
            if (xrOriginTransform != null)
            {
                Vector3 posHundida = posOriginalXR - new Vector3(0f, curvaTragado * profundidadHundimiento, 0f);
                xrOriginTransform.position = posHundida + new Vector3(shakeX, shakeY, shakeZ);
                xrOriginTransform.localRotation = Quaternion.Euler(inclinacion + shakeRot, rotInicialY, shakeRot);
            }
            if (xrOriginTransform == jugadorVR && jugadorVR != null)
            {
                jugadorVR.localRotation = Quaternion.Euler(inclinacion + shakeRot, jugadorVR.localEulerAngles.y, shakeRot);
            }

            // F) PRIMERO LO JALA (40s - 52s / 100% VISIBLE) Y DESPUÉS SE OSCURECE (52s - 60s):
            // - De 30s a 52s (tClimax 0 a 22): 0% oscuridad (Visión 100% clara para ver el jalado y las manos envolviéndolo).
            // - De 52s a 60s (tClimax 22 a 30): La pantalla se va oscureciendo de a poco hasta quedar en 100% negro total.
            float factorOscurecer = Mathf.Clamp01((tClimax - 22.0f) / 8.0f);
            float curvaOscuridad = Mathf.SmoothStep(0f, 1f, factorOscurecer);

            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.overrideState = true;
                colorAdjustments.colorFilter.value = Color.Lerp(Color.white, Color.black, curvaOscuridad);

                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = Mathf.Lerp(0f, -18f, curvaOscuridad);
            }
            if (vignette != null)
            {
                vignette.color.overrideState = true;
                vignette.color.value = Color.black;

                vignette.intensity.overrideState = true;
                vignette.intensity.value = Mathf.Lerp(0f, 1.0f, curvaOscuridad);

                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = Mathf.Lerp(0.2f, 1.0f, curvaOscuridad);
            }
            if (luzDireccional != null)
            {
                luzDireccional.intensity = Mathf.Lerp(intensidadLuzOriginal, 0f, curvaOscuridad);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // SEGUNDO 60: OSCURIDAD TOTAL Y SILENCIO ABSOLUTO (FIN DE LA EXPERIENCIA)
        // ---------------------------------------------------------------------
        if (colorAdjustments != null)
        {
            colorAdjustments.colorFilter.value = Color.black;
            colorAdjustments.postExposure.value = -20f;
        }
        if (vignette != null)
        {
            vignette.intensity.value = 1.0f;
            vignette.smoothness.value = 1.0f;
        }
        if (luzDireccional != null)
        {
            luzDireccional.intensity = 0f;
        }

        if (audioMusicaEstatica != null) audioMusicaEstatica.Stop();
        if (audioManosCharco != null) audioManosCharco.Stop();

        Debug.Log("🏁 Fin Escena 2 (60s): Jugador totalmente tragado por las manos en oscuridad absoluta.");
    }
}