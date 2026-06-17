/* PROPIA - cliente SignalR para Conversaciones realtime.
 * Conecta a /hubs/chat del API maestro. JWT viaja en ?access_token=.
 * Expone window.propiaChatHub con connect/disconnect/joinConv/leaveConv.
 * El componente Razor pasa una DotNetObjectReference para recibir callbacks.
 */
(function () {
    var connection = null;
    var dotnetRef = null;

    function apiBase() {
        var m = document.querySelector('meta[name="propia-api-base"]');
        return (m && m.content) ? m.content : '';
    }

    function jwt() {
        try { return sessionStorage.getItem('propia_jwt') || localStorage.getItem('propia_jwt'); }
        catch (e) { return null; }
    }

    window.propiaChatHub = {
        /**
         * Conecta al hub. ref es un DotNetObjectReference con metodos:
         *   - OnMessageAdded(MensajeDto)
         *   - OnBoardChanged()
         */
        connect: async function (ref) {
            if (connection) {
                try { await connection.stop(); } catch (e) { }
                connection = null;
            }
            dotnetRef = ref;
            var token = jwt();
            if (!token) return false;

            connection = new signalR.HubConnectionBuilder()
                .withUrl(apiBase() + '/hubs/chat?access_token=' + encodeURIComponent(token), {
                    skipNegotiation: false
                })
                .withAutomaticReconnect()
                .build();

            connection.on('MessageAdded', function (mensaje) {
                if (dotnetRef) {
                    try { dotnetRef.invokeMethodAsync('OnMessageAdded', mensaje); } catch (e) { }
                }
            });
            connection.on('BoardChanged', function (_payload) {
                if (dotnetRef) {
                    try { dotnetRef.invokeMethodAsync('OnBoardChanged'); } catch (e) { }
                }
            });

            try {
                await connection.start();
                return true;
            } catch (e) {
                console.warn('SignalR connect failed', e);
                return false;
            }
        },

        joinConversation: async function (conversationId) {
            if (!connection || connection.state !== 'Connected') return;
            try { await connection.invoke('JoinConversation', conversationId); } catch (e) { }
        },

        leaveConversation: async function (conversationId) {
            if (!connection || connection.state !== 'Connected') return;
            try { await connection.invoke('LeaveConversation', conversationId); } catch (e) { }
        },

        disconnect: async function () {
            dotnetRef = null;
            if (connection) {
                try { await connection.stop(); } catch (e) { }
                connection = null;
            }
        }
    };
})();
