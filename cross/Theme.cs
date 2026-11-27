using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AgyPet
{
    // Website OKLCH hue-250 dark design tokens + zero-dependency PNG/WindowIcon generator for macOS & Linux.
    static class PetTheme
    {
        public static readonly FontFamily UiFont = new FontFamily("Plus Jakarta Sans, Inter, Segoe UI Variable, Noto Sans, sans-serif");

        public static readonly IBrush BgCanvas = FrozenRgb(0x12, 0x14, 0x1A);
        public static readonly IBrush BgRaised = FrozenRgb(0x1B, 0x1E, 0x26);
        public static readonly IBrush BgElevated = FrozenRgb(0x25, 0x29, 0x34);
        public static readonly IBrush BgPopover = FrozenArgb(0xF5, 0x24, 0x29, 0x34);
        public static readonly IBrush BgPillHover = FrozenArgb(0x14, 0xF4, 0xF6, 0xFA);

        public static readonly IBrush BorderSubtle = FrozenArgb(0x1C, 0xF5, 0xF7, 0xFC);
        public static readonly IBrush BorderStrong = FrozenArgb(0x30, 0xF5, 0xF7, 0xFC);

        public static readonly IBrush InkPrimary = FrozenRgb(0xF1, 0xF3, 0xF7);
        public static readonly IBrush InkSecondary = FrozenRgb(0xAD, 0xB4, 0xC2);
        public static readonly IBrush InkMuted = FrozenRgb(0x8C, 0x94, 0xA4);

        public static readonly IBrush AccentBlue = FrozenRgb(0x2E, 0x69, 0xE8);
        public static readonly IBrush AccentBlueHi = FrozenRgb(0x42, 0x7A, 0xF2);
        public static readonly IBrush AccentEmerald = FrozenRgb(0x47, 0xB8, 0x66);
        public static readonly IBrush AccentAmber = FrozenRgb(0xF5, 0x9E, 0x0B);

        public static IBrush FrozenRgb(byte r, byte g, byte b)
        {
            return new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
        }

        public static IBrush FrozenArgb(byte a, byte r, byte g, byte b)
        {
            return new ImmutableSolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        public static IBrush DotBrushForMood(MascotMood mood, bool darkSkin)
        {
            if (darkSkin)
            {
                if (mood == MascotMood.Idle || mood == MascotMood.Sleeping) return FrozenRgb(0x71, 0x71, 0x7A);
                return FrozenRgb(0xF1, 0xF3, 0xF7);
            }
            switch (mood)
            {
                case MascotMood.Done: return AccentEmerald;
                case MascotMood.Error: return FrozenRgb(0xEF, 0x44, 0x44);
                case MascotMood.Delegating: return FrozenRgb(0x8B, 0x5C, 0xF6);
                case MascotMood.Alert: return AccentAmber;
                case MascotMood.Question: return FrozenRgb(0x38, 0xBD, 0xF8);
                case MascotMood.Listening:
                case MascotMood.Speaking: return FrozenRgb(0x22, 0xD3, 0xEE);
                case MascotMood.Sleeping:
                case MascotMood.Idle: return InkMuted;
                case MascotMood.Background: return AccentEmerald;
                case MascotMood.Multitasking: return AccentBlueHi;
                default: return AccentBlue;
            }
        }

        // Generates an in-memory 32x32 PNG of the official colorful Antigravity mascot icon
        // without relying on System.Drawing / libgdiplus on Linux or macOS.
        public static WindowIcon CreateMascotWindowIcon()
        {
            byte[] png = RenderMascotPng(32, false);
            return new WindowIcon(new MemoryStream(png));
        }

        // Generates a monochrome template PNG (36x36 @2x) of the plain Antigravity arch silhouette
        // for the macOS menu bar NSStatusItem.
        public static WindowIcon CreateMacMenuBarIcon()
        {
            byte[] png = RenderMacMenuBarPng(36);
            return new WindowIcon(new MemoryStream(png));
        }

        public static byte[] RenderMacMenuBarPng(int size)
        {
            byte[] rgba = new byte[size * size * 4];
            var mask = MascotCore.AgyArchMask;
            int rows = mask.Length;
            int cols = mask[0].Length;
            double padX = size * 0.08;
            double padY = size * 0.12;
            double cellW = (size - padX * 2.0) / cols;
            double cellH = (size - padY * 2.0) / rows;

            for (int r = 0; r < rows; r++)
            {
                string row = mask[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != '#') continue;
                    int x0 = Math.Max(0, (int)Math.Floor(padX + c * cellW));
                    int x1 = Math.Min(size - 1, (int)Math.Ceiling(padX + (c + 1) * cellW) - 1);
                    int y0 = Math.Max(0, (int)Math.Floor(padY + r * cellH));
                    int y1 = Math.Min(size - 1, (int)Math.Ceiling(padY + (r + 1) * cellH) - 1);
                    for (int py = y0; py <= y1; py++)
                    {
                        for (int px = x0; px <= x1; px++)
                        {
                            int idx = (py * size + px) * 4;
                            rgba[idx] = 0;
                            rgba[idx + 1] = 0;
                            rgba[idx + 2] = 0;
                            rgba[idx + 3] = 255;
                        }
                    }
                }
            }

            return EncodeUncompressedPng(size, size, rgba);
        }

        public static byte[] RenderMascotPng(int size, bool darkSkin)
        {
            byte[] rgba = new byte[size * size * 4];
            var mask = MascotCore.AgyArchMask;
            double cellW = size / 20.0;
            double cellH = (size - 4.0) / 18.0;
            double yOff = 2.0;

            for (int r = 0; r < mask.Length; r++)
            {
                string row = mask[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != '#') continue;
                    bool hasTop = r > 0 && mask[r - 1][c] == '#';
                    bool hasBottom = r + 1 < mask.Length && mask[r + 1][c] == '#';
                    bool hasLeft = c > 0 && row[c - 1] == '#';
                    bool hasRight = c + 1 < row.Length && row[c + 1] == '#';
                    RgbColor rgb = darkSkin
                        ? MascotCore.DarkArchPixel(c / 19.0, r / 17.0, false, !hasTop, !hasLeft || !hasRight, !hasBottom)
                        : MascotCore.OfficialArchPixel(c / 19.0, r / 17.0, false, !hasTop, !hasBottom && r >= 8 && r <= 12 && c >= 6 && c <= 13);

                    int x0 = Math.Max(0, (int)Math.Floor(c * cellW));
                    int x1 = Math.Min(size - 1, (int)Math.Ceiling((c + 1) * cellW) - 1);
                    int y0 = Math.Max(0, (int)Math.Floor(yOff + r * cellH));
                    int y1 = Math.Min(size - 1, (int)Math.Ceiling(yOff + (r + 1) * cellH) - 1);
                    for (int py = y0; py <= y1; py++)
                    {
                        for (int px = x0; px <= x1; px++)
                        {
                            int idx = (py * size + px) * 4;
                            rgba[idx] = rgb.R;
                            rgba[idx + 1] = rgb.G;
                            rgba[idx + 2] = rgb.B;
                            rgba[idx + 3] = 255;
                        }
                    }
                }
            }

            // Cute dark eyes so the tray/app icon reads crisply even at 16px
            FillRect(rgba, size, (int)(size * 0.34), (int)(size * 0.34), Math.Max(2, size / 10), Math.Max(2, size / 10), 11, 18, 32);
            FillRect(rgba, size, (int)(size * 0.56), (int)(size * 0.34), Math.Max(2, size / 10), Math.Max(2, size / 10), 11, 18, 32);

            return EncodeUncompressedPng(size, size, rgba);
        }

        static void FillRect(byte[] rgba, int size, int rx, int ry, int rw, int rh, byte r, byte g, byte b)
        {
            for (int y = ry; y < Math.Min(size, ry + rh); y++)
            {
                for (int x = rx; x < Math.Min(size, rx + rw); x++)
                {
                    int idx = (y * size + x) * 4;
                    if (rgba[idx + 3] == 0) continue;
                    rgba[idx] = r;
                    rgba[idx + 1] = g;
                    rgba[idx + 2] = b;
                }
            }
        }

        static byte[] EncodeUncompressedPng(int w, int h, byte[] rgba)
        {
            int rawStride = 1 + w * 4;
            byte[] raw = new byte[rawStride * h];
            for (int y = 0; y < h; y++)
            {
                raw[y * rawStride] = 0; // Filter None
                Buffer.BlockCopy(rgba, y * w * 4, raw, y * rawStride + 1, w * 4);
            }

            // Uncompressed DEFLATE stream wrapped in zlib header + adler32
            int zlibLen = 2 + 5 + raw.Length + 4;
            byte[] idat = new byte[zlibLen];
            idat[0] = 0x78;
            idat[1] = 0x01;
            idat[2] = 0x01; // BFINAL=1, BTYPE=00 (no compression)
            int len = raw.Length;
            idat[3] = (byte)(len & 0xFF);
            idat[4] = (byte)((len >> 8) & 0xFF);
            idat[5] = (byte)(~len & 0xFF);
            idat[6] = (byte)((~len >> 8) & 0xFF);
            Buffer.BlockCopy(raw, 0, idat, 7, raw.Length);
            uint adler = Adler32(raw);
            WriteBe32(idat, 7 + raw.Length, adler);

            using (var ms = new MemoryStream())
            {
                ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                byte[] ihdr = new byte[13];
                WriteBe32(ihdr, 0, (uint)w);
                WriteBe32(ihdr, 4, (uint)h);
                ihdr[8] = 8; // bit depth
                ihdr[9] = 6; // RGBA
                WriteChunk(ms, "IHDR", ihdr);
                WriteChunk(ms, "IDAT", idat);
                WriteChunk(ms, "IEND", new byte[0]);
                return ms.ToArray();
            }
        }

        static void WriteChunk(MemoryStream ms, string type, byte[] data)
        {
            byte[] len = new byte[4];
            WriteBe32(len, 0, (uint)data.Length);
            ms.Write(len, 0, 4);
            byte[] tag = System.Text.Encoding.ASCII.GetBytes(type);
            ms.Write(tag, 0, 4);
            ms.Write(data, 0, data.Length);
            uint crc = Crc32(tag, data);
            WriteBe32(len, 0, crc);
            ms.Write(len, 0, 4);
        }

        static void WriteBe32(byte[] buf, int off, uint val)
        {
            buf[off] = (byte)((val >> 24) & 0xFF);
            buf[off + 1] = (byte)((val >> 16) & 0xFF);
            buf[off + 2] = (byte)((val >> 8) & 0xFF);
            buf[off + 3] = (byte)(val & 0xFF);
        }

        static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        static uint Crc32(byte[] tag, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < tag.Length; i++) crc = CrcByte(crc, tag[i]);
            for (int i = 0; i < data.Length; i++) crc = CrcByte(crc, data[i]);
            return crc ^ 0xFFFFFFFF;
        }

        static uint CrcByte(uint crc, byte b)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : (crc >> 1);
            return crc;
        }
    }
}
