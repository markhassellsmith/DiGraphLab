using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiGraphLab.Harmony
{
    public interface IChordLabelParser
    {
        Chord? Parse(string label);
    }

    public sealed class SimpleChordLabelParser : IChordLabelParser
    {
        private static readonly string[] NoteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly Regex RootRegex = new Regex("^[A-G](#|b)?", RegexOptions.Compiled);

        public Chord? Parse(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            label = label.Trim();
            var m = RootRegex.Match(label);
            if (!m.Success) return null;
            var root = m.Value;
            int rootPc = ParseRootPc(root);
            var quality = label.Substring(m.Length).Trim();
            // very naive mapping: major if blank or "maj", minor if starts with m
            var q = "maj";
            if (!string.IsNullOrEmpty(quality))
            {
                var ql = quality.ToLowerInvariant();
                if (ql.StartsWith("m") && !ql.StartsWith("maj")) q = "min";
                else if (ql.StartsWith("dim")) q = "dim";
                else if (ql.StartsWith("aug")) q = "aug";
                else if (ql.StartsWith("7") || ql.Contains("7")) q = "7";
            }

            // simple pitch class sets for common qualities
            IEnumerable<int> pcs = q switch
            {
                "maj" => new[] { rootPc, (rootPc + 4) % 12, (rootPc + 7) % 12 },
                "min" => new[] { rootPc, (rootPc + 3) % 12, (rootPc + 7) % 12 },
                "dim" => new[] { rootPc, (rootPc + 3) % 12, (rootPc + 6) % 12 },
                "aug" => new[] { rootPc, (rootPc + 4) % 12, (rootPc + 8) % 12 },
                "7" => new[] { rootPc, (rootPc + 4) % 12, (rootPc + 7) % 12, (rootPc + 10) % 12 },
                _ => new[] { rootPc }
            };

            return new Chord(label, rootPc, q, 0, pcs);
        }

        private static int ParseRootPc(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return 0;
            char c = s[0];
            int basePc = c switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => 0
            };
            if (s.Length > 1)
            {
                var acc = s[1];
                if (acc == '#') basePc = (basePc + 1) % 12;
                else if (acc == 'b' || acc == 'B') basePc = (basePc + 11) % 12;
            }
            return basePc;
        }
    }
}
