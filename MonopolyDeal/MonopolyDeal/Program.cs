using System;

namespace MonopolyDeal
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (MonopolyDeal game = new MonopolyDeal())
            {
                game.Run();
            }
        }
    }
#endif
}

