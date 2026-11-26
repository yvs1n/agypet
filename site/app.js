(() => {
  'use strict';

  // ---- 1. Exact 20x18 Antigravity Pixel Mascot Engine (18 Reactions + Classic & Dark Remake) ----
  const ARCH_MASK = [
    "........####........",
    ".......######.......",
    "......########......",
    "......########......",
    ".....##########.....",
    ".....##########.....",
    ".....##########.....",
    "....############....",
    "....############....",
    "....############....",
    "....####....####....",
    "...####......####...",
    "...###........###...",
    "...###........###...",
    "..###..........###..",
    ".###............###.",
    ".##..............##.",
    "##................##",
  ];

  const COMPACT_MASK = [
    "....................",
    "....................",
    "....................",
    "....................",
    "......########......",
    ".....##########.....",
    "....############....",
    "....############....",
    "....############....",
    "....############....",
    "....############....",
    "....############....",
    ".....##########.....",
    "......########......",
    "....................",
    "....................",
    "....................",
    "....................",
  ];

  const MOODS = [
    { id: "Typing", sub: "Agent or you typing" },
    { id: "Thinking", sub: "Planning steps" },
    { id: "Working", sub: "Editing files" },
    { id: "Terminal", sub: "Running command" },
    { id: "Browsing", sub: "Live browser agent" },
    { id: "Delegating", sub: "Subagents & swarms" },
    { id: "Reading", sub: "Scanning code" },
    { id: "Searching", sub: "Looking up" },
    { id: "Listening", sub: "Voice & dictation" },
    { id: "Speaking", sub: "Voice reply" },
    { id: "Multitasking", sub: "2+ active sessions" },
    { id: "Background", sub: "Done, server active" },
    { id: "Alert", sub: "Needs approval" },
    { id: "Question", sub: "Asking a choice" },
    { id: "Done", sub: "Task complete" },
    { id: "Error", sub: "Step hit error" },
    { id: "Idle", sub: "At rest" },
    { id: "Sleeping", sub: "Quiet" },
  ];

  function gauss(u, v, mu, mv, su, sv) {
    const du = (u - mu) / su;
    const dv = (v - mv) / sv;
    return Math.exp(-0.5 * (du * du + dv * dv));
  }

  function lerpRgb(a, b, t) {
    const k = Math.max(0, Math.min(1, t));
    return [
      Math.round(a[0] + (b[0] - a[0]) * k),
      Math.round(a[1] + (b[1] - a[1]) * k),
      Math.round(a[2] + (b[2] - a[2]) * k),
    ];
  }

  function shiftLight(c, d) {
    if (d >= 0) {
      return [
        Math.min(255, Math.round(c[0] + (255 - c[0]) * d)),
        Math.min(255, Math.round(c[1] + (255 - c[1]) * d)),
        Math.min(255, Math.round(c[2] + (255 - c[2]) * d)),
      ];
    }
    const k = 1 + d;
    return [Math.max(0, Math.round(c[0] * k)), Math.max(0, Math.round(c[1] * k)), Math.max(0, Math.round(c[2] * k))];
  }

  function rgbCss(c) {
    return `rgb(${c[0]} ${c[1]} ${c[2]})`;
  }

  function officialPixelColor(u, v, compact, topEdge, innerArch) {
    if (compact) v = Math.max(0.04, Math.min(0.96, (v - 0.16) / 0.68));
    let c = lerpRgb([30, 120, 255], [44, 134, 255], Math.min(1, Math.max(0, (v - 0.35) / 0.65)));
    c = lerpRgb(c, [78, 196, 70], Math.min(1, gauss(u, v, 0.23, 0.34, 0.18, 0.22) * 1.15));
    c = lerpRgb(c, [31, 168, 216], Math.min(0.85, gauss(u, v, 0.20, 0.54, 0.14, 0.16) * 0.85));
    c = lerpRgb(c, [248, 198, 24], Math.min(1, gauss(u, v, 0.40, 0.06, 0.17, 0.17) * 1.25));
    c = lerpRgb(c, [250, 116, 32], Math.min(1, gauss(u, v, 0.54, 0.08, 0.16, 0.16) * 1.2));
    c = lerpRgb(c, [239, 68, 58], Math.min(1, gauss(u, v, 0.65, 0.21, 0.16, 0.19) * 1.18));
    c = lerpRgb(c, [123, 97, 236], Math.min(0.85, gauss(u, v, 0.72, 0.44, 0.14, 0.16) * 0.9));
    if (topEdge && u < 0.6) c = shiftLight(c, 0.08);
    else if (innerArch) c = shiftLight(c, -0.14);
    return rgbCss(c);
  }

  function darkPixelColor(u, v, compact, topEdge, sideEdge, bottomEdge) {
    if (compact) v = Math.max(0.05, Math.min(0.95, (v - 0.16) / 0.68));
    let c = v < 0.45 ? lerpRgb([68, 68, 74], [44, 44, 49], v / 0.45) : lerpRgb([44, 44, 49], [24, 24, 27], (v - 0.45) / 0.55);
    c = shiftLight(c, gauss(u, v, 0.52, 0.16, 0.22, 0.20) * 0.14);
    if (topEdge) c = lerpRgb(c, [110, 110, 120], 0.58);
    else if (sideEdge) c = lerpRgb(c, [110, 110, 120], 0.34);
    else if (bottomEdge && v < 0.72) c = shiftLight(c, -0.18);
    return rgbCss(c);
  }

  function buildMaskCache(skin, compact) {
    const mask = compact ? COMPACT_MASK : ARCH_MASK;
    const cells = [];
    for (let r = 0; r < mask.length; r++) {
      const row = mask[r];
      for (let c = 0; c < row.length; c++) {
        if (row[c] !== '#') continue;
        const hasLeft = c > 0 && row[c - 1] === '#';
        const hasRight = c + 1 < row.length && row[c + 1] === '#';
        const hasTop = r > 0 && mask[r - 1][c] === '#';
        const hasBottom = r + 1 < mask.length && mask[r + 1][c] === '#';
        const u = c / 19;
        const v = r / 17;
        const innerArch = !hasBottom && r >= 8 && r <= 12 && c >= 6 && c <= 13;
        const fill = skin === 'dark'
          ? darkPixelColor(u, v, compact, !hasTop, !hasLeft || !hasRight, !hasBottom)
          : officialPixelColor(u, v, compact, !hasTop, innerArch);
        cells.push({ x: 0.5 + c * 0.5, y: r * 0.5, w: 0.5, h: 0.5, fill });
      }
    }
    return cells;
  }

  const CACHE = {
    classic: { normal: buildMaskCache('classic', false), compact: buildMaskCache('classic', true) },
    dark: { normal: buildMaskCache('dark', false), compact: buildMaskCache('dark', true) },
  };

  function setupHiDpiCanvas(canvas) {
    const logicalW = parseInt(canvas.getAttribute('width'), 10) || canvas.width;
    const logicalH = parseInt(canvas.getAttribute('height'), 10) || canvas.height;
    const dpr = Math.min(3, Math.max(1, window.devicePixelRatio || 1));
    canvas.style.width = `${logicalW}px`;
    canvas.style.height = `${logicalH}px`;
    canvas.width = Math.round(logicalW * dpr);
    canvas.height = Math.round(logicalH * dpr);
    const ctx = canvas.getContext('2d');
    ctx.imageSmoothingEnabled = false;
    return { ctx, w: logicalW, h: logicalH, dpr };
  }

  function drawMascot(ctx, w, h, px, skin, mood, tick, state = 0, dpr = 1, orbCount = 3, lookX = 0, lookY = 0) {
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, Math.round(w * dpr), Math.round(h * dpr));
    const isDark = skin === 'dark';
    const compact = mood === 'Sleeping';
    const hurt = typeof state === 'object' ? (state.hurt > 0) : state > 0;
    const hurtT = typeof state === 'object' ? (state.hurt || 0) : (state || 0);
    const annoyed = typeof state === 'object' && (state.annoyed > 0);
    const dancing = typeof state === 'object' && (state.dance > 0) && !annoyed && !hurt;

    const shiftLeftProp = (mood === 'Working' || mood === 'Terminal') ? 1.4 * px : 0;
    const ox = (w - 11 * px) * 0.5 + shiftLeftProp;
    const oy = (h - 9 * px) * 0.5 + 1.3 * px;

    const danceBob = dancing ? -Math.abs(Math.sin(tick * 0.34)) * 0.45 * px : 0;
    const doneHop = mood === 'Done' && !annoyed ? -Math.round(Math.abs(Math.sin(tick * 0.45)) * 1.1) * px : 0;
    const baseBob = dancing ? danceBob : doneHop;
    const wiggles = (mood === 'Alert' || mood === 'Question' || mood === 'Error') && !annoyed;
    const bobY = hurt && hurtT >= 4 ? baseBob + px * 0.45 : baseBob;
    const danceSway = dancing ? Math.sin(tick * 0.34) * 0.5 * px : 0;
    const wigX = hurt && hurtT >= 4 ? ((hurtT % 2 === 0 ? -0.28 : 0.28) * px)
      : (mood === 'Error' && !annoyed && tick % 6 < 2) ? ((tick % 2 === 0 ? -0.4 : 0.4) * px)
      : (wiggles && tick % 12 < 4 ? (tick % 2 === 0 ? -px * 0.4 : px * 0.4) : danceSway);

    // Edge-to-edge physical pixel rounding guarantees zero transparent 1px seams through any mascot!
    const dot = (fill, x, y, dw = 1, dh = 1, inSprite = true) => {
      ctx.fillStyle = fill;
      const bx = ox + (inSprite ? wigX : 0) + x * px;
      const by = oy + (inSprite ? bobY : 0) + y * px;
      const x1 = Math.round(bx * dpr);
      const y1 = Math.round(by * dpr);
      const x2 = Math.round((bx + dw * px) * dpr);
      const y2 = Math.round((by + dh * px) * dpr);
      ctx.fillRect(x1, y1, Math.max(1, x2 - x1), Math.max(1, y2 - y1));
    };

    const shape = (fill, x, y, rows, inSprite = false) => {
      for (let r = 0; r < rows.length; r++) {
        for (let c = 0; c < rows[r].length; c++) {
          if (rows[r][c] === '#') dot(fill, x + c, y + r, 1, 1, inSprite);
        }
      }
    };

    const eyeMain = isDark ? '#F8FAFC' : '#0B1220';
    const armLU = isDark ? '#52525B' : '#1FA8D8';
    const armLL = isDark ? '#3A3A40' : '#1E78FF';
    const armRU = isDark ? '#52525B' : '#7B61EC';
    const armRL = isDark ? '#3A3A40' : '#2C86FF';

    ctx.save();
    if (hurt && hurtT >= 4) {
      const cx = (ox + 5.5 * px) * dpr, cy = (oy + 7.6 * px) * dpr;
      ctx.translate(cx, cy);
      ctx.scale(1.05, 0.94);
      ctx.translate(-cx, -cy);
    }

    const cells = CACHE[skin][compact ? 'compact' : 'normal'];
    for (let i = 0; i < cells.length; i++) {
      const cell = cells[i];
      dot(cell.fill, cell.x, cell.y, cell.w, cell.h, true);
    }

    const closed = !hurt && !annoyed && (tick % 58 >= 56);
    const faceBlocked = hurt || annoyed;
    if (mood === 'Idle' || mood === 'Listening' || mood === 'Speaking' || mood === 'Searching' || mood === 'Browsing' || mood === 'Background') {
      dot(armLU, 1.5, 4.8, 0.5, 1.0); dot(armLL, 1.1, 5.4, 0.5, 0.5);
      dot(armRU, 9.0, 4.8, 0.5, 1.0); dot(armRL, 9.4, 5.4, 0.5, 0.5);
    }

    const drawEyes = (lx, ly, rx, ry, isClosed, shortH = false) => {
      if (isClosed) {
        dot(eyeMain, lx, ly + 0.4, 0.9, 0.35);
        dot(eyeMain, rx, ry + 0.4, 0.9, 0.35);
        return;
      }
      const eh = shortH ? 0.5 : 0.9;
      dot(eyeMain, lx, ly, 0.85, eh);
      dot(eyeMain, rx, ry, 0.85, eh);
      if (!isDark) {
        dot('#F8FAFC', lx + 0.4, ly, 0.4, 0.4);
        dot('#F8FAFC', rx + 0.4, ry, 0.4, 0.4);
      } else {
        dot('#0B1220', lx + 0.45, ly + 0.35, 0.35, 0.35);
        dot('#0B1220', rx + 0.45, ry + 0.35, 0.35, 0.35);
      }
    };

    const drawHappyEyes = () => {
      dot(eyeMain, 3.7, 3.4, 0.4, 0.4); dot(eyeMain, 4.1, 3.0, 0.5, 0.4); dot(eyeMain, 4.6, 3.4, 0.4, 0.4);
      dot(eyeMain, 6.0, 3.4, 0.4, 0.4); dot(eyeMain, 6.4, 3.0, 0.5, 0.4); dot(eyeMain, 6.9, 3.4, 0.4, 0.4);
    };

    switch (mood) {
      case 'Idle':
        if (!faceBlocked) drawEyes(3.9 + lookX * 0.48, 3.2 + lookY * 0.34, 6.3 + lookX * 0.48, 3.2 + lookY * 0.34, closed);
        if (dancing) {
          const nt = Math.floor(tick / 3) % 4;
          dot(isDark ? '#F4F4F5' : '#F8C618', -0.2, 0.6 - nt * 0.3, 0.48, 1.2, false);
          dot(isDark ? '#F4F4F5' : '#F8C618', -0.65, 1.5 - nt * 0.3, 0.65, 0.48, false);
          dot(isDark ? '#A1A1AA' : '#38BDF8', 10.2, 0.2 - ((nt + 2) % 4) * 0.3, 0.48, 1.2, false);
          dot(isDark ? '#A1A1AA' : '#38BDF8', 9.75, 1.1 - ((nt + 2) % 4) * 0.3, 0.65, 0.48, false);
        } else if (!annoyed && tick % 24 < 14) {
          dot(isDark ? '#F4F4F5' : '#FFF6D6', 8.9, 1.3, 0.5, 0.5, false);
          if (tick % 24 < 9) {
            const rCol = isDark ? '#A1A1AA' : '#E2E8F0';
            dot(rCol, 8.4, 1.3, 0.5, 0.5, false); dot(rCol, 9.4, 1.3, 0.5, 0.5, false);
            dot(rCol, 8.9, 0.8, 0.5, 0.5, false); dot(rCol, 8.9, 1.8, 0.5, 0.5, false);
          }
        }
        break;
      case 'Sleeping':
        if (!faceBlocked) { dot(eyeMain, 4.0, 4.3, 0.9, 0.35); dot(eyeMain, 6.2, 4.3, 0.9, 0.35); }
        if ((Math.floor(tick / 10) % 4) >= 1) shape(isDark ? '#71717A' : '#64748B', 8.6, -1.1, ["###", "..#", ".#.", "###"]);
        if ((Math.floor(tick / 10) % 4) >= 2) shape(isDark ? '#F4F4F5' : '#38BDF8', 11.4, -2.5, ["###", "..#", ".#.", "###"]);
        break;
      case 'Listening':
        if (!faceBlocked) drawEyes(3.9, 3.1, 6.3, 3.1, false);
        for (let i = 0; i <= Math.floor(tick / 3) % 3; i++) {
          const wh = 3.6 - i * 0.85, wt = 4.5 - wh / 2;
          const wc = i === (Math.floor(tick / 3) % 3) ? (isDark ? '#F4F4F5' : '#38BDF8') : (isDark ? '#A1A1AA' : '#1E78FF');
          dot(wc, -0.4 - i * 0.95, wt, 0.75, wh, false);
          dot(wc, 10.6 + i * 0.95, wt, 0.75, wh, false);
        }
        break;
      case 'Thinking': {
        if (!faceBlocked) {
          dot(eyeMain, 3.9, 2.75, 0.85, 0.55);
          dot(eyeMain, 6.3, 2.75, 0.85, 0.8);
          if (!isDark) { dot('#F8FAFC', 4.35, 2.75, 0.35, 0.35); dot('#F8FAFC', 6.75, 2.75, 0.35, 0.35); }
        }
        dot(armLU, 1.5, 4.8, 0.5, 1.0); dot(armLL, 1.1, 5.4, 0.5, 0.5);
        dot(armRU, 8.6, 4.6, 0.55, 0.85); dot(armRL, 7.6, 4.2, 1.05, 0.5);
        const rim = isDark ? '#A1A1AA' : '#1E78FF';
        const bg = isDark ? '#27272A' : '#0F172A';
        dot(rim, 8.4, 0.6, 0.5, 0.5, false);
        dot(rim, 9.1, -0.2, 0.75, 0.75, false);
        dot(rim, 8.7, -2.6, 4.8, 2.2, false);
        dot(bg, 9.1, -2.25, 4.0, 1.5, false);
        const pulse = Math.floor(tick / 3) % 3;
        const sparks = isDark ? ['#F4F4F5', '#A1A1AA', '#F4F4F5'] : ['#F8C618', '#4EC446', '#38BDF8'];
        for (let d = 0; d < 3; d++) {
          const lit = d === pulse;
          dot(lit ? sparks[d] : (isDark ? '#52525B' : '#64748B'), 9.6 + d * 1.15, lit ? -1.85 : -1.65, 0.7, 0.7, false);
        }
        break;
      }
      case 'Speaking':
        if (!faceBlocked) {
          drawEyes(3.9, 3.1, 6.3, 3.1, false);
          dot(eyeMain, 4.7, 4.4, 1.6, tick % 4 < 2 ? 0.9 : 0.4);
        }
        if ((Math.floor(tick / 3) % 4) >= 1) dot(isDark ? '#F4F4F5' : '#F8FAFC', 10.3, 3.8, 0.9, 0.9, false);
        if ((Math.floor(tick / 3) % 4) >= 2) dot(isDark ? '#A1A1AA' : '#38BDF8', 11.4, 2.9, 0.9, 0.9, false);
        break;
      case 'Typing': {
        if (!faceBlocked) drawEyes(3.7, 3.3, 6.1, 3.3, false, true);
        dot(isDark ? '#27272A' : '#3F4A5C', 1.4, 6.2, 8.2, 1.4);
        const tap = Math.floor(tick / 2) % 4;
        for (let i = 0; i < 4; i++) {
          const down = i === tap;
          dot(down ? (isDark ? '#F4F4F5' : '#38BDF8') : (isDark ? '#A1A1AA' : '#E2E8F0'), 2.1 + i * 1.8, down ? 6.65 : 6.45, 1.1, 0.55);
        }
        const leftDown = tick % 4 < 2;
        dot(armLU, 1.6, leftDown ? 5.3 : 4.8, 0.65, 0.9);
        dot(armRU, 8.7, leftDown ? 4.8 : 5.3, 0.65, 0.9);
        dot(isDark ? '#27272A' : '#0F172A', 8.7, -0.9, 4.2, 2.0, false);
        for (let d = 0; d < 3; d++) {
          const lift = ((Math.floor(tick / 3) + d) % 3 === 0) ? -0.3 : 0;
          const dc = d === 0 ? (isDark ? '#F4F4F5' : '#F8C618') : (d === 1 ? (isDark ? '#A1A1AA' : '#4EC446') : (isDark ? '#F4F4F5' : '#38BDF8'));
          dot(dc, 9.35 + d * 1.15, -0.18 + lift, 0.62, 0.62, false);
        }
        break;
      }
      case 'Searching': {
        const sx = Math.round(Math.sin(tick * 0.14)) * 0.35;
        if (!faceBlocked) drawEyes(3.9 + sx, 3.2, 6.3 + sx, 3.2, false);
        const gx = 3.5 + Math.round(Math.sin(tick * 0.14) * 2.8);
        shape(isDark ? '#F4F4F5' : '#38BDF8', gx, -2.4, [".###.", "#...#", "#...#", ".###."]);
        dot(isDark ? '#A1A1AA' : '#1E78FF', gx + 4, 1.4, 0.9, 0.9, false);
        break;
      }
      case 'Reading':
        if (!faceBlocked) drawEyes(3.9, 3.1, 6.3, 3.1, false, true);
        dot(isDark ? '#18181B' : '#0F172A', 0.5, 4.3, 10, 3);
        dot(isDark ? '#A1A1AA' : '#1E78FF', 5.25, 4.3, 0.5, 3);
        dot(isDark ? '#F4F4F5' : '#F8C618', 1.5, 5.0, 3, 0.4);
        dot(isDark ? '#A1A1AA' : '#4EC446', 6.5, 5.0, 3, 0.4);
        dot(isDark ? '#F4F4F5' : '#F8C618', 1.5, 6.0, 3, 0.4);
        dot(isDark ? '#A1A1AA' : '#4EC446', 6.5, 6.0, 3, 0.4);
        break;
      case 'Working':
        if (!faceBlocked) {
          drawEyes(3.6, 3.4, 6.0, 3.4, false, true);
          dot(eyeMain, 3.3, 2.7, 0.9, 0.3); dot(eyeMain, 6.1, 2.7, 0.9, 0.3);
        }
        dot(isDark ? '#A1A1AA' : '#94A3B8', -4.2, 1.8, 5.0, 6.0, false);
        dot(isDark ? '#18181B' : '#E2E8F0', -3.4, 2.5, 3.4, 4.4, false);
        dot(isDark ? '#27272A' : '#3F4A5C', -4.2, 7.8, 6.2, 0.9, false);
        dot(isDark ? '#F4F4F5' : '#1E78FF', -3.1, 3.0, 2.4, 0.45, false);
        dot(isDark ? '#A1A1AA' : '#EF443A', -2.7, 4.0, 2.0, 0.45, false);
        dot(isDark ? '#F4F4F5' : '#4EC446', -3.1, 5.0, 1.2 + (tick % 3) * 0.55, 0.45, false);
        dot(armLU, 0.6, 5.0, 1.6, 0.85, false);
        break;
      case 'Terminal': {
        if (!faceBlocked) {
          drawEyes(3.6, 3.4, 6.0, 3.4, false, true);
          dot(eyeMain, 3.3, 2.7, 0.9, 0.3); dot(eyeMain, 6.1, 2.7, 0.9, 0.3);
        }
        const tx = -4.3;
        dot(isDark ? '#A1A1AA' : '#3F4A5C', tx, 1.7, 5.2, 6.2, false);
        dot('#18181B', tx + 0.45, 2.2, 4.3, 5.2, false);
        dot(isDark ? '#F4F4F5' : '#EF443A', tx + 0.75, 2.45, 0.45, 0.45, false);
        dot(isDark ? '#A1A1AA' : '#F8C618', tx + 1.4, 2.45, 0.45, 0.45, false);
        dot(isDark ? '#52525B' : '#4EC446', tx + 2.05, 2.45, 0.45, 0.45, false);
        const prompt = isDark ? '#F4F4F5' : '#4EC446';
        dot(prompt, tx + 0.85, 3.5, 0.4, 0.4, false); dot(prompt, tx + 1.25, 3.85, 0.4, 0.4, false); dot(prompt, tx + 0.85, 4.2, 0.4, 0.4, false);
        dot(isDark ? '#A1A1AA' : '#E2E8F0', tx + 1.9, 3.7, 1.2 + (Math.floor(tick / 4) % 3) * 0.55, 0.45, false);
        dot(isDark ? '#71717A' : '#38BDF8', tx + 0.9, 5.0, 2.5, 0.42, false);
        if (tick % 6 < 3) dot(prompt, tx + 3.5, 5.9, 0.6, 0.6, false);
        dot(armLU, 0.6, 5.0, 1.6, 0.85, false);
        break;
      }
      case 'Browsing': {
        const bx = Math.round(Math.sin(tick * 0.16)) * 0.35;
        if (!faceBlocked) drawEyes(3.9 + bx, 3.1, 6.3 + bx, 3.1, false);
        shape(isDark ? '#F4F4F5' : '#38BDF8', 8.6, -1.0, [".###.", "#...#", "#...#", ".###."]);
        dot(isDark ? '#A1A1AA' : '#1E78FF', 9.6, 0.5, 3, 0.45, false);
        const sw = 1.2 + Math.round(Math.sin(tick * 0.25) * 0.9);
        dot(isDark ? '#F4F4F5' : '#F8C618', 8.6 + sw, -0.5, 0.65, 3.0, false);
        break;
      }
      case 'Multitasking': {
        const mx = (Math.floor(tick / 5) % 2 === 0) ? -0.45 : 0.45;
        if (!faceBlocked) drawEyes(3.9 + mx, 3.1, 6.3 + mx, 3.1, false);
        const ph = tick % 6 < 3;
        dot(armLU, 1.0, ph ? 3.5 : 4.2, 0.55, 1.0); dot(armLL, 1.5, 4.4, 0.5, 0.6);
        dot(armRL, 9.45, ph ? 4.2 : 3.5, 0.55, 1.0); dot(armRU, 8.95, 4.4, 0.5, 0.6);
        const orbs = isDark ? ['#F4F4F5', '#A1A1AA'] : ['#F8C618', '#4EC446', '#38BDF8', '#7B61EC', '#EF443A', '#1E78FF', '#1FA8D8', '#F8FAFC'];
        const nOrbs = Math.max(2, Math.min(8, orbCount || 3));
        const orbSize = nOrbs >= 6 ? 0.9 : 1.05;
        for (let i = 0; i < nOrbs; i++) {
          const a = tick * 0.24 + i * ((Math.PI * 2) / nOrbs);
          dot(orbs[i % orbs.length], 5.1 + Math.cos(a) * 3.9, -0.4 + Math.sin(a) * 1.55, orbSize, orbSize, false);
        }
        break;
      }
      case 'Background': {
        if (!faceBlocked) drawHappyEyes();
        const beat = tick % 10 < 5;
        dot(isDark ? '#27272A' : '#0F172A', 9.0, 0.6, 3.8, 4.6, false);
        dot(isDark ? '#52525B' : '#3F4A5C', 9.35, 1.0, 3.1, 1.5, false);
        dot(isDark ? '#52525B' : '#3F4A5C', 9.35, 3.1, 3.1, 1.5, false);
        dot(beat ? (isDark ? '#F4F4F5' : '#4EC446') : (isDark ? '#71717A' : '#1FA8D8'), 9.8, 1.45, 0.65, 0.65, false);
        dot(!beat ? (isDark ? '#F4F4F5' : '#38BDF8') : (isDark ? '#71717A' : '#1E78FF'), 9.8, 3.55, 0.65, 0.65, false);
        dot(isDark ? '#F4F4F5' : '#E2E8F0', 10.9, 1.6, 1.2, 0.35, false);
        dot(isDark ? '#A1A1AA' : '#E2E8F0', 10.9, 3.7, 1.2, 0.35, false);
        break;
      }
      case 'Alert':
        if (!faceBlocked) {
          dot('#F8FAFC', 3.6, 2.9, 1.25, 1.25); dot('#0B1220', 3.95, 3.25, 0.55, 0.55);
          dot('#F8FAFC', 6.1, 2.9, 1.25, 1.25); dot('#0B1220', 6.45, 3.25, 0.55, 0.55);
        }
        dot(armLU, 1.1, 3.7, 0.5, 1.0); dot(armRL, 9.4, 3.7, 0.5, 1.0);
        if (tick % 10 >= 2) {
          dot(isDark ? '#F4F4F5' : '#F59E0B', 5.0, -2.3, 1, 2.2, false);
          dot(isDark ? '#F4F4F5' : '#F59E0B', 5.0, 0.3, 1, 0.8, false);
        }
        break;
      case 'Question':
        if (!faceBlocked) {
          drawEyes(3.7, 3.2, 6.2, 3.2, false);
          dot('#F8FAFC', 6.0, 2.8, 1.35, 1.35); dot('#0B1220', 6.45, 3.25, 0.55, 0.55);
        }
        if (tick % 16 >= 2) {
          shape(isDark ? '#F4F4F5' : '#38BDF8', 4.0, -2.3, [".##.", "...#", "..#."]);
          dot(isDark ? '#F4F4F5' : '#38BDF8', 6.0, 0.5, 0.9, 0.9, false);
        }
        break;
      case 'Delegating': {
        if (!faceBlocked) drawEyes(4.1, 3.1, 6.5, 3.1, false);
        dot(armLU, 1.5, 4.8, 0.5, 1.0); dot(armLL, 1.1, 5.4, 0.5, 0.5);
        dot(armRU, 8.7, 4.2, 1.1, 0.65, false);
        const frame = isDark ? '#27272A' : '#0F172A';
        const slot = isDark ? '#A1A1AA' : '#3F4A5C';
        for (let row = 0; row < 2; row++) {
          const ny = row === 0 ? 0.1 : 3.4;
          dot(slot, 9.5, ny, 4.1, 2.6, false);
          dot(frame, 9.85, ny + 0.35, 3.4, 1.9, false);
          const active = (Math.floor(tick / 4) % 2) === row;
          const badge = row === 0 ? (isDark ? '#F4F4F5' : '#F8C618') : (isDark ? '#A1A1AA' : '#7B61EC');
          dot(badge, 10.2, ny + 0.7, 0.65, 0.65, false);
          dot(active ? (isDark ? '#F4F4F5' : '#38BDF8') : (isDark ? '#52525B' : '#64748B'), 11.1, ny + 0.8, 1.5, 0.45, false);
          dot(isDark ? '#52525B' : '#4EC446', 10.2, ny + 1.55, 2.1, 0.38, false);
        }
        const frac = (tick % 6) / 5.0;
        const upper = Math.floor(tick / 6) % 2 === 0;
        dot(isDark ? '#F4F4F5' : '#F8C618', 8.9 + frac * 0.8, 4.2 + (upper ? -2.6 : -0.1) * frac, 0.65, 0.65, false);
        break;
      }
      case 'Error': {
        if (!faceBlocked) {
          dot('#F8FAFC', 3.55, 2.95, 0.45, 0.35); dot('#F8FAFC', 4.0, 3.3, 0.45, 0.35); dot('#F8FAFC', 3.55, 3.65, 0.45, 0.35);
          dot('#F8FAFC', 6.85, 2.95, 0.45, 0.35); dot('#F8FAFC', 6.4, 3.3, 0.45, 0.35); dot('#F8FAFC', 6.85, 3.65, 0.45, 0.35);
        }
        dot(armLU, 1.1, 3.7, 0.5, 1.0); dot(armRL, 9.4, 3.7, 0.5, 1.0);
        if (tick % 8 >= 2) {
          shape(isDark ? '#F4F4F5' : '#EF4444', 4.3, -2.7, ["..##", ".##.", "###.", ".##.", ".#.."]);
          dot(isDark ? '#A1A1AA' : '#FFD24A', 5.2, -1.1, 0.45, 0.45, false);
        }
        break;
      }
      case 'Done': {
        if (!faceBlocked) drawHappyEyes();
        dot(armLU, 1.1, 3.7, 0.5, 1.0); dot(armRL, 9.4, 3.7, 0.5, 1.0);
        const lift = tick % 8 < 4 ? -0.25 : 0.15;
        dot(isDark ? '#F4F4F5' : '#EF443A', 0.2, 1.2 + lift, 0.45, 0.85, false);
        dot(isDark ? '#A1A1AA' : '#4EC446', 1.3, -0.7 - lift, 1.0, 0.36, false);
        dot(isDark ? '#F4F4F5' : '#F8C618', 3.1, -1.4 + lift, 0.45, 0.45, false);
        dot(isDark ? '#A1A1AA' : '#38BDF8', 8.7, -0.6 - lift, 0.9, 0.36, false);
        break;
      }
    }

    if (hurt) {
      const ey = compact ? 3.85 : 3.05;
      dot(eyeMain, 3.65, ey, 0.4, 0.35); dot(eyeMain, 4.05, ey + 0.32, 0.45, 0.35); dot(eyeMain, 3.65, ey + 0.64, 0.4, 0.35);
      dot(eyeMain, 6.75, ey, 0.4, 0.35); dot(eyeMain, 6.3, ey + 0.32, 0.45, 0.35); dot(eyeMain, 6.75, ey + 0.64, 0.4, 0.35);
    } else if (annoyed) {
      const ey = compact ? 4.05 : 3.25;
      dot(eyeMain, 3.7, ey + 0.25, 1.0, 0.45);
      dot(eyeMain, 6.2, ey + 0.25, 1.0, 0.45);
      dot(eyeMain, 3.5, ey - 0.45, 0.6, 0.32); dot(eyeMain, 4.05, ey - 0.2, 0.6, 0.32);
      dot(eyeMain, 6.8, ey - 0.45, 0.6, 0.32); dot(eyeMain, 6.25, ey - 0.2, 0.6, 0.32);
      if (tick % 8 < 6) {
        const mk = isDark ? '#F4F4F5' : '#EF4444';
        const mx = 8.4, my = compact ? 0.5 : -0.5;
        dot(mk, mx, my, 0.45, 0.45, false); dot(mk, mx + 0.9, my, 0.45, 0.45, false);
        dot(mk, mx + 0.45, my + 0.45, 0.45, 0.45, false);
        dot(mk, mx, my + 0.9, 0.45, 0.45, false); dot(mk, mx + 0.9, my + 0.9, 0.45, 0.45, false);
      }
    }

    ctx.restore();
  }

  // ---- 2. Antigravity App Simulation State ----
  const CHATS = {
    overlay: {
      heading: 'AgyPet Overlay',
      status: 'Agent typing a reply (press mascot above chatbox)',
      userMsg: 'Add live chat detection, multitasking, and a subtle press wince.',
      agentMsg: 'Updating WindowTracker.cs, ActivityMonitor.cs, and all 18 reactions in Mascot.cs…',
      mood: 'Typing',
      voice: false,
      settings: false,
    },
    server: {
      heading: 'Live Dev Server',
      status: 'Chat done, background server running on :4173',
      userMsg: 'Start the local preview server so I can record the landing page.',
      agentMsg: 'Server is live at http://127.0.0.1:4173/site/ and humming in the background.',
      mood: 'Background',
      voice: false,
      settings: false,
    },
    sandbox: {
      heading: 'Idle Sandbox',
      status: 'Agent idle: start typing below to trigger Typing mood',
      userMsg: 'Let me draft the Reddit release post here.',
      agentMsg: 'Ready whenever you are. Start typing in the box below!',
      mood: 'Idle',
      voice: false,
      settings: false,
    },
    voice: {
      heading: 'Voice Mode',
      status: 'Syncs live to who is speaking vs listening',
      userMsg: 'Live voice session active.',
      agentMsg: 'Toggle who is talking below to watch AgyPet flip between Listening and Speaking.',
      mood: 'Listening',
      voice: true,
      settings: false,
    },
    cli: {
      heading: 'Windows Terminal · agy',
      status: 'Detects agy CLI terminals & perches cleanly by the prompt',
      userMsg: 'agy --model gemini-3-pro "run test suite"',
      agentMsg: 'Attached natively to Windows Terminal, PowerShell, pwsh, CMD, Alacritty & more (Bottom/Top edge placements).',
      mood: 'Terminal',
      voice: false,
      settings: false,
    },
    settings: {
      heading: 'Settings',
      status: 'No chatbox on screen: mascot calmly parks in the corner',
      userMsg: '',
      agentMsg: '',
      mood: 'Idle',
      voice: false,
      settings: true,
    },
  };

  const MOCK_SESSIONS = [
    { name: 'AgyPet Overlay', act: 'Editing PetWindow.cs', pct: 75, steps: '3/4 steps' },
    { name: 'Antigravity IDE', act: 'Running unit tests', pct: 50, steps: '2/4 steps' },
    { name: 'Antigravity CLI', act: 'Searching documentation', pct: 25, steps: '1/4 steps' },
  ];

  let currentSkin = 'classic';
  let activeChat = 'overlay';
  let heroMood = 'Typing';
  let heroPx = 4.2;
  let awayMode = 'none'; // 'none' | 'multi' | 'one'
  let dictationOn = false;
  let userTypingTimer = 0;
  let motionPaused = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  let globalTick = 0;
  let hurtTicks = 0;
  let danceTicks = 0;
  let annoyedTicks = 0;
  let clickStreak = 0;
  let lastClickMs = 0;
  let simLookX = 0;
  let simLookY = 0;

  const navCanvas = document.getElementById('nav-mascot');
  const heroCanvas = document.getElementById('hero-mascot');
  const stagePet = document.getElementById('stage-pet');
  const stageViewport = document.getElementById('stage-viewport');
  const composerCard = document.getElementById('composer-card');
  const mockApp = document.getElementById('mock-app');
  const awayBanner = document.getElementById('away-banner');
  const awayCopy = document.getElementById('away-copy');
  const hoverCard = document.getElementById('stage-hover-card');
  const hoverScroll = document.getElementById('hover-scroll');
  const hoverCount = document.getElementById('hover-count');
  const hoverScrollHint = document.getElementById('hover-scroll-hint');
  const chatHeading = document.getElementById('chat-heading');
  const chatStatus = document.getElementById('chat-status');
  const msgUser = document.getElementById('msg-user');
  const msgAgent = document.getElementById('msg-agent');
  const voiceBar = document.getElementById('voice-bar');
  const settingsPane = document.getElementById('settings-pane');
  const cycleMoodBtn = document.getElementById('cycle-mood');
  const dictationBtn = document.getElementById('dictation-btn');
  const stageLive = document.getElementById('stage-live');

  // Build 18-reaction carousel + mouse drag-to-scroll & prev/next controls
  const stripEl = document.getElementById('mood-strip');
  const moodPrevBtn = document.getElementById('mood-prev');
  const moodNextBtn = document.getElementById('mood-next');
  const moodTiles = [];
  let suppressTileClickUntil = 0;

  MOODS.forEach((m) => {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'mood-tile';
    btn.setAttribute('aria-pressed', String(m.id === heroMood));
    const c = document.createElement('canvas');
    c.width = 90;
    c.height = 66;
    c.setAttribute('aria-hidden', 'true');
    const setup = setupHiDpiCanvas(c);
    const name = document.createElement('span');
    name.className = 'mood-name';
    name.textContent = m.id;
    const sub = document.createElement('span');
    sub.className = 'mood-sub';
    sub.textContent = m.sub;
    btn.append(c, name, sub);
    btn.addEventListener('click', () => {
      if (performance.now() < suppressTileClickUntil) return;
      if (awayMode !== 'none') setAwayMode('none');
      if (activeChat === 'settings') selectChat('overlay');
      setHeroMood(m.id);
      document.getElementById('demo').scrollIntoView({ behavior: motionPaused ? 'auto' : 'smooth' });
    });
    stripEl.appendChild(btn);
    moodTiles.push({ mood: m.id, canvas: c, ctx: setup.ctx, w: setup.w, h: setup.h, dpr: setup.dpr, btn });
  });

  // Mouse drag-to-scroll on 18-reaction strip
  let stripDragging = false, stripDidMove = false, stripStartX = 0, stripStartScroll = 0;
  stripEl.addEventListener('pointerdown', (e) => {
    if (e.pointerType !== 'mouse' || e.button !== 0) return;
    stripDragging = true;
    stripDidMove = false;
    stripStartX = e.clientX;
    stripStartScroll = stripEl.scrollLeft;
  });
  window.addEventListener('pointermove', (e) => {
    if (!stripDragging) return;
    const dx = e.clientX - stripStartX;
    if (!stripDidMove && Math.abs(dx) > 5) {
      stripDidMove = true;
      stripEl.classList.add('is-dragging');
    }
    if (stripDidMove) {
      stripEl.scrollLeft = stripStartScroll - dx;
    }
  });
  const endStripDrag = () => {
    if (!stripDragging) return;
    stripDragging = false;
    stripEl.classList.remove('is-dragging');
    if (stripDidMove) suppressTileClickUntil = performance.now() + 90;
  };
  window.addEventListener('pointerup', endStripDrag);
  window.addEventListener('pointercancel', endStripDrag);

  function syncCarouselArrows() {
    if (!moodPrevBtn || !moodNextBtn) return;
    const maxScroll = stripEl.scrollWidth - stripEl.clientWidth;
    moodPrevBtn.disabled = stripEl.scrollLeft <= 4;
    moodNextBtn.disabled = stripEl.scrollLeft >= maxScroll - 4;
  }
  if (moodPrevBtn && moodNextBtn) {
    moodPrevBtn.addEventListener('click', () => stripEl.scrollBy({ left: -360, behavior: motionPaused ? 'auto' : 'smooth' }));
    moodNextBtn.addEventListener('click', () => stripEl.scrollBy({ left: 360, behavior: motionPaused ? 'auto' : 'smooth' }));
    stripEl.addEventListener('scroll', syncCarouselArrows, { passive: true });
    window.addEventListener('resize', syncCarouselArrows);
    requestAnimationFrame(syncCarouselArrows);
  }

  const skinSamples = Array.from(document.querySelectorAll('.skin-sample')).map((c) => {
    const s = setupHiDpiCanvas(c);
    return { canvas: c, ctx: s.ctx, w: s.w, h: s.h, dpr: s.dpr, skin: c.dataset.skin, mood: c.dataset.mood };
  });

  function setHeroMood(m) {
    heroMood = m;
    cycleMoodBtn.textContent = `Mood: ${heroMood}`;
    const info = MOODS.find((item) => item.id === m);
    const sub = info ? info.sub : '';
    const wakeHint = m === 'Sleeping' ? ' — press to wake' : ' — press to poke';
    stagePet.setAttribute('aria-label', `AgyPet mascot: ${m}${sub ? ` (${sub})` : ''}${wakeHint}`);
    if (stageLive) stageLive.textContent = `Mascot state: ${m}${sub ? `, ${sub}` : ''}`;
    moodTiles.forEach((t) => t.btn.setAttribute('aria-pressed', String(t.mood === heroMood)));
    computeStageTarget();
    renderAllOnce();
  }

  function renderHoverSessions(count) {
    const list = MOCK_SESSIONS.slice(0, count);
    hoverCount.textContent = count > 1 ? `${count} active sessions` : '1 active session';
    hoverScrollHint.textContent = count > 2 ? 'Scroll' : '';
    hoverScroll.innerHTML = list.map((s) => `
      <div class="session-item">
        <div class="session-name">${s.name}</div>
        <div class="session-act">${s.act}</div>
        <div class="bar-track"><div class="bar-fill" style="transform: scaleX(${s.pct / 100});"></div></div>
        <div class="session-pct">${s.pct}% done, ${100 - s.pct}% left (${s.steps})</div>
      </div>
    `).join('');
  }
  renderHoverSessions(3);

  function selectChat(id) {
    activeChat = id;
    dictationOn = false;
    dictationBtn.setAttribute('aria-pressed', 'false');
    const c = CHATS[id];
    document.querySelectorAll('.chat-tab').forEach((tab) => {
      tab.setAttribute('aria-selected', String(tab.dataset.chat === id));
    });
    chatHeading.textContent = c.heading;
    chatStatus.textContent = c.status;
    msgUser.hidden = c.settings;
    msgAgent.hidden = c.settings;
    if (!c.settings) {
      msgUser.textContent = c.userMsg;
      msgAgent.textContent = c.agentMsg;
    }
    voiceBar.classList.toggle('is-active', c.voice);
    settingsPane.classList.toggle('is-active', c.settings);
    composerCard.hidden = c.settings;
    setHeroMood(c.mood);
    computeStageTarget();
  }

  const chatTabs = Array.from(document.querySelectorAll('.chat-tab'));
  chatTabs.forEach((tab, idx) => {
    tab.addEventListener('click', () => {
      if (awayMode !== 'none') setAwayMode('none');
      selectChat(tab.dataset.chat);
    });
    tab.addEventListener('keydown', (e) => {
      let next = -1;
      if (e.key === 'ArrowDown' || e.key === 'ArrowRight') next = (idx + 1) % chatTabs.length;
      else if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') next = (idx - 1 + chatTabs.length) % chatTabs.length;
      else if (e.key === 'Home') next = 0;
      else if (e.key === 'End') next = chatTabs.length - 1;
      if (next >= 0) {
        e.preventDefault();
        if (awayMode !== 'none') setAwayMode('none');
        chatTabs[next].focus();
        selectChat(chatTabs[next].dataset.chat);
      }
    });
  });

  // Voice Mode speaker switcher
  const voiceUserBtn = document.getElementById('voice-user-btn');
  const voiceAgentBtn = document.getElementById('voice-agent-btn');
  voiceUserBtn.addEventListener('click', () => {
    voiceUserBtn.setAttribute('aria-pressed', 'true');
    voiceAgentBtn.setAttribute('aria-pressed', 'false');
    setHeroMood('Listening');
  });
  voiceAgentBtn.addEventListener('click', () => {
    voiceUserBtn.setAttribute('aria-pressed', 'false');
    voiceAgentBtn.setAttribute('aria-pressed', 'true');
    setHeroMood('Speaking');
  });

  // Dictation mic button
  dictationBtn.addEventListener('click', () => {
    dictationOn = !dictationOn;
    dictationBtn.setAttribute('aria-pressed', String(dictationOn));
    setHeroMood(dictationOn ? 'Listening' : CHATS[activeChat].mood);
  });

  // Smooth compositor positioning of mascot above chatbox or in corner
  let petX = 0, petY = 0, targetX = 0, targetY = 0, posReady = false;

  function isPerchedOnChatBox() {
    return awayMode === 'none' && activeChat !== 'settings' && !composerCard.hidden;
  }

  function computeStageTarget() {
    const vp = stageViewport.getBoundingClientRect();
    const spriteW = 11 * heroPx;
    const spriteH = 9 * heroPx;
    const shiftLeftProp = (heroMood === 'Working' || heroMood === 'Terminal') ? 1.4 * heroPx : 0;
    const canvasOffsetX = (heroMeta.w - spriteW) * 0.5 + shiftLeftProp;
    const canvasOffsetY = (heroMeta.h - spriteH) * 0.5 + 1.3 * heroPx;

    if (isPerchedOnChatBox()) {
      const box = composerCard.getBoundingClientRect();
      const relRight = box.right - vp.left;
      const relTop = box.top - vp.top;
      targetX = (relRight - 24) - spriteW - canvasOffsetX - 8;
      targetY = (relTop + 1) - spriteH - canvasOffsetY - 8;
    } else {
      targetX = vp.width - heroMeta.w - 14;
      targetY = 10;
    }
    if (!posReady) {
      petX = targetX;
      petY = targetY;
      posReady = true;
    }
  }

  // Single-click gently dances; rapid clicking (4+ within 2.2s) makes him annoyed; Sleeping wakes up!
  stagePet.addEventListener('click', () => {
    if (heroMood === 'Sleeping') {
      setHeroMood('Idle');
      chatStatus.textContent = 'Woke up from sleep (At rest)';
    }
    const nowMs = performance.now();
    if (nowMs - lastClickMs > 2200) clickStreak = 0;
    clickStreak++;
    lastClickMs = nowMs;
    if (clickStreak >= 4) {
      danceTicks = 0;
      annoyedTicks = 28;
    } else {
      annoyedTicks = 0;
      danceTicks = 24;
    }
    renderAllOnce();
  });

  window.addEventListener('pointermove', (e) => {
    const r = stagePet.getBoundingClientRect();
    if (r.width <= 0) return;
    const cx = r.left + r.width * 0.5;
    const cy = r.top + r.height * 0.45;
    simLookX = Math.tanh((e.clientX - cx) / 200);
    simLookY = Math.tanh((e.clientY - cy) / 140);
  }, { passive: true });

  // Hover card ONLY when away / minimized
  let hoverLeave = 0;
  const openHover = () => {
    clearTimeout(hoverLeave);
    if (awayMode !== 'none') hoverCard.classList.add('is-open');
  };
  const closeHover = () => {
    clearTimeout(hoverLeave);
    hoverLeave = setTimeout(() => hoverCard.classList.remove('is-open'), 180);
  };
  stagePet.addEventListener('mouseenter', openHover);
  stagePet.addEventListener('mouseleave', closeHover);
  hoverCard.addEventListener('mouseenter', openHover);
  hoverCard.addEventListener('mouseleave', closeHover);

  const btnInApp = document.getElementById('btn-in-app');
  const btnAwayMulti = document.getElementById('btn-away-multi');
  const btnAwayOne = document.getElementById('btn-away-one');

  function setAwayMode(mode) {
    awayMode = mode;
    btnInApp.setAttribute('aria-pressed', String(mode === 'none'));
    btnAwayMulti.setAttribute('aria-pressed', String(mode === 'multi'));
    btnAwayOne.setAttribute('aria-pressed', String(mode === 'one'));
    const away = mode !== 'none';
    mockApp.classList.toggle('is-minimized', away);
    awayBanner.classList.toggle('is-shown', away);

    if (mode === 'none') {
      hoverCard.classList.remove('is-open');
      setHeroMood(CHATS[activeChat].mood);
    } else if (mode === 'multi') {
      renderHoverSessions(3);
      awayCopy.textContent = '3 active sessions running while minimized: AgyPet switches to Multitasking and the hover card scrolls all active chats.';
      setHeroMood('Multitasking');
      hoverCard.classList.add('is-open');
    } else {
      renderHoverSessions(1);
      awayCopy.textContent = '1 active session running while minimized: AgyPet shows that session’s exact live reaction.';
      setHeroMood('Working');
      hoverCard.classList.add('is-open');
    }
    computeStageTarget();
  }

  btnInApp.addEventListener('click', () => setAwayMode('none'));
  btnAwayMulti.addEventListener('click', () => setAwayMode('multi'));
  btnAwayOne.addEventListener('click', () => setAwayMode('one'));
  document.getElementById('btn-restore-app').addEventListener('click', () => setAwayMode('none'));

  // Size switcher
  document.querySelectorAll('.size-btn').forEach((btn) => {
    btn.addEventListener('click', () => {
      heroPx = parseFloat(btn.dataset.size);
      document.querySelectorAll('.size-btn').forEach((b) => b.setAttribute('aria-pressed', String(b === btn)));
      computeStageTarget();
      renderAllOnce();
    });
  });

  // Attachments & real user typing trigger
  const attachRow = document.getElementById('attachment-row');
  const sampleFiles = ['PetWindow.cs', 'screenshot.png', 'Mascot.cs'];
  let fileIdx = 0;
  document.getElementById('add-attachment').addEventListener('click', () => {
    if (attachRow.children.length >= 3) {
      attachRow.innerHTML = '';
      attachRow.classList.remove('has-items');
    } else {
      const chip = document.createElement('span');
      chip.className = 'file-chip';
      chip.textContent = sampleFiles[fileIdx++ % sampleFiles.length];
      attachRow.appendChild(chip);
      attachRow.classList.add('has-items');
    }
    computeStageTarget();
  });

  const composerInput = document.getElementById('composer-input');
  composerInput.addEventListener('input', () => {
    composerInput.rows = Math.min(4, Math.max(1, composerInput.value.split('\n').length + (composerInput.value.length > 52 ? 1 : 0)));
    computeStageTarget();
    if (activeChat === 'sandbox' && !dictationOn && awayMode === 'none') {
      setHeroMood('Typing');
      clearTimeout(userTypingTimer);
      userTypingTimer = setTimeout(() => {
        if (activeChat === 'sandbox' && !dictationOn && awayMode === 'none') setHeroMood('Idle');
      }, 1500);
    }
  });

  cycleMoodBtn.addEventListener('click', () => {
    const idx = (MOODS.findIndex((m) => m.id === heroMood) + 1) % MOODS.length;
    setHeroMood(MOODS[idx].id);
  });

  function applySkin(skin) {
    currentSkin = skin;
    try { localStorage.setItem('agypet-skin', skin); } catch {}
    document.querySelectorAll('.skin-btn').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.skin === skin)));
    renderAllOnce();
  }
  try {
    const savedSkin = localStorage.getItem('agypet-skin');
    if (savedSkin === 'classic' || savedSkin === 'dark') applySkin(savedSkin);
  } catch {}
  document.querySelectorAll('.skin-btn').forEach((b) => b.addEventListener('click', () => applySkin(b.dataset.skin)));
  document.querySelectorAll('.try-skin-btn').forEach((b) => b.addEventListener('click', () => {
    applySkin(b.dataset.trySkin);
    document.getElementById('demo').scrollIntoView({ behavior: motionPaused ? 'auto' : 'smooth' });
  }));

  // Pause / Play motion toggle (WCAG 2.2.2)
  const motionBtn = document.getElementById('motion-toggle');
  const motionLabel = document.getElementById('motion-label');
  const iconPause = document.getElementById('icon-pause');
  const iconPlay = document.getElementById('icon-play');
  function syncMotionUi() {
    const label = motionPaused ? 'Play motion' : 'Pause motion';
    motionBtn.title = label;
    motionLabel.textContent = label;
    iconPause.classList.toggle('is-hidden', motionPaused);
    iconPlay.classList.toggle('is-hidden', !motionPaused);
  }
  syncMotionUi();
  motionBtn.addEventListener('click', () => {
    motionPaused = !motionPaused;
    syncMotionUi();
  });

  const navMeta = setupHiDpiCanvas(navCanvas);
  const heroMeta = setupHiDpiCanvas(heroCanvas);
  let lastTickAt = 0;

  function renderAllOnce() {
    drawMascot(navMeta.ctx, navMeta.w, navMeta.h, 2.1, currentSkin, 'Idle', globalTick, 0, navMeta.dpr, 3, simLookX, simLookY);
    drawMascot(heroMeta.ctx, heroMeta.w, heroMeta.h, heroPx, currentSkin, heroMood, globalTick, { hurt: hurtTicks, dance: danceTicks, annoyed: annoyedTicks }, heroMeta.dpr, 3, simLookX, simLookY);
    moodTiles.forEach((t, i) => drawMascot(t.ctx, t.w, t.h, 3.6, currentSkin, t.mood, globalTick + i * 3, 0, t.dpr, 3, t.mood === 'Idle' ? simLookX : 0, t.mood === 'Idle' ? simLookY : 0));
    skinSamples.forEach((s, i) => drawMascot(s.ctx, s.w, s.h, 4.2, s.skin, s.mood, globalTick + i * 5, 0, s.dpr));
  }

  function frame(now) {
    computeStageTarget();
    if (motionPaused) {
      petX = targetX;
      petY = targetY;
    } else {
      petX += (targetX - petX) * 0.22;
      petY += (targetY - petY) * 0.22;
    }
    stagePet.style.transform = `translate3d(${petX.toFixed(1)}px, ${petY.toFixed(1)}px, 0)`;

    if (!motionPaused && now - lastTickAt >= 85) {
      lastTickAt = now;
      globalTick++;
      if (hurtTicks > 0) hurtTicks--;
      if (danceTicks > 0) danceTicks--;
      if (annoyedTicks > 0) annoyedTicks--;
      renderAllOnce();
    }
    requestAnimationFrame(frame);
  }

  window.addEventListener('resize', computeStageTarget);
  renderAllOnce();
  requestAnimationFrame(frame);

  // ---- 3. Distortion-Free Fixed-Width Sliding Navbar Bar ----
  const navLinksWrap = document.getElementById('pill-nav-links');
  const navBar = document.getElementById('nav-bar');
  if (navLinksWrap && navBar) {
    navLinksWrap.querySelectorAll('.nav-item').forEach((item) => {
      const move = () => {
        const ir = item.getBoundingClientRect();
        const nr = navLinksWrap.getBoundingClientRect();
        const centerX = (ir.left - nr.left) + (ir.width * 0.5) - 10;
        navBar.style.transform = `translateX(${centerX.toFixed(1)}px)`;
        navBar.style.opacity = '1';
      };
      item.addEventListener('mouseenter', move);
      item.addEventListener('focus', move);
    });
    navLinksWrap.addEventListener('mouseleave', () => {
      navBar.style.opacity = '0';
    });
  }

  // ---- 4. Mobile Navigation Drawer (100dvh, Escape close, optical blur icon swap) ----
  const menuBtn = document.getElementById('menu-toggle');
  const drawer = document.getElementById('mobile-drawer');
  const iconMenu = document.getElementById('icon-menu');
  const iconClose = document.getElementById('icon-close');

  function setDrawerOpen(open) {
    drawer.hidden = !open;
    menuBtn.setAttribute('aria-expanded', String(open));
    iconMenu.classList.toggle('is-hidden', open);
    iconClose.classList.toggle('is-hidden', !open);
  }

  menuBtn.addEventListener('click', () => setDrawerOpen(drawer.hidden));
  drawer.querySelectorAll('a').forEach((a) => a.addEventListener('click', () => setDrawerOpen(false)));
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && !drawer.hidden) setDrawerOpen(false);
  });

  // ---- 5. Modern Web Guidance 2-State Dark Mode Override ----
  const metaScheme = document.querySelector('meta[name="color-scheme"]');
  const themeBtn = document.getElementById('theme-toggle');
  const iconSun = document.getElementById('icon-sun');
  const iconMoon = document.getElementById('icon-moon');
  const themeLabel = document.getElementById('theme-label');
  const darkMql = window.matchMedia('(prefers-color-scheme: dark)');

  function getRenderedScheme() {
    let stored = null;
    try { stored = localStorage.getItem('color-scheme'); } catch {}
    if (stored === 'dark' || stored === 'light') return stored;
    return darkMql.matches ? 'dark' : 'light';
  }

  function syncThemeIcons() {
    const isDark = getRenderedScheme() === 'dark';
    iconSun.classList.toggle('is-hidden', !isDark);
    iconMoon.classList.toggle('is-hidden', isDark);
    themeLabel.textContent = isDark ? 'Switch to light theme' : 'Switch to dark theme';
  }

  themeBtn.addEventListener('click', () => {
    const target = getRenderedScheme() === 'dark' ? 'light' : 'dark';
    const sys = darkMql.matches ? 'dark' : 'light';
    if (target === sys) {
      try { localStorage.removeItem('color-scheme'); } catch {}
      metaScheme.content = 'light dark';
    } else {
      try { localStorage.setItem('color-scheme', target); } catch {}
      metaScheme.content = target;
    }
    syncThemeIcons();
  });

  darkMql.addEventListener('change', syncThemeIcons);
  window.addEventListener('storage', (e) => {
    if (e.key === 'color-scheme') {
      metaScheme.content = e.newValue ?? 'light dark';
      syncThemeIcons();
    }
  });
  syncThemeIcons();

  // ---- 6. Copy Snippet with Optical Blur Swap ----
  const copyBtn = document.getElementById('copy-cmd');
  const iconCopy = document.getElementById('icon-copy');
  const iconCheck = document.getElementById('icon-check');
  let copyReset = 0;
  copyBtn.addEventListener('click', async () => {
    try { await navigator.clipboard.writeText('.\\build.ps1'); } catch {}
    iconCopy.classList.add('is-hidden');
    iconCheck.classList.remove('is-hidden');
    clearTimeout(copyReset);
    copyReset = setTimeout(() => {
      iconCheck.classList.add('is-hidden');
      iconCopy.classList.remove('is-hidden');
    }, 1400);
  });

  // ---- 7. Feedback Form (GitHub Issue + Clipboard + Draft Restore) ----
  const fbForm = document.getElementById('feedback-form');
  const fbMsg = document.getElementById('feedback-msg');
  const fbStatus = document.getElementById('feedback-status');
  const fbCopy = document.getElementById('feedback-copy');
  let fbCategory = 'Reaction Idea';

  try {
    const savedDraft = localStorage.getItem('agypet-feedback-draft');
    if (savedDraft) fbMsg.value = savedDraft;
  } catch {}

  fbMsg.addEventListener('input', () => {
    try { localStorage.setItem('agypet-feedback-draft', fbMsg.value); } catch {}
  });
  fbMsg.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      e.preventDefault();
      fbForm.requestSubmit();
    }
  });

  document.querySelectorAll('.fb-cat').forEach((btn) => {
    btn.addEventListener('click', () => {
      fbCategory = btn.dataset.cat;
      document.querySelectorAll('.fb-cat').forEach((b) => b.setAttribute('aria-pressed', String(b === btn)));
    });
  });

  fbForm.addEventListener('submit', (e) => {
    e.preventDefault();
    const text = fbMsg.value.trim();
    if (!text) {
      fbStatus.textContent = 'Please write a short note first.';
      fbMsg.focus();
      return;
    }
    const label = fbCategory === 'Bug Report' ? 'bug' : (fbCategory === 'Reaction Idea' || fbCategory === 'Feature Request' ? 'enhancement' : '');
    const title = encodeURIComponent(`[AgyPet ${fbCategory}] ${text.slice(0, 56)}${text.length > 56 ? '…' : ''}`);
    const body = encodeURIComponent(`${text}\n\n---\nSubmitted from AgyPet website (Skin: ${currentSkin})`);
    const labelParam = label ? `&labels=${label}` : '';
    window.open(`https://github.com/yvs1n/agypet/issues/new?title=${title}&body=${body}${labelParam}`, '_blank', 'noopener,noreferrer');
    fbStatus.textContent = 'Opened pre-filled GitHub issue.';
    try { localStorage.removeItem('agypet-feedback-draft'); } catch {}
  });

  fbCopy.addEventListener('click', async () => {
    const text = fbMsg.value.trim();
    if (!text) {
      fbStatus.textContent = 'Please write a short note first.';
      fbMsg.focus();
      return;
    }
    const formatted = `[AgyPet ${fbCategory}]\n${text}\n\n-- Skin: ${currentSkin}`;
    try { await navigator.clipboard.writeText(formatted); } catch {}
    fbStatus.textContent = 'Copied feedback to clipboard.';
  });

  // ---- 8. IntersectionObserver Fallback for Scroll-Driven Entry Effects ----
  if (!CSS.supports('(animation-timeline: view()) and (animation-range: entry)') &&
      window.matchMedia('(prefers-reduced-motion: no-preference)').matches) {
    const items = document.querySelectorAll('.reveal-on-scroll');
    items.forEach((el) => el.classList.add('reveal-fallback-hidden'));
    const io = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.remove('reveal-fallback-hidden');
          entry.target.classList.add('reveal-fallback-shown');
          io.unobserve(entry.target);
        }
      });
    }, { threshold: 0.15 });
    items.forEach((el) => io.observe(el));
  }
})();
