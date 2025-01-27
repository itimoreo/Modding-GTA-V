using GTA;
using GTA.Math;
using GTA.UI;

public class LaunderingSystem
{
    private Vector3 launderingLocation = new Vector3(640.3064f, 2780.3027f, 41.9824f);

    public void Initialize()
    {
        BlipManager.CreateBlip(launderingLocation, "Money Laundering", BlipSprite.Lester, BlipColor.RedLight);
    }

    public void Update()
    {
        Vector3 playerPosition = Game.Player.Character.Position;
        if (playerPosition.DistanceTo(launderingLocation) <= 5.0f)
        {
            NotificationManager.ShowNotification("~b~Press ~y~E~b~ to launder money.");
        }
    }

    public void HandleKeyPress()
    {
        Vector3 playerPosition = Game.Player.Character.Position;
        if (playerPosition.DistanceTo(launderingLocation) <= 5.0f)
        {
            // Logique de blanchiment
            NotificationManager.ShowNotification("~g~Money laundered successfully.");
        }
    }
}
