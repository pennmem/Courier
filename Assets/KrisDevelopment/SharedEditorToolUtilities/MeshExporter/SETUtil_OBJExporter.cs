////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System.IO;
using System.Text;
using System.Collections.Generic;
using U = UnityEngine;

namespace SETUtil.MeshExporter
{
	public static class OBJExporter
	{
		///<summary> Generate a data string compatible with the OBJ file format </summary>
		public static string MeshToString (U.Mesh mesh, U.Material[] materials)
		{
			U.Debug.Assert(mesh.isReadable, "Trying to export an unreadable mesh!");

			StringBuilder _output = new StringBuilder();

			_output.Append("o ").Append(mesh.name).Append("\n");
			foreach(U.Vector3 v in mesh.vertices) {
				_output.Append(string.Format("v {0} {1} {2}\n", v.x, v.y, v.z));
			}

			_output.Append("\n");
			foreach(U.Vector3 v in mesh.normals) {
				_output.Append(string.Format("vn {0} {1} {2}\n", v.x, v.y, v.z));
			}
			
			_output.Append("\n");
			foreach(U.Vector3 v in mesh.uv) {
				_output.Append(string.Format("vt {0} {1}\n", v.x, v.y));
			}

			for (int m = 0; m < mesh.subMeshCount; m++) {
				_output.Append("\n");
				_output.Append("usemtl ").Append(materials[m].name).Append("\n");
				_output.Append("usemap ").Append(materials[m].name).Append("\n");
	
				int[] _triangles = mesh.GetTriangles(m);
				for (int i = 0; i < _triangles.Length; i += 3) {
					_output.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", 
						_triangles[i] + 1, _triangles[i + 1] + 1, _triangles[i + 2] + 1));
				}
			}
			return _output.ToString();
		}

		public static void ExportMesh (string path, U.Mesh mesh, U.Material[] materials)
		{
			if(!Path.GetExtension(path).Equals(".obj", System.StringComparison.OrdinalIgnoreCase))
				throw new System.Exception("Target file extension must be .obj");

			FileUtil.WriteTextToFile(path, MeshToString(mesh, materials));
		}


		/// <summary>
		/// Export the mesh of the game object to an OBJ file. 
		/// If worldOrigin option is enabled all mesh vertices will be relative to the world position.
		/// </summary>
		public static void ExportObject (string path, U.GameObject gameObject, bool worldOrigin = false) 
		{
			U.Mesh _mesh = null;
			List<U.Material> _materials = new List<U.Material>();

			var _filter = gameObject.GetComponent<U.MeshFilter>();
			if(_filter != null) {
				_mesh = _filter.sharedMesh;

				U.Debug.Assert(_mesh.isReadable, "Trying to export an unreadable mesh!");

				var _renderer = gameObject.GetComponent<U.Renderer>();
				if(_renderer != null) {
					_materials.AddRange(_renderer.sharedMaterials);
				}

				if (!worldOrigin)
				{
					// simply export the mesh
					ExportMesh(path, _mesh, _materials.ToArray());
				}
				else
				{
					// create a mesh with world-relative vertices
					var _localToWorldMtx = gameObject.transform.localToWorldMatrix;
					var _vertices = _mesh.vertices;
					
					for(int i = 0; i < _vertices.Length; i++)
					{
						_vertices[i] = _localToWorldMtx.MultiplyPoint3x4(_vertices[i]);
					}

					var _meshCopy = U.Object.Instantiate(_mesh);
					_meshCopy.vertices = _vertices;
					ExportMesh(path, _mesh, _materials.ToArray());
					SceneUtil.SmartDestroy(_meshCopy);
				}
			}else{
				U.Debug.LogError("[ERROR ExportObject] Error while exporting object: No mesh found to export!");
			}
		}
	}
}