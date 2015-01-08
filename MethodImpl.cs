using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emitter
{
	public class MethodImpl
	{
		public string MethodName { get; set; }
		public Delegate Implementation { get; set; }
	}
}
