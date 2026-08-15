using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using GeometryGroup = System.Windows.Media.GeometryGroup;
using EllipseGeometry = System.Windows.Media.EllipseGeometry;
using LineGeometry = System.Windows.Media.LineGeometry;
using RectangleGeometry = System.Windows.Media.RectangleGeometry;
using PathGeometry = System.Windows.Media.PathGeometry;
using GeometryDrawing = System.Windows.Media.GeometryDrawing;
using DrawingImage = System.Windows.Media.DrawingImage;
using ImageSource = System.Windows.Media.ImageSource;

namespace Munyu.Services
{
    public static class IconService
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        // Win32 API Imports
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public static ImageSource GetIconForItem(string type, string content)
        {
            string cacheKey = type.ToLowerInvariant();

            if (type == "url")
            {
                return _iconCache.GetOrAdd("url", _ => CreateGlobeIcon());
            }

            if (type == "text")
            {
                return _iconCache.GetOrAdd("text", _ => CreateTextIcon());
            }

            if (type == "binder")
            {
                return _iconCache.GetOrAdd("binder", _ => CreateBinderIcon());
            }

            if (type == "file")
            {
                bool isDirectory = false;
                string ext = string.Empty;

                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        if (Directory.Exists(content))
                        {
                            isDirectory = true;
                            cacheKey = "folder_icon";
                        }
                        else
                        {
                            ext = Path.GetExtension(content).ToLowerInvariant();
                            if (string.IsNullOrEmpty(ext))
                            {
                                ext = ".generic";
                            }
                            cacheKey = "file_ext_" + ext;
                        }
                    }
                    catch
                    {
                        cacheKey = "file_ext_.generic";
                    }
                }
                else
                {
                    cacheKey = "file_ext_.generic";
                }

                return _iconCache.GetOrAdd(cacheKey, _ => ExtractShellIcon(content, isDirectory, ext));
            }

            return _iconCache.GetOrAdd("generic", _ => CreateTextIcon());
        }

        private static ImageSource ExtractShellIcon(string path, bool isDirectory, string extension)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;

            // If path exists on disk, use direct file info; otherwise use extension attribute lookup
            if (!string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path)))
            {
                SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
            }
            else
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                uint attr = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                string lookupPath = isDirectory ? "folder" : (string.IsNullOrEmpty(extension) ? "file.tmp" : $"dummy{extension}");
                SHGetFileInfo(lookupPath, attr, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
            }

            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    // Freeze for performance & thread safety
                    bs.Freeze();
                    return bs;
                }
                finally
                {
                    // CRITICAL: Prevent memory leak by calling DestroyIcon
                    DestroyIcon(shfi.hIcon);
                }
            }

            // Fallback if Shell icon retrieval fails
            return CreateTextIcon();
        }

        #region Custom Vector Icons
        private static ImageSource CreateGlobeIcon()
        {
            var group = new GeometryGroup();
            group.Children.Add(new EllipseGeometry(new Point(16, 16), 14, 14));
            group.Children.Add(new LineGeometry(new Point(2, 16), new Point(30, 16)));
            group.Children.Add(new EllipseGeometry(new Point(16, 16), 7, 14));

            var geometryDrawing = new GeometryDrawing
            {
                Geometry = group,
                Pen = new Pen(new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)), 2)
            };

            var drawingImage = new DrawingImage(geometryDrawing);
            drawingImage.Freeze();
            return drawingImage;
        }

        private static ImageSource CreateTextIcon()
        {
            var group = new GeometryGroup();
            var rect = new RectangleGeometry(new Rect(4, 3, 24, 26), 3, 3);
            group.Children.Add(rect);
            group.Children.Add(new LineGeometry(new Point(9, 10), new Point(23, 10)));
            group.Children.Add(new LineGeometry(new Point(9, 16), new Point(23, 16)));
            group.Children.Add(new LineGeometry(new Point(9, 22), new Point(18, 22)));

            var geometryDrawing = new GeometryDrawing
            {
                Geometry = group,
                Pen = new Pen(new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)), 2)
            };

            var drawingImage = new DrawingImage(geometryDrawing);
            drawingImage.Freeze();
            return drawingImage;
        }

        private static ImageSource CreateBinderIcon()
        {
            var group = new GeometryGroup();
            var pathGeom = PathGeometry.Parse("M 4 8 C 4 6, 6 4, 8 4 L 14 4 L 17 7 L 26 7 C 28 7, 30 9, 30 11 L 30 25 C 30 27, 28 29, 26 29 L 8 29 C 6 29, 4 27, 4 25 Z");
            group.Children.Add(pathGeom);

            group.Children.Add(new LineGeometry(new Point(10, 15), new Point(24, 15)));
            group.Children.Add(new LineGeometry(new Point(10, 20), new Point(24, 20)));

            var geometryDrawing = new GeometryDrawing
            {
                Geometry = group,
                Brush = new SolidColorBrush(Color.FromArgb(0x30, 0xA8, 0x55, 0xF7)),
                Pen = new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7)), 2)
            };

            var drawingImage = new DrawingImage(geometryDrawing);
            drawingImage.Freeze();
            return drawingImage;
        }
        #endregion
    }
}
