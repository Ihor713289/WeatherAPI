using System.Globalization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WeatherAPI.Shared.Contracts;

namespace ImageGenerator.Services;

/// <summary>Draws the weather overlay onto a background. Pure (no I/O), so it is easy to unit test.</summary>
public static class WeatherImageRenderer
{
    // Resolved once per process: bundled Fonts/*.ttf first, system fonts as a fallback.
    private static readonly Lazy<FontFamily> FontFamily = new(ResolveFontFamily);

    public static void Render(Image<Rgba32> image, WeatherStation station, Image<Rgba32>? icon, string? attribution)
    {
        var family = FontFamily.Value;
        var padding = image.Width * 0.03f;
        var iconSize = (int)(image.Width * 0.12f);

        var textX = padding * 2 + (icon is null ? 0 : iconSize + padding);
        // Keep the panel inside the image: panel starts at `padding`, text has `padding` right padding in it.
        var maxTextWidth = image.Width - textX - padding * 2;

        var temperatureOptions = new RichTextOptions(family.CreateFont(image.Width * 0.08f, FontStyle.Bold))
        {
            WrappingLength = maxTextWidth
        };
        var detailsOptions = new RichTextOptions(family.CreateFont(image.Width * 0.035f, FontStyle.Regular))
        {
            WrappingLength = maxTextWidth
        };

        var temperatureText = station.Temperature is { } temperature
            ? temperature.ToString("0.0", CultureInfo.InvariantCulture) + " °C"
            : "n/a";
        var detailsText = string.IsNullOrWhiteSpace(station.WeatherDescription)
            ? station.Name
            : $"{station.Name}\n{station.WeatherDescription}";

        var temperatureSize = TextMeasurer.MeasureSize(temperatureText, temperatureOptions);
        var detailsSize = TextMeasurer.MeasureSize(detailsText, detailsOptions);

        var textBlockHeight = temperatureSize.Height + padding / 2 + detailsSize.Height;
        var contentHeight = Math.Max(icon is null ? 0 : iconSize, textBlockHeight);
        // Panel starts at x = padding, so this leaves `padding` of space right of the widest text line.
        var panelWidth = textX + Math.Max(temperatureSize.Width, detailsSize.Width);
        var panelHeight = contentHeight + padding * 2;
        var panel = new RectangularPolygon(padding, padding, panelWidth, panelHeight);

        var textY = padding * 2 + (contentHeight - textBlockHeight) / 2;
        temperatureOptions.Origin = new PointF(textX, textY);
        detailsOptions.Origin = new PointF(textX, textY + temperatureSize.Height + padding / 2);

        icon?.Mutate(i => i.Resize(iconSize, iconSize));

        image.Mutate(ctx =>
        {
            ctx.Fill(Color.Black.WithAlpha(0.55f), panel);

            if (icon is not null)
            {
                var iconY = padding * 2 + (contentHeight - iconSize) / 2;
                ctx.DrawImage(icon, new Point((int)(padding * 2), (int)iconY), 1f);
            }

            ctx.DrawText(temperatureOptions, temperatureText, Color.White);
            ctx.DrawText(detailsOptions, detailsText, Color.LightGray);

            if (!string.IsNullOrWhiteSpace(attribution))
            {
                DrawAttribution(ctx, image.Width, image.Height, family, attribution);
            }
        });
    }

    private static void DrawAttribution(IImageProcessingContext ctx, int width, int height, FontFamily family,
        string attribution)
    {
        var margin = width * 0.015f;
        var options = new RichTextOptions(family.CreateFont(width * 0.018f, FontStyle.Regular));
        var size = TextMeasurer.MeasureSize(attribution, options);

        var x = width - size.Width - margin * 2;
        var y = height - size.Height - margin * 2;
        options.Origin = new PointF(x + margin / 2, y + margin / 2);

        ctx.Fill(Color.Black.WithAlpha(0.45f), new RectangularPolygon(x, y, size.Width + margin, size.Height + margin));
        ctx.DrawText(options, attribution, Color.White);
    }

    private static FontFamily ResolveFontFamily()
    {
        // Prefer the fonts shipped with the app (Fonts/*.ttf), so output is identical on every machine and
        // works in container images without system fonts.
        var fontsDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "Fonts");
        if (Directory.Exists(fontsDirectory))
        {
            var collection = new FontCollection();
            FontFamily? bundled = null;
            foreach (var file in Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
            {
                // Regular and bold share the family name, so both styles end up in the same family.
                var family = collection.Add(file);
                bundled ??= family;
            }

            if (bundled is { } bundledFamily)
            {
                return bundledFamily;
            }
        }

        foreach (var name in new[] { "DejaVu Sans", "Verdana", "Liberation Sans", "Arial" })
        {
            if (SystemFonts.TryGet(name, out var family))
            {
                return family;
            }
        }

        foreach (var family in SystemFonts.Families)
        {
            return family;
        }

        throw new InvalidOperationException(
            "No system fonts installed. Install a font package (e.g. fonts-dejavu-core) in the container.");
    }
}
