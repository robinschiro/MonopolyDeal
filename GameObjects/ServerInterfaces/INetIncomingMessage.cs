namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// Incoming message either sent from a remote peer or generated within the library
    /// </summary>
    interface INetIncomingMessage
    {
        /// <summary>
        /// NetConnection of sender, if any
        /// </summary>
        INetConnection SenderConnection { get; }

        /// <summary>
        /// Gets the type of this incoming message
        /// </summary>
        NetIncomingMessageType MessageType { get; }

        /// <summary>
        /// Reads an integer value from the incoming message packet
        /// </summary>
        /// <returns>The integer from the message</param>
        int ReadInt32();

        /// <summary>
        /// Reads a string value from the incoming message packet
        /// </summary>
        /// <returns>The string from the message</param>
        string ReadString();

        /// <summary>
        /// Reads a boolean value from the incoming message packet
        /// </summary>
        /// <returns>The boolean from the message</param>
        bool ReadBoolean();

        /// <summary>
        /// Reads a byte value from the incoming message packet
        /// </summary>
        /// <returns>The byte from the message</param>
        byte ReadByte();
    }
}
