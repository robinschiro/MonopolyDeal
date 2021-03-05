namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// Outgoing message used to send data to remote peer(s)
    /// </summary>
    interface INetOutgoingMessage
    {
        /// <summary>
        /// Writes an integer value to the outgoing message packet
        /// </summary>
        /// <param name="source"></param>
        void Write( int source );

        /// <summary>
        /// Writes a string value to the outgoing message packet
        /// </summary>
        /// <param name="source"></param>
        void Write( string source );

        /// <summary>
        /// Writes a boolean value to the outgoing message packet
        /// </summary>
        /// <param name="source"></param>
        void Write( bool source );

        /// <summary>
        /// Writes a byte value to the outgoing message packet
        /// </summary>
        /// <param name="source"></param>
        void Write( byte source );
    }
}
