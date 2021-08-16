using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshCuttingInfo))]
public class MeshCuttingInfoEditor : Editor
{
    SerializedProperty combinedSliceProp;
    SerializedProperty sliceToSubMeshProp;
    SerializedProperty sliceMatProp;

    private void OnEnable()
    {
        combinedSliceProp = serializedObject.FindProperty("CombinedSlice");
        sliceToSubMeshProp = serializedObject.FindProperty("SliceToSubMesh");
        sliceMatProp = serializedObject.FindProperty("SliceMat");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(combinedSliceProp);
        EditorGUILayout.PropertyField(sliceToSubMeshProp);
        if (sliceToSubMeshProp.boolValue)
            EditorGUILayout.PropertyField(sliceMatProp);
        serializedObject.ApplyModifiedProperties();
    }
}