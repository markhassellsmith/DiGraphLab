using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DiGraphLab.Harmony
{
    public readonly struct PitchClassSet : IEnumerable<int>
    {
        private readonly int _bits;

        public PitchClassSet(int bits)
        {
            _bits = bits & 0xFFF; // keep 12 bits
        }

        public static PitchClassSet Empty => new PitchClassSet(0);

        public static PitchClassSet FromPcs(IEnumerable<int> pcs)
        {
            int bits = 0;
            foreach (var p in pcs ?? Array.Empty<int>())
            {
                var v = ((p % 12) + 12) % 12;
                bits |= 1 << v;
            }
            return new PitchClassSet(bits);
        }

        public bool Has(int pc) => ((_bits >> (pc % 12)) & 1) != 0;

        public int Count => System.Numerics.BitOperations.PopCount((uint)_bits);

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < 12; i++)
                if (((_bits >> i) & 1) != 0)
                    yield return i;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            return string.Join(",", this.Select(i => i.ToString()));
        }

        public int ToBits() => _bits;
    }
}
