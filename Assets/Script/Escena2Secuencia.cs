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

    [Tooltip("Duración de la fase final donde el charco consume al jugador (20 segundos: del segundo 40 al 60)")]
    public float duracionFaseCharco = 20.0f;

    [Header("Charco y Manos (Fase Final: 40s - 60s)")]
    [Tooltip("GameObject o Prefab del modelo charco.fbx")]
    public GameObject charcoObjeto;

    [Tooltip("Posición en el suelo donde emergerán las manos. Si está vacío, se coloca automáticamente en el suelo bajo el jugador")]
    public Transform puntoSpawnCharco;

    [Tooltip("Controlador de animación para el charco")]
    public RuntimeAnimatorController controladorCharco;

    [Tooltip("Profundidad en metros a la que el jugador se hundirá en el suelo")]
    public float profundidadHundimiento = 3.0f;

    [Tooltip("Ángulo de inclinación forzada hacia abajo para mirar el charco")]
    public float anguloMirarAbajo = 70.0f;

    [Tooltip("Activar temblor angustioso de cámara")]
    public bool aplicarTemblorCamara = true;

    [Header("Audio")]
    [Tooltip("Música y estática ambiental que abruma")]
    public AudioSource audioMusicaEstatica;

    [Tooltip("Sonido de manos emergiendo y atrapando al jugador")]
    public AudioSource audioManosCharco;

    [Header("Efectos de Niebla / Partículas")]
    [Tooltip("Partículas o GameObject de la niebla espesa")]
    public GameObject nieblaEspesa;

    [Header("PRUEBAS / DEBUGER (Para probar en el Editor)")]
    [Tooltip("¡ACTIVAR PARA PROBAR DE INMEDIATO! Salta directamente a las manos, temblor y hundimiento")]
    public bool probarFaseCharcoDeInmediato = false;

    // Componentes internos y de Post-Processing
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private FilmGrain filmGrain;
    private Renderer[] renderersCharco;
    private Animator animatorCharco;
    private Material materialBlackout;
    private GameObject quadBlackout;
    private List<Transform> huesosManos = new List<Transform>();
    private List<Quaternion> rotacionesOriginalesHuesos = new List<Quaternion>();
    private bool animarManosProcedural = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        tiempoTotalEscena = 60.0f;
        duracionFaseCharco = 20.0f;
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

        // Buscar automáticamente el charco si no fue asignado
        if (charcoObjeto == null)
        {
            GameObject foundCharco = GameObject.Find("charco");
            if (foundCharco == null) foundCharco = GameObject.Find("charco.fbx");
            if (foundCharco == null)
            {
                Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (var r in allRenderers)
                {
                    if (r.gameObject.name.ToLower().Contains("charco"))
                    {
                        foundCharco = r.gameObject;
                        break;
                    }
                }
            }
            charcoObjeto = foundCharco ?? this.gameObject;
        }

        // Preparar charco, animator y huesos
        if (charcoObjeto != null)
        {
            renderersCharco = charcoObjeto.GetComponentsInChildren<Renderer>(true);
            animatorCharco = charcoObjeto.GetComponentInChildren<Animator>(true);
            if (animatorCharco == null)
            {
                animatorCharco = charcoObjeto.AddComponent<Animator>();
            }

            // Asignar el Charco_Controller si no tiene uno
            if (controladorCharco == null)
            {
                controladorCharco = Resources.Load<RuntimeAnimatorController>("Charco_Controller");
            }
            if (controladorCharco != null && animatorCharco != null)
            {
                animatorCharco.runtimeAnimatorController = controladorCharco;
            }

            // Registrar todos los huesos / partes de las manos para movimiento continuo y gesticulación
            huesosManos.Clear();
            rotacionesOriginalesHuesos.Clear();
            Transform[] todosTransforms = charcoObjeto.GetComponentsInChildren<Transform>(true);
            foreach (var t in todosTransforms)
            {
                if (t != charcoObjeto.transform)
                {
                    huesosManos.Add(t);
                    rotacionesOriginalesHuesos.Add(t.localRotation);
                }
            }
        }

        EstablecerVisibilidadCharco(false);

        // Crear pantalla negra física de bloqueo (Quad en lente de cámara)
        CrearBlackoutQuad();

        // Configurar TimerVR en 60 segundos y oculto
        TimerVR timer = FindFirstObjectByType<TimerVR>();
        if (timer == null)
        {
            timer = gameObject.AddComponent<TimerVR>();
        }
        timer.tiempoTotalSegundos = 60.0f;
        timer.mostrarHUD = false;
        timer.OcultarHUD();

        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);
            volumeAmbiente.profile.TryGet(out filmGrain);

            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.colorFilter.overrideState = true;
                colorAdjustments.colorFilter.value = Color.white;
                colorAdjustments.postExposure.value = 0f;
            }
        }

        StartCoroutine(CronologiaEscena2());
    }

    private void CrearBlackoutQuad()
    {
        if (jugadorVR == null) return;

        quadBlackout = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadBlackout.name = "VR_Blackout_Screen";
        Destroy(quadBlackout.GetComponent<Collider>());

        quadBlackout.transform.SetParent(jugadorVR, false);
        quadBlackout.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        quadBlackout.transform.localRotation = Quaternion.identity;
        quadBlackout.transform.localScale = new Vector3(3f, 3f, 3f);

        // Crear material negro absoluto con máxima prioridad de renderizado
        Shader shaderUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        materialBlackout = new Material(shaderUnlit);
        materialBlackout.color = new Color(0f, 0f, 0f, 1f);

        // Configuración de transparencia
        materialBlackout.SetFloat("_Surface", 1f); // Transparent en URP
        materialBlackout.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        materialBlackout.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        materialBlackout.SetInt("_ZWrite", 0);
        materialBlackout.renderQueue = 5000; // Por encima de todo

        MeshRenderer mr = quadBlackout.GetComponent<MeshRenderer>();
        mr.material = materialBlackout;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void SetBlackoutAlpha(float alpha)
    {
        float a = Mathf.Clamp01(alpha);

        if (materialBlackout != null)
        {
            materialBlackout.color = new Color(0f, 0f, 0f, a);
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = Mathf.Lerp(0f, -20f, a);
            colorAdjustments.colorFilter.overrideState = true;
            colorAdjustments.colorFilter.value = Color.Lerp(Color.white, Color.black, a);
        }

        if (vignette != null)
        {
            vignette.color.value = Color.black;
            vignette.intensity.value = Mathf.Lerp(0f, 1f, a);
        }
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
        // Gesticulación y movimiento sinuoso de las manos al emerger
        if (animarManosProcedural && huesosManos.Count > 0)
        {
            float tiempo = Time.time * 4.5f;
            for (int i = 0; i < huesosManos.Count; i++)
            {
                Transform h = huesosManos[i];
                if (h != null)
                {
                    float desfase = i * 0.4f;
                    float rotacionX = Mathf.Sin(tiempo + desfase) * 12f;
                    float rotacionZ = Mathf.Cos(tiempo + desfase * 1.3f) * 10f;
                    float rotacionY = Mathf.Sin(tiempo * 0.7f + desfase) * 8f;

                    h.localRotation = rotacionesOriginalesHuesos[i] * Quaternion.Euler(rotacionX, rotacionY, rotacionZ);
                }
            }
        }
    }

    // =========================================================================
    // CRONOLOGÍA EXACTA ESCENA 2 (Total: 60 seg / 1:00 min)
    // 
    // 1. (0s - 10s): Levantarse (Fade-in suave desde negro y elevación de cámara).
    // 2. (10s - 40s): Exploración, estática y música ambiental creciente en la niebla.
    // 3. (40s - 60s): ¡CLÍMAX! Surgen las manos en el suelo a los pies del jugador,
    //                 se mueven vivas, la cámara mira abajo, tiembla, se hunde
    //                 y la pantalla se torna 100% negra en Game View y VR.
    // 4. (60s): Oscuridad 100% total y fin de la experiencia.
    // =========================================================================
    IEnumerator CronologiaEscena2()
    {
        // MODO PRUEBA: Ir directamente a las manos, temblor y hundimiento
        if (probarFaseCharcoDeInmediato)
        {
            Debug.Log("🧪 MODO PRUEBA ACTIVADO: Iniciando fase de manos y hundimiento de inmediato...");
            yield return StartCoroutine(EjecutarFaseCharcoYConsumo());
            yield break;
        }

        // ---------------------------------------------------------------------
        // FASE 1 (0s - 10s / 10 seg): LEVANTARSE (Fade-in y posición de pie)
        // ---------------------------------------------------------------------
        SetBlackoutAlpha(1.0f);

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

            // Fade-in a visión clara
            SetBlackoutAlpha(1.0f - factor);

            if (xrOriginTransform != null)
            {
                xrOriginTransform.position = Vector3.Lerp(posicionOriginalXR - new Vector3(0, 0.7f, 0), posicionOriginalXR, factor);
            }

            yield return null;
        }

        SetBlackoutAlpha(0.0f);

        // ---------------------------------------------------------------------
        // FASE 2 (10s - 40s / 30 seg): EXPLORACIÓN, ESTÁTICA Y NIEBLA
        // ---------------------------------------------------------------------
        if (audioMusicaEstatica != null && !audioMusicaEstatica.isPlaying) audioMusicaEstatica.Play();
        if (nieblaEspesa != null) nieblaEspesa.SetActive(true);

        float duracionFase2 = 30.0f; // 30 segundos (del segundo 10 al 40)
        float tFase2 = 0f;

        while (tFase2 < duracionFase2)
        {
            tFase2 += Time.deltaTime;
            float factorFase2 = Mathf.Clamp01(tFase2 / duracionFase2);

            if (audioMusicaEstatica != null)
            {
                audioMusicaEstatica.volume = Mathf.Lerp(0.15f, 0.85f, factorFase2);
            }

            yield return null;
        }

        // ---------------------------------------------------------------------
        // FASE 3 (40s - 60s / 20 seg): LAS MANOS SURGEN EN EL SUELO, CÁMARA MIRA ABAJO,
        // TIEMBLA, ES ARRASTRADO HACIA ABAJO Y PANTALLA SE TORNA NEGRA DEL TODO
        // ---------------------------------------------------------------------
        yield return StartCoroutine(EjecutarFaseCharcoYConsumo());
    }

    IEnumerator EjecutarFaseCharcoYConsumo()
    {
        // 1. Calcular la altura REAL del suelo en la posición del jugador
        if (charcoObjeto != null)
        {
            float alturaSuelo = (xrOriginTransform != null) ? xrOriginTransform.position.y : (jugadorVR.position.y - 1.4f);

            Terrain terreno = Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>();
            if (terreno != null)
            {
                alturaSuelo = terreno.SampleHeight(jugadorVR.position) + terreno.transform.position.y;
            }

            Vector3 dirMirada = Vector3.ProjectOnPlane(jugadorVR.forward, Vector3.up).normalized;
            if (dirMirada == Vector3.zero) dirMirada = Vector3.forward;

            if (puntoSpawnCharco != null)
            {
                charcoObjeto.transform.position = puntoSpawnCharco.position;
                charcoObjeto.transform.rotation = puntoSpawnCharco.rotation;
            }
            else
            {
                // Colocar el charco en el suelo justo a los pies del jugador
                Vector3 posCharco = new Vector3(jugadorVR.position.x, alturaSuelo, jugadorVR.position.z) + (dirMirada * 0.45f);
                charcoObjeto.transform.position = posCharco;
                charcoObjeto.transform.rotation = Quaternion.LookRotation(dirMirada);
                charcoObjeto.transform.localScale = Vector3.one * 1.2f;
            }

            // Hacer visible el charco
            EstablecerVisibilidadCharco(true);

            // Activar movimiento y animación de las manos
            animarManosProcedural = true;

            if (animatorCharco != null)
            {
                animatorCharco.enabled = true;
                if (animatorCharco.runtimeAnimatorController != null)
                {
                    animatorCharco.Play(0, -1, 0f);
                }
            }

            Animation legacyAnim = charcoObjeto.GetComponentInChildren<Animation>(true);
            if (legacyAnim != null)
            {
                legacyAnim.enabled = true;
                legacyAnim.Play();
            }
        }

        // Sonido de manos emergiendo y atrapando
        if (audioManosCharco != null) audioManosCharco.Play();

        // 2. Transición del clímax: Arrastre hacia abajo, cámara mira abajo, temblor y oscurecimiento a negro total
        float tFase3 = 0f;
        Vector3 posInicialArrastre = (xrOriginTransform != null) ? xrOriginTransform.position : Vector3.zero;
        float rotInicialY = (xrOriginTransform != null) ? xrOriginTransform.localEulerAngles.y : 0f;

        while (tFase3 < duracionFaseCharco)
        {
            tFase3 += Time.deltaTime;
            float factor = Mathf.Clamp01(tFase3 / duracionFaseCharco);

            // Curva suave de arrastre hacia el fondo
            float progresoHundimiento = Mathf.SmoothStep(0f, 1f, factor);

            // Temblor angustioso que aumenta a medida que lo arrastran
            float intensidadTemblor = aplicarTemblorCamara ? Mathf.Lerp(0.02f, 0.09f, factor) : 0f;
            float temblorPosX = (Mathf.PerlinNoise(Time.time * 36f, 0f) - 0.5f) * intensidadTemblor;
            float temblorPosY = (Mathf.PerlinNoise(0f, Time.time * 36f) - 0.5f) * (intensidadTemblor * 0.5f);
            float temblorPosZ = (Mathf.PerlinNoise(Time.time * 36f, Time.time * 36f) - 0.5f) * intensidadTemblor;

            float temblorRotX = (Mathf.PerlinNoise(Time.time * 42f, 15f) - 0.5f) * (intensidadTemblor * 45f);
            float temblorRotZ = (Mathf.PerlinNoise(15f, Time.time * 42f) - 0.5f) * (intensidadTemblor * 45f);

            // A) ARRASTRAR HACIA ABAJO (Hundimiento en el suelo)
            if (xrOriginTransform != null)
            {
                Vector3 posDescenso = posInicialArrastre - new Vector3(0f, progresoHundimiento * profundidadHundimiento, 0f);
                xrOriginTransform.position = posDescenso + new Vector3(temblorPosX, temblorPosY, temblorPosZ);

                // B) LA CÁMARA MIRA HACIA ABAJO (Inclinación forzada hacia las manos en el suelo)
                float inclinacionX = Mathf.Lerp(0f, anguloMirarAbajo, Mathf.SmoothStep(0f, 1f, factor * 2.0f));
                xrOriginTransform.localRotation = Quaternion.Euler(inclinacionX + temblorRotX, rotInicialY, temblorRotZ);
            }

            if (xrOriginTransform == jugadorVR && jugadorVR != null)
            {
                float inclinacionX = Mathf.Lerp(0f, anguloMirarAbajo, Mathf.SmoothStep(0f, 1f, factor * 2.0f));
                jugadorVR.localRotation = Quaternion.Euler(inclinacionX + temblorRotX, jugadorVR.localEulerAngles.y, temblorRotZ);
            }

            // C) LA PANTALLA SE VA TORNANDO NEGRA DEL TODO EN LA CÁMARA GAME
            SetBlackoutAlpha(factor);

            yield return null;
        }

        // ---------------------------------------------------------------------
        // CIERRE ABRUPTO: Oscuridad 100% total y silencio absoluto (Muerte)
        // ---------------------------------------------------------------------
        SetBlackoutAlpha(1.0f); // 100% NEGRO TOTAL GARANTIZADO

        if (audioMusicaEstatica != null) audioMusicaEstatica.Stop();
        if (audioManosCharco != null) audioManosCharco.Stop();

        Debug.Log("🏁 Fin Escena 2 (60s): Jugador totalmente consumido bajo el suelo en oscuridad absoluta.");
    }
}