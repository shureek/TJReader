/*
 * Created by SharpDevelop.
 * User: Kuzin.A
 * Date: 04.04.2014
 * Time: 12:56
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Management.Automation;

namespace ConsoleNS
{
	class Program
	{
		public static void Main(string[] args)
		{
			string filename = args[0];
			TJLib.TJReader reader = new TJLib.TJReader();
			reader.Open(filename);
			reader.ErrorOccured += HandleErrorOccured;
			PSObject record;
			do
			{
				record = reader.ReadRecord();
				Console.WriteLine("{0}", record);
			}
			while (record != null);
			
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}

		static void HandleErrorOccured(object sender, TJLib.ErrorEventArgs e)
		{
			var defaultColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Error: {0}", e.Error);
			Console.ForegroundColor = defaultColor;
		}
	}
}