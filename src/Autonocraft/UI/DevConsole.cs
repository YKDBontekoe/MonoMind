using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.UI.Menu;
using DevCommands = Autonocraft.Core.DevCommands.DevCommandRouter;

namespace Autonocraft.UI
{
    public class DevConsole
    {
        private const int MaxLines = 100;

        private readonly UiRenderer _ui;
        private readonly List<string> _lines = new();
        private readonly List<string> _history = new();
        private string _input = string.Empty;
        private int _historyIndex = -1;

        public bool IsOpen { get; private set; }

        public DevConsole(UiRenderer ui)
        {
            _ui = ui;
            Log("Dev console ready. Press F3 or ` to toggle. Type 'help' for commands.");
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
        }

        public void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            foreach (string line in message.Split('\n'))
            {
                _lines.Add(line);
            }

            while (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
            }
        }

        public void Update(Viewport viewport, KeyboardState kb, KeyboardState prevKb, GameHostContext host)
        {
            if (!IsOpen) return;

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                IsOpen = false;
                return;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                SubmitInput(host);
                return;
            }

            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back))
            {
                if (_input.Length > 0)
                    _input = _input[..^1];
                _historyIndex = -1;
                return;
            }

            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
            {
                NavigateHistory(-1);
                return;
            }

            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
            {
                NavigateHistory(1);
                return;
            }

            if (TextInputKeys.AppendPressedCharacters(kb, prevKb, ref _input, int.MaxValue, TextInputCharacterSet.Console))
            {
                _historyIndex = -1;
            }
        }

        public void Draw(Viewport viewport)
        {
            if (!IsOpen) return;

            var layout = new UiLayout(viewport);
            float fontSize = layout.S(UiTheme.FontSmall);
            float lineHeight = fontSize + layout.S(4f);
            float panelH = layout.Height * 0.45f;
            float panelW = layout.Width;
            float panelX = 0f;
            float panelY = 0f;
            float contentPadding = layout.S(12f);

            _ui.DrawPanel(panelX, panelY, panelW, panelH,
                UiTheme.PanelBgMuted * 0.92f,
                UiTheme.Accent, 0.9f);

            _ui.DrawString("Dev console", panelX + contentPadding, panelY + layout.S(8f), layout.S(UiTheme.FontSection), UiTheme.Accent);

            float contentX = panelX + contentPadding;
            float contentY = panelY + layout.S(32f);
            float contentW = panelW - contentPadding * 2f;
            float contentH = panelH - layout.S(72f);

            int visibleLines = Math.Max(1, (int)(contentH / lineHeight));
            int startLine = Math.Max(0, _lines.Count - visibleLines);

            for (int i = startLine; i < _lines.Count; i++)
            {
                float y = contentY + (i - startLine) * lineHeight;
                var color = _lines[i].StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) ||
                            _lines[i].StartsWith("Usage:", StringComparison.OrdinalIgnoreCase)
                    ? UiTheme.Danger
                    : UiTheme.StatValue;
                _ui.DrawString(_lines[i], contentX, y, fontSize, color);
            }

            float inputY = panelY + panelH - layout.S(36f);
            _ui.DrawFilledRect(contentX - 2f, inputY - layout.S(4f), contentW + 4f, layout.S(28f), UiTheme.PanelFill * 0.95f);
            _ui.DrawString("> " + _input + "_", contentX, inputY, fontSize, UiTheme.Title);
        }

        private void SubmitInput(GameHostContext host)
        {
            string trimmed = _input.Trim();
            if (trimmed.Length == 0)
                return;

            Log("> " + trimmed);
            _history.Add(trimmed);
            _historyIndex = -1;
            _input = string.Empty;

            string result = DevCommands.Execute(host, trimmed);
            if (result == "__CLEAR__")
            {
                _lines.Clear();
                Log("Console cleared.");
            }
            else if (!string.IsNullOrEmpty(result))
            {
                Log(result);
            }
        }

        private void NavigateHistory(int direction)
        {
            if (_history.Count == 0) return;

            if (_historyIndex < 0)
                _historyIndex = _history.Count;

            _historyIndex += direction;
            _historyIndex = Math.Clamp(_historyIndex, 0, _history.Count);

            _input = _historyIndex >= _history.Count ? string.Empty : _history[_historyIndex];
        }

    }
}
