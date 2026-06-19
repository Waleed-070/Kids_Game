using UnityEngine;
using System.Collections;

namespace Bunssar
{
public class PlayFaces : MonoBehaviour
{
    FaceAnimator anim;
    int validFaces;
    public float Delay;
    void Start()
    {
        StartCoroutine(Next());
        anim = GetComponent<FaceAnimator>();
        validFaces = anim.FaceSet.FaceElements.Length;
    }

    IEnumerator Next()
    {
        yield return new WaitForSeconds(Delay);
        if(anim.CurrentFaceIndex+1<validFaces)
        anim.CurrentFaceIndex++;
        else
        anim.CurrentFaceIndex=0;
        StartCoroutine(Next());
    }
}
}
