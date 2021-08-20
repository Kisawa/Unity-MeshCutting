using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MeshCuttingInfo : MonoBehaviour
{
    public MeshCutting.ParentAction _ParentAction = MeshCutting.ParentAction.Destroy;
    public bool CombinedSlice = true;
    public bool SliceToSubMesh = false;
    public Material SliceMat;

    public TwoGameObjectEvent EndCuttingCallback;
}

[System.Serializable]
public class TwoGameObjectEvent : UnityEvent<GameObject, GameObject> { }