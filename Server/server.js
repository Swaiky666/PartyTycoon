'use strict';
const WebSocket = require('ws');

const PORT = process.env.PORT || 3000;
const wss  = new WebSocket.Server({ port: PORT });

// State 值与 C# SlotState 枚举一致 (Empty=0, Human=1, AI=2)
const State = { Empty: 0, Human: 1, AI: 2 };

const rooms  = new Map();  // roomCode → room
const wsInfo = new Map();  // ws → { playerId, roomCode }
let nextPlayerId = 1;

// ── 工具函数 ─────────────────────────────────────────────────

function genCode() {
    const ch = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    let code;
    do { code = Array.from({length:6}, () => ch[Math.floor(Math.random()*ch.length)]).join(''); }
    while (rooms.has(code));
    return code;
}

function send(ws, obj) {
    if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
}

function broadcastRoom(room, obj, excludeWs = null) {
    const str = JSON.stringify(obj);
    for (const ws of room.clients.values())
        if (ws !== excludeWs && ws.readyState === WebSocket.OPEN) ws.send(str);
}

function makeSlot(i) {
    return { slotIndex: i, state: State.Empty, playerId: -1, playerName: `玩家${i+1}`, colorIndex: i, isReady: false, isHost: false };
}

function firstEmptySlot(room) {
    return room.slots.findIndex(s => s.state === State.Empty);
}

function firstFreeColor(room, excludeSlot = -1) {
    const taken = new Set(
        room.slots.filter((s, i) => i !== excludeSlot && s.state !== State.Empty).map(s => s.colorIndex)
    );
    for (let i = 0; i < 12; i++) if (!taken.has(i)) return i;
    return 0;
}

function slots(room) {
    return room.slots.map(s => ({ ...s }));
}

// ── 房间操作 ──────────────────────────────────────────────────

function handleCreateRoom(ws, playerName) {
    const code = genCode();
    const pid  = nextPlayerId++;
    const room = {
        code,
        slots: Array.from({ length: 6 }, (_, i) => makeSlot(i)),
        roundCount: 5,
        clients: new Map(),
        started: false,
    };
    room.slots[0] = { slotIndex: 0, state: State.Human, playerId: pid, playerName: playerName || '玩家1', colorIndex: 0, isReady: true, isHost: true };
    room.clients.set(pid, ws);
    rooms.set(code, room);
    wsInfo.set(ws, { playerId: pid, roomCode: code });

    send(ws, { cmd: 'ROOM_CREATED', roomCode: code, playerId: pid, slotIndex: 0, slots: slots(room), roundCount: room.roundCount });
    console.log(`[Room] Created ${code} by player ${pid} (${playerName})`);
}

function handleJoinRoom(ws, roomCode, playerName) {
    roomCode = (roomCode || '').toUpperCase();
    const room = rooms.get(roomCode);
    if (!room)         { send(ws, { cmd: 'JOIN_FAILED', reason: '房间不存在' }); return; }
    if (room.started)  { send(ws, { cmd: 'JOIN_FAILED', reason: '游戏已开始' }); return; }
    const idx = firstEmptySlot(room);
    if (idx < 0)       { send(ws, { cmd: 'JOIN_FAILED', reason: '房间已满'   }); return; }

    const pid   = nextPlayerId++;
    const color = firstFreeColor(room);
    room.slots[idx] = { slotIndex: idx, state: State.Human, playerId: pid, playerName: playerName || `玩家${idx+1}`, colorIndex: color, isReady: false, isHost: false };
    room.clients.set(pid, ws);
    wsInfo.set(ws, { playerId: pid, roomCode });

    send(ws, { cmd: 'JOIN_SUCCESS', roomCode, playerId: pid, slotIndex: idx, slots: slots(room), roundCount: room.roundCount });
    broadcastRoom(room, { cmd: 'PLAYER_JOINED', slots: slots(room) }, ws);
    console.log(`[Room] ${roomCode}: player ${pid} joined slot ${idx}`);
}

function handleLeave(ws, info) {
    const room = rooms.get(info.roomCode);
    if (!room) return;
    const slot = room.slots.find(s => s.playerId === info.playerId);
    if (!slot) return;

    const wasHost = slot.isHost;
    const idx = slot.slotIndex;
    Object.assign(room.slots[idx], makeSlot(idx));
    room.clients.delete(info.playerId);
    wsInfo.delete(ws);

    if (wasHost) {
        broadcastRoom(room, { cmd: 'ROOM_DISBANDED' });
        rooms.delete(info.roomCode);
        console.log(`[Room] ${info.roomCode} disbanded`);
    } else {
        broadcastRoom(room, { cmd: 'PLAYER_LEFT', slotIndex: idx, slots: slots(room) });
        if (room.clients.size === 0) rooms.delete(info.roomCode);
    }
}

// ── 消息路由 ──────────────────────────────────────────────────

function handleMessage(ws, msg) {
    const info = wsInfo.get(ws);
    const room = info?.roomCode ? rooms.get(info.roomCode) : null;

    switch (msg.cmd) {
        case 'CREATE_ROOM': handleCreateRoom(ws, msg.playerName); break;
        case 'JOIN_ROOM':   handleJoinRoom(ws, msg.roomCode, msg.playerName); break;
        case 'LEAVE_ROOM':  if (info) handleLeave(ws, info); break;

        case 'UPDATE_COLOR': {
            if (!room || !info) break;
            const slot = room.slots.find(s => s.playerId === info.playerId);
            if (!slot) break;
            const taken = new Set(room.slots.filter(s => s.playerId !== info.playerId && s.state !== State.Empty).map(s => s.colorIndex));
            if (taken.has(msg.colorIndex)) break;
            slot.colorIndex = msg.colorIndex;
            broadcastRoom(room, { cmd: 'COLOR_UPDATED', playerId: info.playerId, colorIndex: msg.colorIndex });
            break;
        }

        case 'UPDATE_NAME': {
            if (!room || !info) break;
            const slot = room.slots.find(s => s.playerId === info.playerId);
            if (!slot) break;
            slot.playerName = (msg.name || '').trim().slice(0, 12) || slot.playerName;
            broadcastRoom(room, { cmd: 'NAME_UPDATED', playerId: info.playerId, name: slot.playerName });
            break;
        }

        case 'TOGGLE_READY': {
            if (!room || !info) break;
            const slot = room.slots.find(s => s.playerId === info.playerId);
            if (!slot || slot.isHost) break;
            slot.isReady = !slot.isReady;
            broadcastRoom(room, { cmd: 'READY_UPDATED', playerId: info.playerId, isReady: slot.isReady });
            break;
        }

        case 'ADD_AI': {
            if (!room || !info) break;
            if (!room.slots.find(s => s.playerId === info.playerId)?.isHost) break;
            const { slotIndex } = msg;
            if (slotIndex < 0 || slotIndex >= 6 || room.slots[slotIndex].state !== State.Empty) break;
            const color = firstFreeColor(room);
            room.slots[slotIndex] = { slotIndex, state: State.AI, playerId: -(slotIndex+1), playerName: `AI ${slotIndex+1}`, colorIndex: color, isReady: true, isHost: false };
            broadcastRoom(room, { cmd: 'AI_ADDED', slots: slots(room) });
            break;
        }

        case 'KICK_PLAYER': {
            if (!room || !info) break;
            if (!room.slots.find(s => s.playerId === info.playerId)?.isHost) break;
            const { slotIndex } = msg;
            const target = room.slots[slotIndex];
            if (!target || target.state === State.Empty) break;
            const targetWs = room.clients.get(target.playerId);
            if (targetWs) { send(targetWs, { cmd: 'KICKED' }); room.clients.delete(target.playerId); wsInfo.delete(targetWs); }
            Object.assign(room.slots[slotIndex], makeSlot(slotIndex));
            broadcastRoom(room, { cmd: 'PLAYER_KICKED', slotIndex, slots: slots(room) });
            break;
        }

        case 'SET_ROUNDS': {
            if (!room || !info) break;
            if (!room.slots.find(s => s.playerId === info.playerId)?.isHost) break;
            room.roundCount = Math.max(3, Math.min(20, msg.count | 0));
            broadcastRoom(room, { cmd: 'ROUNDS_SET', count: room.roundCount });
            break;
        }

        case 'START_GAME': {
            if (!room || !info) break;
            if (!room.slots.find(s => s.playerId === info.playerId)?.isHost) break;
            const notReady = room.slots.some(s => s.state === State.Human && !s.isHost && !s.isReady);
            const hasParticipant = room.slots.some(s => !s.isHost && s.state !== State.Empty);
            if (notReady || !hasParticipant) break;
            room.started = true;
            broadcastRoom(room, { cmd: 'GAME_STARTED', slots: slots(room), roundCount: room.roundCount });
            console.log(`[Room] ${info.roomCode} game started`);
            break;
        }

        default:
            console.log('[WS] Unknown cmd:', msg.cmd);
    }
}

// ── WebSocket 服务 ────────────────────────────────────────────

wss.on('connection', (ws) => {
    console.log('[WS] Client connected');
    ws.on('message', (raw) => {
        try { handleMessage(ws, JSON.parse(raw.toString())); }
        catch (e) { console.error('[WS] Message error:', e.message); }
    });
    ws.on('close', () => {
        const info = wsInfo.get(ws);
        if (info?.roomCode) handleLeave(ws, info);
        wsInfo.delete(ws);
        console.log('[WS] Client disconnected');
    });
    ws.on('error', (err) => console.error('[WS] Socket error:', err.message));
});

console.log(`[Server] PartyTycoon server on port ${PORT}`);
