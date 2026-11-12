(() => {
  const QUESTIONS = window.QUIZ_QUESTIONS || [];
  const TOTAL = QUESTIONS.length;

  // === DOM HELPERS ===
  const $ = (id) => document.getElementById(id);

  const views = {
    home: $("view-home"),
    lobby: $("view-lobby"),
    game: $("view-game"),
    summary: $("view-summary"),
  };

  function showView(name) {
    Object.entries(views).forEach(([k, el]) => {
      el.classList.toggle("active", k === name);
    });
  }

  // === STATE ===
  const state = {
    sfxEnabled: true,
    musicEnabled: false,

    // Online mode state
    online: {
      code: null,
      playerId: null, // "p1" | "p2"
      eventSource: null,
      lastPhaseKey: "",
      roomState: null,
    },

    reviewFilter: "all",
  };

  // === WEB AUDIO API (SFX) & MP3 BACKGROUND MUSIC ===
  let audioCtx = null;
  const bgAudio = new Audio("/bg-music.mp3");
  bgAudio.loop = true;
  bgAudio.volume = 0.45;
  bgAudio.preload = "auto";

  function ensureAudio() {
    if (!audioCtx) {
      const Ctx = window.AudioContext || window.webkitAudioContext;
      if (Ctx) audioCtx = new Ctx();
    }
    if (audioCtx && audioCtx.state === "suspended") {
      audioCtx.resume().catch(() => {});
    }
    return audioCtx;
  }

  function playTone(freq, duration = 0.18, type = "sine", vol = 0.09, delay = 0) {
    if (!state.sfxEnabled) return;
    const ctx = ensureAudio();
    if (!ctx) return;
    const now = ctx.currentTime + delay;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, now);
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(vol, now + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(now);
    osc.stop(now + duration + 0.02);
  }

  function playSound(kind) {
    if (!state.sfxEnabled) return;
    if (kind === "lock") {
      playTone(587.33, 0.14, "triangle", 0.08, 0);
      playTone(880, 0.18, "sine", 0.07, 0.06);
    } else if (kind === "match") {
      playTone(523.25, 0.22, "triangle", 0.09, 0);
      playTone(659.25, 0.22, "triangle", 0.09, 0.08);
      playTone(783.99, 0.26, "triangle", 0.1, 0.16);
      playTone(1046.5, 0.42, "sine", 0.1, 0.24);
    } else if (kind === "diff") {
      playTone(440, 0.18, "triangle", 0.08, 0);
      playTone(392, 0.26, "sine", 0.08, 0.11);
    } else if (kind === "victory") {
      [523.25, 659.25, 783.99, 1046.5, 1318.5].forEach((f, i) => {
        playTone(f, 0.38, "triangle", 0.085, i * 0.09);
      });
    }
  }

  function updateMusicButton() {
    $("btn-music").classList.toggle("active", state.musicEnabled);
    $("music-label").textContent = state.musicEnabled ? "Nhạc: Bật" : "Nhạc: Tắt";
  }

  $("btn-music").addEventListener("click", async () => {
    state.musicEnabled = !state.musicEnabled;
    updateMusicButton();
    if (state.musicEnabled) {
      try {
        await bgAudio.play();
      } catch {
        state.musicEnabled = false;
        updateMusicButton();
      }
    } else {
      bgAudio.pause();
    }
  });

  $("btn-sfx").addEventListener("click", () => {
    state.sfxEnabled = !state.sfxEnabled;
    $("sfx-icon").textContent = state.sfxEnabled ? "🔊" : "🔇";
  });

  // === FALLING SNOWFLAKES, AVOCADOS & MATCH CONFETTI ===
  function createAvocadoSprite() {
    const off = document.createElement("canvas");
    off.width = 72;
    off.height = 72;
    const c = off.getContext("2d");
    c.translate(36, 36);

    // Outer pear-shaped green skin
    c.beginPath();
    c.moveTo(0, -26);
    c.bezierCurveTo(13, -26, 22, -6, 22, 8);
    c.bezierCurveTo(22, 22, 13, 28, 0, 28);
    c.bezierCurveTo(-13, 28, -22, 22, -22, 8);
    c.bezierCurveTo(-22, -6, -13, -26, 0, -26);
    c.closePath();
    c.fillStyle = "#3f7d20";
    c.fill();

    // Creamy light-green flesh
    c.beginPath();
    c.moveTo(0, -21);
    c.bezierCurveTo(10, -21, 17, -4, 17, 8);
    c.bezierCurveTo(17, 19, 10, 23, 0, 23);
    c.bezierCurveTo(-10, 23, -17, 19, -17, 8);
    c.bezierCurveTo(-17, -4, -10, -21, 0, -21);
    c.closePath();
    c.fillStyle = "#c9e874";
    c.fill();

    // Inner bright highlight ring
    c.beginPath();
    c.ellipse(0, 7, 12, 13, 0, 0, Math.PI * 2);
    c.fillStyle = "#e6f59e";
    c.fill();

    // Warm brown seed/pit
    c.beginPath();
    c.arc(0, 9, 8.5, 0, Math.PI * 2);
    c.fillStyle = "#8c5230";
    c.fill();

    // Cute tiny specular shine on seed
    c.beginPath();
    c.arc(-3, 6, 2.3, 0, Math.PI * 2);
    c.fillStyle = "rgba(255, 255, 255, 0.72)";
    c.fill();

    return off;
  }

  function createSnowflakeSprite() {
    const off = document.createElement("canvas");
    off.width = 40;
    off.height = 40;
    const c = off.getContext("2d");
    c.translate(20, 20);
    c.strokeStyle = "rgba(255, 255, 255, 0.95)";
    c.lineWidth = 2.2;
    c.lineCap = "round";
    for (let i = 0; i < 6; i++) {
      c.save();
      c.rotate((Math.PI / 3) * i);
      c.beginPath();
      c.moveTo(0, 0);
      c.lineTo(0, -14);
      c.moveTo(0, -8);
      c.lineTo(-4, -11);
      c.moveTo(0, -8);
      c.lineTo(4, -11);
      c.stroke();
      c.restore();
    }
    return off;
  }

  function initSnowCanvas() {
    const canvas = $("snow-canvas");
    const ctx = canvas.getContext("2d");
    let w = (canvas.width = window.innerWidth);
    let h = (canvas.height = window.innerHeight);

    const avocadoSprite = createAvocadoSprite();
    const flakeSprite = createSnowflakeSprite();

    const flakes = Array.from({ length: 58 }, (_, idx) => ({
      kind: idx % 4 === 0 ? "crystal" : "dot",
      x: Math.random() * w,
      y: Math.random() * h,
      r: 2 + Math.random() * 3.4,
      size: 12 + Math.random() * 12,
      vy: 0.45 + Math.random() * 0.95,
      vx: (Math.random() - 0.5) * 0.4,
      alpha: 0.4 + Math.random() * 0.5,
      wobble: Math.random() * Math.PI * 2,
      rot: Math.random() * Math.PI * 2,
      vrot: (Math.random() - 0.5) * 0.015,
    }));

    const avocados = Array.from({ length: 14 }, () => ({
      x: Math.random() * w,
      y: Math.random() * h,
      size: 22 + Math.random() * 14,
      vy: 0.55 + Math.random() * 0.75,
      vx: (Math.random() - 0.5) * 0.35,
      alpha: 0.52 + Math.random() * 0.33,
      wobble: Math.random() * Math.PI * 2,
      rot: (Math.random() - 0.5) * 0.6,
      vrot: (Math.random() - 0.5) * 0.014,
    }));

    window.addEventListener("resize", () => {
      w = canvas.width = window.innerWidth;
      h = canvas.height = window.innerHeight;
    });

    function frame() {
      ctx.clearRect(0, 0, w, h);

      for (const f of flakes) {
        f.wobble += 0.015;
        f.y += f.vy;
        f.x += f.vx + Math.sin(f.wobble) * 0.35;
        f.rot += f.vrot;
        if (f.y > h + 20) {
          f.y = -20;
          f.x = Math.random() * w;
        }
        if (f.x > w + 20) f.x = -20;
        if (f.x < -20) f.x = w + 20;

        if (f.kind === "crystal") {
          ctx.save();
          ctx.globalAlpha = f.alpha;
          ctx.translate(f.x, f.y);
          ctx.rotate(f.rot);
          ctx.drawImage(flakeSprite, -f.size / 2, -f.size / 2, f.size, f.size);
          ctx.restore();
        } else {
          ctx.beginPath();
          ctx.arc(f.x, f.y, f.r, 0, Math.PI * 2);
          ctx.fillStyle = `rgba(255, 255, 255, ${f.alpha})`;
          ctx.shadowColor = "rgba(150, 195, 245, 0.55)";
          ctx.shadowBlur = 6;
          ctx.fill();
        }
      }
      ctx.shadowBlur = 0;

      for (const a of avocados) {
        a.wobble += 0.014;
        a.y += a.vy;
        a.x += a.vx + Math.sin(a.wobble) * 0.45;
        a.rot += a.vrot;
        if (a.y > h + 36) {
          a.y = -36;
          a.x = Math.random() * w;
        }
        if (a.x > w + 36) a.x = -36;
        if (a.x < -36) a.x = w + 36;

        ctx.save();
        ctx.globalAlpha = a.alpha;
        ctx.translate(a.x, a.y);
        ctx.rotate(a.rot);
        ctx.drawImage(avocadoSprite, -a.size / 2, -a.size / 2, a.size, a.size);
        ctx.restore();
      }

      requestAnimationFrame(frame);
    }
    frame();
  }

  let confettiParticles = [];
  let confettiRunning = false;

  function burstSnowConfetti(count = 46) {
    const canvas = $("confetti-canvas");
    const ctx = canvas.getContext("2d");
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
    const colors = ["#2575fc", "#e14b6a", "#10b981", "#60a5fa", "#f59e0b", "#ffffff"];
    const cx = canvas.width / 2;
    const cy = canvas.height * 0.42;

    for (let i = 0; i < count; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = 4 + Math.random() * 8;
      confettiParticles.push({
        x: cx,
        y: cy,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed - 3.5,
        size: 5 + Math.random() * 5,
        color: colors[i % colors.length],
        rot: Math.random() * Math.PI,
        vrot: (Math.random() - 0.5) * 0.25,
        life: 1,
      });
    }

    if (confettiRunning) return;
    confettiRunning = true;

    function tick() {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      confettiParticles = confettiParticles.filter((p) => p.life > 0.02);
      for (const p of confettiParticles) {
        p.x += p.vx;
        p.y += p.vy;
        p.vy += 0.22;
        p.vx *= 0.985;
        p.rot += p.vrot;
        p.life -= 0.016;

        ctx.save();
        ctx.globalAlpha = Math.max(0, p.life);
        ctx.translate(p.x, p.y);
        ctx.rotate(p.rot);
        ctx.fillStyle = p.color;
        ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.68);
        ctx.restore();
      }
      if (confettiParticles.length > 0) {
        requestAnimationFrame(tick);
      } else {
        confettiRunning = false;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
      }
    }
    tick();
  }

  function resetToHome() {
    if (state.online.eventSource) {
      state.online.eventSource.close();
      state.online.eventSource = null;
    }
    state.online.code = null;
    state.online.playerId = null;
    state.online.roomState = null;
    $("topbar-room-pill").classList.add("hidden");
    showView("home");
  }

  $("brand-home").addEventListener("click", resetToHome);
  $("btn-back-home").addEventListener("click", resetToHome);

  // === SHARED GAME RENDERER ===
  function createPlayerPill(name, playerSlot, cssClass) {
    const span = document.createElement("span");
    span.className = `${cssClass} ${playerSlot}`;
    span.textContent = `${playerSlot === "p1" ? "❄️" : "🧣"} ${name}`;
    return span;
  }

  function countMatches(answers, upToCount) {
    let matches = 0;
    for (let i = 0; i < upToCount; i++) {
      const a = answers[i];
      if (a && a.p1 && a.p2 && a.p1 === a.p2) matches++;
    }
    return matches;
  }

  function renderGameplay({
    index,
    p1Name,
    p2Name,
    p1Locked,
    p2Locked,
    p1Choice,
    p2Choice,
    revealed,
    myChoice,
    canClick,
    centerHint,
    matchesSoFar,
    isNewQuestion,
  }) {
    const q = QUESTIONS[index];
    if (!q) return;

    if (isNewQuestion) {
      const card = $("question-card");
      card.classList.remove("entering");
      void card.offsetWidth;
      card.classList.add("entering");
      // Preload next image smoothly
      if (QUESTIONS[index + 1]) {
        const preload = new Image();
        preload.src = QUESTIONS[index + 1].image;
      }
    }

    $("q-number").textContent = `Câu ${index + 1} / ${TOTAL}`;
    $("q-category").textContent = q.category;
    $("hud-match-count").textContent = String(matchesSoFar);
    $("progress-fill").style.width = `${((index + 1) / TOTAL) * 100}%`;

    $("chip-p1-name").textContent = p1Name;
    $("chip-p2-name").textContent = p2Name;

    const chipP1 = $("chip-p1");
    const chipP2 = $("chip-p2");
    chipP1.classList.toggle("locked", p1Locked);
    chipP2.classList.toggle("locked", p2Locked);
    chipP1.classList.toggle("active-turn", !revealed && !p1Locked && state.online.playerId === "p1");
    chipP2.classList.toggle("active-turn", !revealed && !p2Locked && state.online.playerId === "p2");

    $("chip-p1-state").textContent = revealed
      ? `Chọn ${p1Choice}`
      : p1Locked
      ? "Đã khóa ✓"
      : "Đang chọn...";
    $("chip-p2-state").textContent = revealed
      ? `Chọn ${p2Choice}`
      : p2Locked
      ? "Đã khóa ✓"
      : "Đang chọn...";

    $("turn-center-status").textContent = centerHint;
    $("question-text").textContent = q.text;
    $("question-image").src = q.image;
    $("label-opt-a").textContent = q.optionA;
    $("label-opt-b").textContent = q.optionB;

    const btnA = $("btn-opt-a");
    const btnB = $("btn-opt-b");
    const ovA = $("img-overlay-a");
    const ovB = $("img-overlay-b");
    const imgBadgesA = $("img-badges-a");
    const imgBadgesB = $("img-badges-b");
    const cardBadgesA = $("players-on-a");
    const cardBadgesB = $("players-on-b");
    const centerVs = $("center-vs");
    const revealBanner = $("reveal-banner");

    imgBadgesA.innerHTML = "";
    imgBadgesB.innerHTML = "";
    cardBadgesA.innerHTML = "";
    cardBadgesB.innerHTML = "";

    btnA.disabled = !canClick;
    btnB.disabled = !canClick;

    btnA.classList.remove("my-locked", "chosen-reveal", "dimmed-reveal");
    btnB.classList.remove("my-locked", "chosen-reveal", "dimmed-reveal");
    ovA.classList.remove("picked", "dimmed");
    ovB.classList.remove("picked", "dimmed");
    centerVs.classList.remove("match", "diff");

    if (!revealed) {
      revealBanner.classList.add("hidden");
      centerVs.textContent = "VS";
      if (myChoice === "A") btnA.classList.add("my-locked");
      if (myChoice === "B") btnB.classList.add("my-locked");
      $("next-row").classList.add("hidden");
    } else {
      const aHasP1 = p1Choice === "A";
      const aHasP2 = p2Choice === "A";
      const bHasP1 = p1Choice === "B";
      const bHasP2 = p2Choice === "B";
      const aPicked = aHasP1 || aHasP2;
      const bPicked = bHasP1 || bHasP2;
      const isMatch = p1Choice === p2Choice;

      // Highlight image halves & cards
      ovA.classList.add(aPicked ? "picked" : "dimmed");
      ovB.classList.add(bPicked ? "picked" : "dimmed");
      btnA.classList.add(aPicked ? "chosen-reveal" : "dimmed-reveal");
      btnB.classList.add(bPicked ? "chosen-reveal" : "dimmed-reveal");

      if (aHasP1) {
        imgBadgesA.appendChild(createPlayerPill(p1Name, "p1", "img-player-pill"));
        cardBadgesA.appendChild(createPlayerPill(p1Name, "p1", "card-player-pill"));
      }
      if (aHasP2) {
        imgBadgesA.appendChild(createPlayerPill(p2Name, "p2", "img-player-pill"));
        cardBadgesA.appendChild(createPlayerPill(p2Name, "p2", "card-player-pill"));
      }
      if (bHasP1) {
        imgBadgesB.appendChild(createPlayerPill(p1Name, "p1", "img-player-pill"));
        cardBadgesB.appendChild(createPlayerPill(p1Name, "p1", "card-player-pill"));
      }
      if (bHasP2) {
        imgBadgesB.appendChild(createPlayerPill(p2Name, "p2", "img-player-pill"));
        cardBadgesB.appendChild(createPlayerPill(p2Name, "p2", "card-player-pill"));
      }

      centerVs.textContent = isMatch ? "✨ TRÙNG Ý!" : "❄️ LỆCH PHA";
      centerVs.classList.add(isMatch ? "match" : "diff");

      revealBanner.classList.remove("hidden", "is-match", "is-diff");
      revealBanner.classList.add(isMatch ? "is-match" : "is-diff");
      $("reveal-pill").textContent = isMatch ? "💖 Hai bạn chọn giống hệt nhau!" : "✨ Hai bạn có gu khác nhau rồi!";
      const p1OptText = p1Choice === "A" ? q.optionA : q.optionB;
      const p2OptText = p2Choice === "A" ? q.optionA : q.optionB;
      $("reveal-detail").textContent = isMatch
        ? `Cả ${p1Name} và ${p2Name} đều chọn "${p1OptText}"`
        : `${p1Name} chọn "${p1OptText}" • ${p2Name} chọn "${p2OptText}"`;

      $("next-row").classList.remove("hidden");
      $("btn-next").textContent = index + 1 >= TOTAL ? "Xem kết quả tổng kết →" : "Câu tiếp theo →";
    }
  }

  // === SUMMARY RENDERER ===
  function renderSummary(p1Name, p2Name, answers) {
    showView("summary");
    playSound("victory");
    burstSnowConfetti(60);

    const matches = countMatches(answers, TOTAL);
    const pct = Math.round((matches / TOTAL) * 100);

    $("summary-title").textContent = `${p1Name} & ${p2Name} — Độ Hiểu Nhau`;
    $("summary-percent").textContent = `${pct}%`;
    $("summary-fraction").textContent = `${matches} / ${TOTAL} câu`;

    const circumference = 2 * Math.PI * 58; // ~364.4
    const offset = circumference * (1 - matches / TOTAL);
    const ring = $("score-ring-fg");
    ring.style.strokeDashoffset = String(circumference);
    requestAnimationFrame(() => {
      ring.style.strokeDashoffset = String(offset);
    });

    let badge = "Tri Kỷ Mùa Đông ❄️💖";
    let desc = "Hai bạn hợp gu đến bất ngờ — đi ăn chung hay chọn trái cây chắc chắn không bao giờ cãi nhau!";
    if (pct < 45) {
      badge = "Khác Biệt Thú Vị ☕";
      desc = "Mỗi người một gu riêng biệt! Đi ăn chung tha hồ gọi được gấp đôi món ngon không lo đụng hàng.";
    } else if (pct < 65) {
      badge = "Bù Trừ Hoàn Hảo 🧣";
      desc = "Có những món hợp rơ nhưng vẫn đủ nét riêng để hai bạn khám phá thêm sở thích của nhau!";
    } else if (pct < 85) {
      badge = "Hợp Gu Đáng Gờm ✨";
      desc = "Hai bạn có rất nhiều điểm chung từ món chính tới hoa quả tráng miệng!";
    }
    $("verdict-badge").textContent = badge;
    $("verdict-desc").textContent = desc;

    $("filter-match-btn").textContent = `Trùng ý (${matches})`;
    $("filter-diff-btn").textContent = `Khác biệt (${TOTAL - matches})`;

    state.summaryData = { p1Name, p2Name, answers };
    renderReviewList();
  }

  function renderReviewList() {
    if (!state.summaryData) return;
    const { p1Name, p2Name, answers } = state.summaryData;
    const list = $("review-list");
    list.innerHTML = "";

    QUESTIONS.forEach((q, idx) => {
      const ans = answers[idx] || {};
      const isMatch = ans.p1 && ans.p2 && ans.p1 === ans.p2;
      if (state.reviewFilter === "match" && !isMatch) return;
      if (state.reviewFilter === "diff" && isMatch) return;

      const p1Text = ans.p1 === "A" ? q.optionA : ans.p1 === "B" ? q.optionB : "—";
      const p2Text = ans.p2 === "A" ? q.optionA : ans.p2 === "B" ? q.optionB : "—";

      const row = document.createElement("div");
      row.className = "review-item";
      row.innerHTML = `
        <img class="review-thumb" src="${q.image}" alt="Câu ${idx + 1}" loading="lazy" />
        <div class="review-body">
          <div class="review-q">Câu ${idx + 1}. ${q.text}</div>
          <div class="review-picks">
            <span class="pick-tag">❄️ <strong>${escapeHtml(p1Name)}:</strong> ${escapeHtml(p1Text)}</span>
            <span class="pick-tag">🧣 <strong>${escapeHtml(p2Name)}:</strong> ${escapeHtml(p2Text)}</span>
          </div>
        </div>
        <span class="review-status ${isMatch ? "match" : "diff"}">${isMatch ? "Trùng ý" : "Khác biệt"}</span>
      `;
      list.appendChild(row);
    });
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  document.querySelectorAll(".filter-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      state.reviewFilter = btn.dataset.filter;
      document.querySelectorAll(".filter-btn").forEach((b) => b.classList.toggle("active", b === btn));
      renderReviewList();
    });
  });

  // === ONLINE MODE LOGIC ===
  let lastOnlinePhaseKey = "";

  function showOnlineError(msg) {
    const el = $("online-error");
    el.textContent = msg;
    el.classList.remove("hidden");
  }

  function clearOnlineError() {
    $("online-error").classList.add("hidden");
  }

  async function postJson(url, body) {
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body || {}),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || "Có lỗi kết nối máy chủ");
    return data;
  }

  function connectRoomEvents(code, playerId) {
    if (state.online.eventSource) {
      state.online.eventSource.close();
    }
    const es = new EventSource(`/api/rooms/${code}/events?playerId=${playerId}`);
    state.online.eventSource = es;
    es.onmessage = (ev) => {
      try {
        const roomState = JSON.parse(ev.data);
        applyOnlineRoomState(roomState);
      } catch {}
    };
  }

  function applyOnlineRoomState(rs) {
    state.online.roomState = rs;
    const isHost = state.online.playerId === "p1";
    const p1Name = rs.players.p1 ? rs.players.p1.name : "Người 1";
    const p2Name = rs.players.p2 ? rs.players.p2.name : "Người 2";

    $("topbar-room-pill").classList.remove("hidden");
    $("topbar-room-code").textContent = rs.code;

    if (rs.phase === "lobby") {
      showView("lobby");
      $("lobby-code").textContent = rs.code;
      $("invite-link").value = `${window.location.origin}/?room=${rs.code}`;
      $("lobby-p1-name").textContent = p1Name;

      const hasP2 = Boolean(rs.players.p2);
      $("slot-p2").classList.toggle("filled", hasP2);
      $("lobby-p2-name").textContent = hasP2 ? p2Name : "Đang chờ vào phòng...";
      $("lobby-p2-status").textContent = hasP2 ? "Đã sẵn sàng" : "Đang chờ";
      $("lobby-p2-status").className = `slot-status ${hasP2 ? "ready" : "waiting"}`;

      $("btn-online-start").classList.toggle("hidden", !isHost);
      $("btn-online-start").disabled = !hasP2;
      $("lobby-guest-note").classList.toggle("hidden", isHost);
      lastOnlinePhaseKey = "lobby";
      return;
    }

    if (rs.phase === "summary") {
      if (lastOnlinePhaseKey !== "summary") {
        lastOnlinePhaseKey = "summary";
        renderSummary(p1Name, p2Name, rs.answers);
      }
      return;
    }

    // phase: question | reveal
    showView("game");
    const phaseKey = `${rs.phase}:${rs.currentIndex}`;
    const isNewQ = !lastOnlinePhaseKey.endsWith(`:${rs.currentIndex}`) || lastOnlinePhaseKey === "lobby" || lastOnlinePhaseKey === "summary";
    const justRevealed = rs.phase === "reveal" && lastOnlinePhaseKey !== phaseKey;
    lastOnlinePhaseKey = phaseKey;

    const st = rs.currentStatus;
    const myLocked = isHost ? st.p1Locked : st.p2Locked;
    const myChoice = isHost ? st.p1Choice : st.p2Choice;
    const revealed = rs.phase === "reveal";

    const hint = revealed
      ? "✨ Kết quả"
      : myLocked
      ? "⏳ Đang chờ người kia chọn..."
      : "👉 Chọn A hoặc B của bạn";

    renderGameplay({
      index: rs.currentIndex,
      p1Name,
      p2Name,
      p1Locked: st.p1Locked,
      p2Locked: st.p2Locked,
      p1Choice: st.p1Choice,
      p2Choice: st.p2Choice,
      revealed,
      myChoice,
      canClick: !revealed && !myLocked,
      centerHint: hint,
      matchesSoFar: countMatches(rs.answers, revealed ? rs.currentIndex + 1 : rs.currentIndex),
      isNewQuestion: isNewQ,
    });

    if (justRevealed) {
      if (st.p1Choice === st.p2Choice) {
        playSound("match");
        burstSnowConfetti(34);
      } else {
        playSound("diff");
      }
    }
  }

  $("btn-create-room").addEventListener("click", async () => {
    clearOnlineError();
    try {
      const name = $("online-name").value.trim() || "Minh";
      const res = await postJson("/api/rooms", { playerName: name });
      state.online.code = res.code;
      state.online.playerId = res.playerId;
      lastOnlinePhaseKey = "";
      connectRoomEvents(res.code, res.playerId);
      applyOnlineRoomState(res.state);
    } catch (err) {
      showOnlineError(err.message);
    }
  });

  $("btn-join-room").addEventListener("click", async () => {
    clearOnlineError();
    try {
      const code = $("join-code").value.trim().toUpperCase();
      if (code.length !== 4) {
        return showOnlineError("Mã phòng cần đủ 4 ký tự nhé!");
      }
      const name = $("online-name").value.trim() || "Linh";
      const res = await postJson(`/api/rooms/${code}/join`, { playerName: name });
      state.online.code = res.code;
      state.online.playerId = res.playerId;
      lastOnlinePhaseKey = "";
      connectRoomEvents(res.code, res.playerId);
      applyOnlineRoomState(res.state);
    } catch (err) {
      showOnlineError(err.message);
    }
  });

  $("btn-copy-link").addEventListener("click", async () => {
    const link = $("invite-link").value;
    try {
      await navigator.clipboard.writeText(link);
      $("btn-copy-link").textContent = "Đã chép ✓";
      setTimeout(() => ($("btn-copy-link").textContent = "Sao chép"), 1800);
    } catch {
      $("invite-link").select();
    }
  });

  $("btn-online-start").addEventListener("click", async () => {
    try {
      await postJson(`/api/rooms/${state.online.code}/start`, {});
    } catch (err) {
      alert(err.message);
    }
  });

  // === OPTION CLICK & NEXT CLICK ===
  async function onPick(choice) {
    playSound("lock");
    try {
      await postJson(`/api/rooms/${state.online.code}/answer`, {
        playerId: state.online.playerId,
        choice,
      });
    } catch (err) {
      console.error(err);
    }
  }

  $("btn-opt-a").addEventListener("click", () => onPick("A"));
  $("btn-opt-b").addEventListener("click", () => onPick("B"));

  $("btn-next").addEventListener("click", async () => {
    try {
      await postJson(`/api/rooms/${state.online.code}/next`, {});
    } catch (err) {
      console.error(err);
    }
  });

  $("btn-replay").addEventListener("click", async () => {
    try {
      await postJson(`/api/rooms/${state.online.code}/restart`, {});
    } catch (err) {
      console.error(err);
    }
  });

  // Keyboard shortcuts for snappy play (1/A, 2/B, Enter/Space for Next)
  window.addEventListener("keydown", (e) => {
    if (!views.game.classList.contains("active")) return;
    if (e.target && ["INPUT", "TEXTAREA"].includes(e.target.tagName)) return;
    const k = e.key.toLowerCase();
    if ((k === "a" || k === "1" || k === "arrowleft") && !$("btn-opt-a").disabled) {
      onPick("A");
    } else if ((k === "b" || k === "2" || k === "arrowright") && !$("btn-opt-b").disabled) {
      onPick("B");
    } else if ((k === "enter" || k === " ") && !$("next-row").classList.contains("hidden")) {
      e.preventDefault();
      $("btn-next").click();
    }
  });

  // Auto-fill room code if ?room=XXXX is in URL
  const params = new URLSearchParams(window.location.search);
  const urlRoom = params.get("room");
  if (urlRoom) {
    $("join-code").value = urlRoom.toUpperCase().slice(0, 4);
  }

  initSnowCanvas();
})();

