using UnityEngine;

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class Memo : MonoBehaviour
{
    [TextArea(3, 10)]
    public string memoText = "Enter your note here";
}
