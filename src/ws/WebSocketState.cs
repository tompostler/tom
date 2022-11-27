﻿using System.IO;

namespace Unlimitedinf.Tom.WebSocket
{
    internal sealed class WebSocketState
    {
        public DirectoryInfo CurrentDirectory { get; set; }

        /// <summary>
        /// Set when we are receiving a file from the client
        /// </summary>
        public FileInfo ReceivingFile { get; set; }
        /// <summary>
        /// To validate the receivied file when complete
        /// </summary>
        public string ReceivingFileSha256 { get; set; }

        /// <summary>
        /// Set when we are sending a file to the client
        /// </summary>
        public FileInfo SendingFile { get; set; }
    }
}