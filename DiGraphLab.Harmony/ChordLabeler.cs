using System;
using System.Linq;

namespace DiGraphLab.Harmony
{
    public static class ChordLabeler
    {
        public enum ScaleType { Major, NaturalMinor, HarmonicMinor, MelodicMinor }

        public sealed class Options
        {
            public bool PreferSharps { get; init; } = true;
            public bool IncludeBass { get; init; } = true;
            public bool ShowSevenths { get; init; } = true;
            public bool UseLowercaseForMinor { get; init; } = true;
        }

        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] NaturalMinorScale = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] HarmonicMinorScale = { 0, 2, 3, 5, 7, 8, 11 };
        private static readonly int[] MelodicMinorScale = { 0, 2, 3, 5, 7, 9, 11 };

        private static readonly string[] SharpNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly string[] FlatNames  = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

        public static string FormatRomanNumeral(Chord chord, int tonicPc, ScaleType scaleType, Options? options = null)
        {
            options ??= new Options();
            var scale = scaleType switch
            {
                ScaleType.Major => MajorScale,
                ScaleType.NaturalMinor => NaturalMinorScale,
                ScaleType.HarmonicMinor => HarmonicMinorScale,
                ScaleType.MelodicMinor => MelodicMinorScale,
                _ => MajorScale
            };

            var interval = (chord.RootPc - Mod12(tonicPc) + 12) % 12;

            // Find diatonic degree or accidental alteration relative to scale
            int degreeIndex = -1;
            string accidental = string.Empty;
            for (int i = 0; i < scale.Length; i++)
            {
                if (scale[i] == interval)
                {
                    degreeIndex = i;
                    accidental = string.Empty;
                    break;
                }
                if ((scale[i] + 1) % 12 == interval)
                {
                    degreeIndex = i;
                    accidental = "#";
                    break;
                }
                if ((scale[i] + 11) % 12 == interval)
                {
                    degreeIndex = i;
                    accidental = "b";
                    break;
                }
            }

            string roman;
            if (degreeIndex >= 0)
            {
                roman = ToRoman(degreeIndex + 1);
            }
            else
            {
                // Off-scale root; fall back to numeric interval in parentheses
                return "(" + interval.ToString() + ")" + (options.IncludeBass ? "/" + BassName(chord, options.PreferSharps) : string.Empty);
            }

            // Determine case and suffix based on chord quality
            var q = (chord.Quality ?? string.Empty).ToLowerInvariant();
            bool isMinor = q.StartsWith("m") || q == "min";
            bool isDiminished = q.Contains("dim");
            bool isHalfDiminished = q.Contains("m7b5") || q.Contains("ø");
            bool isMajor = q.Contains("maj") || q == "maj" || (!isMinor && !isDiminished && !isHalfDiminished && !q.StartsWith("m") && !q.StartsWith(""));

            string baseRoman;
            if (isDiminished)
            {
                baseRoman = roman.ToLowerInvariant() + "°";
            }
            else if (isHalfDiminished)
            {
                baseRoman = roman.ToLowerInvariant() + "ø";
            }
            else if (isMinor)
            {
                baseRoman = options.UseLowercaseForMinor ? roman.ToLowerInvariant() : roman;
            }
            else
            {
                baseRoman = roman; // major/default uppercase
            }

            // Append seventh/extension markers if requested
            string suffix = string.Empty;
            if (options.ShowSevenths)
            {
                if (q.Contains("maj7")) suffix = "maj7";
                else if (q.Contains("7") && !q.Contains("maj7") && !isHalfDiminished && !isDiminished) suffix = "7";
                else if (isHalfDiminished) suffix = "7"; // ø7
                else if (q.Contains("9")) suffix = q.Contains("maj9") ? "maj9" : "9";
            }

            var result = accidental + baseRoman + suffix;
            if (options.IncludeBass)
            {
                var bass = BassName(chord, options.PreferSharps);
                if (!string.IsNullOrEmpty(bass)) result += "/" + bass;
            }
            return result;
        }

        private static string BassName(Chord chord, bool preferSharps)
        {
            var bassPc = chord.GetInversionPitchClasses().FirstOrDefault();
            return NoteName(bassPc, preferSharps);
        }

        private static string NoteName(int pc, bool preferSharps) => (preferSharps ? SharpNames : FlatNames)[Mod12(pc)];

        private static int Mod12(int x) => ((x % 12) + 12) % 12;

        private static string ToRoman(int n)
        {
            return n switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                6 => "VI",
                7 => "VII",
                _ => n.ToString()
            };
        }
    }
}
