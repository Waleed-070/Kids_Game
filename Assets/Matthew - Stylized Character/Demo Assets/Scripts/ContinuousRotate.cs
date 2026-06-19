using UnityEngine;
namespace Bunssar
{
public class ContinuousRotate : MonoBehaviour
{
    public Vector3 RotationSpeed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.Rotate(RotationSpeed);
    }
}
}
