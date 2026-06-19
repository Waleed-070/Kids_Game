using UnityEngine;
namespace Bunssar
{
[CreateAssetMenu(fileName = "New Face Set", menuName = "Bunssar/Facial Animation/Face Set", order = 1)]
public class FaceSet : ScriptableObject
{ 
    [Header("<size=40>Face Editor")]
    [Header("<color=#AAAAAA>Welcome to <color=white>Face Editor<color=#AAAAAA>!")]
    [Header("<color=#AAAAAA>Use this tool to easily create faces for Matthew.")]
    [Header("<size=25>Faces")]
    public FaceElement[] FaceElements;
   
}
}