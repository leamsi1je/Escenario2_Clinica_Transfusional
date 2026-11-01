using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Encuadre estable: no utiliza la cámara ni el gotero sanguíneo como origen.</summary>
public static class UbicacionNaClReferencia
{
    public const float SeparacionPantalla = .12f;

    /// <summary>
    /// Calcula sin modificar la escena: mismo suelo y extremo superior que sangre,
    /// delante de la pantalla y cerca de la cama. No usa el pivote de meshes FBX.
    /// </summary>
    public static bool CalcularPresentacion(VisualSalinaIndependiente visual,Transform sangre,
        out Vector3 posicion,out Vector3 escala)
    {
        posicion = visual.transform.position;
        escala = visual.transform.localScale;
        if (sangre == null) return false;
        Renderer soporte = sangre.Find("Soporte")?.GetComponent<Renderer>();
        Renderer metal = visual.transform.Find("Parante_Metal")?.GetComponent<Renderer>();
        Renderer bolsa = visual.transform.Find("Recipiente_Transparente")?.GetComponent<Renderer>();
        Scene escena = visual.gameObject.scene;
        Transform pantalla = Buscar(escena,"MonitorScreen");
        Renderer pantallaRenderer = pantalla != null ? pantalla.GetComponent<Renderer>() : null;
        Transform derecha = Buscar(escena,"RightHand_083");
        Transform izquierda = Buscar(escena,"LeftHand_026");
        Transform cabeza = Buscar(escena,"Head_043");
        if (soporte == null || metal == null || bolsa == null || pantallaRenderer == null ||
            derecha == null || izquierda == null || cabeza == null) return false;
        float alturaSangre = soporte.bounds.size.y;
        float alturaMetal = metal.bounds.max.y - visual.transform.position.y;
        if (alturaSangre < 1f || alturaMetal < 1f) return false;
        float factor = alturaSangre/alturaMetal;
        if (factor < .25f || factor > 4f) return false;
        Vector3 origen = posicion;
        // Deshacer el desplazamiento lateral V4: partir de la cabecera, no de
        // la posición anterior que quedó junto a la cortina/carro.
        Vector3 baseCercana;
        Quaternion rotacionBase;
        Transform acceso;
        if (!Calcular(escena,sangre,out baseCercana,out rotacionBase,out acceso)) return false;
        posicion = baseCercana;
        escala *= factor; // Igualar la altura, nunca comprimir solo el eje Y.
        Bounds bolsaAjustada = new Bounds(posicion+(bolsa.bounds.center-origen)*factor,
            bolsa.bounds.size*factor);
        Vector3 haciaDelante = (derecha.position+izquierda.position)*.5f-cabeza.position;
        haciaDelante.y = 0f;
        if (haciaDelante.sqrMagnitude < .01f) return false;
        haciaDelante.Normalize();
        Bounds limitesPantalla = pantallaRenderer.bounds;
        // Incluir el Canvas que realmente dibuja las curvas, además de la malla.
        // Leerlo NO cambia su tamaño, su capa ni la configuración del monitor.
        Transform objetoCanvas = pantalla.parent != null
            ? pantalla.parent.Find("CanvasMonitorSignosVitales") : null;
        RectTransform rect = objetoCanvas as RectTransform;
        if (rect != null && rect.gameObject.activeInHierarchy)
        {
            Vector3[] esquinas = new Vector3[4];
            rect.GetWorldCorners(esquinas);
            Bounds limitesCanvas = new Bounds(esquinas[0],Vector3.zero);
            for (int i = 1; i < 4; i++) limitesCanvas.Encapsulate(esquinas[i]);
            // No usar un Canvas heredado de cientos de metros como referencia.
            if (limitesCanvas.size.magnitude < 3f &&
                Vector3.Distance(limitesCanvas.center,limitesPantalla.center) < .75f)
                limitesPantalla.Encapsulate(limitesCanvas);
        }
        float bordePantalla = Vector3.Dot(limitesPantalla.center,haciaDelante)+
            ExtensionProyectada(limitesPantalla,haciaDelante);
        float bordeBolsa = Vector3.Dot(bolsaAjustada.center,haciaDelante)-
            ExtensionProyectada(bolsaAjustada,haciaDelante);
        float separar = Mathf.Max(0f,bordePantalla+SeparacionPantalla-bordeBolsa);
        if (separar > .45f) return false; // No alejar el conjunto metros por un Canvas inválido.
        posicion += haciaDelante*separar;
        return true;
    }

    public static float ExtensionProyectada(Bounds limites,Vector3 direccion)
    {
        Vector3 e = limites.extents;
        return Mathf.Abs(direccion.x)*e.x + Mathf.Abs(direccion.y)*e.y + Mathf.Abs(direccion.z)*e.z;
    }

    public static bool Calcular(Scene escena, Transform sangre, out Vector3 posicion,
        out Quaternion rotacion, out Transform acceso)
    {
        posicion = Vector3.zero;
        rotacion = Quaternion.identity;
        acceso = null;
        Transform cabeza = Buscar(escena, "Head_043");
        Transform derecha = Buscar(escena, "RightHand_083");
        Transform izquierda = Buscar(escena, "LeftHand_026");
        if (cabeza == null || derecha == null || izquierda == null || sangre == null)
            return false; // No adivinar una ubicación si cambia el avatar.
        Vector3 lateral = derecha.position - izquierda.position;
        lateral.y = 0f;
        Vector3 haciaPies = (derecha.position + izquierda.position) * 0.5f - cabeza.position;
        haciaPies.y = 0f;
        if (lateral.sqrMagnitude < 0.01f || haciaPies.sqrMagnitude < 0.01f) return false;
        lateral.Normalize();
        haciaPies.Normalize();
        Renderer soporte = null;
        foreach (Transform t in sangre.GetComponentsInChildren<Transform>(true))
            if (t.name == "Soporte") soporte = t.GetComponent<Renderer>();
        if (soporte == null) return false;
        float signoSangre = Mathf.Sign(Vector3.Dot(soporte.bounds.center - cabeza.position, lateral));
        if (signoSangre == 0f) signoSangre = 1f;
        Vector3 ladoNaCl = -lateral * signoSangre;
        posicion = cabeza.position + ladoNaCl * 0.80f + haciaPies * 0.45f;
        posicion.y = soporte.bounds.min.y;
        rotacion = Quaternion.LookRotation(haciaPies, Vector3.up);
        acceso = signoSangre > 0f ? izquierda : derecha;
        return true;
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
        return null;
    }
}
