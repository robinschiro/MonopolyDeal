namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// Specialized version of NetPeer used for a "client" connection. It does not accept any incoming connections and maintains a ServerConnection property
    /// </summary>
    interface INetClient : INetPeer
    {
        /// <summary>
        /// Gets the connection to the server, if any
        /// </summary>
        INetConnection ServerConnection { get; }

        /// <summary>
        /// Sends message to server
        /// </summary>
        /// <param name="msg">The message to send</param>
        void SendMessage( INetOutgoingMessage msg );

        /// <summary>
        /// Disconnect from server
        /// </summary>
        /// <param name="byeMessage">Reason for disconnect</param>
        void Disconnect( string byeMessage );
    }
}
