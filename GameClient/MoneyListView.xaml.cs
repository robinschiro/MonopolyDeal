using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GameObjects;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for MoneyListView.xaml
    /// </summary>
    public partial class MoneyListView : UserControl
    {
        public MoneyListView(Player player)
        {
            InitializeComponent();

            this.DataContext = player;
        }
    }
}
