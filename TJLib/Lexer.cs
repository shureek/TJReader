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
using System.Diagnostics;

namespace TJLib
{
	public sealed class Lexer
	{
        const int BufferSize = 2048;

		readonly char[] buffer = new char[BufferSize];
		const string quoteChars = "'\"";
		const string operatorChars = ",=";
		const string newLineChars = "\r\n";
		
		/// <summary>
		/// Начало информации в буфере.
		/// </summary>
		int bufferOffset;
		
		/// <summary>
		/// Остаточное кол-во символов в буфере.
		/// </summary>
		int bufferLength;
		
		StringBuilder sb = new StringBuilder(BufferSize);
		StreamReader reader = null;
		
		/// <summary>
		/// Номер текущей строки в файле.
		/// </summary>
		public long LineNumber { get; private set; }
		/// <summary>
		/// Индекс текущего символа в файле.
		/// </summary>
		long charPosition;
		public long CharPosition { get { return charPosition + bufferOffset; } }
		/// <summary>
		/// Индекс текущего символа в строке.
		/// </summary>
		public int LineOffset { get; private set; }
		
		public Lexer()
		{ }
		
		/// <summary>
		/// Открывает указанный файл.
		/// </summary>
		/// <param name="fileName">Имя файла.</param>
		/// <param name="encoding">Кодировка.</param>
		public void Open(string fileName, Encoding encoding = null)
		{
			var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 32);
			Open(stream, encoding);
		}
		
		/// <summary>
		/// Открывает указанный поток.
		/// </summary>
		/// <param name="stream">Поток.</param>
		/// <param name="encoding">Кодировка.</param>
		public void Open(Stream stream, Encoding encoding = null)
		{
			if (encoding != null)
				reader = new StreamReader(stream, encoding);
			else
				reader = new StreamReader(stream, true);
			
			bufferOffset = 0;
			bufferLength = 0;
			charPosition = 0;
			LineNumber = 1;
			LineOffset = 1;
		}
		
		/// <summary>
		/// Закрывает открытый файл или поток.
		/// </summary>
		public void Close()
		{
			if (reader != null)
			{
				reader.Close();
				reader = null;
			}
		}
		
		bool ReadBuffer()
		{
			charPosition += bufferOffset;
			bufferOffset = 0;
			bufferLength = reader.ReadBlock(buffer, bufferOffset, buffer.Length);
			return bufferLength > 0;
		}
		
		/// <summary>
		/// Читает очередную лексему из потока.
		/// </summary>
		/// <returns></returns>
		public string ReadLexem()
		{
			sb.Length = 0;
			string lexem = null;
			int index = 0;
			char quote = (char)0;
			LexemType lexemType = LexemType.None;
			do
			{
				if (bufferOffset + index == bufferLength)
				{
					if (index > 0)
					{
						sb.Append(buffer, bufferOffset, index);
						bufferOffset += index;
						LineOffset += index;
						index = 0;
					}
					if (!ReadBuffer())
						// Поток закончился. Возвращаем что есть или null
						return sb.Length > 0 ? sb.ToString() : null;
				}
				
				char ch = buffer[bufferOffset + index];
				
				switch(lexemType)
				{
					case LexemType.None:
						{
							//Debug.Assert(index == 0, "lexemType == None, index != 0)");
							if (newLineChars.IndexOf(ch) >= 0)
							{
								LineNumber++;
								LineOffset = 1;
								lexem = Environment.NewLine;
								bufferOffset++;
								// Если следующий символ парный, то пропустим его
								if ((bufferOffset < bufferLength || ReadBuffer()) && ((buffer[bufferOffset] == '\r' || buffer[bufferOffset] == '\n') && buffer[bufferOffset] != ch))
									bufferOffset++;
							}
							else if (operatorChars.IndexOf(ch) >= 0)
							{
								lexem = ch.ToString();
								bufferOffset++;
								LineOffset++;
							}
							else if (quoteChars.IndexOf(ch) >= 0)
							{
								lexemType = LexemType.String;
								quote = ch;
								// Не будем включать кавычки
								bufferOffset++;
								LineOffset++;
								index--;
							}
							else if (ch == ' ')
							{
								// Просто пропускаем
								bufferOffset++;
								LineOffset++;
								index--;
							}
							else
								lexemType = LexemType.Word;
							break;
						}
					case LexemType.String:
						{
							if (newLineChars.IndexOf(ch) >= 0)
							{
								LineNumber++;
								LineOffset = 1;
								sb.Append(buffer, bufferOffset, index);
								sb.Append(Environment.NewLine);
								bufferOffset += index + 1;
								if ((bufferOffset < bufferLength || ReadBuffer()) && ((buffer[bufferOffset] == '\r' || buffer[bufferOffset] == '\n') && buffer[bufferOffset] != ch))
									bufferOffset++;
                                index = -1;
							}
							else if (ch == quote)
							{
                                // Если следующий символ такой же, то это просто кавычка внутри строки
                                if (bufferOffset + index + 1 == bufferLength)
                                {
                                    // Если мы в конце буфера, то прочитаем следующий кусок
                                    sb.Append(buffer, bufferOffset, index);
                                    ReadBuffer();
                                    index = -1;
                                }

                                if (bufferLength > 0 && buffer[bufferOffset + index + 1] == quote)
                                {
                                    // Это кавычка внутри строки. Запишем с строку одну кавычку, вторую пропустим и будем читать дальше
                                    sb.Append(buffer, bufferOffset, index + 1);
                                    bufferOffset += index + 2;
                                    LineOffset += index + 2;
                                    index = -1;
                                }
                                else
                                {
                                    if (sb.Length > 0)
                                    {
                                        if (index > 0)
                                            sb.Append(buffer, bufferOffset, index);
                                        lexem = sb.ToString();
                                        sb.Length = 0;
                                    }
                                    else
                                        lexem = new String(buffer, bufferOffset, index);
                                    bufferOffset += index + 1;
                                    LineOffset += index + 1;
                                    index = -1;
                                }
							}
							break;
						}
					case LexemType.Word:
						{
							if (newLineChars.IndexOf(ch) >= 0 || operatorChars.IndexOf(ch) >= 0)
							{
								if (sb.Length > 0)
								{
									sb.Append(buffer, bufferOffset, index);
									lexem = sb.ToString();
									sb.Length = 0;
								}
								else
									lexem = new String(buffer, bufferOffset, index);
								bufferOffset += index;
								LineOffset += index;
								index = -1;
							}
							break;
						}
				}
				
				index++;
			}
			while(lexem == null);
			
			return lexem;
		}
	}
}