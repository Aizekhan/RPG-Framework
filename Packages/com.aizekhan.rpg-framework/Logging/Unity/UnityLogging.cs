using System.IO;
using UnityEngine;

namespace RPGFramework.Logging.Unity
{
    /// <summary>
    /// Builds a <see cref="Logger"/> with sensible Unity defaults:
    /// Editor → Debug level, console + file; development build → Information, console + file;
    /// release → Warning, console only.
    /// </summary>
    public static class UnityLogging
    {
        public static Logger CreateDefault(string filePrefix = "game")
        {
            bool development = Application.isEditor || Debug.isDebugBuild;
            var logger = new Logger(Application.isEditor ? LogSeverity.Debug : development ? LogSeverity.Information : LogSeverity.Warning);
            logger.AddSink(new UnityConsoleLogSink());

            if (development)
                logger.AddSink(CreateFileSink(filePrefix));

            return logger;
        }

        /// <summary>
        /// File sink in Application.persistentDataPath/Logs with a device/version header.
        /// </summary>
        public static FileLogSink CreateFileSink(string filePrefix = "game")
        {
            string header =
                $"=== {Application.productName} {Application.version} | Unity {Application.unityVersion} | {Application.platform} ===\n" +
                $"Device: {SystemInfo.deviceModel} | OS: {SystemInfo.operatingSystem} | CPU: {SystemInfo.processorType} | " +
                $"RAM: {SystemInfo.systemMemorySize} MB | GPU: {SystemInfo.graphicsDeviceName}";

            return new FileLogSink(Path.Combine(Application.persistentDataPath, "Logs"), filePrefix, header: header);
        }
    }
}
