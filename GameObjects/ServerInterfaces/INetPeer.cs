using System.Collections.Generic;
using System.Threading;

namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// Represents a local peer capable of holding zero, one or more connections to remote peers.
    /// Implementation of INetPeer should have a constructor that takes in an INetPeerConfiguration object.
    /// </summary>
    interface INetPeer
    {
        /// <summary>
        /// Gets a copy of the list of connections
        /// </summary>
        IList<INetConnection> Connections { get; }

        /// <summary>
        /// Binds to socket and spawns the networking thread
        /// </summary>
        void Start();

        /// <summary>
        /// Create a connection to a remote endpoint 
        /// </summary>
        /// <param name="host">The host IP address</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="hailMessage">The first message to send upon connecting</param>
        /// <returns></returns>
        INetConnection Connect( string host, int port, INetOutgoingMessage hailMessage );

        /// <summary>
        /// Call this to register a callback for when a new message arrives 
        /// </summary>
        /// <param name="callback">The callback</param>
        void RegisterReceivedCallback( SendOrPostCallback callback );

        /// <summary>
        /// Read a pending message from any connection, if any
        /// </summary>
        /// <returns>An incoming message, if it exists; otherwise null</returns>
        INetIncomingMessage ReadMessage();

        /// <summary>
        /// Creates a new message for sending
        /// </summary>
        /// <returns>The empty outgoing message</returns>
        INetOutgoingMessage CreateMessage();

        /// <summary>
        /// Send a message to a list of connections
        /// </summary>
        /// <param name="msg">The message to send</param>
        /// <param name="recipients">The list of recipients to send to</param>
        void SendMessage( INetOutgoingMessage msg, IList<INetConnection> recipients );
    }
}
