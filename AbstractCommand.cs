using BepInEx;

namespace Rcon
{
    public abstract class AbstractCommand : ICommand
    {
        protected BaseUnityPlugin Plugin { get; private set; }

        void ICommand.SetOwner(BaseUnityPlugin owner)
        {
            Plugin = owner;
        }

        public abstract string OnCommand(string[] args);
    }
}
