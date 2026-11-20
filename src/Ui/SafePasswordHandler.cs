using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class SafePasswordHandler : IDisposable
    {
        private static readonly FieldInfo State = UiResources.Field(typeof(KinkoMiniGame), "state_");
        private static readonly object InputState = Enum.Parse(State.FieldType, "Input");
        private static readonly object CheckState = Enum.Parse(State.FieldType, "CheckAnim");
        private static readonly FieldInfo Layout = UiResources.Field(typeof(KinkoMiniGame), "button_layout_");
        private static readonly FieldInfo CursorX = UiResources.Field(typeof(KinkoMiniGame), "cursor_x");
        private static readonly FieldInfo CursorY = UiResources.Field(typeof(KinkoMiniGame), "cursor_y");
        private static readonly FieldInfo Digits = UiResources.Field(typeof(KinkoMiniGame), "lock_pass");
        private static readonly FieldInfo Position = UiResources.Field(typeof(KinkoMiniGame), "current_number_");
        private static readonly FieldInfo Body = UiResources.Field(typeof(KinkoMiniGame), "body_");
        private readonly UiRuntime _runtime;
        private KinkoMiniGame _owner;
        private int _generation, _lastButton = -1;
        private bool _intro, _force, _disposed;
        private string _notice;
        internal SafePasswordHandler(UiRuntime runtime) { _runtime = runtime; }
        internal string Page { get { return CanRead() ? "safe-password:" + _generation.ToString(CultureInfo.InvariantCulture) : null; } }
        private bool Active()
        {
            return !_disposed && _owner != null && _owner == KinkoMiniGame.instance && !_owner.is_end
                && _owner.enabled && NativeFields.Visible(_owner) && Application.isFocused
                && (Transform)Body.GetValue(_owner) != null && ((Transform)Body.GetValue(_owner)).gameObject.activeInHierarchy
                && advCtrl.instance != null && advCtrl.instance.active
                && _runtime.Presentation != null && _runtime.Presentation.Foreground()
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE;
        }
        internal bool IsOpen { get { return !_disposed && _owner != null && _owner == KinkoMiniGame.instance && !_owner.is_end; } }
        internal bool CanRead() { return Active() && InputState.Equals(State.GetValue(_owner)); }
        internal void Start(KinkoMiniGame owner)
        {
            Close(); _owner = owner; _intro = true; _lastButton = -1;
            _runtime.Trace("safe_password_start", "generation=" + _generation);
        }
        internal void Ready(KinkoMiniGame owner) { if (_owner == owner && CanRead()) Request(); }
        internal void InputObserved() { if (CanRead()) Request(); }
        internal int BeforeNumber(KinkoMiniGame owner)
        { return _owner == owner && CanRead() ? (int)Position.GetValue(owner) : -1; }
        internal void NumberCommitted(KinkoMiniGame owner, int number, int previousCount)
        {
            if (_owner != owner || previousCount < 0 || !Active()) return;
            int[] digits = CurrentDigits();
            if (number == 10)
            {
                if (digits.Length >= previousCount) return;
                _notice = string.Format(_runtime.Messages.Get("safe-password.deleted"), previousCount);
            }
            else
            {
                if (number < 0 || number > 9 || digits.Length != previousCount + 1 || digits[digits.Length - 1] != number)
                    throw new InvalidOperationException("Native safe password digit commit changed.");
                _notice = string.Format(_runtime.Messages.Get("safe-password.digit"), digits.Length, number);
            }
            Request();
        }
        private int CurrentButton()
        {
            int[,] layout = (int[,])Layout.GetValue(_owner);
            int x = (int)CursorX.GetValue(_owner), y = (int)CursorY.GetValue(_owner);
            if (layout == null || y < 0 || x < 0 || y >= layout.GetLength(0) || x >= layout.GetLength(1))
                throw new InvalidOperationException("Native safe password cursor outside keypad.");
            int button = layout[y, x];
            if (button < 0 || button > 10) throw new InvalidOperationException("Unknown native safe password button.");
            return button;
        }
        private int[] CurrentDigits()
        {
            int[] source = (int[])Digits.GetValue(_owner);
            if (source == null || source.Length != 7) throw new InvalidOperationException("Native safe password panel size changed.");
            List<int> digits = new List<int>(); bool empty = false;
            foreach (int digit in source)
            {
                if (digit == -1) { empty = true; continue; }
                if (empty || digit < 0 || digit > 9) throw new InvalidOperationException("Native safe password panel contains an invalid digit.");
                digits.Add(digit);
            }
            return digits.ToArray();
        }
        private string Focus(int button)
        {
            string label = button == 10 ? _runtime.Messages.Get("safe-password.delete") : button.ToString(CultureInfo.InvariantCulture);
            return string.Format(_runtime.Messages.Get("safe-password.focus"), label);
        }
        private string Entered()
        {
            List<string> digits = new List<string>();
            foreach (int digit in CurrentDigits()) digits.Add(digit.ToString(CultureInfo.InvariantCulture));
            return string.Format(_runtime.Messages.Get("safe-password.entered"), digits.Count == 0
                ? _runtime.Messages.Get("safe-password.empty") : string.Join(" ", digits.ToArray()));
        }
        internal void Repeat() { if (CanRead()) { _force = true; Request(); } }
        private void Request()
        {
            KinkoMiniGame owner = _owner; int generation = _generation;
            if (owner == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_disposed || owner != _owner || generation != _generation) return;
                try { Read(); } catch (Exception error) { _runtime.ReadFailure("safe-password-read", error); }
            });
        }
        private void Read()
        {
            bool input = CanRead();
            if (!input && !(Active() && _notice != null && CheckState.Equals(State.GetValue(_owner))))
            { _notice = null; _force = false; return; }
            int button = CurrentButton(); List<string> text = new List<string>();
            if (_intro || _force)
            {
                text.Add(_runtime.Messages.Get("safe-password.title")); text.Add(Focus(button)); text.Add(Entered());
            }
            else if (_notice != null) text.Add(_notice);
            else if (button != _lastButton) text.Add(Focus(button));
            _intro = _force = false; _notice = null; _lastButton = button;
            if (text.Count != 0) _runtime.Presentation.Ui(_owner, string.Join("\n", text.ToArray()));
        }
        internal void Ended(KinkoMiniGame owner) { if (_owner == owner && owner.is_end) Close(); }
        internal void Close()
        {
            if (!ReferenceEquals(_owner, null) && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _owner = null; _generation++; _intro = _force = false; _notice = null; _lastButton = -1;
        }
        /// <summary>Releases only this keypad's pending presentation requests.</summary>
        public void Dispose() { Close(); _disposed = true; }
    }
}
