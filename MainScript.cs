using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.UI;

namespace CarDealerShipMod
{
    public class MainScript : Script
    {
        private readonly int _baseCarPrice = 25000;
        private readonly int _maxCarriedDirtyMoney = 350000;
        private readonly float _stashRadius = 2.0f;
        private readonly float _marketRadius = 5.0f;

        private Vector3 _marketLocation = new Vector3(170.8461f, 6359.0230f, 31.4532f);
        private readonly List<Vector3> _blackMarketLocations = new List<Vector3>
        {
            new Vector3(-335.5068f, -2717.748f, 6.0003f),
            new Vector3(-1583.057f, 5156.8511f, 19.6654f),
            new Vector3(3716.1060f, 4525.1758f, 21.6604f),
            new Vector3(-560.7960f, 302.1563f, 83.1715f),
        };

        private readonly Vector3 _launderingLocation = new Vector3(640.3064f, 2780.3027f, 41.9824f);

        private Blip _marketBlip;
        private Blip _launderingBlip;
        private Blip _stashBlip;

        private bool _isPlayerInMarket;
        private DateTime _notificationStartTime;
        private bool _isNotificationVisible;

        private PedHash _lastCharacter;

        private readonly Dictionary<PedHash, List<Vector3>> _characterStashLocations = new Dictionary<PedHash, List<Vector3>>
        {
            { PedHash.Franklin, new List<Vector3> { new Vector3(-26.1794f, -1424.530f, 30.7456f), new Vector3(4.5484f, 530.7266f, 170.6173f) } },
            { PedHash.Michael, new List<Vector3> { new Vector3(-826.8317f, 180.2720f, 71.4480f) } },
            { PedHash.Trevor, new List<Vector3> { new Vector3(1975.6405f, 3818.4612f, 33.4363f), new Vector3(92.9859f, -1291.684f, 29.2688f) } }
        };

        private readonly List<VehicleHash> _availableVehicles = new List<VehicleHash>
        {
            VehicleHash.Adder,
            VehicleHash.T20,
            VehicleHash.Zentorno,
            VehicleHash.Osiris,
            VehicleHash.Bullet,
            VehicleHash.Vacca,
            VehicleHash.Infernus,
            VehicleHash.Cheetah,
            VehicleHash.Turismo2,
            VehicleHash.Turismor,
            VehicleHash.Tempesta,
            VehicleHash.Nero,
            VehicleHash.Nero2,
        };

        private readonly List<Vector3> _spawnLocations = new List<Vector3>
        {
            new Vector3(-1152.933f, -2734.399f, 13.9526f),
            new Vector3(217.603f, -800.745f, 30.655f),
            new Vector3(-1034.553f, -491.692f, 36.214f),
            new Vector3(1210.756f, 2658.333f, 37.899f),
            new Vector3(-205.123f, 6218.567f, 31.489f)
        };

        private readonly Vector3 _missionDeliveryLocation = new Vector3(-560.7960f, 302.1563f, 83.1715f);

        private EconomyManager _economy;
        private MissionManager _missions;
        private MenuManager _menus;

        public MainScript()
        {
            _lastCharacter = (PedHash)Game.Player.Character.Model.Hash;

            _economy = new EconomyManager("scripts\\save.txt", _maxCarriedDirtyMoney, _stashRadius, _characterStashLocations);
            _missions = new MissionManager(_availableVehicles, _spawnLocations, _missionDeliveryLocation);
            _menus = new MenuManager(_economy, _missions, _baseCarPrice, MoveBlackMarket);

            _economy.Load();
            UpdateBlips();
            InitializeStashBlip();
            _menus.InitializeAll();

            Tick += OnTick;
            KeyDown += OnKeyDown;
        }

        private void OnTick(object sender, EventArgs e)
        {
            PedHash currentCharacter = (PedHash)Game.Player.Character.Model.Hash;

            if (currentCharacter != _lastCharacter && Game.Player.CanControlCharacter)
            {
                _lastCharacter = currentCharacter;
                _menus.UpdateStashMenuTitle();
            }

            CheckMarketLocation();
            CheckLaunderingLocation();

            DrawMarketLocation();
            DrawLaunderingLocation();
            DrawStashLocation();

            HandleNotificationTimeout();
            _economy.DrawDirtyMoneyHud();

            _menus.Update();

            _missions.TryHandleTheftStage();
            if (_missions.TryDeliverStolenVehicle(out int reward))
            {
                int overflow;
                _economy.TryAddDirtyMoneyWithLimit(reward, out overflow);
                _economy.Save();
            }
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.E)
            {
                Vector3 currentStash = _economy.GetCurrentStash();

                if (Game.Player.Character.Position.DistanceTo(_launderingLocation) < 5.0f)
                {
                    _economy.TryLaunderingMoney(_launderingLocation);
                    _menus.UpdateStashMenuTitle();
                }
                else if (Game.Player.Character.Position.DistanceTo(currentStash) < _stashRadius)
                {
                    _menus.OpenStashMenu();
                }
                else if (_isPlayerInMarket)
                {
                    OpenBlackMarketMenu();
                    _menus.UpdateStashMenuTitle();
                }
                else
                {
                    Notification.Show("~r~You are not in the correct location to perform this action!");
                }
            }

            if (e.KeyCode == System.Windows.Forms.Keys.T)
            {
                _missions.ForceResetMission();
            }

            if (e.KeyCode == System.Windows.Forms.Keys.F7)
            {
                ref int playerDirtyMoney = ref _economy.GetPlayerDirtyMoney();
                playerDirtyMoney += 350000;
                _economy.Save();
                Notification.Show("~g~Cheat activated: +$350,000 dirty money!");
            }
        }

        private void OpenBlackMarketMenu()
        {
            Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
            if (playerVehicle == null)
            {
                Notification.Show("~r~You must be in a vehicle to sell it!");
                return;
            }

            int salePrice = Utils.CalculatePriceBasedOnTypeAndDamage(playerVehicle, _baseCarPrice);
            _menus.OpenBlackMarketMenu(salePrice, playerVehicle.DisplayName);
        }

        private void MoveBlackMarket()
        {
            int index = Utils.Rng.Next(_blackMarketLocations.Count);
            Vector3 selectedLocation = _blackMarketLocations[index];

            float correctedHeight = Utils.GetCorrectedGroundHeight(selectedLocation);
            selectedLocation.Z = correctedHeight;

            _marketLocation = selectedLocation;
            UpdateBlips();

            Notification.Show("~g~The Black Market has moved to a new location!");
        }

        private void CheckMarketLocation()
        {
            Vector3 playerPosition = Game.Player.Character.Position;

            if (playerPosition.DistanceTo(_marketLocation) <= _marketRadius)
            {
                if (!_isPlayerInMarket)
                {
                    _isPlayerInMarket = true;
                    ShowMarketNotification();
                }
            }
            else
            {
                _isPlayerInMarket = false;
            }
        }

        private void ShowMarketNotification()
        {
            Notification.Show("~y~You are at the black market! Press ~b~E~y~ to sell a vehicle.");
            _isNotificationVisible = true;
            _notificationStartTime = DateTime.Now;
        }

        private void HandleNotificationTimeout()
        {
            if (_isNotificationVisible && (DateTime.Now - _notificationStartTime).TotalSeconds > 5)
            {
                _isNotificationVisible = false;
            }
        }

        private void CheckLaunderingLocation()
        {
            Vector3 playerPosition = Game.Player.Character.Position;

            if (playerPosition.DistanceTo(_launderingLocation) <= 5.0f)
            {
                Notification.Show("~b~You are at the money laundering spot! Press ~y~E~b~ to launder dirty money.");
            }
        }

        private void DrawMarketLocation()
        {
            World.DrawMarker(
                MarkerType.VerticalCylinder,
                _marketLocation,
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(3.0f, 3.0f, 0.5f),
                Color.Red,
                false,
                false
            );
        }

        private void DrawLaunderingLocation()
        {
            World.DrawMarker(
                MarkerType.VerticalCylinder,
                _launderingLocation,
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(3.0f, 3.0f, 0.5f),
                Color.Red,
                false,
                false
            );
        }

        private void InitializeStashBlip()
        {
            Vector3 stashLocation = _economy.GetCurrentStash();
            _stashBlip = World.CreateBlip(stashLocation);
            _stashBlip.Sprite = BlipSprite.Safehouse;
            _stashBlip.Color = BlipColor.Yellow;
            _stashBlip.Name = "Money Stash";
        }

        private void DrawStashLocation()
        {
            Vector3 stashLocation = _economy.GetCurrentStash();

            if (_stashBlip == null)
            {
                InitializeStashBlip();
            }
            else
            {
                _stashBlip.Position = stashLocation;
            }

            World.DrawMarker(
                MarkerType.VerticalCylinder,
                stashLocation,
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(2.0f, 2.0f, 0.5f),
                Color.Green,
                false,
                false
            );
        }

        private void UpdateBlips()
        {
            CreateMarketBlip();
            CreateLaunderingBlip();
        }

        private void CreateMarketBlip()
        {
            if (_marketBlip != null)
            {
                _marketBlip.Delete();
            }

            _marketBlip = World.CreateBlip(_marketLocation);
            _marketBlip.Sprite = BlipSprite.GunCar;
            _marketBlip.Color = BlipColor.Red;
            _marketBlip.Name = "Black Market";
            _marketBlip.Scale = 1.0f;
        }

        private void CreateLaunderingBlip()
        {
            if (_launderingBlip != null)
            {
                _launderingBlip.Delete();
            }

            _launderingBlip = World.CreateBlip(_launderingLocation);
            _launderingBlip.Sprite = BlipSprite.Lester;
            _launderingBlip.Color = BlipColor.RedLight;
            _launderingBlip.Name = "Money Laundering";
            _launderingBlip.Scale = 1.0f;
        }
    }
}
