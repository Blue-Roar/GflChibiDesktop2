using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDTLPanel
{
    internal interface IPanelControl : INotifyPropertyChanged
    {
    }

    internal interface ISaveableControl : IPanelControl
    {
        void Save(ManagedIpc.IpcWriter writer);
    }
}
