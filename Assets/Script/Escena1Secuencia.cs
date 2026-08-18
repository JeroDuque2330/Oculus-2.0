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

    [Tooltip("El Prefab del NPC con la animación de caminar")]
    public GameObject npcPrefab;

    [Tooltip("El objeto Global Volume con el Post-Processing")]
    public Volume volumeAmbiente;

    [Header("Audio (Opcional)")]
    [Tooltip("Sonido de vidrio rompiéndose al segundo 45")]
    public AudioSource audioVidrioRoto;

    [Tooltip("Zumbido agudo que sube de volumen durante los 45s")]
    public AudioSource audioZumbido;

    [Tooltip("Audio ambiente de la ciudad (al que se le aplicará LowPass)")]
    public AudioLowPassFilter filtroAmbiente;

    [Header("Configuración de Multitud")]
    public float distanciaSpawn = 18.0f;
    public float cercaniaAlJugador = 1.2f;
    public float velocidadMin = 0.75f;
    public float velocidadMax = 0.95f;
    public float tiempoEntreSpawns = 0.15f;
    public int multitudInicial = 50;

    [Header("Colores del Filtro Rojo")]
    public Color colorRojoFinal = new Color(1.0f, 0.45f, 0.45f); // Rojo progresivo

    [Header("Transición Final")]
    [Tooltip("Nombre de la Escena 2 para cargar al segundo 60")]
    public string nombreEscena2 = "Escena 2";

    // Componentes internos de Post-Processing
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    
    // Lista para rastrear a todos los NPCs en escena
    public static List<CaminanteMarcha> listaNPCs = new List<CaminanteMarcha>();
    private bool permitirSpawns = true;

    void Start()
    {
        listaNPCs.Clear();

        if (jugadorVR == null && Camera.main != null)
        {
            jugadorVR = Camera.main.transform;
        }

        // 1. Setup Inicial (t = 0s): Estado limpio y normal
        if (volumeAmbiente != null && volumeAmbiente.profile != null)
        {
            volumeAmbiente.profile.TryGet(out colorAdjustments);
            volumeAmbiente.profile.TryGet(out vignette);

            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.white;
            if (vignette != null)
            {
                vignette.color.value = Color.red;
                vignette.intensity.value = 0f;
            }
        }

        // Iniciar multitud desde el segundo 0 con total indiferencia
        for (int i = 0; i < multitudInicial; i++)
        {
            SpawnearNPC(Random.Range(2.0f, distanciaSpawn));
        }

        StartCoroutine(GeneradorContinuo());
        StartCoroutine(TimelineEscena1());
    }

    // =========================================================================
    // TIMELINE EXACTO DE LA ESCENA 1 (0s - 60s)
    // =========================================================================
    IEnumerator TimelineEscena1()
    {
        // ---------------------------------------------------------------------
        // ESTADO A: Acumulación de estática (0s - 45s)
        // ---------------------------------------------------------------------

        // Tramo 1: t=0s a 20s (Cobertura 0% -> 20%) Lento e imperceptible
        yield return StartCoroutine(ProgresionEstatica(0.0f, 0.20f, 20.0f, 22000f, 12000f, 0.0f, 0.3f));

        // Tramo 2: t=20s a 35s (Cobertura 20% -> 60%) Aceleración media perturbadora
        yield return StartCoroutine(ProgresionEstatica(0.20f, 0.60f, 15.0f, 12000f, 4000f, 0.3f, 0.7f));

        // Tramo 3: t=35s a 45s (Cobertura 60% -> 100%) Aceleración final / pánico visual
        yield return StartCoroutine(ProgresionEstatica(0.60f, 1.0f, 10.0f, 4000f, 800f, 0.7f, 1.0f));

        // ---------------------------------------------------------------------
        // TRIGGER DE QUIEBRE (t = 45s exacto)
        // ---------------------------------------------------------------------
        permitirSpawns = false; // Ya no nacen más caminantes

        // Disparo sonoro estridente de vidrio rompiéndose
        if (audioVidrioRoto != null)
        {
            audioVidrioRoto.Play();
        }

        // Corte inmediato (sin fade) del overlay: visión recuperada al instante
        if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.white;
        if (vignette != null) vignette.intensity.value = 0f;
        if (audioZumbido != null) audioZumbido.Stop();

        // ---------------------------------------------------------------------
        // ESTADO B: Retorno abrupto y giro de la multitud (45s - 60s)
        // ---------------------------------------------------------------------

        // t=45s a 47s (2s): Silencio casi total y pausa de tensión
        // Congelar marcha de indiferencia de todos los NPCs
        foreach (var npc in listaNPCs)
        {
            if (npc != null) npc.PausarMarcha();
        }
        yield return new WaitForSeconds(2.0f);

        // t=47s a 52s (5s): Giro escalonado de la multitud hacia el usuario
        foreach (var npc in listaNPCs)
        {
            if (npc != null)
            {
                float retrasoOleada = Random.Range(0.0f, 2.5f); // Oleadas de 0.2s - 0.4s
                StartCoroutine(npc.GirarHaciaJugador(jugadorVR, retrasoOleada));
            }
        }
        yield return new WaitForSeconds(5.0f);

        // t=52s a 60s (8s): Acercamiento de las figuras cubriendo el campo visual
        foreach (var npc in listaNPCs)
        {
            if (npc != null)
            {
                npc.AvanzarHaciaJugador(jugadorVR, 1.8f);
            }
        }

        // Oscurecimiento final al acercarse (t=57s a 60s)
        float tiempoFinal = 0f;
        while (tiempoFinal < 8.0f)
        {
            tiempoFinal += Time.deltaTime;
            if (tiempoFinal > 5.0f && vignette != null)
            {
                // Fundido a negro envolvente
                vignette.color.value = Color.black;
                vignette.intensity.value = Mathf.Lerp(0f, 1f, (tiempoFinal - 5.0f) / 3.0f);
            }
            yield return null;
        }

        // Transición directa a la Escena 2
        if (Application.CanStreamedLevelBeLoaded(nombreEscena2))
        {
            SceneManager.LoadScene(nombreEscena2);
        }
        else
        {
            Debug.Log("🏁 Fin de la Escena 1 (Transición a Escena 2)");
        }
    }

    // Corrutina auxiliar para la curva de estática y audio
    IEnumerator ProgresionEstatica(float coberturaInicio, float coberturaFin, float duracion, float cutoffInicio, float cutoffFin, float volZumbidoInicio, float volZumbidoFin)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float factor = Mathf.Clamp01(t / duracion);
            float coberturaActual = Mathf.Lerp(coberturaInicio, coberturaFin, factor);

            // Ajuste visual progresivo en el casco VR
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.value = Color.Lerp(Color.white, colorRojoFinal, coberturaActual);
            }
            if (vignette != null)
            {
                vignette.color.value = Color.red;
                vignette.intensity.value = Mathf.Lerp(0f, 0.75f, coberturaActual);
            }

            // Filtro pasa-bajos de la ciudad
            if (filtroAmbiente != null)
            {
                filtroAmbiente.cutoffFrequency = Mathf.Lerp(cutoffInicio, cutoffFin, factor);
            }

            // Zumbido creciente
            if (audioZumbido != null)
            {
                if (!audioZumbido.isPlaying) audioZumbido.Play();
                audioZumbido.volume = Mathf.Lerp(volZumbidoInicio, volZumbidoFin, factor);
            }

            yield return null;
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

        CaminanteMarcha caminante = nuevoNPC.AddComponent<CaminanteMarcha>();
        float velocidad = Random.Range(velocidadMin, velocidadMax);
        caminante.Iniciar(puntoDestino, velocidad);
    }
}

// =============================================================================
// COMPORTAMIENTO INDIVIDUAL DEL CAMINANTE (MARCHA INDIFERENTE -> GIRO -> ENGLOBAR)
// =============================================================================
public class CaminanteMarcha : MonoBehaviour
{
    private Vector3 direccionBase;
    private Vector3 vectorDerecha;
    private float velocidad;
    private float tiempoVida = 35.0f;
    private bool enEstadoIndiferente = true;
    private bool acorralando = false;
    private Transform objetivoJugador;

    void OnEnable()
    {
        Escena1Secuencia.listaNPCs.Add(this);
    }

    void OnDisable()
    {
        Escena1Secuencia.listaNPCs.Remove(this);
    }

    public void Iniciar(Vector3 puntoObjetivo, float vel)
    {
        velocidad = vel;
        Vector3 dir = (puntoObjetivo - transform.position);
        dir.y = 0;
        direccionBase = dir.normalized;

        if (direccionBase != Vector3.zero)
        {
            vectorDerecha = Vector3.Cross(Vector3.up, direccionBase).normalized;
            transform.rotation = Quaternion.LookRotation(direccionBase);
        }

        Destroy(gameObject, tiempoVida);
    }

    void Update()
    {
        if (enEstadoIndiferente)
        {
            // Marcha indiferente sin colisiones
            float desvioLateral = 0f;
            for (int i = 0; i < Escena1Secuencia.listaNPCs.Count; i++)
            {
                CaminanteMarcha otro = Escena1Secuencia.listaNPCs[i];
                if (otro != null && otro != this)
                {
                    Vector3 distanciaVec = transform.position - otro.transform.position;
                    float dist = distanciaVec.magnitude;
                    if (dist < 0.85f && dist > 0.05f)
                    {
                        float lado = Vector3.Dot(distanciaVec, vectorDerecha);
                        desvioLateral += ((lado >= 0) ? 1f : -1f) * ((0.85f - dist) / 0.85f);
                    }
                }
            }

            desvioLateral = Mathf.Clamp(desvioLateral, -0.6f, 0.6f);
            Vector3 dirFinal = (direccionBase + (vectorDerecha * desvioLateral)).normalized;
            dirFinal.y = 0;

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirFinal), Time.deltaTime * 8f);
            transform.position += dirFinal * (velocidad * Time.deltaTime);
        }
        else if (acorralando && objetivoJugador != null)
        {
            // Avanzar todos de frente hacia la cabeza del jugador
            Vector3 haciaJugador = (objetivoJugador.position - transform.position);
            haciaJugador.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaJugador), Time.deltaTime * 6f);
            transform.position += transform.forward * (velocidad * 1.5f * Time.deltaTime);
        }
    }

    public void PausarMarcha()
    {
        enEstadoIndiferente = false;
    }

    public IEnumerator GirarHaciaJugador(Transform jugador, float retraso)
    {
        yield return new WaitForSeconds(retraso);

        objetivoJugador = jugador;
        float tiempoGiro = 0f;

        while (tiempoGiro < 1.5f)
        {
            tiempoGiro += Time.deltaTime;
            if (objetivoJugador != null)
            {
                Vector3 dir = objetivoJugador.position - transform.position;
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
                }
            }
            yield return null;
        }
    }

    public void AvanzarHaciaJugador(Transform jugador, float vel)
    {
        objetivoJugador = jugador;
        velocidad = vel;
        acorralando = true;
    }
}