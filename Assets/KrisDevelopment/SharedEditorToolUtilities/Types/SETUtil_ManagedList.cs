
////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System.Collections.Generic;
using System.Linq;

namespace SETUtil.Types
{
	///<summary> A list that auto removes old elements (call .Age() method)</summary>
	public class TimedList<T> where T : class
	{
		public class TimedElement<J>
		{
			public J el = default(J);

			public int
				life = 0;

			public TimedElement(J element, int life)
			{
				this.el = element;
				this.life = life;
			}
		}

		private static int MAX_LIFE = 5; //if an element lives this long then delete it
		List<TimedElement<T>> elements = new List<TimedElement<T>>();

		public int Count { get { return elements.Count; } }
		public T this[int i] { get { return elements[i].el; } }

		//METHODS:
		private void Push(TimedElement<T> element)
		{
			elements.Add(element);
		}

		public T Pop()
		{
			var popElement = elements.LastOrDefault();
			if (popElement != null) {
				return popElement.el;
			}

			return default(T);
		}

		/// <summary> Push new or overwrite existing </summary>
		public void SmartPush(T element)
		{
			var foundExisting = elements.FirstOrDefault(a => a.el == element);
			if (foundExisting == null) {
				Push(new TimedElement<T>(element, 0));
			} else {
				foundExisting.life = 0;
			}
		}

		/// <summary>
		/// Age the elements
		/// </summary>
		public void Tick()
		{
			for (int i = 0; i < elements.Count; elements[i].life++, i++) ;
			Trim();
		}

		private void Trim()
		{
			//check for life and delete variables that are not in use
			var _el = elements.FirstOrDefault(a => a.life > MAX_LIFE);
			if (_el != null)
				elements.Remove(_el);
			elements.RemoveAll(a => a.el == null);
		}
	}
}