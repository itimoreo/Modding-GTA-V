using GTA;
using GTA.Math;

public static class BlipManager
{
    public static void CreateBlip(Vector3 location, string name, BlipSprite sprite, BlipColor color)
    {
        Blip blip = World.CreateBlip(location);
        blip.Name = name;
        blip.Sprite = sprite;
        blip.Color = color;
        blip.Scale = 1.0f;
    }
}
