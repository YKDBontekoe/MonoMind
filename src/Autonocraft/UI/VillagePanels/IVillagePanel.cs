using Microsoft.Xna.Framework;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.World;
using Autonocraft.UI.Village;
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI.VillagePanels
{
    /// <summary>
    /// Lightweight abstraction for a single village screen panel (tab content).
    /// Panels are responsible for tab-specific layout and drawing, while
    /// VillageScreen owns overall chrome, input routing, and request flags.
    /// </summary>
    public interface IVillagePanel
    {
        /// <summary>Zero-based tab index used by VillageScreen.</summary>
        int TabIndex { get; }

        /// <summary>Label shown in the tab bar.</summary>
        string Label { get; }

        /// <summary>Whether this panel's tab should be visible for the given context.</summary>
        bool IsVisible(VillagePanelContext context);

        /// <summary>Draw the panel contents inside the shared village window chrome.</summary>
        void Draw(VillagePanelContext context);
    }

    /// <summary>
    /// Shared data needed by village panels for drawing.
    /// This intentionally mirrors the data VillageScreen already uses so that
    /// behavior and visuals stay identical while allowing the implementation
    /// to move behind panel classes.
    /// </summary>
    public sealed class VillagePanelContext
    {
        public required UiRenderer Ui { get; init; }
        public required UiLayout UiLayout { get; init; }

        public required VillageEntity Village { get; init; }
        public VillageViewModel? ViewModel { get; init; }
        public required VillagerManager Villagers { get; init; }
        public required Vector3 PlayerPosition { get; init; }
        public required bool PlayerCreative { get; init; }
        public required bool PlayWithAi { get; init; }
        public int EarlyGuideStage { get; init; }

        public float PanelX { get; init; }
        public float PanelY { get; init; }
        public float PanelWidth { get; init; }
        public float PanelHeight { get; init; }
        public float ContentLeft { get; init; }
        public float ContentTop { get; init; }
        public float ContentHeight { get; init; }
        public float FooterHeight { get; init; }

        public float Alpha { get; init; }
        public Color Accent { get; init; }

        // Scroll and selection state are still owned by VillageScreen but passed
        // through the context so panels can use them for layout.
        public float BuildScroll { get; init; }
        public float PeopleScroll { get; init; }
        public int SelectedVillagerId { get; init; }
        public int SelectedGoalBlockIndex { get; init; }
        public int SelectedGoalCountIndex { get; init; }
        public int HoveredButton { get; init; }

        public IItemContainer? PlayerPayer { get; init; }

        public string? OpeningNote { get; init; }
    }

    /// <summary>Context for the founding-mode screen (no village yet).</summary>
    public sealed class FoundingPanelContext
    {
        public required UiRenderer Ui { get; init; }
        public required UiLayout UiLayout { get; init; }
        public required IItemContainer? PlayerPayer { get; init; }
        public required bool PlayerCreative { get; init; }
        public required bool CanClaimNearby { get; init; }
        public int HoveredButton { get; init; }
        public float PanelX { get; init; }
        public float PanelY { get; init; }
        public float PanelWidth { get; init; }
        public float PanelHeight { get; init; }
        public float ContentLeft { get; init; }
        public float Alpha { get; init; }
    }
}

