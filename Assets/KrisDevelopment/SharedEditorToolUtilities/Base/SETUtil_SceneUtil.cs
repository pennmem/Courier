////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System.Collections.Generic;
using U = UnityEngine;
using Gl = UnityEngine.GUILayout;

#if UNITY_EDITOR
using E = UnityEditor;
using EGl = UnityEditor.EditorGUILayout;
#endif

namespace SETUtil
{
    /// <summary>
    /// Collection of scene object utilities
    /// </summary>
    public static class SceneUtil
    {
        // OBJECT TERMINATION HANDLING --------------------------------------

        /// <summary>
        /// Calls appropriate destroy method based on the build conditions
        /// </summary>
        public static void SmartDestroy<T>(T target) where T : U.Object
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (E.AssetDatabase.Contains(target) && ValidatePrefabTermination(target))
            {
                E.AssetDatabase.DeleteAsset(E.AssetDatabase.GetAssetPath(target));
            }
            else
            {
                if (U.Application.isPlaying)
                    U.Object.Destroy(target);
                else
                    U.Object.DestroyImmediate(target, ValidatePrefabTermination(target));
            }
#else
				U.Object.Destroy(target);
#endif
        }

        ///<summary> 
        ///Calls SmartDestroy for reach element in the array 
        ///</summary>
        public static void DestroyArray<T>(ref T[] instances) where T : U.Object
        {
            foreach (T instance in instances)
                SmartDestroy(instance);

            instances = new T[0];
        }

        ///<summary> 
        ///Method meant to prevent unwanted asset termination 
        ///</summary>
        public static bool ValidatePrefabTermination<T>(T obj) where T : U.Object
        {
            if (obj == null)
            {
                U.Debug.Log("[ValidatePrefabTermination] NULL object");
                return false;
            }

            bool _validTermination = false;

#if UNITY_EDITOR
            bool _isPrefab = false;
            U.Object _prefab = null;

#if UNITY_2018_3_OR_NEWER
            _prefab = E.PrefabUtility.GetPrefabInstanceHandle(obj);
#else
			_prefab = E.PrefabUtility.GetPrefabObject(obj);
#endif

            _isPrefab = _prefab != null && _prefab.Equals(obj);

            if (!_isPrefab || !E.EditorUtility.DisplayDialog("Confirm action", "Seems like object " + obj.name + " is a prefab! This action will permanently delete the asset from your project!\nContinue?", "Yes", "No"))
                _validTermination = true;
#else
				_validTermination = true;
#endif

            return _validTermination;
        }

        // GAME OBJECT CREATION -------------------------------------

        ///<summary>
        /// Spawns a prefab-linked instance if in editor and normal instance during run-time, at default coordinates.
        /// </summary>
        public static T Instantiate<T>(T unityObject) where T : U.Component
        {
            return Instantiate(unityObject.gameObject, U.Vector3.zero, U.Quaternion.identity).GetComponent<T>();
        }

        ///<summary> 
        ///Spawns a prefab-linked instance if in editor and normal instance during run-time, at default coordinates. 
        ///</summary>
        public static U.GameObject Instantiate(U.GameObject prefab)
        {
            return Instantiate(prefab, U.Vector3.zero, U.Quaternion.identity);
        }

        ///<summary> 
        ///Spawns a prefab-linked instance if in editor and normal instance during run-time, with default rotation.
        ///</summary>
        public static U.GameObject Instantiate(U.GameObject prefab, U.Vector3 position)
        {
            return Instantiate(prefab, position, U.Quaternion.identity);
        }

        ///<summary> 
        ///Spawns a prefab-linked instance if in editor and normal instance during run-time 
        ///</summary>
        public static U.GameObject Instantiate(U.GameObject prefab, U.Vector3 position, U.Quaternion rotation, U.Transform parent = null)
        {
            U.Debug.Assert(prefab != null, "Null prefab parameter");

            U.GameObject _instance = null;
#if UNITY_EDITOR
            if (!U.Application.isPlaying && E.PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                _instance = (U.GameObject)E.PrefabUtility.InstantiatePrefab(prefab, parent);
                _instance.transform.position = position;
                _instance.transform.rotation = rotation;
                return _instance;
            }
#endif
            _instance = U.GameObject.Instantiate(prefab, position, rotation, parent);
            return _instance;
        }

        // GO MANAGEMENT ------------------------------------------------

        ///<summary> 
        ///Returns all children inside the given root transform. 
        ///</summary>
        public static U.Transform[] CollectAllChildren(U.Transform root, bool includeParent = false)
        {
            if (includeParent)
            {
                return root.GetComponentsInChildren<U.Transform>(true);
            }

            List<U.Transform> _children = new List<U.Transform>();
            IterateChildren(ref _children, root);
            _children.Remove(root);
            return _children.ToArray();
        }

        /// <summary>
        /// Used by CollectAllChildren() to realize recursive children iteration
        /// </summary>
        private static void IterateChildren(ref List<U.Transform> children, U.Transform currentNode)
        {
            children.Add(currentNode);
            foreach (U.Transform child in currentNode)
                IterateChildren(ref children, child);
        }

        /// <summary>
        /// Tries to find the top-most root of the given GameObject 
        /// </summary>
        public static U.GameObject FindTopRoot(U.GameObject gameObject)
        {
            U.Transform root = gameObject.transform;

            while (root.parent != null)
            {
                root = root.parent;
            }

            return root.gameObject;
        }


        /// <summary>
        /// Returns true if the object is not a prefab instance or doesn't need to be opened to be edited.
        /// Error prevention.
        /// </summary>
        public static bool CanEditGameObjectState(U.GameObject gameObject)
        {
            return CanEditGameObjectState(gameObject, out string _);
        }

        /// <summary>
        /// Returns true if the object is not a prefab instance or doesn't need to be opened to be edited.
        /// Error prevention. Outputs the error.
        /// </summary>
        public static bool CanEditGameObjectState(U.GameObject gameObject, out string error)
        {

#if UNITY_2018_1_OR_NEWER && UNITY_EDITOR
            if (E.PrefabUtility.GetPrefabInstanceStatus(gameObject) == E.PrefabInstanceStatus.Connected)
            {
                error = "Open Prefab before editing: " + gameObject?.ToString();
                return false;
            }

            if (E.PrefabUtility.GetPrefabAssetType(gameObject) == E.PrefabAssetType.Model)
            {
                error = "This asset is a model. Unpack before editing: " + gameObject?.ToString();
                return false;
            }
#endif
            error = null;
            return true;
        }
    }
}