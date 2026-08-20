using UnityEngine;
using System.Collections.Generic;

public class CaminanteMarcha : MonoBehaviour
{
    public enum EstadoCaminante
    {
        MarchaIndiferente,
        CorriendoCercaRodeando,
        TotalmenteQuietoMirando
    }

    [Header("Configuración")]
    public Transform centroJugador;
    public float velocidadLineal = 0.85f;
    public float radioOrbita = 1.8f;
    public float anguloActual = 0f;
    public float sentidoGiro = 1f; // +1 horario, -1 antihorario
    public EstadoCaminante estado = EstadoCaminante.MarchaIndiferente;

    private Vector3 direccionBase;
    private Vector3 vectorDerecha;
    private float alturaBaseY;
    private Animator animador;

    void Awake()
    {
        animador = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        if (!Escena1Secuencia.listaNPCs.Contains(this))
        {
            Escena1Secuencia.listaNPCs.Add(this);
        }
    }

    void OnDisable()
    {
        Escena1Secuencia.listaNPCs.Remove(this);
    }

    // 1. Caminan normal por el escenario
    public void IniciarMarchaIndiferente(Transform jugador, Vector3 puntoObjetivo, float vel, float radioCercano, float sentido)
    {
        centroJugador = jugador;
        velocidadLineal = vel;
        alturaBaseY = transform.position.y;
        radioOrbita = radioCercano;
        sentidoGiro = sentido;
        estado = EstadoCaminante.MarchaIndiferente;

        Vector3 dir = (puntoObjetivo - transform.position);
        dir.y = 0;
        direccionBase = dir.normalized;

        if (direccionBase != Vector3.zero)
        {
            vectorDerecha = Vector3.Cross(Vector3.up, direccionBase).normalized;
            transform.rotation = Quaternion.LookRotation(direccionBase);
        }

        if (animador != null) animador.speed = 1f;
    }

    // 2. Se quedan totalmente quietos e inmóviles mirando al personaje
    public void QuedarseTotalmenteQuietoMirando(Transform jugador)
    {
        centroJugador = jugador;
        estado = EstadoCaminante.TotalmenteQuietoMirando;

        // Congelar animación de marcha
        if (animador != null)
        {
            animador.speed = 0f;
        }

        // Girar para mirar de frente al personaje
        if (centroJugador != null)
        {
            Vector3 haciaJugador = centroJugador.position - transform.position;
            haciaJugador.y = 0;
            if (haciaJugador != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(haciaJugador);
            }
        }
    }

    // 3. Corren alrededor del personaje muy cerca (por 10 segundos)
    public void CorrerMuyCercaRodeando(Transform jugador, float radioCercano, float velocidadCorrer)
    {
        centroJugador = jugador;
        radioOrbita = radioCercano;
        velocidadLineal = velocidadCorrer;
        estado = EstadoCaminante.CorriendoCercaRodeando;

        // Animación acelerada para representar que están corriendo
        if (animador != null)
        {
            animador.speed = 1.7f;
        }

        if (centroJugador != null)
        {
            Vector3 offset = transform.position - centroJugador.position;
            offset.y = 0;
            anguloActual = Mathf.Atan2(offset.x, offset.z);
        }
    }

    // Sobrecarga de compatibilidad
    public void Iniciar(Vector3 puntoObjetivo, float vel)
    {
        if (Camera.main != null) centroJugador = Camera.main.transform;
        IniciarMarchaIndiferente(centroJugador, puntoObjetivo, vel, 1.8f, (Random.value > 0.5f) ? 1f : -1f);
    }

    void Update()
    {
        if (centroJugador == null)
        {
            if (Camera.main != null) centroJugador = Camera.main.transform;
            else return;
        }

        switch (estado)
        {
            case EstadoCaminante.MarchaIndiferente:
                ActualizarMarchaIndiferente(Time.deltaTime);
                break;

            case EstadoCaminante.CorriendoCercaRodeando:
                ActualizarCorriendoCercaRodeando(Time.deltaTime);
                break;

            case EstadoCaminante.TotalmenteQuietoMirando:
                ActualizarQuietoMirando(Time.deltaTime);
                break;
        }
    }

    private void ActualizarMarchaIndiferente(float dt)
    {
        float desvioLateral = 0f;
        for (int i = 0; i < Escena1Secuencia.listaNPCs.Count; i++)
        {
            CaminanteMarcha otro = Escena1Secuencia.listaNPCs[i];
            if (otro != null && otro != this)
            {
                Vector3 distanciaVec = transform.position - otro.transform.position;
                float dist = distanciaVec.magnitude;
                if (dist < 0.8f && dist > 0.05f)
                {
                    float lado = Vector3.Dot(distanciaVec, vectorDerecha);
                    desvioLateral += ((lado >= 0) ? 1f : -1f) * ((0.8f - dist) / 0.8f);
                }
            }
        }

        desvioLateral = Mathf.Clamp(desvioLateral, -0.5f, 0.5f);
        Vector3 dirFinal = (direccionBase + (vectorDerecha * desvioLateral)).normalized;
        dirFinal.y = 0;

        if (dirFinal != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirFinal), dt * 6f);
            transform.position += dirFinal * (velocidadLineal * dt);
        }
    }

    private void ActualizarCorriendoCercaRodeando(float dt)
    {
        // Avanzar rápidamente en ángulo alrededor del personaje
        float velAng = (velocidadLineal / Mathf.Max(0.5f, radioOrbita)) * sentidoGiro;
        anguloActual += velAng * dt;

        if (anguloActual > Mathf.PI * 2f) anguloActual -= Mathf.PI * 2f;
        if (anguloActual < 0f) anguloActual += Mathf.PI * 2f;

        // Posición en círculo cerrado muy cerca del jugador
        Vector3 posObjetivo = new Vector3(
            centroJugador.position.x + Mathf.Sin(anguloActual) * radioOrbita,
            alturaBaseY,
            centroJugador.position.z + Mathf.Cos(anguloActual) * radioOrbita
        );

        Vector3 repulsion = CalcularRepulsionVecinos();
        posObjetivo += repulsion;

        // Orientar hacia el vector tangente de carrera
        Vector3 tangente = new Vector3(
            Mathf.Cos(anguloActual) * sentidoGiro,
            0f,
            -Mathf.Sin(anguloActual) * sentidoGiro
        ).normalized;

        if (tangente != Vector3.zero)
        {
            Quaternion rotDeseada = Quaternion.LookRotation(tangente);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotDeseada, dt * 10f);
        }

        // Desplazamiento ágil hacia la posición orbital
        transform.position = Vector3.Lerp(transform.position, posObjetivo, dt * 8f);
    }

    private void ActualizarQuietoMirando(float dt)
    {
        // Totalmente inmóvil mirando fijamente al jugador
        if (centroJugador != null)
        {
            Vector3 haciaJugador = centroJugador.position - transform.position;
            haciaJugador.y = 0;
            if (haciaJugador != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaJugador), dt * 8f);
            }
        }
    }

    private Vector3 CalcularRepulsionVecinos()
    {
        Vector3 repulsion = Vector3.zero;
        for (int i = 0; i < Escena1Secuencia.listaNPCs.Count; i++)
        {
            CaminanteMarcha otro = Escena1Secuencia.listaNPCs[i];
            if (otro != null && otro != this)
            {
                Vector3 distVec = transform.position - otro.transform.position;
                distVec.y = 0;
                float dist = distVec.magnitude;
                if (dist < 0.75f && dist > 0.01f)
                {
                    repulsion += distVec.normalized * ((0.75f - dist) * 0.25f);
                }
            }
        }
        return repulsion;
    }
}