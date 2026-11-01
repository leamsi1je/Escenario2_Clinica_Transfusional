using UnityEngine;

/// <summary>
/// Guía corta verificada con la escena entregada. No busca caminos, no lee
/// mallas y no crea materiales, colliders ni objetos. Coste fijo: 18 puntos.
/// </summary>
public static class RutaGuiadaNaCl
{
    public const int CantidadPuntos = 18;

    public static void Generar(Vector3 inicio,Vector3 fin,Vector3[] puntos,out Vector3 regulador)
    {
        float bajada=Mathf.Clamp((inicio.y-fin.y)*.45f,.18f,.45f);
        regulador=inicio+Vector3.down*bajada;
        Vector3 salida=regulador+Vector3.down*.07f;
        Vector3 control1=salida+Vector3.down*.15f;
        Vector3 control2=fin+Vector3.up*.14f;
        puntos[0]=inicio;
        puntos[1]=salida;
        for (int i=1;i<=16;i++)
        {
            float t=i/16f, u=1f-t;
            puntos[i+1]=u*u*u*salida+3f*u*u*t*control1+3f*u*t*t*control2+t*t*t*fin;
        }
    }

    public static bool FueraDeCamilla(Vector3[] puntos,Transform marco,float radio)
    {
        if (marco == null) return false;
        Matrix4x4 inversa=marco.worldToLocalMatrix;
        Vector3 margen=new Vector3(new Vector3(inversa.m00,inversa.m01,inversa.m02).magnitude,
            new Vector3(inversa.m10,inversa.m11,inversa.m12).magnitude,
            new Vector3(inversa.m20,inversa.m21,inversa.m22).magnitude)*(radio+.004f);
        // Región local en la que se contrastaron las 6.251 cajas del archivo.
        Vector3 minimo=new Vector3(3.810948f,.893459f,-1.076970f);
        Vector3 maximo=new Vector3(4.125877f,2.325925f,-.636645f);
        // Unión conservadora de las diez caras que alcanzan esta región.
        // Las demás están fuera de ella. NO es la caja global de la cama.
        Bounds cercano=new Bounds();
        cercano.SetMinMax(new Vector3(4.103386f,.703668f,-.891125f),
            new Vector3(5.092993f,1.296166f,.378085f));
        cercano.Expand(margen*2f);
        Vector3 anterior=inversa.MultiplyPoint3x4(puntos[0]);
        for (int i=0;i<puntos.Length;i++)
        {
            Vector3 p=inversa.MultiplyPoint3x4(puntos[i]);
            if (p.x-margen.x<minimo.x || p.y-margen.y<minimo.y || p.z-margen.z<minimo.z ||
                p.x+margen.x>maximo.x || p.y+margen.y>maximo.y || p.z+margen.z>maximo.z) return false;
            if (i>0 && RutaSondaNaCl.IntersecaCaja(anterior,p,cercano)) return false;
            anterior=p;
        }
        return true;
    }
}
