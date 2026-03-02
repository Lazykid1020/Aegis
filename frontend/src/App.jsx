import { useState, useRef, useEffect } from 'react';
import { sendMessage, approveAction, rejectAction, submitFeedback } from './api';

function getBarColor(score) {
  if (score >= 70) return 'bar-green';
  if (score >= 40) return 'bar-amber';
  return 'bar-red';
}

function getTierClass(tier) {
  if (tier?.includes('1')) return 'tier-1';
  if (tier?.includes('2')) return 'tier-2';
  return 'tier-3';
}

function getTierLabel(tier) {
  if (tier?.includes('1')) return 'AUTO-REMEDIATE';
  if (tier?.includes('2')) return 'HUMAN-IN-THE-LOOP';
  return 'ESCALATE';
}

function getActionTag(action) {
  switch (action) {
    case 'gathering_info': return { cls: 'action-info', icon: '🔍', text: 'GATHERING INFO' };
    case 'auto_remediated': return { cls: 'action-auto', icon: '⚡', text: 'AUTO-REMEDIATED' };
    case 'awaiting_approval': return { cls: 'action-approval', icon: '🔒', text: 'AWAITING APPROVAL' };
    case 'ticket_created': return { cls: 'action-ticket', icon: '📋', text: 'TICKET CREATED' };
    default: return null;
  }
}

function getRiskClass(level) {
  if (level === 'Low') return 'risk-low';
  if (level === 'Medium') return 'risk-medium';
  return 'risk-high';
}

function formatTime() {
  return new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}

// ── Confidence Card Component ──
function ConfidenceCard({ confidence }) {
  if (!confidence) return null;
  return (
    <div className="intel-card">
      <div className="intel-card-header">Confidence Analysis</div>
      <div className="confidence-bar-container">
        <div>
          <div className="confidence-row">
            <span className="label">Similarity</span>
            <span className="value" style={{ color: confidence.similarityScore >= 70 ? 'var(--accent-cyan)' : 'var(--accent-amber)' }}>{confidence.similarityScore}%</span>
          </div>
          <div className="confidence-bar">
            <div className={`confidence-bar-fill ${getBarColor(confidence.similarityScore)}`} style={{ width: `${confidence.similarityScore}%` }} />
          </div>
        </div>
        <div>
          <div className="confidence-row">
            <span className="label">Historical</span>
            <span className="value" style={{ color: confidence.historicalSuccessRate >= 70 ? 'var(--accent-cyan)' : 'var(--accent-amber)' }}>{confidence.historicalSuccessRate}%</span>
          </div>
          <div className="confidence-bar">
            <div className={`confidence-bar-fill ${getBarColor(confidence.historicalSuccessRate)}`} style={{ width: `${confidence.historicalSuccessRate}%` }} />
          </div>
        </div>
        <div>
          <div className="confidence-row">
            <span className="label">LLM Certainty</span>
            <span className="value" style={{ color: confidence.llmCertainty >= 70 ? 'var(--accent-cyan)' : 'var(--accent-amber)' }}>{confidence.llmCertainty}%</span>
          </div>
          <div className="confidence-bar">
            <div className={`confidence-bar-fill ${getBarColor(confidence.llmCertainty)}`} style={{ width: `${confidence.llmCertainty}%` }} />
          </div>
        </div>
      </div>
      <div className="final-score">
        <span className="score-value" style={{ color: confidence.finalScore >= 80 ? 'var(--accent-cyan)' : confidence.finalScore >= 50 ? 'var(--accent-amber)' : 'var(--accent-red)' }}>{confidence.finalScore}%</span>
        <span className="score-label">FINAL</span>
        <span className={`tier-badge ${getTierClass(confidence.tier)}`}>{getTierLabel(confidence.tier)}</span>
      </div>
    </div>
  );
}

// ── Risk Card Component ──
function RiskCard({ risk }) {
  if (!risk) return null;
  return (
    <div className="intel-card">
      <div className="intel-card-header">Risk Assessment</div>
      <div className="risk-display">
        <div className={`risk-level-big ${getRiskClass(risk.riskLevel)}`}>
          {risk.riskLevel} {risk.isBlocked && '⛔'}
        </div>
        {risk.riskScore > 0 && (
          <div className="confidence-row">
            <span className="label">Risk Score</span>
            <span className="value" style={{ color: risk.riskScore >= 80 ? 'var(--accent-red)' : 'var(--accent-amber)' }}>{risk.riskScore}/100</span>
          </div>
        )}
        <div className="risk-reason">{risk.reasoning}</div>
        {risk.policyViolation && (
          <div className="policy-violation">
            ⚠ POLICY: {risk.policyViolation}
          </div>
        )}
      </div>
    </div>
  );
}

// ── Main App ──
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
      const data = await sendMessage('demo-user', msg);
      const assistantMsg = {
        role: 'assistant',
        content: data.message,
        time: formatTime(),
        confidence: data.confidence,
        risk: data.risk,
        actionTaken: data.actionTaken,
        ticketId: data.ticketId,
        requiresApproval: data.requiresApproval,
        sessionId: data.sessionId,
        feedbackGiven: null,
      };
      setMessages(prev => [...prev, assistantMsg]);
    } catch (err) {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: `❌ Error connecting to Aegis backend: ${err.message}. Make sure the server is running on localhost:5000.`,
        time: formatTime(),
      }]);
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (sessionId, idx) => {
    setLoading(true);
    try {
      const data = await approveAction(sessionId);
      setMessages(prev => {
        const updated = [...prev];
        updated[idx] = { ...updated[idx], requiresApproval: false, actionTaken: 'approved' };
        return [...updated, {
          role: 'assistant',
          content: data.message,
          time: formatTime(),
          actionTaken: data.actionTaken,
        }];
      });
    } catch (err) {
      setMessages(prev => [...prev, { role: 'assistant', content: `❌ Approval failed: ${err.message}`, time: formatTime() }]);
    } finally {
      setLoading(false);
    }
  };

  const handleReject = async (sessionId, idx) => {
    setLoading(true);
    try {
      const data = await rejectAction(sessionId);
      setMessages(prev => {
        const updated = [...prev];
        updated[idx] = { ...updated[idx], requiresApproval: false, actionTaken: 'rejected' };
        return [...updated, {
          role: 'assistant',
          content: data.message,
          time: formatTime(),
          actionTaken: 'ticket_created',
        }];
      });
    } catch (err) {
      setMessages(prev => [...prev, { role: 'assistant', content: `❌ Rejection failed: ${err.message}`, time: formatTime() }]);
    } finally {
      setLoading(false);
    }
  };

  const handleFeedback = async (sessionId, isPositive, idx) => {
    try {
      await submitFeedback(sessionId, isPositive);
      setMessages(prev => {
        const updated = [...prev];
        updated[idx] = { ...updated[idx], feedbackGiven: isPositive ? 'positive' : 'negative' };
        return updated;
      });
    } catch (err) {
      console.error('Feedback error:', err);
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
    { icon: '🔒', text: 'My account got locked after too many attempts' },
    { icon: '🛡️', text: 'Grant admin access to production VPN' },
    { icon: '🖥️', text: 'The production server crashed and is unresponsive' },
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
          <div className={`nav-item ${currentPage === 'policies' ? 'active' : ''}`} onClick={() => setCurrentPage('policies')}>
            <span className="nav-icon">🛡️</span> Policies
          </div>
        </nav>
        <div className="sidebar-footer">
          <div className="status-indicator">
            <div className="status-dot" />
            <span>Gemini 2.5 Flash • Active</span>
          </div>
        </div>
      </aside>

      {/* ── Main Content ── */}
      <main className="main-content">
        <header className="header">
          <div className="header-left">
            <span className="page-title">Issue Resolution Chat</span>
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
                <p>Describe your IT issue in natural language. I'll analyze it, assess risk, and either resolve it automatically or route it to the right team.</p>
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

                      {/* Intel Cards */}
                      {(msg.confidence || msg.risk) && (
                        <div className="intel-cards">
                          <ConfidenceCard confidence={msg.confidence} />
                          <RiskCard risk={msg.risk} />
                        </div>
                      )}

                      {/* Action Tag */}
                      {msg.actionTaken && getActionTag(msg.actionTaken) && (
                        <div className={`action-tag ${getActionTag(msg.actionTaken).cls}`}>
                          {getActionTag(msg.actionTaken).icon} {getActionTag(msg.actionTaken).text}
                          {msg.ticketId && <span> • {msg.ticketId}</span>}
                        </div>
                      )}

                      {/* HITL Approve/Reject */}
                      {msg.requiresApproval && (
                        <div className="hitl-actions">
                          <button className="btn btn-approve" onClick={() => handleApprove(msg.sessionId, idx)} disabled={loading}>
                            ✓ Approve
                          </button>
                          <button className="btn btn-reject" onClick={() => handleReject(msg.sessionId, idx)} disabled={loading}>
                            ✕ Reject
                          </button>
                        </div>
                      )}

                      {/* Feedback */}
                      {msg.role === 'assistant' && msg.actionTaken && !msg.requiresApproval && (
                        <div className="feedback-row">
                          <button
                            className={`feedback-btn ${msg.feedbackGiven === 'positive' ? 'selected' : ''}`}
                            onClick={() => handleFeedback(msg.sessionId, true, idx)}
                            disabled={msg.feedbackGiven != null}
                          >👍 Helpful</button>
                          <button
                            className={`feedback-btn ${msg.feedbackGiven === 'negative' ? 'selected' : ''}`}
                            onClick={() => handleFeedback(msg.sessionId, false, idx)}
                            disabled={msg.feedbackGiven != null}
                          >👎 Not helpful</button>
                        </div>
                      )}
                    </div>
                  </div>
                ))}

                {/* Typing Indicator */}
                {loading && (
                  <div className="typing-indicator">
                    <div className="message-avatar" style={{ background: 'linear-gradient(135deg, var(--accent-cyan), #0fa87a)', color: 'var(--bg-primary)', width: 32, height: 32, borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14, fontWeight: 600 }}>A</div>
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
                id="chat-input"
              />
            </div>
            <button className="send-btn" onClick={() => handleSend()} disabled={!input.trim() || loading} id="send-button">
              ➤
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}
