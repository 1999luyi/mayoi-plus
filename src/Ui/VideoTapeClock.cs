using System;
using System.Collections.Generic;
using System.Xml;

namespace Pwaat.Accessibility.Ui
{
    internal static class VideoTapeClock
    {
        private sealed class Reading { internal int First, Last; internal string Text; }
        private static readonly Reading[] Readings = Load();
        private static Reading[] Load()
        {
            List<Reading> result = new List<Reading>(); int next = 1;
            foreach (XmlElement entry in UiResources.EmbeddedXml("Pwaat.Accessibility.VideoFrames.xml").SelectNodes("/videoFrames/clock"))
            {
                int first = int.Parse(entry.GetAttribute("first")), last = int.Parse(entry.GetAttribute("last"));
                string text = entry.GetAttribute("text");
                if (first != next || last < first || last > 1420 || text.Length != 8) throw new InvalidOperationException("Invalid reviewed video clock range.");
                result.Add(new Reading { First = first, Last = last, Text = text }); next = last + 1;
            }
            if (next != 1421) throw new InvalidOperationException("Incomplete reviewed video clock coverage.");
            return result.ToArray();
        }
        internal static string At(int frame)
        {
            foreach (Reading reading in Readings) if (frame >= reading.First && frame <= reading.Last) return reading.Text;
            throw new ArgumentOutOfRangeException("frame");
        }
    }
}
