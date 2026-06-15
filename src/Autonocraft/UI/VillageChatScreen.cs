using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Ai;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.UI
{
    public sealed class VillageChatScreen
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 440f;
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 34f;
        private const int MaxHistoryLines = 12;

        private readonly UiRenderer _ui;
        private readonly VillageAiOrchestrator _orchestrator;
        private readonly List<string> _history = new();
        private string _input = string.Empty;
        private string _statusMessage = string.Empty;
        private float _statusTimer;
        private bool _waitingForReply;
        private VillageEntity? _village;
        private string _target = "mayor";
        private bool _stewardMode = true;
        private int _hoveredButton = -1;

        public bool IsOpen { get; private set; }

        public VillageChatScreen(UiRenderer ui, VillageAiOrchestrator? orchestrator = null)
        {
            _ui = ui;
            _orchestrator = orchestrator ?? new VillageAiOrchestrator();
        }

        private string? _villagerDisplayName;

        public void OpenWithVillager(VillageEntity village, int villagerId, string villagerName)
        {
            _villagerDisplayName = villagerName;
            Open(village, villagerId.ToString(), stewardMode: false);
        }

        public void Open(VillageEntity village, string target = "mayor", bool stewardMode = true)
        {
            _village = village;
            _target = target;
            _stewardMode = stewardMode;
            IsOpen = true;
            _input = string.Empty;
            _hoveredButton = -1;
            _history.Clear();
            if (_stewardMode)
            {
                _history.Add("Steward: How may I serve the village?");
            }
            else
            {
                string villagerName = _villagerDisplayName ?? $"Villager {target}";
                _history.Add($"{villagerName}: Yes?");
            }
        }

        public string GetChatHeaderLabel()
        {
            if (_stewardMode || _village == null)
            {
                return "VILLAGE STEWARD";
            }

            return (_villagerDisplayName ?? "VILLAGER").ToUpperInvariant();
        }

        public void Close()
        {
            IsOpen = false;
            _village = null;
            _input = string.Empty;
            _waitingForReply = false;
            _villagerDisplayName = null;
            _orchestrator.ClearPendingConfirmation();
        }

        public void Update(
            Viewport viewport,
            GameSession session,
            KeyboardState kb,
            KeyboardState prevKb,
            MouseState mouse,
            MouseState prevMouse,
            float deltaTime)
        {
            if (!IsOpen || _village == null)
            {
                return;
            }

            if (_statusTimer > 0f)
            {
                _statusTimer -= deltaTime;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                Close();
                return;
            }

            UpdateModeButtons(viewport, session, mouse, prevMouse);

            if (_waitingForReply)
            {
                return;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                _ = SubmitMessageAsync(session);
                return;
            }

            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back) && _input.Length > 0)
            {
                _input = _input[..^1];
                return;
            }

            foreach (Keys key in Enum.GetValues<Keys>())
            {
                if (!IsTextKey(key) || !kb.IsKeyDown(key) || prevKb.IsKeyDown(key))
                {
                    continue;
                }

                char ch = KeyToChar(key, kb);
                if (ch != '\0' && _input.Length < 120)
                {
                    _input += ch;
                }
            }
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            if (!IsOpen || _village == null || alpha <= 0.01f)
            {
                return;
            }

            var layout = new UiLayout(viewport);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;
            float left = panelX + layout.S(20f);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float modeY = panelY + layout.S(44f);
            float stewardX = panelX + layout.S(20f);
            float villagerX = stewardX + buttonW + layout.S(10f);
            float historyY = panelY + layout.S(88f);
            float inputY = panelY + panelH - layout.S(72f);

            _ui.DrawFullscreenBackground(new Color(0.02f, 0.03f, 0.06f) * (0.65f * alpha));
            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, UiTheme.PanelFill * alpha, UiTheme.PanelBorder, alpha);

            string title = GetChatHeaderLabel();
            _ui.DrawCenteredText(title, panelY + layout.S(16f), layout.S(1.5f), UiTheme.Title * alpha);
            _ui.DrawCenteredText(_village.Name.ToUpperInvariant(), panelY + layout.S(34f), layout.S(UiTheme.ScaleNormal), UiTheme.Subtitle * alpha);

            _ui.DrawButton(stewardX, modeY, buttonW, buttonH, "STEWARD", _hoveredButton == 0, _stewardMode, layout.S(1.05f), alpha);
            _ui.DrawButton(villagerX, modeY, buttonW, buttonH, "VILLAGER", _hoveredButton == 1, !_stewardMode, layout.S(1.05f), alpha);

            float y = historyY;
            int start = Math.Max(0, _history.Count - MaxHistoryLines);
            for (int i = start; i < _history.Count; i++)
            {
                _ui.DrawString(Truncate(_history[i], 64), left, y, layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);
                y += layout.S(16f);
            }

            string prompt = _waitingForReply ? "THINKING..." : "> " + _input + (_waitingForReply ? "" : "_");
            _ui.DrawString(prompt, left, inputY, layout.S(UiTheme.ScaleNormal), UiTheme.Meta * alpha);

            if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusMessage))
            {
                _ui.DrawCenteredText(_statusMessage, panelY + panelH - layout.S(28f), layout.S(UiTheme.ScaleSmall), UiTheme.Section * alpha);
            }
            else
            {
                _ui.DrawCenteredText("ENTER TO SEND  ESC TO CLOSE", panelY + panelH - layout.S(28f), layout.S(UiTheme.ScaleSmall), UiTheme.Hint * alpha);
            }
        }

        private void UpdateModeButtons(Viewport viewport, GameSession session, MouseState mouse, MouseState prevMouse)
        {
            var layout = new UiLayout(viewport);
            float panelX = layout.CenterX - layout.S(PanelWidth) / 2f;
            float panelY = layout.CenterY - layout.S(PanelHeight) / 2f;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float modeY = panelY + layout.S(44f);
            float stewardX = panelX + layout.S(20f);
            float villagerX = stewardX + buttonW + layout.S(10f);

            var stewardRect = new Rectangle((int)stewardX, (int)modeY, (int)buttonW, (int)buttonH);
            var villagerRect = new Rectangle((int)villagerX, (int)modeY, (int)buttonW, (int)buttonH);

            _hoveredButton = -1;
            if (stewardRect.Contains(mouse.X, mouse.Y))
            {
                _hoveredButton = 0;
            }
            else if (villagerRect.Contains(mouse.X, mouse.Y))
            {
                _hoveredButton = 1;
            }

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (!click)
            {
                return;
            }

            if (_hoveredButton == 0)
            {
                _stewardMode = true;
                _target = "mayor";
            }
            else if (_hoveredButton == 1)
            {
                _stewardMode = false;
                if (_village != null)
                {
                    session.Villages.SyncCitizensForVillage(_village);
                    foreach (var citizen in VillageSettlementHealth.EnumerateLiveCitizens(_village, session.Villagers))
                    {
                        _target = citizen.Id.ToString();
                        break;
                    }
                }

                if (_target == "mayor")
                {
                    _target = "villager";
                }
            }
        }

        private async Task SubmitMessageAsync(GameSession session)
        {
            string message = _input.Trim();
            if (message.Length == 0)
            {
                return;
            }

            _input = string.Empty;
            _history.Add($"You: {message}");
            _waitingForReply = true;

            try
            {
                string reply = await _orchestrator.HandleChatAsync(message, _target, session).ConfigureAwait(false);
                string prefix = _stewardMode ? "Steward" : "Villager";
                _history.Add($"{prefix}: {reply}");
            }
            catch (Exception ex)
            {
                _statusMessage = ex.Message;
                _statusTimer = 3f;
                _history.Add("System: Chat failed.");
            }
            finally
            {
                _waitingForReply = false;
            }
        }

        private static bool IsTextKey(Keys key)
        {
            return key is >= Keys.A and <= Keys.Z
                or >= Keys.D0 and <= Keys.D9
                or Keys.Space or Keys.OemPeriod or Keys.OemComma or Keys.OemQuestion;
        }

        private static char KeyToChar(Keys key, KeyboardState kb)
        {
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            if (key >= Keys.A && key <= Keys.Z)
            {
                char ch = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(ch) : ch;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => '.',
                Keys.OemComma => ',',
                Keys.OemQuestion => '?',
                _ => '\0'
            };
        }

        private static string Truncate(string text, int max)
        {
            if (text.Length <= max)
            {
                return text;
            }

            return text[..(max - 3)] + "...";
        }
    }
}
