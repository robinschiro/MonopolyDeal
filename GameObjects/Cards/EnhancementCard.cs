//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Windows.Controls;
//using System.Windows.Media.Imaging;
//using System.IO;
//using System.Collections;


//namespace GameObjects
//{    
//    public class EnhancementCard : Card
//    {
//        public enum EnhancementTypeEnum
//        {
//            HOUSE,
//            HOTEL
//        }

//        public EnhancementTypeEnum EnhancementType { get; set; }

//        public EnhancementCard()
//        {
//        }

//        public EnhancementCard( string name, EnhancementTypeEnum type, int value, string uriPath, int actionID, int cardID ) :
//            base(name, CardType.None, value, PropertyType.None, PropertyType.None, uriPath, actionID, cardID)
//        {
//            this.EnhancementType = type;

//        }
//    }
//}
