using System;
using System.Text.RegularExpressions;
namespace Pwaat.Accessibility.Ui
{
    internal static class ScientificPointState
    {
        private static readonly Regex Exclusion = new Regex("nuki|nuke", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex Number = new Regex("\\d+?", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        internal static int ColliderIndex(string name)
        {
            if (Exclusion.IsMatch(name)) return -1;
            Match match = Number.Match(name);
            // Matches the native non-greedy numeric regex, including its single-digit behavior.
            return match.Success ? int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture) - 1 : 0;
        }
        internal static int Next(int current, int direction, int count)
        { if (count <= 0) throw new ArgumentOutOfRangeException("count"); if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException("direction"); return ((current < 0 ? (direction > 0 ? -1 : 0) : current) + direction + count) % count; }
    }
}
