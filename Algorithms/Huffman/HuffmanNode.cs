using System;
using System.Collections.Generic;

namespace Compresion_RAR.Algorithms
{
    public class HuffmanNode : IComparable<HuffmanNode>
    {
        public byte? Character { get; set; }
        public int Frequency { get; set; }
        public HuffmanNode Left { get; set; }
        public HuffmanNode Right { get; set; }

        private static int _idCounter = 0;
        public readonly int Id = _idCounter++;

        public int CompareTo(HuffmanNode other)
        {
            int freqComparison = Frequency.CompareTo(other.Frequency);
            return freqComparison != 0
                ? freqComparison
                : Character.HasValue && other.Character.HasValue ? Character.Value.CompareTo(other.Character.Value) : Id.CompareTo(other.Id);
        }
    }
}