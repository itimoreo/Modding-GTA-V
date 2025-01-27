using GTA;
using GTA.Math;
using GTA.UI;
using System.Collections.Generic;

public class VehicleTheftSystem
{
    private VehicleTheftMission activeMission;
    private Vehicle targetVehicle;
    private readonly List<VehicleTheftMission> missions = new List<VehicleTheftMission>
    {
        new VehicleTheftMission
        {
            Name = "Steal a Supercar",
            TargetVehicle = VehicleHash.T20, // Exemple de véhicule cible
            SpawnLocation = new Vector3(-1152.933f, -2734.399f, 13.9526f),
            DeliveryLocation = new Vector3(-560.7960f, 302.1563f, 83.1715f)
        }
    };

    public void Initialize()
    {
        NotificationManager.ShowNotification("~g~Vehicle Theft System initialized.");
    }

    public void Update()
    {
        CheckVehicleTheft();
        DeliverStolenVehicle();
    }

    public void StartMission()
    {
        if (activeMission != null && activeMission.IsActive)
        {
            NotificationManager.ShowNotification("~r~A mission is already active!");
            return;
        }

        activeMission = missions[0]; // Prend la première mission
        activeMission.IsActive = true;

        // Crée un blip pour la localisation du véhicule
        BlipManager.CreateBlip(activeMission.SpawnLocation, "Target Vehicle", BlipSprite.PersonalVehicleCar, BlipColor.Yellow);

        // Fait apparaître le véhicule cible
        targetVehicle = World.CreateVehicle(activeMission.TargetVehicle, activeMission.SpawnLocation);
        if (targetVehicle != null)
        {
            targetVehicle.IsPersistent = true;
            NotificationManager.ShowNotification($"~y~Mission started: {activeMission.Name}. Go to the location!");
        }
        else
        {
            NotificationManager.ShowNotification("~r~Failed to spawn the target vehicle!");
        }
    }

    private void CheckVehicleTheft()
    {
        if (activeMission == null || !activeMission.IsActive)
            return;

        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

        // Vérifie si le joueur est dans le véhicule cible
        if (playerVehicle != null && playerVehicle.Model.Hash == (int)activeMission.TargetVehicle)
        {
            NotificationManager.ShowNotification("~g~You stole the target vehicle! Deliver it to the drop-off point.");
            BlipManager.CreateBlip(activeMission.DeliveryLocation, "Delivery Point", BlipSprite.Garage, BlipColor.Green);

            activeMission.IsActive = false; // Désactive la mission active
        }
    }

    private void DeliverStolenVehicle()
    {
        if (activeMission == null || targetVehicle == null)
            return;

        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

        if (playerVehicle != null && playerVehicle == targetVehicle)
        {
            float distanceToDelivery = playerVehicle.Position.DistanceTo(activeMission.DeliveryLocation);
            if (distanceToDelivery < 5.0f) // Rayon de 5 mètres
            {
                NotificationManager.ShowNotification("~g~Vehicle delivered successfully! Mission completed. You earned ~y~$120,000!");

                // Réinitialise la mission
                targetVehicle.Delete();
                targetVehicle = null;
                activeMission = null;

                // Ajoute la récompense
                int reward = 120000; // Récompense fixe
                FileManager.Save("scripts\\dirtyMoney.txt", reward.ToString()); // Ajoute l'argent au fichier (exemple simplifié)
            }
        }
    }
}

public class VehicleTheftMission
{
    public string Name { get; set; }
    public VehicleHash TargetVehicle { get; set; }
    public Vector3 SpawnLocation { get; set; }
    public Vector3 DeliveryLocation { get; set; }
    public bool IsActive { get; set; }
}
