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
    SerializedProperty endCuttingCallbackProp;

    private void OnEnable()
    {
        combinedSliceProp = serializedObject.FindProperty("CombinedSlice");
        sliceToSubMeshProp = serializedObject.FindProperty("SliceToSubMesh");
        sliceMatProp = serializedObject.FindProperty("SliceMat");
        endCuttingCallbackProp = serializedObject.FindProperty("EndCuttingCallback");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(combinedSliceProp);
        if (combinedSliceProp.boolValue)
        {
            EditorGUILayout.PropertyField(sliceToSubMeshProp);
            if (sliceToSubMeshProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sliceMatProp);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.PropertyField(endCuttingCallbackProp);
        serializedObject.ApplyModifiedProperties();
    }
}