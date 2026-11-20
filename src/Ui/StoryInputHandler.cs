using System;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class StoryInputHandler
    {
        private readonly UiRuntime _runtime;
        private int _frame = -1;
        internal StoryInputHandler(UiRuntime runtime) { _runtime = runtime; }
        // Only input edges are inspected without a user query; no UI/scene scan is performed here.
        internal void Dispatch()
        {
            if (_frame == Time.frameCount) { return; }
            _frame = Time.frameCount;
            if (!Application.isFocused) { if (_runtime.DyingMessage != null) _runtime.DyingMessage.Hide(); if (_runtime.VaseShow != null) _runtime.VaseShow.StopRotation(); _runtime.Speech.InputPolicy.Released(); if (_runtime.Fingerprints != null) _runtime.Fingerprints.StopDust(); return; }
            if (Input.anyKeyDown) { if (_runtime.DyingMessage != null) _runtime.DyingMessage.Resume(); _runtime.Speech.InputPolicy.Pressed(_runtime.Speech.Clock); if (_runtime.Investigation != null) { _runtime.Investigation.InputPressed(); } if (_runtime.Luminol != null) { _runtime.Luminol.InputPressed(); } }
            if (!Input.anyKey) { _runtime.Speech.InputPolicy.Released(); }
            if (_runtime.Assistance != null && _runtime.Assistance.BlocksInput)
            {
                if (_runtime.VaseShow != null) _runtime.VaseShow.StopRotation();
                if (_runtime.Fingerprints != null) _runtime.Fingerprints.StopDust();
                _runtime.Assistance.InputFrame();
                if (!_runtime.Assistance.IsOpen) { return; }
                ReviewKeys(); return;
            }
            bool previousPoint = Input.GetKeyDown(KeyCode.LeftBracket);
            bool nextPoint = Input.GetKeyDown(KeyCode.RightBracket);
            bool previousPicturePoint = previousPoint && _runtime.PicturePointing != null && _runtime.PicturePointing.IsReady;
            bool nextPicturePoint = nextPoint && _runtime.PicturePointing != null && _runtime.PicturePointing.IsReady;
            bool debugPoint = _runtime.Settings.Debug && Input.GetKeyDown(KeyCode.F6);
            bool reloadLabels = _runtime.Settings.Debug && Input.GetKeyDown(KeyCode.F7);
            bool help = Input.GetKeyDown(KeyCode.F1);
            bool repeat = Input.GetKeyDown(KeyCode.F2);
            bool health = Input.GetKeyDown(KeyCode.F4);
            bool vaseQuery = Input.GetKeyDown(KeyCode.F3);
            bool review = Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown);
            if (!help && !repeat && !health && !vaseQuery && !review && !previousPoint && !nextPoint && !debugPoint && !reloadLabels) { return; }
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (alt || shift) { return; }
            if (control) { if (review) { ReviewKeys(); } return; }
            if (vaseQuery)
            {
                if (_runtime.VaseShow == null || !_runtime.VaseShow.CanRead()) return;
                if (IsBound(KeyCode.F3)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), "F3"), false); return; }
                try { _runtime.VaseShow.Query(); } catch (Exception error) { _runtime.ReadFailure("vase-show-query", error); }
                return;
            }
            if (debugPoint || reloadLabels)
            {
                if (_runtime.Investigation == null || !_runtime.Investigation.CanInspect()) return;
                KeyCode key = debugPoint ? KeyCode.F6 : KeyCode.F7;
                if (IsBound(key)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), key.ToString()), false); return; }
                if (debugPoint == reloadLabels) return;
                try { _runtime.Investigation.DebugPoint(reloadLabels); }
                catch (Exception error) { _runtime.ReadFailure("investigation-editable-labels", error, "investigation.file-failed"); }
                return;
            }
            if (previousPoint || nextPoint)
            {
                if (previousPoint && nextPoint) return;
                if (_runtime.VideoTape != null && _runtime.VideoTape.CanNavigate())
                {
                    KeyCode videoKey = previousPoint ? KeyCode.LeftBracket : KeyCode.RightBracket;
                    if (IsBound(videoKey)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), ImageText.KeyName(videoKey)), false); return; }
                    try { _runtime.VideoTape.Navigate(previousPoint ? -1 : 1); }
                    catch (Exception error) { _runtime.ReadFailure("video-navigation", error); }
                    return;
                }
                if (_runtime.Fingerprints != null && _runtime.Fingerprints.CanNavigate())
                {
                    KeyCode fingerprintKey = previousPoint ? KeyCode.LeftBracket : KeyCode.RightBracket;
                    if (IsBound(fingerprintKey)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), ImageText.KeyName(fingerprintKey)), false); return; }
                    if (_runtime.Fingerprints.CanHoldPowderNavigation) return; // Actual native cursor commit handles held powder navigation.
                    try { _runtime.Fingerprints.Navigate(previousPoint ? -1 : 1); }
                    catch (Exception error) { _runtime.ReadFailure("fingerprint-navigation", error); }
                    return;
                }
                if (_runtime.Luminol != null && _runtime.Luminol.CanInspect())
                {
                    KeyCode luminolKey = previousPoint ? KeyCode.LeftBracket : KeyCode.RightBracket;
                    if (IsBound(luminolKey)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), ImageText.KeyName(luminolKey)), false); return; }
                    try { _runtime.Luminol.Navigate(previousPoint ? -1 : 1); }
                    catch (Exception error) { _runtime.ReadFailure("luminol-navigation", error); }
                    return;
                }
                if (_runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect())
                {
                    KeyCode scienceKey = previousPoint ? KeyCode.LeftBracket : KeyCode.RightBracket;
                    if (IsBound(scienceKey)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), ImageText.KeyName(scienceKey)), false); return; }
                    try { _runtime.ScientificInvestigation.Navigate(previousPoint ? -1 : 1); }
                    catch (Exception error) { _runtime.ReadFailure("science-navigation", error); }
                    return;
                }
                if (previousPicturePoint || nextPicturePoint)
                {
                    KeyCode pictureKey = previousPicturePoint ? KeyCode.LeftBracket : KeyCode.RightBracket;
                    if (IsBound(pictureKey)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), ImageText.KeyName(pictureKey)), false); return; }
                    if (previousPicturePoint != nextPicturePoint) { try { _runtime.PicturePointing.Navigate(previousPicturePoint ? -1 : 1); } catch (Exception error) { _runtime.ReadFailure("picture-pointing-navigation", error); } }
                    return;
                }
                if (_runtime.Investigation == null || !_runtime.Investigation.CanInspect()) { return; }
                KeyCode pointKey = previousPoint ? KeyCode.LeftBracket : KeyCode.RightBracket;
                if (IsBound(pointKey)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), ImageText.KeyName(pointKey)), false); return; }
                if (previousPoint != nextPoint)
                {
                    try { _runtime.Investigation.Navigate(previousPoint ? -1 : 1); }
                    catch (Exception error) { _runtime.ReadFailure("investigation-navigation", error); }
                }
                return;
            }
            if (help) { if (_runtime.VaseShow != null) _runtime.VaseShow.StopRotation(); if (_runtime.Assistance != null) { _runtime.Assistance.Open(); } return; }
            if (_runtime.Presentation == null) { return; }
            if (health)
            {
                _runtime.Presentation.TraceHealthRequest();
                if (!_runtime.Presentation.CanAcceptHealthQuery()) { return; }
            }
            else if (!(_runtime.DyingMessage != null && _runtime.DyingMessage.CanRead()) && !(_runtime.VaseShow != null && _runtime.VaseShow.CanRead()) && !(_runtime.SafePassword != null && _runtime.SafePassword.CanRead()) && !(_runtime.VideoTape != null && _runtime.VideoTape.CanRead()) && !(_runtime.Fingerprints != null && _runtime.Fingerprints.CanRead()) && !(_runtime.VasePuzzle != null && _runtime.VasePuzzle.CanRead()) && !(_runtime.Luminol != null && _runtime.Luminol.CanRead()) && !(_runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect()) && !_runtime.Presentation.Foreground()) { return; }
            KeyCode requested = repeat ? KeyCode.F2 : KeyCode.F4;
            if (IsBound(requested))
            {
                _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), requested.ToString()), false); return;
            }
            if (repeat && _runtime.DyingMessage != null && _runtime.DyingMessage.CanRead()) { _runtime.DyingMessage.Repeat(); }
            else if (repeat && _runtime.VaseShow != null && _runtime.VaseShow.CanRead()) { _runtime.VaseShow.Repeat(); }
            else if (repeat && _runtime.SafePassword != null && _runtime.SafePassword.IsOpen) { _runtime.SafePassword.Repeat(); }
            else if (repeat && _runtime.VideoTape != null && _runtime.VideoTape.CanRead()) { _runtime.VideoTape.Repeat(); }
            else if (repeat && _runtime.Fingerprints != null && _runtime.Fingerprints.CanRead()) { _runtime.Fingerprints.Repeat(); }
            else if (repeat && _runtime.VasePuzzle != null && _runtime.VasePuzzle.CanRead()) { _runtime.VasePuzzle.Repeat(); }
            else if (repeat && _runtime.Luminol != null && _runtime.Luminol.CanRead()) { _runtime.Luminol.Repeat(); }
            else if (repeat && _runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect()) { _runtime.ScientificInvestigation.Repeat(); }
            else if (repeat && _runtime.PicturePointing != null && _runtime.PicturePointing.IsReady) { try { _runtime.PicturePointing.Repeat(); } catch (Exception error) { _runtime.ReadFailure("picture-pointing-repeat", error); } }
            else if (repeat && _runtime.Story != null) { _runtime.Story.RepeatRendered(); }
            else if (health) { _runtime.Presentation.QueryHealth(); }
        }
        private void ReviewKeys()
        {
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) { return; }
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) { return; }
            bool previous = Input.GetKeyDown(KeyCode.PageUp), next = Input.GetKeyDown(KeyCode.PageDown); if (!previous && !next) return;
            if (!_runtime.Speech.Review(previous ? -1 : 1))
            { if (_runtime.Speech.History.Count == 0) { _runtime.Speech.OverlaySay(_runtime.Messages.Get("history.empty")); } }
        }
        internal static bool IsBound(KeyCode key)
        {
            if (padCtrl.instance == null) { return false; }
            foreach (long[] slots in padCtrl.instance.key_type_)
            {
                if (slots == null) { continue; }
                for (int i = 0; i < Math.Min(2, slots.Length); i++) { if (slots[i] == (long)key) { return true; } }
            }
            return false;
        }
    }
}
