using System;
using Sebez.XmlTools.TrxToPlaylist;

namespace Sebez.XmlTools
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start XmlTools");

            var trxPath = @"C:\Data\Temp\Dalkia\20211018\MergedTrx.trx";
            var playListPath = @"C:\Data\Temp\Dalkia\20211018\20211018.playlist";

            try
            {
                new Transformer().Start(new Config { TrxPath = trxPath, PlaylistPath = playListPath });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("Fin du XmlTools");
        }
    }
}
