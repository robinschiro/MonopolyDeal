namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// Represents a connection to a remote peer
    /// </summary>
    interface INetConnection
    {
        /// <summary>
        /// Gets the unique identifier of the remote NetPeer for this connection
        /// </summary>
        long RemoteUniqueIdentifier { get; }

        /// <summary>
        /// Approves this connection; sending a connection response to the remote host
        /// </summary>
        void Approve();

        /// <summary>
        /// Denies this connection; disconnecting it
        /// </summary>
        void Deny();
    }
}
