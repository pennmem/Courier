using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BuildingController))]
[CanEditMultipleObjects]
public class BuildingEditor : Editor
{
    // Start is called before the first frame update
    public override void OnInspectorGUI () {
        DrawDefaultInspector();
        BuildingController building = (BuildingController) target;
        building.updateFloors();
        if(GUILayout.Button("Generate")) {
            building.Generate();
        }
        if(GUILayout.Button("Add Floor")) {
            building.addFloor(1);
            building.updateTopPosition();
        }
        if(GUILayout.Button("Remove Floor")) {
            building.removeFloor(-1);
        building.updateTopPosition();
        }
         
    }
}
