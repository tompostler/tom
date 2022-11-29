using System.Collections.Generic;
using System.IO;

namespace Unlimitedinf.Tom.WebSocket
{
    internal sealed class WebSocketState
    {
        public DirectoryInfo CurrentDirectory { get; set; }

        /// <summary>
        /// Set when we are receiving file(s) from the client
        /// </summary>
        public Queue<FileInfo> ReceivingFiles { get; set; }

        /// <summary>
        /// Set when we are sending file(s) to the client
        /// </summary>
        public Queue<FileInfo> SendingFiles { get; set; }
    }
}
