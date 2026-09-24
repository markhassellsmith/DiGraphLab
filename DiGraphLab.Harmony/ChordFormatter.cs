using System;
using System.Linq;
using System.Text;

namespace DiGraphLab.Harmony
{
    /// <summary>
    /// Quick formatter for chord symbols. Supports traditional chord-symbol notation and a basic Nashville-numbered mode.
    /// This is a lightweight formatter intended for quick human-readable output, not exhaustive music-engraving-quality spelling.
    /// </summary>
    public static class ChordFormatter
    {
        public sealed class Options
        {
            public bool PreferSharps { get; init; } = true;
            public bool UseNashville { get; init; } = false;
            /// <summary>Tonic pitch-class (0..11) required for Nashville mode. If null Nashville falls back to traditional.</summary>
            public int? TonicPc { get; init; }
            /// <summary>When true, include the chord inversion/bass as a slash (e.g., C/E).</summary>
            public bool IncludeBass { get; init; } = true;
            /// <summary>When true, include the numeric 7/9/maj7 suffixes detected in the Chord. Otherwise triads may omit 'maj'.</summary>
            public bool ShowExtensions { get; init; } = true;
        }

        private static readonly string[] SharpNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly string[] FlatNames =  { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

        public static string Format(Chord chord, Options? options = null)
        {
            options ??= new Options();
            if (options.UseNashville && options.TonicPc.HasValue)
            {
                var n = GetNashvilleNumber(options.TonicPc.Value, chord.RootPc);
                var suffix = NashvilleQualitySuffix(chord, options.ShowExtensions);
                var sb = new StringBuilder();
                sb.Append(n);
                if (!string.IsNullOrEmpty(suffix)) sb.Append(suffix);
                if (options.IncludeBass)
                {
                    var bass = GetBassName(chord, options.PreferSharps);
                    if (!string.IsNullOrEmpty(bass)) sb.Append('/').Append(bass);
                }
                return sb.ToString();
            }

            // Traditional
            var rootName = NoteName(chord.RootPc, options.PreferSharps);
            var quality = chord.Quality ?? string.Empty;
            var label = BuildTraditionalSymbol(rootName, chord, quality, options.ShowExtensions);
            if (options.IncludeBass)
            {
                var bass = GetBassName(chord, options.PreferSharps);
                if (!string.IsNullOrEmpty(bass)) label = label + "/" + bass;
            }
            return label;
        }

        private static string NoteName(int pc, bool preferSharps) => (preferSharps ? SharpNames : FlatNames)[((pc % 12) + 12) % 12];

        private static string GetBassName(Chord chord, bool preferSharps)
        {
            var bassPc = chord.GetInversionPitchClasses().FirstOrDefault();
            return NoteName(bassPc, preferSharps);
        }

        private static string BuildTraditionalSymbol(string rootName, Chord chord, string quality, bool showExtensions)
        {
            // Normal forms: triads, minors, sevenths, suspended, add9, augmented, diminished
            switch (quality)
            {
                case "maj":
                    return rootName; // C == C major triad
                case "min":
                    return rootName + "m";
                case "dim":
                    return rootName + "dim";
                case "aug":
                    return rootName + "aug";
                case "sus2":
                case "sus4":
                    return rootName + quality;
                case "add9":
                    return rootName + "add9";
                case "maj7":
                    return rootName + (showExtensions ? "maj7" : "");
                case "7":
                    return rootName + "7";
                case "m7":
                    return rootName + "m7";
                case "m7b5":
                    return rootName + "m7b5";
                case "dim7":
                    return rootName + "dim7";
                case "9":
                case "m9":
                case "maj9":
                    return rootName + quality;
                default:
                    // Unknown or empty quality: attempt to generate from intervals
                    var q = chord.Quality;
                    if (string.IsNullOrEmpty(q))
                    {
                        // fallback: if chord has a 3rd and 7th include them
                        var intervals = chord.GetIntervalsFromRoot();
                        var has3 = intervals.Contains(3);
                        var has4 = intervals.Contains(4);
                        var has7 = intervals.Contains(7);
                        var has10 = intervals.Contains(10);
                        if (has4 && has7) return rootName;
                        if (has3 && has7) return rootName + "m";
                        if (has4 && has10) return rootName + "7";
                    }
                    return rootName + q;
            }
        }

        private static string NashvilleQualitySuffix(Chord chord, bool showExtensions)
        {
            // Map chord.Quality to Nashville suffixes (keep mostly same): m, 7, maj7, sus4, add9, etc.
            var q = chord.Quality ?? string.Empty;
            if (string.IsNullOrEmpty(q)) return string.Empty;
            if (q == "maj") return string.Empty;
            if (q == "min") return "m";
            return q; // most qualities are fine as-is (7, maj7, m7, sus4, add9, etc.)
        }

        private static string GetNashvilleNumber(int tonicPc, int rootPc)
        {
            var interval = (rootPc - tonicPc + 12) % 12;
            // Major scale mapping
            var major = new[] { 0, 2, 4, 5, 7, 9, 11 };
            for (int i = 0; i < major.Length; i++)
            {
                if (major[i] == interval) return (i + 1).ToString();
            }
            // Not diatonic: attempt simple accidental notation (#/b)
            for (int i = 0; i < major.Length; i++)
            {
                if ((major[i] + 1) % 12 == interval) return "#" + (i + 1).ToString();
                if ((major[i] + 11) % 12 == interval) return "b" + (i + 1).ToString();
            }
            // As a last resort, return chromatic as "(n)" with raw semitone distance
            return "(" + interval.ToString() + ")";
        }
    }
}
