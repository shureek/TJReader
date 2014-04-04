/*
 * Created by SharpDevelop.
 * User: Alexander Kuzin
 * Date: 29.03.2014
 * Time: 14:35
 * 
 */

using System;
using System.IO;
using System.Text;

namespace TJLib
{
	sealed class Lexer
	{
		readonly char[] charBuffer = new char[2048];
		int charBufferOffset;
		int charBufferLength;
		const string quoteChars = "\"'";
		const string separators = ",=\n"; 
		
		StringBuilder sb = null;
		StreamReader reader = null;
		
		public long LineNumber { get; private set; }
		public long CharPosition { get; private set; }
		public int LineOffset { get; private set; }
		
		public Lexer()
		{ }
		
		public void Open(string fileName, Encoding encoding = null)
		{
			var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 32);
			Open(stream, encoding);
		}
		
		public void Open(Stream stream, Encoding encoding = null)
		{
			if (encoding != null)
				reader = new StreamReader(stream, encoding);
			else
				reader = new StreamReader(stream, true);
			Init();
		}
		
		public void Close()
		{
			if (reader != null)
			{
				reader.Close();
				reader = null;
			}
		}
		
		void Init()
		{
			charBufferOffset = 0;
			charBufferLength = 0;
			CharPosition = 0;
			LineNumber = 1;
			LineOffset = 0;
		}
		
		public string ReadLexem()
		{
			int index = 0;
			bool isString = false;
			char quote = (char)0;
			while (true)
			{
				if (index == charBufferLength)
				{
					if (index > 0)
					{
						// Мы уже кое-что прочитали, сохраним это
						if (sb != null)
							sb.Append(charBuffer, charBufferOffset, index);
						else
							sb = new StringBuilder(new String(charBuffer, charBufferOffset, index));
						index = 0;
					}
					
					charBufferOffset = 0;
					charBufferLength = reader.ReadBlock(charBuffer, charBufferOffset, charBuffer.Length);
					if (charBufferLength == 0)
					{
						// Если что-то у нас уже было прочитано, то возвращаем
						if (sb != null)
							return sb.ToString();
						else
							// Если вернули null, то закончался поток
							return null;
					}
				}
				
				char ch = charBuffer[charBufferOffset + index];
				if (ch == '\n')
				{
					LineNumber++;
					LineOffset++;
				}
				
				if (isString)
				{
					// Если мы в строке, то ждем окончания строки
					if (ch == quote)
						isString = false;
				}
				else if (quoteChars.IndexOf(ch) >= 0)
				{
					isString = true;
					quote = ch;
				}
				else if (separators.IndexOf(ch) >= 0)
				{
					string result;
					if (index > 0)
					{
						// Конец элемента
						if (sb != null)
						{
							sb.Append(charBuffer, charBufferOffset, index);
							result = sb.ToString();
							sb = null;
						}
						else
							result = new String(charBuffer, charBufferOffset, index);
						result = result.Trim();
					}
					else
					{
						// Это разделитель
						result = ch.ToString(); 
					}
					
					charBufferOffset += result.Length;
					charBufferLength -= result.Length;
					CharPosition += result.Length;
					LineOffset += result.Length;
					
					return result;
				}
				
				index++;
			}
		}
	}
}