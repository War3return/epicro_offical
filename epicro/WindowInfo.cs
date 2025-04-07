using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace epicro
{   
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public int ProcessId { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; } // 필요 시

        public override string ToString()
        {
            return $"{Title} (PID: {ProcessId})";
        }
    }
}
