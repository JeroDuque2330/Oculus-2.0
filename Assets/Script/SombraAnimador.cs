using UnityEngine;
using System.Collections;

public class SombraAnimador : MonoBehaviour
{
    [Header("Configuración de Animación")]
    public Animator animador;
    public string paramMarcha = "Marchando";
    public string paramVistaDerecha = "VistaDerecha";
    public string paramVistaIzquierda = "VistaIzquierda";

    [Header("Clips de Animación Directos (Opcionales)")]
    public AnimationClip clipMarcha;
    public AnimationClip clipVistaDerecha;
    public AnimationClip clipVistaIzquierda;

    [Header("Comportamiento")]
    public float velocidadMarcha = 0.85f;
    public float velocidadAproximacion = 1.4f;

    private Vector3 direccionMarcha;
    private Vector3 vectorLateral;
    private Transform transformJugador;
    private bool estaMarchando = true;
    private bool estaAcorralando = false;
    private Vector3 escalaObjetivo = Vector3.one;

    void Awake()
    {
        if (animador == null)
        {
            animador = GetComponentInChildren<Animator>();
        }
    }

    public void ConfigurarMarcha(Vector3 destino, float velocidad)
    {
        velocidadMarcha = velocidad;
        Vector3 direccion = (destino - transform.position);
        direccion.y = 0;
        direccionMarcha = direccion.normalized;

        if (direccionMarcha != Vector3.zero)
        {
            vectorLateral = Vector3.Cross(Vector3.up, direccionMarcha).normalized;
            transform.rotation = Quaternion.LookRotation(direccionMarcha);
        }

        estaMarchando = true;
        estaAcorralando = false;
    }

    void Update()
    {
        if (estaMarchando)
        {
            // Marcha indiferente con evasión lateral suave
            transform.position += direccionMarcha * (velocidadMarcha * Time.deltaTime);
        }
        else if (estaAcorralando && transformJugador != null)
        {
            // Avanzar y escalar para cubrir el campo visual
            Vector3 haciaJugador = (transformJugador.position - transform.position);
            haciaJugador.y = 0;
            if (haciaJugador != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(haciaJugador), Time.deltaTime * 5f);
            }

            transform.position += transform.forward * (velocidadAproximacion * Time.deltaTime);

            // Escala progresiva
            if (escalaObjetivo != Vector3.one)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, escalaObjetivo, Time.deltaTime * 1.5f);
            }
        }
    }

    public void DetenerMarcha()
    {
        estaMarchando = false;
    }

    public IEnumerator GirarYMirarJugador(Transform jugador, float retraso)
    {
        yield return new WaitForSeconds(retraso);
        transformJugador = jugador;
        estaMarchando = false;

        if (transformJugador == null) yield break;

        Vector3 haciaJugador = (transformJugador.position - transform.position);
        haciaJugador.y = 0;

        // Determinar si el jugador está a la derecha o a la izquierda de la sombra
        float productoCruz = Vector3.Cross(transform.forward, haciaJugador.normalized).y;
        bool mirarDerecha = productoCruz >= 0;

        if (animador != null)
        {
            if (mirarDerecha)
            {
                animador.SetTrigger(paramVistaDerecha);
            }
            else
            {
                animador.SetTrigger(paramVistaIzquierda);
            }
        }

        // Giro suave hacia el jugador
        float t = 0f;
        while (t < 2.0f)
        {
            t += Time.deltaTime;
            if (transformJugador != null)
            {
                Vector3 dir = (transformJugador.position - transform.position);
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
                }
            }
            yield return null;
        }
    }

    public void IniciarAcorralamiento(Transform jugador, float velocidad, float escalaAumento = 1.35f)
    {
        transformJugador = jugador;
        velocidadAproximacion = velocidad;
        escalaObjetivo = transform.localScale * escalaAumento;
        estaAcorralando = true;
    }
}