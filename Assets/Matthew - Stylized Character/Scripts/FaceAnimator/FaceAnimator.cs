using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
namespace Bunssar
{
[System.Serializable]
public class FaceAnimator : MonoBehaviour
{
    private FaceElement element;
    [Header("<size=40>Face Animator")]
    [Header("<size=20>Setup & Materials")]
    public FaceSet FaceSet;
    [Header("<size=10><i>Materials")]
    public Material RightEye;
    public Material LeftEye;
    public Material RightIris;
    public Material LeftIris;
    public Material RightEyebrow;
    public Material LeftEyebrow;
    public Material Nose;
    public Material Mouth;
    public Material Extra;
    [Header("<size=10><i>Renderers")]
    public  SkinnedMeshRenderer RendererRightEye;
    public  SkinnedMeshRenderer RendererLeftEye;
    public  SkinnedMeshRenderer RendererRightIris;
    public  SkinnedMeshRenderer RendererLeftIris;
    public  SkinnedMeshRenderer RendererRightEyebrow;
    public  SkinnedMeshRenderer RendererLeftEyebrow;
    public  SkinnedMeshRenderer RendererNose;
    public  SkinnedMeshRenderer RendererMouth;
    public  SkinnedMeshRenderer RendererExtra;
    [Header("<size=10><i>Object References")]
    public Transform RightInnerbrow;
    public Transform RightOuterbrow;
    public Transform LeftInnerbrow;
    public Transform LeftOuterbrow;
    [Header("<size=20>Logic & Animation")]
    [Header("<size=10><i><color=green>Constants")]
    public float TransitionSpeed;
    public float TextureUpdateDelay;
    public Color DefaultIrisColor;
    [Header("<size=10><color=red><i>Only public for debugging, variable during runtime.")]
    [Range(0,30)]
    public int CurrentFaceIndex;
    [Range(0,30)]
    public int DefaultFaceIndex;
    [TextArea]
    public string ImportantMessages;
    private int ChangeCheckReference;
    //Hidden
      Material i_RightEye;
      Material i_LeftEye;
      Material i_RightIris;
      Material i_LeftIris;
      Material i_RightEyebrow;
      Material i_LeftEyebrow;
      Material i_Nose;
      Material i_Mouth;
      Material i_Extra;
      Vector3 R_browOrigin;
      Vector3 L_browOrigin;
      Vector3 BrowRotationOrigin;
    void Awake()
    {
        R_browOrigin = RightOuterbrow.localPosition;
        L_browOrigin = LeftOuterbrow.localPosition;
        BrowRotationOrigin.x = RightOuterbrow.localEulerAngles.x;
        BrowRotationOrigin.y = RightOuterbrow.localEulerAngles.y;

        i_RightEye=new Material(RightEye);
        i_LeftEye=new Material(LeftEye);
        i_RightIris=new Material(RightIris);
        i_LeftIris=new Material(LeftIris);
        i_RightEyebrow=new Material(RightEyebrow);
        i_LeftEyebrow=new Material(LeftEyebrow);
        i_Nose=new Material(Nose);
        i_Mouth=new Material(Mouth);
        i_Extra=new Material(Extra);
        
        RendererRightEye.material = i_RightEye;
        RendererLeftEye.material = i_LeftEye;
        RendererRightIris.material = i_RightIris;
        RendererLeftIris.material = i_LeftIris;
        RendererRightEyebrow.material = i_RightEyebrow;
        RendererLeftEyebrow.material = i_LeftEyebrow;
        RendererNose.material = i_Nose;
        RendererMouth.material = i_Mouth;
        RendererExtra.material = i_Extra;

             #if UNITY_EDITOR
             if(FaceSet == null)
            EditorUtility.DisplayDialog("Inkjet3D FaceAnimator", "No FaceSet assigned in '" + gameObject.name +"'. FaceAnimator will be disabled until a FaceSet is assigned.", "Got it");
            #endif
            StartCoroutine(UpdateTextures());
            ReturnToDefaultFace();
    }
    
    void SetChangeCheckReference(int input)
    {
      ChangeCheckReference = input;
    }

    bool ChangeCheck(int input)
    {
      if(input == ChangeCheckReference)
      {
      return false;
      }
      else
      {
      return true;
      }
    }

    public void ReturnToDefaultFace()
    {
        CurrentFaceIndex = DefaultFaceIndex;
    }
      public void SetDefaultFaceTo(int FaceIndex)
    {
        DefaultFaceIndex = FaceIndex;
    }
    public void SetFace(int FaceIndex)
    {
        CurrentFaceIndex = FaceIndex;
    }

    void FixedUpdate()
    {
        if(FaceSet != null)
        {
        
       
        if(CurrentFaceIndex < FaceSet.FaceElements.Length)
        {
        element = FaceSet.FaceElements[CurrentFaceIndex];
        ImportantMessages = "Current face: " + element.FaceName;
        }
        if(CurrentFaceIndex+1>FaceSet.FaceElements.Length)
        ImportantMessages = "Face not found. Requested index is out of bounds.";

        if(ChangeCheck(CurrentFaceIndex))
        StartCoroutine(UpdateTextures());

        //Default euler angles for the eyebrows. Tuned specifically for Matthew.
        Vector3 BrowEulerAngles;
        BrowEulerAngles.x =  BrowRotationOrigin.x;
        BrowEulerAngles.z =  BrowRotationOrigin.z;
        BrowEulerAngles.y =  (element.RightOuterRotation-0.5f)*180+BrowRotationOrigin.z;
        RightOuterbrow.localRotation = Quaternion.Lerp(RightOuterbrow.localRotation,Quaternion.Euler(BrowEulerAngles), Time.fixedDeltaTime*TransitionSpeed);

        BrowEulerAngles.x =  0;
        BrowEulerAngles.z =  0;
        BrowEulerAngles.y =  element.RightInnerRotation*180;
        RightInnerbrow.localRotation = Quaternion.Lerp(RightInnerbrow.localRotation,Quaternion.Euler(BrowEulerAngles), Time.fixedDeltaTime*TransitionSpeed);    
        
        BrowEulerAngles.x =  BrowRotationOrigin.x;
        BrowEulerAngles.z =  -BrowRotationOrigin.z;
        BrowEulerAngles.y =  (-element.LeftOuterRotation+0.5f)*180-BrowRotationOrigin.z;
        LeftOuterbrow.localRotation = Quaternion.Lerp(LeftOuterbrow.localRotation,Quaternion.Euler(BrowEulerAngles), Time.fixedDeltaTime*TransitionSpeed);

        BrowEulerAngles.x =  0;
        BrowEulerAngles.z =  0;
        BrowEulerAngles.y =  -element.LeftInnerRotation*180;
        LeftInnerbrow.localRotation = Quaternion.Lerp(LeftInnerbrow.localRotation,Quaternion.Euler(BrowEulerAngles), Time.fixedDeltaTime*TransitionSpeed); 
        
        RightOuterbrow.localPosition = Vector3.Lerp(RightOuterbrow.localPosition,element.RightEyebrowPosition+R_browOrigin, Time.fixedDeltaTime * TransitionSpeed);   
        LeftOuterbrow.localPosition = Vector3.Lerp(LeftOuterbrow.localPosition,element.LeftEyebrowPosition+L_browOrigin, Time.fixedDeltaTime * TransitionSpeed);   

        i_Mouth.SetVector("_MainTextureOffset", Vector3.Lerp(i_Mouth.GetVector("_MainTextureOffset"),element.MouthPosition, Time.fixedDeltaTime * TransitionSpeed));
        i_RightEye.SetVector("_MainTextureOffset", Vector3.Lerp(i_RightEye.GetVector("_MainTextureOffset"),element.RightEyePosition, Time.fixedDeltaTime * TransitionSpeed));
        i_LeftEye.SetVector("_MainTextureOffset", Vector3.Lerp(i_LeftEye.GetVector("_MainTextureOffset"),element.LeftEyePosition, Time.fixedDeltaTime * TransitionSpeed));
        i_RightIris.SetVector("_MainTextureOffset", Vector3.Lerp(i_RightIris.GetVector("_MainTextureOffset"),element.RightIrisPosition, Time.fixedDeltaTime * TransitionSpeed));
        i_LeftIris.SetVector("_MainTextureOffset", Vector3.Lerp(i_LeftIris.GetVector("_MainTextureOffset"),element.LeftIrisPosition, Time.fixedDeltaTime * TransitionSpeed));
        
        i_Mouth.SetFloat("_AlphaClipThreshold", Mathf.Lerp(i_Mouth.GetFloat("_AlphaClipThreshold"),element.MouthCutoff, Time.fixedDeltaTime * TransitionSpeed));
        i_RightIris.SetFloat("_AlphaClipThreshold", Mathf.Lerp(i_RightIris.GetFloat("_AlphaClipThreshold"),element.RightIrisCutoff, Time.fixedDeltaTime * TransitionSpeed));
        i_LeftIris.SetFloat("_AlphaClipThreshold", Mathf.Lerp(i_LeftIris.GetFloat("_AlphaClipThreshold"),element.LeftIrisCutoff, Time.fixedDeltaTime * TransitionSpeed));
        if(element.OverrideIrisColor)
        {
        i_LeftIris.SetColor("_MainColor", Color.Lerp(i_LeftIris.GetColor("_MainColor"),element.IrisColor, Time.fixedDeltaTime * TransitionSpeed));
        i_RightIris.SetColor("_MainColor", Color.Lerp(i_RightIris.GetColor("_MainColor"),element.IrisColor, Time.fixedDeltaTime * TransitionSpeed));     
        }
        else
        {
        i_LeftIris.SetColor("_MainColor", Color.Lerp(i_LeftIris.GetColor("_MainColor"),DefaultIrisColor, Time.fixedDeltaTime * TransitionSpeed));
        i_RightIris.SetColor("_MainColor", Color.Lerp(i_RightIris.GetColor("_MainColor"),DefaultIrisColor, Time.fixedDeltaTime * TransitionSpeed));   
        }  
        SetChangeCheckReference(CurrentFaceIndex);

        }
    }

    IEnumerator UpdateTextures()
    {
        yield return new WaitForSeconds(TextureUpdateDelay);
        i_RightEye.SetTexture("_MainTexture",element.RightEye);
        i_LeftEye.SetTexture("_MainTexture",element.LeftEye);
        i_RightIris.SetTexture("_MainTexture",element.RightIris);
        i_LeftIris.SetTexture("_MainTexture",element.LeftIris);
        i_RightEyebrow.SetTexture("_MainTexture",element.RightEyebrow);
        i_LeftEyebrow.SetTexture("_MainTexture",element.LeftEyebrow);
        i_Nose.SetTexture("_MainTexture",element.Nose);
        i_Mouth.SetTexture("_MainTexture",element.Mouth);
        i_Extra.SetTexture("_BaseMap",element.Extra);
    }
}
}