const http = require("http");
const fs = require("fs");
const path = require("path");
const { URL } = require("url");

const PORT = Number(process.env.PORT) || 3000;
const PUBLIC_DIR = path.join(__dirname, "public");
const IMAGES_DIR = path.join(__dirname, "kahoot_images");
const TOTAL_QUESTIONS = 30;

const MIME_TYPES = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
  ".mp3": "audio/mpeg",
};

const rooms = new Map();

function generateRoomCode() {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 4; i++) {
    code += chars[Math.floor(Math.random() * chars.length)];
  }
  return rooms.has(code) ? generateRoomCode() : code;
}

function sendJson(res, status, data) {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  res.end(body);
}

function readJsonBody(req) {
  return new Promise((resolve, reject) => {
    let raw = "";
    req.on("data", (chunk) => {
      raw += chunk;
      if (raw.length > 64 * 1024) {
        reject(new Error("Payload too large"));
        req.destroy();
      }
    });
    req.on("end", () => {
      try {
        resolve(raw ? JSON.parse(raw) : {});
      } catch (err) {
        reject(err);
      }
    });
    req.on("error", reject);
  });
}

function serializeRoomFor(room, viewerId) {
  const idx = room.currentIndex;
  const currentAns = room.answers[idx] || { p1: null, p2: null };
  const isRevealed = room.phase === "reveal" || room.phase === "summary";

  return {
    code: room.code,
    phase: room.phase,
    currentIndex: idx,
    totalQuestions: TOTAL_QUESTIONS,
    players: {
      p1: room.players.p1 ? { name: room.players.p1.name } : null,
      p2: room.players.p2 ? { name: room.players.p2.name } : null,
    },
    currentStatus: {
      p1Locked: Boolean(currentAns.p1),
      p2Locked: Boolean(currentAns.p2),
      p1Choice: isRevealed || viewerId === "p1" ? currentAns.p1 : null,
      p2Choice: isRevealed || viewerId === "p2" ? currentAns.p2 : null,
    },
    answers: isRevealed
      ? room.answers.map((a, i) =>
          i < idx || (i === idx && room.phase === "reveal") || room.phase === "summary"
            ? a
            : { p1: null, p2: null }
        )
      : room.answers.map((a, i) => (i < idx ? a : { p1: null, p2: null })),
  };
}

function broadcastRoom(room) {
  room.updatedAt = Date.now();
  for (const client of room.clients) {
    const payload = `data: ${JSON.stringify(serializeRoomFor(room, client.playerId))}\n\n`;
    try {
      client.res.write(payload);
    } catch {
      room.clients.delete(client);
    }
  }
}

function createEmptyAnswers() {
  return Array.from({ length: TOTAL_QUESTIONS }, () => ({ p1: null, p2: null }));
}

function serveStaticFile(req, res, baseDir, relPath) {
  const safePath = path.normalize(relPath).replace(/^(\.\.[/\\])+/, "");
  const fullPath = path.join(baseDir, safePath);
  if (!fullPath.startsWith(baseDir)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }
  fs.stat(fullPath, (err, stat) => {
    if (err || !stat.isFile()) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }
    const ext = path.extname(fullPath).toLowerCase();
    const contentType = MIME_TYPES[ext] || "application/octet-stream";
    const cacheControl = ext === ".jpg" || ext === ".mp3" ? "public, max-age=86400" : "no-cache";
    const range = req.headers.range;

    if (range) {
      const parts = range.replace(/bytes=/, "").split("-");
      const start = parseInt(parts[0], 10);
      const end = parts[1] ? parseInt(parts[1], 10) : stat.size - 1;
      if (Number.isNaN(start) || start >= stat.size || end >= stat.size || start > end) {
        res.writeHead(416, { "Content-Range": `bytes */${stat.size}` });
        res.end();
        return;
      }
      res.writeHead(206, {
        "Content-Range": `bytes ${start}-${end}/${stat.size}`,
        "Accept-Ranges": "bytes",
        "Content-Length": end - start + 1,
        "Content-Type": contentType,
        "Cache-Control": cacheControl,
      });
      fs.createReadStream(fullPath, { start, end }).pipe(res);
      return;
    }

    res.writeHead(200, {
      "Content-Type": contentType,
      "Content-Length": stat.size,
      "Accept-Ranges": "bytes",
      "Cache-Control": cacheControl,
    });
    fs.createReadStream(fullPath).pipe(res);
  });
}

// Cleanup stale rooms every 30 minutes
setInterval(() => {
  const cutoff = Date.now() - 6 * 60 * 60 * 1000;
  for (const [code, room] of rooms.entries()) {
    if (room.updatedAt < cutoff && room.clients.size === 0) {
      rooms.delete(code);
    }
  }
}, 30 * 60 * 1000);

const server = http.createServer(async (req, res) => {
  const reqUrl = new URL(req.url, `http://${req.headers.host || "localhost"}`);
  const pathname = reqUrl.pathname;

  // API routes
  if (pathname === "/api/rooms" && req.method === "POST") {
    try {
      const body = await readJsonBody(req);
      const name = String(body.playerName || "Người chơi 1").trim().slice(0, 24) || "Người chơi 1";
      const code = generateRoomCode();
      const room = {
        code,
        phase: "lobby",
        currentIndex: 0,
        players: { p1: { name }, p2: null },
        answers: createEmptyAnswers(),
        clients: new Set(),
        updatedAt: Date.now(),
      };
      rooms.set(code, room);
      return sendJson(res, 201, { code, playerId: "p1", state: serializeRoomFor(room, "p1") });
    } catch {
      return sendJson(res, 400, { error: "Yêu cầu không hợp lệ" });
    }
  }

  const roomMatch = pathname.match(/^\/api\/rooms\/([A-Za-z0-9]{4})(?:\/([a-z]+))?$/);
  if (roomMatch) {
    const code = roomMatch[1].toUpperCase();
    const action = roomMatch[2] || "";
    const room = rooms.get(code);
    if (!room) {
      return sendJson(res, 404, { error: "Không tìm thấy phòng này. Kiểm tra lại mã phòng nhé!" });
    }

    if (action === "events" && req.method === "GET") {
      const viewerId = reqUrl.searchParams.get("playerId") === "p2" ? "p2" : "p1";
      res.writeHead(200, {
        "Content-Type": "text/event-stream; charset=utf-8",
        "Cache-Control": "no-cache, no-transform",
        Connection: "keep-alive",
      });
      const client = { res, playerId: viewerId };
      room.clients.add(client);
      res.write(`data: ${JSON.stringify(serializeRoomFor(room, viewerId))}\n\n`);

      const ping = setInterval(() => {
        try {
          res.write(": ping\n\n");
        } catch {
          clearInterval(ping);
        }
      }, 20000);

      req.on("close", () => {
        clearInterval(ping);
        room.clients.delete(client);
      });
      return;
    }

    try {
      const body = await readJsonBody(req);

      if (action === "join" && req.method === "POST") {
        const name = String(body.playerName || "Người chơi 2").trim().slice(0, 24) || "Người chơi 2";
        if (!room.players.p2) {
          room.players.p2 = { name };
        } else if (room.players.p2.name === name) {
          // Rejoin as p2
        } else if (room.players.p1 && room.players.p1.name === name) {
          return sendJson(res, 200, { code, playerId: "p1", state: serializeRoomFor(room, "p1") });
        } else {
          room.players.p2 = { name };
        }
        broadcastRoom(room);
        return sendJson(res, 200, { code, playerId: "p2", state: serializeRoomFor(room, "p2") });
      }

      if (action === "start" && req.method === "POST") {
        if (!room.players.p2) {
          return sendJson(res, 400, { error: "Đang chờ người chơi thứ 2 vào phòng!" });
        }
        room.phase = "question";
        room.currentIndex = 0;
        room.answers = createEmptyAnswers();
        broadcastRoom(room);
        return sendJson(res, 200, { ok: true });
      }

      if (action === "answer" && req.method === "POST") {
        const playerId = body.playerId === "p2" ? "p2" : "p1";
        const choice = body.choice === "B" ? "B" : body.choice === "A" ? "A" : null;
        if (!choice || room.phase !== "question") {
          return sendJson(res, 400, { error: "Không thể gửi đáp án lúc này" });
        }
        const entry = room.answers[room.currentIndex];
        entry[playerId] = choice;
        if (entry.p1 && entry.p2) {
          room.phase = "reveal";
        }
        broadcastRoom(room);
        return sendJson(res, 200, { ok: true });
      }

      if (action === "next" && req.method === "POST") {
        if (room.phase !== "reveal") {
          return sendJson(res, 400, { error: "Chưa mở kết quả câu hiện tại" });
        }
        if (room.currentIndex + 1 >= TOTAL_QUESTIONS) {
          room.phase = "summary";
        } else {
          room.currentIndex += 1;
          room.phase = "question";
        }
        broadcastRoom(room);
        return sendJson(res, 200, { ok: true });
      }

      if (action === "restart" && req.method === "POST") {
        room.phase = "question";
        room.currentIndex = 0;
        room.answers = createEmptyAnswers();
        broadcastRoom(room);
        return sendJson(res, 200, { ok: true });
      }

      return sendJson(res, 404, { error: "API không tồn tại" });
    } catch {
      return sendJson(res, 400, { error: "Dữ liệu không hợp lệ" });
    }
  }

  // Static images from kahoot_images
  if (pathname.startsWith("/images/")) {
    const fileName = decodeURIComponent(pathname.slice("/images/".length));
    return serveStaticFile(req, res, IMAGES_DIR, fileName);
  }

  // Static public files
  const rel = pathname === "/" ? "index.html" : decodeURIComponent(pathname.slice(1));
  return serveStaticFile(req, res, PUBLIC_DIR, rel);
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(`Winter Quiz server running at http://localhost:${PORT}`);
});

