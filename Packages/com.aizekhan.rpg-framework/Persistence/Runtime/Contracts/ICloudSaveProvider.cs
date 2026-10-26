using System.Threading.Tasks;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Interface for cloud save providers (Steam, Epic, Xbox, PlayStation, etc.).
    /// </summary>
    public interface ICloudSaveProvider
    {
        /// <summary>
        /// Gets whether cloud saves are available for the current user.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Uploads a save file to the cloud.
        /// </summary>
        Task<bool> UploadSaveAsync(string slotId, byte[] data);

        /// <summary>
        /// Downloads a save file from the cloud.
        /// </summary>
        Task<byte[]> DownloadSaveAsync(string slotId);

        /// <summary>
        /// Deletes a save file from the cloud.
        /// </summary>
        Task<bool> DeleteSaveAsync(string slotId);

        /// <summary>
        /// Synchronizes local saves with cloud saves.
        /// </summary>
        Task<bool> SynchronizeAsync();
    }
}
