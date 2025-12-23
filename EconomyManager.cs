using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace CarDealerShipMod
{
    public class EconomyManager
    {
        private readonly string _saveFilePath;
        private readonly int _maxCarriedDirtyMoney;

        private int _dirtyMoneyFranklin;
        private int _dirtyMoneyMichael;
        private int _dirtyMoneyTrevor;

        private int _storedDirtyMoneyFranklin;
        private int _storedDirtyMoneyMichael;
        private int _storedDirtyMoneyTrevor;

        private int _blackMarketStoredMoney;

        private readonly Dictionary<PedHash, List<Vector3>> _characterStashLocations;
        private readonly float _stashRadius;

        public EconomyManager(string saveFilePath, int maxCarriedDirtyMoney, float stashRadius, Dictionary<PedHash, List<Vector3>> characterStashLocations)
        {
            _saveFilePath = saveFilePath;
            _maxCarriedDirtyMoney = maxCarriedDirtyMoney;
            _stashRadius = stashRadius;
            _characterStashLocations = characterStashLocations;
        }

        public int MaxCarriedDirtyMoney => _maxCarriedDirtyMoney;

        public int BlackMarketStoredMoney
        {
            get => _blackMarketStoredMoney;
            set => _blackMarketStoredMoney = value;
        }

        public ref int GetPlayerDirtyMoney()
        {
            switch ((uint)Game.Player.Character.Model.Hash)
            {
                case (uint)PedHash.Michael:
                    return ref _dirtyMoneyMichael;
                case (uint)PedHash.Franklin:
                    return ref _dirtyMoneyFranklin;
                case (uint)PedHash.Trevor:
                    return ref _dirtyMoneyTrevor;
                default:
                    return ref _dirtyMoneyMichael;
            }
        }

        public ref int GetPlayerStashMoney()
        {
            switch ((uint)Game.Player.Character.Model.Hash)
            {
                case (uint)PedHash.Michael:
                    return ref _storedDirtyMoneyMichael;
                case (uint)PedHash.Franklin:
                    return ref _storedDirtyMoneyFranklin;
                case (uint)PedHash.Trevor:
                    return ref _storedDirtyMoneyTrevor;
                default:
                    return ref _storedDirtyMoneyMichael;
            }
        }

        public Vector3 GetCurrentStash()
        {
            PedHash playerModel = (PedHash)Game.Player.Character.Model.Hash;

            if (playerModel == PedHash.Franklin)
                return GetFranklinStash();
            if (playerModel == PedHash.Michael)
                return _characterStashLocations[PedHash.Michael][0];
            if (playerModel == PedHash.Trevor)
            {
                Vector3 playerPos = Game.Player.Character.Position;
                float distCaravane = playerPos.DistanceTo(_characterStashLocations[PedHash.Trevor][0]);
                float distStripClub = playerPos.DistanceTo(_characterStashLocations[PedHash.Trevor][1]);
                return (distCaravane < distStripClub) ? _characterStashLocations[PedHash.Trevor][0] : _characterStashLocations[PedHash.Trevor][1];
            }

            return new Vector3();
        }

        private Vector3 GetFranklinStash()
        {
            Vector3 franklinPosition = Game.Player.Character.Position;

            float distanceToOldHouse = franklinPosition.DistanceTo(_characterStashLocations[PedHash.Franklin][0]);
            float distanceToNewHouse = franklinPosition.DistanceTo(_characterStashLocations[PedHash.Franklin][1]);

            return (distanceToOldHouse < distanceToNewHouse)
                ? _characterStashLocations[PedHash.Franklin][0]
                : _characterStashLocations[PedHash.Franklin][1];
        }

        public bool TryStoreDirtyMoney(int amount)
        {
            if (amount <= 0)
            {
                Notification.Show("~r~Invalid amount selected!");
                return false;
            }

            Vector3 stashLocation = GetCurrentStash();

            if (Game.Player.Character.Position.DistanceTo(stashLocation) < _stashRadius)
            {
                ref int playerDirtyMoney = ref GetPlayerDirtyMoney();
                ref int playerStashMoney = ref GetPlayerStashMoney();

                if (playerDirtyMoney >= amount)
                {
                    playerStashMoney += amount;
                    playerDirtyMoney -= amount;
                    Save();
                    Notification.Show($"~g~You stored ${amount} in your personal stash! New balance: ${playerStashMoney}");
                    return true;
                }

                Notification.Show("~r~Not enough dirty money to store!");
                return false;
            }

            Notification.Show("~r~You are not at your stash location!");
            return false;
        }

        public bool TryWithdrawDirtyMoney(int amount)
        {
            if (amount <= 0)
            {
                Notification.Show("~r~Invalid amount selected!");
                return false;
            }

            Vector3 stashLocation = GetCurrentStash();

            if (Game.Player.Character.Position.DistanceTo(stashLocation) < _stashRadius)
            {
                ref int playerDirtyMoney = ref GetPlayerDirtyMoney();
                ref int playerStashMoney = ref GetPlayerStashMoney();

                if (playerStashMoney >= amount)
                {
                    int availableSpace = _maxCarriedDirtyMoney - playerDirtyMoney;
                    int amountToWithdraw = Math.Min(amount, availableSpace);

                    playerStashMoney -= amountToWithdraw;
                    playerDirtyMoney += amountToWithdraw;

                    Save();
                    Notification.Show($"~g~You withdrew ${amountToWithdraw} from your stash! New balance: ${playerStashMoney}");

                    if (amountToWithdraw < amount)
                    {
                        Notification.Show("~y~You couldn't withdraw the full amount due to carry limit.");
                    }

                    return true;
                }

                Notification.Show("~r~Not enough money in your stash!");
                return false;
            }

            Notification.Show("~r~You are not at your stash location!");
            return false;
        }

        public bool TryLaunderingMoney(Vector3 launderingLocation)
        {
            if (Game.Player.Character.Position.DistanceTo(launderingLocation) < 5.0f)
            {
                if (Game.Player.WantedLevel > 0)
                {
                    Notification.Show("~r~You must lose your wanted level before laundering dirty money!");
                    return false;
                }

                ref int playerDirtyMoney = ref GetPlayerDirtyMoney();

                if (playerDirtyMoney > 0)
                {
                    if (IsDetectedDuringLaundering())
                    {
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 3, false);
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
                        Notification.Show("~r~Laundry detected! Hostiles incoming!");
                        return false;
                    }

                    int convertedMoney = (int)(playerDirtyMoney * 0.8);
                    int fee = playerDirtyMoney - convertedMoney;
                    Game.Player.Money += convertedMoney;
                    playerDirtyMoney = 0;
                    Save();
                    Notification.Show($"~b~Laundry successful! ${convertedMoney} laundered, ${fee} launder fee.");
                    return true;
                }

                Notification.Show("~r~You don't have any dirty money to launder!");
                return false;
            }

            Notification.Show("~r~You are not at the laundering location!");
            return false;
        }

        private bool IsDetectedDuringLaundering()
        {
            int chance = Utils.Rng.Next(0, 101);
            ref int playerDirtyMoneyDetect = ref GetPlayerDirtyMoney();
            int detectionRisk = Math.Min(playerDirtyMoneyDetect / 500, 50);
            return chance <= detectionRisk;
        }

        public bool TryAddDirtyMoneyWithLimit(int amount, out int storedOverflow)
        {
            storedOverflow = 0;
            if (amount <= 0)
                return true;

            ref int playerDirtyMoney = ref GetPlayerDirtyMoney();
            int availableSpace = _maxCarriedDirtyMoney - playerDirtyMoney;

            if (availableSpace >= amount)
            {
                playerDirtyMoney += amount;
                return true;
            }

            playerDirtyMoney = _maxCarriedDirtyMoney;
            storedOverflow = amount - availableSpace;
            _blackMarketStoredMoney += storedOverflow;
            return false;
        }

        public int GetAvailableCarrySpace()
        {
            ref int playerDirtyMoney = ref GetPlayerDirtyMoney();
            return _maxCarriedDirtyMoney - playerDirtyMoney;
        }

        public void Save()
        {
            try
            {
                string data = $"{_dirtyMoneyFranklin},{_dirtyMoneyMichael},{_dirtyMoneyTrevor}," +
                              $"{_storedDirtyMoneyFranklin},{_storedDirtyMoneyMichael},{_storedDirtyMoneyTrevor}," +
                              $"{_blackMarketStoredMoney}";

                System.IO.File.WriteAllText(_saveFilePath, data);
                Notification.Show("~g~Dirty money and stash saved!");
            }
            catch (Exception ex)
            {
                Notification.Show($"~r~Error saving dirty money: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (System.IO.File.Exists(_saveFilePath))
                {
                    string[] values = System.IO.File.ReadAllText(_saveFilePath).Split(',');

                    if (values.Length == 7)
                    {
                        _dirtyMoneyFranklin = int.Parse(values[0]);
                        _dirtyMoneyMichael = int.Parse(values[1]);
                        _dirtyMoneyTrevor = int.Parse(values[2]);

                        _storedDirtyMoneyFranklin = int.Parse(values[3]);
                        _storedDirtyMoneyMichael = int.Parse(values[4]);
                        _storedDirtyMoneyTrevor = int.Parse(values[5]);

                        _blackMarketStoredMoney = int.Parse(values[6]);

                        Notification.Show($"~g~Loaded Money: F=${_storedDirtyMoneyFranklin}, M=${_storedDirtyMoneyMichael}, T=${_storedDirtyMoneyTrevor}");
                        return;
                    }

                    Notification.Show("~r~Error: Incorrect save file format. Resetting values.");
                    Reset();
                    return;
                }

                Reset();
            }
            catch (Exception ex)
            {
                Notification.Show($"~r~Error loading dirty money: {ex.Message}");
                Reset();
            }
        }

        public void Reset()
        {
            _dirtyMoneyFranklin = 0;
            _dirtyMoneyMichael = 0;
            _dirtyMoneyTrevor = 0;

            _storedDirtyMoneyFranklin = 0;
            _storedDirtyMoneyMichael = 0;
            _storedDirtyMoneyTrevor = 0;

            _blackMarketStoredMoney = 0;
        }

        public void DrawDirtyMoneyHud()
        {
            string text = $"Dirty Money: ${GetPlayerDirtyMoney()}";

            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 0, 0, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.9f, 0.3f);
        }
    }
}
