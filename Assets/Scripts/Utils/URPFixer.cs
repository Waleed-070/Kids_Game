// ============================================================================
// URPFixer.cs — Utility to quickly fix missing magenta materials in URP
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;

public static class URPFixer
{
    private static Material defaultMaterial;

    /// <summary>
    /// Replaces the default Standard shader with the active pipeline's default material
    /// to fix the pink/magenta missing material issue on primitive objects.
    /// </summary>
    public static void FixMaterial(Renderer renderer, Color newColor)
    {
        if (renderer == null) return;

        if (defaultMaterial == null)
        {
            // Load the material from Resources. 
            // This guarantees the shader was included in the WebGL build and not stripped.
            defaultMaterial = Resources.Load<Material>("URP_BaseMaterial");

            // Editor fallback just in case
            if (defaultMaterial == null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null) urpShader = Shader.Find("Standard");
                if (urpShader != null) defaultMaterial = new Material(urpShader);
            }
        }

        // Apply new material and color
        if (defaultMaterial != null)
        {
            Material mat = new Material(defaultMaterial);
            
            // Support transparency if alpha < 1.0
            if (newColor.a < 1.0f)
            {
                mat.SetFloat("_Surface", 1); // 1 = Transparent in URP
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            
            mat.color = newColor;
            renderer.material = mat;
        }
    }
}
