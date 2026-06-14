namespace Autonocraft.Crafting
{
    public sealed class DiscoveryJournal
    {
        private readonly HashSet<string> _unlocked = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        public void Unlock(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            _unlocked.Add(id);
        }

        public void Load(IEnumerable<string>? ids)
        {
            _unlocked.Clear();
            if (ids == null)
            {
                return;
            }

            foreach (string id in ids)
            {
                Unlock(id);
            }
        }

        public List<string> Export() => _unlocked.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
