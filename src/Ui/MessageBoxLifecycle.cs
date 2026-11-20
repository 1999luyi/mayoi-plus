namespace Pwaat.Accessibility.Ui
{
    // Logical ownership is established by native Open calls, never by serialized activeSelf defaults.
    internal sealed class MessageBoxLifecycle
    {
        private object _owner;
        private bool _opened;

        internal void Open(object owner)
        {
            _owner = owner;
            _opened = owner != null;
        }

        internal void Close(object owner)
        {
            if (!object.ReferenceEquals(_owner, owner)) { return; }
            _opened = false;
            _owner = null;
        }

        internal bool IsOwned(object owner)
        {
            return _opened && owner != null && object.ReferenceEquals(_owner, owner);
        }

        internal bool Blocks(object owner, bool activeSelf, bool activeInHierarchy)
        {
            return IsOwned(owner) && activeSelf && activeInHierarchy;
        }
    }
}
