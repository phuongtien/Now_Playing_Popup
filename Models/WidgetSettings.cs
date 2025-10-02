using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NowPlayingPopup.Models
{
    /// <summary>
    /// Represents widget configuration settings with validation
    /// </summary>
    public class WidgetSettings
    {
        // Appearance
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

        // Display toggles
        [JsonPropertyName("showProgressBar")]
        public bool ShowProgressBar { get; set; } = true;

        [JsonPropertyName("showVolumeBar")]
        public bool ShowVolumeBar { get; set; } = true;

        [JsonPropertyName("enableVisualizer")]
        public bool EnableVisualizer { get; set; } = true;

        [JsonPropertyName("showAlbumArt")]
        public bool ShowAlbumArt { get; set; } = true;

        [JsonPropertyName("showArtistName")]
        public bool ShowArtistName { get; set; } = true;

        [JsonPropertyName("showAlbumName")]
        public bool ShowAlbumName { get; set; } = true;

        [JsonPropertyName("showTrackTime")]
        public bool ShowTrackTime { get; set; } = true;

        // Style
        [JsonPropertyName("coverStyle")]
        public string CoverStyle { get; set; } = "picture";

        [JsonPropertyName("textAlignment")]
        public string TextAlignment { get; set; } = "left";

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 14;

        // Position
        [JsonPropertyName("positionX")]
        public double PositionX { get; set; } = 100;

        [JsonPropertyName("positionY")]
        public double PositionY { get; set; } = 100;

        [JsonPropertyName("rememberPosition")]
        public bool RememberPosition { get; set; } = true;

        [JsonPropertyName("popupPosition")]
        public string PopupPosition { get; set; } = "bottom-right";

        // Animation
        [JsonPropertyName("enableAnimations")]
        public bool EnableAnimations { get; set; } = true;

        [JsonPropertyName("animationSpeed")]
        public string AnimationSpeed { get; set; } = "normal";

        [JsonPropertyName("fadeInOut")]
        public bool FadeInOut { get; set; } = true;

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

        // Validation rules
        private static readonly string[] ValidAppearances = { "compact", "boxy", "gallery", "macos", "shell", "discord" };
        private static readonly string[] ValidThemes = { "dark", "light" };
        private static readonly string[] ValidCoverStyles = { "picture", "spinning-disc", "circle", "square", "rounded" };
        private static readonly string[] ValidTextAlignments = { "left", "center", "right" };
        private static readonly string[] ValidAnimationSpeeds = { "slow", "normal", "fast" };
        private static readonly string[] ValidPopupPositions = { "top-left", "top-right", "bottom-left", "bottom-right" };

        public ValidationResult Validate()
        {
            var errors = new List<string>();

            // Validate enums
            if (!ValidAppearances.Contains(PlayerAppearance))
                errors.Add($"Invalid PlayerAppearance: {PlayerAppearance}. Valid: {string.Join(", ", ValidAppearances)}");

            if (!ValidThemes.Contains(Theme))
                errors.Add($"Invalid Theme: {Theme}. Valid: {string.Join(", ", ValidThemes)}");

            if (!ValidCoverStyles.Contains(CoverStyle))
                errors.Add($"Invalid CoverStyle: {CoverStyle}. Valid: {string.Join(", ", ValidCoverStyles)}");

            if (!ValidTextAlignments.Contains(TextAlignment))
                errors.Add($"Invalid TextAlignment: {TextAlignment}. Valid: {string.Join(", ", ValidTextAlignments)}");

            if (!ValidAnimationSpeeds.Contains(AnimationSpeed))
                errors.Add($"Invalid AnimationSpeed: {AnimationSpeed}. Valid: {string.Join(", ", ValidAnimationSpeeds)}");

            if (!ValidPopupPositions.Contains(PopupPosition))
                errors.Add($"Invalid PopupPosition: {PopupPosition}. Valid: {string.Join(", ", ValidPopupPositions)}");

            // Validate TintColor
            if (!int.TryParse(TintColor, out int colorInt) || colorInt < 1 || colorInt > 8)
                errors.Add($"Invalid TintColor: {TintColor} (must be 1-8)");

            // Validate ranges
            if (Opacity < 0.1 || Opacity > 1.0)
                errors.Add("Opacity must be between 0.1 and 1.0");

            if (FontSize < 8 || FontSize > 32)
                errors.Add("FontSize must be between 8 and 32");

            if (CornerRadius < 0 || CornerRadius > 50)
                errors.Add("CornerRadius must be between 0 and 50");

            if (BorderWidth < 0 || BorderWidth > 10)
                errors.Add("BorderWidth must be between 0 and 10");

            if (ShadowIntensity < 0 || ShadowIntensity > 1.0)
                errors.Add("ShadowIntensity must be between 0 and 1.0");

            // Validate color format
            if (!string.IsNullOrEmpty(BorderColor) && !IsValidHexColor(BorderColor))
                errors.Add($"Invalid BorderColor format: {BorderColor}");

            if (!string.IsNullOrEmpty(CustomBackgroundColor) && !IsValidHexColor(CustomBackgroundColor))
                errors.Add($"Invalid CustomBackgroundColor format: {CustomBackgroundColor}");

            return new ValidationResult(errors);
        }

        // Legacy method for backward compatibility
        public string[] ValidateSettings() => Validate().Errors.ToArray();

        private static bool IsValidHexColor(string color)
        {
            if (string.IsNullOrEmpty(color)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(color, @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
        }

        public WidgetSettings Clone()
        {
            var json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
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

        public static class Defaults
        {
            public static string[] GetValidAppearances() => (string[])ValidAppearances.Clone();
            public static string[] GetValidThemes() => (string[])ValidThemes.Clone();
            public static string[] GetValidCoverStyles() => (string[])ValidCoverStyles.Clone();
            public static string[] GetValidTextAlignments() => (string[])ValidTextAlignments.Clone();
            public static string[] GetValidAnimationSpeeds() => (string[])ValidAnimationSpeeds.Clone();
            public static string[] GetValidPopupPositions() => (string[])ValidPopupPositions.Clone();
        }
    }

    /// <summary>
    /// Validation result with helper methods
    /// </summary>
    public class ValidationResult
    {
        public List<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;

        public ValidationResult(List<string> errors)
        {
            Errors = errors ?? new List<string>();
        }

        public string GetErrorMessage(string separator = "\n")
        {
            return string.Join(separator, Errors);
        }

        public void ThrowIfInvalid()
        {
            if (!IsValid)
                throw new ValidationException(GetErrorMessage());
        }
    }

    /// <summary>
    /// Exception for validation errors
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }
}