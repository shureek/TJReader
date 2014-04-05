/*
 * Created by SharpDevelop.
 * User: Kuzin.A
 * Date: 02.04.2014
 * Time: 15:59
 * 
 */

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Management.Automation;
using System.Diagnostics;

namespace TJLib
{
	public sealed class TJReader
	{
		readonly Lexer lexer = new Lexer();
		
		public DateTime FileDate { get; set; }
		public string ProcessName { get; set; }
		public int ProcessID { get; set; }
		public Encoding Encoding { get; set; }
		
		public event EventHandler<ErrorEventArgs> ErrorOccured;
		
		static readonly Regex reTime = new Regex("^(?<Minute>\\d{2}):(?<Second>\\d{2}).(?<Fraction>\\d+)-(?<Duration>-?\\d+).*$", RegexOptions.Compiled);
		
		public TJReader()
		{
			this.Encoding = System.Text.Encoding.UTF8;
		}
		
		public void Open(string filename)
		{
			lexer.Open(filename, this.Encoding);
		}
		
		public void Open(System.IO.Stream stream)
		{
			lexer.Open(stream, this.Encoding);
		}
		
		public void Close()
		{
			lexer.Close();
		}
		
		void OnError(Exception error)
		{
			var handler = ErrorOccured;
			if (handler != null)
				handler(this, new ErrorEventArgs(error));
		}
		
		public PSObject ReadRecord()
		{
			PSObject obj = null;
			ParserState step = ParserState.Begin;
			string propertyName = null;
			
			do
			{
				long line = lexer.LineNumber;
				int linePos = lexer.LineOffset;
				string lexem = lexer.ReadLexem();
				System.Diagnostics.Debug.WriteLine(String.Format("Parser: {0}, \"{1}\"", step, lexem));
				
				try
				{
					if (lexem == null)
					{
						if (step != ParserState.Begin)
							throw new ParserException("Неожиданный конец файла", step, line, linePos);
						step = ParserState.End;
					}
					
					switch(step)
					{
						case ParserState.Begin:
							{
								obj = new PSObject();
								obj.TypeNames.Insert(0, "TJRecord");
								obj.Properties.Add(new PSNoteProperty("ProcessName", ProcessName));
								obj.Properties.Add(new PSNoteProperty("ProcessID", ProcessID));
								
								// Это начало, сейчас будет время и длительность
								var match = reTime.Match(lexem);
								if (!match.Success)
									throw new ParserException("Неверный формат строки. Ожидается строка в формате \"MM:SS.FFFFFF-D\"", step, line, linePos);
								long minute = Int64.Parse(match.Groups["Minute"].Value);
								long second = Int64.Parse(match.Groups["Second"].Value);
								string fractionStr = match.Groups["Fraction"].Value;
								if (fractionStr.Length < 7)
									fractionStr = (fractionStr + "0000000").Substring(0, 7);
								long fraction = Int64.Parse(fractionStr);
								long timeTicks = minute * TimeSpan.TicksPerMinute + second * TimeSpan.TicksPerSecond + fraction;
								obj.Properties.Add(new PSNoteProperty("Date", FileDate.AddTicks(timeTicks)));
								
								long duration = Int64.Parse(match.Groups["Duration"].Value);
								if (duration < 0)
									// Переполнение Int32, такое бывает
									duration += (long)UInt32.MaxValue + 1;
								obj.Properties.Add(new PSNoteProperty("Duration", new TimeSpan(duration * 1000)));
								step++;
								break;
							}
						case ParserState.EventType:
							{
								obj.Properties.Add(new PSNoteProperty("EventType", lexem));
								step++;
								break;
							}
						case ParserState.Level:
							{
								obj.Properties.Add(new PSNoteProperty("Level", Int32.Parse(lexem)));
								step++;
								break;
							}
						case ParserState.PropertyName:
							{
								propertyName = lexem;
								step++;
								break;
							}
						case ParserState.PropertyValue:
							{
								Debug.Assert(!String.IsNullOrWhiteSpace(propertyName), "Пустое имя свойства");
								
								string propertyValue = lexem;
								// Если есть кавычки, уберем их
								if (propertyValue.Length >= 2 && (propertyValue[0] == '\'' || propertyValue[0] == '"') && propertyValue[propertyValue.Length - 1] == propertyValue[0])
									propertyValue = propertyValue.Substring(1, propertyValue.Length - 2);
								
								var property = obj.Properties[propertyName];
								if (property != null)
								{
									// Такое свойство уже есть. Сделаем в нем коллекцию
									var currentCollection = property.Value as object[];
									int valueCount = currentCollection != null ? currentCollection.Length : 1;
									object[] collection = new object[valueCount + 1];
									if (currentCollection != null)
										currentCollection.CopyTo(collection, 0);
									else
										collection[0] = property.Value;
									collection[valueCount] = propertyValue;
								}
								else
									obj.Properties.Add(new PSNoteProperty(propertyName, propertyValue));
								step++;
								break;
							}
						case ParserState.BefereEventType:
						case ParserState.BeforeLevel:
						case ParserState.BeforeProperty:
							{
								if (lexem == ",")
									step++;
								else
									throw new ParserException("Ожидается \",\"", step, line, linePos);
								break;
							}
						case ParserState.EqualSign:
							{
								if (lexem == "=")
									step++;
								else
									throw new ParserException("Ожидается \"=\"", step, line, linePos);
								break;
							}
						case ParserState.AfterProperty:
							{
								if (lexem == ",")
									step = ParserState.PropertyName;
								else if (lexem == Environment.NewLine || lexem == null)
									step = ParserState.End;
								else
									throw new ParserException("Ожидается \",\" или конец строки", step, line, linePos);
								break;
							}
					} // switch
				} // try
				catch (Exception ex)
				{
					ParserException error = null;
					if (ex is ParserException)
						error = (ParserException)ex;
					else if (ex is FormatException || ex is OverflowException)
						error = new ParserException(ex.Message, step, line, linePos, ex);
					else
						// Какая-то неизвестная ошибка
						throw;
					OnError(error);
					if (step < ParserState.PropertyName)
						step = ParserState.Error;
					else
						step = ParserState.Unknown;
				}
				
				if (step < 0)
				{
					if (lexem == null || lexem == Environment.NewLine)
						step = ParserState.End;
					else if (step == ParserState.Unknown && lexem == ",")
						step = ParserState.PropertyName;
				}
			}
			while (step != ParserState.End);
			return obj;
		}
	}
}