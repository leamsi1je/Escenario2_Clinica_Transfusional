using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Triángulos de la camilla, con cajas únicamente para acelerar las consultas.
/// Se captura en el editor y se serializa con la sonda; Play/Quest no necesitan
/// leer vértices del FBX ni añadir colliders a los muebles.
/// </summary>
[Serializable]
public sealed class GeometriaSondaNaCl
{
    [SerializeField] private MeshFilter filtro;
    [SerializeField] private Mesh malla;
    [SerializeField] private Bounds[] cajas;
    [SerializeField] private Vector3[] triangulos;
    [NonSerialized] private Nodo raiz;

    private sealed class Nodo
    {
        public Bounds limites;
        public Nodo izquierda, derecha;
        public int[] hojas;
    }

    public bool Vigente => Corresponde(filtro);

    public bool Corresponde(MeshFilter candidato)
    {
        return candidato != null && candidato == filtro && candidato.sharedMesh == malla &&
            cajas != null && cajas.Length > 0 && triangulos != null && triangulos.Length == cajas.Length*3;
    }

    public bool Interseca(Vector3 a,Vector3 b,Vector3 expansion,Matrix4x4 directa,float margen)
    {
        if (cajas == null || cajas.Length == 0 || triangulos == null) return true;
        if (raiz == null)
        {
            int[] indices=new int[cajas.Length];
            for (int i=0;i<indices.Length;i++) indices[i]=i;
            raiz=Construir(indices,0,indices.Length);
        }
        return Interseca(raiz,a,b,expansion,directa,
            directa.MultiplyPoint3x4(a),directa.MultiplyPoint3x4(b),margen*margen);
    }

    private Nodo Construir(int[] datos,int inicio,int cantidad)
    {
        Bounds limites=cajas[datos[inicio]];
        for (int i=inicio+1;i<inicio+cantidad;i++) limites.Encapsulate(cajas[datos[i]]);
        Nodo nodo=new Nodo { limites=limites };
        if (cantidad <= 8)
        {
            nodo.hojas=new int[cantidad];
            Array.Copy(datos,inicio,nodo.hojas,0,cantidad);
            return nodo;
        }
        int eje=limites.size.x >= limites.size.y ? 0 : 1;
        if (limites.size.z > limites.size[eje]) eje=2;
        Array.Sort(datos,inicio,cantidad,Comparer<int>.Create(
            (x,y) => cajas[x].center[eje].CompareTo(cajas[y].center[eje])));
        int mitad=cantidad/2;
        nodo.izquierda=Construir(datos,inicio,mitad);
        nodo.derecha=Construir(datos,inicio+mitad,cantidad-mitad);
        return nodo;
    }

    private bool Interseca(Nodo nodo,Vector3 a,Vector3 b,Vector3 expansion,
        Matrix4x4 directa,Vector3 mundoA,Vector3 mundoB,float margen2)
    {
        Bounds limites=nodo.limites;
        limites.Expand(expansion);
        if (!RutaSondaNaCl.IntersecaCaja(a,b,limites)) return false;
        if (nodo.hojas == null)
            return Interseca(nodo.izquierda,a,b,expansion,directa,mundoA,mundoB,margen2) ||
                Interseca(nodo.derecha,a,b,expansion,directa,mundoA,mundoB,margen2);
        foreach (int indice in nodo.hojas)
        {
            Bounds caja=cajas[indice];
            caja.Expand(expansion);
            if (!RutaSondaNaCl.IntersecaCaja(a,b,caja)) continue;
            int i=indice*3;
            // Medir en METROS mundiales: válido también con escala FBX y giro.
            if (DistanciaSegmentoTriangulo2(mundoA,mundoB,
                directa.MultiplyPoint3x4(triangulos[i]),directa.MultiplyPoint3x4(triangulos[i+1]),
                directa.MultiplyPoint3x4(triangulos[i+2])) <= margen2) return true;
        }
        return false;
    }

    public static float DistanciaSegmentoTriangulo2(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c)
    {
        Vector3 ab=b-a, ac=c-a, delta=q-p, cruz=Vector3.Cross(delta,ac);
        float determinante=Vector3.Dot(ab,cruz);
        if (Mathf.Abs(determinante) > 1e-12f)
        {
            Vector3 pa=p-a;
            float u=Vector3.Dot(pa,cruz)/determinante;
            Vector3 vcruz=Vector3.Cross(pa,ab);
            float v=Vector3.Dot(delta,vcruz)/determinante;
            float t=Vector3.Dot(ac,vcruz)/determinante;
            if (u >= 0f && v >= 0f && u+v <= 1f && t >= 0f && t <= 1f) return 0f;
        }
        return Mathf.Min(Mathf.Min(DistanciaPuntoTriangulo2(p,a,b,c),DistanciaPuntoTriangulo2(q,a,b,c)),
            Mathf.Min(DistanciaSegmentos2(p,q,a,b),
                Mathf.Min(DistanciaSegmentos2(p,q,b,c),DistanciaSegmentos2(p,q,c,a))));
    }

    private static float DistanciaPuntoTriangulo2(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {
        Vector3 ab=b-a, ac=c-a, ap=p-a;
        if (Vector3.Cross(ab,ac).sqrMagnitude < 1e-20f)
            return Mathf.Min(DistanciaSegmentos2(p,p,a,b),
                Mathf.Min(DistanciaSegmentos2(p,p,b,c),DistanciaSegmentos2(p,p,c,a)));
        float d1=Vector3.Dot(ab,ap), d2=Vector3.Dot(ac,ap);
        if (d1 <= 0f && d2 <= 0f) return ap.sqrMagnitude;
        Vector3 bp=p-b;
        float d3=Vector3.Dot(ab,bp), d4=Vector3.Dot(ac,bp);
        if (d3 >= 0f && d4 <= d3) return bp.sqrMagnitude;
        float vc=d1*d4-d3*d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return (p-(a+ab*(d1/(d1-d3)))).sqrMagnitude;
        Vector3 cp=p-c;
        float d5=Vector3.Dot(ab,cp), d6=Vector3.Dot(ac,cp);
        if (d6 >= 0f && d5 <= d6) return cp.sqrMagnitude;
        float vb=d5*d2-d1*d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return (p-(a+ac*(d2/(d2-d6)))).sqrMagnitude;
        float va=d3*d6-d5*d4;
        if (va <= 0f && d4-d3 >= 0f && d5-d6 >= 0f)
            return (p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).sqrMagnitude;
        float inversa=1f/(va+vb+vc);
        return (p-(a+ab*(vb*inversa)+ac*(vc*inversa))).sqrMagnitude;
    }

    private static float DistanciaSegmentos2(Vector3 p,Vector3 q,Vector3 a,Vector3 b)
    {
        Vector3 u=q-p, v=b-a, w=p-a;
        float uu=Vector3.Dot(u,u), vv=Vector3.Dot(v,v), vw=Vector3.Dot(v,w), s, t;
        if (uu < 1e-20f && vv < 1e-20f) return w.sqrMagnitude;
        if (uu < 1e-20f) { s=0f; t=Mathf.Clamp01(vw/vv); }
        else
        {
            float uw=Vector3.Dot(u,w);
            if (vv < 1e-20f) { t=0f; s=Mathf.Clamp01(-uw/uu); }
            else
            {
                float uv=Vector3.Dot(u,v), denominador=uu*vv-uv*uv;
                s=denominador > 1e-20f ? Mathf.Clamp01((uv*vw-uw*vv)/denominador) : 0f;
                t=(uv*s+vw)/vv;
                if (t < 0f) { t=0f; s=Mathf.Clamp01(-uw/uu); }
                else if (t > 1f) { t=1f; s=Mathf.Clamp01((uv-uw)/uu); }
            }
        }
        return (p+u*s-a-v*t).sqrMagnitude;
    }

#if UNITY_EDITOR
    public static GeometriaSondaNaCl Capturar(MeshFilter filtro)
    {
        Mesh malla=filtro.sharedMesh;
        List<Bounds> cajas=new List<Bounds>();
        List<Vector3> triangulos=new List<Vector3>();
        // Esta API de editor admite el FBX con Read/Write desactivado.
        // No modificar el importador de los modelos compartidos del proyecto.
        using (var datos=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(malla))
        {
            var fuente=datos[0];
            using (var vertices=new Unity.Collections.NativeArray<Vector3>(
                fuente.vertexCount,Unity.Collections.Allocator.Temp))
            {
                fuente.GetVertices(vertices);
                for (int s=0;s<fuente.subMeshCount;s++)
                {
                    var sub=fuente.GetSubMesh(s);
                    if (sub.topology != MeshTopology.Triangles) continue;
                    using (var indices=new Unity.Collections.NativeArray<int>(
                        sub.indexCount,Unity.Collections.Allocator.Temp))
                    {
                        fuente.GetIndices(indices,s,true); // Incluir baseVertex de cada submalla.
                        for (int i=0;i+2<indices.Length;i+=3)
                        {
                            Vector3 a=vertices[indices[i]], b=vertices[indices[i+1]], c=vertices[indices[i+2]];
                            if (Vector3.Cross(b-a,c-a).sqrMagnitude == 0f) continue;
                            Bounds caja=new Bounds(a,Vector3.zero);
                            caja.Encapsulate(b);
                            caja.Encapsulate(c);
                            cajas.Add(caja);
                            triangulos.Add(a); triangulos.Add(b); triangulos.Add(c);
                        }
                    }
                }
            }
        }
        if (cajas.Count == 0)
            throw new InvalidOperationException("[NaCl] No se pudo leer la geometría de "+filtro.name);
        return new GeometriaSondaNaCl { filtro=filtro,malla=malla,cajas=cajas.ToArray(),triangulos=triangulos.ToArray() };
    }
#endif
}

#if UNITY_EDITOR
// También preparar la copia de escena usada al construir el player, aunque
// todavía no se haya guardado la caché generada por ExecuteAlways en el editor.
public sealed class GeometriaSondaNaClAlConstruir : UnityEditor.Build.IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(UnityEngine.SceneManagement.Scene escena,UnityEditor.Build.Reporting.BuildReport informe)
    {
        if (!escena.IsValid() || !escena.isLoaded) return;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (VisualSalinaIndependiente visual in raiz.GetComponentsInChildren<VisualSalinaIndependiente>(true))
            visual.PrepararGeometriaCamilla(true);
    }
}
#endif
