using UnityEngine;

public sealed class ParametricControlPointHandle : MonoBehaviour
{
    public int Index { get; private set; }

    public void SetIndex(int index)
    {
        Index = index;
        gameObject.name = "Control Point " + index;
    }
}
