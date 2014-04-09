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
			Console.WriteLine("Press any key to begin");
			Console.ReadKey(true);
			string filename = args[0];
			ReadRecords(filename);
			//ReadLexems(filename);
			
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
		
		static void ReadLexems(string filename)
		{
			TJLib.Lexer lexer = new TJLib.Lexer();
			lexer.Open(filename);
			while(true)
			{
				long line = lexer.LineNumber;
				int pos = lexer.LineOffset;
				string lexem = lexer.ReadLexem();
				if (lexem == null)
					break;
				
				int length = lexem.Length;
				if (lexem == Environment.NewLine)
					lexem = "NewLine";
				else
					lexem = "\"" + lexem + "\""; 
				Console.WriteLine("{0},{1}({2}): {3}", line, pos, length, lexem);
			}
		}
		
		static void ReadRecords(string filename)
		{
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