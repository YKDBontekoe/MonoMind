using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;

namespace Autonocraft.UI
{
    public sealed class MainMenuSettingsScreen
    {
        private enum EditField
        {
            None,
            OpenRouterKey,
            OpenRouterModel,
            LlamaCppUrl,
            LlamaCppModel
        }

        private const float PanelWidth = 560f;
        private const float PanelHeight = 640f;
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 38f;
        private const float SliderWidth = 320f;
        private const float SliderTrackHeight = 12f;
        private const float SliderThumbSize = 22f;

        private enum SliderTarget
        {
            None,
            RenderDistance,
            MasterVolume,
            SfxVolume,
            AmbientVolume,
            MusicVolume
        }

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop(24);
        private readonly UiTransition _transition = new UiTransition();
        private GameSettings _working = new();
        private EditField _activeField = EditField.None;
        private string _openRouterKey = string.Empty;
        private string _openRouterModel = string.Empty;
        private string _llamaCppUrl = string.Empty;
        private string _llamaCppModel = string.Empty;
        private bool _sliderDragging;
        private SliderTarget _sliderTarget = SliderTarget.None;
        private int _hoveredButton = -1;

        public bool IsOpen { get; private set; }
        public bool SaveRequested { get; private set; }
        public bool CancelRequested { get; private set; }

        public MainMenuSettingsScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Open(GameSettings current)
        {
            _working = new GameSettings
            {
                RenderDistance = current.RenderDistance,
                PlayWithAi = current.PlayWithAi,
                AiProvider = current.AiProvider,
                OpenRouterModel = current.OpenRouterModel,
                OpenRouterApiKey = current.OpenRouterApiKey,
                LlamaCppBaseUrl = current.LlamaCppBaseUrl,
                LlamaCppModel = current.LlamaCppModel,
                MasterVolume = current.MasterVolume,
                SfxVolume = current.SfxVolume,
                AmbientVolume = current.AmbientVolume,
                MusicVolume = current.MusicVolume,
                MuteAudio = current.MuteAudio
            };
            _openRouterKey = current.OpenRouterApiKey ?? string.Empty;
            _openRouterModel = current.OpenRouterModel;
            _llamaCppUrl = current.LlamaCppBaseUrl;
            _llamaCppModel = current.LlamaCppModel;
            IsOpen = true;
            SaveRequested = false;
            CancelRequested = false;
            _activeField = EditField.None;
            _transition.BeginFadeIn(0.2f);
        }

        public void Close()
        {
            IsOpen = false;
            _activeField = EditField.None;
        }

        public GameSettings GetWorkingCopy() => ApplyEditsToSettings();

        public void Update(Viewport viewport, KeyboardState kb, KeyboardState prevKb, MouseState mouse, MouseState prevMouse, float deltaTime)
        {
            SaveRequested = false;
            CancelRequested = false;
            if (!IsOpen)
            {
                return;
            }

            _transition.Update(deltaTime);
            _backdrop.Update(deltaTime);

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CancelRequested = true;
                return;
            }

            HandleTextInput(kb, prevKb);

            var layout = new UiLayout(viewport);
            float cx = layout.CenterX;
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;
            float left = panelX + layout.S(24f);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);

            float y = panelY + layout.S(72f);
            float rowH = layout.S(34f);
            float renderSliderY = y + rowH * 1.1f;
            float audioHeaderY = y + rowH * 3.2f;
            float masterSliderY = audioHeaderY + rowH * 1.1f;
            float sfxSliderY = masterSliderY + rowH * 1.2f;
            float ambientSliderY = sfxSliderY + rowH * 1.2f;
            float musicSliderY = ambientSliderY + rowH * 1.2f;
            float muteY = musicSliderY + rowH * 1.1f;
            float aiHeaderY = muteY + rowH * 1.6f;
            float toggleY = aiHeaderY + rowH * 1.1f;
            float providerY = toggleY + rowH * 1.2f;
            float fieldStartY = providerY + rowH * 1.4f;

            var playAiRect = new Rectangle((int)left, (int)toggleY, (int)layout.S(220f), (int)layout.S(28f));
            var providerRect = new Rectangle((int)left, (int)providerY, (int)layout.S(320f), (int)layout.S(28f));
            var muteRect = new Rectangle((int)left, (int)muteY, (int)layout.S(220f), (int)layout.S(28f));
            var saveRect = GetButtonRect(cx - buttonW - layout.S(8f), panelY + panelH - layout.S(52f), buttonW, buttonH);
            var cancelRect = GetButtonRect(cx + layout.S(8f), panelY + panelH - layout.S(52f), buttonW, buttonH);

            UpdateSettingsSlider(viewport, mouse, prevMouse, renderSliderY, layout, SliderTarget.RenderDistance);
            UpdateSettingsSlider(viewport, mouse, prevMouse, masterSliderY, layout, SliderTarget.MasterVolume);
            UpdateSettingsSlider(viewport, mouse, prevMouse, sfxSliderY, layout, SliderTarget.SfxVolume);
            UpdateSettingsSlider(viewport, mouse, prevMouse, ambientSliderY, layout, SliderTarget.AmbientVolume);
            UpdateSettingsSlider(viewport, mouse, prevMouse, musicSliderY, layout, SliderTarget.MusicVolume);

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            _hoveredButton = -1;
            if (saveRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (cancelRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;

            if (click)
            {
                if (muteRect.Contains(mouse.X, mouse.Y))
                {
                    _working.MuteAudio = !_working.MuteAudio;
                }
                else if (playAiRect.Contains(mouse.X, mouse.Y))
                {
                    _working.PlayWithAi = !_working.PlayWithAi;
                }
                else if (providerRect.Contains(mouse.X, mouse.Y))
                {
                    _working.AiProvider = (AiProviderKind)(((int)_working.AiProvider + 1) % 4);
                }
                else if (TryHitField(left, fieldStartY, layout.S(420f), layout.S(26f), mouse, EditField.OpenRouterModel))
                {
                    _activeField = EditField.OpenRouterModel;
                }
                else if (TryHitField(left, fieldStartY + rowH, layout.S(420f), layout.S(26f), mouse, EditField.OpenRouterKey))
                {
                    _activeField = EditField.OpenRouterKey;
                }
                else if (TryHitField(left, fieldStartY + rowH * 2.2f, layout.S(420f), layout.S(26f), mouse, EditField.LlamaCppUrl))
                {
                    _activeField = EditField.LlamaCppUrl;
                }
                else if (TryHitField(left, fieldStartY + rowH * 3.4f, layout.S(420f), layout.S(26f), mouse, EditField.LlamaCppModel))
                {
                    _activeField = EditField.LlamaCppModel;
                }
                else if (_hoveredButton == 0)
                {
                    SaveRequested = true;
                }
                else if (_hoveredButton == 1)
                {
                    CancelRequested = true;
                }
                else
                {
                    _activeField = EditField.None;
                }
            }
        }

        public void Draw(Viewport viewport)
        {
            if (!IsOpen)
            {
                return;
            }

            float alpha = _transition.Alpha;
            var layout = new UiLayout(viewport);
            float cx = layout.CenterX;
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + _transition.OffsetY;
            float left = panelX + layout.S(24f);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float rowH = layout.S(34f);

            _backdrop.Draw(_ui, viewport, alpha * 0.85f);
            _ui.DrawFilledRect(0, 0, viewport.Width, viewport.Height, Color.Black * (0.35f * alpha));

            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.06f, 0.1f) * 0.96f, new Color(0.2f, 0.45f, 0.65f), alpha);
            _ui.DrawCenteredTitle("SETTINGS", panelY + layout.S(16f), layout.S(1.9f), new Color(0.82f, 0.92f, 1.0f), alpha);
            _ui.DrawHorizontalRule(panelX + layout.S(20f), panelY + layout.S(48f), panelW - layout.S(40f), new Color(0.16f, 0.32f, 0.48f), 1f, alpha * 0.7f);

            float y = panelY + layout.S(72f);
            _ui.DrawString("GRAPHICS", left, y, layout.S(1.2f), new Color(0.55f, 0.72f, 0.88f) * alpha);
            y += rowH;
            _ui.DrawString($"Render distance: {_working.RenderDistance}", left, y, layout.S(1.05f), Color.White * alpha);
            DrawSlider(left, y + layout.S(22f), layout, alpha, SliderTarget.RenderDistance, _working.RenderDistance, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);

            y += rowH * 3.2f;
            _ui.DrawString("AUDIO", left, y, layout.S(1.2f), new Color(0.55f, 0.72f, 0.88f) * alpha);
            y += rowH;
            DrawVolumeSlider(left, y, layout, alpha, SliderTarget.MasterVolume, "Master", _working.MasterVolume);
            y += rowH * 1.2f;
            DrawVolumeSlider(left, y, layout, alpha, SliderTarget.SfxVolume, "SFX", _working.SfxVolume);
            y += rowH * 1.2f;
            DrawVolumeSlider(left, y, layout, alpha, SliderTarget.AmbientVolume, "Ambient", _working.AmbientVolume);
            y += rowH * 1.2f;
            DrawVolumeSlider(left, y, layout, alpha, SliderTarget.MusicVolume, "Music", _working.MusicVolume);
            y += rowH * 1.1f;
            _ui.DrawString($"Mute audio: {(_working.MuteAudio ? "ON" : "OFF")} (click)", left, y, layout.S(1.05f), Color.White * alpha);

            y += rowH * 1.6f;
            _ui.DrawString("VILLAGE AI", left, y, layout.S(1.2f), new Color(0.55f, 0.72f, 0.88f) * alpha);
            y += rowH;
            _ui.DrawString($"Play with AI: {(_working.PlayWithAi ? "ON" : "OFF")} (click)", left, y, layout.S(1.05f), Color.White * alpha);
            y += rowH * 1.2f;
            _ui.DrawString($"Provider: {ProviderLabel(_working.AiProvider)} (click)", left, y, layout.S(1.05f), Color.White * alpha);

            y += rowH * 1.4f;
            DrawField(left, y, "OpenRouter model", _openRouterModel, EditField.OpenRouterModel, layout, alpha);
            y += rowH;
            DrawField(left, y, "OpenRouter API key", MaskKey(_openRouterKey), EditField.OpenRouterKey, layout, alpha);
            y += rowH * 1.2f;
            DrawField(left, y, "llama.cpp URL", _llamaCppUrl, EditField.LlamaCppUrl, layout, alpha);
            y += rowH;
            DrawField(left, y, "llama.cpp model", _llamaCppModel, EditField.LlamaCppModel, layout, alpha);

            y = panelY + panelH - layout.S(52f);
            DrawButton(cx - buttonW - layout.S(8f), y, buttonW, buttonH, "SAVE", _hoveredButton == 0, layout.S(1.4f), alpha);
            DrawButton(cx + layout.S(8f), y, buttonW, buttonH, "BACK", _hoveredButton == 1, layout.S(1.4f), alpha);

            _ui.DrawCenteredText("Run: llama-server -m model.gguf --port 8080", panelY + panelH - layout.S(18f), layout.S(0.95f), new Color(0.45f, 0.5f, 0.55f) * alpha, 0.85f);
        }

        private GameSettings ApplyEditsToSettings()
        {
            _working.OpenRouterApiKey = string.IsNullOrWhiteSpace(_openRouterKey) ? null : _openRouterKey.Trim();
            _working.OpenRouterModel = _openRouterModel.Trim();
            _working.LlamaCppBaseUrl = string.IsNullOrWhiteSpace(_llamaCppUrl) ? GameSettings.DefaultLlamaCppBaseUrl : _llamaCppUrl.Trim();
            _working.LlamaCppModel = _llamaCppModel.Trim();
            _working.Clamp();
            return _working;
        }

        private void UpdateSettingsSlider(Viewport viewport, MouseState mouse, MouseState prevMouse, float sliderY, UiLayout layout, SliderTarget target)
        {
            float sliderW = layout.S(SliderWidth);
            float left = layout.CenterX - sliderW / 2f;
            float trackH = layout.S(SliderTrackHeight);
            float thumb = layout.S(SliderThumbSize);
            var trackRect = new Rectangle((int)left, (int)(sliderY - trackH / 2f), (int)sliderW, (int)trackH);

            bool mouseDown = mouse.LeftButton == ButtonState.Pressed;
            if (_sliderTarget == target && _sliderDragging)
            {
                if (!mouseDown)
                {
                    _sliderDragging = false;
                    _sliderTarget = SliderTarget.None;
                    return;
                }

                float t = Math.Clamp((mouse.X - left) / sliderW, 0f, 1f);
                ApplySliderValue(target, t);
                return;
            }

            if (mouseDown && prevMouse.LeftButton == ButtonState.Released && trackRect.Contains(mouse.X, mouse.Y))
            {
                _sliderDragging = true;
                _sliderTarget = target;
                float t = Math.Clamp((mouse.X - left) / sliderW, 0f, 1f);
                ApplySliderValue(target, t);
            }
        }

        private void ApplySliderValue(SliderTarget target, float t)
        {
            switch (target)
            {
                case SliderTarget.RenderDistance:
                    _working.RenderDistance = (int)MathF.Round(
                        GameSettings.MinRenderDistance + t * (GameSettings.MaxRenderDistance - GameSettings.MinRenderDistance));
                    break;
                case SliderTarget.MasterVolume:
                    _working.MasterVolume = t;
                    break;
                case SliderTarget.SfxVolume:
                    _working.SfxVolume = t;
                    break;
                case SliderTarget.AmbientVolume:
                    _working.AmbientVolume = t;
                    break;
                case SliderTarget.MusicVolume:
                    _working.MusicVolume = t;
                    break;
            }
        }

        private void DrawSlider(float left, float sliderY, UiLayout layout, float alpha, SliderTarget target, float value, float min, float max)
        {
            float sliderW = layout.S(SliderWidth);
            float trackH = layout.S(SliderTrackHeight);
            float thumb = layout.S(SliderThumbSize);
            float t = max <= min ? 0f : (value - min) / (max - min);
            float thumbX = left + t * sliderW - thumb / 2f;
            _ui.DrawProgressBar(left, sliderY - trackH / 2f, sliderW, trackH, t, string.Empty, layout.Scale, alpha);
            _ui.DrawPanel(thumbX, sliderY - thumb / 2f, thumb, thumb, new Color(0.55f, 0.78f, 0.98f) * alpha, new Color(0.2f, 0.45f, 0.65f), 0.8f, alpha);
        }

        private void DrawVolumeSlider(float left, float y, UiLayout layout, float alpha, SliderTarget target, string label, float value)
        {
            _ui.DrawString($"{label}: {MathF.Round(value * 100f)}%", left, y, layout.S(1.0f), Color.White * alpha);
            DrawSlider(left, y + layout.S(22f), layout, alpha, target, value, 0f, 1f);
        }

        private void DrawSlider(float left, float sliderY, UiLayout layout, float alpha)
        {
            DrawSlider(left, sliderY, layout, alpha, SliderTarget.RenderDistance, _working.RenderDistance, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);
        }

        private void DrawField(float x, float y, string label, string value, EditField field, UiLayout layout, float alpha)
        {
            bool active = _activeField == field;
            _ui.DrawString(label.ToUpperInvariant(), x, y, layout.S(0.95f), new Color(0.5f, 0.6f, 0.65f) * alpha);
            string display = active ? value + "_" : value;
            _ui.DrawString(display, x, y + layout.S(16f), layout.S(1.0f), (active ? new Color(0.9f, 0.95f, 1f) : Color.White) * alpha);
        }

        private void HandleTextInput(KeyboardState kb, KeyboardState prevKb)
        {
            if (_activeField == EditField.None)
            {
                return;
            }

            ref string target = ref GetActiveBuffer();
            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back) && target.Length > 0)
            {
                target = target[..^1];
                return;
            }

            if (kb.IsKeyDown(Keys.Tab) && !prevKb.IsKeyDown(Keys.Tab))
            {
                _activeField = _activeField switch
                {
                    EditField.OpenRouterModel => EditField.OpenRouterKey,
                    EditField.OpenRouterKey => EditField.LlamaCppUrl,
                    EditField.LlamaCppUrl => EditField.LlamaCppModel,
                    _ => EditField.OpenRouterModel
                };
                return;
            }

            foreach (Keys key in Enum.GetValues<Keys>())
            {
                if (!kb.IsKeyDown(key) || prevKb.IsKeyDown(key))
                {
                    continue;
                }

                char? ch = KeyToChar(key, kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
                if (ch.HasValue && target.Length < 120)
                {
                    target += ch.Value;
                }
            }
        }

        private ref string GetActiveBuffer()
        {
            switch (_activeField)
            {
                case EditField.OpenRouterKey:
                    return ref _openRouterKey;
                case EditField.OpenRouterModel:
                    return ref _openRouterModel;
                case EditField.LlamaCppUrl:
                    return ref _llamaCppUrl;
                case EditField.LlamaCppModel:
                    return ref _llamaCppModel;
                default:
                    throw new InvalidOperationException("No text field is active.");
            }
        }

        private static bool TryHitField(float x, float y, float w, float h, MouseState mouse, EditField field)
        {
            return new Rectangle((int)x, (int)y, (int)w, (int)h).Contains(mouse.X, mouse.Y);
        }

        private static string ProviderLabel(AiProviderKind kind) => kind switch
        {
            AiProviderKind.Disabled => "Off",
            AiProviderKind.Mock => "Mock",
            AiProviderKind.OpenRouter => "OpenRouter",
            AiProviderKind.LlamaCpp => "llama.cpp Local",
            _ => kind.ToString()
        };

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "(empty — uses env/file)";
            }

            return key.Length <= 4 ? "****" : key[..4] + "…";
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
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.Space => ' ',
                _ => null
            };
        }

        private void DrawButton(float x, float y, float w, float h, string label, bool hovered, float textSize, float alpha)
        {
            _ui.DrawButton(x, y, w, h, label, hovered, false, textSize, alpha);
        }

        private static Rectangle GetButtonRect(float x, float y, float w, float h) => new((int)x, (int)y, (int)w, (int)h);
    }
}
