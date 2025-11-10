/*
 * Switcheroo - The incremental-search task switcher for Windows.
 * https://github.com/coezbek/switcheroo
 * Copyright 2009, 2010 James Sulak
 * Copyright 2014 Regin Larsen
 * 
 * Switcheroo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Switcheroo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Switcheroo.Core
{
    public enum WindowIconSize
    {
        Small,
        Large
    }

    public class WindowIconFinder
    {
        public Icon Find(AppWindow window, WindowIconSize size)
        {
            Icon icon = null;
            try
            {
                if (window.IsUwpApp())
                {
                    icon = GetUwpIcon2(window);
                }

                if (icon == null)
                {
                    // http://msdn.microsoft.com/en-us/library/windows/desktop/ms632625(v=vs.85).aspx
                    IntPtr response;
                    var outvalue = WinApi.SendMessageTimeout(window.HWnd, 0x007F,
                        size == WindowIconSize.Small ? new IntPtr(2) : new IntPtr(1),
                        IntPtr.Zero, WinApi.SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, 100, out response);

                    if (outvalue == IntPtr.Zero || response == IntPtr.Zero)
                    {
                        response = WinApi.GetClassLongPtr(window.HWnd,
                            size == WindowIconSize.Small
                                ? WinApi.ClassLongFlags.GCLP_HICONSM
                                : WinApi.ClassLongFlags.GCLP_HICON);
                    }

                    if (response != IntPtr.Zero)
                    {
                        icon = Icon.FromHandle(response);
                    }
                }

                if (icon == null)
                {
                    var executablePath = window.ExecutablePath;
                    icon = Icon.ExtractAssociatedIcon(executablePath);
                }
            }
            catch (Win32Exception ex)
            {
                // Could not extract icon
                Console.WriteLine($"[ERROR] Win32Exception in IconFinder: {ex.Message}");
            }
            return icon;
        }

        public static Icon GetUwpIcon2(AppWindow window)
        {
            try
            {
                var uwpProcess = window.UwpUnderlyingProcess;
                if (uwpProcess == null)
                {
                    return null;
                }

                var hprocess = WinApi.OpenProcess(WinApi.ProcessAccess.QueryLimitedInformation, false, uwpProcess.Id);
                if (hprocess == IntPtr.Zero)
                {
                    Console.WriteLine("[ERROR] Could not open UWP process in GetUwpIcon2.");
                    return null;
                }

                try
                {
                    uint bufferLength = 0;
                    WinApi.GetPackageFullName(hprocess, ref bufferLength, null);
                    if (bufferLength == 0)
                    {
                        return null;
                    }

                    var sb = new StringBuilder((int)bufferLength);
                    if (WinApi.GetPackageFullName(hprocess, ref bufferLength, sb) != 0)
                    {
                        Console.WriteLine("[ERROR] GetPackageFullName failed in GetUwpIcon2.");
                        return null;
                    }

                    var packageFullName = sb.ToString();

                    IntPtr packageInfoReference;
                    if (WinApi.OpenPackageInfoByFullName(packageFullName, 0, out packageInfoReference) != 0)
                    {
                        Console.WriteLine("[ERROR] OpenPackageInfoByFullName failed in GetUwpIcon2.");
                        return null;
                    }

                    try
                    {
                        bufferLength = 0;
                        uint count;
                        WinApi.GetPackageInfo(packageInfoReference, 0x00000010, ref bufferLength, IntPtr.Zero, out count);
                        if (bufferLength == 0)
                        {
                            Console.WriteLine("[ERROR] Package info buffer length is zero in GetUwpIcon2.");
                            return null;
                        }

                        var buffer = Marshal.AllocHGlobal((int)bufferLength);
                        try
                        {
                            if (WinApi.GetPackageInfo(packageInfoReference, 0x00000010, ref bufferLength, buffer, out count) != 0)
                            {
                                Console.WriteLine("[ERROR] GetPackageInfo failed in GetUwpIcon2.");
                                return null;
                            }

                            var packageInfo = (WinApi.PACKAGE_INFO)Marshal.PtrToStructure(buffer, typeof(WinApi.PACKAGE_INFO));
                            var path = Marshal.PtrToStringUni(packageInfo.path);

                            var manifestPath = Path.Combine(path, "AppxManifest.xml");

                            if (!File.Exists(manifestPath))
                            {
                                Console.WriteLine($"[ERROR] Manifest file not found at path: {manifestPath}");
                                return null;
                            }

                            var manifest = new XmlDocument();
                            manifest.Load(manifestPath);

                            var namespaces = new XmlNamespaceManager(manifest.NameTable);
                            namespaces.AddNamespace("m", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
                            namespaces.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

                            var node = manifest.SelectSingleNode("//m:Package/m:Applications/m:Application/uap:VisualElements/@Square44x44Logo", namespaces) ??
                                       manifest.SelectSingleNode("//m:Package/m:Properties/m:Logo", namespaces);

                            if (node == null)
                            {
                                Console.WriteLine("[ERROR] Logo node not found in manifest.");
                                return null;
                            }

                            var logoRelativePath = (node is XmlAttribute) ? node.Value : node.InnerText;
                            var logoPath = Path.Combine(path, logoRelativePath.Replace('/', '\\'));
                            var logoDirectory = Path.GetDirectoryName(logoPath);
                            var logoFileName = Path.GetFileNameWithoutExtension(logoPath);
                            var logoFileExt = Path.GetExtension(logoPath);
                            var partialLogoName = Path.Combine(logoDirectory, logoFileName);

                            var exactMatchLogoPath = FindBestMatchLogo(partialLogoName, logoFileExt);
                            if (File.Exists(exactMatchLogoPath))
                            {
                                IntPtr hicon = IntPtr.Zero;
                                try
                                {
                                    using (var originalBitmap = new Bitmap(exactMatchLogoPath))
                                    {
                                        using (var bitmap32 = new Bitmap(originalBitmap.Width, originalBitmap.Height, PixelFormat.Format32bppArgb))
                                        {
                                            using (var g = Graphics.FromImage(bitmap32))
                                            {
                                                g.DrawImage(originalBitmap, new Rectangle(0, 0, bitmap32.Width, bitmap32.Height));
                                            }
                                            hicon = bitmap32.GetHicon();
                                        }
                                    }

                                    if (hicon != IntPtr.Zero)
                                    {
                                        using (var tempIcon = Icon.FromHandle(hicon))
                                        {
                                            return (Icon)tempIcon.Clone();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ERROR] Exception creating icon from '{exactMatchLogoPath}': {ex.Message}");
                                    return null;
                                }
                                finally
                                {
                                    if (hicon != IntPtr.Zero)
                                    {
                                        WinApi.DestroyIcon(hicon);
                                    }
                                }
                            }
                            return null;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                    finally
                    {
                        WinApi.ClosePackageInfo(packageInfoReference);
                    }
                }
                finally
                {
                    WinApi.CloseHandle(hprocess);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in GetUwpIcon2: {ex.Message}");
                return null;
            }
        }

        private static string FindBestMatchLogo(string partialLogoName, string logoFileExt)
        {
            string[] preferredSuffixes =
            {
                ".targetsize-48_altform-unplated",
                ".targetsize-44_altform-unplated",
                ".targetsize-32_altform-unplated",
                ".targetsize-24_altform-unplated",
                ".targetsize-16_altform-unplated",
                ".targetsize-48",
                ".targetsize-44",
                ".targetsize-32",
                ".targetsize-24",
                ".targetsize-16"
            };

            foreach (var suffix in preferredSuffixes)
            {
                var path = partialLogoName + suffix + logoFileExt;
                if (File.Exists(path))
                {
                    return path;
                }
            }

            var directory = Path.GetDirectoryName(partialLogoName);
            var fileName = Path.GetFileName(partialLogoName);
            var matchingFiles = Directory.GetFiles(directory, fileName + ".scale-*" + logoFileExt);

            if (matchingFiles.Any())
            {
                var scale100 = matchingFiles.FirstOrDefault(f => f.Contains(".scale-100"));
                if (scale100 != null) return scale100;
                return matchingFiles[0];
            }

            var basePath = partialLogoName + logoFileExt;
            if (File.Exists(basePath))
            {
                return basePath;
            }

            return basePath;
        }

        private static Icon GetUwpIcon(AppWindow window)
        {
            try
            {
                var hprocess = WinApi.OpenProcess(WinApi.ProcessAccess.QueryLimitedInformation, false, window.Process.Id);
                if (hprocess == IntPtr.Zero) return null;

                try
                {
                    uint bufferLength = 0;
                    WinApi.GetPackageFullName(hprocess, ref bufferLength, null);
                    if (bufferLength == 0) return null;

                    var sb = new StringBuilder((int)bufferLength);
                    WinApi.GetPackageFullName(hprocess, ref bufferLength, sb);

                    var packageFullName = sb.ToString();

                    IntPtr packageInfoReference;
                    WinApi.OpenPackageInfoByFullName(packageFullName, 0, out packageInfoReference);

                    bufferLength = 0;
                    WinApi.GetPackageInfo(packageInfoReference, 0x00000010, ref bufferLength, IntPtr.Zero, out _);

                    var buffer = Marshal.AllocHGlobal((int)bufferLength);
                    WinApi.GetPackageInfo(packageInfoReference, 0x00000010, ref bufferLength, buffer, out _);

                    var packageInfo = (WinApi.PACKAGE_INFO)Marshal.PtrToStructure(buffer, typeof(WinApi.PACKAGE_INFO));
                    var path = Marshal.PtrToStringUni(packageInfo.path);

                    WinApi.ClosePackageInfo(packageInfoReference);
                    Marshal.FreeHGlobal(buffer);

                    var manifestPath = Path.Combine(path, "AppxManifest.xml");

                    if (!File.Exists(manifestPath)) return null;

                    var manifest = new XmlDocument();
                    manifest.Load(manifestPath);

                    var node = manifest.SelectSingleNode("//*[local-name()='Logo']");
                    if (node == null) return null;
                    
                    var logoPath = Path.Combine(path, node.InnerText);

                    return new Icon(logoPath);
                }
                finally
                {
                    WinApi.CloseHandle(hprocess);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}