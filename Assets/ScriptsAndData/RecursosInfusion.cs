using System;
using UnityEngine;

/// <summary>Referencias persistentes a materiales, sin Shader.Find en el build.</summary>
public sealed class RecursosInfusion : ScriptableObject
{
    [Serializable]
    public sealed class ParteSanguinea
    {
        public string ruta;
        public Material[] materiales;
    }

    // Extraídos de Escena_nueva.unity en 73b6b014, no deducidos por color/nombre.
    public ParteSanguinea[] sangreOriginal;
    public Material plasticoNaCl;
    public Material sangre;
}
