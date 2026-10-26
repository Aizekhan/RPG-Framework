// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Contracts/IItemStack.cs

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Represents a stack of identical items.
    /// Supports stacking, splitting, and merging operations.
    /// </summary>
    public interface IItemStack
    {
        /// <summary>
        /// The item being stacked (ID, instance, or definition).
        /// Type is object for maximum flexibility.
        /// </summary>
        object Item { get; }

        /// <summary>
        /// Current stack quantity.
        /// </summary>
        int Quantity { get; }

        /// <summary>
        /// Maximum stack size.
        /// -1 for unlimited stacking.
        /// </summary>
        int MaxStackSize { get; }

        /// <summary>
        /// Whether this stack is at maximum capacity.
        /// </summary>
        bool IsFull { get; }

        /// <summary>
        /// Adds quantity to this stack.
        /// </summary>
        /// <param name="amount">Amount to add</param>
        /// <returns>Amount successfully added (may be less if stack reaches max)</returns>
        int AddToStack(int amount);

        /// <summary>
        /// Removes quantity from this stack.
        /// </summary>
        /// <param name="amount">Amount to remove</param>
        /// <returns>Amount successfully removed (may be less if insufficient quantity)</returns>
        int RemoveFromStack(int amount);

        /// <summary>
        /// Splits this stack into two stacks.
        /// Original stack quantity is reduced by splitAmount.
        /// </summary>
        /// <param name="splitAmount">Quantity to split off</param>
        /// <returns>New stack with split amount, or null if invalid split</returns>
        IItemStack Split(int splitAmount);

        /// <summary>
        /// Merges another stack into this one.
        /// Items must match for merge to succeed.
        /// </summary>
        /// <param name="other">Stack to merge</param>
        /// <returns>Remaining quantity that couldn't be merged (0 if all merged)</returns>
        int Merge(IItemStack other);
    }
}
