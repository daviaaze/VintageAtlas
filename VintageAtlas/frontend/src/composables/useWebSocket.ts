import { ref } from 'vue';

type MessageHandler = (data: any) => void;

const isConnected = ref(false);
const socket = ref<WebSocket | null>(null);
const handlers = new Map<string, Set<MessageHandler>>();
let reconnectTimer: number | null = null;

export function useWebSocket() {
    const connect = (url?: string) => {
        if (socket.value?.readyState === WebSocket.OPEN) return;

        const wsUrl = url || getWebSocketUrl();
        console.log('[VintageAtlas] Connecting to WebSocket:', wsUrl);

        socket.value = new WebSocket(wsUrl);

        socket.value.onopen = () => {
            console.log('[VintageAtlas] WebSocket connected');
            isConnected.value = true;
            if (reconnectTimer) {
                clearTimeout(reconnectTimer);
                reconnectTimer = null;
            }
        };

        socket.value.onclose = () => {
            console.log('[VintageAtlas] WebSocket disconnected');
            isConnected.value = false;
            socket.value = null;
            scheduleReconnect();
        };

        socket.value.onerror = (error) => {
            console.error('[VintageAtlas] WebSocket error:', error);
            socket.value?.close();
        };

        socket.value.onmessage = (event) => {
            try {
                const message = JSON.parse(event.data);
                if (message.type && handlers.has(message.type)) {
                    handlers.get(message.type)?.forEach(handler => handler(message.data));
                }
            } catch (e) {
                console.error('[VintageAtlas] Failed to parse WebSocket message:', e);
            }
        };
    };

    const scheduleReconnect = () => {
        if (reconnectTimer) return;
        reconnectTimer = window.setTimeout(() => {
            console.log('[VintageAtlas] Attempting to reconnect...');
            reconnectTimer = null;
            connect();
        }, 5000);
    };

    const subscribe = (type: string, handler: MessageHandler) => {
        if (!handlers.has(type)) {
            handlers.set(type, new Set());
        }
        handlers.get(type)?.add(handler);

        // Return unsubscribe function
        return () => {
            const typeHandlers = handlers.get(type);
            if (typeHandlers) {
                typeHandlers.delete(handler);
                if (typeHandlers.size === 0) {
                    handlers.delete(type);
                }
            }
        };
    };

    return {
        isConnected,
        connect,
        subscribe
    };
}

function getWebSocketUrl(): string {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const host = window.location.host;
    // If running in dev mode (port 5173), connect to backend port (usually 5001 or configured)
    // But for now assume relative path works if proxied, or construct full URL
    // Since the backend serves the frontend, relative path is best, but WS needs absolute URL
    return `${protocol}//${host}/`;
}
