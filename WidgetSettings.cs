using System;
using System.Text.Json.Serialization;

namespace NowPlayingPopup
{
    public class WidgetSettings
    {
        [JsonPropertyName("playerAppearance")]
        public string PlayerAppearance { get; set; } = "boxy";

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "dark";

        [JsonPropertyName("tintColor")]
        public string TintColor { get; set; } = "1";

        [JsonPropertyName("magicColors")]
        public bool MagicColors { get; set; } = false;

        [JsonPropertyName("coverGlow")]
        public bool CoverGlow { get; set; } = false;

        [JsonPropertyName("opacity")]
        public double Opacity { get; set; } = 0.95;

        [JsonPropertyName("alwaysOnTop")]
        public bool AlwaysOnTop { get; set; } = true;

        [JsonPropertyName("showProgressBar")]
        public bool ShowProgressBar { get; set; } = true;

        [JsonPropertyName("showVolumeBar")]
        public bool ShowVolumeBar { get; set; } = true;

        [JsonPropertyName("enableVisualizer")]
        public bool EnableVisualizer { get; set; } = true;

        [JsonPropertyName("coverStyle")]
        public string CoverStyle { get; set; } = "picture";

        [JsonPropertyName("textAlignment")]
        public string TextAlignment { get; set; } = "left";

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 14;

        // Position settings
        [JsonPropertyName("positionX")]
        public double PositionX { get; set; } = 100;

        [JsonPropertyName("positionY")]
        public double PositionY { get; set; } = 100;

        [JsonPropertyName("rememberPosition")]
        public bool RememberPosition { get; set; } = true;

        [JsonPropertyName("popupPosition")]
        public string PopupPosition { get; set; } = "bottom-right";

        // Animation settings
        [JsonPropertyName("enableAnimations")]
        public bool EnableAnimations { get; set; } = true;

        [JsonPropertyName("animationSpeed")]
        public string AnimationSpeed { get; set; } = "normal";

        [JsonPropertyName("fadeInOut")]
        public bool FadeInOut { get; set; } = true;

        // Display settings
        [JsonPropertyName("showAlbumArt")]
        public bool ShowAlbumArt { get; set; } = true;

        [JsonPropertyName("showArtistName")]
        public bool ShowArtistName { get; set; } = true;

        [JsonPropertyName("showAlbumName")]
        public bool ShowAlbumName { get; set; } = true;

        [JsonPropertyName("showTrackTime")]
        public bool ShowTrackTime { get; set; } = true;

        // Visual customization
        [JsonPropertyName("cornerRadius")]
        public int CornerRadius { get; set; } = 8;

        [JsonPropertyName("borderWidth")]
        public int BorderWidth { get; set; } = 0;

        [JsonPropertyName("borderColor")]
        public string BorderColor { get; set; } = "#E2E8F0";

        [JsonPropertyName("shadowIntensity")]
        public double ShadowIntensity { get; set; } = 0.3;

        [JsonPropertyName("backgroundBlur")]
        public bool BackgroundBlur { get; set; } = false;

        [JsonPropertyName("customBackgroundColor")]
        public string? CustomBackgroundColor { get; set; } = null;

        public string[] ValidateSettings()
        {
            var errors = new List<string>();

            // Validate PlayerAppearance
            var validAppearances = new[] { "compact", "boxy", "gallery", "macos", "shell", "discord" };
            if (!validAppearances.Contains(PlayerAppearance))
            {
                errors.Add($"Invalid PlayerAppearance: {PlayerAppearance}");
            }

            // Validate Theme
            var validThemes = new[] { "dark", "light" };
            if (!validThemes.Contains(Theme))
            {
                errors.Add($"Invalid Theme: {Theme}");
            }

            // Validate TintColor
            if (!int.TryParse(TintColor, out int colorInt) || colorInt < 1 || colorInt > 8)
            {
                errors.Add($"Invalid TintColor: {TintColor} (must be 1-8)");
            }

            // Validate CoverStyle
            var validCoverStyles = new[] { "picture", "spinning-disc", "circle", "square", "rounded" };
            if (!validCoverStyles.Contains(CoverStyle))
            {
                errors.Add($"Invalid CoverStyle: {CoverStyle}");
            }

            // Validate ranges
            if (Opacity < 0.1 || Opacity > 1.0)
            {
                errors.Add("Opacity must be between 0.1 and 1.0");
            }

            if (FontSize < 8 || FontSize > 32)
            {
                errors.Add("FontSize must be between 8 and 32");
            }

            if (CornerRadius < 0 || CornerRadius > 50)
            {
                errors.Add("CornerRadius must be between 0 and 50");
            }

            if (BorderWidth < 0 || BorderWidth > 10)
            {
                errors.Add("BorderWidth must be between 0 and 10");
            }

            if (ShadowIntensity < 0 || ShadowIntensity > 1.0)
            {
                errors.Add("ShadowIntensity must be between 0 and 1.0");
            }

            return errors.ToArray();
        }

        public WidgetSettings Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
        }

        public (double Width, double Height) GetDimensions()
        {
            return PlayerAppearance switch
            {
                "compact" => (300, 80),
                "boxy" => (420, 140),
                "gallery" => (500, 300),
                "macos" => (380, 120),
                "shell" => (400, 100),
                "discord" => (350, 90),
                _ => (420, 140)
            };
        }
    }
}