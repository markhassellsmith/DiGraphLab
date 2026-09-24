using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGraphLab.Harmony
{
    /// <summary>
    /// Immutable representation of a chord as a set of pitch-classes (0..11) with a designated root and optional inversion.
    /// The class normalizes pitch-classes mod 12 and, by default, orders them by interval from the root.
    /// </summary>
    public sealed class Chord
    {
        public string Label { get; init; }
        public int RootPc { get; init; } // 0=C,1=C#, ...,11=B (normalized)
        public string Quality { get; init; } = string.Empty;
        public int Inversion { get; init; } = 0;
        /// <summary>Pitch-classes normalized to 0..11 and ordered by interval from RootPc (unless PreserveDoubling=true was used).</summary>
        public IReadOnlyList<int> PitchClasses { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Create a chord.
        /// By default duplicates are removed and pitch-classes are normalized to 0..11 and ordered by interval from RootPc.
        /// Set preserveDoubling to true to keep duplicated pitch-classes (useful for voicings with doublings).
        /// </summary>
        public Chord(string label, int rootPc, string? quality, int inversion, IEnumerable<int>? pitchClasses, bool preserveDoubling = false)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            RootPc = Mod12(rootPc);
            Inversion = inversion;

            var pcs = (pitchClasses ?? Array.Empty<int>()).Select(Mod12);
            if (!preserveDoubling)
                pcs = pcs.Distinct();

            // Order by ascending interval from the root (0..11)
            var ordered = pcs.OrderBy(pc => IntervalFromRoot(pc, RootPc)).ToArray();
            PitchClasses = ordered;
            Quality = string.IsNullOrWhiteSpace(quality) ? InferQualityFromPcs(ordered, RootPc) : quality!;
        }

        public override string ToString() => Label;

        private static int Mod12(int x) => ((x % 12) + 12) % 12;

        private static int IntervalFromRoot(int pc, int rootPc) => (pc - rootPc + 12) % 12;

        /// <summary>
        /// Return the intervals (in semitones, 0..11) of the chord tones from the root, ordered ascending.
        /// </summary>
        public IReadOnlyList<int> GetIntervalsFromRoot() => PitchClasses.Select(pc => IntervalFromRoot(pc, RootPc)).ToArray();

        /// <summary>
        /// Return the chord's pitch-classes rotated for the requested inversion (still 0..11).
        /// The inversion index will be reduced modulo the number of chord tones.
        /// </summary>
        public IReadOnlyList<int> GetInversionPitchClasses(int? inversion = null)
        {
            var tones = PitchClasses.ToArray();
            if (tones.Length == 0) return tones;
            var k = NormalizeInversion(inversion ?? Inversion, tones.Length);
            if (k == 0) return tones;
            return tones.Skip(k).Concat(tones.Take(k)).ToArray();
        }

        /// <summary>
        /// Return a voiced list of semitone values starting from the bass (0 and upwards). This applies the inversion and
        /// ensures the sequence is monotonically non-decreasing by adding 12 where a pitch-class wraps around.
        /// Useful for rendering actual voicings (intervals may exceed 11 when voices cross octaves).
        /// </summary>
        public IReadOnlyList<int> GetVoicedSemitones(int? inversion = null)
        {
            var intervals = GetIntervalsFromRoot().ToArray();
            if (intervals.Length == 0) return intervals;
            var k = NormalizeInversion(inversion ?? Inversion, intervals.Length);
            var rotated = intervals.Skip(k).Concat(intervals.Take(k)).ToArray();
            var result = new List<int>(rotated.Length);
            int prev = rotated[0];
            result.Add(prev);
            for (int i = 1; i < rotated.Length; i++)
            {
                var cur = rotated[i];
                while (cur < prev) cur += 12;
                result.Add(cur);
                prev = cur;
            }
            return result;
        }

        private static int NormalizeInversion(int inversion, int count)
        {
            if (count <= 0) return 0;
            var k = inversion % count;
            if (k < 0) k += count;
            return k;
        }

        // --- Diatonic helpers ---
        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] NaturalMinorScale = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] HarmonicMinorScale = { 0, 2, 3, 5, 7, 8, 11 };
        private static readonly int[] MelodicMinorScale = { 0, 2, 3, 5, 7, 9, 11 }; // ascending melodic minor

        /// <summary>
        /// Create a diatonic triad (or seventh when addSeventh=true) for a given degree in the major scale.
        /// degree is 1..7.
        /// </summary>
        public static Chord FromMajorDiatonic(string label, int tonicPc, int degree, bool addSeventh = false)
            => FromDiatonic(label, tonicPc, degree, MajorScale, addSeventh);

        /// <summary>
        /// Create a diatonic triad (or seventh when addSeventh=true) for a given degree in the natural minor scale.
        /// degree is 1..7.
        /// </summary>
        public static Chord FromNaturalMinorDiatonic(string label, int tonicPc, int degree, bool addSeventh = false)
            => FromDiatonic(label, tonicPc, degree, NaturalMinorScale, addSeventh);

        /// <summary>
        /// Create a diatonic triad (or seventh when addSeventh=true) for a given degree in the harmonic minor scale.
        /// degree is 1..7.
        /// Useful for producing dominant V (major triad on V) and its seventh in minor keys.
        /// </summary>
        public static Chord FromHarmonicMinorDiatonic(string label, int tonicPc, int degree, bool addSeventh = false)
            => FromDiatonic(label, tonicPc, degree, HarmonicMinorScale, addSeventh);

        /// <summary>
        /// Create a diatonic triad (or seventh when addSeventh=true) for a given degree in the melodic minor scale (ascending form).
        /// degree is 1..7.
        /// </summary>
        public static Chord FromMelodicMinorDiatonic(string label, int tonicPc, int degree, bool addSeventh = false)
            => FromDiatonic(label, tonicPc, degree, MelodicMinorScale, addSeventh);

        private static Chord FromDiatonic(string label, int tonicPc, int degree, int[] scale, bool addSeventh)
        {
            if (degree < 1 || degree > 7) throw new ArgumentOutOfRangeException(nameof(degree));
            var rootIndex = degree - 1;
            int pc(int idx) => Mod12(tonicPc + scale[idx % 7]);
            var rootPc = pc(rootIndex);
            var thirdPc = pc(rootIndex + 2);
            var fifthPc = pc(rootIndex + 4);
            var pcs = new List<int> { rootPc, thirdPc, fifthPc };
            if (addSeventh)
            {
                var seventhPc = pc(rootIndex + 6);
                pcs.Add(seventhPc);
            }
            var quality = InferQualityFromPcs(pcs, rootPc);
            return new Chord(label, rootPc, quality: quality, inversion: 0, pitchClasses: pcs);
        }

        /// <summary>
        /// Infer a human-readable chord quality from an unordered set of pitch-classes and a proposed root.
        /// Examples: maj, min, dim, aug, maj7, 7, m7, m7b5, dim7, sus2, sus4, add9, maj9, 9
        /// </summary>
        public static string InferQualityFromPcs(IEnumerable<int> pitchClasses, int rootPc)
        {
            var intervals = pitchClasses.Select(Mod12).Distinct().Select(pc => IntervalFromRoot(pc, rootPc)).OrderBy(i => i).ToArray();
            if (intervals.Length == 0) return string.Empty;
            var has2 = intervals.Contains(2);
            var has3 = intervals.Contains(3);
            var has4 = intervals.Contains(4);
            var has5 = intervals.Contains(5);
            var has6 = intervals.Contains(6);
            var has7 = intervals.Contains(7);
            var has8 = intervals.Contains(8);
            var has9 = intervals.Contains(9);
            var has10 = intervals.Contains(10);
            var has11 = intervals.Contains(11);

            // Seventh-chord detection (preferential)
            if (intervals.Length >= 4 || has10 || has11)
            {
                // Major seventh
                if (has4 && has7 && has11)
                {
                    if (has2) return "maj9";
                    return "maj7";
                }
                // Dominant seventh (V7)
                if (has4 && has7 && has10)
                {
                    if (has2) return "9";
                    return "7";
                }
                // Minor seventh
                if (has3 && has7 && has10)
                {
                    if (has2) return "m9";
                    return "m7";
                }
                // Half-diminished (m7b5)
                if (has3 && has6 && has10)
                {
                    return "m7b5";
                }
                // Fully diminished seventh
                if (has3 && has6 && intervals.Contains(9))
                {
                    return "dim7";
                }
            }

            // Triad / suspended / extended detection
            if (has4 && has7) return "maj";
            if (has3 && has7) return "min";
            if (has3 && has6) return "dim";
            if (has4 && has8) return "aug";
            if (has5 && has7 && !has3 && !has4) return "sus4";
            if (has2 && has7 && !has3 && !has4) return "sus2";

            // add9 / 9 without a seventh
            if (has2 && has7)
            {
                return "add9";
            }

            return string.Empty;
        }
    }
}
