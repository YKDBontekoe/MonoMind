namespace Autonocraft.UI.Menu
{
    public enum MenuLayer
    {
        RootHub,
        SaveBrowser,
        SettingsOverlay,
        StatsOverlay
    }

    /// <summary>
    /// Tracks which pre-game menu screen or overlay is active while <c>GameState == MainMenu</c>.
    /// </summary>
    public sealed class MenuNavigationState
    {
        public MenuLayer Layer { get; private set; } = MenuLayer.RootHub;
        public MenuLayer PreviousLayer { get; private set; } = MenuLayer.RootHub;
        public MenuLayer NewWorldBackTarget { get; set; } = MenuLayer.RootHub;

        public bool IsOverlayActive =>
            Layer is MenuLayer.SettingsOverlay or MenuLayer.StatsOverlay;

        public MenuLayer BaseLayer =>
            Layer is MenuLayer.SettingsOverlay or MenuLayer.StatsOverlay
                ? PreviousLayer
                : Layer;

        public void ResetToRootHub()
        {
            Layer = MenuLayer.RootHub;
            PreviousLayer = MenuLayer.RootHub;
        }

        public void NavigateTo(MenuLayer layer)
        {
            if (layer is MenuLayer.SettingsOverlay or MenuLayer.StatsOverlay)
            {
                OpenOverlay(layer);
                return;
            }

            PreviousLayer = Layer;
            Layer = layer;
        }

        public void OpenOverlay(MenuLayer overlay)
        {
            if (overlay is not (MenuLayer.SettingsOverlay or MenuLayer.StatsOverlay))
            {
                return;
            }

            if (!IsOverlayActive)
            {
                PreviousLayer = BaseLayer;
            }

            Layer = overlay;
        }

        public void CloseOverlay()
        {
            if (!IsOverlayActive)
            {
                return;
            }

            Layer = PreviousLayer;
        }

        public void ReturnToSaveBrowserFromGameplay()
        {
            Layer = MenuLayer.SaveBrowser;
            PreviousLayer = MenuLayer.SaveBrowser;
        }
    }

}
