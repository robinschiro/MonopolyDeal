namespace GameObjects
{
    /// <summary>
    /// Request to ring a player's "bell", used to passively tell a player to finish their turn.
    /// </summary>
    public class RingBellRequest : PlaySoundRequest
    {
        /// <summary>
        ///  Name of the player whose bell is rung
        /// </summary>
        string RingeeName { get; set; }

        public RingBellRequest( string uriPath, string ringeeName ) : base(uriPath)
        {
            this.RingeeName = ringeeName;
        }
    }
}
