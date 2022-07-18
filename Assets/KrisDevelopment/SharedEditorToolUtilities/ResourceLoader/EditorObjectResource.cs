////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System.Collections;
using System.Collections.Generic;
using U = UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using E = UnityEditor;
#endif

namespace SETUtil.ResourceLoader
{
	/// <summary>
	/// Retrieve a resource by name
	/// </summary>
	[U.CreateAssetMenu(menuName = "Editor Resource Link/Editor Object Resource")]
	public class EditorObjectResource : U.ScriptableObject
	{
		[System.Serializable]
		private class Resource
		{
			public string resourceName;
			public U.Object obj;
		}

		[U.SerializeField] private Resource[] resources = new Resource[1];

		private static Dictionary<string, U.Object> cache = new Dictionary<string, U.Object>();


		private void OnValidate ()
        {
            foreach (var _resource in resources)
			{
				// Invalidate cache when fields change
				cache.Remove(_resource.resourceName);
            }
        }

		/// <summary>
		/// Works only in Unity Editor
		/// </summary>
		public static T Get<T> (string name) where T : U.Object
		{
#if UNITY_EDITOR
			if (cache.ContainsKey(name))
			{
				return cache[name] as T;
			}

			var _scriptableObjects = E.AssetDatabase.FindAssets("t:EditorObjectResource").Select(
				a => E.AssetDatabase.LoadAssetAtPath<EditorObjectResource>(E.AssetDatabase.GUIDToAssetPath(a)));

			foreach(var _scriptableObject in _scriptableObjects)
			{
				if(_scriptableObject == null)
				{
					continue;
				}

				var _resourceMatch = 
					_scriptableObject.resources.FirstOrDefault(
						a => (string.IsNullOrEmpty(a.resourceName) ? a.obj.name : a.resourceName) == name);

				if (_resourceMatch == null)
				{
					continue;
				}

				cache[name] = _resourceMatch.obj;
				return cache[name] as T;
			}
#endif
			return null;
		}

		/// <summary>
		/// Will clear the cache and tell Unity Editor to unload all unused resources in memory
		/// </summary>
		public static void UnloadResources ()
		{
			cache.Clear();
#if UNITY_EDITOR
			E.EditorUtility.UnloadUnusedAssetsImmediate();
#else
			U.Resources.UnloadUnusedAssets();
#endif
		}
	}
}