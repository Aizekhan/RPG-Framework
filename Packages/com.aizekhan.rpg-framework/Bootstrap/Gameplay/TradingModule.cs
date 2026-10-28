using RPGFramework.Events;
using RPGFramework.Trading;

namespace RPGFramework.Bootstrap.Gameplay
{
    public sealed class TradingOptions
    {
        /// <summary>
        /// Wallet that holds the players' gold; a new <see cref="CurrencyWallet"/> publishing on the event bus is
        /// registered when null.
        /// </summary>
        public ICurrencyWallet Wallet { get; set; }

        /// <summary>
        /// Inventory that trades take items from and give items to; an <see cref="ITradeInventory"/> registered in the
        /// container is used when null, and items are only listed in trade results when there is neither.
        /// </summary>
        public ITradeInventory Inventory { get; set; }
    }

    /// <summary>
    /// Registers <see cref="ITraderRegistry"/>, <see cref="ICurrencyWallet"/> and an <see cref="ITradingManager"/> that
    /// pays from the wallet and moves items through the options' (or the container's) <see cref="ITradeInventory"/>;
    /// events go to the event bus. The game registers its traders and their offers on the registry when its own
    /// module initializes, and a shop purchase is <see cref="ITradingManager.Trade"/>.
    /// </summary>
    public sealed class TradingModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Trading";

        private readonly TradingOptions _options;

        public TradingModule(TradingOptions options = null)
        {
            _options = options ?? new TradingOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<ITraderRegistry>(_ => new TraderRegistry());
            Container.RegisterFactory<ICurrencyWallet>(c => _options.Wallet ?? new CurrencyWallet(c.Resolve<IEventPublisher>()));
            Container.RegisterFactory<ITradingManager>(c => new TradingManager(
                c.Resolve<ITraderRegistry>(),
                c.Resolve<IEventPublisher>(),
                c.Resolve<ICurrencyWallet>(),
                _options.Inventory ?? (c.IsRegistered<ITradeInventory>() ? c.Resolve<ITradeInventory>() : null)));
        }
    }
}
