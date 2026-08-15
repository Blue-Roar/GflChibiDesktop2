#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace GflChibiDesktop.Windows
{
    /// <summary>
    /// OpenWindow.xaml 的互动逻辑
    /// </summary>//初始数据集

    internal class ComponentModel : INotifyPropertyChanged
    {
        #region Data
        
        public int ComponentID { get; set; }
        public string ComponentName { get; set; }
        public int ParentID { get; set; }
        public int Level { get; set; }
        public string Header { get; set; }
        public string ToolTip { get; set; }
        public SolidColorBrush Foreground { get; set; }
        public string[] Tag { get; set; }
        public Visibility Visibility { get; set; }


        //readonly ReadOnlyCollection<ComponentModel> _children;
        readonly ComponentModel _parent = null;

        bool _isExpanded;
        bool _isSelected;

        #endregion // Data

        #region Constructors

        public List<ComponentModel> Children { get; set; }
        public ComponentModel()
        {
            Children = new List<ComponentModel>();
        }

        #endregion // Constructors

        #region Dummy Properties

        public string Name
        {
            get { return ComponentName; }
        }

        #endregion // Dummy Properties

        #region Presentation Members

        #region IsExpanded

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value != _isExpanded)
                {
                    _isExpanded = value;
                    this.OnPropertyChanged("IsExpanded");
                }

                // Expand all the way up to the root.
                if (_isExpanded && _parent != null)
                    _parent.IsExpanded = true;
            }
        }

        #endregion // IsExpanded

        #region IsSelected

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is selected.
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    this.OnPropertyChanged("IsSelected");
                }
            }
        }

        #endregion // IsSelected

        #region NameContainsText

        public bool NameContainsText(string text)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(this.Name))
                return false;

            return this.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) > -1;
        }

        #endregion // NameContainsText

        #region Parent

        public ComponentModel Parent
        {
            get { return _parent; }
        }

        #endregion // Parent

        #endregion // Presentation Members        

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion // INotifyPropertyChanged Members
    }
}
