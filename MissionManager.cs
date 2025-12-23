using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.UI;

namespace CarDealerShipMod
{
    public class MissionManager
    {
        public class VehicleTheftMission
        {
            public string Name { get; set; }
            public VehicleHash TargetVehicle { get; set; }
            public Vector3 SpawnLocation { get; set; }
            public Vector3 DeliveryLocation { get; set; }
            public bool IsActive { get; set; }
        }

        private VehicleTheftMission _activeMission;
        private Vehicle _targetVehicle;

        private Blip _targetVehicleBlip;
        private Blip _deliveryBlip;

        private readonly List<VehicleHash> _availableVehicles;
        private readonly List<Vector3> _spawnLocations;

        private readonly Vector3 _deliveryLocation;

        public MissionManager(List<VehicleHash> availableVehicles, List<Vector3> spawnLocations, Vector3 deliveryLocation)
        {
            _availableVehicles = availableVehicles;
            _spawnLocations = spawnLocations;
            _deliveryLocation = deliveryLocation;
        }

        public bool HasActiveMission => _activeMission != null;

        public void StartVehicleTheftMission()
        {
            if (_activeMission != null && _activeMission.IsActive)
            {
                Notification.Show("~r~A mission is already active! Complete it before starting another.");
                return;
            }

            VehicleHash selectedVehicle = _availableVehicles[Utils.Rng.Next(_availableVehicles.Count)];
            Vector3 selectedSpawnLocation = _spawnLocations[Utils.Rng.Next(_spawnLocations.Count)];

            _activeMission = new VehicleTheftMission
            {
                Name = $"Steal the {selectedVehicle}",
                TargetVehicle = selectedVehicle,
                SpawnLocation = selectedSpawnLocation,
                DeliveryLocation = _deliveryLocation,
                IsActive = true
            };

            _targetVehicleBlip = World.CreateBlip(_activeMission.SpawnLocation);
            _targetVehicleBlip.Sprite = BlipSprite.PersonalVehicleCar;
            _targetVehicleBlip.Color = BlipColor.Yellow;
            _targetVehicleBlip.Name = "Target Vehicle";
            _targetVehicleBlip.ShowRoute = true;

            _targetVehicle = World.CreateVehicle(_activeMission.TargetVehicle, _activeMission.SpawnLocation);
            if (_targetVehicle != null)
            {
                _targetVehicle.IsPersistent = true;
                _targetVehicle.PlaceOnGround();
                _targetVehicle.LockStatus = VehicleLockStatus.CanBeBrokenIntoPersist;
            }
            else
            {
                Notification.Show("~r~Failed to spawn the target vehicle!");
                _activeMission = null;
                return;
            }

            Notification.Show($"~y~Mission started: {_activeMission.Name}. Go to the location!");
        }

        public bool TryHandleTheftStage()
        {
            if (_activeMission == null || !_activeMission.IsActive)
                return false;

            Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

            if (playerVehicle != null && playerVehicle.Model.Hash == (int)_activeMission.TargetVehicle)
            {
                Notification.Show("~g~You stole the target vehicle! Deliver it to the drop-off point.");

                if (_targetVehicleBlip != null)
                {
                    _targetVehicleBlip.Delete();
                    _targetVehicleBlip = null;
                }

                if (_deliveryBlip == null)
                {
                    _deliveryBlip = World.CreateBlip(_activeMission.DeliveryLocation);
                    _deliveryBlip.Sprite = BlipSprite.Garage;
                    _deliveryBlip.Color = BlipColor.Green;
                    _deliveryBlip.Name = "Delivery Point";
                    _deliveryBlip.ShowRoute = true;
                }

                _activeMission.IsActive = false;
                return true;
            }

            return false;
        }

        public bool TryDeliverStolenVehicle(out int reward)
        {
            reward = 0;

            if (_activeMission == null || _targetVehicle == null)
                return false;

            Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;

            if (playerVehicle != null && playerVehicle == _targetVehicle)
            {
                float distanceToDelivery = playerVehicle.Position.DistanceTo(_activeMission.DeliveryLocation);
                if (distanceToDelivery < 5.0f)
                {
                    reward = 120000;
                    Notification.Show("~g~Vehicle delivered successfully! Mission completed. You earned ~y~$120,000!");

                    _targetVehicle.Delete();
                    _targetVehicle = null;
                    _activeMission = null;

                    if (_deliveryBlip != null)
                    {
                        _deliveryBlip.Delete();
                        _deliveryBlip = null;
                    }

                    return true;
                }

                return false;
            }

            if (playerVehicle != _targetVehicle)
            {
                Notification.Show("~r~You must deliver the target vehicle!");
            }

            return false;
        }

        public void ForceResetMission()
        {
            Notification.Show("~r~Mission reset due to an error or manual override.");
            ResetMissionState();
        }

        public void ResetMissionState()
        {
            if (_targetVehicle != null)
            {
                _targetVehicle.IsPersistent = false;
                _targetVehicle.Delete();
                _targetVehicle = null;
            }

            if (_targetVehicleBlip != null)
            {
                _targetVehicleBlip.Delete();
                _targetVehicleBlip = null;
            }

            if (_deliveryBlip != null)
            {
                _deliveryBlip.Delete();
                _deliveryBlip = null;
            }

            _activeMission = null;
        }
    }
}
