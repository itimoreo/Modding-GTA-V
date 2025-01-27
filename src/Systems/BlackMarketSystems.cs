using GTA;
using GTA.Math;
using GTA.UI;

public class BlackMarketSystem
{
    private Vector3 marketLocation = new Vector3(170.8461f, 6359.0230f, 31.4532f);
    private bool isPlayerInMarket = false;

    public void Initialize()
    {
        BlipManager.CreateBlip(marketLocation, "Black Market", BlipSprite.GunCar, BlipColor.Red);
    }

    public void Update()
    {
        Vector3 playerPosition = Game.Player.Character.Position;
        isPlayerInMarket = playerPosition.DistanceTo(marketLocation) <= 5.0f;

        if (isPlayerInMarket)
        {
            NotificationManager.ShowNotification("~y~Press ~b~E~y~ to interact with the Black Market.");
        }
    }

    public void HandleKeyPress()
    {
        if (isPlayerInMarket)
        {
            NotificationManager.ShowNotification("~g~Selling vehicle...");
            // Logique de vente
        }
    }
}
