﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using MahApps.Metro.Controls;

namespace Lithnet.Miiserver.AutoSync.UI.Windows
{
    /// <summary>
    /// Interaction logic for Connect.xaml
    /// </summary>
    public partial class ConnectDialog : MetroWindow
    {
        public ConnectDialog()
        {
            this.InitializeComponent();
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            ConnectDialogViewModel vm = (ConnectDialogViewModel)this.DataContext;

            if (vm.ValidateConnection())
            {
                this.DialogResult = true;
            }
        }
    }
}
