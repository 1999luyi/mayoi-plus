using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Pwaat.Accessibility.Ui
{
    internal enum GuideAnnouncement { Off, OncePerRun, EveryEntry }
    internal enum InterruptionHandling { ModInput, ScreenReader }
    internal sealed class AccessibilityPreferences
    {
        private readonly string _path;
        internal bool PuzzleAdaptation = true;
        internal bool FirstRunCompleted;
        internal GuideAnnouncement Guides = GuideAnnouncement.OncePerRun;
        internal InterruptionHandling Interruptions = InterruptionHandling.ScreenReader;
        internal AccessibilityPreferences(string directory, bool load = true)
        {
            _path = Path.Combine(directory, "accessibility.xml");
            if (!load || !File.Exists(_path)) { return; }
            XmlElement root = UiResources.LoadXml(_path).DocumentElement;
            int guides, interruptions;
            if (root == null || root.Name != "accessibilityPreferences" || !int.TryParse(root.GetAttribute("guides"), out guides) || guides < 0 || guides > 2
                || !int.TryParse(root.GetAttribute("interruptions"), out interruptions) || interruptions < 0 || interruptions > 1) { throw new InvalidDataException("Invalid accessibility preferences; original preserved."); }
            string adaptation = root.GetAttribute("puzzleAdaptation");
            if (adaptation.Length > 0 && !bool.TryParse(adaptation, out PuzzleAdaptation)) { throw new InvalidDataException("Invalid puzzle adaptation preference; original preserved."); }
            string firstRun = root.GetAttribute("firstRunCompleted");
            if (firstRun.Length > 0 && !bool.TryParse(firstRun, out FirstRunCompleted)) { throw new InvalidDataException("Invalid first-run preference; original preserved."); }
            Guides = (GuideAnnouncement)guides; Interruptions = (InterruptionHandling)interruptions;
        }
        internal void Save()
        {
            string temporary = _path + ".new";
            XmlDocument doc = new XmlDocument(); XmlElement root = doc.CreateElement("accessibilityPreferences"); doc.AppendChild(root);
            root.SetAttribute("guides", ((int)Guides).ToString()); root.SetAttribute("interruptions", ((int)Interruptions).ToString());
            root.SetAttribute("puzzleAdaptation", PuzzleAdaptation.ToString());
            root.SetAttribute("firstRunCompleted", FirstRunCompleted.ToString());
            doc.Save(temporary);
            if (File.Exists(_path)) { File.Replace(temporary, _path, null); } else { File.Move(temporary, _path); }
        }
    }
    internal sealed class SpeechReviewHistory
    {
        private readonly List<string> _entries = new List<string>();
        private int _cursor = -1;
        internal void Add(string text) { if (_entries.Count == 20) { _entries.RemoveAt(0); } _entries.Add(text); _cursor = _entries.Count - 1; }
        internal string Move(int delta) { if (_entries.Count == 0) { return null; } _cursor = Math.Max(0, Math.Min(_entries.Count - 1, _cursor + delta)); return _entries[_cursor]; }
        internal int Count { get { return _entries.Count; } }
        internal void Clear() { _entries.Clear(); _cursor = -1; }
    }
    internal sealed class InputSpeechPolicy
    {
        private double _lastInput = double.NegativeInfinity;
        private double _lastSerial = double.NegativeInfinity;
        private long _serial;
        private long _consumed;
        internal void Pressed(double now)
        {
            _lastInput = now;
            if (now - _lastSerial >= 30d) { _lastSerial = now; _serial++; }
        }
        internal void Released() { } // Release does not erase ExternalTts's 700 ms input grace.
        internal long Consume(double now)
        {
            if (_serial != _consumed && now - _lastInput <= 700d)
            { _consumed = _serial; return _serial; }
            return 0;
        }
        internal bool Recent(double now, bool held) { return held || now - _lastInput <= 700d; }
    }
    internal sealed class InputSpeechBatch
    {
        internal sealed class Output { internal string Text; internal bool Interrupt; internal bool Remember; }
        private readonly List<string> _text = new List<string>();
        private long _inputSerial;
        private bool _interrupt;
        private bool _remember;
        private double _start;
        private double _due;
        private double _lastInteractive = double.NegativeInfinity;
        internal bool Pending { get { return _text.Count > 0; } }
        internal Output Add(string text, bool remember, long inputSerial, double now, bool held)
        {
            Output preceding = null;
            if (Pending && (_remember != remember || (inputSerial == 0 && _interrupt && now - _start >= 100d))) { preceding = Take(now); }
            if (inputSerial > 0 && (!_interrupt || _inputSerial != inputSerial)) { _text.Clear(); _start = now; }
            if (!Pending) { _start = now; _remember = remember; }
            if (inputSerial > 0) { _inputSerial = inputSerial; }
            _text.Add(text); _interrupt |= inputSerial > 0;
            _due = (_interrupt || held) ? Math.Max(_start + 100d, _lastInteractive + 260d) : now + 100d;
            return preceding;
        }
        internal Output Due(double now) { return Pending && now >= _due ? Take(now) : null; }
        internal Output Take(double now)
        {
            if (!Pending) { return null; }
            Output output = new Output { Text = string.Join("\n", _text.ToArray()), Interrupt = _interrupt, Remember = _remember };
            if (_interrupt) { _lastInteractive = now; }
            _text.Clear(); _interrupt = false; _inputSerial = 0;
            return output;
        }
        internal void Clear() { _text.Clear(); _interrupt = false; _inputSerial = 0; }
    }
    internal sealed class GuideAnnouncementPolicy
    {
        private readonly HashSet<string> _visited = new HashSet<string>();
        private readonly Dictionary<string, string> _active = new Dictionary<string, string>();
        private string _page;
        internal void Enter(string page) { if (_page != page) { _active.Clear(); _page = page; } }
        internal bool Announce(string page, string owner, string text, GuideAnnouncement mode)
        {
            if (string.IsNullOrEmpty(text) || mode == GuideAnnouncement.Off) { return false; }
            Enter(page);
            string previous;
            if (_active.TryGetValue(owner, out previous) && previous == text) { return false; }
            _active[owner] = text;
            if (mode == GuideAnnouncement.OncePerRun && _visited.Contains(page + ":" + owner)) { return false; }
            _visited.Add(page + ":" + owner); return true;
        }
        internal void Leave() { _page = null; _active.Clear(); }
        internal void ChangedPreference() { _active.Clear(); }
    }
}
