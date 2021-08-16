using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshCutting))]
public class MeshCuttingEditor : Editor
{
    SerializedProperty screenCutTestProp;
    SerializedProperty maxStepProp;
    SerializedProperty maxDepthProp;
    SerializedProperty layerMaskProp;
    SerializedProperty combinedSliceProp;
    SerializedProperty sliceToSubMeshProp;
    SerializedProperty sliceMatProp;
    string[] layers;

    private void OnEnable()
    {
        screenCutTestProp = serializedObject.FindProperty("ScreenCutTest");
        layerMaskProp = serializedObject.FindProperty("Mask");
        maxStepProp = serializedObject.FindProperty("MaxStep");
        maxDepthProp = serializedObject.FindProperty("MaxDepth");
        combinedSliceProp = serializedObject.FindProperty("CombinedSlice");
        sliceToSubMeshProp = serializedObject.FindProperty("SliceToSubMesh");
        sliceMatProp = serializedObject.FindProperty("SliceMat");
        List<string> _layers = new List<string>();
        _layers.AddRange(UnityEditorInternal.InternalEditorUtility.layers);
        _layers.Insert(3, "");
        _layers.Insert(6, "");
        _layers.Insert(7, "");
        layers = _layers.ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(screenCutTestProp);
        if (screenCutTestProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(maxStepProp);
            EditorGUILayout.PropertyField(maxDepthProp);
            EditorGUI.indentLevel--;
        }
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Default Setting:");
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("LayerMask:", GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.35f));
        layerMaskProp.intValue = EditorGUILayout.MaskField(layerMaskProp.intValue, layers);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(combinedSliceProp);
        EditorGUILayout.PropertyField(sliceToSubMeshProp);
        if(sliceToSubMeshProp.boolValue)
            EditorGUILayout.PropertyField(sliceMatProp);
        serializedObject.ApplyModifiedProperties();
    }
}