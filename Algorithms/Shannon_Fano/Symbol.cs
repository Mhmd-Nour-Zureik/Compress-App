using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compresion_RAR.Algorithms.Shannon_Fano
{
    public class Symbol
    {
        public byte Value { get; }
        public int Frequency { get; }
        public Symbol(byte value, int frequency) { Value = value; Frequency = frequency; }
    }
}
