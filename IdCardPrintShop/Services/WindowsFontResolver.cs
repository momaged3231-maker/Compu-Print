using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using PdfSharp.Fonts;

namespace IdCardPrintShop.Services
{
    /// <summary>
    /// Production Windows Font Resolver for PDFsharp 6.x.
    /// Safely resolves system fonts (Arial, Segoe UI, Tahoma) and provides fallbacks.
    /// </summary>
    public class WindowsFontResolver : IFontResolver
    {
        private static readonly ConcurrentDictionary<string, byte[]> FontCache = new(StringComparer.OrdinalIgnoreCase);
        private static bool _isRegistered;
        private static readonly object _lock = new();

        public static void EnsureRegistered()
        {
            if (_isRegistered) return;
            lock (_lock)
            {
                if (_isRegistered) return;
                try
                {
                    if (GlobalFontSettings.FontResolver == null)
                    {
                        GlobalFontSettings.FontResolver = new WindowsFontResolver();
                    }
                }
                catch
                {
                    // Ignore if already registered by another component or test runner
                }
                finally
                {
                    _isRegistered = true;
                }
            }
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            string suffix = "";
            if (isBold && isItalic) suffix = "-BoldItalic";
            else if (isBold) suffix = "-Bold";
            else if (isItalic) suffix = "-Italic";

            string faceName = $"{familyName}{suffix}";
            return new FontResolverInfo(faceName);
        }

        public byte[]? GetFont(string faceName)
        {
            if (FontCache.TryGetValue(faceName, out var cached))
                return cached;

            byte[]? data = LoadFontData(faceName);
            if (data != null)
            {
                FontCache[faceName] = data;
                return data;
            }

            // Fallback to Arial regular or first available ttf in fonts directory
            if (FontCache.TryGetValue("fallback", out var fallbackCached))
                return fallbackCached;

            byte[]? fallback = LoadFontData("Arial");
            if (fallback == null)
            {
                try
                {
                    string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                    if (Directory.Exists(fontsDir))
                    {
                        var anyTtf = Directory.GetFiles(fontsDir, "*.ttf").FirstOrDefault();
                        if (anyTtf != null && File.Exists(anyTtf))
                        {
                            fallback = File.ReadAllBytes(anyTtf);
                        }
                    }
                }
                catch
                {
                    // Suppress
                }
            }

            if (fallback != null)
            {
                FontCache["fallback"] = fallback;
                return fallback;
            }

            return null;
        }

        private static byte[]? LoadFontData(string faceName)
        {
            try
            {
                string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                if (!Directory.Exists(fontsDir))
                    return null;

                string lower = faceName.ToLowerInvariant();
                string fileName = "arial.ttf";

                if (lower.Contains("arial"))
                {
                    if (lower.Contains("bold") && lower.Contains("italic")) fileName = "arialbi.ttf";
                    else if (lower.Contains("bold")) fileName = "arialbd.ttf";
                    else if (lower.Contains("italic")) fileName = "ariali.ttf";
                    else fileName = "arial.ttf";
                }
                else if (lower.Contains("segoe"))
                {
                    if (lower.Contains("bold")) fileName = "segoeuib.ttf";
                    else fileName = "segoeui.ttf";
                }
                else if (lower.Contains("tahoma"))
                {
                    if (lower.Contains("bold")) fileName = "tahomabd.ttf";
                    else fileName = "tahoma.ttf";
                }

                string fullPath = Path.Combine(fontsDir, fileName);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }

                // If specific file not found, try arial.ttf
                string arialPath = Path.Combine(fontsDir, "arial.ttf");
                if (File.Exists(arialPath))
                {
                    return File.ReadAllBytes(arialPath);
                }
            }
            catch
            {
                // Suppress font read errors
            }

            return null;
        }
    }
}
