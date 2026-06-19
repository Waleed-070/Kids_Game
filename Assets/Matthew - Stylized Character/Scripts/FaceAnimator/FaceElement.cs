using UnityEngine;
namespace Bunssar
{
[System.Serializable]   
public class FaceElement
{
    public string FaceName;
    [Header("<size=15>Textures")]
    public Texture2D RightEye;
    public Texture2D LeftEye;
    public Texture2D RightIris;
    public Texture2D LeftIris;
    public Texture2D RightEyebrow;
    public Texture2D LeftEyebrow;
    public Texture2D Nose;
    public Texture2D Mouth;
    public Texture2D Extra;
    [Header("<size=15>Parameters")]
    [Header("<size=12><i>-Eyes-")]
    public Vector2 RightEyePosition;
    public Vector2 LeftEyePosition;
    public Vector2 RightIrisPosition;
    public Vector2 LeftIrisPosition;
    [Range(0.05f,1)]
    public float RightIrisCutoff;
    [Range(0.05f,1)]
    public float LeftIrisCutoff;
    public bool OverrideIrisColor;
    public Color IrisColor;
    [Header("<size=12><i>-Mouth-")]
    public Vector2 MouthPosition;
     [Range(0.05f,1)]
    public float MouthCutoff;
    [Header("<size=12><i>-Eyebrows-")]
    public Vector3 LeftEyebrowPosition;
    public Vector3 RightEyebrowPosition;
    [Range(-1,1)]
    public float RightOuterRotation;
    [Range(-1,1)]
    public float LeftOuterRotation;
    [Range(-1,1)]
    public float RightInnerRotation;
    [Range(-1,1)]
    public float LeftInnerRotation;
    
}
}
