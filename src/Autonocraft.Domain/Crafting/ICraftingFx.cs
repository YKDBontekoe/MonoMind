namespace Autonocraft.Domain.Crafting
{
    /// <summary>UI transition hooks for crafting screens — implemented by Engine/Core.</summary>
    public interface ICraftingFx
    {
        float JournalOpenProgress { get; }
        void UpdateJournalTransition(float deltaTime, bool journalOpen);
    }
}
