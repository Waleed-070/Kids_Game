// ============================================================================
// URPBuildFixer.cs — Editor script to guarantee the URP shader is included in WebGL builds
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class URPBuildFixer
{
    // This runs automatically every time Unity compiles scripts
    [InitializeOnLoadMethod]
    public static void GuaranteeMaterialForBuilds()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string path = "Assets/Resources/URP_BaseMaterial.mat";
        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(path);
        
        // If the material doesn't exist in Resources, create it.
        // Putting it in Resources forces Unity to NOT strip the shader in WebGL builds.
        if (existingMat == null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Standard"); // Fallback

            if (urpShader != null)
            {
                Material newMat = new Material(urpShader);
                AssetDatabase.CreateAsset(newMat, path);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=green>[URPBuildFixer] Created URP_BaseMaterial in Resources folder to prevent pink grids in WebGL builds!</color>");
            }
        }
    }
}
#endif
