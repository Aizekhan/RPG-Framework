using System;
using RPGFramework.Housing.Runtime.Contracts;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Implementations
{
    /// <summary>
    /// Implementation of IFurniture representing a placeable furniture item.
    /// </summary>
    public class Furniture : IFurniture
    {
        private FurniturePlacementData _data;

        public Guid FurnitureId => _data.FurnitureId;
        public string TemplateId => _data.TemplateId;
        public string Name => _data.Name;
        public FurnitureCategory Category => _data.Category;
        public PlacementMode PlacementMode => _data.PlacementMode;
        public (float X, float Y, float Z) Position => (_data.PositionX, _data.PositionY, _data.PositionZ);
        public float Rotation => _data.Rotation;
        public bool HasStorage => _data.HasStorage;
        public int StorageCapacity => _data.StorageCapacity;
        public bool IsCraftingStation => _data.IsCraftingStation;
        public string CraftingStationType => _data.CraftingStationType;

        /// <summary>
        /// Create a new furniture instance.
        /// </summary>
        public Furniture(FurniturePlacementData data)
        {
            _data = data;
        }

        public void SetPosition(float x, float y, float z)
        {
            _data.PositionX = x;
            _data.PositionY = y;
            _data.PositionZ = z;
        }

        public void SetRotation(float rotation)
        {
            _data.Rotation = rotation % 360f;
        }

        /// <summary>
        /// Get the underlying data structure.
        /// </summary>
        public FurniturePlacementData GetData()
        {
            return _data;
        }

        /// <summary>
        /// Update the data structure.
        /// </summary>
        public void UpdateData(FurniturePlacementData data)
        {
            _data = data;
        }
    }
}
