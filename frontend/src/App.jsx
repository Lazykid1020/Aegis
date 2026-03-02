import { useState, useRef, useEffect } from 'react';
import { sendMessage, approveAction, rejectAction } from './api';

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
      const data = await sendMessage('demo-user', msg, 'rag-session');
      const assistantMsg = {
        role: 'assistant',
        content: data.message,
        actionTaken: data.actionTaken,
        requiresApproval: data.requiresApproval,
        proposedTicket: data.proposedTicket,
        time: formatTime()
      };
      setMessages(prev => [...prev, assistantMsg]);
    } catch (err) {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: `❌ Error connecting to Aegis backend: ${err.message}.`,
        time: formatTime()
      }]);
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async () => {
    setLoading(true);
    try {
      const data = await approveAction('rag-session');
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: `✔️ **Ticket Created Successfully.**\n\n${data.message}`,
        actionTaken: data.actionTaken,
        time: formatTime()
      }]);
    } catch (err) {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: `❌ Error creating ticket: ${err.message}`,
        time: formatTime()
      }]);
    } finally {
      setLoading(false);
    }
  };

  const handleReject = async () => {
    setLoading(true);
    try {
      const data = await rejectAction('rag-session');
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: data.message,
        actionTaken: data.actionTaken,
        time: formatTime()
      }]);
    } catch (err) {
      console.error(err);
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
            <span>Aegis Agent • Active</span>
          </div>
        </div>
      </aside>

      {/* ── Main Content ── */}
      <main className="main-content">
        <header className="header">
          <div className="header-left">
            <span className="page-title">IT Support Chat</span>
          </div>
          <div className="header-right">
            <span className="model-badge">gpt-5-mini</span>
          </div>
        </header>

        <div className="chat-area">
          <div className="chat-container">
            {messages.length === 0 && !loading ? (
              <div className="empty-state">
                <div className="empty-icon">🛡️</div>
                <h2>How can Aegis help?</h2>
                <p>Describe your IT issue and I'll help troubleshoot it. If I can't resolve it, I'll help you raise a support ticket.</p>
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
                          .replace(/\*(.*?)\*/g, '<em>$1</em>')
                          .replace(/\n/g, '<br/>')
                      }} />

                      {/* Ticket Preview Card */}
                      {msg.role === 'assistant' && msg.proposedTicket && (
                        <div className="ticket-preview">
                          <div className="ticket-preview-header">📋 Proposed Ticket</div>
                          <div className="ticket-preview-body">
                            <div className="ticket-field">
                              <span className="ticket-label">Title</span>
                              <span className="ticket-value">{msg.proposedTicket.title}</span>
                            </div>
                            <div className="ticket-field">
                              <span className="ticket-label">Category</span>
                              <span className="ticket-value">{msg.proposedTicket.category}</span>
                            </div>
                            <div className="ticket-field">
                              <span className="ticket-label">Urgency</span>
                              <span className={`ticket-urgency urgency-${msg.proposedTicket.urgency?.toLowerCase()}`}>
                                {msg.proposedTicket.urgency}
                              </span>
                            </div>
                            <div className="ticket-field full-width">
                              <span className="ticket-label">Description</span>
                              <span className="ticket-value">{msg.proposedTicket.description}</span>
                            </div>
                          </div>
                        </div>
                      )}

                      {/* HITL Approval Buttons */}
                      {msg.role === 'assistant' && msg.requiresApproval && (
                        <div className="hitl-actions">
                          <button className="btn btn-approve" onClick={handleApprove} disabled={loading}>
                            ✓ Approve & Create Ticket
                          </button>
                          <button className="btn btn-reject" onClick={handleReject} disabled={loading}>
                            ✕ No, don't create
                          </button>
                        </div>
                      )}
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
