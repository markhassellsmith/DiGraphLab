using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiGraphLab.Harmony.Viewer.Playback
{
    // Minimal MIDI file writer for a single track containing chord note-on/note-off events.
    internal static class MidiExporter
    {
        public static void WriteMidiFile(string path, IEnumerable<(int[] notes, int ms)> sequence, int tempoBpm, bool arpeggiate = false, int velocity = 100)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            // header (format 1, one track)
            bw.Write(System.Text.Encoding.ASCII.GetBytes("MThd"));
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(6))); // header size 6
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((short)0))); // format 0
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((short)1))); // one track
            short ticksPerQuarter = 480;
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(ticksPerQuarter)));

            // prepare events
            var events = new List<byte>();
            // track chunk header placeholder
            using var trackMs = new MemoryStream();
            using var tw = new BinaryWriter(trackMs);
            // tempo meta event
            var microPerQuarter = 60000000 / tempoBpm;
            WriteVarLen(tw, 0);
            tw.Write((byte)0xFF); tw.Write((byte)0x51); tw.Write((byte)0x03);
            tw.Write((byte)((microPerQuarter >> 16) & 0xFF)); tw.Write((byte)((microPerQuarter >> 8) & 0xFF)); tw.Write((byte)(microPerQuarter & 0xFF));

            int absoluteMs = 0;
            foreach (var (notes, ms) in sequence)
            {
                if (notes == null || notes.Length == 0) { absoluteMs += ms; continue; }
                // convert ms to ticks
                int totalTicks = Math.Max(1, (int)(ms * ticksPerQuarter / (60000.0 / tempoBpm)));
                if (!arpeggiate)
                {
                    // note on events at delta=0
                    WriteVarLen(tw, 0);
                    foreach (var n in notes)
                    {
                        tw.Write((byte)0x90); tw.Write((byte)n); tw.Write((byte)velocity);
                    }
                    // note off after totalTicks
                    WriteVarLen(tw, totalTicks);
                    foreach (var n in notes)
                    {
                        tw.Write((byte)0x80); tw.Write((byte)n); tw.Write((byte)0);
                    }
                }
                else
                {
                    // stagger notes across the duration: small delta per note
                    int notesCount = notes.Length;
                    int per = Math.Max(1, totalTicks / Math.Max(1, notesCount));
                    // first note on at delta=0
                    WriteVarLen(tw, 0);
                    tw.Write((byte)0x90); tw.Write((byte)notes[0]); tw.Write((byte)velocity);
                    for (int i = 1; i < notesCount; i++)
                    {
                        WriteVarLen(tw, per);
                        tw.Write((byte)0x90); tw.Write((byte)notes[i]); tw.Write((byte)velocity);
                    }
                    // turn all notes off after remaining ticks
                    int remaining = totalTicks - per * (notesCount - 1);
                    if (remaining < 0) remaining = 0;
                    WriteVarLen(tw, remaining);
                    foreach (var n in notes)
                    {
                        tw.Write((byte)0x80); tw.Write((byte)n); tw.Write((byte)0);
                    }
                }
                absoluteMs += ms;
            }

            // end of track
            WriteVarLen(tw, 0);
            tw.Write((byte)0xFF); tw.Write((byte)0x2F); tw.Write((byte)0x00);

            // write track chunk header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("MTrk"));
            var trackBytes = ((MemoryStream)trackMs).ToArray();
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(trackBytes.Length)));
            bw.Write(trackBytes);
        }

        private static void WriteVarLen(BinaryWriter w, int value)
        {
            // write variable-length quantity
            var buffer = new List<byte>();
            int val = value & 0x0FFFFFFF;
            buffer.Add((byte)(val & 0x7F));
            val >>= 7;
            while (val > 0)
            {
                buffer.Insert(0, (byte)((val & 0x7F) | 0x80));
                val >>= 7;
            }
            foreach (var b in buffer) w.Write(b);
        }
    }
}
