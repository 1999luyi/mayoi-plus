using System;

namespace Pwaat.Accessibility.Ui
{
    // State follows the displayed name plate, including transitions through an unnamed passage.
    internal sealed class StorySpeakerState
    {
        private string _displayed;
        private string _spoken;
        internal void Observe(string identity)
        {
            if (!string.Equals(_displayed, identity, StringComparison.Ordinal)) { _spoken = null; }
            _displayed = identity;
            if (string.IsNullOrEmpty(identity)) { _spoken = null; }
        }
        internal bool ShouldSpeak(string identity)
        {
            Observe(identity);
            return !string.IsNullOrEmpty(identity) && !string.Equals(_spoken, identity, StringComparison.Ordinal);
        }
        internal void Committed(string identity) { _spoken = identity; }
        internal void Clear() { _displayed = null; _spoken = null; }
    }
}
