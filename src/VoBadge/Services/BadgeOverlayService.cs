using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.VoBadge.Services;

/// <summary>
/// Service that handles adding/removing the VO badge overlay on poster images.
/// </summary>
public class BadgeOverlayService
{
    private readonly ILogger<BadgeOverlayService> _logger;
    private readonly object _sync = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BadgeOverlayService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public BadgeOverlayService(ILogger<BadgeOverlayService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a badge overlay to the specified image file.
    /// Backs up the original image before modifying.
    /// </summary>
    /// <param name="imagePath">Path to the poster image.</param>
    /// <param name="badgeText">Text to display on the badge.</param>
    /// <param name="badgeColorHex">Badge background color in hex.</param>
    /// <param name="textColorHex">Badge text color in hex.</param>
    /// <param name="badgeSizePercent">Badge size as a percentage of image width.</param>
    /// <param name="filled">True to fill the pill with the badge color. False draws a dark pill with a colored stroke.</param>
    /// <returns>True if the badge was successfully added.</returns>
    public bool AddBadge(string imagePath, string badgeText, string badgeColorHex, string textColorHex, int badgeSizePercent, bool filled = false)
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning("Image file not found: {Path}", imagePath);
                    return false;
                }

                var backupPath = GetManagedBackupPath(imagePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                EnsureManagedBackup(imagePath, backupPath);

                if (File.Exists(backupPath) && !IsCurrentImageOurBadge(imagePath))
                {
                    DeleteBackup(imagePath);
                }

                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, imagePath, true);
                }
                else
                {
                    File.Copy(imagePath, backupPath, false);
                }

                using var originalBitmap = SKBitmap.Decode(imagePath);
                if (originalBitmap is null)
                {
                    _logger.LogWarning("Failed to decode image: {Path}", imagePath);
                    return false;
                }

                var width = originalBitmap.Width;
                var height = originalBitmap.Height;

                using var surface = SKSurface.Create(new SKImageInfo(width, height));
                var canvas = surface.Canvas;
                canvas.DrawBitmap(originalBitmap, 0, 0);
                DrawPill(canvas, width, badgeText, badgeColorHex, textColorHex, badgeSizePercent, accentStroke: !filled);

                canvas.Flush();
                using var image = surface.Snapshot();
                using var data = image.Encode(GetImageFormat(imagePath), 95);
                if (data is null)
                {
                    _logger.LogWarning("Failed to encode badged image: {Path}", imagePath);
                    return false;
                }

                File.WriteAllBytes(imagePath, data.ToArray());
                WriteStamp(imagePath, badgeText);

                _logger.LogDebug("Badge added to image: {Path}", imagePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding badge to image: {Path}", imagePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Removes the badge overlay by restoring the original backed-up image.
    /// </summary>
    /// <param name="imagePath">Path to the poster image.</param>
    /// <returns>True if the original was restored.</returns>
    public bool RemoveBadge(string imagePath)
    {
        lock (_sync)
        {
            try
            {
                var backupPath = FindExistingBackup(imagePath);
                if (backupPath is null)
                {
                    _logger.LogDebug("No backup found, image was not modified: {Path}", imagePath);
                    return false;
                }

                if (!IsCurrentImageOurBadge(imagePath))
                {
                    DeleteBackup(imagePath);
                    _logger.LogDebug("Poster changed since badge was applied, discarded stale backup: {Path}", imagePath);
                    return false;
                }

                File.Copy(backupPath, imagePath, true);
                DeleteBackup(imagePath);
                _logger.LogDebug("Restored original image from backup: {Path}", imagePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring original image: {Path}", imagePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Gets the label stored when the current badge was drawn.
    /// </summary>
    /// <param name="imagePath">Path to the poster image.</param>
    /// <returns>The badge label, or null if unknown.</returns>
    public string? GetBadgeLabel(string imagePath)
    {
        lock (_sync)
        {
            var stampPath = GetStampPath(imagePath);
            if (!File.Exists(stampPath))
            {
                return null;
            }

            var parts = File.ReadAllText(stampPath).Trim().Split('|');
            return parts.Length > 1 ? parts[1] : null;
        }
    }

    /// <summary>
    /// Checks if a backup exists for the given image (i.e., the image has a badge).
    /// </summary>
    /// <param name="imagePath">Path to the poster image.</param>
    /// <returns>True if a backup exists.</returns>
    public bool HasBadge(string imagePath)
    {
        lock (_sync)
        {
            if (FindExistingBackup(imagePath) is null)
            {
                return false;
            }

            if (IsCurrentImageOurBadge(imagePath))
            {
                return true;
            }

            DeleteBackup(imagePath);
            return false;
        }
    }

    private bool IsCurrentImageOurBadge(string imagePath)
    {
        var stampPath = GetStampPath(imagePath);
        if (!File.Exists(stampPath) || !File.Exists(imagePath))
        {
            return FindExistingBackup(imagePath) is not null;
        }

        var expected = File.ReadAllText(stampPath).Trim().Split('|')[0];
        var actual = ComputeHash(imagePath);
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteStamp(string imagePath, string label)
    {
        File.WriteAllText(GetStampPath(imagePath), ComputeHash(imagePath) + "|" + label);
    }

    private static void DrawPill(SKCanvas canvas, int width, string badgeText, string badgeColorHex, string textColorHex, int badgeSizePercent, bool accentStroke)
    {
        var text = string.IsNullOrWhiteSpace(badgeText) ? "VO" : badgeText.Trim();
        var scale = Math.Clamp(badgeSizePercent, 5, 50) / 18f;
        var pillHeight = width * 0.072f * scale;
        var accent = ParseColor(badgeColorHex, new SKColor(196, 163, 90));
        var textColor = ParseColor(textColorHex, new SKColor(246, 241, 231));

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var font = new SKFont(typeface ?? SKTypeface.Default, pillHeight * 0.46f);
        var textWidth = font.MeasureText(text);
        var padX = pillHeight * 0.62f;
        var pillWidth = Math.Max(textWidth + (padX * 2f), pillHeight * 1.7f);
        var margin = width * 0.038f;
        var x = width - pillWidth - margin;
        var y = margin;
        var radius = pillHeight / 2f;
        var rect = new SKRoundRect(new SKRect(x, y, x + pillWidth, y + pillHeight), radius, radius);

        using var shadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 90),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, pillHeight * 0.18f)
        };
        canvas.DrawRoundRect(rect, shadow);

        using var fill = new SKPaint
        {
            Color = accentStroke ? new SKColor(16, 16, 16, 214) : accent.WithAlpha(230),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(rect, fill);

        if (accentStroke)
        {
            using var stroke = new SKPaint
            {
                Color = accent,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1.5f, pillHeight * 0.045f)
            };
            canvas.DrawRoundRect(rect, stroke);
        }

        using var textPaint = new SKPaint
        {
            Color = textColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        var metrics = font.Metrics;
        var textX = x + (pillWidth / 2f);
        var textY = y + (pillHeight / 2f) - ((metrics.Ascent + metrics.Descent) / 2f);
        canvas.DrawText(text, textX, textY, SKTextAlign.Center, font, textPaint);
    }

    private static void DeleteBackup(string imagePath)
    {
        TryDelete(GetManagedBackupPath(imagePath));
        TryDelete(GetStampPath(imagePath));
        TryDelete(GetLegacyBackupPath(imagePath));
        TryDelete(GetLegacyStampPath(imagePath));
    }

    private static void EnsureManagedBackup(string imagePath, string backupPath)
    {
        if (File.Exists(backupPath))
        {
            return;
        }

        var legacy = GetLegacyBackupPath(imagePath);
        if (!string.Equals(legacy, backupPath, StringComparison.OrdinalIgnoreCase) && File.Exists(legacy))
        {
            File.Copy(legacy, backupPath, false);
            TryDelete(legacy);
            TryDelete(GetLegacyStampPath(imagePath));
        }
    }

    private static string? FindExistingBackup(string imagePath)
    {
        var managed = GetManagedBackupPath(imagePath);
        if (File.Exists(managed))
        {
            return managed;
        }

        var legacy = GetLegacyBackupPath(imagePath);
        return File.Exists(legacy) ? legacy : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string ComputeHash(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static SKColor ParseColor(string? hex, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        try
        {
            return SKColor.Parse(hex.Trim());
        }
        catch
        {
            return fallback;
        }
    }

    private static SKEncodedImageFormat GetImageFormat(string imagePath)
    {
        return Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg
        };
    }

    private static string GetManagedBackupPath(string imagePath)
    {
        var dataFolder = Plugin.Instance?.DataFolderPath;
        if (string.IsNullOrEmpty(dataFolder))
        {
            return GetLegacyBackupPath(imagePath);
        }

        return Path.Combine(dataFolder, "backups", GetPathKey(imagePath) + Path.GetExtension(imagePath));
    }

    private static string GetStampPath(string imagePath)
    {
        return GetManagedBackupPath(imagePath) + ".stamp";
    }

    private static string GetLegacyBackupPath(string imagePath)
    {
        var directory = Path.GetDirectoryName(imagePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(imagePath);
        var extension = Path.GetExtension(imagePath);
        return Path.Combine(directory, $"{fileName}_vobadge_original{extension}");
    }

    private static string GetLegacyStampPath(string imagePath)
    {
        return GetLegacyBackupPath(imagePath) + ".stamp";
    }

    private static string GetPathKey(string imagePath)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imagePath)));
    }
}
