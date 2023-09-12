using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace VirtualMaze.Assets.Scripts.Misc
{
    public abstract class KahanSummation
    {
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void Sum(ref decimal sum, ref decimal c, decimal item) {
        decimal y = item - c;
        decimal t = sum + y;
        c = (t - sum) - y;
        sum = t;
        }
    }
}