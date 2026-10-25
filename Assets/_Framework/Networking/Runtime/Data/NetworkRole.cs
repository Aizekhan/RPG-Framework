namespace RPGFramework.Networking
{
    /// <summary>
    /// Role of the local peer in the network.
    /// </summary>
    public enum NetworkRole
    {
        None,
        Server,
        Client,
        Host // Server + Client combined (listen server)
    }
}
