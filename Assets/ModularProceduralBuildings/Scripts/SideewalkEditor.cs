using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SidewalkController))]
[CanEditMultipleObjects]
public class SidewalkEditor : Editor
{
    // Start is called before the first frame update
    public override void OnInspectorGUI () {
        DrawDefaultInspector();
        SidewalkController sidewalk = (SidewalkController) target;
        sidewalk.setSize();
        if(GUILayout.Button("Increase Size")) {
            sidewalk.increaseSize(true);
        }
        if(GUILayout.Button("Decrease Size")) {
            sidewalk.decreaseSize(true);
        }
        if(GUILayout.Button("Increase Width")) {
            sidewalk.updateWidth(1);
        }
        if(GUILayout.Button("Decrease Width")) {
            sidewalk.updateWidth(-1);
        }   
        if(GUILayout.Button("Add Corner")) {
            sidewalk.addCorner();
        }
        if(GUILayout.Button("Remove Corner")) {
            sidewalk.removeCorner();
        }
    }
}
