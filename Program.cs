using System;
using System.Drawing;
using System.Collections.Generic;
using GTA;
using GTA.UI;
using GTA.Native;
using GTA.Math;
using iFruitAddon2; // Ensure this library is referenced in your project
using LemonUI;
using LemonUI.Menus;

public class CarDealership : Script
{
    private ObjectPool menuPool;
    private ObjectPool blackMarketMenuPool;
    private NativeMenu stashMenu;
    private NativeMenu confirmMenu;
    private NativeMenu blackMarketMenu;

    private readonly CustomiFruit _ifruit = new CustomiFruit();
    private Vector3 marketLocation = new Vector3(170.8461f, 6359.0230f, 31.4532f); // Localisation du marché noir
    private readonly List<Vector3> blackMarketLocations = new List<Vector3>
{
    new Vector3(-335.5068f, -2717.748f, 6.0003f), // Exemple de localisation 1
    new Vector3(-1583.057f, 5156.8511f, 19.6654f),       // Exemple de localisation 2
    new Vector3(3716.1060f, 4525.1758f, 21.6604f),        // Exemple de localisation 3
    new Vector3(-560.7960f, 302.1563f, 83.1715f),        // Exemple de localisation 4
};
    private Vector3 launderingLocation = new Vector3(640.3064f, 2780.3027f, 41.9824f); // Localisation du blanchiment d'argent
    private float marketRadius = 5.0f; // Rayon d'interaction
    private bool isPlayerInMarket = false;
    private bool isPlayerInLaundering = false;
    private DateTime notificationStartTime;
    private bool isNotificationVisible = false;
    private int baseCarPrice = 25000; // Prix de base pour les voitures
    //private int baseSuperCarPrice = 150000; // Prix de base pour les voitures de luxe
    private Blip marketBlip;
    private Blip launderingBlip;
    private int dirtyMoney = 0;
    private int dirtyMoneyFranklin, dirtyMoneyTrevor, dirtyMoneyMichael = 0;
    private string saveFilePath = "scripts\\save.txt";
    private bool playerHasSoldCar = false;

    // For the vehicle theft mission --------
    private VehicleTheftMission activeMission;
    private Vehicle targetVehicle;
    private readonly List<VehicleHash> availableVehicles = new List<VehicleHash>
{
    VehicleHash.Adder, // Véhicule de type supercar
    VehicleHash.T20,   // Véhicule de type supercar rapide
    VehicleHash.Zentorno, // Véhicule emblématique de sport
    VehicleHash.Osiris, // Osiris
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

    private readonly List<Vector3> spawnLocations = new List<Vector3>
{
    new Vector3(-1152.933f, -2734.399f, 13.9526f), // Localisation près d'un entrepôt
    new Vector3(217.603f, -800.745f, 30.655f), // Localisation en centre-ville
    new Vector3(-1034.553f, -491.692f, 36.214f), // Localisation proche des docks
    new Vector3(1210.756f, 2658.333f, 37.899f), // Localisation dans une zone industrielle
    new Vector3(-205.123f, 6218.567f, 31.489f) // Localisation rurale éloignée
};
    private Blip targetVehicleBlip;
    private Blip deliveryBlip;
    private int maxCarriedDirtyMoney = 350000; // Montant maximum d'argent sale pouvant être transporté
    private int storedDirtyMoney = 0; // Argent sale stocké dans le coffre
    private int blackMarketStoredMoney = 0; // Argent sale stocké dans le coffre du marché noir
    private Vector3 stashLocation = new Vector3(-826.8317f, 180.2720f, 71.4480f); // Localisation du coffre
    private float stashRadius = 2.0f;
    private Blip stashBlip;

    public CarDealership()
    {
        Tick += OnTick; // Appelé à chaque frame
        KeyDown += OnKeyDown; // Détecte les touches appuyées

        LoadDirtyMoney(); // Charge l'argent sale depuis un fichier
        UpdateBlips(); // Crée les marqueurs sur la carte
        RemoveBlips(); // Supprime les marqueurs sur la carte
        InitializePhone(); // Initialise le téléphone
        InitializeStash(); // Initialise le coffre
        InitializeStashMenu(); // Initialise le menu du coffre
        InitializeBlackMarketMenu(); // Initialise le menu du marché noir
    }

    private void OnTick(object sender, System.EventArgs e)
    {
        CheckMarketLocation(); // Vérifie la position du joueur par rapport au marché noir
        CheckLaunderingLocation(); // Vérifie la position du joueur par rapport au blanchiment d'argent
        DrawMarketLocation(); // Dessine un marqueur visible
        DrawLaunderingLocation(); // Dessine un marqueur visible
        DrawStashLocation(); // Dessine un marqueur visible
        HandleNotificationTimeout(); // Gère l'affichage temporaire des notifications
        DisplayDirtyMoney(); // Affiche l'argent sale
        _ifruit.Update(); // Met à jour le téléphone
        CheckVehicleTheft(); // Vérifie si le joueur a volé un véhicule
        DeliverStolenVehicle();
        menuPool.Process(); // Met à jour le menu LemonUI
        blackMarketMenuPool.Process(); // Met à jour le menu LemonUI


    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.E)
        {

            if (Game.Player.Character.Position.DistanceTo(launderingLocation) < 5.0f)
            {
                TryLaunderingMoney();
                UpdateStashMenu();
            }
            else if (Game.Player.Character.Position.DistanceTo(stashLocation) < stashRadius)
            {
                stashMenu.Visible = true; // Ouvre le menu LemonUI
            }
            else if (isPlayerInMarket)
            {
                OpenBlackMarketMenu();
                //TrySellVehicle();
                UpdateStashMenu();
            }
            else
            {
                Notification.Show("~r~You are not in the correct location to perform this action!");
            }
        }
        if (e.KeyCode == System.Windows.Forms.Keys.T)
        {
            // Vérifie si une mission est active
            if (activeMission != null && activeMission.IsActive)
            {
                ForceResetMission(); // Réinitialise la mission manuellement
            }
        }

        // if (e.KeyCode == System.Windows.Forms.Keys.F7)
        // {
        //    dirtyMoney += 350000; // Déplace le marché noir à une nouvelle position
        // }
    }

    /// -------------- Black Market Menu Methods --------------

    private void InitializeBlackMarketMenu()
    {
        blackMarketMenuPool = new ObjectPool();
        blackMarketMenu = new NativeMenu("Black Market", "Sell stolen vehicles.");

        var vehicleInfo = new NativeItem("🚗 Vehicle: None");
        var vehiclePrice = new NativeItem("💰 Price: $0");
        var withdrawStoredMoney = new NativeItem($"💰 Withdraw Stored Money: ~g~ ${blackMarketStoredMoney}", "Take back your stored earnings.");
        var sellVehicle = new NativeItem("✔ Sell Vehicle");
        var cancel = new NativeItem("❌ Cancel");

        vehicleInfo.Enabled = false;
        vehiclePrice.Enabled = false;

        sellVehicle.Activated += (sender, args) => ConfirmVehicleSale();
        withdrawStoredMoney.Activated += (sender, args) => WithdrawBlackMarketMoney();
        cancel.Activated += (sender, args) => blackMarketMenu.Visible = false;

        blackMarketMenu.Add(vehicleInfo);
        blackMarketMenu.Add(vehiclePrice);
        blackMarketMenu.Add(withdrawStoredMoney);
        blackMarketMenu.Add(sellVehicle);
        blackMarketMenu.Add(cancel);

        blackMarketMenuPool.Add(blackMarketMenu);
    }

    private void OpenBlackMarketMenu()
    {
        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
        if (playerVehicle == null)
        {
            Notification.Show("~r~You must be in a vehicle to sell it!");
            return;
        }

        int salePrice = CalculatePriceBasedOnTypeAndDamage(playerVehicle);

        blackMarketMenu.Items[0].Title = $"🚗 Vehicle: {playerVehicle.DisplayName}";
        blackMarketMenu.Items[1].Title = $"💰 Price: ~g~ ${salePrice}";

        blackMarketMenu.Visible = true;
    }

    private void ConfirmVehicleSale()
    {
        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
        if (playerVehicle == null)
        {
            Notification.Show("~r~No vehicle detected!");
            return;
        }

        int salePrice = CalculatePriceBasedOnTypeAndDamage(playerVehicle);
        int availableSpace = maxCarriedDirtyMoney - dirtyMoney;

        if (availableSpace >= salePrice)
        {
            // Si tout l'argent peut être transporté
            GetPlayerDirtyMoney() += salePrice;
            SaveDirtyMoney();
            Notification.Show($"~g~Vehicle sold for ${salePrice}!");
        }
        else
        {
            // Stocke l'excédent au Black Market
            dirtyMoney = maxCarriedDirtyMoney; // Atteint la limite max
            blackMarketStoredMoney += (salePrice - availableSpace);
            blackMarketMenu.Items[blackMarketMenu.Items.Count - 1].Title = $"💰 Withdraw Stored Money: ${blackMarketStoredMoney}";
            SaveDirtyMoney();
            Notification.Show($"~g~Vehicle sold! You reached the money limit. ${salePrice - availableSpace} stored at Black Market.");
        }

        playerVehicle.Delete(); // Supprime le véhicule après la vente
        SaveDirtyMoney(); // Sauvegarde l'argent sale

        blackMarketMenu.Visible = false;
    }
    private void WithdrawBlackMarketMoney()
    {
        if (blackMarketStoredMoney <= 0)
        {
            Notification.Show("~r~No stored money available!");
            return;
        }

        int availableSpace = maxCarriedDirtyMoney - dirtyMoney;
        int amountToWithdraw = Math.Min(blackMarketStoredMoney, availableSpace);

        GetPlayerDirtyMoney() += amountToWithdraw;
        blackMarketStoredMoney -= amountToWithdraw;
        blackMarketMenu.Items[blackMarketMenu.Items.Count - 1].Title = $"💰 Withdraw Stored Money: ${blackMarketStoredMoney}";

        Notification.Show($"~g~You withdrew ${amountToWithdraw} from the Black Market!");

        SaveDirtyMoney();
    }



    /// -------------- Stash Menu Methods ----------------

    private void InitializeStashMenu()
    {
        menuPool = new ObjectPool();
        stashMenu = new NativeMenu("💰 Money Stash", "Manage your dirty money");

        var stashInfo = new NativeItem($"~r~Stash: ${storedDirtyMoney} | ~y~ Carried: ${dirtyMoney}");
        stashInfo.Enabled = false; // Désactiver l'interaction (juste une info)

        // Liste fixe des montants disponibles pour le dépôt/retrait
        var amountList = new List<int> { 100, 500, 1000, 5000, 8000, 10000, 15000, 35000, 50000, 100000 };
        var amountSelector = new NativeListItem<int>("Amount", amountList.ToArray());

        var depositItem = new NativeItem("Deposit", "Store dirty money in the stash.");
        var withdrawItem = new NativeItem("Withdraw", "Take dirty money from the stash.");

        depositItem.Activated += (sender, args) =>
        {
            ConfirmTransaction(true, amountSelector.SelectedItem);
        };

        withdrawItem.Activated += (sender, args) =>
        {
            ConfirmTransaction(false, amountSelector.SelectedItem);
        };

        stashMenu.Add(stashInfo);
        stashMenu.Add(amountSelector);
        stashMenu.Add(depositItem);
        stashMenu.Add(withdrawItem);
        menuPool.Add(stashMenu);
    }


    private void ConfirmTransaction(bool isDeposit, int amount)
    {
        if (amount <= 0)
        {
            Notification.Show("~r~Invalid amount selected!");
            return;
        }

        stashMenu.Visible = false; // Ferme le menu du stash avant d'afficher la confirmation

        string action = isDeposit ? "deposit" : "withdraw";
        string message = $"Are you sure you want to {action} ${amount}?";

        confirmMenu = new NativeMenu("Confirm Transaction", message);
        menuPool.Add(confirmMenu);

        var confirmItem = new NativeItem("✔ Confirm");
        var cancelItem = new NativeItem("❌ Cancel");

        confirmItem.Activated += (sender, args) =>
        {
            if (isDeposit)
                TryStoreDirtyMoney(amount);
            else
                TryWithdrawDirtyMoney(amount);

            UpdateStashMenu();
            confirmMenu.Visible = false;
            stashMenu.Visible = true;
        };

        cancelItem.Activated += (sender, args) =>
        {
            confirmMenu.Visible = false;
            stashMenu.Visible = true;
        };

        confirmMenu.Add(confirmItem);
        confirmMenu.Add(cancelItem);
        confirmMenu.Visible = true;
    }

    private void UpdateStashMenu()
    {
        stashMenu.Items[0].Title = $"💰 Stash: ${storedDirtyMoney} | Carried: ${dirtyMoney}";
    }



    /// -------------- Stash Methods --------------

    private void InitializeStash()
    {
        stashBlip = World.CreateBlip(stashLocation);
        stashBlip.Sprite = BlipSprite.Safehouse;
        stashBlip.Color = BlipColor.Yellow;
        stashBlip.Name = "Money Stash";
    }

    private void TryStoreDirtyMoney(int amount)
    {
        if (Game.Player.Character.Position.DistanceTo(stashLocation) < 5.0f)
        {
            if (dirtyMoney >= amount)
            {
                storedDirtyMoney += amount;
                dirtyMoney -= amount;
                Notification.Show($"~g~You stored ${amount} in the stash!");
                SaveDirtyMoney();
                UpdateStashMenu();
            }
            else
            {
                Notification.Show("~r~Not enough dirty money to store!");
            }
        }
        else
        {
            Notification.Show("~r~You are not at the stash location!");
        }
    }


    private void TryWithdrawDirtyMoney(int amount)
    {
        if (Game.Player.Character.Position.DistanceTo(stashLocation) < 5.0f)
        {
            if (storedDirtyMoney >= amount)
            {
                int availableSpace = maxCarriedDirtyMoney - dirtyMoney;
                int amountToWithdraw = Math.Min(amount, availableSpace);

                storedDirtyMoney -= amountToWithdraw;
                GetPlayerDirtyMoney() += amountToWithdraw;
                Notification.Show($"~g~You withdrew ${amountToWithdraw} from the stash!");
                SaveDirtyMoney();

                if (amountToWithdraw < amount)
                {
                    Notification.Show("~y~You couldn't withdraw the full amount due to carry limit.");
                }

                UpdateStashMenu();
            }
            else
            {
                Notification.Show("~r~Not enough money in stash!");
            }
        }
        else
        {
            Notification.Show("~r~You are not at the stash location!");
        }
    }



    private void TrySellVehicleWithMoneyLimit(Vehicle vehicle)
    {
        if (dirtyMoney >= maxCarriedDirtyMoney)
        {
            Notification.Show("~r~You are carrying too much dirty money! Store some before selling!");
            return;
        }

        int price = CalculatePriceBasedOnTypeAndDamage(vehicle);
        int maxEarnings = maxCarriedDirtyMoney - dirtyMoney;
        int actualEarnings = Math.Min(price, maxEarnings);
        GetPlayerDirtyMoney() += actualEarnings;
        vehicle.Delete();
        Notification.Show($"~g~Car sold for ${actualEarnings}!");
        SaveDirtyMoney();
        if (actualEarnings < price)
        {
            Notification.Show("~y~Some money was lost as you reached your carry limit.");
        }
    }




    /// ---------- Vehicle Theft Mission Methods ----------

    public class VehicleTheftMission
    {
        public string Name { get; set; }
        public VehicleHash TargetVehicle { get; set; }
        public Vector3 SpawnLocation { get; set; }
        public Vector3 DeliveryLocation { get; set; }
        public bool IsActive { get; set; }
    }

    private void StartVehicleTheftMission()
    {
        // Validation : empêche le démarrage d'une nouvelle mission si une est déjà active
        if (activeMission != null && activeMission.IsActive)
        {
            Notification.Show("~r~A mission is already active! Complete it before starting another.");
            return;
        }

        // Choix aléatoire d'un véhicule parmi la liste des véhicules disponibles
        Random random = new Random();
        VehicleHash selectedVehicle = availableVehicles[random.Next(availableVehicles.Count)];

        // Sélectionne une localisation de spawn aléatoire parmi les emplacements définis
        Vector3 selectedSpawnLocation = spawnLocations[random.Next(spawnLocations.Count)];

        // Initialisation de la mission avec les paramètres choisis
        activeMission = new VehicleTheftMission
        {
            Name = $"Steal the {selectedVehicle}", // Nom de la mission généré dynamiquement
            TargetVehicle = selectedVehicle, // Modèle du véhicule cible
            SpawnLocation = selectedSpawnLocation, // Localisation où le véhicule spawn
            DeliveryLocation = new Vector3(-560.7960f, 302.1563f, 83.1715f), // Localisation fixe pour la livraison
            IsActive = true // La mission est maintenant active
        };

        // Crée un blip sur la carte pour indiquer la localisation du véhicule cible
        targetVehicleBlip = World.CreateBlip(activeMission.SpawnLocation);
        targetVehicleBlip.Sprite = BlipSprite.PersonalVehicleCar; // Icône de voiture sur le radar
        targetVehicleBlip.Color = BlipColor.Yellow; // Couleur jaune pour identifier facilement
        targetVehicleBlip.Name = "Target Vehicle"; // Nom affiché pour le blip

        // Fait apparaître le véhicule cible à la localisation choisie
        targetVehicle = World.CreateVehicle(activeMission.TargetVehicle, activeMission.SpawnLocation);
        if (targetVehicle != null)
        {
            targetVehicle.IsPersistent = true; // Le véhicule reste dans le monde jusqu'à la fin de la mission
            targetVehicle.PlaceOnGround(); // Place le véhicule sur le sol pour éviter les bugs
            targetVehicle.LockStatus = VehicleLockStatus.CanBeBrokenIntoPersist; // Verrouille le véhicule pour simuler un vol
        }
        else
        {
            // En cas d'échec de spawn, annule la mission et informe le joueur
            Notification.Show("~r~Failed to spawn the target vehicle!");
            activeMission = null; // Réinitialisation de la mission
            return;
        }

        // Notifie le joueur du début de la mission
        Notification.Show($"~y~Mission started: {activeMission.Name}. Go to the location!");
    }

    private void CheckVehicleTheft()
    {
        if (activeMission == null || !activeMission.IsActive)
            return;

        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

        // Vérifie si le joueur est dans le véhicule cible
        if (playerVehicle != null && playerVehicle.Model.Hash == (int)activeMission.TargetVehicle)
        {
            Notification.Show("~g~You stole the target vehicle! Deliver it to the drop-off point.");

            // Ajoute un blip pour la localisation de livraison si pas déjà créé
            if (deliveryBlip == null)
            {
                deliveryBlip = World.CreateBlip(activeMission.DeliveryLocation);
                deliveryBlip.Sprite = BlipSprite.Garage;
                deliveryBlip.Color = BlipColor.Green;
                deliveryBlip.Name = "Delivery Point";
            }

            // Supprime le blip du véhicule cible
            if (targetVehicleBlip != null)
            {
                targetVehicleBlip.Delete();
                targetVehicleBlip = null;
            }

            activeMission.IsActive = false; // Désactive l'indicateur d'activité de la mission
        }
    }

    private void DeliverStolenVehicle()
    {
        // Vérifie si une mission est active et si le véhicule cible existe
        if (activeMission == null || targetVehicle == null)
        {
            return;
        }

        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

        // Vérifie si le joueur est dans le véhicule cible
        if (playerVehicle != null && playerVehicle == targetVehicle)
        {
            // Vérifie si le joueur est dans la zone de livraison
            float distanceToDelivery = playerVehicle.Position.DistanceTo(activeMission.DeliveryLocation);
            if (distanceToDelivery < 5.0f) // Rayon de 5 mètres
            {
                Notification.Show("~g~Vehicle delivered successfully! Mission completed. You earned ~y~$120,000!");

                // Supprime le véhicule cible et réinitialise la mission
                targetVehicle.Delete();
                targetVehicle = null;
                activeMission = null; // Réinitialise la mission
                GetPlayerDirtyMoney() += 120000; // Récompense pour la livraison
                SaveDirtyMoney(); // Sauvegarde l'argent sale

                // Supprime le blip de livraison s'il existe
                if (deliveryBlip != null)
                {
                    deliveryBlip.Delete();
                    deliveryBlip = null;
                }
            }
        }
        else if (playerVehicle != targetVehicle)
        {
            Notification.Show("~r~You must deliver the target vehicle!");
        }
    }

    private void ResetMissionState()
    {
        // Réinitialise l'état de la mission et supprime les entités persistantes
        if (targetVehicle != null)
        {
            targetVehicle.IsPersistent = false; // Rend le véhicule non persistant
            targetVehicle.Delete();
            targetVehicle = null;
        }

        if (targetVehicleBlip != null)
        {
            targetVehicleBlip.Delete();
            targetVehicleBlip = null;
        }

        if (deliveryBlip != null)
        {
            deliveryBlip.Delete();
            deliveryBlip = null;
        }

        activeMission = null;
    }

    // Appelle cette méthode pour réinitialiser manuellement en cas de bug ou d'arrêt forcé
    private void ForceResetMission()
    {
        Notification.Show("~r~Mission reset due to an error or manual override.");
        ResetMissionState();
    }



    /// -------------- Phone Methods --------------

    private void InitializePhone()
    {
        // Configure le contact du marché noir
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
        contactA.Answered += (contact) => OnBlackMarketCalled(contact);
        _ifruit.Contacts.Add(contactA);

        iFruitContact contactB = new iFruitContact("Spencer")
        {
            DialTimeout = 4000,
            Active = true,
            Icon = ContactIcon.Blank
        };
        contactB.Answered += (contact) => OnSpencerCalled(contact);
        _ifruit.Contacts.Add(contactB);
    }

    private void OnSpencerCalled(iFruitContact contact)
    {
        if (activeMission != null && activeMission.IsActive)
        {
            Notification.Show("~r~A mission is already active! Complete it before starting another.");
            return;
        }

        // Démarre une mission de vol
        StartVehicleTheftMission();
    }

    private void OnBlackMarketCalled(iFruitContact contact)
    {
        // Répond à l'appel du marché noir
        Notification.Show("~y~Black Market: How can I help you?");
        MoveBlackMarket(); // Déplace le marché noir
    }

    /// -------------- Black Market Methods --------------


    private void MoveBlackMarket()
    {
        Random random = new Random();
        int index = random.Next(blackMarketLocations.Count);

        // Sélectionne une localisation aléatoire
        Vector3 selectedLocation = blackMarketLocations[index];

        // Ajuste la hauteur avec le raycast
        float correctedHeight = GetCorrectedGroundHeight(selectedLocation);
        selectedLocation.Z = correctedHeight;

        // Met à jour la position du marché noir
        marketLocation = selectedLocation;
        UpdateBlips();

        Notification.Show("~g~The Black Market has moved to a new location!");
    }


    private void CheckMarketLocation()
    {
        Vector3 playerPosition = Game.Player.Character.Position;

        // Vérifie si le joueur est dans la zone
        if (playerPosition.DistanceTo(marketLocation) <= marketRadius)
        {
            if (!isPlayerInMarket)
            {
                isPlayerInMarket = true; // Le joueur entre dans la zone
                ShowMarketNotification();
            }
        }
        else
        {
            isPlayerInMarket = false; // Le joueur quitte la zone
        }
    }

    private void TrySellVehicle()
    {
        // Vérifie si le joueur est dans la zone du marché noir
        if (!isPlayerInMarket)
        {
            Notification.Show("~r~You must be at the black market to sell vehicles !");
            return;
        }
        else if (Game.Player.WantedLevel > 0)
        {
            Notification.Show("~r~You must lose your wanted level before selling vehicles !");
            return;
        }

        var player = Game.Player.Character;

        // Vérifie si le joueur est dans un véhicule
        if (player.IsInVehicle())
        {
            Vehicle vehicle = player.CurrentVehicle;

            // Vérifie si le véhicule est volé
            if (vehicle != null && IsVehicleStolen(vehicle))
            {
                TrySellVehicleWithMoneyLimit(vehicle); // Vendre le véhicule
            }
            else
            {
                // Si le véhicule n'est pas volé
                Notification.Show("~r~You can only sell stolen vehicles at the black market!");
            }
        }
        else
        {
            // Notification si le joueur est à pied
            Notification.Show("~r~You must be in a vehicle to sell it!");
        }
    }

    private void SellVehicle(Vehicle vehicle)
    {
        // Calcule le prix basé sur les dégâts et le type de véhicule
        int price = CalculatePriceBasedOnTypeAndDamage(vehicle);

        // Ajoute l'argent au joueur
        GetPlayerDirtyMoney() += price; // Ajoute l'argent au montant d'argent sale
        SaveDirtyMoney(); // Sauvegarde l'argent sale

        // Supprime la voiture après la vente
        vehicle.Delete();

        // Affiche une notification confirmant la vente
        Notification.Show($"~g~Car sold for ${price} !");

        // Déplace le marché noir à sa position initiale
        marketLocation = new Vector3(170.8461f, 6359.0230f, 31.4532f); // Position initiale
        UpdateBlips(); // Met à jour le blip pour refléter la nouvelle position
        Notification.Show("~y~The black market has returned to its initial location.");
    }


    private void DrawMarketLocation()
    {
        // Dessine un marqueur rouge sur l'emplacement du marché noir
        World.DrawMarker(
            MarkerType.VerticalCylinder,
            marketLocation,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(3.0f, 3.0f, 0.5f),
            Color.Red,
            false,
            false
        );
    }
    private void DrawStashLocation()
    {
        // Dessine un marqueur vert sur l'emplacement du coffre
        World.DrawMarker(
            MarkerType.VerticalCylinder,
            stashLocation,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(stashRadius, stashRadius, 0.5f),
            Color.Green,
            false,
            false
        );
    }


    /// ------------- Laundering Methods -------------

    // Vérifie si le joueur est dans la zone de blanchiment
    private void CheckLaunderingLocation()
    {
        Vector3 playerPosition = Game.Player.Character.Position;

        if (playerPosition.DistanceTo(launderingLocation) <= 5.0f) // Rayon de 5 mètres
        {
            // Notification indiquant que le joueur est dans la zone
            Notification.Show("~b~You are at the money laundering spot! Press ~y~E~b~ to launder dirty money.");
        }
    }

    private void DrawLaunderingLocation()
    {
        // Dessine un marqueur rouge sur l'emplacement du marché noir
        World.DrawMarker(
            MarkerType.VerticalCylinder,
            launderingLocation,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(3.0f, 3.0f, 0.5f),
            Color.Red,
            false,
            false
        );
    }

    private void TryLaunderingMoney()
{
    if (Game.Player.Character.Position.DistanceTo(launderingLocation) < 5.0f)
    {
        if (Game.Player.WantedLevel > 0)
        {
            Notification.Show("~r~You must lose your wanted level before laundering dirty money!");
            return;
        }

        ref int playerDirtyMoney = ref GetPlayerDirtyMoney();

        if (playerDirtyMoney > 0)
        {
            // Vérifie si le joueur est détecté
            if (IsDetectedDuringLaundering())
            {
                // Ajoute des étoiles de recherche
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 3, false); // 3 étoiles
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);

                // Affiche une notification d'alerte
                Notification.Show("~r~Laundry detected! Hostiles incoming!");
                return;
            }

            // Calcule l'argent blanchi avec un taux de conversion
            int convertedMoney = (int)(playerDirtyMoney * 0.8); // 80 % converti
            int fee = playerDirtyMoney - convertedMoney; // Perte due au blanchiment

            // Ajoute l'argent converti au joueur
            Game.Player.Money += convertedMoney;

            // Réinitialise l'argent sale pour ce personnage
            playerDirtyMoney = 0;
            SaveDirtyMoney(); // Sauvegarde l'argent sale

            // Affiche une notification confirmant le blanchiment
            Notification.Show($"~b~Laundry successful! ${convertedMoney} laundered, ${fee} launder fee.");
        }
        else
        {
            // Pas d'argent sale à blanchir
            Notification.Show("~r~You don't have any dirty money to launder!");
        }
    }
    else
    {
        Notification.Show("~r~You are not at the laundering location!");
    }
}


    private bool IsDetectedDuringLaundering()
    {
        Random random = new Random();
        int chance = random.Next(0, 101); // Génère un nombre aléatoire entre 0 et 100
        int detectionRisk = Math.Min(dirtyMoney / 500, 50); // Risque de détection basé sur l'argent sale 50% max
        return chance <= detectionRisk; // Retourne vrai si le joueur est détecté
    }

    /// -------------- Utility Methods --------------

    private ref int GetPlayerDirtyMoney()
    {
        switch ((uint)Game.Player.Character.Model.Hash)
        {
            case (uint)PedHash.Michael:
                return ref dirtyMoneyMichael;
            case (uint)PedHash.Franklin:
                return ref dirtyMoneyFranklin;
            case (uint)PedHash.Trevor:
                return ref dirtyMoneyTrevor;
            default:
                return ref dirtyMoneyMichael;
        }
    }



    private float GetCorrectedGroundHeight(Vector3 position)
    {
        RaycastResult raycast = World.Raycast(position + new Vector3(0, 0, 50), position + new Vector3(0, 0, -50), IntersectFlags.Everything);
        if (raycast.DidHit)
        {
            return raycast.HitPosition.Z;
        }
        return position.Z; // Si le raycast échoue, retourne la hauteur originale
    }


    private void ShowMarketNotification()
    {
        // Affiche une notification pour indiquer l'entrée dans la zone
        Notification.Show("~y~You are at the black market! Press ~b~E~y~ to sell a vehicle.");
        isNotificationVisible = true;
        notificationStartTime = DateTime.Now;
    }

    private void HandleNotificationTimeout()
    {
        // Vérifie si la notification doit disparaître
        if (isNotificationVisible && (DateTime.Now - notificationStartTime).TotalSeconds > 5)
        {
            isNotificationVisible = false;
        }
    }
    private bool IsVehicleStolen(Vehicle vehicle)
    {
        // Considère le véhicule comme volé si le joueur est le conducteur et que le véhicule n'est pas "propriétaire" du joueur
        return vehicle.Driver == Game.Player.Character && !vehicle.IsPersistent;
    }

    private int CalculatePimpValue(Vehicle vehicle)
    {
        int pimpValue = 0;

        // Vérifie si un moteur amélioré est installé
        if (vehicle.Mods[VehicleModType.Engine].Index != -1)
        {
            pimpValue += 5000; // Ajoute 5000$ pour un moteur amélioré
        }

        // Vérifie si des freins améliorés sont installés
        if (vehicle.Mods[VehicleModType.Brakes].Index != -1)
        {
            pimpValue += 3000; // Ajoute 3000$ pour des freins améliorés
        }

        // Vérifie si une transmission améliorée est installée
        if (vehicle.Mods[VehicleModType.Transmission].Index != -1)
        {
            pimpValue += 4000; // Ajoute 4000$ pour une transmission améliorée
        }

        // Vérifie si un turbo est installé
        if (vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled)
        {
            pimpValue += 6000; // Ajoute 6000$ pour un turbo
        }

        // Vérifie si des néons sont installés
        if (vehicle.Mods.HasNeonLights)
        {
            pimpValue += 2000; // Ajoute 2000$ pour des néons
        }

        // Vérifie si un spoiler est installé
        if (vehicle.Mods[VehicleModType.Spoilers].Index != -1)
        {
            pimpValue += 1500; // Ajoute 1500$ pour un spoiler
        }

        return pimpValue;
    }


    /// -------------- Save/Load Methods -------------- 


    private void SaveDirtyMoney()
    {
        try
        {
            string data = $"{dirtyMoneyFranklin},{dirtyMoneyMichael},{dirtyMoneyTrevor},{storedDirtyMoney},{blackMarketStoredMoney}";
            System.IO.File.WriteAllText(saveFilePath, data);
        }
        catch (Exception ex)
        {
            Notification.Show($"~r~Error saving dirty money: {ex.Message}");
        }
    }




    private void LoadDirtyMoney()
    {
        try
        {
            if (System.IO.File.Exists(saveFilePath))
            {
                string[] values = System.IO.File.ReadAllText(saveFilePath).Split(',');

                if (values.Length == 5)
                {
                    dirtyMoneyFranklin = int.Parse(values[0]);
                    dirtyMoneyMichael = int.Parse(values[1]);
                    dirtyMoneyTrevor = int.Parse(values[2]);
                    storedDirtyMoney = int.Parse(values[3]);
                    blackMarketStoredMoney = int.Parse(values[4]);
                }
                else
                {
                    Notification.Show("~r~Error: Incorrect save file format. Resetting values.");
                    dirtyMoneyFranklin = 0;
                    dirtyMoneyMichael = 0;
                    dirtyMoneyTrevor = 0;
                    storedDirtyMoney = 0;
                    blackMarketStoredMoney = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Notification.Show($"~r~Error loading dirty money: {ex.Message}");
            dirtyMoneyFranklin = 0;
            dirtyMoneyMichael = 0;
            dirtyMoneyTrevor = 0;
            storedDirtyMoney = 0;
            blackMarketStoredMoney = 0;
        }
    }




    /// ---------------------------------------------
    /// -------------- Display Methods --------------
    /// ---------------------------------------------

    private void DisplayDirtyMoney()
    {
        string text = $"Dirty Money: ${GetPlayerDirtyMoney()}"; // Texte à afficher

        // Configure la police, taille et couleur
        Function.Call(Hash.SET_TEXT_FONT, 0); // Police standard
        Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f); // Taille du texte
        Function.Call(Hash.SET_TEXT_COLOUR, 255, 0, 0, 255); // Couleur rouge (RGBA)
        Function.Call(Hash.SET_TEXT_OUTLINE); // Ajoute une bordure pour la lisibilité

        // Prépare le texte
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);

        // Affiche le texte à l'écran (position X, Y)
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.9f, 0.3f); // 0.9 = Droite, 0.1 = Haut
    }

    /// --------------- Blip Methods ---------------

    private void CreateMarketBlip()
    {
        // Supprime le blip précédent s'il existe
        if (marketBlip != null)
        {
            marketBlip.Delete();
        }

        // Crée un nouveau blip pour le marché noir
        marketBlip = World.CreateBlip(marketLocation);
        marketBlip.Sprite = BlipSprite.GunCar;
        marketBlip.Color = BlipColor.Red;
        marketBlip.Name = "Black Market";
        marketBlip.Scale = 1.0f;
    }


    private void CreateLaunderingBlip()
    {
        // Supprime le blip précédent s'il existe
        if (launderingBlip != null)
        {
            launderingBlip.Delete();
        }

        // Crée un nouveau blip pour le blanchiment d'argent
        launderingBlip = World.CreateBlip(launderingLocation);
        launderingBlip.Sprite = BlipSprite.Lester; // Icône par défaut (peut être remplacée par une autre)
        launderingBlip.Color = BlipColor.RedLight; // Couleur bleue pour différencier du marché noir
        launderingBlip.Name = "Money Laundering";
        launderingBlip.Scale = 1.0f;
    }
    private void UpdateBlips()
    {
        CreateMarketBlip();
        CreateLaunderingBlip();
    }

    // remove blips if there is more than 2 blip in the map
    private void RemoveBlips()
    {
        Blip[] blips = World.GetAllBlips();
        if (blips.Length > 2)
        {
            foreach (Blip blip in blips)
            {
                if (blip != marketBlip && blip != launderingBlip)
                {
                    blip.Delete();
                }
            }
        }
    }

    /// ---------------------------------------------------------
    /// -------------- Price Calculation Methods ----------------
    /// ---------------------------------------------------------

    private int CalculatePriceBasedOnTypeAndDamage(Vehicle vehicle)
    {
        int basePrice = baseCarPrice;

        // Détermine un multiplicateur basé sur la classe du véhicule
        float classMultiplier = GetClassMultiplier(vehicle);

        // Calcule l'état du véhicule (santé moyenne du moteur et de la carrosserie)
        float engineHealth = vehicle.EngineHealth;
        if (engineHealth < 0) engineHealth = 0;

        float bodyHealth = vehicle.BodyHealth;
        if (bodyHealth < 0) bodyHealth = 0;

        float overallHealth = (engineHealth + bodyHealth) / 2;

        // Normalise la santé pour un pourcentage
        float healthPercentage = overallHealth / 1000.0f;

        // Calcule la valeur des modifications (pimp)
        int pimpValue = CalculatePimpValue(vehicle);

        // Applique le multiplicateur de classe, la santé et la valeur des modifications
        int finalPrice = (int)((basePrice + pimpValue) * classMultiplier * healthPercentage);

        return finalPrice;
    }

    private float GetClassMultiplier(Vehicle vehicle)
    {
        // Récupère la classe du véhicule
        VehicleClass vehicleClass = vehicle.ClassType;

        // Définit un multiplicateur pour chaque type de véhicule
        switch (vehicleClass)
        {
            case VehicleClass.Super: // Voitures sportives de luxe
                return 5.0f; // 500 % du prix de base
            case VehicleClass.Sports: // Voitures sportives
                return 1.5f; // 150 % du prix de base
            case VehicleClass.SUVs: // SUV
                return 1.2f; // 120 % du prix de base
            case VehicleClass.OffRoad: // Véhicules tout-terrain
                return 1.1f; // 110 % du prix de base
            case VehicleClass.Vans: // Vans
                return 0.8f; // 80 % du prix de base
            case VehicleClass.Compacts: // Voitures compactes
                return 0.7f; // 70 % du prix de base
            default: // Autres véhicules
                return 1.0f; // Prix normal
        }
    }
}