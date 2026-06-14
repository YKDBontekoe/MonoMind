using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Engine;

namespace Autonocraft.UI.Village
{
    public interface IVillageTab
    {
        int Index { get; }
        string Label { get; }
        void Draw(VillageTabContext context);
        int HandleClick(VillageTabContext context, float mouseX, float mouseY);
    }

    public sealed class VillageTabContext
    {
        public required UiRenderer Ui { get; init; }
        public required VillageViewModel ViewModel { get; init; }
        public required Autonocraft.Village.Village Village { get; init; }
        public float ContentLeft { get; init; }
        public float ContentY { get; init; }
        public float ContentHeight { get; init; }
        public float Alpha { get; init; } = 1f;
        public Color Accent { get; init; } = Color.White;
    }
}
