using System;
using GTA;
using GTA.Math;

namespace CarDealerShipMod
{
    public static class Utils
    {
        public static readonly Random Rng = new Random();

        public static float DistanceToSquared(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        public static float GetCorrectedGroundHeight(Vector3 position)
        {
            RaycastResult raycast = World.Raycast(position + new Vector3(0, 0, 50), position + new Vector3(0, 0, -50), IntersectFlags.Everything);
            if (raycast.DidHit)
            {
                return raycast.HitPosition.Z;
            }

            return position.Z;
        }

        public static bool IsVehicleStolen(Vehicle vehicle)
        {
            return vehicle.Driver == Game.Player.Character && !vehicle.IsPersistent;
        }

        public static int CalculatePriceBasedOnTypeAndDamage(Vehicle vehicle, int baseCarPrice)
        {
            int basePrice = baseCarPrice;
            float classMultiplier = GetClassMultiplier(vehicle);

            float engineHealth = vehicle.EngineHealth;
            if (engineHealth < 0) engineHealth = 0;

            float bodyHealth = vehicle.BodyHealth;
            if (bodyHealth < 0) bodyHealth = 0;

            float overallHealth = (engineHealth + bodyHealth) / 2;
            float healthPercentage = overallHealth / 1000.0f;

            int pimpValue = CalculatePimpValue(vehicle);

            int finalPrice = (int)((basePrice + pimpValue) * classMultiplier * healthPercentage);
            finalPrice = Math.Max(finalPrice, 1500);
            return finalPrice;
        }

        private static int CalculatePimpValue(Vehicle vehicle)
        {
            int pimpValue = 0;

            if (vehicle.Mods[VehicleModType.Engine].Index != -1)
            {
                pimpValue += 5000;
            }

            if (vehicle.Mods[VehicleModType.Brakes].Index != -1)
            {
                pimpValue += 3000;
            }

            if (vehicle.Mods[VehicleModType.Transmission].Index != -1)
            {
                pimpValue += 4000;
            }

            if (vehicle.Mods[VehicleToggleModType.Turbo].IsInstalled)
            {
                pimpValue += 6000;
            }

            if (vehicle.Mods.HasNeonLights)
            {
                pimpValue += 2000;
            }

            if (vehicle.Mods[VehicleModType.Spoilers].Index != -1)
            {
                pimpValue += 1500;
            }

            return pimpValue;
        }

        private static float GetClassMultiplier(Vehicle vehicle)
        {
            VehicleClass vehicleClass = vehicle.ClassType;

            switch (vehicleClass)
            {
                case VehicleClass.Super:
                    return 5.0f;
                case VehicleClass.Sports:
                    return 1.5f;
                case VehicleClass.SUVs:
                    return 1.2f;
                case VehicleClass.OffRoad:
                    return 1.1f;
                case VehicleClass.Vans:
                    return 0.8f;
                case VehicleClass.Compacts:
                    return 0.7f;
                default:
                    return 1.0f;
            }
        }
    }
}
