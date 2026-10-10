namespace EIStarter
{
    internal static class WineCompatibility
    {
        internal static void PrepareGameStart(System.Diagnostics.ProcessStartInfo start)
        {
            // The player's tested Wine heap diagnostic is inherited only by the game.
            start.EnvironmentVariables["WINEDEBUG"] = "warn+heap";
        }
    }
}
