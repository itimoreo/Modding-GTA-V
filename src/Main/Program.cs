using GTA;

public class Program : Script
{
    private readonly BlackMarketSystem _blackMarketSystem = new BlackMarketSystem();
    private readonly LaunderingSystem _launderingSystem = new LaunderingSystem();
    private readonly VehicleTheftSystem _vehicleTheftSystem = new VehicleTheftSystem();

    public Program()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;

        _blackMarketSystem.Initialize();
        _launderingSystem.Initialize();
        _vehicleTheftSystem.Initialize();
    }

    private void OnTick(object sender, System.EventArgs e)
    {
        _blackMarketSystem.Update();
        _launderingSystem.Update();
        _vehicleTheftSystem.Update();
    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.E)
        {
            _blackMarketSystem.HandleKeyPress();
            _launderingSystem.HandleKeyPress();
        }
    }
}
