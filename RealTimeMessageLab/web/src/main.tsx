import React, { FormEvent, useEffect, useMemo, useState } from "react";
import ReactDOM from "react-dom/client";
import "./styles.css";

const apiBaseUrl = "http://localhost:5000";
const websocketUrl = "ws://localhost:5000/ws";

type Message = {
  id: number;
  text: string;
  createdAt: string;
  clientName: string | null;
};

type RealtimeEvent = {
  type: "message.created";
  message: Message;
};

function App() {
  const [messages, setMessages] = useState<Message[]>([]);
  const [clientName, setClientName] = useState("Browser");
  const [text, setText] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [isRealtimeEnabled, setIsRealtimeEnabled] = useState(true);
  const [connectionState, setConnectionState] = useState<"connecting" | "open" | "closed">("connecting");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isActive = true;

    async function loadMessages() {
      try {
        const response = await fetch(`${apiBaseUrl}/messages`);

        if (!response.ok) {
          throw new Error("Could not load messages.");
        }

        const snapshot = (await response.json()) as Message[];

        if (isActive) {
          setMessages(snapshot);
        }
      } catch (loadError) {
        if (isActive) {
          setError(loadError instanceof Error ? loadError.message : "Could not load messages.");
        }
      }
    }

    loadMessages();

    return () => {
      isActive = false;
    };
  }, []);

  useEffect(() => {
    if (!isRealtimeEnabled) {
      setConnectionState("closed");
      return;
    }

    setConnectionState("connecting");
    const socket = new WebSocket(websocketUrl);

    socket.addEventListener("open", () => {
      setConnectionState("open");
      setError(null);
    });

    socket.addEventListener("message", (event) => {
      const realtimeEvent = JSON.parse(event.data) as RealtimeEvent;

      if (realtimeEvent.type === "message.created") {
        setMessages((currentMessages) => {
          if (currentMessages.some((message) => message.id === realtimeEvent.message.id)) {
            return currentMessages;
          }

          return [...currentMessages, realtimeEvent.message];
        });
      }
    });

    socket.addEventListener("close", () => {
      setConnectionState("closed");
    });

    socket.addEventListener("error", () => {
      setConnectionState("closed");
      setError("Realtime connection failed. Check that the API is running on port 5000.");
    });

    return () => {
      socket.close();
    };
  }, [isRealtimeEnabled]);

  async function sendMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedText = text.trim();

    if (!trimmedText) {
      setError("Message text is required.");
      return;
    }

    setIsSending(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/messages`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          text: trimmedText,
          clientName: clientName.trim() || null
        })
      });

      if (!response.ok) {
        const payload = (await response.json()) as { error?: string };
        throw new Error(payload.error ?? "Could not send message.");
      }

      const createdMessage = (await response.json()) as Message;
      setMessages((currentMessages) => {
        if (currentMessages.some((message) => message.id === createdMessage.id)) {
          return currentMessages;
        }

        return [...currentMessages, createdMessage];
      });
      setText("");
    } catch (sendError) {
      setError(sendError instanceof Error ? sendError.message : "Could not send message.");
    } finally {
      setIsSending(false);
    }
  }

  const latestMessage = useMemo(() => messages.at(-1), [messages]);

  return (
    <main className="app-shell">
      <section className="workspace">
        <header className="topbar">
          <div>
            <p className="eyebrow">Realtime Message Lab</p>
            <h1>Message Stream</h1>
          </div>
          <div className="connection-controls">
            <ConnectionBadge state={connectionState} />
            <button
              className="connection-toggle"
              type="button"
              onClick={() => setIsRealtimeEnabled((enabled) => !enabled)}
            >
              {isRealtimeEnabled ? "Disable" : "Enable"}
            </button>
          </div>
        </header>

        <section className="stream-layout">
          <div className="message-panel">
            <div className="panel-header">
              <div>
                <h2>Live Feed</h2>
                <p>{messages.length} messages received</p>
              </div>
              {latestMessage ? <span>Last #{latestMessage.id}</span> : <span>Waiting</span>}
            </div>

            <ol className="message-list">
              {messages.length === 0 ? (
                <li className="empty-state">No messages yet.</li>
              ) : (
                messages.map((message) => <MessageItem key={message.id} message={message} />)
              )}
            </ol>
          </div>

          <form className="composer" onSubmit={sendMessage}>
            <label>
              Name
              <input
                value={clientName}
                onChange={(event) => setClientName(event.target.value)}
                maxLength={40}
                placeholder="Your name"
              />
            </label>

            <label>
              Message
              <textarea
                value={text}
                onChange={(event) => setText(event.target.value)}
                rows={6}
                maxLength={400}
                placeholder="Write a message"
              />
            </label>

            {error ? <p className="error-message">{error}</p> : null}

            <button type="submit" disabled={isSending}>
              {isSending ? "Sending..." : "Send Message"}
            </button>
          </form>
        </section>
      </section>
    </main>
  );
}

function ConnectionBadge({ state }: { state: "connecting" | "open" | "closed" }) {
  return <span className={`connection-badge ${state}`}>{state}</span>;
}

function MessageItem({ message }: { message: Message }) {
  const timestamp = new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
  }).format(new Date(message.createdAt));

  return (
    <li className="message-item">
      <div className="message-meta">
        <strong>{message.clientName ?? "Anonymous"}</strong>
        <span>{timestamp}</span>
      </div>
      <p>{message.text}</p>
    </li>
  );
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
