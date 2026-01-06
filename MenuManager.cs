using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.UI;
using iFruitAddon2;
using LemonUI;
using LemonUI.Menus;

namespace CarDealerShipMod
{
    public class MenuManager
    {
        private readonly EconomyManager _economy;
        private readonly MissionManager _missions;

        private readonly int _baseCarPrice;
        private readonly Action _onBlackMarketCalled;

        private ObjectPool _menuPool;
        private ObjectPool _blackMarketMenuPool;

        private NativeMenu _stashMenu;
        private NativeMenu _confirmMenu;
        private NativeMenu _blackMarketMenu;
        private NativeMenu _servicesMenu; // Le nouveau menu pour le téléphone

        private readonly CustomiFruit _ifruit = new CustomiFruit();

        public ObjectPool MenuPool => _menuPool;
        public ObjectPool BlackMarketMenuPool => _blackMarketMenuPool;

        public MenuManager(EconomyManager economy, MissionManager missions, int baseCarPrice, Action onBlackMarketCalled)
        {
            _economy = economy;
            _missions = missions;
            _baseCarPrice = baseCarPrice;
            _onBlackMarketCalled = onBlackMarketCalled;
        }

        public void InitializeAll()
        {
            InitializeStashMenu();
            InitializeServicesMenu();
            InitializeBlackMarketMenu();
            InitializePhone();
        }

        public void Update()
        {
            _ifruit.Update();
            _menuPool?.Process();
            _blackMarketMenuPool?.Process();
        }

        public void UpdateStashMenuTitle()
        {
            if (_stashMenu == null || _stashMenu.Items.Count == 0)
                return;

            ref int playerStashMoney = ref _economy.GetPlayerStashMoney();
            ref int playerDirtyMoney = ref _economy.GetPlayerDirtyMoney();
            _stashMenu.Items[0].Title = $"💰 Stash: ${playerStashMoney} | Carried: ${playerDirtyMoney}";
        }

        public void OpenStashMenu()
        {
            if (_stashMenu == null)
                return;

            UpdateStashMenuTitle();
            _stashMenu.Visible = true;
        }

        public void OpenBlackMarketMenu(int salePrice, string vehicleDisplayName)
        {
            if (_blackMarketMenu == null)
                return;

            _blackMarketMenu.Items[0].Title = $"🚗 Vehicle: {vehicleDisplayName}";
            _blackMarketMenu.Items[1].Title = $"💰 Price: ~g~ ${salePrice}";
            _blackMarketMenu.Items[2].Title = $"💰 Withdraw Stored Money: ${_economy.BlackMarketStoredMoney}";

            _blackMarketMenu.Visible = true;
        }

        private void InitializeBlackMarketMenu()
        {
            _blackMarketMenuPool = new ObjectPool();
            _blackMarketMenu = new NativeMenu("Black Market", "Sell stolen vehicles.");

            var vehicleInfo = new NativeItem("🚗 Vehicle: None");
            var vehiclePrice = new NativeItem("💰 Price: $0");
            var withdrawStoredMoney = new NativeItem($"💰 Withdraw Stored Money: ~g~ ${_economy.BlackMarketStoredMoney}", "Take back your stored earnings.");
            var sellVehicle = new NativeItem("✔ Sell Vehicle");
            var cancel = new NativeItem("❌ Cancel");

            vehicleInfo.Enabled = false;
            vehiclePrice.Enabled = false;

            sellVehicle.Activated += (sender, args) => ConfirmVehicleSale();
            withdrawStoredMoney.Activated += (sender, args) => WithdrawBlackMarketMoney();
            cancel.Activated += (sender, args) => _blackMarketMenu.Visible = false;

            _blackMarketMenu.Add(vehicleInfo);
            _blackMarketMenu.Add(vehiclePrice);
            _blackMarketMenu.Add(withdrawStoredMoney);
            _blackMarketMenu.Add(sellVehicle);
            _blackMarketMenu.Add(cancel);

            _blackMarketMenuPool.Add(_blackMarketMenu);
        }

        private void ConfirmVehicleSale()
        {
            Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
            if (playerVehicle == null)
            {
                Notification.Show("~r~No vehicle detected!");
                return;
            }

            int salePrice = Utils.CalculatePriceBasedOnTypeAndDamage(playerVehicle, _baseCarPrice);

            int overflow;
            bool allCarried = _economy.TryAddDirtyMoneyWithLimit(salePrice, out overflow);

            if (allCarried)
            {
                _economy.Save();
                Notification.Show($"~g~Vehicle sold for ${salePrice}!");
            }
            else
            {
                _blackMarketMenu.Items[2].Title = $"💰 Withdraw Stored Money: ${_economy.BlackMarketStoredMoney}";
                _economy.Save();
                Notification.Show($"~g~Vehicle sold! You reached the money limit. ${overflow} stored at Black Market.");
            }

            playerVehicle.Delete();
            _economy.Save();
            _blackMarketMenu.Visible = false;
        }

        private void WithdrawBlackMarketMoney()
        {
            if (_economy.BlackMarketStoredMoney <= 0)
            {
                Notification.Show("~r~No stored money available!");
                return;
            }

            int availableSpace = _economy.GetAvailableCarrySpace();
            int amountToWithdraw = Math.Min(_economy.BlackMarketStoredMoney, availableSpace);

            ref int playerDirtyMoney = ref _economy.GetPlayerDirtyMoney();
            playerDirtyMoney += amountToWithdraw;
            _economy.BlackMarketStoredMoney -= amountToWithdraw;

            _blackMarketMenu.Items[2].Title = $"💰 Withdraw Stored Money: ${_economy.BlackMarketStoredMoney}";
            Notification.Show($"~g~You withdrew ${amountToWithdraw} from the Black Market!");
            _economy.Save();
        }

        private void InitializeStashMenu()
        {
            _menuPool = new ObjectPool();
            _stashMenu = new NativeMenu("💰 Money Stash", "Manage your dirty money");

            var stashInfo = new NativeItem($"~r~Stash: ${_economy.GetPlayerStashMoney()} | ~y~ Carried: ${_economy.GetPlayerDirtyMoney()}");
            stashInfo.Enabled = false;

            var amountList = new List<int> { 100, 500, 1000, 5000, 8000, 10000, 15000, 35000, 50000, 100000 };
            var amountSelector = new NativeListItem<int>("Amount", amountList.ToArray());

            var depositItem = new NativeItem("Deposit", "Store dirty money in the stash.");
            var withdrawItem = new NativeItem("Withdraw", "Take dirty money from the stash.");

            depositItem.Activated += (sender, args) => ConfirmTransaction(true, amountSelector.SelectedItem);
            withdrawItem.Activated += (sender, args) => ConfirmTransaction(false, amountSelector.SelectedItem);

            _stashMenu.Add(stashInfo);
            _stashMenu.Add(amountSelector);
            _stashMenu.Add(depositItem);
            _stashMenu.Add(withdrawItem);
            _menuPool.Add(_stashMenu);
        }
        
        private void InitializeServicesMenu()
{
    _servicesMenu = new NativeMenu("Réseau Criminel", "Services du Marché Noir");
    _menuPool.Add(_servicesMenu);

    // ITEM 1 : Voir l'argent sale (Lecture seule)
    var moneyItem = new NativeItem("Argent Sale", "L'argent liquide sale que vous portez.");
    moneyItem.AltTitle = "$0"; // Sera mis à jour à l'ouverture
    moneyItem.Enabled = false; // On ne peut pas cliquer dessus, c'est juste pour info

    // ITEM 2 : Demander un déplacement
    var moveMarketItem = new NativeItem("Déplacer le Marché", "Demande au marché de changer de planque (~g~$5000~s~).");
    
    // Action quand on clique sur "Déplacer"
    moveMarketItem.Activated += (sender, args) =>
    {
        _servicesMenu.Visible = false; // On ferme le menu
        _onBlackMarketCalled?.Invoke(); // On déclenche le déplacement (ta fonction MoveBlackMarket)
    };

    _servicesMenu.Add(moneyItem);
    _servicesMenu.Add(moveMarketItem);

    // Mise à jour dynamique à l'ouverture du menu
    _servicesMenu.Shown += (sender, args) =>
    {
        // On récupère l'argent actuel
        int currentDirtyMoney = _economy.GetPlayerDirtyMoney();
        moneyItem.AltTitle = $"~r~${currentDirtyMoney}";
        
        // Optionnel : Tu peux désactiver le bouton déplacer si le joueur n'a pas assez d'argent
        // moveMarketItem.Enabled = currentDirtyMoney >= 5000; 
    };
}

        private void ConfirmTransaction(bool isDeposit, int amount)
        {
            if (amount <= 0)
            {
                Notification.Show("~r~Invalid amount selected!");
                return;
            }

            _stashMenu.Visible = false;

            string action = isDeposit ? "deposit" : "withdraw";
            string message = $"Are you sure you want to {action} ${amount}?";

            _confirmMenu = new NativeMenu("Confirm Transaction", message);
            _menuPool.Add(_confirmMenu);

            var confirmItem = new NativeItem("✔ Confirm");
            var cancelItem = new NativeItem("❌ Cancel");

            confirmItem.Activated += (sender, args) =>
            {
                if (isDeposit)
                    _economy.TryStoreDirtyMoney(amount);
                else
                    _economy.TryWithdrawDirtyMoney(amount);

                UpdateStashMenuTitle();
                _confirmMenu.Visible = false;
                _stashMenu.Visible = true;
            };

            cancelItem.Activated += (sender, args) =>
            {
                _confirmMenu.Visible = false;
                _stashMenu.Visible = true;
            };

            _confirmMenu.Add(confirmItem);
            _confirmMenu.Add(cancelItem);
            _confirmMenu.Visible = true;
        }

        private void InitializePhone()
        {
            _ifruit.SetWallpaper(Wallpaper.Orange8Bit);
            _ifruit.SetWallpaper("prop_screen_dct1");
            _ifruit.LeftButtonColor = System.Drawing.Color.LimeGreen;
            _ifruit.CenterButtonColor = System.Drawing.Color.Orange;
            _ifruit.RightButtonColor = System.Drawing.Color.Purple;
            _ifruit.LeftButtonIcon = SoftKeyIcon.Police;
            _ifruit.CenterButtonIcon = SoftKeyIcon.Fire;
            _ifruit.RightButtonIcon = SoftKeyIcon.Website;

            iFruitContact contactA = new iFruitContact("Black Market")
            {
                DialTimeout = 4000,
                Active = true,
                Icon = ContactIcon.Blank
            };

            contactA.Answered += (contact) =>
            {
                // On ferme le téléphone proprement (l'animation se fait toute seule généralement)
                _ifruit.Close(); 
    
                // On ouvre le menu des services
                _servicesMenu.Visible = true;
            };
                _ifruit.Contacts.Add(contactA);

            iFruitContact contactB = new iFruitContact("Spencer")
            {
                DialTimeout = 4000,
                Active = true,
                Icon = ContactIcon.Blank
            };
            contactB.Answered += (contact) =>
            {
                if (_missions.CurrentState != MissionState.None)
                {
                    Notification.Show("~r~A mission is already active! Complete it before starting another.");
                    return;
                }

                _missions.StartMission();
            };
            _ifruit.Contacts.Add(contactB);
        }
    }
}
