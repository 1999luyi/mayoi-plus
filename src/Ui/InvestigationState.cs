using System;
using System.Collections.Generic;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class TopicSnapshot
    {
        internal string Identity;
        internal bool Read;
        internal bool Updated;
        internal bool Locked;
    }
    internal sealed class TopicHistory
    {
        private readonly Dictionary<string, Dictionary<string, TopicSnapshot>> _contexts = new Dictionary<string, Dictionary<string, TopicSnapshot>>();
        internal bool Commit(string context, IList<TopicSnapshot> rows)
        {
            Dictionary<string, TopicSnapshot> previous;
            bool known = _contexts.TryGetValue(context, out previous);
            Dictionary<string, TopicSnapshot> next = new Dictionary<string, TopicSnapshot>();
            bool added = false;
            foreach (TopicSnapshot row in rows)
            {
                TopicSnapshot old = null;
                bool existed = known && previous.TryGetValue(row.Identity, out old);
                if (!existed) { old = null; if (known) { added = true; } }
                row.Updated = !row.Read && old != null && (old.Read || old.Updated);
                next[row.Identity] = new TopicSnapshot { Identity = row.Identity, Read = row.Read, Updated = row.Updated, Locked = row.Locked };
            }
            _contexts[context] = next;
            return added;
        }
        internal void Clear() { _contexts.Clear(); }
    }
    internal sealed class InvestigationFeedback
    {
        private string _identity;
        private int _state = -1;
        private int _edge;
        private double _lastEdgeMilliseconds;
        internal bool Focus(string identity, int state)
        {
            if (_identity == identity && _state == state) { return false; }
            _identity = identity; _state = state; return true;
        }
        internal bool Edge(int blockedDirections, double nowMilliseconds)
        {
            if (blockedDirections == 0) { _edge = 0; return false; }
            if (_edge != blockedDirections || nowMilliseconds < _lastEdgeMilliseconds || nowMilliseconds - _lastEdgeMilliseconds >= 600.0)
            { _edge = blockedDirections; _lastEdgeMilliseconds = nowMilliseconds; return true; }
            return false;
        }
        internal void Clear() { _identity = null; _state = -1; _edge = 0; _lastEdgeMilliseconds = 0; }
    }
}
