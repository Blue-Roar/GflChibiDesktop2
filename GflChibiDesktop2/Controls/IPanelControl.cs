using System.ComponentModel;

namespace GflChibiDesktop2
{
    internal interface IPanelControl : INotifyPropertyChanged
    {
    }

    internal interface ISaveableControl : IPanelControl
    {
        void Save(ManagedIpc.IpcWriter writer);
    }
}
