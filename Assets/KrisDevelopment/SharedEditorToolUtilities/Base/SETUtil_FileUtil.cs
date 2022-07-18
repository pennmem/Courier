////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System.IO;
using U = UnityEngine;

#if UNITY_EDITOR
using E = UnityEditor;
#endif

namespace SETUtil
{
	public static class FileUtil
	{
		internal static string[] standardUnityRootFolders = new []{
			"Assets",
			"Library",
			"Packages",
			"ProjectSettings",
		};

		/// <summary>
		/// Creates a normalized file path
		/// Will assume 'Assets' folder as the default directly-subordinate to project directory (root) folder
		/// </summary>
		public static string CreateFilePathString (string fileName, string fileExtension, bool relativePath = true, string subordinateRootFolderName = "Assets") 
		{
			return CreatePathString(string.Format("{0}.{1}", fileName, fileExtension), relativePath, subordinateRootFolderName);
		}

		///<summary>
		/// Creates a normalized path
		/// Will assume 'Assets' folder as the default directly-subordinate to project directory (root) folder
		/// </summary>
		public static string CreatePathString (string folderName, bool relativePath = true, string subordinateRootFolderName = "Assets") 
		{
			return relativePath ? ParseToLocalUnityPath(folderName, subordinateRootFolderName) : ParseToAbsolutePath(folderName);
		}

		public static void WriteTextToFile (string path, string content, string subordinateRootFolderName = "Assets")
		{
			string _filePath = ParseToAbsolutePath(path, subordinateRootFolderName);
			FileInfo _fileInfo = new FileInfo(_filePath);

			_fileInfo.Directory.Create();
			StreamWriter _writer = File.CreateText(_filePath);
			_writer.Close();
			File.WriteAllText(_filePath, content);
		}

		/// <summary>
		/// Outputs a string result and returns true if it was successful
		/// </summary>
		public static bool ReadTextFromFile (string path, out string content, string subordinateRootFolderName = "Assets")
		{
			string _filePath = ParseToAbsolutePath(path, subordinateRootFolderName);
			content = string.Empty;

			if(File.Exists(_filePath)){
				content = File.ReadAllText(_filePath);
				return true; //read success
			}

			return false; //read failed
		}

		///<summary>
		/// Local paths in unity start with "Assets/".
		/// This method adds that to the path if it isn't already there.
		/// Will assume 'Assets' folder as the default directly-subordinate to project directory (root) folder
		///</summary>
		public static string NormalizeToLocalUnityPath (string localPath, string subordinateRootFolderName = "Assets")
		{
			if(localPath.StartsWith("/")){
				localPath = localPath.TrimStart('/');
			}

			if(localPath.StartsWith(string.Format("{0}/", subordinateRootFolderName))){
				return localPath;
			}

			return string.Format("{0}/{1}", subordinateRootFolderName, localPath);
		}

		///<summary>
		/// Makes the given path compatible with unity local path operations such as AssetDatabase ones
		/// Will assume 'Assets' folder as the default directly-subordinate to project directory (root) folder
		///</summary>
		public static string ParseToLocalUnityPath (string path, string subordinateRootFolderName = "Assets")
		{
			var _applicationPath = U.Application.dataPath;
			if(path.StartsWith(_applicationPath)){
				return NormalizeToLocalUnityPath(path.Remove(0, _applicationPath.Length));
			}

			return NormalizeToLocalUnityPath(path);
		}

		///<summary> 
		/// Creates an absolute path given any path.
		/// Will assume 'Assets' folder as the default directly-subordinate to project directory (root) folder
		///</summary>
		public static string ParseToAbsolutePath (string path, string subordinateRootFolderName = "Assets")
		{
			path = path.Replace('\\', '/');

			// if absolute system path
			if (path.Length > 2 && path.Remove(0, 1).StartsWith(":/")){
				return path;
			}

			// if local path
			var _applicationPath = U.Application.dataPath;
			string _assetsStr = string.Format("{0}/", subordinateRootFolderName);

			path = ParseToLocalUnityPath(path);
			return Path.Combine(_applicationPath, path.Remove(0, _assetsStr.Length));
		}

		/// <summary>
		/// Method that tries to guess what Unity root folder the input path contains
		/// </summary>
		public static string AssumeRootFolderFromPath (string path)
		{
			foreach (var _folderName in standardUnityRootFolders)
			{
				if (path.IndexOf(_folderName, System.StringComparison.InvariantCultureIgnoreCase) >= 0)
				{
					return _folderName;
				}
			}

			throw new System.Exception(string.Format("Failed to find a standard root folder in path string: {0}", path));
		}

		///<summary> Creates a relative to the project root path </summary>
		public static string ParseToRelativePath (string absolutePath, string subordinateRootFolderName = "Assets")
		{
			string removedEnd = string.Format("/{0}", subordinateRootFolderName);
			string projectPath = U.Application.dataPath.Substring(0, U.Application.dataPath.Length - removedEnd.Length);
			
			absolutePath = absolutePath.Replace('\\', '/');
			
			return absolutePath.Replace(projectPath + "/", string.Empty);
		}
	}
}