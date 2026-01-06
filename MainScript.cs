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
            new Vector3(-28.2225f, -1085.1254f, 26.0007f),
            new Vector3(605.1786f, -417.6840f, 24.6840f),
            new Vector3(119.9849f, 6642.1274f, 31.5756f),
            new Vector3(1170.9813f, -2973.4436f, 5.0628f),
        };

        private readonly List<LaunderingPoint> _launderingPoints = new List<LaunderingPoint>
{
    // 1. Petit Dealer (Vespucci) : Frais énormes (40%), limite basse, risque doublé
    new LaunderingPoint { Name = "Street Dealer", Position = new Vector3(-1225.9032f, -1439.8582f, 4.3372f), Fee = 0.40f, MaxAmount = 5000, RiskMultiplier = 2.0f },
    
    // 2. Moyen (Lavage auto) : Frais moyens (20%), limite $25k, risque normal
    new LaunderingPoint { Name = "Car Wash Front", Position = new Vector3(34.8f, -1391.8f, 29.3f), Fee = 0.20f, MaxAmount = 25000, RiskMultiplier = 1.0f },
    
    // 3. Gros (Comptable pro) : Frais bas (10%), limite énorme, risque divisé par deux
    new LaunderingPoint { Name = "Professional Accountant", Position = new Vector3(-1419.1924f, -251.2594f, 46.3792f), Fee = 0.10f, MaxAmount = 250000, RiskMultiplier = 0.5f }
};

        private Blip _marketBlip;
        private List<Blip> _activeLaunderingBlips = new List<Blip>();
        private Blip _stashBlip;

        private bool _isPlayerInMarket;
        private DateTime _notificationStartTime;
        private bool _isNotificationVisible;

        private PedHash _lastCharacter;

        private readonly Dictionary<PedHash, List<Vector3>> _characterStashLocations = new Dictionary<PedHash, List<Vector3>>
        {
            { PedHash.Franklin, new List<Vector3> { new Vector3(-26.1794f, -1424.530f, 30.7456f), new Vector3(4.5484f, 530.7266f, 170.6173f) } },
            { PedHash.Michael, new List<Vector3> { new Vector3(-809.9568f, 189.4705f, 72.4787f) } },
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
            CleanUpOldBlips();
            _lastCharacter = (PedHash)Game.Player.Character.Model.Hash;

            _economy = new EconomyManager("scripts\\save.txt", _maxCarriedDirtyMoney, _stashRadius, _characterStashLocations);
            _missions = new MissionManager(_availableVehicles, _spawnLocations, _missionDeliveryLocation);
            _menus = new MenuManager(_economy, _missions, _baseCarPrice, MoveBlackMarket);

            _missions.MissionCompleted += OnMissionCompleted;

            _economy.Load();
            UpdateBlips();
            InitializeStashBlip();
            _menus.InitializeAll();

            Aborted += OnAborted;

            Tick += OnTick;
            KeyDown += OnKeyDown;
        }

        private void OnMissionCompleted(int reward)
        {
            int overflow;
            bool allCarried = _economy.TryAddDirtyMoneyWithLimit(reward, out overflow);
            _economy.Save();

            if (!allCarried && overflow > 0)
            {
                Notification.Show($"~y~Carry limit reached. ${overflow} stored at Black Market.");
            }
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
            //_economy.DrawDirtyMoneyHud();

            _menus.Update();

            _missions.Update();
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.E)
{
    Vector3 currentStash = _economy.GetCurrentStash();
    
    // On cherche le blanchisseur le plus proche (moins de 5m)
    LaunderingPoint currentLaunderer = _launderingPoints.Find(p => Game.Player.Character.Position.DistanceTo(p.Position) < 5.0f);

    if (currentLaunderer != null)
    {
        _economy.TryLaunderingMoney(currentLaunderer);
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
}

            if (e.KeyCode == System.Windows.Forms.Keys.T)
            {
                _missions.ForceReset();
            }

            // if (e.KeyCode == System.Windows.Forms.Keys.F7)
            // {
            //     ref int playerDirtyMoney = ref _economy.GetPlayerDirtyMoney();
            //     playerDirtyMoney += 350000;
            //     _economy.Save();
            //     Notification.Show("~g~Cheat activated: +$350,000 dirty money!");
            // }
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

    foreach (var point in _launderingPoints)
    {
        if (playerPosition.DistanceTo(point.Position) <= 5.0f)
        {
            Notification.Show($"~b~You are at {point.Name}! Press ~y~E~b~ to launder.");
            return; // On arrête la boucle dès qu'on en a trouvé un
        }
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
    foreach (var point in _launderingPoints)
    {
        World.DrawMarker(
            MarkerType.VerticalCylinder,
            point.Position,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(3.0f, 3.0f, 0.5f),
            Color.Red,
            false,
            false
        );
    }
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
                new Vector3(1.0f, 1.0f, 0.5f),
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
    // On nettoie d'abord notre liste de blips actifs pour éviter les doublons
    foreach (Blip b in _activeLaunderingBlips)
    {
        if (b != null && b.Exists()) b.Delete();
    }
    _activeLaunderingBlips.Clear();

    // On crée un nouveau blip pour chaque point de la liste
    foreach (var point in _launderingPoints)
    {
        Blip newBlip = World.CreateBlip(point.Position);
        newBlip.Sprite = BlipSprite.Lester;
        newBlip.Color = BlipColor.RedLight;
        newBlip.Name = "Laundering: " + point.Name;
        newBlip.Scale = 0.8f;
        newBlip.IsShortRange = true;

        // On l'ajoute à notre liste pour pouvoir le supprimer plus tard
        _activeLaunderingBlips.Add(newBlip);
    }
}

        private void OnAborted(object sender, EventArgs e)
        {
            // Nettoyage du Marché Noir
            if (_marketBlip != null && _marketBlip.Exists())
            {
                _marketBlip.Delete();
            }

// Nettoyage de tous les blips de blanchiment actifs
foreach (Blip b in _activeLaunderingBlips)
{
    if (b != null && b.Exists())
    {
        b.Delete();
    }
}
_activeLaunderingBlips.Clear();

            // Nettoyage de la Planque (Stash)
            if (_stashBlip != null && _stashBlip.Exists())
            {
                _stashBlip.Delete();
            }
        }

        private void CleanUpOldBlips()
    {
    // On récupère TOUS les blips de la carte
    foreach (Blip b in World.GetAllBlips())
    {
        // Sécurité : on vérifie que le blip existe
        if (!b.Exists()) continue;

        // 1. Nettoyage du Marché Noir (Sprite GunCar + Rouge)
        // On vérifie si c'est le bon icône ET s'il est proche d'une des positions connues du marché
        if (b.Sprite == BlipSprite.GunCar && b.Color == BlipColor.Red)
        {
            // On vérifie la distance avec ta position actuelle du marché
            if (b.Position.DistanceTo(_marketLocation) < 5.0f)
            {
                b.Delete();
                continue; // On passe au suivant
            }

            // Optionnel : Si tu veux être sûr de virer ceux des autres emplacements possibles
            foreach (Vector3 pos in _blackMarketLocations)
            {
                if (b.Position.DistanceTo(pos) < 5.0f)
                {
                    b.Delete();
                    break;
                }
            }
        }

// 2. Nettoyage du Blanchiment (Sprite Lester + RougeLight)
if (b.Sprite == BlipSprite.Lester && b.Color == BlipColor.RedLight)
{
    // On vérifie si ce blip est proche d'un des points de la liste
    foreach (var point in _launderingPoints)
    {
        if (b.Position.DistanceTo(point.Position) < 5.0f)
        {
            b.Delete();
            break; // On a trouvé le point, on peut quitter la boucle foreach
        }
    }
}
        
        // 3. Nettoyage du Stash (Sprite Safehouse + Jaune)
        // Attention à ne pas supprimer la vraie maison de Franklin (qui est blanche/verte par défaut)
        if (b.Sprite == BlipSprite.Safehouse && b.Color == BlipColor.Yellow)
        {
             // On supprime sans pitié car seul ton mod met des safehouses jaunes
             b.Delete();
        }
    }
    }
    }
}
