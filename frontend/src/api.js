const API_BASE = 'http://localhost:5000/api';

export async function sendMessage(userId, message, sessionId) {
    const res = await fetch(`${API_BASE}/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, message, sessionId }),
    });
    if (!res.ok) throw new Error(`API error: ${res.status}`);
    return res.json();
}

export async function approveAction(sessionId) {
    const res = await fetch(`${API_BASE}/chat/approve/${sessionId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
    });
    if (!res.ok) throw new Error(`API error: ${res.status}`);
    return res.json();
}

export async function rejectAction(sessionId) {
    const res = await fetch(`${API_BASE}/chat/reject/${sessionId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
    });
    if (!res.ok) throw new Error(`API error: ${res.status}`);
    return res.json();
}

export async function submitFeedback(sessionId, isPositive, comment) {
    const res = await fetch(`${API_BASE}/chat/feedback`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessionId, isPositive, comment }),
    });
    if (!res.ok) throw new Error(`API error: ${res.status}`);
    return res.json();
}
