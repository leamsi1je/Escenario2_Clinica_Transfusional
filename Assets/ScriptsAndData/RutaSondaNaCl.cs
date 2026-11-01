using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trazado geométrico, no simulación de una cuerda. Usa cajas locales de mallas
/// cercanas (también sin Collider), conserva su orientación y comprueba el
/// grosor del tubo. Nunca crea colliders ni modifica muebles/escena.
/// </summary>
public sealed class RutaSondaNaCl
{
    private struct Obstaculo
    {
        public Bounds local, mundo;
        public Matrix4x4 inversa;
        public Matrix4x4 directa;
        public string nombre;
        public Vector3 expansion;
        public GeometriaSondaNaCl detalle;
    }

    private readonly List<Obstaculo> obstaculos = new List<Obstaculo>();
    private readonly List<Vector3> nodos = new List<Vector3>();
    private readonly List<Vector3> suavizada = new List<Vector3>();
    private readonly Collider[] contactos = new Collider[128];
    private Transform propietario, paciente;
    private Scene escena;
    private float radio;
    private GeometriaSondaNaCl geometriaCamilla;
    private struct Paso
    {
        public Vector3Int celda;
        public float prioridad;
    }
    private readonly List<Paso> abiertos = new List<Paso>();
    private readonly Dictionary<Vector3Int,float> distanciasRejilla = new Dictionary<Vector3Int,float>();
    private readonly Dictionary<Vector3Int,Vector3Int> padres = new Dictionary<Vector3Int,Vector3Int>();
    private readonly HashSet<Vector3Int> cerrados = new HashSet<Vector3Int>();
    private readonly Vector3Int[] direcciones = { Vector3Int.up,Vector3Int.right,Vector3Int.left,
        new Vector3Int(0,0,1),new Vector3Int(0,0,-1),Vector3Int.down };
    public string Bloqueo { get; private set; }

    public bool Calcular(Scene escenaActual,Transform equipo,Transform avatar,
        Vector3 inicio,Vector3 salidaVertical,Vector3 fin,float radioTubo,List<Vector3> resultado,
        GeometriaSondaNaCl detalleCamilla = null)
    {
        // OnEnable también se ejecuta mientras Unity carga o descarga escenas
        // y prefabs. No consultar sus raíces ni borrar la última ruta entonces.
        if (!escenaActual.IsValid() || !escenaActual.isLoaded)
        {
            Bloqueo = "escena todavía no cargada";
            return false;
        }
        escena = escenaActual;
        propietario = equipo;
        paciente = avatar;
        radio = radioTubo;
        geometriaCamilla = detalleCamilla;
        Bloqueo = null;
        if (!CapturarObstaculos(inicio,fin)) return false;
        Physics.SyncTransforms();
        // Revalidar una ruta ya calculada cuesta menos que volver a buscarla.
        if (resultado.Count >= 3 && (resultado[0]-inicio).sqrMagnitude < 1e-10f &&
            (resultado[1]-salidaVertical).sqrMagnitude < 1e-10f &&
            (resultado[resultado.Count-1]-fin).sqrMagnitude < 1e-10f && RutaLibre(resultado))
        {
            Bloqueo=null;
            return true;
        }
        resultado.Clear();
        if (!TramoLibre(inicio,salidaVertical))
        {
            Bloqueo="salida del gotero/regulador: "+Bloqueo;
            return false;
        }
        if (!TramoLibre(fin,fin))
        {
            Bloqueo="extremo de acceso a la paciente: "+Bloqueo;
            return false;
        }
        nodos.Clear();
        nodos.Add(salidaVertical);
        nodos.Add(fin);
        for (int i = 1; i <= 3; i++) AgregarNodo(fin+Vector3.up*(i*.16f));

        // Las cajas se comprueban TODAS. Solo se limita el número de esquinas
        // candidatas para no construir un grafo enorme en el visor.
        Vector3 medio = (salidaVertical+fin)*.5f;
        obstaculos.Sort((a,b) => a.mundo.SqrDistance(medio).CompareTo(b.mundo.SqrDistance(medio)));
        for (int i = 0; i < Mathf.Min(16,obstaculos.Count); i++)
        {
            Bounds b = obstaculos[i].mundo;
            b.Expand((radio+.035f)*2f);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                float y = Mathf.Clamp(b.max.y,fin.y+.10f,inicio.y+.20f);
                AgregarNodo(new Vector3(b.center.x+x*b.extents.x,y,b.center.z+z*b.extents.z));
            }
        }
        if (!BuscarCamino(resultado) && !BuscarRejilla(salidaVertical,fin,resultado)) return false;
        AcortarRuta(resultado);
        resultado.Insert(0,inicio); // Tramo recto por el centro del interruptor.
        SuavizarSiEsSeguro(resultado);
        // La suavización y el grosor se validan sobre TODOS los segmentos finales.
        if (!RutaLibre(resultado)) return false;
        Bloqueo = null;
        return true;
    }

    private bool Excluido(Transform t)
    {
        if (t == null || t.IsChildOf(propietario) || (paciente != null && t.IsChildOf(paciente)))
            return true;
        if (t.GetComponentInParent<Canvas>() != null) return true;
        for (Transform p = t; p != null; p = p.parent)
            if (p.CompareTag("Hand")) return true;
        return false;
    }

    private bool CapturarObstaculos(Vector3 inicio,Vector3 fin)
    {
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Bloqueo = "escena todavía no cargada";
            return false;
        }
        GameObject[] raices = escena.GetRootGameObjects();
        obstaculos.Clear();
        Bounds region = new Bounds(inicio,Vector3.zero);
        region.Encapsulate(fin);
        region.Expand(1f);
        foreach (GameObject raiz in raices)
        foreach (MeshRenderer renderer in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                renderer.gameObject.scene != escena || Excluido(renderer.transform)) continue;
            Bounds mundo = renderer.bounds;
            if (!mundo.Intersects(region) || mundo.max.y < fin.y-.05f) continue;
            // Una única malla cóncava de toda la habitación NO es una caja sólida.
            // Sus colliders reales se comprueban al final, sin llenar sus huecos.
            if (Mathf.Max(mundo.size.x,Mathf.Max(mundo.size.y,mundo.size.z)) > 4f) continue;
            MeshFilter filtro = renderer.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null) continue;
            GeometriaSondaNaCl detalle = geometriaCamilla != null && geometriaCamilla.Corresponde(filtro)
                ? geometriaCamilla : null;
            AgregarObstaculo(filtro.sharedMesh.bounds,renderer.transform.worldToLocalMatrix,mundo,renderer.name,detalle);
        }
        foreach (GameObject raiz in raices)
        foreach (BoxCollider collider in raiz.GetComponentsInChildren<BoxCollider>(true))
        {
            if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy ||
                collider.gameObject.scene != escena || Excluido(collider.transform) ||
                !collider.bounds.Intersects(region) || collider.bounds.max.y < fin.y-.05f) continue;
            AgregarObstaculo(new Bounds(collider.center,collider.size),
                collider.transform.worldToLocalMatrix,collider.bounds,collider.name);
        }
        return true;
    }

    private void AgregarObstaculo(Bounds local,Matrix4x4 inversa,Bounds mundo,string nombre,
        GeometriaSondaNaCl detalle = null)
    {
        // Transformar el radio mundial a cada eje local evita confundir las
        // escalas 100/0,8 del FBX con metros. No requiere Read/Write del mesh.
        float margen = radio+.004f;
        Vector3 expansion = new Vector3(
            new Vector3(inversa.m00,inversa.m01,inversa.m02).magnitude,
            new Vector3(inversa.m10,inversa.m11,inversa.m12).magnitude,
            new Vector3(inversa.m20,inversa.m21,inversa.m22).magnitude)*margen*2f;
        local.Expand(expansion);
        obstaculos.Add(new Obstaculo { local=local,mundo=mundo,inversa=inversa,nombre=nombre,
            directa=inversa.inverse,expansion=expansion,detalle=detalle });
    }

    public static bool IntersecaCaja(Vector3 a,Vector3 b,Bounds caja)
    {
        Vector3 delta = b-a, minimo = caja.min, maximo = caja.max;
        float entrada = 0f, salida = 1f;
        for (int eje = 0; eje < 3; eje++)
        {
            if (Mathf.Abs(delta[eje]) < .0000001f)
            {
                if (a[eje] < minimo[eje] || a[eje] > maximo[eje]) return false;
                continue;
            }
            float t0 = (minimo[eje]-a[eje])/delta[eje];
            float t1 = (maximo[eje]-a[eje])/delta[eje];
            if (t0 > t1) { float auxiliar=t0; t0=t1; t1=auxiliar; }
            entrada = Mathf.Max(entrada,t0);
            salida = Mathf.Min(salida,t1);
            if (entrada > salida) return false;
        }
        return true;
    }

    private bool Libre(Vector3 a,Vector3 b)
    {
        foreach (Obstaculo obstaculo in obstaculos)
        {
            Vector3 localA=obstaculo.inversa.MultiplyPoint3x4(a);
            Vector3 localB=obstaculo.inversa.MultiplyPoint3x4(b);
            if (IntersecaCaja(localA,localB,obstaculo.local) &&
                (obstaculo.detalle == null || obstaculo.detalle.Interseca(localA,localB,
                    obstaculo.expansion,obstaculo.directa,radio+.004f)))
            {
                Bloqueo = obstaculo.nombre;
                return false;
            }
        }
        return true;
    }

    private void AgregarNodo(Vector3 punto)
    {
        if (!TramoLibre(punto,punto)) return;
        foreach (Vector3 anterior in nodos)
            if ((anterior-punto).sqrMagnitude < .00001f) return;
        nodos.Add(punto);
    }

    private bool BuscarCamino(List<Vector3> resultado)
    {
        int cantidad = nodos.Count;
        float[] distancias = new float[cantidad];
        int[] previos = new int[cantidad];
        bool[] visitados = new bool[cantidad];
        for (int i = 0; i < cantidad; i++) { distancias[i]=float.PositiveInfinity; previos[i]=-1; }
        distancias[0]=0f;
        for (int paso = 0; paso < cantidad; paso++)
        {
            int actual = -1;
            for (int i = 0; i < cantidad; i++)
                if (!visitados[i] && (actual < 0 || distancias[i] < distancias[actual])) actual=i;
            if (actual < 0 || float.IsPositiveInfinity(distancias[actual])) return false;
            if (actual == 1) break;
            visitados[actual]=true;
            for (int destino = 0; destino < cantidad; destino++)
            {
                if (visitados[destino] || destino == actual) continue;
                float distancia = distancias[actual]+Vector3.Distance(nodos[actual],nodos[destino]);
                if (distancia >= distancias[destino] || !TramoLibre(nodos[actual],nodos[destino])) continue;
                distancias[destino]=distancia;
                previos[destino]=actual;
            }
        }
        if (previos[1] < 0) return false;
        for (int i = 1; i >= 0; i = previos[i]) resultado.Add(nodos[i]);
        resultado.Reverse();
        return true;
    }

    private bool TramoLibre(Vector3 a,Vector3 b) => Libre(a,b) && FisicaLibre(a,b);

    private bool RutaLibre(List<Vector3> puntos)
    {
        for (int i=1;i<puntos.Count;i++)
            if (!TramoLibre(puntos[i-1],puntos[i])) return false;
        return true;
    }

    // Segunda búsqueda: también explora laterales y huecos intermedios, no
    // solo las cuatro esquinas SUPERIORES de las cajas globales.
    private bool BuscarRejilla(Vector3 inicio,Vector3 fin,List<Vector3> resultado)
    {
        const float paso=.04f;
        Bounds region=new Bounds((inicio+fin)*.5f,new Vector3(
            Mathf.Abs(inicio.x-fin.x)+1f,Mathf.Abs(inicio.y-fin.y)+.30f,Mathf.Abs(inicio.z-fin.z)+1f));
        abiertos.Clear(); distanciasRejilla.Clear(); padres.Clear(); cerrados.Clear();
        Vector3Int origen=Vector3Int.zero;
        distanciasRejilla[origen]=0f;
        Insertar(new Paso { celda=origen,prioridad=Vector3.Distance(inicio,fin) });
        for (int intentos=0;abiertos.Count>0 && intentos<4096;intentos++)
        {
            Vector3Int actual=Extraer().celda;
            if (!cerrados.Add(actual)) continue;
            Vector3 punto=inicio+(Vector3)actual*paso;
            if (TramoLibre(punto,fin))
            {
                resultado.Clear(); resultado.Add(fin);
                for (Vector3Int cursor=actual;;cursor=padres[cursor])
                {
                    Vector3 p=inicio+(Vector3)cursor*paso;
                    if ((resultado[resultado.Count-1]-p).sqrMagnitude > 1e-10f) resultado.Add(p);
                    if (cursor == origen) break;
                }
                resultado.Reverse();
                return true;
            }
            foreach (Vector3Int direccion in direcciones)
            {
                Vector3Int siguiente=actual+direccion;
                Vector3 destino=inicio+(Vector3)siguiente*paso;
                if (cerrados.Contains(siguiente) || !region.Contains(destino)) continue;
                float distancia=distanciasRejilla[actual]+paso, anterior;
                if (distanciasRejilla.TryGetValue(siguiente,out anterior) && distancia >= anterior) continue;
                if (!TramoLibre(punto,destino)) continue;
                distanciasRejilla[siguiente]=distancia;
                padres[siguiente]=actual;
                Insertar(new Paso { celda=siguiente,prioridad=distancia+Vector3.Distance(destino,fin) });
            }
        }
        return false;
    }

    private void AcortarRuta(List<Vector3> puntos)
    {
        for (int i=0;i<puntos.Count-2;i++)
            for (int j=puntos.Count-1;j>i+1;j--)
                if (TramoLibre(puntos[i],puntos[j]))
                {
                    puntos.RemoveRange(i+1,j-i-1);
                    break;
                }
    }

    private void Insertar(Paso paso)
    {
        int i=abiertos.Count;
        abiertos.Add(paso);
        while (i>0)
        {
            int padre=(i-1)/2;
            if (abiertos[padre].prioridad <= paso.prioridad) break;
            abiertos[i]=abiertos[padre]; i=padre;
        }
        abiertos[i]=paso;
    }

    private Paso Extraer()
    {
        Paso minimo=abiertos[0], ultimo=abiertos[abiertos.Count-1];
        abiertos.RemoveAt(abiertos.Count-1);
        int i=0;
        while (i*2+1<abiertos.Count)
        {
            int hijo=i*2+1;
            if (hijo+1<abiertos.Count && abiertos[hijo+1].prioridad<abiertos[hijo].prioridad) hijo++;
            if (ultimo.prioridad <= abiertos[hijo].prioridad) break;
            abiertos[i]=abiertos[hijo]; i=hijo;
        }
        if (abiertos.Count>0) abiertos[i]=ultimo;
        return minimo;
    }

    private void SuavizarSiEsSeguro(List<Vector3> puntos)
    {
        suavizada.Clear();
        suavizada.Add(puntos[0]);
        // Mantener íntegra la salida vertical (índice 1) del regulador.
        for (int i = 1; i < puntos.Count-1; i++)
        {
            if (i == 1) { suavizada.Add(puntos[i]); continue; }
            Vector3 centro=puntos[i], previo=puntos[i-1], siguiente=puntos[i+1];
            float radioCurva=Mathf.Min(.035f,Mathf.Min(Vector3.Distance(centro,previo),
                Vector3.Distance(centro,siguiente))*.20f);
            Vector3 a=Vector3.MoveTowards(centro,previo,radioCurva);
            Vector3 b=Vector3.MoveTowards(centro,siguiente,radioCurva);
            bool libre=TramoLibre(suavizada[suavizada.Count-1],a);
            Vector3 ultimo=a;
            for (int j=1;j<=6 && libre;j++)
            {
                float t=j/6f;
                Vector3 p=(1f-t)*(1f-t)*a+2f*(1f-t)*t*centro+t*t*b;
                libre=TramoLibre(ultimo,p);
                ultimo=p;
            }
            if (!libre) { suavizada.Add(centro); continue; }
            suavizada.Add(a);
            for (int j=1;j<=6;j++)
            {
                float t=j/6f;
                suavizada.Add((1f-t)*(1f-t)*a+2f*(1f-t)*t*centro+t*t*b);
            }
        }
        suavizada.Add(puntos[puntos.Count-1]);
        puntos.Clear();
        puntos.AddRange(suavizada);
    }

    private bool FisicaLibre(Vector3 a,Vector3 b)
    {
        int cantidad=Physics.OverlapCapsuleNonAlloc(a,b,radio+.003f,contactos,~0,QueryTriggerInteraction.Ignore);
        if (cantidad == contactos.Length) { Bloqueo="demasiados contactos físicos"; return false; }
        for (int i=0;i<cantidad;i++)
        {
            Collider contacto=contactos[i];
            if (contacto.gameObject.scene != escena || Excluido(contacto.transform)) continue;
            Bloqueo=contacto.name;
            return false;
        }
        return true;
    }
}
