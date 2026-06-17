using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Village;
using Autonocraft.UI.Village;
using Autonocraft.UI.VillagePanels;
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI
{

    public sealed partial class VillageScreen
    {
        private void UpdateFounding(
            Viewport viewport,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse)
        {
            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CloseRequested = true;
                return;
            }

            var layout = CreateLayout(viewport);
            float panelX = layout.PanelX;
            float panelY = layout.PanelY;
            float left = layout.Left;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float footerY = panelY + layout.S(PanelHeight) - layout.S(FooterHeight);

            _hoveredButton = -1;
            HitRect(left, footerY, buttonW, buttonH, 14, mouse);
            if (_canClaimNearby)
            {
                HitRect(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, 12, mouse);
            }

            float closeX = panelX + layout.S(PanelWidth) - layout.S(20f) - buttonW;
            float closeY = panelY + layout.S(PanelHeight) - layout.S(30f);
            HitRect(closeX, closeY, buttonW, buttonH, 11, mouse);

            bool activate = (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                || ((kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space))
                    && !prevKb.IsKeyDown(Keys.Enter)
                    && !prevKb.IsKeyDown(Keys.Space));

            if (!activate)
            {
                return;
            }

            HandleFoundingActivate();
        }

        private void DrawFounding(Viewport viewport, float alpha, float offsetY)
        {
            var layout = CreateLayout(viewport);
            layout.PanelY += offsetY;
            var foundingContext = new FoundingPanelContext
            {
                Ui = _ui,
                UiLayout = layout.Ui,
                PlayerPayer = _playerPayer,
                PlayerCreative = _playerCreative,
                CanClaimNearby = _canClaimNearby,
                HoveredButton = _hoveredButton,
                PanelX = layout.PanelX,
                PanelY = layout.PanelY,
                PanelWidth = layout.S(PanelWidth),
                PanelHeight = layout.S(PanelHeight),
                ContentLeft = layout.Left,
                Alpha = alpha
            };
            _foundingPanel.Draw(foundingContext);
        }

        private void HandleFoundingActivate()
        {
            if (_hoveredButton == 11)
            {
                CloseRequested = true;
                return;
            }

            if (_hoveredButton == 12)
            {
                ClaimRequested = true;
                return;
            }

            if (_hoveredButton == 14 && FoundingPanel.CanAffordTownHeart(_playerPayer, _playerCreative))
            {
                PlaceTownHeartRequested = true;
            }
        }
    }
}
