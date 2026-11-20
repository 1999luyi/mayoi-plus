using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    // SE156 loops only on this module's private source while the current movement
    // input is held. Never use native PlaySE's global looping-SE replacement.
    internal sealed class CursorFeedbackAudio
    {
        private static readonly MethodInfo Clip = typeof(soundCtrl).GetMethod("GetClipSE", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
        private static readonly List<CursorFeedbackAudio> Armed = new List<CursorFeedbackAudio>();
        private AudioSource _movement, _range;
        private Component _owner;
        private bool _sampled, _blocked;
        private string _region;
        internal static void Install(Harmony harmony)
        {
            HarmonyMethod stop = new HarmonyMethod(typeof(CursorFeedbackAudio), "ModalOpened");
            foreach (Type type in new[] { typeof(optionCtrl), typeof(SaveLoadUICtrl), typeof(messageBoxCtrl), typeof(recordListCtrl) })
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (method.Name == "Open" || method.Name == "OpenWindow" || method.Name == "OpenWindowSelect" || method.Name == "playRecord")
                        harmony.Patch(method, prefix: stop);
            harmony.Patch(AccessTools.Method(typeof(AccessibilityHelpHandler), "Open"), prefix: stop);
        }
        private static void ModalOpened() { StopAll(); }
        private static bool MovementHeld()
        {
            padCtrl pad = padCtrl.instance;
            if (pad != null && (pad.GetKey(KeyType.Left) || pad.GetKey(KeyType.Right) || pad.GetKey(KeyType.Up) || pad.GetKey(KeyType.Down)
                || pad.axis_pos_L.x != 0f || pad.axis_pos_L.y != 0f)) return true;
            bool bracket = Input.GetKey(KeyCode.LeftBracket) != Input.GetKey(KeyCode.RightBracket);
            bool modified = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt)
                || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return bracket && !modified || Input.GetMouseButton(0);
        }
        internal static void InputFrame()
        {
            if (Armed.Count == 0) return;
            bool held = Application.isFocused && MovementHeld();
            // Input ownership only: cached, event-authorized sources; no UI/model scan.
            foreach (CursorFeedbackAudio audio in Armed)
            {
                if (held) audio.Move(); else audio.StopMovement();
            }
            if (!Application.isFocused) Armed.Clear();
        }
        internal static void StopAll()
        {
            foreach (CursorFeedbackAudio audio in Armed) audio.StopMovement();
            Armed.Clear();
        }
        internal void Commit(Component owner, Vector3 position, string region, int blockedDirections = 0)
        {
            if (_owner != owner) Reset();
            _owner = owner; BlockMovement(blockedDirections);
            if (owner != null && !Armed.Contains(this)) Armed.Add(this);
            if (Application.isFocused && MovementHeld()) Move(); else StopMovement();
            if (_sampled && _region != region)
            {
                if (_region != null) Range(51);
                if (region != null) Range(363);
            }
            _sampled = true; _region = region;
        }
        internal void BlockMovement(int blockedDirections)
        {
            // Persistent native boundary state, independent of the 600 ms cue throttle.
            _blocked = blockedDirections != 0;
            if (_blocked) StopMovement();
        }
        private void Move()
        {
            if (_blocked) { StopMovement(); return; }
            if (soundCtrl.instance == null || _owner == null) return;
            if (_movement == null)
            {
                AudioClip clip = (AudioClip)Clip.Invoke(soundCtrl.instance, new object[] { 156 });
                if (clip == null) throw new InvalidOperationException("Cursor browsing cue SE156 unavailable.");
                _movement = _owner.gameObject.AddComponent<AudioSource>();
                _movement.playOnAwake = false; _movement.loop = true; _movement.spatialBlend = 0f; _movement.clip = clip;
            }
            _movement.volume = 0.5f * soundCtrl.instance.option_set_se_rate * SoundDefine.GetVolume(_movement.clip);
            if (!_movement.isPlaying) _movement.Play();
        }
        private void Range(int id)
        {
            if (soundCtrl.instance == null || _owner == null) return;
            AudioClip clip = (AudioClip)Clip.Invoke(soundCtrl.instance, new object[] { id });
            if (clip == null) throw new InvalidOperationException("Cursor range cue unavailable: " + id);
            if (_range == null)
            {
                _range = _owner.gameObject.AddComponent<AudioSource>();
                _range.playOnAwake = false; _range.loop = false; _range.spatialBlend = 0f;
            }
            _range.volume = 1f;
            _range.PlayOneShot(clip, 0.5f * soundCtrl.instance.option_set_se_rate * SoundDefine.GetVolume(clip));
        }
        private void StopMovement() { if (_movement != null && _movement.isPlaying) _movement.Stop(); }
        internal static void Cue(int id) { if (soundCtrl.instance != null) soundCtrl.instance.PlaySE(id, false); }
        internal static void Edge() { Cue(53); }
        internal void Reset()
        {
            Armed.Remove(this); _owner = null; _sampled = _blocked = false; _region = null;
            StopMovement();
            if (_movement != null) { UnityEngine.Object.Destroy(_movement); _movement = null; }
            if (_range != null) { _range.Stop(); UnityEngine.Object.Destroy(_range); _range = null; }
        }
    }
}
