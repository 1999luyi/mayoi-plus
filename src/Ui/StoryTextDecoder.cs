using System;
using System.IO;
using System.Text;

namespace Pwaat.Accessibility.Ui
{
    // Reads the native bytecode only. No command is executed and no visible-string comparison is used.
    internal static class StoryTextDecoder
    {
        internal sealed class Segment
        {
            internal uint Start;
            internal uint End;
            internal string Text;
        }
        internal static bool IsBarrier(ushort code, Func<ushort, bool> nativePage)
        {
            if (nativePage(code)) { return true; }
            switch (code)
            {
                // Input, speaker, window, record, flow and script/movie commands commit their own state first.
                case 0x04:
                case 0x0d:
                case 0x0e:
                case 0x11:
                case 0x13:
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1c:
                case 0x1f:
                case 0x21:
                case 0x28:
                case 0x29:
                case 0x2a:
                case 0x2c:
                case 0x34:
                case 0x35:
                case 0x36:
                case 0x49:
                case 0x4a:
                case 0x4c:
                case 0x4e:
                case 0x54:
                case 0x55:
                case 0x57:
                case 0x5c:
                case 0x5e:
                case 0x64:
                case 0x69:
                case 0x6a:
                case 0x6c:
                case 0x6d:
                case 0x71:
                case 0x74:
                case 0x75:
                case 0x78:
                case 0x7a:
                case 0x7b:
                case 0x7c:
                    return true;
                default: return false;
            }
        }
        internal static Segment Read(ushort[] data, uint start, ushort[] argumentCounts, Func<ushort, bool> nativePage, Func<int, string> inlineKey)
        {
            if (data == null || argumentCounts == null || nativePage == null || start >= data.Length) { throw new InvalidDataException("Invalid native message range."); }
            StringBuilder text = new StringBuilder();
            uint cursor = start;
            while (cursor < data.Length)
            {
                ushort code = data[cursor];
                if (code > 128) { text.Append((char)(code - 128)); cursor++; continue; }
                if (code >= argumentCounts.Length) { throw new InvalidDataException("Unknown native message opcode."); }
                if (IsBarrier(code, nativePage)) { break; }
                uint next = cursor + 1u + argumentCounts[code];
                if (next > data.Length) { throw new InvalidDataException("Truncated native message arguments."); }
                if (code == 1) { text.Append('\n'); }
                else if (code == 0x7f)
                {
                    if (inlineKey == null) { break; }
                    text.Append(inlineKey(data[cursor + 1]));
                }
                // Color, speed, sound, timing and other presentation arguments are not text.
                cursor = next;
            }
            return new Segment { Start = start, End = cursor, Text = text.ToString() };
        }
    }
}
