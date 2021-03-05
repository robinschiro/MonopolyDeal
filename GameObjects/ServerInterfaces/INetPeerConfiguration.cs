namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// Partly immutable after NetPeer has been initialized
    /// </summary>
    interface INetPeerConfiguration
    {
        /// <summary>
        /// Gets or sets the local port to bind to. Defaults to 0. Cannot be changed once NetPeer is initialized
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// Gets or sets the maximum amount of connections this peer can hold. Cannot be changed once NetPeer is initialized
        /// </summary>
        int MaximumConnections { get; set; }
    }
}
