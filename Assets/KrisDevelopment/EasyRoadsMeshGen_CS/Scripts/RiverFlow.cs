using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KrisDevelopment.ERMG
{
	/// <summary>
	/// Note: consider using a shader instead
	/// </summary>
	[AddComponentMenu("Easy Roads Mesh Gen/River Flow")]
	[RequireComponent(typeof(Renderer))]
	public class RiverFlow : MonoBehaviour
	{
		public string[] properties = new string[] { "_MainTex" , "_BumpMap" };

		public Vector2 direction = new Vector2(0, 1);

		private float x = 0;
		private float y = 0;

		private Material initialMaterial;
		private Material targetMaterial;


		void Update()
		{
			if (targetMaterial == null)
			{
				var _r = GetComponent<Renderer>();
				initialMaterial = _r.sharedMaterial;
				targetMaterial =  _r.material;
			}

			x += direction.x * Time.deltaTime;
			y += direction.y * Time.deltaTime;
			x %= 1.0f;
			y %= 1.0f;

			foreach (var p in properties)
			{
				if (targetMaterial.HasProperty(p))
				{
					targetMaterial.SetTextureOffset(p, new Vector2(x, y));
				}
			}
		}

		private void OnDestroy()
		{
			if(initialMaterial != null)
            {
				GetComponent<Renderer>().sharedMaterial = initialMaterial;	
            }
			if (targetMaterial != null)
			{
				SETUtil.SceneUtil.SmartDestroy(targetMaterial);
			}
		}
	}
}
