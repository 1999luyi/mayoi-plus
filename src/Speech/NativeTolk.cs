using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Pwaat.Accessibility.Speech
{
    internal sealed class NativeTolk : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool BoolCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ReaderCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool OutputCall([MarshalAs(UnmanagedType.LPWStr)] string text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int capacity);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        private readonly int _ownerThread = Thread.CurrentThread.ManagedThreadId;
        private IntPtr _module;
        private VoidCall _load;
        private VoidCall _unload;
        private BoolCall _isLoaded;
        private BoolCall _hasSpeech;
        private ReaderCall _reader;
        private OutputCall _output;
        private bool _ownsInitialization;

        internal string LoadedPath { get; private set; }
        internal string Sha256 { get; private set; }
        internal string Reader { get; private set; }
        internal bool HasSpeech { get; private set; }

        internal void Initialize(string fullPath)
        {
            AssertOwner();
            if (_module != IntPtr.Zero) { throw new InvalidOperationException("Native backend is already open."); }
            fullPath = Path.GetFullPath(fullPath);
            Sha256 = InspectNativeFile(fullPath);
            // Restrict implicit dependencies to this DLL directory and default trusted locations.
            _module = LoadLibraryEx(fullPath, IntPtr.Zero, 0x00001100);
            if (_module == IntPtr.Zero) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot load the selected Tolk DLL."); }
            StringBuilder actual = new StringBuilder(32768);
            uint length = GetModuleFileName(_module, actual, actual.Capacity);
            if (length == 0 || length >= actual.Capacity) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot verify the loaded native path."); }
            LoadedPath = Path.GetFullPath(actual.ToString());
            if (!string.Equals(LoadedPath, fullPath, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("A different native module was loaded."); }
            _load = (VoidCall)Bind("Tolk_Load", typeof(VoidCall));
            _unload = (VoidCall)Bind("Tolk_Unload", typeof(VoidCall));
            _isLoaded = (BoolCall)Bind("Tolk_IsLoaded", typeof(BoolCall));
            _hasSpeech = (BoolCall)Bind("Tolk_HasSpeech", typeof(BoolCall));
            _reader = (ReaderCall)Bind("Tolk_DetectScreenReader", typeof(ReaderCall));
            _output = (OutputCall)Bind("Tolk_Output", typeof(OutputCall));
            if (_isLoaded()) { throw new InvalidOperationException("Tolk was already initialized; backend ownership is unknown."); }
            _load();
            _ownsInitialization = _isLoaded();
            if (!_ownsInitialization) { throw new InvalidOperationException("Tolk initialization was not confirmed."); }
            IntPtr name = _reader();
            Reader = name == IntPtr.Zero ? "<none>" : Marshal.PtrToStringUni(name);
            HasSpeech = _hasSpeech();
        }

        internal static string InspectNativeFile(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d)
                    throw new BadImageFormatException("Tolk.dll is not a Windows PE file.");
                stream.Position = 0x3c;
                int pe = reader.ReadInt32();
                if (pe < 64 || pe > stream.Length - 24)
                    throw new BadImageFormatException("Tolk.dll has an invalid PE header.");
                stream.Position = pe;
                if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x014c)
                    throw new BadImageFormatException("Tolk.dll must be a Windows x86 DLL.");
                stream.Position = pe + 22L;
                if ((reader.ReadUInt16() & 0x2000) == 0)
                    throw new BadImageFormatException("Tolk.dll must be a DLL, not an executable.");
                // Record the identity for diagnostics; it is not a compatibility allowlist.
                stream.Position = 0;
                using (SHA256 algorithm = SHA256.Create())
                    return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        internal bool Output(string text, bool interrupt)
        {
            AssertOwner();
            if (!_ownsInitialization || _module == IntPtr.Zero) { throw new InvalidOperationException("Tolk is not owned and ready."); }
            if (string.IsNullOrEmpty(text)) { throw new ArgumentException("Speech text is empty.", "text"); }
            return _output(text, interrupt);
        }

        private Delegate Bind(string name, Type type)
        {
            IntPtr address = GetProcAddress(_module, name);
            if (address == IntPtr.Zero) { throw new EntryPointNotFoundException(name); }
            return Marshal.GetDelegateForFunctionPointer(address, type);
        }

        private void AssertOwner()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThread) { throw new InvalidOperationException("Tolk must stay on its owning thread."); }
        }

        /// <summary>Releases only this instance's initialization and module reference.</summary>
        public void Dispose()
        {
            AssertOwner();
            if (_module == IntPtr.Zero) { return; }
            try
            {
                if (_ownsInitialization) { _unload(); }
            }
            finally
            {
                _ownsInitialization = false;
                IntPtr module = _module;
                _module = IntPtr.Zero;
                if (!FreeLibrary(module)) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot release Tolk module reference."); }
            }
        }
    }
}
