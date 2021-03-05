namespace GameObjects.ServerInterfaces
{
    /// <summary>
    /// The type of a NetIncomingMessage
    /// </summary>
    public enum NetIncomingMessageType
    {
        Error = 0,
        StatusChanged = 1,
        UnconnectedData = 2,
        ConnectionApproval = 4,
        Data = 8,
        Receipt = 16,
        DiscoveryRequest = 32,
        DiscoveryResponse = 64,
        VerboseDebugMessage = 128,
        DebugMessage = 256,
        WarningMessage = 512,
        ErrorMessage = 1024,
        NatIntroductionSuccess = 2048,
        ConnectionLatencyUpdated = 4096
    }
}
