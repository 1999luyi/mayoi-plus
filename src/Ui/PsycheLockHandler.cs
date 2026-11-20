using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class PsycheLockNative
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private readonly FieldInfo _state, _remaining, _level;
        internal PsycheLockNative()
        {
            _state = typeof(GSPsylock).GetField("psy", Flags);
            if (_state == null) throw new MissingFieldException("GSPsylock", "psy");
            _remaining = _state.FieldType.GetField("rest", Flags); _level = _state.FieldType.GetField("level", Flags);
            if (_remaining == null || _level == null || _remaining.FieldType != typeof(short) || _level.FieldType != typeof(short))
                throw new MissingFieldException("GSPsylock.PsylockDispController", "rest/level");
        }
        internal int Remaining { get { return (short)_remaining.GetValue(_state.GetValue(null)); } }
        internal int Level { get { return (short)_level.GetValue(_state.GetValue(null)); } }
    }
    internal sealed class PsycheLockHandler : IDisposable
    {
        private sealed class Notice { internal string Text; internal AnimationObject Banner; }
        private readonly UiRuntime _runtime;
        private readonly PsycheLockNative _native = new PsycheLockNative();
        private readonly List<Notice> _pending = new List<Notice>();
        private readonly Dictionary<string, string> _banners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private advCtrl _owner;
        private int _generation;
        private bool _disposed;
        internal PsycheLockHandler(UiRuntime runtime)
        {
            _runtime = runtime;
            foreach (XmlElement row in UiResources.EmbeddedXml("Pwaat.Accessibility.PsycheLockText.xml").SelectNodes("/psycheLocks/banner"))
                _banners.Add(row.GetAttribute("title") + ":" + row.GetAttribute("animation"), row.InnerText);
            if (_banners.Count != 18) throw new InvalidOperationException("Psyche-lock banner inventory changed.");
        }
        internal void Appeared()
        {
            int count = _native.Remaining;
            if (count < 1 || count > 5) throw new InvalidOperationException("Invalid visible lock count.");
            Queue("appeared", string.Format(_runtime.Messages.Get("psyche-lock.discovered"), count), null);
        }
        internal void Broken()
        {
            int count = _native.Remaining;
            if (count < 0 || count > 4) throw new InvalidOperationException("Invalid remaining lock count after break.");
            Queue("broken", _runtime.Messages.Get("psyche-lock.broken"), null);
        }
        internal void Banner(AnimationObject animation)
        {
            if (animation == null) return;
            int title = (int)GSStatic.global_work_.title;
            if ((title == 1 && animation.ObjectFOA == 56) || (title == 2 && animation.ObjectFOA == 86))
                Queue("unlocked-banner", null, animation);
        }
        private void Queue(string kind, string text, AnimationObject banner)
        {
            if (_disposed || GSStatic.global_work_.title == TitleId.GS1 || advCtrl.instance == null) return;
            advCtrl owner = advCtrl.instance;
            if (_owner != null && _owner != owner) Reset();
            _owner = owner; int generation = _generation;
            string context = Context();
            _pending.Add(new Notice { Text = text, Banner = banner });
            _runtime.Trace("psyche_lock", "stage=" + kind + " context=" + context + " rest=" + _native.Remaining);
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_generation != generation || _owner != owner) return;
                List<Notice> notices = new List<Notice>(_pending); _pending.Clear();
                if (_disposed || owner != advCtrl.instance || context != Context() || !_runtime.Presentation.Foreground() || !Application.isFocused) return;
                List<string> output = new List<string>();
                foreach (Notice notice in notices)
                {
                    if (notice.Banner == null) { if (!string.IsNullOrEmpty(notice.Text)) output.Add(notice.Text); continue; }
                    AnimationObject current = notice.Banner;
                    if (!current.gameObject.activeInHierarchy || !current.IsPlaying) continue;
                    string nativeText;
                    if (_banners.TryGetValue(((int)GSStatic.global_work_.title).ToString(CultureInfo.InvariantCulture) + ":" + current.PlayingAnimationName, out nativeText)) output.Add(nativeText);
                    else throw new InvalidOperationException("Unreviewed psyche-lock banner: " + current.PlayingAnimationName);
                }
                if (output.Count > 0) _runtime.Presentation.Ui(owner, string.Join("\n", output.ToArray()));
            });
        }
        private static string Context()
        { return GSStatic.global_work_.title + ":" + GSStatic.global_work_.scenario + ":" + GSStatic.global_work_.Room; }
        internal string RemainingText(bool trace = false)
        {
            if (_disposed) return null;
            GlobalWork g = GSStatic.global_work_;
            // Scripted presenting uses SHOW (5:9) as its return owner. Native menu/page flags identify that challenge independently of the backup routine.
            bool current = CanQuery((int)g.title, g.r.no_0, g.r.no_1, g.r.no_2);
            bool status = !current && IsStatusChallenge();
            if (!current && !status)
            {
                if (trace) TraceQuery("outside-challenge", g, null, null);
                return null;
            }
            int level = _native.Level, count = _native.Remaining;
            if (level < 1 || level > 6 || count < 0 || count > 5)
            {
                if (trace) TraceQuery("invalid-display-state", g, level, count);
                return null;
            }
            if (trace) TraceQuery(current ? "current" : "status-return", g, level, count);
            return string.Format(_runtime.Messages.Get("psyche-lock.remaining"), count);
        }
        internal bool IsStatusChallenge()
        {
            GlobalWork g = GSStatic.global_work_;
            if (_disposed || g == null || ((int)g.title != 1 && (int)g.title != 2) || g.r.no_0 != 8 || g.r_bk.no_0 != 5) return false;
            if (CanQuery((int)g.title, g.r_bk.no_0, g.r_bk.no_1, g.r_bk.no_2)) return true;
            // GSMain_TanteiPart.Show saves 5:9; GSMain_Status consumes menu flag 3 and then page flag ST_PS_PSYLOCK_TUKITUKE.
            return g.r_bk.no_1 == 9 && (g.psy_menu_active_flag == 3
                || GSStatic.status_work_ != null && (GSStatic.status_work_.page_status & GSMain_Status.ST_PS_PSYLOCK_TUKITUKE) != 0);
        }
        private void TraceQuery(string stage, GlobalWork g, int? level, int? count)
        {
            _runtime.Trace("psyche_lock_query", "stage=" + stage + " title=" + (int)g.title
                + " routine=" + g.r.no_0 + ":" + g.r.no_1 + ":" + g.r.no_2
                + " return=" + g.r_bk.no_0 + ":" + g.r_bk.no_1 + ":" + g.r_bk.no_2
                + " menu=" + g.psy_menu_active_flag + " page=" + (GSStatic.status_work_ == null ? "unavailable" : GSStatic.status_work_.page_status.ToString(CultureInfo.InvariantCulture))
                + " level=" + (level.HasValue ? level.Value.ToString(CultureInfo.InvariantCulture) : "unread")
                + " rest=" + (count.HasValue ? count.Value.ToString(CultureInfo.InvariantCulture) : "unread"));
        }
        internal static bool CanQuery(int title, int part, int mode, int phase)
        { return (title == 1 || title == 2) && part == 5 && mode == 11 && phase >= 2 && phase <= 7; }
        internal void Reset()
        {
            if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _generation++; _owner = null; _pending.Clear();
        }
        /// <summary>Releases this feature's pending visual notices without stopping speech.</summary>
        public void Dispose() { Reset(); _disposed = true; _banners.Clear(); }
    }
}
