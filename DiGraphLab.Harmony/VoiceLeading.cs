using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGraphLab.Harmony
{
    public static class VoiceLeading
    {
        /// <summary>
        /// Compute an average voice-leading cost between two chords by trying inversions and aligning voiced semitone lists.
        /// Cost is average absolute semitone movement per voice for the best-found inversion pairing.
        /// </summary>
        public static double ComputeAverageVoiceLeadingCost(Chord a, Chord b)
        {
            var aCount = Math.Max(1, a.PitchClasses.Count);
            var bCount = Math.Max(1, b.PitchClasses.Count);

            double best = double.MaxValue;
            // try inversions for both chords
            for (int ia = 0; ia < Math.Max(1, a.PitchClasses.Count); ia++)
            {
                var va = a.GetVoicedSemitones(ia).ToArray();
                for (int ib = 0; ib < Math.Max(1, b.PitchClasses.Count); ib++)
                {
                    var vb = b.GetVoicedSemitones(ib).ToArray();
                    var cost = AlignAndComputeCost(va, vb);
                    if (cost < best) best = cost;
                }
            }

            if (best == double.MaxValue) return 0.0;
            return best;
        }

        private static double AlignAndComputeCost(int[] a, int[] b)
        {
            // Align lengths by extending the shorter by adding 12 to highest voices
            var la = a.Length; var lb = b.Length;
            var L = Math.Max(la, lb);
            var ea = ExtendToLength(a, L);
            var eb = ExtendToLength(b, L);

            // compute sum absolute differences
            double sum = 0;
            for (int i = 0; i < L; i++) sum += Math.Abs(ea[i] - eb[i]);
            return sum / L; // average per voice
        }

        private static int[] ExtendToLength(int[] v, int length)
        {
            if (v.Length == length) return v.ToArray();
            var res = new List<int>(v);
            int idx = 0;
            while (res.Count < length)
            {
                // append next tone transposed up an octave
                var val = res[idx % v.Length] + 12 * (1 + idx / v.Length);
                res.Add(val);
                idx++;
            }
            return res.ToArray();
        }
    }
}
