using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace GameObjects
{
    public class MoneyList : ObservableCollection<int>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Dictionary<int, int> ValueToIndexMap = new Dictionary<int, int>();
        private int total;
        public int Total
        {
            get { return total; }
            set
            {
                total = value;
                OnPropertyChanged("Total");
            }
        }

        public MoneyList()
        {
            // Add six '0's representing each possible money value.
            // Add a seventh to represent the total.
            for ( int i = 0; i < 6; ++i )
            {
                this.Add(0);
            }

            // Populate the map.
            ValueToIndexMap.Add(1, 0);
            ValueToIndexMap.Add(2, 1);
            ValueToIndexMap.Add(3, 2);
            ValueToIndexMap.Add(4, 3);
            ValueToIndexMap.Add(5, 4);
            ValueToIndexMap.Add(10, 5);            
        }

        private void SumValues()
        {
            this.Total = 0;

            for ( int i = 0; i < 6; i++ )
            {
                this.Total += this[i] * ValueToIndexMap.First(keyVal => keyVal.Value == i).Key;
            }
        }


        public bool Remove( Card card )
        {
            this[ValueToIndexMap[card.Value]]--;

            // Re-sum the cards
            SumValues();

            return false;
        }

        public void Add( Card card )
        {
            this[ValueToIndexMap[card.Value]]++;            

            // Re-sum the cards
            SumValues();
        }

        // Create the OnPropertyChanged method to raise the event 
        protected void OnPropertyChanged( string name )
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if ( handler != null )
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }



    }
}
