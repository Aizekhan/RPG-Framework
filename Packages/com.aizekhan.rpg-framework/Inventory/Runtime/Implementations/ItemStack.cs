// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Implementations/ItemStack.cs

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Default implementation of IItemStack.
    /// Manages quantity for stackable items.
    /// </summary>
    public class ItemStack : IItemStack
    {
        private object _item;
        private int _quantity;
        private readonly int _maxStackSize;

        /// <summary>
        /// Creates a new item stack.
        /// </summary>
        /// <param name="item">Item to stack</param>
        /// <param name="quantity">Initial quantity (minimum 1)</param>
        /// <param name="maxStackSize">Maximum stack size (-1 for unlimited)</param>
        public ItemStack(object item, int quantity = 1, int maxStackSize = 99)
        {
            _item = item;
            _quantity = quantity > 0 ? quantity : 1;
            _maxStackSize = maxStackSize;
        }

        public object Item => _item;
        public int Quantity => _quantity;
        public int MaxStackSize => _maxStackSize;
        public bool IsFull => _maxStackSize > 0 && _quantity >= _maxStackSize;

        public int AddToStack(int amount)
        {
            if (amount <= 0) return 0;

            int spaceAvailable = _maxStackSize > 0 ? _maxStackSize - _quantity : int.MaxValue;
            int amountToAdd = System.Math.Min(amount, spaceAvailable);

            _quantity += amountToAdd;
            return amountToAdd;
        }

        public int RemoveFromStack(int amount)
        {
            if (amount <= 0) return 0;

            int amountToRemove = System.Math.Min(amount, _quantity);
            _quantity -= amountToRemove;
            return amountToRemove;
        }

        public IItemStack Split(int splitAmount)
        {
            if (splitAmount <= 0 || splitAmount >= _quantity) return null;

            int removed = RemoveFromStack(splitAmount);
            if (removed == 0) return null;

            return new ItemStack(_item, removed, _maxStackSize);
        }

        public int Merge(IItemStack other)
        {
            if (other == null || !_item.Equals(other.Item)) return other?.Quantity ?? 0;

            int amountAdded = AddToStack(other.Quantity);
            int remaining = other.Quantity - amountAdded;

            return remaining;
        }
    }
}
