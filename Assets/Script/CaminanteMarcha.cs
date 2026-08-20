using UnityEngine;
using System.Collections.Generic;

public class CaminanteMarcha : MonoBehaviour
{
    private Vector3 direccionBase;
    private Vector3 vectorDerecha;
    private float velocidad;
    private float tiempoVida = 45.0f;
    private bool enEstadoIndiferente = true;
    private bool acorralando = false;
    private Transform objetivoJugador;

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
            // Marcha indiferente con evasión lateral suave entre sombras
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

            if (dirFinal != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirFinal), Time.deltaTime * 8f);
                transform.position += dirFinal * (velocidad * Time.deltaTime);
            }
        }
        else if (acorralando && objetivoJugador != null)
        {
            Vector3 haciaJugador = (objetivoJugador.position - transform.position);
            haciaJugador.y = 0;
            if (haciaJugador != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaJugador), Time.deltaTime * 6f);
                transform.position += transform.forward * (velocidad * 1.5f * Time.deltaTime);
            }
        }
    }

    public void PausarMarcha()
    {
        enEstadoIndiferente = false;
    }

    public void AvanzarHaciaJugador(Transform jugador, float vel)
    {
        objetivoJugador = jugador;
        velocidad = vel;
        acorralando = true;
    }
}