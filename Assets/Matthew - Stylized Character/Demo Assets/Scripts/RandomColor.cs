using UnityEngine;
using System.Collections;
namespace Bunssar
{
public class RandomColor : MonoBehaviour
{
    Material mat;
    public float interval;
    public bool chooseFromColors;
    public Color[] colors;
    public bool sync;
    public bool syncOnlyColor;
    public Renderer[] syncWith;
    public Renderer[] syncOnlyColorWith;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    
        mat = new Material(gameObject.GetComponent<Renderer>().sharedMaterial);
        gameObject.GetComponent<Renderer>().sharedMaterial = mat;
        if(sync)
        {
            foreach(Renderer r in syncWith)
            {
               r.material = mat;
            }
        }
        StartCoroutine(Randomize());
    }
    IEnumerator Randomize()
    {
        yield return new WaitForSeconds(interval);
        if(!chooseFromColors)
        {
        mat.SetColor("_MainColor", Random.ColorHSV(0,1,1,1,1,1));
        mat.SetColor("_SecondaryColor", Random.ColorHSV(0,1,1,1,0,1));
        }
        else
        {
        mat.SetColor("_MainColor", colors[Random.Range(0,colors.Length)]);
        mat.SetColor("_SecondaryColor", colors[Random.Range(0,colors.Length)]);
        }
          if(syncOnlyColor)
        {
            foreach(Renderer r in syncOnlyColorWith)
            {
               r.material.SetColor("_MainColor", mat.GetColor("_MainColor"));
            }
        }
        StartCoroutine(Randomize());
    }
}
}