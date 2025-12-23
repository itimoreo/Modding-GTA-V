using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace CarDealerShipMod
{
    // On définit les étapes claires de la mission
    public enum MissionState
    {
        None,           // Pas de mission
        GoToVehicle,    // Aller chercher la voiture
        Delivering      // Conduire au point de livraison
    }

    public class MissionManager
    {
        // --- Variables d'état ---
        public MissionState CurrentState { get; private set; } = MissionState.None;
        
        public event Action<int> MissionCompleted;

        private Vehicle _targetVehicle;
        private Blip _missionBlip; // Un seul blip qu'on recycle (plus propre)
        
        private readonly List<VehicleHash> _availableVehicles;
        private readonly List<Vector3> _spawnLocations;
        private readonly Vector3 _deliveryLocation;
        private readonly Random _rng = new Random();

        // Constructeur
        public MissionManager(List<VehicleHash> availableVehicles, List<Vector3> spawnLocations, Vector3 deliveryLocation)
        {
            _availableVehicles = availableVehicles;
            _spawnLocations = spawnLocations;
            _deliveryLocation = deliveryLocation;
        }

        // --- Méthodes Publiques ---

        public void StartMission()
        {
            if (CurrentState != MissionState.None)
            {
                Notification.Show("~r~Une mission est déjà en cours !");
                return;
            }

            // 1. Choix aléatoire
            var vehicleModel = _availableVehicles[_rng.Next(_availableVehicles.Count)];
            var spawnPos = _spawnLocations[_rng.Next(_spawnLocations.Count)];

            // 2. Création du véhicule
            _targetVehicle = World.CreateVehicle(vehicleModel, spawnPos);
            
            if (_targetVehicle == null)
            {
                Notification.Show("~r~Erreur: Impossible de faire apparaître le véhicule.");
                return;
            }

            _targetVehicle.IsPersistent = true;
            _targetVehicle.PlaceOnGround();
            //_targetVehicle.LockStatus = VehicleLockStatus.Locked; // Véhicule verrouillé pour le réalisme
            
            // 3. Création du Blip (Cible)
            CreateBlip(spawnPos, BlipSprite.PersonalVehicleCar, BlipColor.Yellow, "Véhicule Cible");

            // 4. Mise à jour de l'état
            CurrentState = MissionState.GoToVehicle;
            Notification.Show($"~y~Mission : Vole la {_targetVehicle.DisplayName}.");
        }

        public void Update()
        {
            // Si pas de mission, on s'assure qu'il ne reste aucun artefact (blip/route/véhicule persistant)
            if (CurrentState == MissionState.None)
            {
                EnsureNoMissionArtifacts();
                return;
            }

            // Sécurité : Si le véhicule a été détruit
            if (_targetVehicle == null || !_targetVehicle.Exists() || _targetVehicle.IsDead)
            {
                Notification.Show("~r~Le véhicule cible a été détruit ! Mission échouée.");
                CleanupMission();
                return;
            }

            Ped player = Game.Player.Character;

            // --- Logique selon l'étape ---
            switch (CurrentState)
            {
                case MissionState.GoToVehicle:
                    // Vérifie si le joueur est monté dans LE bon véhicule
                    if (player.IsInVehicle(_targetVehicle))
                    {
                        // Transition vers l'étape Livraison
                        CurrentState = MissionState.Delivering;
                        
                        // Mise à jour du Blip vers la destination
                        CreateBlip(_deliveryLocation, BlipSprite.Garage, BlipColor.Green, "Point de Livraison");
                        
                        Notification.Show("~g~Véhicule récupéré ! ~w~Livre-le au garage indiqué.");
                    }
                    break;

                case MissionState.Delivering:
                    // Vérifie si le joueur est proche de la livraison
                    // DistanceToSquared est plus rapide pour le processeur
                    if (_targetVehicle.Position.DistanceToSquared(_deliveryLocation) < 25.0f) // ~5 mètres
                    {
                         // Vérifie que le joueur est toujours dedans (optionnel, mais mieux)
                        if (player.IsInVehicle(_targetVehicle))
                        {
                            CompleteMission();
                        }
                        else
                        {
                            GTA.UI.Screen.ShowHelpTextThisFrame("Montez dans le véhicule pour valider la livraison.");
                        }
                    }
                    break;
            }
        }

        public void ForceReset()
        {
            CleanupMission();
            Notification.Show("~r~Mission annulée.");
        }

        // --- Méthodes Privées (Internes) ---

        private void CompleteMission()
        {
            const int reward = 120000;

            Notification.Show("~g~Mission réussie ! ~y~+$120,000");
            MissionCompleted?.Invoke(reward);
            
            // Penser à ajouter l'argent via ton EconomyManager ici si tu l'as lié
            // ex: _economyManager.AddDirtyMoney(120000); 
            
            CleanupMission();
        }

        private void CleanupMission()
        {
            // 1. Nettoyer le Blip
            if (_missionBlip != null && _missionBlip.Exists())
            {
                _missionBlip.ShowRoute = false;
                _missionBlip.Delete();
                _missionBlip = null;
            }

            // 2. Nettoyer le Véhicule
            if (_targetVehicle != null && _targetVehicle.Exists())
            {
                _targetVehicle.IsPersistent = false; // Rend le contrôle au jeu pour le despawn naturel
                _targetVehicle.Delete();
                _targetVehicle = null;
            }

            // 3. Remettre l'état à zéro
            CurrentState = MissionState.None;
        }

        private void EnsureNoMissionArtifacts()
        {
            if (_missionBlip != null && _missionBlip.Exists())
            {
                _missionBlip.ShowRoute = false;
                _missionBlip.Delete();
                _missionBlip = null;
            }

            if (_targetVehicle != null && _targetVehicle.Exists())
            {
                _targetVehicle.IsPersistent = false;
                _targetVehicle.MarkAsNoLongerNeeded();
                _targetVehicle = null;
            }
        }

        private void CreateBlip(Vector3 pos, BlipSprite sprite, BlipColor color, string name)
        {
            // Supprime l'ancien blip s'il existe (évite les doublons)
            if (_missionBlip != null && _missionBlip.Exists())
            {
                _missionBlip.ShowRoute = false;
                _missionBlip.Delete();
            }

            _missionBlip = World.CreateBlip(pos);
            _missionBlip.Sprite = sprite;
            _missionBlip.Color = color;
            _missionBlip.Name = name;
            _missionBlip.ShowRoute = true; // Active le GPS jaune/vert
        }
    }
}