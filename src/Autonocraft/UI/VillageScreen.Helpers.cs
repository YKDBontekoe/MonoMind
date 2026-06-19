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
        private void ResetRequests()
        {
            RecruitRequested = false;
            SummonSettlersRequested = false;
            ClaimRequested = false;
            PlaceTownHeartRequested = false;
            CloseRequested = false;
            RequestedBlueprintId = null;
            RequestedAssignVillagerId = -1;
            RequestedChatVillagerId = -1;
            RequestedStewardChat = false;
            RequestBlueprintPlacement = false;
            RequestWorkZonePlacement = false;
        }

        private void RefreshVillageState()
        {
            if (_village == null || _villageManager == null)
            {
                return;
            }

            _villageManager.SyncCitizensForVillage(_village);
            _strandedCitizenCount = VillageSettlementHealth.CountStrandedCitizens(_village, _villagers);
            _summonLinksNearby = CountDisplayedCitizens() == 0 && _strandedCitizenCount > 0;
            _viewModel = VillageViewModel.Build(_village, _villageManager, _villagers, _playerCreative, _playerPos, _guidePlayer);

            if (_selectedVillagerId < 0 || !CitizenExists(_selectedVillagerId))
            {
                foreach (var villager in EnumerateCitizens())
                {
                    _selectedVillagerId = villager.Id;
                    break;
                }
            }
        }

        private bool CitizenExists(int villagerId)
        {
            foreach (var villager in EnumerateCitizens())
            {
                if (villager.Id == villagerId)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetRecruitButtonLabel()
        {
            if (_village == null)
            {
                return "Recruit";
            }

            int citizens = CountDisplayedCitizens();
            if (citizens == 0)
            {
                if (_summonLinksNearby)
                {
                    return $"Link nearby settlers ({_strandedCitizenCount})";
                }

                return CanSummonSettlers()
                    ? "Summon settlers"
                    : "Summon settlers (stand at Town Heart)";
            }

            if (citizens >= _village.PopulationCap)
            {
                return "Recruit (build house)";
            }

            if (_playerCreative || _village.Storage.CountBlock(VillageEntity.RationBlock) >= VillageEntity.RecruitFoodCost)
            {
                return _playerCreative ? "Recruit (free)" : "Recruit villager";
            }

            return "Recruit (need 4 planks)";
        }

        private IEnumerable<Villager> EnumerateCitizens()
        {
            if (_village == null)
            {
                yield break;
            }

            _villageManager?.SyncCitizensForVillage(_village);

            foreach (var villager in _villagers.All)
            {
                if (villager.VillageId == _village.Id)
                {
                    yield return villager;
                }
            }
        }

        private bool HasDisplayedCitizens()
        {
            foreach (var _ in EnumerateCitizens())
            {
                return true;
            }

            return false;
        }

        private bool CanSummonSettlers()
        {
            if (_village == null || _villageManager == null || _world == null)
            {
                return false;
            }

            if (CountDisplayedCitizens() > 0)
            {
                return false;
            }

            if (!VillageSettlementHealth.HasEstablishedSettlement(_village))
            {
                return false;
            }

            if (_strandedCitizenCount > 0)
            {
                return VillageSettlementHealth.IsPlayerManagingSettlement(_village, _playerPos);
            }

            return VillageSettlementHealth.IsPlayerManagingSettlement(_village, _playerPos);
        }

        private int CountDisplayedCitizens()
        {
            if (_village == null)
            {
                return 0;
            }

            _villageManager?.SyncCitizensForVillage(_village);
            return VillageSettlementHealth.CountLiveCitizens(_village, _villagers);
        }

        private ScreenLayout CreateLayout(Viewport viewport)
        {
            var ui = new UiLayout(viewport);
            float panelW = ui.S(PanelWidth);
            float panelH = ui.S(PanelHeight);
            return new ScreenLayout
            {
                Ui = ui,
                PanelX = ui.CenterX - panelW / 2f,
                PanelY = ui.CenterY - panelH / 2f,
                Left = ui.CenterX - panelW / 2f + ui.S(20f)
            };
        }

        private static bool PointInPanel(int x, int y, ScreenLayout layout)
        {
            float panelW = layout.Ui.S(PanelWidth);
            float panelH = layout.Ui.S(PanelHeight);
            return x >= layout.PanelX && x <= layout.PanelX + panelW && y >= layout.PanelY && y <= layout.PanelY + panelH;
        }

        private float GetMaxBuildScroll(ScreenLayout layout)
        {
            float panelH = layout.S(PanelHeight);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            float sectionH = layout.S(130f);
            float catalogH = contentH - sectionH - layout.S(12f);
            float cardH = layout.S(58f);
            int count = 0;
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id != "town_heart")
                {
                    count++;
                }
            }

            float contentHeight = count * (cardH + layout.S(8f));
            float visibleHeight = catalogH - layout.S(40f);
            return Math.Max(0f, contentHeight - visibleHeight);
        }

        private float GetMaxPeopleScroll(ScreenLayout layout)
        {
            float panelH = layout.S(PanelHeight);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            float rowH = layout.S(42f);
            int count = CountDisplayedCitizens();
            float contentHeight = count * (rowH + layout.S(4f));
            float visibleHeight = contentH - layout.S(38f);
            return Math.Max(0f, contentHeight - visibleHeight);
        }

        private void HitRect(float x, float y, float w, float h, int buttonId, MouseState mouse)
        {
            var rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            if (HitTest(rect, mouse.X, mouse.Y))
            {
                _hoveredButton = buttonId;
            }
        }

        private static bool HitTest(Rectangle rect, int x, int y)
        {
            if (rect.Contains(x, y))
            {
                return true;
            }

            return new Rectangle(
                rect.X - HitPadding,
                rect.Y - HitPadding,
                rect.Width + HitPadding * 2,
                rect.Height + HitPadding * 2).Contains(x, y);
        }

        private sealed class ScreenLayout
        {
            public required UiLayout Ui { get; init; }
            public float PanelX { get; init; }
            public float PanelY { get; set; }
            public float Left { get; init; }
            public float S(float v) => Ui.S(v);
        }

        private void HandleRenameInput(KeyboardState kb, KeyboardState prevKb)
        {
            if (!_isEditingName)
            {
                return;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                string trimmed = _editingNameBuffer.Trim();
                if (trimmed.Length > 0)
                {
                    _village!.Name = trimmed;
                }
                _isEditingName = false;
                return;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                _isEditingName = false;
                return;
            }

            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back) && _editingNameBuffer.Length > 0)
            {
                _editingNameBuffer = _editingNameBuffer[..^1];
                return;
            }

            foreach (Keys key in Enum.GetValues<Keys>())
            {
                if (!kb.IsKeyDown(key) || prevKb.IsKeyDown(key))
                {
                    continue;
                }

                char? ch = KeyToChar(key, kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
                if (ch.HasValue && _editingNameBuffer.Length < 24)
                {
                    _editingNameBuffer += ch.Value;
                }
            }
        }

        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            return key switch
            {
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPeriod => '.',
                Keys.Space => ' ',
                _ => null
            };
        }
    }
}
