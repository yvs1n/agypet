#!/usr/bin/env bash
set -euo pipefail

# Packages self-contained osx-arm64 & osx-x64 single-file binaries into one
# Universal macOS disk image: bin/AgyPet-macOS.dmg (drag-and-play / double-click).
VERSION="${1:-0.4.0}"
ARM64_BIN="${2:?arm64 binary path required}"
X64_BIN="${3:?x64 binary path required}"
OUT_DMG="${4:-bin/AgyPet-macOS.dmg}"

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

APP="$STAGE/AgyPet.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$STAGE/icon.iconset"

cp "$ARM64_BIN" "$APP/Contents/MacOS/AgyPet-arm64"
cp "$X64_BIN" "$APP/Contents/MacOS/AgyPet-x64"
chmod +x "$APP/Contents/MacOS/AgyPet-arm64" "$APP/Contents/MacOS/AgyPet-x64"

# Compile tiny Universal Mach-O (arm64 + x86_64) dispatcher executable
cat > "$STAGE/main.c" << 'EOF'
#include <libgen.h>
#include <limits.h>
#include <mach-o/dyld.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

int main(int argc, char *argv[]) {
    char raw[PATH_MAX];
    uint32_t sz = sizeof(raw);
    if (_NSGetExecutablePath(raw, &sz) != 0) return 1;
    char resolved[PATH_MAX];
    char *dir = realpath(raw, resolved) ? dirname(resolved) : dirname(raw);
    char target[PATH_MAX];
#if defined(__aarch64__)
    snprintf(target, sizeof(target), "%s/AgyPet-arm64", dir);
#else
    snprintf(target, sizeof(target), "%s/AgyPet-x64", dir);
#endif
    argv[0] = target;
    execv(target, argv);
    perror("AgyPet launch failed");
    return 1;
}
EOF
clang -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -O2 "$STAGE/main.c" -o "$APP/Contents/MacOS/AgyPet"
chmod +x "$APP/Contents/MacOS/AgyPet"

# Render multi-size Antigravity icon PNGs & assemble AgyPet.icns
python3 - "$STAGE/icon.iconset" << 'PYEOF'
import math, os, struct, sys, zlib

out_dir = sys.argv[1]
MASK = [
    "......####......",
    ".....######.....",
    "....########....",
    "....########....",
    "...##########...",
    "...##########...",
    "..############..",
    "..############..",
    "..###......###..",
    "..##........##..",
    ".###........###.",
    ".##..........##.",
    "##............##",
    "#..............#",
]

def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))

def color_at(r, c):
    if r <= 2:
        col = (248, 198, 24) if c < 7 else ((250, 116, 32) if c < 10 else (239, 68, 58))
    elif r <= 6 and c < 7:
        col = (78, 196, 70)
    elif r <= 6 and c >= 9:
        col = (123, 97, 236)
    elif r <= 8 and c < 6:
        col = (31, 168, 216)
    else:
        col = (30, 120, 255)
    return col

def write_png(path, size):
    buf = bytearray(size * size * 4)
    # Dark rounded tile backing
    pad = size * 0.08
    rad = size * 0.22
    for y in range(size):
        for x in range(size):
            dx = max(0.0, max(pad + rad - x, x - (size - pad - rad)))
            dy = max(0.0, max(pad + rad - y, y - (size - pad - rad)))
            if math.hypot(dx, dy) <= rad:
                i = (y * size + x) * 4
                buf[i:i+4] = bytes([18, 20, 26, 255])

    px = max(1, int(size * 0.68 / 16))
    ox = (size - 16 * px) // 2
    oy = (size - 14 * px) // 2
    for r, row in enumerate(MASK):
        for c, ch in enumerate(row):
            if ch != '#':
                continue
            rgb = color_at(r, c)
            for dy in range(px):
                for dx in range(px):
                    x, y = ox + c * px + dx, oy + r * px + dy
                    if 0 <= x < size and 0 <= y < size:
                        i = (y * size + x) * 4
                        buf[i:i+4] = bytes([rgb[0], rgb[1], rgb[2], 255])

    raw = bytearray()
    stride = size * 4
    for y in range(size):
        raw.append(0)
        raw.extend(buf[y*stride:(y+1)*stride])

    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF)

    ihdr = struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')
    with open(path, 'wb') as f:
        f.write(png)

for s, name in [(16, 'icon_16x16.png'), (32, 'icon_16x16@2x.png'), (32, 'icon_32x32.png'),
                (64, 'icon_32x32@2x.png'), (128, 'icon_128x128.png'), (256, 'icon_128x128@2x.png'),
                (256, 'icon_256x256.png'), (512, 'icon_256x256@2x.png'), (512, 'icon_512x512.png')]:
    write_png(os.path.join(out_dir, name), s)
PYEOF
iconutil -c icns "$STAGE/icon.iconset" -o "$APP/Contents/Resources/AgyPet.icns"

cat > "$APP/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>AgyPet</string>
  <key>CFBundleExecutable</key><string>AgyPet</string>
  <key>CFBundleIconFile</key><string>AgyPet</string>
  <key>CFBundleIdentifier</key><string>me.yassinr.agypet</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>AgyPet</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
EOF

codesign --force --deep --sign - "$APP"

DMG_ROOT="$STAGE/dmg"
mkdir -p "$DMG_ROOT"
cp -R "$APP" "$DMG_ROOT/AgyPet.app"
ln -s /Applications "$DMG_ROOT/Applications"

mkdir -p "$(dirname "$OUT_DMG")"
rm -f "$OUT_DMG"
hdiutil create -volname "AgyPet" -srcfolder "$DMG_ROOT" -ov -format UDZO "$OUT_DMG"
echo "Created Universal macOS DMG: $OUT_DMG"
