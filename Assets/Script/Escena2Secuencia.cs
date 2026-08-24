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

    [Tooltip("Posición en el suelo donde emergerán las manos (opcional)")]
    public Transform puntoSpawnCharco;

    [Tooltip("Profundidad final a la que el personaje es tragado por la tierra")]
    public float profundidadHundimiento = 3.2f;

    [Tooltip("Ángulo de inclinación hacia abajo para ver el suelo y las manos")]
    public float anguloMirarAbajo = 65.0f;

    [Header("Audio")]
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
    private Renderer[] renderersCharco;
    private List<Transform> huesosManos = new List<Transform>();
    private List<Quaternion> rotacionesOriginales = new List<Quaternion>();
    private bool activarOndulacionTentaculos = false;

    private Light luzDireccional;
    private float intensidadLuzOriginal = 1f;
    private float alturaSueloReal = 0f;
    private Vector3 posInicialCharcoOculto;
    private Vector3 posFinalCharcoEmergido;

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

        // Registrar huesos para la ondulación tentacular
        if (charcoObjeto != null)
        {
            renderersCharco = charcoObjeto.GetComponentsInChildren<Renderer>(true);
            huesosManos.Clear();
            rotacionesOriginales.Clear();

            Transform[] todos = charcoObjeto.GetComponentsInChildren<Transform>(true);
            foreach (var t in todos)
            {
                if (t != charcoObjeto.transform)
                {
                    huesosManos.Add(t);
                    rotacionesOriginales.Add(t.localRotation);
                }
            }
        }

        // Ocultar el charco al inicio
        EstablecerVisibilidadCharco(false);

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

        // Timer oculto
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null) timer = gameObject.AddComponent<TimerVR>();
        timer.tiempoTotalSegundos = 60.0f;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        StartCoroutine(CronologiaEscena2());
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

    void Update()
    {
        // Movimiento sinuoso y ondulante como TENTÁCULOS vivos
        if (activarOndulacionTentaculos && huesosManos.Count > 0)
        {
            float tiempo = Time.time * 3.2f;
            for (int i = 0; i < huesosManos.Count; i++)
            {
                Transform h = huesosManos[i];
                if (h != null)
                {
                    float desfase = i * 0.55f;
                    float rotX = Mathf.Sin(tiempo + desfase) * 20f;
                    float rotZ = Mathf.Cos(tiempo * 0.85f + desfase * 1.3f) * 18f;
                    float rotY = Mathf.Sin(tiempo * 0.6f + desfase * 0.8f) * 14f;

                    h.localRotation = rotacionesOriginales[i] * Quaternion.Euler(rotX, rotY, rotZ);
                }
            }
        }
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 2 (60 SEGUNDOS)
    // 
    // 1. (0s - 30s): La escena empieza NORMAL (visión clara y limpia del bosque).
    // 2. (30s - 45s): Al segundo 30:
    //                 - La cámara mira hacia abajo suavemente hacia el suelo.
    //                 - Las manos van saliendo suavemente y se retuercen como tentáculos.
    //                 - ¡TODO SE VE CON TOTAL CLARIDAD Y LUZ!
    // 3. (45s - 55s): El personaje empieza a ser arrastrado y tragado por la tierra,
    //                 y la pantalla se va oscureciendo suavemente de a poco.
    // 4. (55s - 60s): El personaje termina de hundirse bajo tierra y la pantalla
    //                 llega al 100% de negro absoluto al segundo 60.
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
                    audioMusicaEstatica.volume = Mathf.Lerp(0.2f, 0.6f, tFase1 / 30.0f);
                }
                yield return null;
            }
        }
        else
        {
            Debug.Log("🧪 MODO PRUEBA ACTIVADO: Iniciando directamente al segundo 30...");
        }

        // ---------------------------------------------------------------------
        // FASE 2 (30s - 60s / 30 seg): CLÍMAX GRADUAL Y BIEN VISIBLE
        // ---------------------------------------------------------------------
        if (audioManosCharco != null && !audioManosCharco.isPlaying) audioManosCharco.Play();

        // 1. Calcular posición exacta del suelo frente a los pies del jugador
        alturaSueloReal = (xrOriginTransform != null) ? xrOriginTransform.position.y : (jugadorVR.position.y - 1.4f);
        Terrain terreno = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
        if (terreno != null)
        {
            alturaSueloReal = terreno.SampleHeight(jugadorVR.position) + terreno.transform.position.y;
        }

        Vector3 dirMirada = Vector3.ProjectOnPlane(jugadorVR.forward, Vector3.up).normalized;
        if (dirMirada == Vector3.zero) dirMirada = Vector3.forward;

        if (charcoObjeto != null)
        {
            posFinalCharcoEmergido = new Vector3(jugadorVR.position.x, alturaSueloReal, jugadorVR.position.z) + (dirMirada * 0.45f);
            posInicialCharcoOculto = posFinalCharcoEmergido - new Vector3(0f, 1.2f, 0f);

            charcoObjeto.transform.position = posInicialCharcoOculto;
            charcoObjeto.transform.rotation = Quaternion.LookRotation(dirMirada);
            charcoObjeto.transform.localScale = Vector3.one * 1.15f;

            EstablecerVisibilidadCharco(true);
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

            // A) Las manos van saliendo suavemente del suelo durante los primeros 12 segundos (30s - 42s)
            float factorSalida = Mathf.Clamp01(tClimax / 12.0f);
            float progresoSalida = Mathf.SmoothStep(0f, 1f, factorSalida);
            if (charcoObjeto != null)
            {
                charcoObjeto.transform.position = Vector3.Lerp(posInicialCharcoOculto, posFinalCharcoEmergido, progresoSalida);
            }

            // B) La cámara mira hacia abajo suavemente hacia las manos en el suelo (30s - 38s)
            float factorInclinacion = Mathf.Clamp01(tClimax / 8.0f);
            float inclinacion = Mathf.Lerp(0f, anguloMirarAbajo, Mathf.SmoothStep(0f, 1f, factorInclinacion));

            // C) Temblor angustioso que aumenta progresivamente
            float intensidadTemblor = Mathf.Lerp(0.012f, 0.11f, factor);
            float shakeX = (Mathf.PerlinNoise(Time.time * 32f, 0f) - 0.5f) * intensidadTemblor;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * 32f) - 0.5f) * (intensidadTemblor * 0.4f);
            float shakeZ = (Mathf.PerlinNoise(Time.time * 32f, Time.time * 32f) - 0.5f) * intensidadTemblor;
            float shakeRot = (Mathf.PerlinNoise(Time.time * 36f, 10f) - 0.5f) * (intensidadTemblor * 35f);

            // D) El personaje es arrastrado y tragado por la tierra (visible desde el segundo 42 al 60)
            float factorHundimiento = Mathf.Clamp01((tClimax - 12.0f) / 18.0f);
            float curvaHundimiento = Mathf.SmoothStep(0f, 1f, factorHundimiento);

            if (xrOriginTransform != null)
            {
                Vector3 posHundida = posOriginalXR - new Vector3(0f, curvaHundimiento * profundidadHundimiento, 0f);
                xrOriginTransform.position = posHundida + new Vector3(shakeX, shakeY, shakeZ);
                xrOriginTransform.localRotation = Quaternion.Euler(inclinacion + shakeRot, rotInicialY, shakeRot);
            }
            if (xrOriginTransform == jugadorVR && jugadorVR != null)
            {
                jugadorVR.localRotation = Quaternion.Euler(inclinacion + shakeRot, jugadorVR.localEulerAngles.y, shakeRot);
            }

            // E) OSCURECIMIENTO SUAVE Y MÁS DESPACIO:
            // - De 30s a 42s (tClimax 0 a 12): 0% negro (totalmente visible para apreciar las manos emergiendo y moviéndose).
            // - De 42s a 55s (tClimax 12 a 25): Se oscurece suave y lentamente de 0% a 50% para ver cómo el personaje es tragado por la tierra.
            // - De 55s a 60s (tClimax 25 a 30): Se completa el fundido al 100% de negro absoluto al quedar totalmente bajo tierra.
            float factorOscurecer = Mathf.Clamp01((tClimax - 12.0f) / 18.0f);
            float curvaOscuridad = Mathf.Pow(factorOscurecer, 1.8f); // Curva suave y retrasada

            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.overrideState = true;
                colorAdjustments.colorFilter.value = Color.Lerp(Color.white, Color.black, curvaOscuridad);

                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = Mathf.Lerp(0f, -14f, curvaOscuridad);
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
            colorAdjustments.postExposure.value = -18f;
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

        Debug.Log("🏁 Fin Escena 2 (60s): Jugador totalmente tragado en oscuridad absoluta.");
    }
}