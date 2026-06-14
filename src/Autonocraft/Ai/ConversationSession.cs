namespace Autonocraft.Ai
{
    public sealed class ConversationSession
    {
        public const int MaxMessages = 20;

        private readonly Dictionary<string, List<(string role, string content)>> _histories = new();

        public IReadOnlyList<(string role, string content)> GetHistory(string target)
        {
            if (_histories.TryGetValue(target, out var history))
            {
                return history;
            }

            return Array.Empty<(string role, string content)>();
        }

        public void AddMessage(string target, string role, string content)
        {
            if (!_histories.TryGetValue(target, out var history))
            {
                history = new List<(string role, string content)>();
                _histories[target] = history;
            }

            history.Add((role, content));
            TrimHistory(history);
        }

        public void Clear(string target) => _histories.Remove(target);

        public void ClearAll() => _histories.Clear();

        private static void TrimHistory(List<(string role, string content)> history)
        {
            while (history.Count > MaxMessages)
            {
                history.RemoveAt(0);
            }
        }
    }
}
