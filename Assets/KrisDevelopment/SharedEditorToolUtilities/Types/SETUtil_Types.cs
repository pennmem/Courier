////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System;
using System.Collections.Generic;
using System.Text;
using U = UnityEngine;
using Gl = UnityEngine.GUILayout;

#if UNITY_EDITOR
using E = UnityEditor;
#endif

namespace SETUtil.Types
{
	//INTERFACES:
	
	public interface iOrderedComponent
	{
		int OrderIndex();
	}

	public interface iDrawableProperty
	{
		void DrawAsProperty();
	}

	//CLASSES:
	
	/// <summary>
	/// Struct representation for the orientation information stored in a Unity Transform
	/// </summary>
	[Serializable]
	public struct TransformData
	{
		public U.Vector3 position;
		public U.Quaternion rotation;

		public U.Vector3 right { get { return rotation * U.Vector3.right; } }
		public U.Vector3 up { get { return rotation * U.Vector3.up; } }
		public U.Vector3 forward { get { return rotation * U.Vector3.forward; } }
		public U.Vector3 left { get { return rotation * -U.Vector3.right; } }
		public U.Vector3 down { get { return rotation * -U.Vector3.up; } }
		public U.Vector3 back { get { return rotation * -U.Vector3.forward; } }

		//CONSTRUCTORS
		public TransformData(U.Vector3 position, U.Quaternion rotation)
		{
			this.position = position;
			this.rotation = rotation;
		}

		public TransformData(U.Transform tr)
		{
			position = tr.position;
			rotation = tr.rotation;
		}

		public TransformData(TransformData dt)
		{
			position = dt.position;
			rotation = dt.rotation;
		}

		//INSTANCE METHODS:
		public void Debug()
		{
			U.Debug.Log(string.Format("[SETUtil.TransformData] Position: {0} Rotation: {1}", position, rotation));
		}

		public void Set(U.Vector3 position, U.Quaternion rotation)
		{
			this.position = position;
			this.rotation = rotation;
		}

		public void Set(U.Transform tr)
		{
			position = tr.position;
			rotation = tr.rotation;
		}

		public void Set(TransformData trdt)
		{
			position = trdt.position;
			rotation = trdt.rotation;
		}

		//STATIC METHODS:
		public static TransformData Lerp(TransformData t1, TransformData t2, float lerp)
		{
			TransformData _t3 = new TransformData();
			_t3.position = U.Vector3.Lerp(t1.position, t2.position, lerp);
			_t3.rotation = U.Quaternion.Lerp(t1.rotation, t2.rotation, lerp);
			return _t3;
		}

		public static void DrawDebug(TransformData tr)
		{
			U.Debug.DrawRay(tr.position, tr.right, U.Color.red);
			U.Debug.DrawRay(tr.position, tr.up, U.Color.green);
			U.Debug.DrawRay(tr.position, tr.forward, U.Color.blue);
		}
	}

	/// <summary>
	/// Extended version of transform data that contaisn a scale field
	/// </summary>
	[Serializable]
	public struct TransformDataScale
	{
		public U.Vector3 position;
		public U.Quaternion rotation;
		public U.Vector3 scale;

		public U.Vector3 right { get { return rotation * U.Vector3.right; } }
		public U.Vector3 up { get { return rotation * U.Vector3.up; } }
		public U.Vector3 forward { get { return rotation * U.Vector3.forward; } }
		public U.Vector3 left { get { return rotation * -U.Vector3.right; } }
		public U.Vector3 down { get { return rotation * -U.Vector3.up; } }
		public U.Vector3 back { get { return rotation * -U.Vector3.forward; } }


		//CONSTRUCTORS
		public TransformDataScale(U.Vector3 position, U.Quaternion rotation, U.Vector3 scale)
		{
			this.position = position;
			this.rotation = rotation;
			this.scale = scale;
		}

		public TransformDataScale(U.Transform tr)
		{
			position = tr.position;
			rotation = tr.rotation;
			scale = tr.localScale;
		}

		public TransformDataScale(TransformDataScale dt)
		{
			position = dt.position;
			rotation = dt.rotation;
			scale = dt.scale;
		}

		public TransformDataScale(TransformData dt)
		{
			position = dt.position;
			rotation = dt.rotation;
			scale = U.Vector3.one;
		}

		//INSTANCE METHODS:
		public void Debug()
		{
			U.Debug.Log(string.Format("[SETUtil.TransformData] Position: {0} Rotation: {1} Scale {2}", position, rotation, scale));
		}

		public void Set(U.Vector3 position, U.Quaternion rotation, U.Vector3 scale)
		{
			this.position = position;
			this.rotation = rotation;
			this.scale = scale;
		}

		public void Set(U.Transform tr)
		{
			position = tr.position;
			rotation = tr.rotation;
			scale = tr.localScale;
		}

		public void Set(TransformDataScale trdt)
		{
			position = trdt.position;
			rotation = trdt.rotation;
			scale = trdt.scale;
		}

		//STATIC METHODS:
		public static TransformDataScale Lerp(TransformDataScale t1, TransformDataScale t2, float lerp)
		{
			TransformDataScale _t3 = new TransformDataScale();
			_t3.position = U.Vector3.Lerp(t1.position, t2.position, lerp);
			_t3.rotation = U.Quaternion.Lerp(t1.rotation, t2.rotation, lerp);
			_t3.scale = U.Vector3.Lerp(t1.scale, t2.scale, lerp);
			return _t3;
		}

		public static implicit operator TransformData(TransformDataScale tds)
		{
			return new TransformData(tds.position, tds.rotation);
		}

		public static implicit operator TransformDataScale(TransformData td)
		{
			return new TransformDataScale(td.position, td.rotation, U.Vector3.one);
		}

		public static void DrawDebug(TransformDataScale tr)
		{
			U.Debug.DrawRay(tr.position, tr.right * tr.scale.x, U.Color.red);
			U.Debug.DrawRay(tr.position, tr.up * tr.scale.y, U.Color.green);
			U.Debug.DrawRay(tr.position, tr.forward * tr.scale.z, U.Color.blue);
		}
	}

	/// <summary>
	/// Stores conditions and returns true if any one of them is met
	/// </summary>
	[Serializable]
	public class ConditionCapacitor
	{
		//Can store many boolean conditions and if one of them is true, it returns true when doing implicit boolean check

		public bool this[int index] { get { return Get(index); } set { Set(value, index); } }
		public int Length { get { return conditions.Length; } }
		public bool[] conditions;

		//CONSTRUCTORS:
		
		public ConditionCapacitor()
		{
			conditions = new bool[0];
		}

		public ConditionCapacitor(params bool[] conditions)
		{
			Set(conditions);
		}

		//OPERATORS:
		
		public static implicit operator bool(ConditionCapacitor cc)
		{
			for (int i = 0; i < cc.Length; i++)
				if (cc[i])
					return true;

			return false;
		}

		//METHODS:
		public void Set(params bool[] conditions)
		{
			this.conditions = new bool[conditions.Length];
			for (int i = 0; i < conditions.Length; this.conditions[i] = conditions[i], i++) ;
		}

		public void Set(bool condition, int index)
		{
			if (index < conditions.Length && index >= 0)
				conditions[index] = condition;
			U.Debug.LogError("[ConditionCapacitor.Set ERROR] Index out of bounds!");
		}

		public bool[] Get()
		{
			return conditions;
		}

		public bool Get(int index)
		{
			if (index < conditions.Length && index >= 0)
				return conditions[index];
			U.Debug.LogError("[ConditionCapacitor.Get ERROR] Index out of bounds!");
			return false;
		}
	}


	public class OrderElement
	{
		public Type type;
		public int order = 0;

		public OrderElement(Type type, int order)
		{
			this.type = type;
			this.order = order;
		}
	}

	public class ComponentOrderList
	{
		static readonly string ADD_ERROR_PREFIX = "[ComponentOrderList.AddElement ERROR]";

		List<OrderElement> list;

		public ComponentOrderList()
		{
			//EMPTY LIST
			list = new List<OrderElement>();
		}

		public ComponentOrderList(List<OrderElement> list)
		{
			this.list = list;
		}

		public void Print()
		{
			StringBuilder _log = new StringBuilder();
			_log.Append("[ComponentOrderList] Component Order List:\n");
			for(int i = 0; i < list.Count; _log.Append(i).Append(' ').Append(list[i].type).Append('\n'), i++);
			U.Debug.Log(_log.ToString());
		}

		public void AddElements(U.Component[] components)
		{
			for (int i = 0; i < components.Length; i++) {
				AddElement(components[i]);
			}
		}

		public void AddElement(U.Component component)
		{
			if (component == null) {
				U.Debug.Log(ADD_ERROR_PREFIX + " null component.");
				return;
			}

			AddElement(component.GetType(), list[list.Count - 1].order + 1);
		}

		public void AddElement(Type type, int order)
		{
			//Define a new order position in the list for the specified type
			for (int i = 0; i < list.Count; i++)
				if (list[i].type == type) {
					U.Debug.Log(ADD_ERROR_PREFIX + " Element of type " + type + " already exists. If it were added to the list, it would make element " + i + " obsolete. Aborting.");
					return;
				}

			list.Add(new OrderElement(type, order));
		}

		public int EvaluateElement(Type type)
		{
			//return the order index of the type
			int _orderIndex = (list.Count > 0) ? list[list.Count - 1].order + 1 : 0;
			bool _foundMatch = false;

			for (int i = 0; i < list.Count; i++) {
				if (list[i].type == type) {
					_orderIndex = list[i].order;
					_foundMatch = true;
				} else if (!_foundMatch)
					if (list[i].type.IsAssignableFrom(type))
						_orderIndex = list[i].order;
			}

			return _orderIndex;
		}
	}

	/// <summary>
	/// An alternative to SETUtil.EditorUtil.BeginColorPocket that utilizes the IDisposable interface.
	/// Modifying GUI colors within this block will affect only the elements within its scope.
	/// Keeps track and restores original GUI colors when disposed.
	/// Supports nesting.
	/// </summary>
	public sealed class ColorPocket : IDisposable
	{
		public ColorPocket()
		{
			SETUtil.EditorUtil.BeginColorPocket();
		}

		public ColorPocket(U.Color color)
		{
			SETUtil.EditorUtil.BeginColorPocket(color);
		}

		public void Dispose()
		{
			SETUtil.EditorUtil.EndColorPocket();
		}
	}

#if UNITY_EDITOR
	/// <summary>
	/// Shows an editor window with some log.
	/// That's here so it can be called from non-editor code!
	/// </summary>
	internal class OperationLogWindow : E.EditorWindow
	{
		private string log;
		private string displayString;
		private string outputPath;
		private U.Vector2 scrollView = U.Vector2.zero;
		
		public static OperationLogWindow ShowWindow(string title, StringBuilder log, int maxDisplayChars = 64000)
		{
			var _win = EditorUtil.ShowUtilityWindow<OperationLogWindow>(title);
			_win.log = log.ToString();
			_win.displayString = log.Length > maxDisplayChars ? _win.log.Substring(0, U.Mathf.Min(_win.log.Length, maxDisplayChars)) : _win.log;
			_win.outputPath = U.Application.dataPath + "/operation_log.txt";
			return _win;
		}

		private void OnGUI()
		{
			scrollView = Gl.BeginScrollView(scrollView);
			{
				Gl.TextArea(displayString);
				
				if (displayString.Length != log.Length) {
					Gl.TextArea("...\n(string too long to display, save to log to view)", E.EditorStyles.miniLabel);
				}
			}
			Gl.EndScrollView();
			
			EditorUtil.HorizontalRule();
			
			Gl.BeginHorizontal();
			{
				Gl.Label("Save Log:", Gl.ExpandWidth(false));
				outputPath = Gl.TextField(outputPath);
			}
			Gl.EndHorizontal();

			Gl.BeginHorizontal();
			{
				if (Gl.Button("Save")) {
					PrintLog();
				}

				if (Gl.Button("Close")) {
					Close();
				}
			}
			Gl.EndHorizontal();
		}

		private void PrintLog()
		{
			FileUtil.WriteTextToFile(outputPath, log);
			E.EditorUtility.DisplayDialog("Done!", "File saved to " + outputPath, "Ok");
		}
	}
#endif
}