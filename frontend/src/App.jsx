import { useState, useRef, useEffect } from 'react';
import { sendMessage } from './api';

function formatTime() {
  return new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}

export default function App() {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState('chat');
  const chatEndRef = useRef(null);
  const inputRef = useRef(null);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, loading]);

  const handleSend = async (text) => {
    const msg = text || input.trim();
    if (!msg || loading) return;
    setInput('');

    const userMsg = { role: 'user', content: msg, time: formatTime() };
    setMessages(prev => [...prev, userMsg]);
    setLoading(true);

    try {
      const data = await sendMessage('demo-user', msg, 'mvp-session');
      const assistantMsg = {
        role: 'assistant',
        content: data.message,
        time: formatTime()
      };
      setMessages(prev => [...prev, assistantMsg]);
    } catch (err) {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: `❌ Error connecting to Aegis backend: ${err.message}.`,
        time: formatTime(),
      }]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const quickActions = [
    { icon: '🔑', text: 'My password expired, please reset it' },
    { icon: '🛡️', text: 'Grant admin access to production VPN' },
    { icon: '🖥️', text: 'The production server crashed and is unresponsive' },
    { icon: '📖', text: 'How do I connect to the guest WiFi?' }
  ];

  return (
    <div className="app">
      {/* ── Sidebar ── */}
      <aside className="sidebar">
        <div className="sidebar-brand">
          <div className="brand-icon">A</div>
          <div className="brand-text">
            <h1>Aegis</h1>
            <span>AI Issue Management</span>
          </div>
        </div>
        <nav className="sidebar-nav">
          <div className={`nav-item ${currentPage === 'chat' ? 'active' : ''}`} onClick={() => setCurrentPage('chat')}>
            <span className="nav-icon">💬</span> Chat
          </div>
          <div className={`nav-item ${currentPage === 'tickets' ? 'active' : ''}`} onClick={() => setCurrentPage('tickets')}>
            <span className="nav-icon">📋</span> Tickets
          </div>
          <div className={`nav-item ${currentPage === 'analytics' ? 'active' : ''}`} onClick={() => setCurrentPage('analytics')}>
            <span className="nav-icon">📊</span> Analytics
          </div>
        </nav>
        <div className="sidebar-footer">
          <div className="status-indicator">
            <div className="status-dot" />
            <span>MVP Agent • Active</span>
          </div>
        </div>
      </aside>

      {/* ── Main Content ── */}
      <main className="main-content">
        <header className="header">
          <div className="header-left">
            <span className="page-title">Issue Resolution Chat (MVP)</span>
          </div>
          <div className="header-right">
            <span className="model-badge">gemini-2.5-flash</span>
          </div>
        </header>

        <div className="chat-area">
          <div className="chat-container">
            {messages.length === 0 && !loading ? (
              <div className="empty-state">
                <div className="empty-icon">🛡️</div>
                <h2>How can Aegis help?</h2>
                <p>Describe your IT issue. I am connected to the Vector DB and can answer questions or create tickets for you.</p>
                <div className="quick-actions">
                  {quickActions.map((qa, i) => (
                    <div key={i} className="quick-action" onClick={() => handleSend(qa.text)}>
                      <div className="qa-icon">{qa.icon}</div>
                      <div className="qa-text">{qa.text}</div>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <>
                {messages.map((msg, idx) => (
                  <div key={idx} className={`message ${msg.role}`}>
                    <div className="message-avatar">
                      {msg.role === 'user' ? 'U' : 'A'}
                    </div>
                    <div className="message-body">
                      <div className="message-sender">
                        {msg.role === 'user' ? 'You' : 'Aegis'}
                        <span className="timestamp">{msg.time}</span>
                      </div>
                      <div className="message-content" dangerouslySetInnerHTML={{
                        __html: msg.content
                          ?.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
                          .replace(/\n/g, '<br/>')
                      }} />
                    </div>
                  </div>
                ))}

                {/* Typing Indicator */}
                {loading && (
                  <div className="typing-indicator">
                    <div className="typing-dots">
                      <span /><span /><span />
                    </div>
                  </div>
                )}
              </>
            )}
            <div ref={chatEndRef} />
          </div>
        </div>

        {/* ── Input ── */}
        <div className="input-area">
          <div className="input-container">
            <div className="input-wrapper">
              <textarea
                ref={inputRef}
                className="chat-input"
                placeholder="Describe your issue..."
                value={input}
                onChange={e => setInput(e.target.value)}
                onKeyDown={handleKeyDown}
                rows={1}
                disabled={loading}
              />
            </div>
            <button className="send-btn" onClick={() => handleSend()} disabled={!input.trim() || loading}>
              ➤
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}
