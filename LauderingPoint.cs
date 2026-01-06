using GTA.Math;

namespace CarDealerShipMod
{
    public class LaunderingPoint
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public float Fee { get; set; }           // Commission (ex: 0.1f pour 10%)
        public int MaxAmount { get; set; }      // Limite par transaction
        public float RiskMultiplier { get; set; } // Dangerosité du lieu
    }
}