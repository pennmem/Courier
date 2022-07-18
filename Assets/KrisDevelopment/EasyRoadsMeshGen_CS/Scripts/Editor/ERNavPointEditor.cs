#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Net.Sockets;

namespace KrisDevelopment.ERMG
{
    [CustomEditor(typeof(ERNavPoint))]
    public class ERNavPointEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ERNavPoint myScirpt = (ERNavPoint)target;

            if (myScirpt.assignedMeshGen)
            {
                SETUtil.EditorUtil.BeginColorPocket(Color.green);

                if (GUILayout.Button("Add Nav Point"))
                {
                    myScirpt.NavPointAction(NavAction.Add);
                }

                SETUtil.EditorUtil.EndColorPocket();

                if (myScirpt.lastKnownIndex > -1 && GUILayout.Button("Delete Nav Point"))
                    myScirpt.NavPointAction(NavAction.Delete);
            }

            if (myScirpt.assignedMeshGen)
            {
                EditorGUILayout.HelpBox("Locking the width will record the current value of the delta width and let ERMG use that instead.", MessageType.None);
                GUILayout.BeginHorizontal();
                {
                    string lockLabel = (myScirpt.lockSize) ?
                        string.Format("Unlock Width (Locked!) ({0})", myScirpt.lockedHalfWidth.ToString("0.00"))
                        : "Lock Width";

                    if (GUILayout.Button(lockLabel))
                    {
                        myScirpt.LockSize(!myScirpt.lockSize);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
#endif
