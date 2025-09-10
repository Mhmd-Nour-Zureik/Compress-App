using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compresion_RAR.Algorithms.Shannon_Fano
{
    public class DecodeNode
    {
        public DecodeNode Left, Right;
        public byte? Symbol;


        public DecodeNode GetOrCreateLeft()
        {
            if (Left == null) Left = new DecodeNode();
            return Left;
        }
        public DecodeNode GetOrCreateRight()
        {
            if (Right == null) Right = new DecodeNode();
            return Right;
        }
    }
}

