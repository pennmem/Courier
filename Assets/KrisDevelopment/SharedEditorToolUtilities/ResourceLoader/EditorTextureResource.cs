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
	[U.CreateAssetMenu(menuName = "Editor Resource Link/Editor Texture Resource")]
	public class EditorTextureResource : U.ScriptableObject
	{
		[System.Serializable]
		private class Resource
		{
			public string resourceName;
			public U.Texture2D texture;
		}

		[U.SerializeField] private Resource[] resources = new Resource[1];

		private static Dictionary<string, U.Texture2D> cache = new Dictionary<string, U.Texture2D>();


		/// <summary>
		/// Works only in Unity Editor
		/// </summary>
		public static U.Texture2D Get (string name)
		{
#if UNITY_EDITOR
			if (cache.ContainsKey(name) && cache[name] != null)
			{
				return cache[name];
			}

			var _scriptableObjects = E.AssetDatabase.FindAssets($"t:{nameof(EditorTextureResource)}").Select(
				a => E.AssetDatabase.LoadAssetAtPath<EditorTextureResource>(E.AssetDatabase.GUIDToAssetPath(a)));

			foreach(var _scriptableObject in _scriptableObjects)
			{
				if(_scriptableObject == null)
				{
					continue;
				}

				var _resourceMatch = 
					_scriptableObject.resources.FirstOrDefault(
						a => (string.IsNullOrEmpty(a.resourceName) ? a.texture.name : a.resourceName) == name);

				if (_resourceMatch == null)
				{
					continue;
				}

				cache[name] = _resourceMatch.texture;
				return cache[name];
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
