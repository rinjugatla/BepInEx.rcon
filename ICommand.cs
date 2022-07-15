using BepInEx;

namespace Rcon
{
    internal interface ICommand
    {
        void SetOwner(BaseUnityPlugin owner);
        string OnCommand(string[] args);
    }
}
