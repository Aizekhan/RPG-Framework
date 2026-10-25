namespace RPGFramework.Networking
{
    /// <summary>
    /// Common message type identifiers.
    /// Game-specific messages should use IDs starting from 1000.
    /// </summary>
    public static class MessageType
    {
        public const int None = 0;
        public const int Connect = 1;
        public const int Disconnect = 2;
        public const int EntitySpawn = 10;
        public const int EntityDespawn = 11;
        public const int EntityStateUpdate = 12;
        public const int EntityDeltaUpdate = 13;
        public const int RpcInvoke = 20;
        public const int TransformSync = 30;
        public const int Ping = 100;
        public const int Pong = 101;

        // Game-specific messages start at 1000
        public const int GameSpecificStart = 1000;
    }
}
